using backend.Storage;
using System.Net.WebSockets;
using Microsoft.AspNetCore.Mvc;
using backend.Battle;
using shared;
using Google.Protobuf;

namespace backend.Controllers;

public class WebSocketController : ControllerBase {
    private readonly ILogger _logger; // Dependency injection reference https://learn.microsoft.com/en-us/aspnet/core/fundamentals/logging/?view=aspnetcore-7.0#create-logs

    private readonly IAuthTokenCache _tokenCache;
    private readonly IRoomManager _roomManager;

    public WebSocketController(ILogger<WebSocketController> logger, IAuthTokenCache tokenCache, IRoomManager roomManager) {
        _logger = logger;
        _tokenCache = tokenCache;
        _roomManager = roomManager;
    }

    [HttpGet] // Adding this Attribute just for Swagger recognition :)
    [Route("/Ws")]
    public async Task Get([FromQuery] string authToken, [FromQuery] int playerId, [FromQuery] int speciesId) {
        if (HttpContext.WebSockets.IsWebSocketRequest) {
            _logger.LogInformation("Got a websocket connection request#1 [ authToken={0}, playerId={1} ]", authToken, playerId);
            if (_tokenCache.ValidateToken(authToken, playerId)) {
                _logger.LogInformation("Got a websocket connection request#2 Validated successfully [ authToken={0}, playerId={1}, speciesId={2} ]", authToken, playerId, speciesId);
                using (var session = await HttpContext.WebSockets.AcceptWebSocketAsync()) {
                    await HandleNewPlayerPrimarySession(session, playerId, speciesId);
                }
            } else {
                _logger.LogWarning("Got a websocket connection request#2 Failed validation [ authToken={0}, playerId={1} ]", authToken, playerId);
                HttpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
            }
        } else {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }

    private async Task HandleNewPlayerPrimarySession(WebSocket session, int playerId, int speciesId) {

        using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource()) {

            CancellationToken cancellationToken = cancellationTokenSource.Token;
            WebSocketCloseStatus closeCode = WebSocketCloseStatus.Empty;
            string? closeReason = null;

            int addPlayerToRoomResult = ErrCode.UnknownError;
            Player player = new Player(new CharacterDownsync());
            int roomId = -1;
            Room? room = null;
            try {
                room = _roomManager.Pop();
                if (null == room) {
                    _logger.LogWarning("No available room [ playerId={0} ]", playerId);
                    return;
                }
                roomId = room.id;
                addPlayerToRoomResult = room.AddPlayerIfPossible(player, playerId, speciesId, session, cancellationTokenSource, cancellationToken);
                if (ErrCode.Ok != addPlayerToRoomResult) {
                    _logger.LogWarning("Failed to add player to room [ roomId={0}, playerId={1}, result={2} ]", room.id, playerId, addPlayerToRoomResult);
                    _roomManager.Push(room.calRoomScore(), room);

					var errWsResp = new WsResp {
						Ret = addPlayerToRoomResult
					};

					await session.SendAsync(new ArraySegment<byte>(errWsResp.ToByteArray()), WebSocketMessageType.Binary, true, cancellationToken);

                    return;
                }

                _logger.LogInformation("Added player to room [ roomId={0}, playerId={1} ]", room.id, playerId);
                long nowRoomState = Interlocked.Read(ref room.state);
                if (shared.Battle.ROOM_STATE_WAITING == nowRoomState && !room.IsFull()) {
                    _logger.LogInformation("Putting room back to manager after player added [ roomId={0}, playerId={1}, roomState={2} ]", room.id, playerId, nowRoomState);
                    _roomManager.Push(room.calRoomScore(), room);
                    // Otherwise would put back after becoming ROOM_STATE_IDLE again   
                } else {
                    _logger.LogWarning("NOT putting room back to manager after player added [ roomId={0}, playerId={1}, roomState={2} ]", room.id, playerId, nowRoomState);
                }

                var bciFrame = new BattleColliderInfo {
                    StageName = room.stageName,

                    BoundRoomId = room.id,
                    BattleDurationFrames = room.battleDurationFrames,
                    InputFrameUpsyncDelayTolerance = room.inputFrameUpsyncDelayTolerance,
                    MaxChasingRenderFramesPerUpdate = room.maxChasingRenderFramesPerUpdate,

                    RenderBufferSize = room.GetRenderCacheSize(),
                    BoundRoomCapacity = room.capacity,
                    PreallocNpcCapacity = room.preallocNpcCapacity,
                    PreallocBulletCapacity = room.preallocBulletCapacity,
                    FrameLogEnabled = room.frameLogEnabled,
                };

                if (null != room.battleUdpTunnelAddr) {
                    bciFrame.BattleUdpTunnel = new PeerUdpAddr {
                        Port = room.battleUdpTunnelAddr.Port,
                        AuthKey = player.BattleUdpTunnelAuthKey
                    };
                };

                var initWsResp = new WsResp {
                    Ret = ErrCode.Ok,
                    Act = shared.Battle.DOWNSYNC_MSG_ACT_BATTLE_COLLIDER_INFO,
                    BciFrame = bciFrame,
                    PeerJoinIndex = player.CharacterDownsync.JoinIndex
                };

                if (null != room.peerUdpAddrList) {
                    // Make sure that the added player can send a holepuncher to the backend `battleUdpTunnelAddr`, thus trigger "room.broadcastPeerUdpAddrList" for all players in the same battle
                    initWsResp.PeerUdpAddrList.AddRange(room.peerUdpAddrList); 
                } 

                _logger.LogInformation("Sending bciFrame for [ roomId={0}, playerId={1} ]: {2}", room.id, playerId, initWsResp);
                await session.SendAsync(new ArraySegment<byte>(initWsResp.ToByteArray()), WebSocketMessageType.Binary, true, cancellationToken);
                
                var recvBuffer = new byte[shared.Battle.BACKEND_WS_RECV_BYTELENGTH];
                var arrSegBytes = new ArraySegment<byte>(recvBuffer); 
                while (!cancellationToken.IsCancellationRequested) {
                    try {
                        var receiveResult = await session.ReceiveAsync(arrSegBytes, cancellationToken);

                        if (receiveResult.CloseStatus.HasValue) {
                            _logger.LogWarning("Player proactively requests close of session for [ roomId={0}, playerId={1} ]", room.id, playerId);
                            closeCode = receiveResult.CloseStatus.Value;
                            closeReason = receiveResult.CloseStatusDescription;
                            break;
                        }

                        WsReq pReq = WsReq.Parser.ParseFrom(recvBuffer, 0, receiveResult.Count);
                        switch (pReq.Act) {
                            case shared.Battle.UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK:

                                var res1 = await room.OnPlayerBattleColliderAcked(playerId, pReq.SelfParsedRdf, pReq.SerializedBarrierPolygons, pReq.SerializedStaticPatrolCues, pReq.SerializedCompletelyStaticTraps, pReq.SerializedStaticTriggers, pReq.SerializedTrapLocalIdToColliderAttrs, pReq.SerializedTriggerTrackingIdToTrapLocalId, pReq.SpaceOffsetX, pReq.SpaceOffsetY, pReq.BattleDurationSeconds);
                                if (!res1) {
                                    _logger.LogWarning("About to cancel session for [ roomId={0}, playerId={1} ] due to failure of OnPlayerBattleColliderAcked", room.id, playerId);
                                    if (!cancellationToken.IsCancellationRequested) {
                                        cancellationTokenSource.Cancel();
                                    }
                                } else {
                                    // [OPTIONAL] TODO: Popup this specific room from RoomManager, then re-push it with the updated score
                                }
                                break;
                            case shared.Battle.UPSYNC_MSG_ACT_PLAYER_CMD:
                                // _logger.LogInformation("Received UPSYNC_MSG_ACT_PLAYER_CMD [ roomId={0}, playerId={1}, messageLength={2} ]", room.id, playerId, receiveResult.Count);
                                room.OnBattleCmdReceived(pReq, playerId, false);
                                break;
                            default:
                                break;
                        }
                    } catch (OperationCanceledException ocEx) {
                        _logger.LogWarning("Session is cancelled for [ roomId={0}, playerId={1}, ocEx={2} ]", room.id, playerId, ocEx.Message);
                        break;
                    } catch (Exception ex) {
                        _logger.LogError(ex, "Session got an exception for [ roomId={0}, playerId={1}]", room.id, playerId);
                        break;
                    }
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Session got an exception");
            } finally {
                _logger.LogInformation("Ending HandleNewPlayerPrimarySession in state={0} for [ roomId={1}, playerId={2}]", session.State, roomId, playerId);
                if (ErrCode.Ok == addPlayerToRoomResult && null != room) {
                    room.OnPlayerDisconnected(playerId);
                }
                // [WARNING] Checking session.State here is possibly not thread-safe, but it's not a big concern for now
                if (WebSocketState.Aborted != session.State && WebSocketState.Closed != session.State) {
                    _logger.LogWarning("About to explicitly close websocket session in state={0} for [ roomId={1}, playerId={2}]", session.State, roomId, playerId);
                    await session.CloseAsync(
                    closeCode,
                    closeReason,
                    CancellationToken.None); // We won't cancel the closing of a websocket session
                }
            }
        }
    }
}
