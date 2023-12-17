using UnityEngine;
using System;
using shared;
using static shared.Battle;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.Collections;

public class OnlineMapController : AbstractMapController {
    Task wsTask, udpTask;
    CancellationTokenSource wsCancellationTokenSource;
    CancellationToken wsCancellationToken;
    int inputFrameUpsyncDelayTolerance;
    WsResp wsRespHolder;
    public NetworkDoctorInfo networkInfoPanel;
    int clientAuthKey;
    bool shouldLockStep = false;
    bool localTimerEnded = false;
    bool lastRenderFrameDerivedFromAllConfirmedInputFrameDownsync = false;
    int timeoutMillisAwaitingLastAllConfirmedInputFrameDownsync = DEFAULT_TIMEOUT_FOR_LAST_ALL_CONFIRMED_IFD;

    public PlayerWaitingPanel playerWaitingPanel;

    void pollAndHandleWsRecvBuffer() {
        while (WsSessionManager.Instance.recvBuffer.TryDequeue(out wsRespHolder)) {
            //Debug.Log(String.Format("Handling wsResp in main thread: {0}", wsRespHolder));
            if (ErrCode.Ok != wsRespHolder.Ret) {
                var msg = String.Format("Received ws error {0}", wsRespHolder);
                popupErrStackPanel(msg);
                onWsSessionClosed();
                break;
            }
            switch (wsRespHolder.Act) {
                case DOWNSYNC_MSG_WS_OPEN:
                    onWsSessionOpen();
                    break;
                case DOWNSYNC_MSG_WS_CLOSED:
                    onWsSessionClosed();
                    break;
                case DOWNSYNC_MSG_ACT_BATTLE_COLLIDER_INFO:
                    Debug.Log(String.Format("Handling DOWNSYNC_MSG_ACT_BATTLE_COLLIDER_INFO in main thread"));
                    battleDurationFrames = (int)wsRespHolder.BciFrame.BattleDurationFrames;
                    inputFrameUpsyncDelayTolerance = wsRespHolder.BciFrame.InputFrameUpsyncDelayTolerance;
                    selfPlayerInfo.Id = WsSessionManager.Instance.GetPlayerId();
                    roomCapacity = wsRespHolder.BciFrame.BoundRoomCapacity;
                    frameLogEnabled = wsRespHolder.BciFrame.FrameLogEnabled;
                    clientAuthKey = wsRespHolder.BciFrame.BattleUdpTunnel.AuthKey;
                    selfPlayerInfo.JoinIndex = wsRespHolder.PeerJoinIndex;
                    preallocateHolders();
                    playerWaitingPanel.InitPlayerSlots(roomCapacity);
                    resetCurrentMatch("TwoStepStageDeep");
                    preallocateVfxNodes();
                    preallocateSfxNodes();
                    preallocateNpcNodes();

                    var tempSpeciesIdList = new int[roomCapacity];
                    for (int i = 0; i < roomCapacity; i++) {
                        tempSpeciesIdList[i] = SPECIES_NONE_CH;
                    }
                    tempSpeciesIdList[selfPlayerInfo.JoinIndex - 1] = WsSessionManager.Instance.GetSpeciesId();
                    var (thatStartRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerTrackingIdToTrapLocalId) = mockStartRdf(tempSpeciesIdList);
                    
                    renderBuffer.Put(thatStartRdf);
                    
                    refreshColliders(thatStartRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerTrackingIdToTrapLocalId, spaceOffsetX, spaceOffsetY, ref collisionSys, ref maxTouchingCellsCnt, ref dynamicRectangleColliders, ref staticColliders, ref collisionHolder, ref completelyStaticTrapColliders, ref trapLocalIdToColliderAttrs, ref triggerTrackingIdToTrapLocalId);

                    var reqData = new WsReq {
                        PlayerId = selfPlayerInfo.Id,
                        Act = UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK,
                        JoinIndex = selfPlayerInfo.JoinIndex,
                        SelfParsedRdf = thatStartRdf,
                        SerializedTrapLocalIdToColliderAttrs = serializedTrapLocalIdToColliderAttrs,
                        SerializedTriggerTrackingIdToTrapLocalId = serializedTriggerTrackingIdToTrapLocalId,
                        SpaceOffsetX = spaceOffsetX,
                        SpaceOffsetY = spaceOffsetY,
                    };

                    reqData.SerializedBarrierPolygons.AddRange(serializedBarrierPolygons);
                    reqData.SerializedStaticPatrolCues.AddRange(serializedStaticPatrolCues);
                    reqData.SerializedCompletelyStaticTraps.AddRange(serializedCompletelyStaticTraps);
                    reqData.SerializedStaticTriggers.AddRange(serializedStaticTriggers);

                    WsSessionManager.Instance.senderBuffer.Enqueue(reqData);
                    Debug.Log("Sent UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK.");

                    var initialPeerUdpAddrList = wsRespHolder.Rdf.PeerUdpAddrList;
                    udpTask = Task.Run(async () => {
                        var serverHolePuncher = new WsReq {
                            PlayerId = selfPlayerInfo.Id,
                            Act = UPSYNC_MSG_ACT_HOLEPUNCH_BACKEND_UDP_TUNNEL,
                            JoinIndex = selfPlayerInfo.JoinIndex,
                            AuthKey = clientAuthKey
                        };
                        var peerHolePuncher = new WsReq {
                            PlayerId = selfPlayerInfo.Id,
                            Act = UPSYNC_MSG_ACT_HOLEPUNCH_PEER_UDP_ADDR,
                            JoinIndex = selfPlayerInfo.JoinIndex,
                            AuthKey = clientAuthKey
                        };
                        await UdpSessionManager.Instance.OpenUdpSession(roomCapacity, selfPlayerInfo.JoinIndex, initialPeerUdpAddrList, serverHolePuncher, peerHolePuncher, wsCancellationToken);
                    });

                    break;
                case DOWNSYNC_MSG_ACT_PLAYER_ADDED_AND_ACKED:
                    playerWaitingPanel.OnParticipantChange(wsRespHolder.Rdf);
                    break;
                case DOWNSYNC_MSG_ACT_BATTLE_START:
                    Debug.Log("Handling DOWNSYNC_MSG_ACT_BATTLE_START in main thread.");
                    var (ok1, startRdf) = renderBuffer.GetByFrameId(DOWNSYNC_MSG_ACT_BATTLE_START);
                    readyGoPanel.playGoAnim();
                    bgmSource.Play();
                    onRoomDownsyncFrame(startRdf, null);
                    enableBattleInput(true);
                    break;
                case DOWNSYNC_MSG_ACT_BATTLE_STOPPED:
                    enableBattleInput(false);
                    StartCoroutine(delayToShowSettlementPanel());
                    // Reference https://docs.unity3d.com/ScriptReference/Application-persistentDataPath.html
                    if (frameLogEnabled) {
                        wrapUpFrameLogs(renderBuffer, inputBuffer, rdfIdToActuallyUsedInput, true, pushbackFrameLogBuffer, Application.persistentDataPath, String.Format("p{0}.log", selfPlayerInfo.JoinIndex));
                    }
                    break;
                case DOWNSYNC_MSG_ACT_INPUT_BATCH:
                    // Debug.Log("Handling DOWNSYNC_MSG_ACT_INPUT_BATCH in main thread.");
                    onInputFrameDownsyncBatch(wsRespHolder.InputFrameDownsyncBatch);
                    break;
                case DOWNSYNC_MSG_ACT_PEER_UDP_ADDR:
                    var newPeerUdpAddrList = wsRespHolder.Rdf.PeerUdpAddrList;
                    Debug.Log(String.Format("Handling DOWNSYNC_MSG_ACT_PEER_UDP_ADDR in main thread, newPeerUdpAddrList: {0}", newPeerUdpAddrList));
                    UdpSessionManager.Instance.UpdatePeerAddr(roomCapacity, selfPlayerInfo.JoinIndex, newPeerUdpAddrList);
                    break;
                case DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START:
                    /*
                     [WARNING] Deliberately trying to START "PunchAllPeers" for every participant at roughly the same time. 
                    
                    In practice, I found a weird case where P2 starts holepunching P1 much earlier than the opposite direction (e.g. when P2 joins the room later, but gets the peer udp addr of P1 earlier upon DOWNSYNC_MSG_ACT_BATTLE_COLLIDER_INFO), the punching for both directions would fail if the firewall(of network provider) of P1 rejected & blacklisted the early holepunching packet from P2 for a short period (e.g. 1 minute).
                     */
                    var speciesIdList = new int[roomCapacity];
                    for (int i = 0; i < roomCapacity; i++) {
                        speciesIdList[i] = wsRespHolder.Rdf.PlayersArr[i].SpeciesId;
                    }
                    var (ok2, toPatchStartRdf) = renderBuffer.GetByFrameId(DOWNSYNC_MSG_ACT_BATTLE_START);
                    patchStartRdf(toPatchStartRdf, speciesIdList);
                    applyRoomDownsyncFrameDynamics(toPatchStartRdf, null);
                    cameraTrack(toPatchStartRdf, null);
                    var playerGameObj = playerGameObjs[selfPlayerInfo.JoinIndex - 1];
                    Debug.Log(String.Format("Battle ready to start, teleport camera to selfPlayer dst={0}", playerGameObj.transform.position));
                    readyGoPanel.playReadyAnim();

                    networkInfoPanel.gameObject.SetActive(true);
                    playerWaitingPanel.gameObject.SetActive(false);
                    UdpSessionManager.Instance.PunchBackendUdpTunnel();
                    UdpSessionManager.Instance.PunchAllPeers();
                    break;
                case DOWNSYNC_MSG_ACT_FORCED_RESYNC:
                  if (null == wsRespHolder.InputFrameDownsyncBatch || 0 >= wsRespHolder.InputFrameDownsyncBatch.Count) {
                    Debug.LogWarning(String.Format("Got empty inputFrameDownsyncBatch upon resync@localRenderFrameId={0}, @lastAllConfirmedInputFrameId={1}, @chaserRenderFrameId={2}, @inputBuffer:{3}", playerRdfId, lastAllConfirmedInputFrameId, chaserRenderFrameId, inputBuffer.toSimpleStat()));
                    return;
                  }
                  onRoomDownsyncFrame(wsRespHolder.Rdf, wsRespHolder.InputFrameDownsyncBatch);
                  break;
                default:
                    break;
            }
        }
    }

    void pollAndHandleUdpRecvBuffer() {
        WsReq wsReqHolder;
        while (UdpSessionManager.Instance.recvBuffer.TryDequeue(out wsReqHolder)) {
            // Debug.Log(String.Format("Handling udpSession wsReq in main thread: {0}", wsReqHolder));
            onPeerInputFrameUpsync(wsReqHolder.JoinIndex, wsReqHolder.InputFrameUpsyncBatch);
        }
    }

    public void onWaitingInterrupted() {
        Debug.Log("OnlineMapController.onWaitingInterrupted");
        cleanupNetworkSessions();
    }

    public override void onCharacterSelectGoAction(int speciesId) {
        characterSelectPanel.GoActionButton.gameObject.SetActive(false);
        Debug.Log(String.Format("Executing extra goAction with selectedSpeciesId={0}", speciesId));
        WsSessionManager.Instance.SetSpeciesId(speciesId);

        // [WARNING] Must avoid blocking MainThread. See "GOROUTINE_TO_ASYNC_TASK.md" for more information.
        Debug.LogWarning(String.Format("About to start ws session: thread id={0} a.k.a. the MainThread.", Thread.CurrentThread.ManagedThreadId));

        wsCancellationTokenSource = new CancellationTokenSource();
        wsCancellationToken = wsCancellationTokenSource.Token;
        wsTask = Task.Run(async () => {
            Debug.LogWarning(String.Format("About to start ws session within Task.Run(async lambda): thread id={0}.", Thread.CurrentThread.ManagedThreadId));

            await wsSessionTaskAsync();

            Debug.LogWarning(String.Format("Ends ws session within Task.Run(async lambda): thread id={0}.", Thread.CurrentThread.ManagedThreadId));

            if (null != udpTask) {
                UdpSessionManager.Instance.CloseUdpSession(); // Would effectively end "ReceiveAsync" if it's blocking "Receive" loop in udpTask.
            }
            // [WARNING] At the end of "wsSessionTaskAsync", we'll have a "DOWNSYNC_MSG_WS_CLOSED" message, thus triggering "onWsSessionClosed -> cleanupNetworkSessions" to clean up other network resources!
        });

        //wsTask = Task.Run(wsSessionActionAsync); // This doesn't make "await wsTask" synchronous in "cleanupNetworkSessions".

        //wsSessionActionAsync(); // [c] no immediate thread switch till AFTER THE FIRST AWAIT
        //_ = wsSessionTaskAsync(); // [d] no immediate thread switch till AFTER THE FIRST AWAIT

        Debug.LogWarning(String.Format("Started ws session: thread id={0} a.k.a. the MainThread.", Thread.CurrentThread.ManagedThreadId));
        characterSelectPanel.gameObject.SetActive(false);
    }

    public override void onCharacterAndLevelSelectGoAction(int speciesId, string levelName) {
        throw new NotImplementedException();
    }

    void Start() {
        Physics.autoSimulation = false;
        Physics2D.simulationMode = SimulationMode2D.Script;

        selfPlayerInfo = new CharacterDownsync();
        inputFrameUpsyncDelayTolerance = TERMINATING_INPUT_FRAME_ID;
        Application.targetFrameRate = 60;
        isOnlineMode = true;
        enableBattleInput(false);
    }

    public void onWsSessionOpen() {
        Debug.Log("Handling WsSession open in main thread.");
        playerWaitingPanel.gameObject.SetActive(true);
    }

    public void onWsSessionClosed() {
        Debug.Log("Handling WsSession closed in main thread.");
        // [WARNING] No need to show SettlementPanel in this case, but instead we should show something meaningful to the player if it'd be better for bug reporting.
        onBattleStopped();
        playerWaitingPanel.gameObject.SetActive(false);
        characterSelectPanel.gameObject.SetActive(true);
        characterSelectPanel.GoActionButton.gameObject.SetActive(true);
        cleanupNetworkSessions(); // Make sure that all resources are properly deallocated
    }

    private async Task wsSessionTaskAsync() {
        Debug.LogWarning(String.Format("In ws session TASK but before first await: thread id={0}.", Thread.CurrentThread.ManagedThreadId));
        string wsEndpoint = Env.Instance.getWsEndpoint();
        await WsSessionManager.Instance.ConnectWsAsync(wsEndpoint, wsCancellationToken, wsCancellationTokenSource);
        Debug.LogWarning(String.Format("In ws session TASK and after first await: thread id={0}.", Thread.CurrentThread.ManagedThreadId));
    }

    private async void wsSessionActionAsync() {
        Debug.LogWarning(String.Format("In ws session ACTION but before first await: thread id={0}.", Thread.CurrentThread.ManagedThreadId));
        string wsEndpoint = Env.Instance.getWsEndpoint();
        await WsSessionManager.Instance.ConnectWsAsync(wsEndpoint, wsCancellationToken, wsCancellationTokenSource);
        Debug.LogWarning(String.Format("In ws session ACTION and after first await: thread id={0}.", Thread.CurrentThread.ManagedThreadId));
    }

    protected override int chaseRolledbackRdfs() {
        int nextChaserRenderFrameId = base.chaseRolledbackRdfs();
        if (nextChaserRenderFrameId == playerRdfId && playerRdfId >= battleDurationFrames) {
            var (rdfAllConfirmed, _) = isRdfAllConfirmed(playerRdfId, inputBuffer, roomCapacity);
            if (rdfAllConfirmed) {
                lastRenderFrameDerivedFromAllConfirmedInputFrameDownsync = true;
            }
        }
        return nextChaserRenderFrameId;
    }

    // Update is called once per frame
    void Update() {
        try {
            pollAndHandleWsRecvBuffer();
            pollAndHandleUdpRecvBuffer();
            if (ROOM_STATE_IN_BATTLE != battleState) {
                return;
            }
            // [WARNING] Chasing should be executed regardless of whether or not "shouldLockStep" -- in fact it's even better to chase during "shouldLockStep"!
            chaseRolledbackRdfs();
            if (localTimerEnded) {
                if (!lastRenderFrameDerivedFromAllConfirmedInputFrameDownsync && 0 < timeoutMillisAwaitingLastAllConfirmedInputFrameDownsync) {
                    // TODO: Popup some GUI hint to tell the player that we're awaiting downsync only, as the local "playerRdfId" is monotonically increasing, there's no way to rewind and change any input from here!
                    timeoutMillisAwaitingLastAllConfirmedInputFrameDownsync -= 16; // hardcoded for now
                } else {
                    StartCoroutine(delayToShowSettlementPanel());
                }
                return;
            }
            if (shouldLockStep) {
                NetworkDoctor.Instance.LogLockedStepCnt();
                shouldLockStep = false;
                return; // An early return here only stops "inputFrameIdFront" from incrementing, "int[] lastIndividuallyConfirmedInputFrameId" would keep increasing by the "pollXxx" calls above. 
            }
            doUpdate();
            if (playerRdfId >= battleDurationFrames) {
                localTimerEnded = true;
            } else {
                readyGoPanel.setCountdown(playerRdfId, battleDurationFrames);
                var (tooFastOrNot, _, sendingFps, srvDownsyncFps, peerUpsyncFps, rollbackFrames, lockedStepsCnt, udpPunchedCnt) = NetworkDoctor.Instance.IsTooFast(roomCapacity, selfPlayerInfo.JoinIndex, lastIndividuallyConfirmedInputFrameId, renderFrameIdLagTolerance);
                shouldLockStep = tooFastOrNot;
                networkInfoPanel.SetValues(sendingFps, srvDownsyncFps, peerUpsyncFps, lockedStepsCnt, rollbackFrames, udpPunchedCnt);
            }
            //throw new NotImplementedException("Intended");
        } catch (Exception ex) {
            var msg = String.Format("Error during OnlineMap.Update {0}", ex);
            popupErrStackPanel(msg);
            // [WARNING] No need to show SettlementPanel in this case, but instead we should show something meaningful to the player if it'd be better for bug reporting.
            onBattleStopped();
        }
    }

    protected override bool shouldSendInputFrameUpsyncBatch(ulong prevSelfInput, ulong currSelfInput, int currInputFrameId) {
        /*
        For a 2-player-battle, this "shouldUpsyncForEarlyAllConfirmedOnBackend" can be omitted, however for more players in a same battle, to avoid a "long time non-moving player" jamming the downsync of other moving players, we should use this flag.

        When backend implements the "force confirmation" feature, we can have "false == shouldUpsyncForEarlyAllConfirmedOnBackend" all the time as well!
        */

        var shouldUpsyncForEarlyAllConfirmedOnBackend = (currInputFrameId - lastUpsyncInputFrameId >= inputFrameUpsyncDelayTolerance);
        return shouldUpsyncForEarlyAllConfirmedOnBackend || (prevSelfInput != currSelfInput);
    }

    protected override void sendInputFrameUpsyncBatch(int latestLocalInputFrameId) {
        // [WARNING] Why not just send the latest input? Because different player would have a different "latestLocalInputFrameId" of changing its last input, and that could make the server not recognizing any "all-confirmed inputFrame"!
        var inputFrameUpsyncBatch = new RepeatedField<InputFrameUpsync>();
        var batchInputFrameIdSt = lastUpsyncInputFrameId + 1;
        if (batchInputFrameIdSt < inputBuffer.StFrameId) {
            // Upon resync, "this.lastUpsyncInputFrameId" might not have been updated properly.
            batchInputFrameIdSt = inputBuffer.StFrameId;
        }

        var batchInputFrameIdEdClosed = latestLocalInputFrameId;
        if (batchInputFrameIdEdClosed >= inputBuffer.EdFrameId) {
            batchInputFrameIdEdClosed = inputBuffer.EdFrameId-1;
        }

        NetworkDoctor.Instance.LogInputFrameIdFront(latestLocalInputFrameId);
        NetworkDoctor.Instance.LogSending(batchInputFrameIdSt, latestLocalInputFrameId);

        for (var i = batchInputFrameIdSt; i <= batchInputFrameIdEdClosed; i++) {
            var (res1, inputFrameDownsync) = inputBuffer.GetByFrameId(i);
            if (false == res1 || null == inputFrameDownsync) {
                Debug.LogError(String.Format("sendInputFrameUpsyncBatch: recentInputCache is NOT having i={0}, at playerRdfId={1}, latestLocalInputFrameId={2}, inputBuffer:{3} ", i, playerRdfId, latestLocalInputFrameId, inputBuffer.toSimpleStat()));
            } else {
                var inputFrameUpsync = new InputFrameUpsync {
                    InputFrameId = i,
                    Encoded = inputFrameDownsync.InputList[selfPlayerInfo.JoinIndex - 1]
                };
                inputFrameUpsyncBatch.Add(inputFrameUpsync);
            }
        }

        var reqData = new WsReq {
            PlayerId = selfPlayerInfo.Id,
            Act = Battle.UPSYNC_MSG_ACT_PLAYER_CMD,
            JoinIndex = selfPlayerInfo.JoinIndex,
            AckingInputFrameId = lastAllConfirmedInputFrameId,
            AuthKey = clientAuthKey
        };
        reqData.InputFrameUpsyncBatch.AddRange(inputFrameUpsyncBatch);

        WsSessionManager.Instance.senderBuffer.Enqueue(reqData);
        UdpSessionManager.Instance.senderBuffer.Enqueue(reqData);
        lastUpsyncInputFrameId = latestLocalInputFrameId;
    }

    protected void onPeerInputFrameUpsync(int peerJoinIndex, RepeatedField<InputFrameUpsync> batch) {
        if (null == batch) {
            return;
        }
        if (null == inputBuffer) {
            return;
        }
        if (ROOM_STATE_IN_BATTLE != battleState) {
            return;
        }

        int effCnt = 0, batchCnt = batch.Count;
        int firstPredictedYetIncorrectInputFrameId = TERMINATING_INPUT_FRAME_ID;
        for (int k = 0; k < batchCnt; k++) {
            var inputFrameUpsync = batch[k];
            int inputFrameId = inputFrameUpsync.InputFrameId;
            ulong peerEncodedInput = inputFrameUpsync.Encoded;

            if (inputFrameId <= lastAllConfirmedInputFrameId) {
                // [WARNING] Don't reject it by "inputFrameId <= lastIndividuallyConfirmedInputFrameId[peerJoinIndex-1]", the arrival of UDP packets might not reserve their sending order!
                // Debug.Log(String.Format("Udp upsync inputFrameId={0} from peerJoinIndex={1} is ignored because it's already confirmed#1! lastAllConfirmedInputFrameId={2}", inputFrameId, peerJoinIndex, lastAllConfirmedInputFrameId));
                continue;
            }
            ulong peerJoinIndexMask = ((ulong)1 << (peerJoinIndex - 1));
            getOrPrefabInputFrameUpsync(inputFrameId, false, prefabbedInputListHolder); // Make sure that inputFrame exists locally
            var (res1, existingInputFrame) = inputBuffer.GetByFrameId(inputFrameId);
            if (!res1 || null == existingInputFrame) {
                throw new ArgumentNullException(String.Format("inputBuffer doesn't contain inputFrameId={0} after prefabbing! Now inputBuffer StFrameId={1}, EdFrameId={2}, Cnt/N={3}/{4}", inputFrameId, inputBuffer.StFrameId, inputBuffer.EdFrameId, inputBuffer.Cnt, inputBuffer.N));
            }
            ulong existingConfirmedList = existingInputFrame.ConfirmedList;
            if (0 < (existingConfirmedList & peerJoinIndexMask)) {
                // Debug.Log(String.Format("Udp upsync inputFrameId={0} from peerJoinIndex={1} is ignored because it's already confirmed#2! lastAllConfirmedInputFrameId={2}, existingInputFrame={3}", inputFrameId, peerJoinIndex, lastAllConfirmedInputFrameId, existingInputFrame));
                continue;
            }
            if (inputFrameId > lastIndividuallyConfirmedInputFrameId[peerJoinIndex - 1]) {
                lastIndividuallyConfirmedInputFrameId[peerJoinIndex - 1] = inputFrameId;
                lastIndividuallyConfirmedInputList[peerJoinIndex - 1] = peerEncodedInput;
            }
            effCnt += 1;

            bool isPeerEncodedInputUpdated = (existingInputFrame.InputList[peerJoinIndex - 1] != peerEncodedInput);
            existingInputFrame.InputList[peerJoinIndex - 1] = peerEncodedInput;

            if (
              TERMINATING_INPUT_FRAME_ID == firstPredictedYetIncorrectInputFrameId
              &&
              isPeerEncodedInputUpdated
            ) {
                firstPredictedYetIncorrectInputFrameId = inputFrameId;
            }
        }
        NetworkDoctor.Instance.LogPeerInputFrameUpsync(batch[0].InputFrameId, batch[batchCnt - 1].InputFrameId);
        /*
        [WARNING] 

        Deliberately NOT setting "existingInputFrame.ConfirmedList = (existingConfirmedList | peerJoinIndexMask)", thus NOT helping the move of "lastAllConfirmedInputFrameId" in "_markConfirmationIfApplicable()". 

        The edge case of concern here is "type#1 forceConfirmation". Assume that there is a battle among [P_u, P_v, P_x, P_y] where [P_x] is being an "ActiveSlowerTicker", then for [P_u, P_v, P_y] there might've been some "inputFrameUpsync"s received from [P_x] by UDP peer-to-peer transmission EARLIER THAN BUT CONFLICTING WITH the "accompaniedInputFrameDownsyncBatch of type#1 forceConfirmation" -- in such case the latter should be respected -- by "conflicting", the backend actually ignores those "inputFrameUpsync"s from [P_x] by "forceConfirmation".
    
        However, we should still call "_handleIncorrectlyRenderedPrediction(...)" here to break rollbacks into smaller chunks, because even if not used for "inputFrameDownsync.ConfirmedList", a "UDP inputFrameUpsync" is still more accurate than the locally predicted inputs.
        */
        _handleIncorrectlyRenderedPrediction(firstPredictedYetIncorrectInputFrameId, true);
    }

    protected override void resetCurrentMatch(string theme) {
        base.resetCurrentMatch(theme);

        // Reset lockstep
        shouldLockStep = false;
        localTimerEnded = false;
        lastRenderFrameDerivedFromAllConfirmedInputFrameDownsync = false;
        timeoutMillisAwaitingLastAllConfirmedInputFrameDownsync = DEFAULT_TIMEOUT_FOR_LAST_ALL_CONFIRMED_IFD;
        NetworkDoctor.Instance.Reset();
    }

    protected void cleanupNetworkSessions() {
        // [WARNING] This method is reentrant-safe!
        if (null != wsCancellationTokenSource) {
            try {
                if (!wsCancellationTokenSource.IsCancellationRequested) {
                    Debug.Log(String.Format("OnlineMapController.cleanupNetworkSessions, cancelling ws session"));
                    wsCancellationTokenSource.Cancel();
                } else {
                    Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, wsCancellationTokenSource is already cancelled!"));
                }
                wsCancellationTokenSource.Dispose();
            } catch (ObjectDisposedException ex) {
                Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, wsCancellationTokenSource is already disposed: {0}", ex));
            }
        }

        if (null != wsTask) {
            try {
                wsTask.Wait();
                wsTask.Dispose(); // frontend of this project targets ".NET Standard 2.1", thus calling "Task.Dispose()" explicitly, reference, reference https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.dispose?view=net-7.0
                Debug.Log(String.Format("OnlineMapController.cleanupNetworkSessions, wsTask disposed"));
            } catch (ObjectDisposedException ex) {
                Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, wsTask is already disposed: {0}", ex));
            }
        }

        if (null != udpTask) {
            try {
                udpTask.Wait();
                udpTask.Dispose(); // frontend of this project targets ".NET Standard 2.1", thus calling "Task.Dispose()" explicitly, reference, reference https://learn.microsoft.com/en-us/dotnet/api/system.threading.tasks.task.dispose?view=net-7.0
                Debug.Log(String.Format("OnlineMapController.cleanupNetworkSessions, udpTask disposed"));
            } catch (ObjectDisposedException ex) {
                Debug.LogWarning(String.Format("OnlineMapController.cleanupNetworkSessions, udpTask is already disposed: {0}", ex));
            }
        }
    }

    protected void OnDestroy() {
        Debug.LogWarning(String.Format("OnlineMapController.OnDestroy#1"));
        cleanupNetworkSessions();
        WsSessionManager.Instance.ClearCredentials();
        Debug.LogWarning(String.Format("OnlineMapController.OnDestroy#2"));
    }

    void OnApplicationQuit() {
        Debug.LogWarning(String.Format("OnlineMapController.OnApplicationQuit"));
    }

}
