﻿using UnityEngine;
using shared;
using System;
using System.Collections.Generic;
using SuperTiled2Unity;
using System.Collections;
using DG.Tweening;
using Google.Protobuf.Collections;
using static shared.Battle;
using static Story.StoryConstants;

public abstract class AbstractMapController : MonoBehaviour {
    protected int levelId = LEVEL_NONE;
    protected int justTriggeredStoryId = STORY_POINT_NONE;

    protected int[] justFulfilledEvtSubArr;
    protected int justFulfilledEvtSubCnt;
    protected int roomCapacity;
    protected int maxTouchingCellsCnt;
    protected int battleDurationFrames;

    protected int preallocNpcCapacity = DEFAULT_PREALLOC_NPC_CAPACITY;
    protected int preallocBulletCapacity = DEFAULT_PREALLOC_BULLET_CAPACITY;
    protected int preallocTrapCapacity = DEFAULT_PREALLOC_TRAP_CAPACITY;
    protected int preallocTriggerCapacity = DEFAULT_PREALLOC_TRIGGER_CAPACITY;
    protected int preallocEvtSubCapacity = DEFAULT_PREALLOC_EVTSUB_CAPACITY;
    protected int preallocPickableCapacity = DEFAULT_PREALLOC_PICKABLE_CAPACITY;

    protected int playerRdfId; // After battle started, always increments monotonically (even upon reconnection)
    protected int lastAllConfirmedInputFrameId;
    protected int lastUpsyncInputFrameId;
    protected int inputFrameUpsyncDelayTolerance;

    protected int chaserRenderFrameId; // at any moment, "chaserRenderFrameId <= playerRdfId", but "chaserRenderFrameId" would fluctuate according to "onInputFrameDownsyncBatch"
    protected int chaserRenderFrameIdLowerBound; // Upon force-resync, each peer receives a "ground truth RoomDownsyncFrame" from the backend, which serves as a "lower bound for the chaserRenderFrameId fluctuation"
    protected int smallChasingRenderFramesPerUpdate;
    protected int bigChasingRenderFramesPerUpdate;
    protected int renderBufferSize;
    public GameObject inplaceHpBarPrefab;
    public GameObject fireballPrefab;
    public GameObject pickablePrefab;
    public GameObject errStackLogPanelPrefab;
    public GameObject teamRibbonPrefab;
    public GameObject sfxSourcePrefab;
    public GameObject pixelVfxNodePrefab;
    protected GameObject errStackLogPanelObj;
    protected GameObject underlyingMap;
    public Canvas canvas;

    protected int[] lastIndividuallyConfirmedInputFrameId;
    protected ulong[] lastIndividuallyConfirmedInputList;
    protected CharacterDownsync selfPlayerInfo = null;
    protected FrameRingBuffer<RoomDownsyncFrame> renderBuffer = null;
    protected FrameRingBuffer<RdfPushbackFrameLog> pushbackFrameLogBuffer = null;
    protected FrameRingBuffer<InputFrameDownsync> inputBuffer = null;
    protected FrameRingBuffer<shared.Collider> residueCollided = null;

    protected ulong[] prefabbedInputListHolder;
    protected GameObject[] playerGameObjs;
    protected List<GameObject> dynamicTrapGameObjs;
    protected Dictionary<int, GameObject> triggerGameObjs; // They actually don't move
    protected Dictionary<int, int> joinIndexRemap;
    protected HashSet<int> justDeadNpcIndices;

    protected long battleState;
    protected int spaceOffsetX;
    protected int spaceOffsetY;
    protected float cameraCapMinX, cameraCapMaxX, cameraCapMinY, cameraCapMaxY;
    protected float effectivelyInfinitelyFar;

    protected RoomDownsyncFrame historyRdfHolder;
    protected shared.Collision collisionHolder;
    protected SatResult overlapResult, primaryOverlapResult;
    protected Dictionary<int, BattleResult> unconfirmedBattleResult;
    protected bool useOthersForcedDownsyncRenderFrameDict = false;
    protected Dictionary<int, RoomDownsyncFrame> othersForcedDownsyncRenderFrameDict;
    protected BattleResult confirmedBattleResult;
    protected Vector[] effPushbacks, softPushbacks;
    protected Vector[][] hardPushbackNormsArr;
    protected bool softPushbackEnabled;
    protected shared.Collider[] dynamicRectangleColliders;
    protected shared.Collider[] staticColliders;
    protected int staticCollidersCnt;

    protected InputFrameDecoded decodedInputHolder, prevDecodedInputHolder;
    protected CollisionSpace collisionSys;

    public GameObject linePrefab;
    protected KvPriorityQueue<string, FireballAnimController> cachedFireballs;
    protected KvPriorityQueue<string, PickableAnimController> cachedPickables;
    protected Vector3[] debugDrawPositionsHolder = new Vector3[4]; // Currently only rectangles are drawn
    protected KvPriorityQueue<string, DebugLine> cachedLineRenderers;
    protected Dictionary<int, GameObject> npcSpeciesPrefabDict;
    protected Dictionary<int, KvPriorityQueue<string, CharacterAnimController>> cachedNpcs;
    protected KvPriorityQueue<string, TeamRibbon> cachedTeamRibbons;
    protected KvPriorityQueue<string, InplaceHpBar> cachedHpBars;

    protected bool shouldDetectRealtimeRenderHistoryCorrection = false; // Not recommended to enable in production, it might have some memory performance impact.
    protected bool frameLogEnabled = false;
    protected Dictionary<int, InputFrameDownsync> rdfIdToActuallyUsedInput;
    protected Dictionary<int, List<TrapColliderAttr>> trapLocalIdToColliderAttrs;
    protected Dictionary<int, int> triggerTrackingIdToTrapLocalId;

    protected List<shared.Collider> completelyStaticTrapColliders;

    protected Dictionary<int, GameObject> vfxSpeciesPrefabDict;

    protected KvPriorityQueue<string, PixelVfxNodeController> cachedPixelVfxNodes;
    protected KvPriorityQueue<string, SFXSource> cachedSfxNodes;
    public AudioSource bgmSource;
    public abstract void onCharacterSelectGoAction(int speciesId);
    public abstract void onCharacterAndLevelSelectGoAction(int speciesId, string levelName);

    public abstract void OnSettingsClicked();

    protected bool debugDrawingAllocation = false;
    protected bool debugDrawingEnabled = false;

    protected ILoggerBridge _loggerBridge = new LoggerBridgeImpl();

    public SelfBattleHeading selfBattleHeading;

    public GameObject playerLightsPrefab;
    protected PlayerLights selfPlayerLights;

    protected Vector2 teamRibbonOffset = new Vector2(-10f, +6f);
    protected Vector2 inplaceHpBarOffset = new Vector2(-8f, +2f);
    protected float lineRendererZ = +5;
    protected float triggerZ = 0;
    protected float characterZ = 0;
    protected float flyingCharacterZ = -1;
    protected float inplaceHpBarZ = +10;
    protected float fireballZ = -5;
    protected float footstepAttenuationZ = 200.0f;

    private string MATERIAL_REF_THICKNESS = "_Thickness";

    protected KvPriorityQueue<string, TeamRibbon>.ValScore cachedTeamRibbonScore = (x) => x.score;
    protected KvPriorityQueue<string, InplaceHpBar>.ValScore cachedHpBarScore = (x) => x.score;
    protected KvPriorityQueue<string, CharacterAnimController>.ValScore cachedNpcScore = (x) => x.score;
    protected KvPriorityQueue<string, FireballAnimController>.ValScore cachedFireballScore = (x) => x.score;
    protected KvPriorityQueue<string, PickableAnimController>.ValScore cachedPickableScore = (x) => x.score;
    protected KvPriorityQueue<string, DebugLine>.ValScore cachedLineScore = (x) => x.score;
    protected KvPriorityQueue<string, SFXSource>.ValScore sfxNodeScore = (x) => x.score;
    protected KvPriorityQueue<string, PixelVfxNodeController>.ValScore pixelVfxNodeScore = (x) => x.score;

    public BattleInputManager iptmgr;

    protected int missionEvtSubId = MAGIC_EVTSUB_ID_NONE;
    protected bool isOnlineMode;
    protected int localExtraInputDelayFrames = 0;
    protected GameObject loadCharacterPrefab(CharacterConfig chConfig) {
        string path = String.Format("Prefabs/{0}", chConfig.SpeciesName);
        return Resources.Load(path) as GameObject;
    }

    protected GameObject loadTrapPrefab(TrapConfig trapConfig) {
        string path = String.Format("TrapPrefabs/{0}", trapConfig.SpeciesName);
        return Resources.Load(path) as GameObject;
    }

    protected GameObject loadTriggerPrefab(TriggerConfig triggerConfig) {
        string path = String.Format("TriggerPrefabs/{0}", triggerConfig.SpeciesName);
        return Resources.Load(path) as GameObject;
    }

    protected GameObject loadPickablePrefab(Pickable pickable) {
        return Resources.Load("Prefabs/Pickable") as GameObject;
    }

    public ReadyGo readyGoPanel;
    public SettlementPanel settlementPanel;

    protected Vector3 newPosHolder = new Vector3();
    protected Vector3 newTlPosHolder = new Vector3(), newTrPosHolder = new Vector3(), newBlPosHolder = new Vector3(), newBrPosHolder = new Vector3();

    protected void spawnPlayerNode(int joinIndex, int speciesId, float wx, float wy, int bulletTeamId) {
        var characterPrefab = loadCharacterPrefab(characters[speciesId]);
        GameObject newPlayerNode = Instantiate(characterPrefab, new Vector3(wx, wy, characterZ), Quaternion.identity, underlyingMap.transform);
        playerGameObjs[joinIndex - 1] = newPlayerNode;
        playerGameObjs[joinIndex - 1].GetComponent<CharacterAnimController>().speciesId = speciesId;
    }

    protected void spawnDynamicTrapNode(int speciesId, float wx, float wy) {
        var trapPrefab = loadTrapPrefab(trapConfigs[speciesId]);
        GameObject newTrapNode = Instantiate(trapPrefab, new Vector3(wx, wy, triggerZ), Quaternion.identity, underlyingMap.transform);
        dynamicTrapGameObjs.Add(newTrapNode);
    }

    protected void spawnTriggerNode(int triggerLocalId, int speciesId, float wx, float wy) {
        var triggerPrefab = loadTriggerPrefab(triggerConfigs[speciesId]);
        if (null == triggerPrefab) return;
        GameObject newTriggerNode = Instantiate(triggerPrefab, new Vector3(wx, wy, triggerZ), Quaternion.identity, underlyingMap.transform);
        triggerGameObjs[triggerLocalId] = newTriggerNode;
    }

    protected (ulong, ulong) getOrPrefabInputFrameUpsync(int inputFrameId, bool canConfirmSelf, ulong[] prefabbedInputList) {
        if (null == selfPlayerInfo) {
            string msg = String.Format("noDelayInputFrameId={0:D} couldn't be generated due to selfPlayerInfo being null", inputFrameId);
            throw new ArgumentException(msg);
        }

        ulong previousSelfInput = 0,
          currSelfInput = 0;
        int joinIndex = selfPlayerInfo.JoinIndex;
        ulong selfJoinIndexMask = (1UL << (joinIndex - 1));
        var (_, existingInputFrame) = inputBuffer.GetByFrameId(inputFrameId);
        var (_, previousInputFrameDownsync) = inputBuffer.GetByFrameId(inputFrameId - 1);
        previousSelfInput = (null == previousInputFrameDownsync ? 0 : previousInputFrameDownsync.InputList[joinIndex - 1]);

        bool selfConfirmedInExistingInputFrame = (null != existingInputFrame && 0 < (existingInputFrame.ConfirmedList & selfJoinIndexMask)); 
        if (selfConfirmedInExistingInputFrame) {
            /*
            [WARNING] 

            As shown in "https://github.com/genxium/DelayNoMoreUnity/blob/v1.6.5/frontend/Assets/Scripts/Abstract/AbstractMapController.cs#L1180", "playerRdfId" would NEVER be rewinded even under the most clumsy condition, i.e. "RING_BUFF_NON_CONSECUTIVE_SET == dumpRenderCacheRet" is carry-forth only (see "https://github.com/genxium/DelayNoMoreUnity/blob/v1.6.5/shared/FrameRingBuffer.cs#L80").

            The only possibility that "true == selfConfirmedInExistingInputFrame" is met here would be due to "putting `getOrPrefabInputFrameUpsync(..., canConfirmSelf=true, ...) > sendInputFrameUpsyncBatch(...)` before `lockstep`" by mistake -- in that case, "playerRdfId" is stuck at the same value thus we might be overwriting already confirmed input history for self (yet backend and other peers will certainly reject the overwrite!).
            */ 
            return (previousSelfInput, existingInputFrame.InputList[joinIndex - 1]);
        }
        if (
          null != existingInputFrame
          &&
          (true != canConfirmSelf)
        ) {
            return (previousSelfInput, existingInputFrame.InputList[joinIndex - 1]);
        }

        Array.Fill<ulong>(prefabbedInputList, 0);
        for (int k = 0; k < roomCapacity; ++k) {
            if (null != existingInputFrame) {
                // When "null != existingInputFrame", it implies that "true == canConfirmSelf" here, we just have to assign "prefabbedInputList[(joinIndex-1)]" specifically and copy all others
                prefabbedInputList[k] = existingInputFrame.InputList[k];
            } else if (lastIndividuallyConfirmedInputFrameId[k] <= inputFrameId) {
                // Don't predict "btnB" -- yet predicting "btnA" for better "jump holding" consistency
                ulong encodedIdx = (lastIndividuallyConfirmedInputList[k] & 15UL);
                if (null != previousInputFrameDownsync && 0 < (previousInputFrameDownsync.InputList[k] & 16UL) && JUMP_HOLDING_IFD_CNT_THRESHOLD_1 > inputFrameId-lastIndividuallyConfirmedInputFrameId[k]) {
                    prefabbedInputList[k] = (lastIndividuallyConfirmedInputList[k] & 31UL);
                    if (2 == encodedIdx || 5 == encodedIdx || 8 == encodedIdx) {
                        // Don't predict slip-jump!
                        prefabbedInputList[k] = encodedIdx;
                    }
                } else {
                    prefabbedInputList[k] = encodedIdx;
                }
            } else if (null != previousInputFrameDownsync) {
                // When "self.lastIndividuallyConfirmedInputFrameId[k] > inputFrameId", don't use it to predict a historical input!
                // Don't predict jump holding in this case.
                prefabbedInputList[k] = (previousInputFrameDownsync.InputList[k] & 15UL);
            }
        }

        // [WARNING] Do not blindly use "selfJoinIndexMask" here, as the "actuallyUsedInput for self" couldn't be confirmed while prefabbing, otherwise we'd have confirmed a wrong self input by "_markConfirmationIfApplicable()"!
        ulong initConfirmedList = 0;
        if (null != existingInputFrame) {
            // When "null != existingInputFrame", it implies that "true == canConfirmSelf" here
            initConfirmedList = (existingInputFrame.ConfirmedList | selfJoinIndexMask);
        }
        currSelfInput = iptmgr.GetEncodedInput(); // When "null == existingInputFrame", it'd be safe to say that "GetImmediateEncodedInput()" is for the requested "inputFrameId"
        prefabbedInputList[(joinIndex - 1)] = currSelfInput;
        while (inputBuffer.EdFrameId <= inputFrameId) {
            // Fill the gap
            int gapInputFrameId = inputBuffer.EdFrameId;
            inputBuffer.DryPut();
            var (ok, ifdHolder) = inputBuffer.GetByFrameId(gapInputFrameId);
            if (!ok || null == ifdHolder) {
                throw new ArgumentNullException(String.Format("inputBuffer was not fully pre-allocated for gapInputFrameId={0}! Now inputBuffer StFrameId={1}, EdFrameId={2}, Cnt/N={3}/{4}", gapInputFrameId, inputBuffer.StFrameId, inputBuffer.EdFrameId, inputBuffer.Cnt, inputBuffer.N));
            }

            ifdHolder.InputFrameId = gapInputFrameId;
            for (int k = 0; k < roomCapacity; ++k) {
                ifdHolder.InputList[k] = prefabbedInputList[k];
            }
            ifdHolder.ConfirmedList = initConfirmedList;
        }

        return (previousSelfInput, currSelfInput);
    }

    protected (RoomDownsyncFrame, RoomDownsyncFrame) rollbackAndChase(int playerRdfIdSt, int playerRdfIdEd, CollisionSpace collisionSys, bool isChasing) {
        RoomDownsyncFrame prevLatestRdf = null, latestRdf = null;
        for (int i = playerRdfIdSt; i < playerRdfIdEd; i++) {
            var (ok1, currRdf) = renderBuffer.GetByFrameId(i);
            if (false == ok1 || null == currRdf) {
                var msg = String.Format("Couldn't find renderFrame for i={0} to rollback, playerRdfId={1}, might've been interrupted by onRoomDownsyncFrame; renderBuffer:{2}", i, playerRdfId, renderBuffer.toSimpleStat());
                Debug.LogWarning(msg);
                throw new ArgumentNullException(msg);
            }
            if (currRdf.Id != i) {
                throw new ArgumentException(String.Format("Corrupted historic rdf for i={0} to rollback, currRdf={1}! renderBuffer:{2}", i, currRdf, renderBuffer.toSimpleStat()));
            }
            int j = ConvertToDelayedInputFrameId(i);
            var (ok2, delayedInputFrame) = inputBuffer.GetByFrameId(j);
            if (false == ok2 || null == delayedInputFrame) {
                throw new ArgumentNullException(String.Format("Couldn't find delayedInputFrame for j={0} to rollback, playerRdfId={1}; inputBuffer:{2}", j, playerRdfId, inputBuffer.toSimpleStat()));
            }

            bool allowUpdateInputFrameInPlaceUponDynamics = (!isChasing);
            if (allowUpdateInputFrameInPlaceUponDynamics) {
                bool hasInputBeenMutated = UpdateInputFrameInPlaceUponDynamics(inputBuffer, j, roomCapacity, delayedInputFrame.ConfirmedList, delayedInputFrame.InputList, lastIndividuallyConfirmedInputFrameId, lastIndividuallyConfirmedInputList, selfPlayerInfo.JoinIndex);
                if (hasInputBeenMutated) {
                    int ii = ConvertToFirstUsedRenderFrameId(j);
                    if (ii < i) {
                        /*
                           [WARNING] 
                           If we don't rollback at this spot, when the mutated "delayedInputFrame.inputList" a.k.a. "inputFrame#j" matches the later downsynced version, rollback WOULDN'T be triggered for the incorrectly rendered "renderFrame#(ii+1)", and it would STAY IN HISTORY FOREVER -- as the history becomes incorrect, EVERY LATEST renderFrame since "inputFrame#j" was mutated would be ALWAYS incorrectly rendering too!

                           The update to chaserRenderFrameId here would NOT impact the current cycle of rollbackAndChase !
                         */
                        _handleIncorrectlyRenderedPrediction(j, false);
                    }
                }
            }

            bool hasIncorrectlyPredictedRenderFrame = false;
            Step(inputBuffer, i, roomCapacity, collisionSys, renderBuffer, ref overlapResult, ref primaryOverlapResult, collisionHolder, effPushbacks, hardPushbackNormsArr, softPushbacks, softPushbackEnabled, dynamicRectangleColliders, decodedInputHolder, prevDecodedInputHolder, residueCollided, trapLocalIdToColliderAttrs, triggerTrackingIdToTrapLocalId, completelyStaticTrapColliders, unconfirmedBattleResult, ref confirmedBattleResult, pushbackFrameLogBuffer, frameLogEnabled, playerRdfId, shouldDetectRealtimeRenderHistoryCorrection, out hasIncorrectlyPredictedRenderFrame, historyRdfHolder, justFulfilledEvtSubArr, ref justFulfilledEvtSubCnt, missionEvtSubId, selfPlayerInfo.JoinIndex, joinIndexRemap, ref justTriggeredStoryId, justDeadNpcIndices, _loggerBridge);
            if (hasIncorrectlyPredictedRenderFrame) {
                Debug.Log(String.Format("@playerRdfId={0}, hasIncorrectlyPredictedRenderFrame=true for i:{1} -> i+1:{2}", playerRdfId, i, i + 1));
            }

            if (frameLogEnabled) {
                rdfIdToActuallyUsedInput[i] = delayedInputFrame.Clone();
            }

            var (ok3, nextRdf) = renderBuffer.GetByFrameId(i + 1);
            if (false == ok3 || null == nextRdf) {
                if (isChasing) {
                    throw new ArgumentNullException(String.Format("Couldn't find nextRdf for i+1={0} to rollback, playerRdfId={1}; renderBuffer:{2}", i + 1, playerRdfId, renderBuffer.toSimpleStat()));
                } else {
                    throw new ArgumentNullException(String.Format("Couldn't find nextRdf for i+1={0} to generate, playerRdfId={1} while rendering; renderBuffer:{2}", i + 1, playerRdfId, renderBuffer.toSimpleStat()));
                }
            }
            if (nextRdf.Id != i + 1) {
                throw new ArgumentException(String.Format("Corrupted historic rdf for i+1={0} to rollback/generate, nextRdf={1}! renderBuffer:{2}", i, nextRdf, renderBuffer.toSimpleStat()));
            }
            if (true == isChasing) {
                // [WARNING] Move the cursor "chaserRenderFrameId" when "true == isChasing", keep in mind that "chaserRenderFrameId" is not monotonic!
                chaserRenderFrameId = nextRdf.Id;
            } else if (nextRdf.Id == chaserRenderFrameId + 1) {
                chaserRenderFrameId = nextRdf.Id; // To avoid redundant calculation 
            }
            prevLatestRdf = currRdf;
            latestRdf = nextRdf;
        }

        return (prevLatestRdf, latestRdf);
    }

    private int _markConfirmationIfApplicable() {
        int newAllConfirmedCnt = 0;
        int candidateInputFrameId = (lastAllConfirmedInputFrameId + 1);
        if (candidateInputFrameId < inputBuffer.StFrameId) {
            candidateInputFrameId = inputBuffer.StFrameId;
        }
        while (inputBuffer.StFrameId <= candidateInputFrameId && candidateInputFrameId < inputBuffer.EdFrameId) {
            var (res1, inputFrameDownsync) = inputBuffer.GetByFrameId(candidateInputFrameId);
            if (false == res1 || null == inputFrameDownsync) break;
            if (false == isAllConfirmed(inputFrameDownsync.ConfirmedList, roomCapacity)) break;
            ++candidateInputFrameId;
            ++newAllConfirmedCnt;
        }
        if (0 < newAllConfirmedCnt) {
            lastAllConfirmedInputFrameId = candidateInputFrameId - 1;
        }
        return newAllConfirmedCnt;
    }

    protected void _handleIncorrectlyRenderedPrediction(int mismatchedInputFrameId, bool fromUDP) {
        if (TERMINATING_INPUT_FRAME_ID == mismatchedInputFrameId) return;
        int playerRdfId1 = ConvertToFirstUsedRenderFrameId(mismatchedInputFrameId);
        if (playerRdfId1 >= chaserRenderFrameId) return;
        // By now playerRdfId1 < chaserRenderFrameId, it's pretty impossible that "playerRdfId1 > playerRdfId" but we're still checking.
        if (playerRdfId1 > playerRdfId) return; // The incorrect prediction is not yet rendered, no visual impact for player.
        int playerRdfId2 = ConvertToLastUsedRenderFrameId(mismatchedInputFrameId);
        if (playerRdfId2 < chaserRenderFrameIdLowerBound) {
            /*
            [WARNING]
            
            There's no need to reset "chaserRenderFrameId" if the impact of this input mismatch couldn't even reach "chaserRenderFrameIdLowerBound".
            */
            Debug.Log(String.Format("@playerRdfId={0}, IGNORING mismatchedInputFrameId: {1} whose last used rdfId: {2} is smaller than chaserRenderFrameIdLowerBound: {3}; chaserRenderFrameId={4}, lastAllConfirmedInputFrameId={5}, fromUDP={6}", playerRdfId, mismatchedInputFrameId, playerRdfId2, chaserRenderFrameIdLowerBound, chaserRenderFrameId, lastAllConfirmedInputFrameId, fromUDP));
            return;
        }
        /*
		   A typical case is as follows.
		   --------------------------------------------------------
		   <playerRdfId1>                           :              36


		   <this.chaserRenderFrameId>                 :              62

		   [this.playerRdfId]                       :              64
		   --------------------------------------------------------
		 */

        // The actual rollback-and-chase would later be executed in "Update()". 
        chaserRenderFrameId = playerRdfId1;

        /* 
        [WARNING] The incorrect prediction was already rendered, there MIGHT BE a visual impact for player.

        However, due to the use of 
        - `UpdateInputFrameInPlaceUponDynamics`, and  
        - `processInertiaWalking` 
        , even if an "inputFrame" for "already rendered renderFrame" was incorrectly predicted, there's still chance that no visual impact is induced. See relevant sections in `README` for details.  

        Printing of this message might induce a performance impact.
            
        TODO: Instead of printing, add frameLog for (currRenderFrameId, rolledBackInputFrameDownsyncId, rolledBackToRenderFrameId)!
            */
        /*
        if (fromUDP) {
            Debug.Log(String.Format("@playerRdfId={5}, mismatched input for rendered history detected, resetting chaserRenderFrameId: {0}->{1}; mismatchedInputFrameId: {2}, lastAllConfirmedInputFrameId={3}, fromUDP={4}", chaserRenderFrameId, playerRdfId1, mismatchedInputFrameId, lastAllConfirmedInputFrameId, fromUDP, playerRdfId));
        }
        */
    }

    public void applyRoomDownsyncFrameDynamics(RoomDownsyncFrame rdf, RoomDownsyncFrame prevRdf) {
        // Put teamRibbons and hpBars to infinitely far first
        for (int i = cachedTeamRibbons.vals.StFrameId; i < cachedTeamRibbons.vals.EdFrameId; i++) {
            var (res, teamRibbon) = cachedTeamRibbons.vals.GetByFrameId(i);
            if (!res || null == teamRibbon) throw new ArgumentNullException(String.Format("There's no cachedTeamRibbon for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedTeamRibbons.vals.StFrameId, cachedTeamRibbons.vals.EdFrameId));

            newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, inplaceHpBarZ);
            teamRibbon.gameObject.transform.position = newPosHolder;
        }

        for (int i = cachedHpBars.vals.StFrameId; i < cachedHpBars.vals.EdFrameId; i++) {
            var (res, hpBar) = cachedHpBars.vals.GetByFrameId(i);
            if (!res || null == hpBar) throw new ArgumentNullException(String.Format("There's no cachedHpBar for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedHpBars.vals.StFrameId, cachedHpBars.vals.EdFrameId));

            newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, inplaceHpBarZ);
            hpBar.gameObject.transform.position = newPosHolder;
        }

        // Put all pixel-vfx nodes to infinitely far first
        for (int i = cachedPixelVfxNodes.vals.StFrameId; i < cachedPixelVfxNodes.vals.EdFrameId; i++) {
            var (res, holder) = cachedPixelVfxNodes.vals.GetByFrameId(i);
            if (!res || null == holder) throw new ArgumentNullException(String.Format("There's no pixelVfxHolder for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedPixelVfxNodes.vals.StFrameId, cachedPixelVfxNodes.vals.EdFrameId));

            newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, holder.gameObject.transform.position.z);
            holder.gameObject.transform.position = newPosHolder;
        }

        float selfPlayerWx = 0f, selfPlayerWy = 0f;

        for (int k = 0; k < roomCapacity; k++) {
            var currCharacterDownsync = rdf.PlayersArr[k];
            var prevCharacterDownsync = (null == prevRdf ? null : prevRdf.PlayersArr[k]);
            //Debug.Log(String.Format("At rdf.Id={0}, currCharacterDownsync[k:{1}] at [vGridX: {2}, vGridY: {3}, velX: {4}, velY: {5}, chState: {6}, framesInChState: {7}, dirx: {8}]", rdf.Id, k, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, currCharacterDownsync.VelX, currCharacterDownsync.VelY, currCharacterDownsync.CharacterState, currCharacterDownsync.FramesInChState, currCharacterDownsync.DirX));
            var (collisionSpaceX, collisionSpaceY) = VirtualGridToPolygonColliderCtr(currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY);

            var chConfig = characters[currCharacterDownsync.SpeciesId];
            float boxCx, boxCy, boxCw, boxCh;
            calcCharacterBoundingBoxInCollisionSpace(currCharacterDownsync, chConfig, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);

            var playerGameObj = playerGameObjs[k]; 
            var chAnimCtrl = playerGameObj.GetComponent<CharacterAnimController>();

            if (chAnimCtrl.speciesId != currCharacterDownsync.SpeciesId) {
                Destroy(playerGameObjs[k]);
                spawnPlayerNode(k + 1, chConfig.SpeciesId, wx, wy, currCharacterDownsync.BulletTeamId);
            }

            newPosHolder.Set(wx, wy, playerGameObj.transform.position.z);

            playerGameObj.transform.position = newPosHolder; // [WARNING] Even if not selfPlayer, we have to set position of the other players regardless of new positions being visible within camera or not, otherwise outdated other players' node might be rendered within camera! 

            if (currCharacterDownsync.JoinIndex == selfPlayerInfo.JoinIndex) {
                selfBattleHeading.SetCharacter(currCharacterDownsync);
                selfPlayerWx = wx;
                selfPlayerWy = wy;
                //newPosHolder.Set(wx, wy, playerGameObj.transform.position.z);
                //selfPlayerLights.gameObject.transform.position = newPosHolder;
                //selfPlayerLights.setDirX(currCharacterDownsync.DirX);

                int effInventoryCount = 0;
                for (int i = 0; i < currCharacterDownsync.Inventory.Slots.Count; i++) {
                    var slotData = currCharacterDownsync.Inventory.Slots[i];
                    if (InventorySlotStockType.NoneIv == slotData.StockType) break;
                    if (InventorySlotStockType.DummyIv == slotData.StockType) continue;
                    var targetBtn = (0 == i ? iptmgr.btnC : (1 == i ? iptmgr.btnD : iptmgr.btnB)); // TODO: Don't hardcode them
                    targetBtn.gameObject.SetActive(true);
                    var ivSlotGui = targetBtn.GetComponent<InventorySlot>();
                    ivSlotGui.updateData(slotData);
                    if (i < 2) {
                        // TODO: Don't hardcode this!
                        effInventoryCount++;
                    }
                }

                if (0 >= effInventoryCount) {
                    iptmgr.btnC.gameObject.SetActive(false);
                    iptmgr.btnD.gameObject.SetActive(false);
                }
            } else {
                float halfBoxCh = .5f * boxCh;
                float halfBoxCw = .5f * boxCw;
                newTlPosHolder.Set(wx - halfBoxCw, wy + halfBoxCh, characterZ);
                newTrPosHolder.Set(wx + halfBoxCw, wy + halfBoxCh, characterZ);
                newBlPosHolder.Set(wx - halfBoxCw, wy - halfBoxCh, characterZ);
                newBrPosHolder.Set(wx + halfBoxCw, wy - halfBoxCh, characterZ);

                if (!isGameObjPositionWithinCamera(newTlPosHolder) && !isGameObjPositionWithinCamera(newTrPosHolder) && !isGameObjPositionWithinCamera(newBlPosHolder) && !isGameObjPositionWithinCamera(newBrPosHolder)) {
                    // No need to update the actual anim if the other players are out of sight
                    continue;
                }

                // Add teamRibbon and inplaceHpBar
                showTeamRibbonAndInplaceHpBar(rdf.Id, currCharacterDownsync, wx, wy, halfBoxCw, halfBoxCh, "pl-" + currCharacterDownsync.JoinIndex);
            }


            chAnimCtrl.updateCharacterAnim(currCharacterDownsync, currCharacterDownsync.CharacterState, prevCharacterDownsync, false, chConfig);

            // Add character vfx
            float distanceAttenuationZ = Math.Abs(wx - selfPlayerWx) + Math.Abs(wy - selfPlayerWy);
            
            playCharacterDamagedVfx(currCharacterDownsync, chConfig, prevCharacterDownsync, playerGameObj, chAnimCtrl);
            
            playCharacterSfx(currCharacterDownsync, prevCharacterDownsync, chConfig, wx, wy, rdf.Id, distanceAttenuationZ);
            playCharacterVfx(currCharacterDownsync, prevCharacterDownsync, chConfig, wx, wy, rdf.Id);
        }

        // Put all npcNodes to infinitely far first
        foreach (var entry in cachedNpcs) {
            var speciesId = entry.Key;
            var speciesKvPq = entry.Value;
            for (int i = speciesKvPq.vals.StFrameId; i < speciesKvPq.vals.EdFrameId; i++) {
                var (res, npcAnimHolder) = speciesKvPq.vals.GetByFrameId(i);
                if (!res || null == npcAnimHolder) throw new ArgumentNullException(String.Format("There's no npcAnimHolder for i={0}, while StFrameId={1}, EdFrameId={2}", i, speciesKvPq.vals.StFrameId, speciesKvPq.vals.EdFrameId));

                newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, npcAnimHolder.gameObject.transform.position.z);
                npcAnimHolder.gameObject.transform.position = newPosHolder;
            }
        }

        for (int k = 0; k < rdf.NpcsArr.Count; k++) {
            var currNpcDownsync = rdf.NpcsArr[k];

            if (TERMINATING_PLAYER_ID == currNpcDownsync.Id) break;
            var prevNpcDownsync = (null == prevRdf ? null : prevRdf.NpcsArr[k]);
            // Debug.Log(String.Format("At rdf.Id={0}, currNpcDownsync[k:{1}] at [vx: {2}, vy: {3}, chState: {4}, framesInChState: {5}]", rdf.Id, k, currNpcDownsync.VirtualGridX, currNpcDownsync.VirtualGridY, currNpcDownsync.CharacterState, currNpcDownsync.FramesInChState));

            var chConfig = characters[currNpcDownsync.SpeciesId];
            float boxCx, boxCy, boxCw, boxCh;
            calcCharacterBoundingBoxInCollisionSpace(currNpcDownsync, chConfig, currNpcDownsync.VirtualGridX, currNpcDownsync.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);

            float halfBoxCh = .5f * boxCh;
            float halfBoxCw = .5f * boxCw;
            newTlPosHolder.Set(wx - halfBoxCw, wy + halfBoxCh, characterZ);
            newTrPosHolder.Set(wx + halfBoxCw, wy + halfBoxCh, characterZ);
            newBlPosHolder.Set(wx - halfBoxCw, wy - halfBoxCh, characterZ);
            newBrPosHolder.Set(wx + halfBoxCw, wy - halfBoxCh, characterZ);

            if (!isGameObjPositionWithinCamera(newTlPosHolder) && !isGameObjPositionWithinCamera(newTrPosHolder) && !isGameObjPositionWithinCamera(newBlPosHolder) && !isGameObjPositionWithinCamera(newBrPosHolder)) continue;
            // if the current position is within camera FOV
            var speciesKvPq = cachedNpcs[currNpcDownsync.SpeciesId];
            string lookupKey = "npc-" + currNpcDownsync.Id;
            var npcAnimHolder = speciesKvPq.PopAny(lookupKey);
            if (null == npcAnimHolder) {
                npcAnimHolder = speciesKvPq.Pop();
                //Debug.Log(String.Format("@rdf.Id={0} using a new npcAnimHolder for rendering for npcId={1}, joinIndex={2} at wpos=({3}, {4})", rdf.Id, currNpcDownsync.Id, currNpcDownsync.JoinIndex, currNpcDownsync.VirtualGridX, currNpcDownsync.VirtualGridY));
            } else {
                //Debug.Log(String.Format("@rdf.Id={0} using a cached vfxAnimHolder for rendering for npcId={1}, joinIndex={2} at wpos=({3}, {4})", rdf.Id, currNpcDownsync.Id, currNpcDownsync.JoinIndex, currNpcDownsync.VirtualGridX, currNpcDownsync.VirtualGridY));
            }

            if (null == npcAnimHolder) {
                throw new ArgumentNullException(String.Format("No available npcAnimHolder node for lookupKey={0}", lookupKey));
            }

            var npcGameObj = npcAnimHolder.gameObject;
            newPosHolder.Set(wx, wy, (!currNpcDownsync.OmitGravity && !chConfig.OmitGravity) ? characterZ : flyingCharacterZ);
            npcGameObj.transform.position = newPosHolder;

            npcAnimHolder.updateCharacterAnim(currNpcDownsync, currNpcDownsync.CharacterState, prevNpcDownsync, false, chConfig);
            npcAnimHolder.score = rdf.Id;
            speciesKvPq.Put(lookupKey, npcAnimHolder);

            // Add teamRibbon and inplaceHpBar
            if (CharacterState.Dying != currNpcDownsync.CharacterState) {
                showTeamRibbonAndInplaceHpBar(rdf.Id, currNpcDownsync, wx, wy, halfBoxCw, halfBoxCh, lookupKey);
            }

            // Add character vfx
            if (currNpcDownsync.NewBirth) {
                var spr = npcGameObj.GetComponent<SpriteRenderer>();
                var material = spr.material;
                DOTween.Sequence().Append(
                    DOTween.To(() => material.GetFloat(MATERIAL_REF_THICKNESS), x => material.SetFloat(MATERIAL_REF_THICKNESS, x), 1.5f, 0.5f))
                    .Append(DOTween.To(() => material.GetFloat(MATERIAL_REF_THICKNESS), x => material.SetFloat(MATERIAL_REF_THICKNESS, x), 0f, 0.5f));
            }
            playCharacterDamagedVfx(currNpcDownsync, chConfig, prevNpcDownsync, npcGameObj, npcAnimHolder);
            float distanceAttenuationZ = Math.Abs(wx - selfPlayerWx) + Math.Abs(wy - selfPlayerWy);
            playCharacterSfx(currNpcDownsync, prevNpcDownsync, chConfig, wx, wy, rdf.Id, distanceAttenuationZ);
            playCharacterVfx(currNpcDownsync, prevNpcDownsync, chConfig, wx, wy, rdf.Id);
        }

        int kDynamicTrap = 0;
        for (int k = 0; k < rdf.TrapsArr.Count; k++) {
            var currTrap = rdf.TrapsArr[k];
            if (TERMINATING_TRAP_ID == currTrap.TrapLocalId) break;
            if (currTrap.IsCompletelyStatic) continue;
            var (collisionSpaceX, collisionSpaceY) = VirtualGridToPolygonColliderCtr(currTrap.VirtualGridX, currTrap.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(collisionSpaceX, collisionSpaceY, spaceOffsetX, spaceOffsetY);
            var dynamicTrapObj = dynamicTrapGameObjs[kDynamicTrap];
            newPosHolder.Set(wx, wy, dynamicTrapObj.transform.position.z);
            dynamicTrapObj.transform.position = newPosHolder;
            var chAnimCtrl = dynamicTrapObj.GetComponent<TrapAnimationController>();
            chAnimCtrl.updateAnim(currTrap.TrapState.ToString(), currTrap.FramesInTrapState, currTrap.DirX, false);
            kDynamicTrap++;
        }

        // Put all fireball nodes to infinitely far first
        for (int i = cachedFireballs.vals.StFrameId; i < cachedFireballs.vals.EdFrameId; i++) {
            var (res, fireballHolder) = cachedFireballs.vals.GetByFrameId(i);
            if (!res || null == fireballHolder) throw new ArgumentNullException(String.Format("There's no fireballHolder for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedFireballs.vals.StFrameId, cachedFireballs.vals.EdFrameId));

            newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, fireballHolder.gameObject.transform.position.z);
            fireballHolder.gameObject.transform.position = newPosHolder;
        }

        for (int k = 0; k < rdf.Bullets.Count; k++) {
            var bullet = rdf.Bullets[k];
            if (TERMINATING_BULLET_LOCAL_ID == bullet.BattleAttr.BulletLocalId) break;

            var (cx, cy) = VirtualGridToPolygonColliderCtr(bullet.VirtualGridX, bullet.VirtualGridY);
            var (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(bullet.Config.HitboxSizeX, bullet.Config.HitboxSizeY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cx, cy, spaceOffsetX, spaceOffsetY);

            float halfBoxCh = .5f * boxCh;
            float halfBoxCw = .5f * boxCw;
            newTlPosHolder.Set(wx - halfBoxCw, wy + halfBoxCh, 0);
            newTrPosHolder.Set(wx + halfBoxCw, wy + halfBoxCh, 0);
            newBlPosHolder.Set(wx - halfBoxCw, wy - halfBoxCh, 0);
            newBrPosHolder.Set(wx + halfBoxCw, wy - halfBoxCh, 0);

            if (!isGameObjPositionWithinCamera(newTlPosHolder) && !isGameObjPositionWithinCamera(newTrPosHolder) && !isGameObjPositionWithinCamera(newBlPosHolder) && !isGameObjPositionWithinCamera(newBrPosHolder)) continue;

            bool isExploding = IsBulletExploding(bullet);
            var skillConfig = skills[bullet.BattleAttr.SkillId];
            var prevHitBulletConfig = (0 < bullet.BattleAttr.ActiveSkillHit ? skillConfig.Hits[bullet.BattleAttr.ActiveSkillHit - 1] : null); // TODO: Make this compatible with simultaneous bullets after a "FromPrevHitXxx" bullet!
            bool isInPrevHitTriggeredMultiHitSubsequence = (null != prevHitBulletConfig && (MultiHitType.FromPrevHitActual == prevHitBulletConfig.MhType || MultiHitType.FromPrevHitAnyway == prevHitBulletConfig.MhType));

            string lookupKey = "bl-" + bullet.BattleAttr.BulletLocalId.ToString(), animName = null;
            bool spontaneousLooping = false;

            int explosionSpeciesId = bullet.Config.ExplosionSpeciesId;
            if (EXPLOSION_SPECIES_FOLLOW == explosionSpeciesId) {
                explosionSpeciesId = bullet.Config.SpeciesId;
            }
            switch (bullet.Config.BType) {
                case BulletType.Melee:
                    if (isExploding) {
                        animName = String.Format("Melee_Explosion{0}", explosionSpeciesId);
                    }
                    break;
                case BulletType.Fireball:
                case BulletType.GroundWave:
                    if (IsBulletActive(bullet, rdf.Id) || isInPrevHitTriggeredMultiHitSubsequence || isExploding) {
                        animName = isExploding ? String.Format("Explosion{0}", explosionSpeciesId) : String.Format("Fireball{0}", bullet.Config.SpeciesId);
                        spontaneousLooping = !isExploding;
                    }
                    break;
                default:
                    break;
            }
            if (null != animName) {
                var fireballOrExplosionAnimHolder = cachedFireballs.PopAny(lookupKey);
                if (null == fireballOrExplosionAnimHolder) {
                    fireballOrExplosionAnimHolder = cachedFireballs.Pop();
                    //Debug.Log(String.Format("@rdf.Id={0}, origRdfId={1} using a new fireball node for rendering for bulletLocalId={2}, btype={3} at wpos=({4}, {5})", rdf.Id, bullet.BattleAttr.OriginatedRenderFrameId, bullet.BattleAttr.BulletLocalId, bullet.Config.BType, wx, wy));
                } else {
                    //Debug.Log(String.Format("@rdf.Id={0}, origRdfId={1} using a cached node for rendering for bulletLocalId={2}, btype={3} at wpos=({4}, {5})", rdf.Id, bullet.BattleAttr.OriginatedRenderFrameId, bullet.BattleAttr.BulletLocalId, bullet.Config.BType, wx, wy));
                }

                if (null != fireballOrExplosionAnimHolder) {
                    if (fireballOrExplosionAnimHolder.lookUpTable.ContainsKey(animName)) {
                        fireballOrExplosionAnimHolder.updateAnim(animName, bullet.FramesInBlState, bullet.DirX, spontaneousLooping, bullet.Config, rdf, bullet.VelX, bullet.VelY);
                        newPosHolder.Set(wx, wy, fireballOrExplosionAnimHolder.gameObject.transform.position.z);
                        fireballOrExplosionAnimHolder.gameObject.transform.position = newPosHolder;
                    }
                    fireballOrExplosionAnimHolder.score = rdf.Id;
                    cachedFireballs.Put(lookupKey, fireballOrExplosionAnimHolder);
                } else {
                    // null == explosionAnimHolder
                    if (EXPLOSION_SPECIES_NONE != explosionSpeciesId) {
                        // Explosion of fireballs is now allowed to use pure particle vfx
                        throw new ArgumentNullException(String.Format("No available fireball node for lookupKey={0}, animName={1}", lookupKey, animName));
                    }
                }
            }

            float distanceAttenuationZ = Math.Abs(wx - selfPlayerWx) + Math.Abs(wy - selfPlayerWy);
            playBulletSfx(bullet, isExploding, wx, wy, rdf.Id, distanceAttenuationZ);
            playBulletVfx(bullet, isExploding, wx, wy, rdf);
        }

        for (int k = 0; k < rdf.TriggersArr.Count; k++) {
            var trigger = rdf.TriggersArr[k];
            if (!triggerGameObjs.ContainsKey(trigger.TriggerLocalId)) continue;
            if (TERMINATING_TRIGGER_ID == trigger.TriggerLocalId) break;
            var triggerGameObj = triggerGameObjs[trigger.TriggerLocalId];
            var animCtrl = triggerGameObj.GetComponent<TrapAnimationController>();
            animCtrl.updateAnim(trigger.State.ToString(), trigger.FramesInState, trigger.ConfigFromTiled.InitVelX, false);
        }

        // Put all pickable nodes to infinitely far first
        for (int i = cachedPickables.vals.StFrameId; i < cachedPickables.vals.EdFrameId; i++) {
            var (res, pickableHolder) = cachedPickables.vals.GetByFrameId(i);
            if (!res || null == pickableHolder) throw new ArgumentNullException(String.Format("There's no pickableHolder for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedPickables.vals.StFrameId, cachedPickables.vals.EdFrameId));

            newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, pickableHolder.gameObject.transform.position.z);
            pickableHolder.gameObject.transform.position = newPosHolder;
        }

        for (int k = 0; k < rdf.Pickables.Count; k++) {
            var pickable = rdf.Pickables[k];
            if (TERMINATING_PICKABLE_LOCAL_ID == pickable.PickableLocalId) break;
            if (!IsPickableAlive(pickable, rdf.Id)) {
                continue;
            }
            var (cx, cy) = VirtualGridToPolygonColliderCtr(pickable.VirtualGridX, pickable.VirtualGridY);
            var (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(DEFAULT_PICKABLE_HITBOX_SIZE_X, DEFAULT_PICKABLE_HITBOX_SIZE_Y);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cx, cy, spaceOffsetX, spaceOffsetY);

            float halfBoxCh = .5f * boxCh;
            float halfBoxCw = .5f * boxCw;
            newTlPosHolder.Set(wx - halfBoxCw, wy + halfBoxCh, 0);
            newTrPosHolder.Set(wx + halfBoxCw, wy + halfBoxCh, 0);
            newBlPosHolder.Set(wx - halfBoxCw, wy - halfBoxCh, 0);
            newBrPosHolder.Set(wx + halfBoxCw, wy - halfBoxCh, 0);

            if (!isGameObjPositionWithinCamera(newTlPosHolder) && !isGameObjPositionWithinCamera(newTrPosHolder) && !isGameObjPositionWithinCamera(newBlPosHolder) && !isGameObjPositionWithinCamera(newBrPosHolder)) continue;

            // By now only "consumable" is available
            if (TERMINATING_CONSUMABLE_SPECIES_ID != pickable.ConfigFromTiled.ConsumableSpeciesId) {
                var consumableConfig = consumableConfigs[pickable.ConfigFromTiled.ConsumableSpeciesId];
                if (PickableState.Pidle == pickable.PkState) {
                    string lookupKey = pickable.PickableLocalId.ToString(), animName = null;
                    animName = String.Format("Consumable{0}", pickable.ConfigFromTiled.ConsumableSpeciesId);
                    var pickableAnimHolder = cachedPickables.PopAny(lookupKey);
                    if (null == pickableAnimHolder) {
                        pickableAnimHolder = cachedPickables.Pop();
                        //Debug.Log(String.Format("@rdf.Id={0}, using a new pickable node for rendering for pickableLocalId={1} at wpos=({2}, {3})", rdf.Id, pickable.PickableLocalId, wx, wy));
                    } else {
                        //Debug.Log(String.Format("@rdf.Id={0}, using a cached pickable node for rendering for pickableLocalId={1} at wpos=({2}, {3})", rdf.Id, pickable.PickableLocalId, wx, wy));
                    }

                    if (null != pickableAnimHolder && null != pickableAnimHolder.lookUpTable) {
                        if (pickableAnimHolder.lookUpTable.ContainsKey(animName)) {
                            pickableAnimHolder.updateAnim(animName);
                            newPosHolder.Set(wx, wy, pickableAnimHolder.gameObject.transform.position.z);
                            pickableAnimHolder.gameObject.transform.position = newPosHolder;
                        }
                        pickableAnimHolder.score = rdf.Id;
                        cachedPickables.Put(lookupKey, pickableAnimHolder);
                    }
                } else if (PickableState.Pconsumed == pickable.PkState) {
                    string vfxLookupKey = "pk-" + pickable.PickableLocalId.ToString();
                    if (NO_VFX_ID != consumableConfig.VfxIdOnPicker) {
                        var vfxConfig = pixelatedVfxDict[consumableConfig.VfxIdOnPicker];
                        string vfxAnimName = vfxConfig.Name;
                        var pixelVfxHolder = cachedPixelVfxNodes.PopAny(vfxLookupKey);
                        if (null == pixelVfxHolder) {
                            pixelVfxHolder = cachedPixelVfxNodes.Pop();
                            //Debug.Log(String.Format("@rdf.Id={0}, using a new pixel-vfx node for rendering for pickableLocalId={1} at wpos=({2}, {3})", rdf.Id, pickable.PickableLocalId, wx, wy));
                        } else {
                            //Debug.Log(String.Format("@rdf.Id={0}, using a cached pixel-vfx node for rendering for pickableLocalId={1} at wpos=({2}, {3})", rdf.Id, pickable.PickableLocalId, wx, wy));
                        }

                        if (null != pixelVfxHolder && null != pixelVfxHolder.lookUpTable) {
                            if (pixelVfxHolder.lookUpTable.ContainsKey(vfxAnimName)) {
                                pixelVfxHolder.updateAnim(vfxAnimName, pickable.FramesInPkState, 0, false, rdf.Id);
                                var playerObj = playerGameObjs[pickable.PickedByJoinIndex-1]; // Guaranteed to be bound to player controlled characters
                                newPosHolder.Set(playerObj.transform.position.x, playerObj.transform.position.y, pixelVfxHolder.gameObject.transform.position.z);
                                pixelVfxHolder.gameObject.transform.position = newPosHolder;
                            }
                            pixelVfxHolder.score = rdf.Id;
                            cachedPixelVfxNodes.Put(vfxLookupKey, pixelVfxHolder);
                        }
                    }
                }
            }
        }
    }

    protected void preallocatePixelVfxNodes() {
        Debug.Log("preallocatePixelVfxNodes begins");
        if (null != cachedPixelVfxNodes) {
            while (0 < cachedPixelVfxNodes.Cnt()) {
                var g = cachedPixelVfxNodes.Pop();
                if (null != g) {
                    Destroy(g.gameObject);
                }
            }
        }
        int pixelVfxNodeCacheCapacity = 64;
        cachedPixelVfxNodes = new KvPriorityQueue<string, PixelVfxNodeController>(pixelVfxNodeCacheCapacity, pixelVfxNodeScore);
        for (int i = 0; i < pixelVfxNodeCacheCapacity; i++) {
            GameObject newPixelVfxNode = Instantiate(pixelVfxNodePrefab, new Vector3(effectivelyInfinitelyFar, effectivelyInfinitelyFar, fireballZ), Quaternion.identity, underlyingMap.transform);
            PixelVfxNodeController newPixelVfxSource = newPixelVfxNode.GetComponent<PixelVfxNodeController>();
            newPixelVfxSource.score = -1;
            var initLookupKey = i.ToString();
            cachedPixelVfxNodes.Put(initLookupKey, newPixelVfxSource);
        }

        Debug.Log("preallocatePixelVfxNodes ends");
    }

    protected void preallocateSfxNodes() {
        // TODO: Shall I use the same preallocation strategy for VFX? Would run for a while and see the difference...
        Debug.Log("preallocateSfxNodes begins");
        if (null != cachedSfxNodes) {
            while (0 < cachedSfxNodes.Cnt()) {
                var g = cachedSfxNodes.Pop();
                if (null != g) {
                    Destroy(g.gameObject);
                }
            }
        }
        int sfxNodeCacheCapacity = 64;
        cachedSfxNodes = new KvPriorityQueue<string, SFXSource>(sfxNodeCacheCapacity, sfxNodeScore);
        string[] allSfxClipsNames = new string[] {
            "Explosion1",
            "Explosion2",
            "Explosion3",
            "Explosion4",
            "Explosion8",
            "Melee_Explosion1",
            "Melee_Explosion2",
            "Melee_Explosion3",
            "Fireball8",
            "FlameBurning1",
            "FlameEmit1",
            "SlashEmitSpd1",
            "SlashEmitSpd2",
            "SlashEmitSpd3",
            "FootStep1",
            "DoorOpen",
            "DoorClose",
        };
        var audioClipDict = new Dictionary<string, AudioClip>();
        foreach (string name in allSfxClipsNames) {
            string prefabPathUnderResources = "SFX/" + name;
            var theClip = Resources.Load(prefabPathUnderResources) as AudioClip;
            audioClipDict[name] = theClip;
        }

        for (int i = 0; i < sfxNodeCacheCapacity; i++) {
            GameObject newSfxNode = Instantiate(sfxSourcePrefab, new Vector3(effectivelyInfinitelyFar, effectivelyInfinitelyFar, fireballZ), Quaternion.identity, underlyingMap.transform);
            SFXSource newSfxSource = newSfxNode.GetComponent<SFXSource>();
            newSfxSource.score = -1;
            newSfxSource.maxDistanceInWorld = effectivelyInfinitelyFar * 0.25f;
            newSfxSource.audioClipDict = audioClipDict;
            var initLookupKey = i.ToString();
            cachedSfxNodes.Put(initLookupKey, newSfxSource);
        }

        Debug.Log("preallocateSfxNodes ends");
    }

    protected void preallocateNpcNodes() {
        Debug.Log("preallocateNpcNodes begins");

        if (0 >= preallocNpcCapacity) {
            throw new ArgumentException(String.Format("preallocNpcCapacity={0} is non-positive, please initialize it first!", preallocNpcCapacity));
        }

        if (null != cachedNpcs) {
            foreach (var (_, v) in cachedNpcs) {
                while (0 < v.Cnt()) {
                    var g = v.Pop();
                    if (null != g) {
                        Destroy(g.gameObject);
                    }
                }
            }
        }

        var mapProps = underlyingMap.GetComponent<SuperCustomProperties>();
        CustomProperty npcPreallocCapDict, missionEvtSubIdProp, levelIdProp;
        mapProps.TryGetCustomProperty("npcPreallocCapDict", out npcPreallocCapDict);
        mapProps.TryGetCustomProperty("missionEvtSubId", out missionEvtSubIdProp);
        mapProps.TryGetCustomProperty("levelId", out levelIdProp);
        if (null == npcPreallocCapDict || npcPreallocCapDict.IsEmpty) {
            throw new ArgumentNullException("No `npcPreallocCapDict` found on map-scope properties, it's required! Example\n\tvalue `1:16;3:15;4096:1` means that we preallocate 16 slots for species 1, 15 slots for species 3 and 1 slot for species 4096");
        }
        missionEvtSubId = (null == missionEvtSubIdProp || missionEvtSubIdProp.IsEmpty ? MAGIC_EVTSUB_ID_NONE : missionEvtSubIdProp.GetValueAsInt());
        levelId = (null == levelIdProp || levelIdProp.IsEmpty ? LEVEL_NONE : levelIdProp.GetValueAsInt());
        Dictionary<int, int> npcPreallocCapDictVal = new Dictionary<int, int>();
        string npcPreallocCapDictStr = npcPreallocCapDict.GetValueAsString();
        foreach (var kvPairPart in npcPreallocCapDictStr.Trim().Split(';')) {
            var intraKvPairParts = kvPairPart.Split(':');
            int speciesId = intraKvPairParts[0].Trim().ToInt();
            int speciesCapacity = intraKvPairParts[1].Trim().ToInt();
            npcPreallocCapDictVal[speciesId] = speciesCapacity;
        }
        npcSpeciesPrefabDict = new Dictionary<int, GameObject>();
        cachedNpcs = new Dictionary<int, KvPriorityQueue<string, CharacterAnimController>>();
        foreach (var kvPair in npcPreallocCapDictVal) {
            int speciesId = kvPair.Key;
            int speciesCapacity = kvPair.Value;
            var cachedNpcNodesOfThisSpecies = new KvPriorityQueue<string, CharacterAnimController>(speciesCapacity, cachedNpcScore);
            var thePrefab = loadCharacterPrefab(characters[speciesId]);
            npcSpeciesPrefabDict[speciesId] = thePrefab;
            for (int i = 0; i < speciesCapacity; i++) {
                GameObject newNpcNode = Instantiate(thePrefab, new Vector3(effectivelyInfinitelyFar, effectivelyInfinitelyFar, characterZ), Quaternion.identity, underlyingMap.transform);
                CharacterAnimController newNpcNodeController = newNpcNode.GetComponent<CharacterAnimController>();
                newNpcNodeController.score = -1;

                var initLookupKey = i.ToString();
                cachedNpcNodesOfThisSpecies.Put(initLookupKey, newNpcNodeController);
            }
            cachedNpcs[speciesId] = cachedNpcNodesOfThisSpecies;
        }

        Debug.Log("preallocateNpcNodes ends");
    }

    protected void preallocateBattleDynamicsHolder() {
        preallocateStepHolders(
            roomCapacity,
            384,
            preallocNpcCapacity,
            preallocBulletCapacity,
            preallocTrapCapacity,
            preallocTriggerCapacity,
            preallocEvtSubCapacity,
            preallocPickableCapacity,
            out justFulfilledEvtSubCnt,
            out justFulfilledEvtSubArr,
            out renderBuffer,
            out pushbackFrameLogBuffer,
            out inputBuffer,
            out lastIndividuallyConfirmedInputFrameId,
            out lastIndividuallyConfirmedInputList,
            out effPushbacks,
            out hardPushbackNormsArr,
            out softPushbacks,
            out decodedInputHolder,
            out prevDecodedInputHolder,
            out confirmedBattleResult,
            out softPushbackEnabled,
            frameLogEnabled
        );

        joinIndexRemap = new Dictionary<int, int>();
        justDeadNpcIndices = new HashSet<int>();
        othersForcedDownsyncRenderFrameDict = new Dictionary<int, RoomDownsyncFrame>();
        missionEvtSubId = MAGIC_EVTSUB_ID_NONE;
    }

    protected void preallocateFrontendOnlyHolders() {
        //---------------------------------------------FRONTEND USE ONLY SEPERARTION---------------------------------------------
        prefabbedInputListHolder = new ulong[roomCapacity];
        Array.Fill<ulong>(prefabbedInputListHolder, 0);

        // fireball
        int fireballHoldersCap = 48;
        if (null != cachedFireballs) {
            for (int i = cachedFireballs.vals.StFrameId; i < cachedFireballs.vals.EdFrameId; i++) {
                var (res, fireball) = cachedFireballs.vals.GetByFrameId(i);
                if (!res || null == fireball) throw new ArgumentNullException(String.Format("There's no cachedFireball for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedFireballs.vals.StFrameId, cachedFireballs.vals.EdFrameId));
                Destroy(fireball.gameObject);
            }
        }
        cachedFireballs = new KvPriorityQueue<string, FireballAnimController>(fireballHoldersCap, cachedFireballScore);

        for (int i = 0; i < fireballHoldersCap; i++) {
            // Fireballs & explosions should be drawn above any character
            GameObject newFireballNode = Instantiate(fireballPrefab, Vector3.zero, Quaternion.identity, underlyingMap.transform);
            FireballAnimController holder = newFireballNode.GetComponent<FireballAnimController>();
            holder.score = -1;
            string initLookupKey = (-(i + 1)).ToString(); // there's definitely no such "bulletLocalId"
            cachedFireballs.Put(initLookupKey, holder);
        }

        // pickable
        int pickableHoldersCap = 16;
        if (null != cachedPickables) {
            for (int i = cachedPickables.vals.StFrameId; i < cachedPickables.vals.EdFrameId; i++) {
                var (res, pickable) = cachedPickables.vals.GetByFrameId(i);
                if (!res || null == pickable) throw new ArgumentNullException(String.Format("There's no cachedPickable for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedPickables.vals.StFrameId, cachedPickables.vals.EdFrameId));
                Destroy(pickable.gameObject);
            }
        }
        cachedPickables = new KvPriorityQueue<string, PickableAnimController>(pickableHoldersCap, cachedPickableScore);

        for (int i = 0; i < pickableHoldersCap; i++) {
            GameObject newPickableNode = Instantiate(pickablePrefab, Vector3.zero, Quaternion.identity, underlyingMap.transform);
            PickableAnimController holder = newPickableNode.GetComponent<PickableAnimController>();
            holder.score = -1;
            string initLookupKey = (-(i + 1)).ToString(); // there's definitely no such "bulletLocalId"
            cachedPickables.Put(initLookupKey, holder);
        }

        // team ribbon
        int teamRibbonHoldersCap = roomCapacity + preallocNpcCapacity;
        if (null != cachedTeamRibbons) {
            for (int i = cachedTeamRibbons.vals.StFrameId; i < cachedTeamRibbons.vals.EdFrameId; i++) {
                var (res, teamRibbons) = cachedTeamRibbons.vals.GetByFrameId(i);
                if (!res || null == teamRibbons) throw new ArgumentNullException(String.Format("There's no cachedTeamRibbon for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedTeamRibbons.vals.StFrameId, cachedTeamRibbons.vals.EdFrameId));
                Destroy(teamRibbons.gameObject);
            }
        }
        cachedTeamRibbons = new KvPriorityQueue<string, TeamRibbon>(teamRibbonHoldersCap, cachedTeamRibbonScore);

        for (int i = 0; i < teamRibbonHoldersCap; i++) {
            GameObject newTeamRibbonNode = Instantiate(teamRibbonPrefab, Vector3.zero, Quaternion.identity, underlyingMap.transform);
            TeamRibbon holder = newTeamRibbonNode.GetComponent<TeamRibbon>();
            holder.score = -1;
            string initLookupKey = (-(i + 1)).ToString();
            cachedTeamRibbons.Put(initLookupKey, holder);
        }

        // hp bar
        if (null != cachedHpBars) {
            for (int i = cachedHpBars.vals.StFrameId; i < cachedHpBars.vals.EdFrameId; i++) {
                var (res, hpBar) = cachedHpBars.vals.GetByFrameId(i);
                if (!res || null == hpBar) throw new ArgumentNullException(String.Format("There's no cachedHpBar for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedHpBars.vals.StFrameId, cachedHpBars.vals.EdFrameId));
                Destroy(hpBar.gameObject);
            }
        }
        int hpBarHoldersCap = teamRibbonHoldersCap;
        cachedHpBars = new KvPriorityQueue<string, InplaceHpBar>(teamRibbonHoldersCap, cachedHpBarScore);

        for (int i = 0; i < hpBarHoldersCap; i++) {
            GameObject newHpBarNode = Instantiate(inplaceHpBarPrefab, Vector3.zero, Quaternion.identity, underlyingMap.transform);
            InplaceHpBar holder = newHpBarNode.GetComponent<InplaceHpBar>();
            holder.score = -1;
            string initLookupKey = (-(i + 1)).ToString();
            cachedHpBars.Put(initLookupKey, holder);
        }

        // debug line
        if (debugDrawingAllocation) {
            int lineHoldersCap = 64;
            if (null != cachedLineRenderers) {
                for (int i = cachedLineRenderers.vals.StFrameId; i < cachedLineRenderers.vals.EdFrameId; i++) {
                    var (res, line) = cachedLineRenderers.vals.GetByFrameId(i);
                    if (!res || null == line) throw new ArgumentNullException(String.Format("There's no line for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedLineRenderers.vals.StFrameId, cachedLineRenderers.vals.EdFrameId));
                    Destroy(line.gameObject);
                }
            }

            cachedLineRenderers = new KvPriorityQueue<string, DebugLine>(lineHoldersCap, cachedLineScore);
            for (int i = 0; i < lineHoldersCap; i++) {
                GameObject newLineObj = Instantiate(linePrefab, new Vector3(effectivelyInfinitelyFar, effectivelyInfinitelyFar, lineRendererZ), Quaternion.identity, underlyingMap.transform);
                DebugLine newLine = newLineObj.GetComponent<DebugLine>();
                newLine.score = -1;
                newLine.SetWidth(2.0f);
                var initLookupKey = i.ToString();
                cachedLineRenderers.Put(initLookupKey, newLine);
            }
        }
    }

    protected void calcCameraCaps() {
        int camFovW = (int)(2.0f * Camera.main.orthographicSize * Camera.main.aspect);
        int camFovH = (int)(2.0f * Camera.main.orthographicSize);
        int paddingX = (camFovW >> 1);
        int paddingY = (camFovH >> 1);
        cameraCapMinX = 0 + paddingX;
        cameraCapMaxX = (spaceOffsetX << 1) - paddingX;

        cameraCapMinY = -(spaceOffsetY << 1) + paddingY;
        cameraCapMaxY = 0 - paddingY;

        effectivelyInfinitelyFar = 4f * Math.Max(spaceOffsetX, spaceOffsetY);
    }

    protected virtual void resetCurrentMatch(string theme) {
        if (null != underlyingMap) {
            Destroy(underlyingMap);
        }
        Debug.Log(String.Format("resetCurrentMatch with roomCapacity={0}", roomCapacity));
        battleState = ROOM_STATE_IMPOSSIBLE;
        levelId = LEVEL_NONE;
        justTriggeredStoryId = STORY_POINT_NONE;
        playerRdfId = 0;
        chaserRenderFrameId = -1;
        chaserRenderFrameIdLowerBound = -1;
        lastAllConfirmedInputFrameId = -1;
        lastUpsyncInputFrameId = -1;
        localExtraInputDelayFrames = 0;

        /*
         [WARNING]

         By observing "NetworkDoctorInfo.XxxIndicator", it's found that "chasedToPlayerRdfIdIndicator" is most often lit, even during obvious graphical inconsistencies. Therefore the combination "smallChasingRenderFramesPerUpdate = 2 && bigChasingRenderFramesPerUpdate = 4" back then was considered too small. 
        
         The current combination is having much better field test results in terms of graphical consistencies.
         */
        smallChasingRenderFramesPerUpdate = 3; // [WARNING] When using "smallChasingRenderFramesPerUpdate", we're giving more chance to "lockstep"
        bigChasingRenderFramesPerUpdate = 5;
        rdfIdToActuallyUsedInput = new Dictionary<int, InputFrameDownsync>();
        unconfirmedBattleResult = new Dictionary<int, BattleResult>();

        playerGameObjs = new GameObject[roomCapacity];
        dynamicTrapGameObjs = new List<GameObject>();
        triggerGameObjs = new Dictionary<int, GameObject>();
        string path = String.Format("Tiled/{0}/map", theme);
        var underlyingMapPrefab = Resources.Load(path) as GameObject;
        if (null == underlyingMapPrefab) {
            Debug.LogErrorFormat("underlyingMapPrefab is null for theme={0}", theme);
        }
        underlyingMap = GameObject.Instantiate(underlyingMapPrefab, this.gameObject.transform);

        var superMap = underlyingMap.GetComponent<SuperMap>();
        int mapWidth = superMap.m_Width, tileWidth = superMap.m_TileWidth, mapHeight = superMap.m_Height, tileHeight = superMap.m_TileHeight;
        spaceOffsetX = ((mapWidth * tileWidth) >> 1);
        spaceOffsetY = ((mapHeight * tileHeight) >> 1);

        selfBattleHeading.reset();
        readyGoPanel.resetCountdown();
        settlementPanel.gameObject.SetActive(false);

        iptmgr.btnB.GetComponent<InventorySlot>().resumeRegularBtnB();
    }

    public void onInputFrameDownsyncBatch(RepeatedField<InputFrameDownsync> batch) {
        // This method is guaranteed to run in UIThread only.
        if (null == batch) {
            return;
        }
        if (null == inputBuffer) {
            return;
        }
        if (ROOM_STATE_IN_SETTLEMENT == battleState) {
            return;
        }
        // Debug.Log(String.Format("onInputFrameDownsyncBatch called for batchInputFrameIdRange [{0}, {1}]", batch[0].InputFrameId, batch[batch.Count-1].InputFrameId));

        NetworkDoctor.Instance.LogInputFrameDownsync(batch[0].InputFrameId, batch[batch.Count - 1].InputFrameId);
        int firstPredictedYetIncorrectInputFrameId = TERMINATING_INPUT_FRAME_ID;
        foreach (var inputFrameDownsync in batch) {
            int inputFrameDownsyncId = inputFrameDownsync.InputFrameId;
            if (inputFrameDownsyncId <= lastAllConfirmedInputFrameId) {
                continue;
            }
            if (inputFrameDownsyncId > inputBuffer.EdFrameId) {
                Debug.LogWarning(String.Format("Possibly resyncing#1 for inputFrameDownsyncId={0}! Now inputBuffer: {1}", inputFrameDownsyncId, inputBuffer.toSimpleStat()));
            }
            // [WARNING] Now that "inputFrameDownsyncId > self.lastAllConfirmedInputFrameId", we should make an update immediately because unlike its backend counterpart "Room.LastAllConfirmedInputFrameId", the frontend "mapIns.lastAllConfirmedInputFrameId" might inevitably get gaps among discrete values due to "either type#1 or type#2 forceConfirmation" -- and only "onInputFrameDownsyncBatch" can catch this! 
            lastAllConfirmedInputFrameId = inputFrameDownsyncId;
            var (res1, localInputFrame) = inputBuffer.GetByFrameId(inputFrameDownsyncId);
            int playerRdfId2 = ConvertToLastUsedRenderFrameId(inputFrameDownsyncId);

            if (null != localInputFrame
              &&
              TERMINATING_INPUT_FRAME_ID == firstPredictedYetIncorrectInputFrameId
              && 
              playerRdfId2 >= chaserRenderFrameIdLowerBound // [WARNING] Such that "inputFrameDownsyncId" has a meaningful impact.
              &&
              !Battle.EqualInputLists(localInputFrame.InputList, inputFrameDownsync.InputList)
            ) {
                firstPredictedYetIncorrectInputFrameId = inputFrameDownsyncId;
            } else if (
                TERMINATING_INPUT_FRAME_ID == firstPredictedYetIncorrectInputFrameId
                &&
                unconfirmedBattleResult.ContainsKey(inputFrameDownsyncId)
                ) {
                // [WARNING] Unconfirmed battle results must be revisited! TODO: Regardless of "playerRdfId2 < chaserRenderFrameIdLowerBound"?
                firstPredictedYetIncorrectInputFrameId = inputFrameDownsyncId;
                unconfirmedBattleResult.Remove(inputFrameDownsyncId);
            }
            // [WARNING] Take all "inputFrameDownsyncBatch" from backend as all-confirmed, it'll be later checked by "rollbackAndChase". 
            inputFrameDownsync.ConfirmedList = (1UL << roomCapacity) - 1;

            for (int j = 0; j < roomCapacity; j++) {
                if (inputFrameDownsync.InputFrameId > lastIndividuallyConfirmedInputFrameId[j]) {
                    lastIndividuallyConfirmedInputFrameId[j] = inputFrameDownsync.InputFrameId;
                    lastIndividuallyConfirmedInputList[j] = inputFrameDownsync.InputList[j];
                }
            }
            //console.log(`Confirmed inputFrameId=${inputFrameDownsync.inputFrameId}`);
            var (res2, oldStFrameId, oldEdFrameId) = inputBuffer.SetByFrameId(inputFrameDownsync, inputFrameDownsync.InputFrameId);
            if (RingBuffer<InputFrameDownsync>.RING_BUFF_FAILED_TO_SET == res2) {
                throw new ArgumentException(String.Format("Failed to dump input cache(maybe recentInputCache too small)! inputFrameDownsync.inputFrameId={0}, lastAllConfirmedInputFrameId={1}", inputFrameDownsyncId, lastAllConfirmedInputFrameId));
            } else if (RingBuffer<InputFrameDownsync>.RING_BUFF_NON_CONSECUTIVE_SET == res2) {
                Debug.LogWarning(String.Format("Possibly resyncing#2! Now inputBuffer: {0}", inputBuffer.toSimpleStat()));
            }
        }
        _markConfirmationIfApplicable();
        _handleIncorrectlyRenderedPrediction(firstPredictedYetIncorrectInputFrameId, false);
    }

    public void onRoomDownsyncFrame(RoomDownsyncFrame pbRdf, RepeatedField<InputFrameDownsync> accompaniedInputFrameDownsyncBatch, bool usingOthersForcedDownsyncRenderFrameDict = false) {
        // This function is also applicable to "re-joining".
        onInputFrameDownsyncBatch(accompaniedInputFrameDownsyncBatch); // Important to do this step before setting IN_BATTLE
        if (null == renderBuffer) {
            return;
        }
        if (ROOM_STATE_IN_SETTLEMENT == battleState) {
            return;
        }
        int rdfId = pbRdf.Id;
        if (rdfId <= chaserRenderFrameIdLowerBound) {
            Debug.LogWarningFormat("No need to handle downsynced rdfId={0} because chaserRenderFrameIdLowerBound={1}! @playerRdfId={2}, chaserRenderFrameId={3}, renderBuffer=[{4}], inputBuffer=[{5}]", rdfId, chaserRenderFrameIdLowerBound, playerRdfId, chaserRenderFrameId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat());
            return;
        }

        bool shouldForceDumping1 = (DOWNSYNC_MSG_ACT_BATTLE_START == rdfId || usingOthersForcedDownsyncRenderFrameDict);
        bool shouldForceDumping2 = (rdfId > playerRdfId); // In "OnlineMapController", the call sequence per "Update" is "[pollAndHandleWsRecvBuffer >> onRoomDownsyncFrame] > [doUpdate >> rollbackAndChase(playerRdfId, playerRdfId+1)]", thus using strict inequality here.
        bool shouldForceResync = pbRdf.ShouldForceResync;
        ulong selfJoinIndexMask = ((ulong)1 << (selfPlayerInfo.JoinIndex - 1));
        bool selfUnconfirmed = (0 < (pbRdf.BackendUnconfirmedMask & selfJoinIndexMask));
        bool selfConfirmed = !selfUnconfirmed;
        if (selfConfirmed && shouldForceDumping2) {
            /*
            [WARNING]

            When "selfConfirmed && false == shouldForceDumping2", it allows "shouldForceResync" to remain true!
            */
            shouldForceDumping2 = false;
            shouldForceResync = false;
            if (useOthersForcedDownsyncRenderFrameDict) {
                othersForcedDownsyncRenderFrameDict[rdfId] = pbRdf;
            }
        }

        var (oldRdfExists, oldRdf) = renderBuffer.GetByFrameId(rdfId);
        var (dumpRenderCacheRet, oldStRenderFrameId, oldEdRenderFrameId) = (shouldForceDumping1 || shouldForceDumping2 || shouldForceResync) ? renderBuffer.SetByFrameId(pbRdf, rdfId) : (RingBuffer<RoomDownsyncFrame>.RING_BUFF_CONSECUTIVE_SET, TERMINATING_RENDER_FRAME_ID, TERMINATING_RENDER_FRAME_ID);

        if (RingBuffer<RoomDownsyncFrame>.RING_BUFF_FAILED_TO_SET == dumpRenderCacheRet) {
            throw new ArgumentException(String.Format("Failed to dump render cache#1 (maybe recentRenderCache too small)! rdfId={0}", rdfId));
        }

        bool isRingBuffConsecutiveSet = (RingBuffer<RoomDownsyncFrame>.RING_BUFF_CONSECUTIVE_SET == dumpRenderCacheRet);
        bool hasRollbackBurst = false;

        if (shouldForceDumping1 || shouldForceDumping2 || shouldForceResync) {
            /*
            [WARNING] 

            "selfConfirmed && rdfId > playerRdfId" WOULD NOT IMMEDIATELY enter this block due to the mutation of "shouldForceDumping2" and "shouldForceResync" -- however it'd defer to enter here by "shouldForceDumping1 = usingOthersForcedDownsyncRenderFrameDict".  
            */
            if (DOWNSYNC_MSG_ACT_BATTLE_START == rdfId) {
                Debug.LogFormat("On battle started! received rdfId={0}", rdfId);
            } else {
                /*  
                    [WARNING] DON'T allow silent replacement of incorrectly calculated history!
                
                    Regarding the assignment to "chaserRenderFrameId", there is no need to calculate "chaserRenderFrameIdCandidate = ConvertToFirstUsedRenderFrameId(ConvertToDelayedInputFrameId(rdfId))", because no correction to input history is made here (even if there were, it would've been done in "onInputFrameDownsyncBatch"). 

                    The consideration behind 
                    ```
                    if (!EqualRdfs(...) && chaserRenderFrameId > rdfId) {
                        chaserRenderFrameId = rdfId;
                    }
                    if (chaserRenderFrameId < rdfId) {
                        chaserRenderFrameId = rdfId;
                    }
                    ```

                    is as follows:
                    - when we're having a "history update", it's implied that the local calculation of "renderBuffer" for "(rdfId-1) => rdfId" was incorrect w.r.t. backend dynamics, so we can only re-chase from "rdfId", i.e. neither (rdfId-1) nor (rdfId+1);
                    - as of the framelogs, updating "chaserRenderFrameId" would only impact "rollbackAndChase(...)" which only updates "rdfIdToActuallyUsedInput" -- yet no correction to input history as aforementioned, and "renderBuffer" would only be wrapped up at the end;
                    - if "chaserRenderFrameId < rdfId", we need pump up "chaserRenderFrameId" also by "chaserRenderFrameId = rdfId", OTHERWISE IF "false == EqualRdfs(oldRdf, pbRdf)" for now there's a chance that by later "rollbackAndChase(...)" we go through the same local calculation errors AGAIN and overwrite "renderBuffer" with a "wrong & new frame at rdfId"; OR IF "true == EqualRdfs(oldRdf, pbRdf)" for now there's no harm to just pump up to a ground truth and reduce calculation.   
                */
                if (null == accompaniedInputFrameDownsyncBatch) {
                    if (usingOthersForcedDownsyncRenderFrameDict) {
                        // [WARNING] "!EqualRdfs(oldRdf, pbRdf, roomCapacity)" already checked
                        Debug.LogFormat("On battle resynced history update from othersForcedDownsyncRenderFrameDict#1! @playerRdfId={0}, chaserRenderFrameId={1}, renderBuffer=[{2}], inputBuffer=[{3}], isRingBuffConsecutiveSet={4}, chaserRenderFrameIdLowerBound={5}", playerRdfId, chaserRenderFrameId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat(), isRingBuffConsecutiveSet, chaserRenderFrameIdLowerBound);

                        // [WARNING] It's impossible that "true == usingOthersForcedDownsyncRenderFrameDict && chaserRenderFrameId > rdfId" because "rdfId == playerRdfId+1" in this case -- hence there's no chance of rollback burst and we should update "chaserRenderFrameIdLowerBound" as well.
                        chaserRenderFrameId = rdfId;
                        chaserRenderFrameIdLowerBound = rdfId;
                        NetworkDoctor.Instance.LogForceResyncFutureApplied();
                    } else {
                        // Check for "hasRollbackBurst" 
                        if (oldRdfExists && null != oldRdf && !EqualRdfs(oldRdf, pbRdf, roomCapacity)) {         
                            Debug.LogFormat("On battle resynced history update {4}#1! @playerRdfId={0}, chaserRenderFrameId={1}; received rdfId={2} & isRingBuffConsecutiveSet={3}, chaserRenderFrameIdLowerBound={5}", playerRdfId, chaserRenderFrameId, rdfId, isRingBuffConsecutiveSet, selfUnconfirmed ? "for self" : "from another player", chaserRenderFrameIdLowerBound);
                            if (0 > chaserRenderFrameId || chaserRenderFrameId > rdfId) {
                                chaserRenderFrameId = rdfId;
                                hasRollbackBurst = true;
                            }
                        }

                        // Even if "false == hasRollbackBurst", there's no point keeping "chaserRenderFrameId < rdfId" now because "pbRdf" is a ground truth from backend.
                        if (chaserRenderFrameId < rdfId) {
                            chaserRenderFrameId = rdfId;
                        }
                        if (chaserRenderFrameIdLowerBound < rdfId) {
                            chaserRenderFrameIdLowerBound = rdfId;
                        }

                        // Kindly note that if "chaserRenderFrameId > rdfId && (!oldRdfExists || EqualRdfs(oldRdf, pbRdf, roomCapacity))", then "chaserRenderFrameId" will remain unchanged
                    }
                } else {
                    if (usingOthersForcedDownsyncRenderFrameDict) {
                        // [WARNING] "!EqualRdfs(oldRdf, pbRdf, roomCapacity)" already checked
                        Debug.LogFormat("On battle resynced history update from othersForcedDownsyncRenderFrameDict#2! @playerRdfId={3}, chaserRenderFrameId={4}, renderBuffer=[{5}], inputBuffer=[{6}], isRingBuffConsecutiveSet={7}, chaserRenderFrameIdLowerBound={8}; received rdfId={0} & accompaniedInputFrameDownsyncBatch[{1}, ..., {2}]", rdfId, accompaniedInputFrameDownsyncBatch[0].InputFrameId, accompaniedInputFrameDownsyncBatch[accompaniedInputFrameDownsyncBatch.Count - 1].InputFrameId, playerRdfId, chaserRenderFrameId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat(), isRingBuffConsecutiveSet, chaserRenderFrameIdLowerBound);

                        // [WARNING] It's impossible that "true == usingOthersForcedDownsyncRenderFrameDict && chaserRenderFrameId > rdfId" because "rdfId == playerRdfId+1" in this case -- hence there's no chance of rollback burst and we should update "chaserRenderFrameIdLowerBound" as well.
                        chaserRenderFrameId = rdfId;
                        chaserRenderFrameIdLowerBound = rdfId;
                        NetworkDoctor.Instance.LogForceResyncFutureApplied();
                    } else {
                        // Check for "hasRollbackBurst" 
                        if (oldRdfExists && null != oldRdf && !EqualRdfs(oldRdf, pbRdf, roomCapacity)) {         
                            Debug.LogFormat("On battle resynced history update {4}#2! @playerRdfId={0}, chaserRenderFrameId={1}; received rdfId={2} & isRingBuffConsecutiveSet={3}, chaserRenderFrameIdLowerBound={5}", playerRdfId, chaserRenderFrameId, rdfId, isRingBuffConsecutiveSet, selfUnconfirmed ? "for self" : "from another player", chaserRenderFrameIdLowerBound);
                            if (0 > chaserRenderFrameId || chaserRenderFrameId > rdfId) {
                                chaserRenderFrameId = rdfId;
                                hasRollbackBurst = true;
                            }
                        }

                        // Even if "false == hasRollbackBurst", there's no point keeping "chaserRenderFrameId < rdfId" now because "pbRdf" is a ground truth from backend.
                        if (chaserRenderFrameId < rdfId) {
                            chaserRenderFrameId = rdfId;
                        }
                        if (chaserRenderFrameIdLowerBound < rdfId) {
                            chaserRenderFrameIdLowerBound = rdfId;
                        }

                        // Kindly note that if "chaserRenderFrameId > rdfId && (!oldRdfExists || EqualRdfs(oldRdf, pbRdf, roomCapacity))", then "chaserRenderFrameId" will remain unchanged
                    }
                }
            }

            if (DOWNSYNC_MSG_ACT_BATTLE_START == rdfId || RingBuffer<RoomDownsyncFrame>.RING_BUFF_NON_CONSECUTIVE_SET == dumpRenderCacheRet) {
                playerRdfId = rdfId; // [WARNING] It's important NOT to re-assign "playerRdfId" when "RING_BUFF_CONSECUTIVE_SET == dumpRenderCacheRet", e.g. when "true == usingOthersForcedDownsyncRenderFrameDict" (on the ACTIVE NORMAL TICKER side).
                NetworkDoctor.Instance.LogForceResyncImmediatePump(); // [WARNING] "selfUnconfirmed" DOESN'T imply "RING_BUFF_NON_CONSECUTIVE_SET == dumpRenderCacheRet" and this is verified in practice by several tens of internet battle tests.
                pushbackFrameLogBuffer.Clear();
                pushbackFrameLogBuffer.StFrameId = rdfId;
                pushbackFrameLogBuffer.EdFrameId = rdfId;
                // [WARNING] Don't break chasing in other "RING_BUFF_CONSECUTIVE_SET == dumpRenderCacheRet" cases (except for "usingOthersForcedDownsyncRenderFrameDict" and "self-unconfirmed"), otherwise the "unchased" history rdfs & ifds between "[chaserRenderFrameId, rdfId)" can become incorrectly remained in framelog (which is written by rollbackAndChase)! 
                chaserRenderFrameId = rdfId;
                chaserRenderFrameIdLowerBound = rdfId;
            }

            // [WARNING] Validate and correct "chaserRenderFrameId" at the end of "onRoomDownsyncFrame", it's sometimes necessary when "RING_BUFF_NON_CONSECUTIVE_SET == dumpRenderCacheRet"
            if (chaserRenderFrameId < renderBuffer.StFrameId) {
                chaserRenderFrameId = renderBuffer.StFrameId;
            }
            if (chaserRenderFrameIdLowerBound < renderBuffer.StFrameId) {
                chaserRenderFrameIdLowerBound = renderBuffer.StFrameId;
            }

            if (pbRdf.ShouldForceResync) {
                bool exclusivelySelfConfirmedAtLastForceResync = ((0 < pbRdf.BackendUnconfirmedMask) && selfConfirmed);
                ulong allConfirmedMask = (1UL << roomCapacity) - 1;
                bool exclusivelySelfUnconfirmedAtLastForceResync = (allConfirmedMask != pbRdf.BackendUnconfirmedMask && selfUnconfirmed);
                int lastForceResyncedIfdId = lastAllConfirmedInputFrameId; // Because "[onInputFrameDownsyncBatch > _markConfirmationIfApplicable]" is already called
                NetworkDoctor.Instance.LogForceResyncedIfdId(lastForceResyncedIfdId, selfConfirmed, selfUnconfirmed, exclusivelySelfConfirmedAtLastForceResync, exclusivelySelfUnconfirmedAtLastForceResync, hasRollbackBurst, inputFrameUpsyncDelayTolerance);

                if (selfConfirmed) {
                    if (null == accompaniedInputFrameDownsyncBatch) {
                        Debug.LogFormat("On battle resynced for another player#1! @playerRdfId={2}, renderBuffer=[{3}], inputBuffer=[{4}]; received rdfId={0} & no accompaniedInputFrameDownsyncBatch & isRingBuffConsecutiveSet={1}", rdfId, isRingBuffConsecutiveSet, playerRdfId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat());
                    } else {
                        Debug.LogFormat("On battle resynced for another player#2! @playerRdfId={4}, renderBuffer=[{5}], inputBuffer=[{6}]; received rdfId={0} & accompaniedInputFrameDownsyncBatch[{1}, ..., {2}] & isRingBuffConsecutiveSet={3}", rdfId, accompaniedInputFrameDownsyncBatch[0].InputFrameId, accompaniedInputFrameDownsyncBatch[accompaniedInputFrameDownsyncBatch.Count - 1].InputFrameId, isRingBuffConsecutiveSet, playerRdfId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat());
                    }
                } else {
                    // selfUnconfirmed
                    if (!usingOthersForcedDownsyncRenderFrameDict) {
                        if (null == accompaniedInputFrameDownsyncBatch) {
                            Debug.LogFormat("On battle resynced for self#1! @playerRdfId={2}, renderBuffer=[{3}], inputBuffer=[{4}]; received rdfId={0} & no accompaniedInputFrameDownsyncBatch & isRingBuffConsecutiveSet={1};", rdfId, isRingBuffConsecutiveSet, playerRdfId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat());
                        } else {
                            Debug.LogFormat("On battle resynced for self#2! @playerRdfId={4}, renderBuffer=[{5}], inputBuffer=[{6}]; received rdfId={0} & accompaniedInputFrameDownsyncBatch[{1}, ..., {2}] & isRingBuffConsecutiveSet={3}", rdfId, accompaniedInputFrameDownsyncBatch[0].InputFrameId, accompaniedInputFrameDownsyncBatch[accompaniedInputFrameDownsyncBatch.Count - 1].InputFrameId, isRingBuffConsecutiveSet, playerRdfId, renderBuffer.toSimpleStat(), inputBuffer.toSimpleStat());
                        }
                    }
                }
            }

            battleState = ROOM_STATE_IN_BATTLE;
        }

        // [WARNING] Leave all graphical updates in "Update()" by "applyRoomDownsyncFrameDynamics"
        return;
    }

    // Update is called once per frame
    protected void doUpdate() {
        int toGenerateInputFrameId = ConvertToDynamicallyGeneratedDelayInputFrameId(playerRdfId, localExtraInputDelayFrames);
        ulong prevSelfInput = 0, currSelfInput = 0;
        if (ShouldGenerateInputFrameUpsync(playerRdfId)) {
            (prevSelfInput, currSelfInput) = getOrPrefabInputFrameUpsync(toGenerateInputFrameId, true, prefabbedInputListHolder);
        }
        int delayedInputFrameId = ConvertToDelayedInputFrameId(playerRdfId);
        var (delayedInputFrameExists, _) = inputBuffer.GetByFrameId(delayedInputFrameId);
        if (!delayedInputFrameExists) {
            // Possible edge case after resync, kindly note that it's OK to prefab a "future inputFrame" here, because "sendInputFrameUpsyncBatch" would be capped by "noDelayInputFrameId from this.playerRdfId". 
            // Debug.LogWarning(String.Format("@playerRdfId={0}, prefabbing delayedInputFrameId={1} while lastAllConfirmedInputFrameId={2}, inputBuffer:{3}", playerRdfId, delayedInputFrameId, lastAllConfirmedInputFrameId, inputBuffer.toSimpleStat()));
            getOrPrefabInputFrameUpsync(delayedInputFrameId, false, prefabbedInputListHolder);
        }

        bool battleResultIsSet = isBattleResultSet(confirmedBattleResult);

        if (isOnlineMode && (battleResultIsSet || shouldSendInputFrameUpsyncBatch(prevSelfInput, currSelfInput, toGenerateInputFrameId))) {
            // [WARNING] If "true == battleResultIsSet", we MUST IMMEDIATELY flush the local inputs to our peers to favor the formation of all-confirmed inputFrameDownsync asap! 
            // TODO: Does the following statement run asynchronously in an implicit manner? Should I explicitly run it asynchronously?
            sendInputFrameUpsyncBatch(toGenerateInputFrameId);
        }

        if (battleResultIsSet) {
            var (ok1, currRdf) = renderBuffer.GetByFrameId(playerRdfId);
            if (ok1 && null != currRdf) {
                cameraTrack(currRdf, null, true);
            }

            Debug.LogFormat("@playerRdfId={0}, #1 confirmedBattleResult={1}, about to show settlement early!", playerRdfId, confirmedBattleResult);
            StartCoroutine(delayToShowSettlementPanel());
            return;
        }

        // Inside the following "rollbackAndChase" actually ROLLS FORWARD w.r.t. the corresponding delayedInputFrame, REGARDLESS OF whether or not "chaserRenderFrameId == playerRdfId" now. 
        var (prevRdf, rdf) = rollbackAndChase(playerRdfId, playerRdfId + 1, collisionSys, false); // Having "prevRdf.Id == playerRdfId" & "rdf.Id == playerRdfId+1" 

        if (useOthersForcedDownsyncRenderFrameDict) {
            // [WARNING] The following calibration against "othersForcedDownsyncRenderFrameDict" can also be placed inside "chaseRolledbackRdfs" for a more rigorous treatment. However when "othersForcedDownsyncRenderFrameDict" is updated, the corresponding "resynced rdf" always has an id not smaller than "playerRdfId", thus no need to take those wasting calibrations.  
            if (othersForcedDownsyncRenderFrameDict.ContainsKey(rdf.Id)) {
                var othersForcedDownsyncRenderFrame = othersForcedDownsyncRenderFrameDict[rdf.Id];
                if (!EqualRdfs(othersForcedDownsyncRenderFrame, rdf, roomCapacity)) {
                    Debug.LogWarningFormat("Mismatched render frame@rdf.id={0} w/ delayedInputFrameId={1}:\nrdf={2}\nothersForcedDownsyncRenderFrame={3}\nnow inputBuffer:{4}, renderBuffer:{5}", rdf.Id, delayedInputFrameId, stringifyRdf(rdf), stringifyRdf(othersForcedDownsyncRenderFrame), inputBuffer.toSimpleStat(), renderBuffer.toSimpleStat());
                    // [WARNING] When this happens, something is intrinsically wrong -- to avoid having an inconsistent history in the "renderBuffer", thus a wrong prediction all the way from here on, clear the history!
                    othersForcedDownsyncRenderFrame.ShouldForceResync = true;
                    othersForcedDownsyncRenderFrame.BackendUnconfirmedMask = ((1ul << roomCapacity) - 1);
                    onRoomDownsyncFrame(othersForcedDownsyncRenderFrame, null, true);
                    Debug.LogWarningFormat("Handled mismatched render frame@rdf.id={0} w/ delayedInputFrameId={1}, playerRdfId={2}:\nnow inputBuffer:{3}, renderBuffer:{4}", rdf.Id, delayedInputFrameId, playerRdfId, inputBuffer.toSimpleStat(), renderBuffer.toSimpleStat());
                }
                othersForcedDownsyncRenderFrameDict.Remove(rdf.Id); // [WARNING] Removes anyway because we won't revisit the same "playerRdfId" in a same battle!
            }
        }

        applyRoomDownsyncFrameDynamics(rdf, prevRdf);
        cameraTrack(rdf, prevRdf, false);

        bool battleResultIsSetAgain = isBattleResultSet(confirmedBattleResult);
        if (!battleResultIsSetAgain) {
            ++playerRdfId;
        }
    }

    protected virtual int chaseRolledbackRdfs() {
        int prevChaserRenderFrameId = chaserRenderFrameId;
        int biggestAllConfirmedRdfId = ConvertToLastUsedRenderFrameId(lastAllConfirmedInputFrameId);
        /*
        [WARNING] 

        As commented in "onPeerInputFrameUpsync", received UDP packets would NOT advance "lastAllConfirmedInputFrameId", hence when "prevChaserRenderFrameId >= biggestAllConfirmedRdfId" we can chase by "smallChasingRenderFramesPerUpdate" and just hope that the UDP packets are advanced enough to make a good prediction!    
        */
        int nextChaserRenderFrameId = (prevChaserRenderFrameId >= biggestAllConfirmedRdfId) ? (prevChaserRenderFrameId + smallChasingRenderFramesPerUpdate) : (prevChaserRenderFrameId + bigChasingRenderFramesPerUpdate);

        if (nextChaserRenderFrameId > playerRdfId) {
            nextChaserRenderFrameId = playerRdfId;
        }

        if (prevChaserRenderFrameId < nextChaserRenderFrameId) {
            // Do not execute "rollbackAndChase" when "prevChaserRenderFrameId == nextChaserRenderFrameId", otherwise if "nextChaserRenderFrameId == self.playerRdfId" we'd be wasting computing power once. 
            rollbackAndChase(prevChaserRenderFrameId, nextChaserRenderFrameId, collisionSys, true);
        }

        return nextChaserRenderFrameId;
    }

    protected virtual void onBattleStopped() {
        if (ROOM_STATE_IMPOSSIBLE != battleState && ROOM_STATE_IN_BATTLE != battleState && ROOM_STATE_IN_SETTLEMENT != battleState) {
            Debug.LogWarningFormat("@playerRdfId={0}, unable to stop battle due to invalid state transition; now battleState={1}", playerRdfId, battleState);
            return;
        }
        playerRdfId = 0;
        bgmSource.Stop();
        battleState = ROOM_STATE_STOPPED;
        
        // Reset the preallocated
        if (null != lastIndividuallyConfirmedInputFrameId) {
            Array.Fill<int>(lastIndividuallyConfirmedInputFrameId, -1);
        }
        if (null != lastIndividuallyConfirmedInputList) {
            Array.Fill<ulong>(lastIndividuallyConfirmedInputList, 0);
        }
        if (frameLogEnabled) {
            if (null != pushbackFrameLogBuffer) {
                pushbackFrameLogBuffer.Clear();
            } 
        }
        if (null != residueCollided) {
            residueCollided.Clear();
        } 
        if (null != prefabbedInputListHolder) {
            Array.Fill<ulong>(prefabbedInputListHolder, 0);
        }
        if (null != confirmedBattleResult) {
            resetBattleResult(ref confirmedBattleResult);
        }
        if (null != iptmgr) {
            iptmgr.ResetSelf();
        }

        Debug.LogWarningFormat("onBattleStopped; now battleState={0}", battleState);
    }

    protected IEnumerator delayToShowSettlementPanel() {
        if (ROOM_STATE_IN_BATTLE != battleState) {
            Debug.LogWarning("Why calling delayToShowSettlementPanel during active battle? playerRdfId = " + playerRdfId);
            yield return new WaitForSeconds(0);
        } else {
            battleState = ROOM_STATE_IN_SETTLEMENT;
            settlementPanel.postSettlementCallback = () => {
                onBattleStopped();
            };
            settlementPanel.gameObject.SetActive(true);
            settlementPanel.toggleUIInteractability(true);
            // TODO: In versus mode, should differentiate between "winnerJoinIndex == selfPlayerIndex" and otherwise
            if (isBattleResultSet(confirmedBattleResult)) {
                settlementPanel.PlaySettlementAnim(true);
            } else {
                settlementPanel.PlaySettlementAnim(false);
            }
        }
    }

    protected abstract bool shouldSendInputFrameUpsyncBatch(ulong prevSelfInput, ulong currSelfInput, int currInputFrameId);

    protected abstract void sendInputFrameUpsyncBatch(int latestLocalInputFrameId);

    protected void enableBattleInput(bool yesOrNo) {
        iptmgr.enable(yesOrNo);
        iptmgr.gameObject.SetActive(yesOrNo);
    }

    protected string ArrToString(int[] speciesIdList) {
        var ret = "";
        for (int i = 0; i < speciesIdList.Length; i++) {
            ret += speciesIdList[i].ToString();
            if (i < speciesIdList.Length - 1) ret += ", ";
        }
        return ret;
    }

    protected void patchStartRdf(RoomDownsyncFrame startRdf, int[] speciesIdList) {
        for (int i = 0; i < roomCapacity; i++) {
            if (SPECIES_NONE_CH == speciesIdList[i]) continue;
            if (selfPlayerInfo.JoinIndex == i + 1) continue;

            Debug.LogFormat("Patching speciesIdList={0} for selfJoinIndex={1}", ArrToString(speciesIdList), selfPlayerInfo.JoinIndex);
            var playerInRdf = startRdf.PlayersArr[i];
            // Only copied species specific part from "mockStartRdf"
            playerInRdf.SpeciesId = speciesIdList[i];
            var chConfig = characters[playerInRdf.SpeciesId];
            playerInRdf.Hp = chConfig.Hp;
            playerInRdf.Mp = chConfig.Mp;
            playerInRdf.Speed = chConfig.Speed;
            playerInRdf.OmitGravity = chConfig.OmitGravity;
            playerInRdf.OmitSoftPushback = chConfig.OmitSoftPushback;
            playerInRdf.RepelSoftPushback = chConfig.RepelSoftPushback;

            var (playerCposX, playerCposY) = VirtualGridToPolygonColliderCtr(playerInRdf.VirtualGridX, playerInRdf.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(playerCposX, playerCposY, spaceOffsetX, spaceOffsetY);

            if (null != chConfig.InitInventorySlots) {
                for (int t = 0; t < chConfig.InitInventorySlots.Count; t++) {
                    var initIvSlot = chConfig.InitInventorySlots[t];
                    if (InventorySlotStockType.NoneIv == initIvSlot.StockType) break;
                    AssignToInventorySlot(initIvSlot.StockType, initIvSlot.Quota, initIvSlot.FramesToRecover, initIvSlot.DefaultQuota, initIvSlot.DefaultFramesToRecover, initIvSlot.BuffSpeciesId, initIvSlot.SkillId, playerInRdf.Inventory.Slots[t]);
                }
            }
            spawnPlayerNode(playerInRdf.JoinIndex, playerInRdf.SpeciesId, wx, wy, playerInRdf.BulletTeamId);
        }
    }

    protected (RoomDownsyncFrame, RepeatedField<SerializableConvexPolygon>, RepeatedField<SerializedCompletelyStaticPatrolCueCollider>, RepeatedField<SerializedCompletelyStaticTrapCollider>, RepeatedField<SerializedCompletelyStaticTriggerCollider>, SerializedTrapLocalIdToColliderAttrs, SerializedTriggerTrackingIdToTrapLocalId, int) mockStartRdf(int[] speciesIdList) {
        Debug.LogFormat("mockStartRdf with speciesIdList={0} for selfJoinIndex={1}", ArrToString(speciesIdList), selfPlayerInfo.JoinIndex);
        var serializedBarrierPolygons = new RepeatedField<SerializableConvexPolygon>();
        var serializedStaticPatrolCues = new RepeatedField<SerializedCompletelyStaticPatrolCueCollider>();
        var serializedCompletelyStaticTraps = new RepeatedField<SerializedCompletelyStaticTrapCollider>();
        var serializedStaticTriggers = new RepeatedField<SerializedCompletelyStaticTriggerCollider>();
        var serializedTrapLocalIdToColliderAttrs = new SerializedTrapLocalIdToColliderAttrs();
        var serializedTriggerTrackingIdToTrapLocalId = new SerializedTriggerTrackingIdToTrapLocalId();
        var grid = underlyingMap.GetComponentInChildren<Grid>();
        var playerStartingCposList = new List<(Vector, int, int)>();
        var npcsStartingCposList = new List<(Vector, int, int, int, int, bool, int, ulong, int, int, int)>();
        var trapList = new List<Trap>();
        var triggerList = new List<(Trigger, float, float)>();
        var pickableList = new List<(Pickable, float, float)>();
        var evtSubList = new List<EvtSubscription>();
        float defaultPatrolCueRadius = 10;
        int trapLocalId = 0;
        int triggerLocalId = 0;
        int pickableLocalId = 0;

        var mapProps = underlyingMap.GetComponent<SuperCustomProperties>();
        CustomProperty battleDurationSeconds;
        mapProps.TryGetCustomProperty("battleDurationSeconds", out battleDurationSeconds);
        int battleDurationSecondsVal = (null == battleDurationSeconds || battleDurationSeconds.IsEmpty) ? 60 : battleDurationSeconds.GetValueAsInt();

        foreach (Transform child in grid.transform) {
            switch (child.gameObject.name) {
                case "EvtSubscription":
                    foreach (Transform evtSubTsf in child) {
                        var evtSubTileObj = evtSubTsf.GetComponent<SuperObject>();
                        var tileProps = evtSubTsf.GetComponent<SuperCustomProperties>();
                        CustomProperty id, demandedEvtMask;
                        tileProps.TryGetCustomProperty("id", out id);
                        tileProps.TryGetCustomProperty("demandedEvtMask", out demandedEvtMask);

                        var evtSub = new EvtSubscription {
                            Id = id.GetValueAsInt(),
                            DemandedEvtMask = (null == demandedEvtMask || demandedEvtMask.IsEmpty) ? EVTSUB_NO_DEMAND_MASK : (ulong)demandedEvtMask.GetValueAsInt(),
                        };

                        evtSubList.Add(evtSub);
                    }
                    Destroy(child.gameObject); // Delete the whole "ObjectLayer"
                    break;
                case "Barrier":
                    foreach (Transform barrierChild in child) {
                        var barrierTileObj = barrierChild.GetComponent<SuperObject>();
                        var inMapCollider = barrierChild.GetComponent<EdgeCollider2D>();

                        if (null == inMapCollider || 0 >= inMapCollider.pointCount) {
                            var (tiledRectCx, tiledRectCy) = (barrierTileObj.m_X + barrierTileObj.m_Width * 0.5f, barrierTileObj.m_Y + barrierTileObj.m_Height * 0.5f);
                            var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                            /*
                             [WARNING] 

                            The "Unity World (0, 0)" is aligned with the top-left corner of the rendered "TiledMap (via SuperMap)".

                            It's noticeable that all the "Collider"s in "CollisionSpace" must be of positive coordinates to work due to the implementation details of "resolv". Thus I'm using a "Collision Space (0, 0)" aligned with the bottom-left of the rendered "TiledMap (via SuperMap)". 
                            */
                            var srcPolygon = NewRectPolygon(rectCx, rectCy, barrierTileObj.m_Width, barrierTileObj.m_Height, 0, 0, 0, 0);
                            serializedBarrierPolygons.Add(srcPolygon.Serialize());
                        } else {
                            var points = inMapCollider.points;
                            List<float> points2 = new List<float>();
                            foreach (var point in points) {
                                points2.Add(point.x);
                                points2.Add(point.y);
                            }
                            var (anchorCx, anchorCy) = TiledLayerPositionToCollisionSpacePosition(barrierTileObj.m_X, barrierTileObj.m_Y, spaceOffsetX, spaceOffsetY);
                            var srcPolygon = new ConvexPolygon(anchorCx, anchorCy, points2.ToArray());
                            serializedBarrierPolygons.Add(srcPolygon.Serialize());
                        }

                        // TODO: By now I have to enable the import of all colliders to see the "inMapCollider: EdgeCollider2D" component, then remove unused components here :(
                        Destroy(barrierChild.GetComponent<EdgeCollider2D>());
                        Destroy(barrierChild.GetComponent<BoxCollider2D>());
                        Destroy(barrierChild.GetComponent<SuperColliderComponent>());
                    }
                    Destroy(child.gameObject); // Delete the whole "ObjectLayer"
                    break;
                case "PlayerStartingPos":
                    int j = 0;
                    foreach (Transform playerPos in child) {
                        var posTileObj = playerPos.gameObject.GetComponent<SuperObject>();
                        var tileProps = playerPos.gameObject.gameObject.GetComponent<SuperCustomProperties>();
                        CustomProperty teamId, dirX;
                        tileProps.TryGetCustomProperty("teamId", out teamId);
                        tileProps.TryGetCustomProperty("dirX", out dirX);

                        var (cx, cy) = TiledLayerPositionToCollisionSpacePosition(posTileObj.m_X, posTileObj.m_Y, spaceOffsetX, spaceOffsetY);

                        playerStartingCposList.Add((
                            new Vector(cx, cy),
                            null == teamId || teamId.IsEmpty ? DEFAULT_BULLET_TEAM_ID : teamId.GetValueAsInt(),
                            null == dirX || dirX.IsEmpty ? +2 : dirX.GetValueAsInt()
                        ));
                        //Debug.Log(String.Format("new playerStartingCposList[i:{0}]=[X:{1}, Y:{2}]", j, cx, cy));
                        j++;
                    }
                    Destroy(child.gameObject); // Delete the whole "ObjectLayer"
                    break;
                case "NpcStartingPos":
                    foreach (Transform npcPos in child) {
                        var tileObj = npcPos.gameObject.GetComponent<SuperObject>();
                        var tileProps = npcPos.gameObject.gameObject.GetComponent<SuperCustomProperties>();
                        var (cx, cy) = TiledLayerPositionToCollisionSpacePosition(tileObj.m_X, tileObj.m_Y, spaceOffsetX, spaceOffsetY);
                        CustomProperty dirX, dirY, speciesId, teamId, isStatic, publishingEvtSubIdUponKilled, publishingEvtMaskUponKilled, subscriptionId, killedToDropConsumableSpeciesId, killedToDropBuffSpeciesId;
                        tileProps.TryGetCustomProperty("dirX", out dirX);
                        tileProps.TryGetCustomProperty("dirY", out dirY);
                        tileProps.TryGetCustomProperty("speciesId", out speciesId);
                        tileProps.TryGetCustomProperty("teamId", out teamId);
                        tileProps.TryGetCustomProperty("static", out isStatic);
                        tileProps.TryGetCustomProperty("publishingEvtSubIdUponKilled", out publishingEvtSubIdUponKilled);
                        tileProps.TryGetCustomProperty("publishingEvtMaskUponKilled", out publishingEvtMaskUponKilled);
                        tileProps.TryGetCustomProperty("subscriptionId", out subscriptionId);
                        tileProps.TryGetCustomProperty("killedToDropConsumableSpeciesId", out killedToDropConsumableSpeciesId);
                        tileProps.TryGetCustomProperty("killedToDropBuffSpeciesId", out killedToDropBuffSpeciesId);

                        npcsStartingCposList.Add((
                                                    new Vector(cx, cy),
                                                    null == dirX || dirX.IsEmpty ? 0 : dirX.GetValueAsInt(),
                                                    null == dirY || dirY.IsEmpty ? 0 : dirY.GetValueAsInt(),
                                                    null == speciesId || speciesId.IsEmpty ? 0 : speciesId.GetValueAsInt(),
                                                    null == teamId || teamId.IsEmpty ? DEFAULT_BULLET_TEAM_ID : teamId.GetValueAsInt(),
                                                    null == isStatic || isStatic.IsEmpty ? false : (1 == isStatic.GetValueAsInt()),
                                                    null == publishingEvtSubIdUponKilled || publishingEvtSubIdUponKilled.IsEmpty ? MAGIC_EVTSUB_ID_NONE : publishingEvtSubIdUponKilled.GetValueAsInt(),
                                                    null == publishingEvtMaskUponKilled || publishingEvtMaskUponKilled.IsEmpty ? 0ul : (ulong)publishingEvtMaskUponKilled.GetValueAsInt(),
                                                    null == subscriptionId || subscriptionId.IsEmpty ? MAGIC_EVTSUB_ID_NONE : subscriptionId.GetValueAsInt(),
                                                    null == killedToDropConsumableSpeciesId || killedToDropConsumableSpeciesId.IsEmpty ? TERMINATING_CONSUMABLE_SPECIES_ID : killedToDropConsumableSpeciesId.GetValueAsInt(),
                                                    null == killedToDropBuffSpeciesId || killedToDropBuffSpeciesId.IsEmpty ? TERMINATING_BUFF_SPECIES_ID : killedToDropBuffSpeciesId.GetValueAsInt()
                        ));
                    }
                    Destroy(child.gameObject); // Delete the whole "ObjectLayer"
                    break;
                case "PatrolCue":
                    foreach (Transform patrolCueChild in child) {
                        var tileObj = patrolCueChild.GetComponent<SuperObject>();
                        var tileProps = patrolCueChild.GetComponent<SuperCustomProperties>();
                        
                        var (patrolCueCx, patrolCueCy) = TiledLayerPositionToCollisionSpacePosition(tileObj.m_X, tileObj.m_Y, spaceOffsetX, spaceOffsetY);
                        if (0 != tileObj.m_Width) {
                            var (tiledRectCx, tiledRectCy) = (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y + tileObj.m_Height * 0.5f);
                            (patrolCueCx, patrolCueCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                        }

                        CustomProperty id, flAct, frAct, flCaptureFrames, frCaptureFrames, fdAct, fuAct, fdCaptureFrames, fuCaptureFrames, collisionTypeMask;
                        tileProps.TryGetCustomProperty("id", out id);
                        tileProps.TryGetCustomProperty("flAct", out flAct);
                        tileProps.TryGetCustomProperty("frAct", out frAct);
                        tileProps.TryGetCustomProperty("flCaptureFrames", out flCaptureFrames);
                        tileProps.TryGetCustomProperty("frCaptureFrames", out frCaptureFrames);
                        tileProps.TryGetCustomProperty("fdAct", out fdAct);
                        tileProps.TryGetCustomProperty("fuAct", out fuAct);
                        tileProps.TryGetCustomProperty("fdCaptureFrames", out fdCaptureFrames);
                        tileProps.TryGetCustomProperty("fuCaptureFrames", out fuCaptureFrames);
                        tileProps.TryGetCustomProperty("collisionTypeMask", out collisionTypeMask);

                        ulong collisionTypeMaskVal = (null != collisionTypeMask && !collisionTypeMask.IsEmpty) ? (ulong)collisionTypeMask.GetValueAsInt() : COLLISION_NPC_PATROL_CUE_INDEX_PREFIX;

                        var newPatrolCue = new PatrolCue {
                            Id = (null == id || id.IsEmpty) ? NO_PATROL_CUE_ID : id.GetValueAsInt(),
                            FlAct = (null == flAct || flAct.IsEmpty) ? 0 : (ulong)flAct.GetValueAsInt(),
                            FrAct = (null == frAct || frAct.IsEmpty) ? 0 : (ulong)frAct.GetValueAsInt(),
                            FlCaptureFrames = (null == flCaptureFrames || flCaptureFrames.IsEmpty) ? 0 : (ulong)flCaptureFrames.GetValueAsInt(),
                            FrCaptureFrames = (null == frCaptureFrames || frCaptureFrames.IsEmpty) ? 0 : (ulong)frCaptureFrames.GetValueAsInt(),

                            FdAct = (null == fdAct || fdAct.IsEmpty) ? 0 : (ulong)fdAct.GetValueAsInt(),
                            FuAct = (null == fuAct || fuAct.IsEmpty) ? 0 : (ulong)fuAct.GetValueAsInt(),
                            FdCaptureFrames = (null == fdCaptureFrames || fdCaptureFrames.IsEmpty) ? 0 : (ulong)fdCaptureFrames.GetValueAsInt(),
                            FuCaptureFrames = (null == fuCaptureFrames || fuCaptureFrames.IsEmpty) ? 0 : (ulong)fuCaptureFrames.GetValueAsInt(),
                            CollisionTypeMask = collisionTypeMaskVal
                        };

                        float cueWidth = (0 == tileObj.m_Width ? 2 * defaultPatrolCueRadius : tileObj.m_Width);
                        float cueHeight = (0 == tileObj.m_Height ? 2 * defaultPatrolCueRadius : tileObj.m_Height);

                        var srcPolygon = NewRectPolygon(patrolCueCx, patrolCueCy, cueWidth, cueHeight, 0, 0, 0, 0);
                        serializedStaticPatrolCues.Add(new SerializedCompletelyStaticPatrolCueCollider {
                            Polygon = srcPolygon.Serialize(),
                            Attr = newPatrolCue,
                        });
                    }
                    Destroy(child.gameObject); // Delete the whole "ObjectLayer"
                    break;
                case "TrapStartingPos":
                    foreach (Transform trapChild in child) {
                        var tileObj = trapChild.GetComponent<SuperObject>();
                        var tileProps = trapChild.GetComponent<SuperCustomProperties>();

                        CustomProperty speciesId, providesHardPushback, providesDamage, providesEscape, providesSlipJump, forcesCrouching, isCompletelyStatic, collisionTypeMask, dirX, dirY, speed, triggerTrackingId, prohibitsWallGrabbing, locked, unlockSubscriptionId;
                        tileProps.TryGetCustomProperty("speciesId", out speciesId);
                        tileProps.TryGetCustomProperty("providesHardPushback", out providesHardPushback);
                        tileProps.TryGetCustomProperty("providesDamage", out providesDamage);
                        tileProps.TryGetCustomProperty("providesEscape", out providesEscape);
                        tileProps.TryGetCustomProperty("providesSlipJump", out providesSlipJump);
                        tileProps.TryGetCustomProperty("forcesCrouching", out forcesCrouching);
                        tileProps.TryGetCustomProperty("static", out isCompletelyStatic);
                        tileProps.TryGetCustomProperty("dirX", out dirX);
                        tileProps.TryGetCustomProperty("dirY", out dirY);
                        tileProps.TryGetCustomProperty("speed", out speed);
                        tileProps.TryGetCustomProperty("triggerTrackingId", out triggerTrackingId);
                        tileProps.TryGetCustomProperty("prohibitsWallGrabbing", out prohibitsWallGrabbing);
                        tileProps.TryGetCustomProperty("locked", out locked);
                        tileProps.TryGetCustomProperty("unlockSubscriptionId", out unlockSubscriptionId);

                        int speciesIdVal = speciesId.GetValueAsInt(); // Not checking null or empty for this property because it shouldn't be, and in case it comes empty anyway, this automatically throws an error 
                        bool providesHardPushbackVal = (null != providesHardPushback && !providesHardPushback.IsEmpty && 1 == providesHardPushback.GetValueAsInt()) ? true : false;
                        bool providesDamageVal = (null != providesDamage && !providesDamage.IsEmpty && 1 == providesDamage.GetValueAsInt()) ? true : false;
                        bool providesEscapeVal = (null != providesEscape && !providesEscape.IsEmpty && 1 == providesEscape.GetValueAsInt()) ? true : false;
                        bool providesSlipJumpVal = (null != providesSlipJump && !providesSlipJump.IsEmpty && 1 == providesSlipJump.GetValueAsInt()) ? true : false;
                        bool forcesCrouchingVal = (null != forcesCrouching && !forcesCrouching.IsEmpty && 1 == forcesCrouching.GetValueAsInt()) ? true : false;
                        bool isCompletelyStaticVal = (null != isCompletelyStatic && !isCompletelyStatic.IsEmpty && 1 == isCompletelyStatic.GetValueAsInt()) ? true : false;
                        bool prohibitsWallGrabbingVal = (null != prohibitsWallGrabbing && !prohibitsWallGrabbing.IsEmpty && 1 == prohibitsWallGrabbing.GetValueAsInt()) ? true : false;

                        int dirXVal = (null == dirX || dirX.IsEmpty ? 0 : dirX.GetValueAsInt());
                        int dirYVal = (null == dirY || dirY.IsEmpty ? 0 : dirY.GetValueAsInt());
                        int speedVal = (null == speed || speed.IsEmpty ? 0 : speed.GetValueAsInt());
                        int triggerTrackingIdVal = (null == triggerTrackingId || triggerTrackingId.IsEmpty ? 0 : triggerTrackingId.GetValueAsInt());

                        var trapDirMagSq = dirXVal * dirXVal + dirYVal * dirYVal;
                        var invTrapDirMag = InvSqrt32(trapDirMagSq);
                        var trapSpeedXfac = invTrapDirMag * dirXVal;
                        var trapSpeedYfac = invTrapDirMag * dirYVal;

                        int trapVelX = (int)(trapSpeedXfac * speedVal);
                        int trapVelY = (int)(trapSpeedYfac * speedVal);

                        bool lockedVal = (null != locked && !locked.IsEmpty && 1 == locked.GetValueAsInt()) ? true : false;
                        int unlockSubscriptionIdVal = (null == unlockSubscriptionId || unlockSubscriptionId.IsEmpty ? MAGIC_EVTSUB_ID_NONE : unlockSubscriptionId.GetValueAsInt());

                        TrapConfig trapConfig = trapConfigs[speciesIdVal];
                        TrapConfigFromTiled trapConfigFromTiled = new TrapConfigFromTiled {
                            SpeciesId = speciesIdVal,
                            Quota = MAGIC_QUOTA_INFINITE,
                            Speed = speedVal,
                            DirX = dirXVal,
                            DirY = dirYVal,
                            ProhibitsWallGrabbing = prohibitsWallGrabbingVal,
                            UnlockSubscriptionId = unlockSubscriptionIdVal, 
                        };

                        tileProps.TryGetCustomProperty("collisionTypeMask", out collisionTypeMask);
                        ulong collisionTypeMaskVal = (null != collisionTypeMask && !collisionTypeMask.IsEmpty) ? (ulong)collisionTypeMask.GetValueAsInt() : 0;

                        TrapColliderAttrArray colliderAttrs = new TrapColliderAttrArray();
                        if (isCompletelyStaticVal) {
                            var (tiledRectCx, tiledRectCy) = (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y + tileObj.m_Height * 0.5f);
                            var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                            var (rectVw, rectVh) = PolygonColliderCtrToVirtualGridPos(tileObj.m_Width, tileObj.m_Height);
                            var (rectCenterVx, rectCenterVy) = PolygonColliderCtrToVirtualGridPos(rectCx, rectCy);

                            Trap trap = new Trap {
                                TrapLocalId = trapLocalId,
                                Config = trapConfig,
                                ConfigFromTiled = trapConfigFromTiled,
                                VirtualGridX = rectCenterVx,
                                VirtualGridY = rectCenterVy,
                                DirX = dirXVal,
                                DirY = dirYVal,
                                VelX = trapVelX,
                                VelY = trapVelY,
                                TriggerTrackingId = triggerTrackingIdVal,
                                IsCompletelyStatic = true,
                                Locked = lockedVal,
                            };

                            TrapColliderAttr colliderAttr = new TrapColliderAttr {
                                ProvidesDamage = providesDamageVal,
                                ProvidesHardPushback = providesHardPushbackVal,
                                ProvidesEscape = providesEscapeVal,
                                ProvidesSlipJump = providesSlipJumpVal,
                                ForcesCrouching = forcesCrouchingVal,
                                HitboxOffsetX = 0,
                                HitboxOffsetY = 0,
                                HitboxSizeX = rectVw,
                                HitboxSizeY = rectVh,
                                CollisionTypeMask = collisionTypeMaskVal,
                                TrapLocalId = trapLocalId
                            };

                            colliderAttrs.List.Add(colliderAttr); // [WARNING] A single completely static trap only supports 1 colliderAttr for now.
                            serializedTrapLocalIdToColliderAttrs.Dict[trapLocalId] = colliderAttrs;

                            var srcPolygon = NewRectPolygon(rectCx, rectCy, tileObj.m_Width, tileObj.m_Height, 0, 0, 0, 0);
                            serializedCompletelyStaticTraps.Add(new SerializedCompletelyStaticTrapCollider {
                                Polygon = srcPolygon.Serialize(),
                                Attr = colliderAttr,
                            });

                            trapList.Add(trap);
                            if (0 != trap.TriggerTrackingId) {
                                serializedTriggerTrackingIdToTrapLocalId.Dict[trap.TriggerTrackingId] = trap.TrapLocalId;
                            }
                            trapLocalId++;
                            // Debug.Log(String.Format("new completely static trap created {0}", trap));
                        } else {
                            var (tiledRectCx, tiledRectCy) = (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y - tileObj.m_Height * 0.5f);
                            var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                            var (rectCenterVx, rectCenterVy) = PolygonColliderCtrToVirtualGridPos(rectCx, rectCy);
                            Trap trap = new Trap {
                                TrapLocalId = trapLocalId,
                                Config = trapConfig,
                                ConfigFromTiled = trapConfigFromTiled,
                                VirtualGridX = rectCenterVx,
                                VirtualGridY = rectCenterVy,
                                DirX = dirXVal,
                                DirY = dirYVal,
                                VelX = trapVelX,
                                VelY = trapVelY,
                                TriggerTrackingId = triggerTrackingIdVal,
                                IsCompletelyStatic = false, 
                                Locked = lockedVal,
                            };
                            if (null != tileObj.m_SuperTile && null != tileObj.m_SuperTile.m_CollisionObjects) {
                                var collisionObjs = tileObj.m_SuperTile.m_CollisionObjects;
                                foreach (var collisionObj in collisionObjs) {
                                    bool childProvidesHardPushbackVal = false, childProvidesDamageVal = false, childProvidesEscapeVal = false, childProvidesSlipJumpVal = false;
                                    foreach (var collisionObjProp in collisionObj.m_CustomProperties) {
                                        if ("providesHardPushback".Equals(collisionObjProp.m_Name)) {
                                            childProvidesHardPushbackVal = (!collisionObjProp.IsEmpty && 1 == collisionObjProp.GetValueAsInt());
                                        }
                                        if ("providesDamage".Equals(collisionObjProp.m_Name)) {
                                            childProvidesDamageVal = (!collisionObjProp.IsEmpty && 1 == collisionObjProp.GetValueAsInt());
                                        }
                                        if ("providesEscape".Equals(collisionObjProp.m_Name)) {
                                            childProvidesEscapeVal = (!collisionObjProp.IsEmpty && 1 == collisionObjProp.GetValueAsInt());
                                        }
                                        if ("providesSlipJump".Equals(collisionObjProp.m_Name)) {
                                            childProvidesSlipJumpVal = (!collisionObjProp.IsEmpty && 1 == collisionObjProp.GetValueAsInt());
                                        }
                                        if ("collisionTypeMask".Equals(collisionObjProp.m_Name) && !collisionObjProp.IsEmpty) {
                                            collisionTypeMaskVal = (ulong)collisionObjProp.GetValueAsInt();
                                        }
                                    }

                                    // [WARNING] The offset (0, 0) of the tileObj within TSX is the top-left corner, but SuperTiled2Unity converted that to bottom-left corner and reverted y-axis by itself... 
                                    var (hitboxOffsetCx, hitboxOffsetCy) = (-tileObj.m_Width * 0.5f + collisionObj.m_Position.x + collisionObj.m_Size.x * 0.5f, collisionObj.m_Position.y - collisionObj.m_Size.y * 0.5f - tileObj.m_Height * 0.5f);
                                    var (hitboxOffsetVx, hitboxOffsetVy) = PolygonColliderCtrToVirtualGridPos(hitboxOffsetCx, hitboxOffsetCy);
                                    var (hitboxSizeVx, hitboxSizeVy) = PolygonColliderCtrToVirtualGridPos(collisionObj.m_Size.x, collisionObj.m_Size.y);
                                    TrapColliderAttr colliderAttr = new TrapColliderAttr {
                                        ProvidesDamage = childProvidesDamageVal,
                                        ProvidesHardPushback = childProvidesHardPushbackVal,
                                        ProvidesEscape = childProvidesEscapeVal,
                                        ProvidesSlipJump = childProvidesSlipJumpVal,
                                        HitboxOffsetX = hitboxOffsetVx,
                                        HitboxOffsetY = hitboxOffsetVy,
                                        HitboxSizeX = hitboxSizeVx,
                                        HitboxSizeY = hitboxSizeVy,
                                        CollisionTypeMask = collisionTypeMaskVal,
                                        TrapLocalId = trapLocalId
                                    };
                                    colliderAttrs.List.Add(colliderAttr);
                                }
                            }
                            serializedTrapLocalIdToColliderAttrs.Dict[trapLocalId] = colliderAttrs;
                            trapList.Add(trap);
                            if (0 != trap.TriggerTrackingId) {
                                serializedTriggerTrackingIdToTrapLocalId.Dict[trap.TriggerTrackingId] = trap.TrapLocalId;
                            }
                            trapLocalId++;
                            Destroy(trapChild.gameObject); // [WARNING] It'll be animated by "TrapPrefab" in "applyRoomDownsyncFrame" instead!
                        }
                    }
                    Destroy(child.gameObject); // Delete the whole "ObjectLayer"
                    break;
                case "TriggerPos":
                    foreach (Transform triggerChild in child) {
                        var tileObj = triggerChild.GetComponent<SuperObject>();
                        var tileProps = triggerChild.GetComponent<SuperCustomProperties>();
                        CustomProperty bulletTeamId, chCollisionTeamId, delayedFrames, initVelX, initVelY, quota, recoveryFrames, speciesId, trackingIdList, subCycleTriggerFrames, subCycleQuota, characterSpawnerTimeSeq, publishingToEvtSubIdUponExhaust, publishingEvtMaskUponExhaust, subscriptionId, storyPointId, locked, unlockSubscriptionId, supplementDemandedEvtMask;
                        tileProps.TryGetCustomProperty("bulletTeamId", out bulletTeamId);
                        tileProps.TryGetCustomProperty("chCollisionTeamId", out chCollisionTeamId);
                        tileProps.TryGetCustomProperty("delayedFrames", out delayedFrames);
                        tileProps.TryGetCustomProperty("initVelX", out initVelX);
                        tileProps.TryGetCustomProperty("initVelY", out initVelY);
                        tileProps.TryGetCustomProperty("quota", out quota);
                        tileProps.TryGetCustomProperty("recoveryFrames", out recoveryFrames);
                        tileProps.TryGetCustomProperty("speciesId", out speciesId);
                        tileProps.TryGetCustomProperty("trackingIdList", out trackingIdList);
                        tileProps.TryGetCustomProperty("subCycleTriggerFrames", out subCycleTriggerFrames);
                        tileProps.TryGetCustomProperty("subCycleQuota", out subCycleQuota);
                        tileProps.TryGetCustomProperty("characterSpawnerTimeSeq", out characterSpawnerTimeSeq);
                        tileProps.TryGetCustomProperty("publishingToEvtSubIdUponExhaust", out publishingToEvtSubIdUponExhaust);
                        tileProps.TryGetCustomProperty("publishingEvtMaskUponExhaust", out publishingEvtMaskUponExhaust);
                        tileProps.TryGetCustomProperty("subscriptionId", out subscriptionId);
                        tileProps.TryGetCustomProperty("storyPointId", out storyPointId);
                        tileProps.TryGetCustomProperty("locked", out locked);
                        tileProps.TryGetCustomProperty("unlockSubscriptionId", out unlockSubscriptionId);
                        tileProps.TryGetCustomProperty("supplementDemandedEvtMask", out supplementDemandedEvtMask);

                        int speciesIdVal = speciesId.GetValueAsInt(); // must have 
                        int bulletTeamIdVal = (null != bulletTeamId && !bulletTeamId.IsEmpty ? bulletTeamId.GetValueAsInt() : 0);
                        int chCollisionTeamIdVal = (null != chCollisionTeamId && !chCollisionTeamId.IsEmpty ? chCollisionTeamId.GetValueAsInt() : 0);
                        int delayedFramesVal = (null != delayedFrames && !delayedFrames.IsEmpty ? delayedFrames.GetValueAsInt() : 0);
                        int initVelXVal = (null != initVelX && !initVelX.IsEmpty ? initVelX.GetValueAsInt() : 0);
                        int initVelYVal = (null != initVelY && !initVelY.IsEmpty ? initVelY.GetValueAsInt() : 0);
                        int quotaVal = (null != quota && !quota.IsEmpty ? quota.GetValueAsInt() : 1);
                        int recoveryFramesVal = (null != recoveryFrames && !recoveryFrames.IsEmpty ? recoveryFrames.GetValueAsInt() : delayedFramesVal + 1); // By default we must have "recoveryFramesVal > delayedFramesVal"
                        var trackingIdListStr = (null != trackingIdList && !trackingIdList.IsEmpty ? trackingIdList.GetValueAsString() : "");
                        int subCycleTriggerFramesVal = (null != subCycleTriggerFrames && !subCycleTriggerFrames.IsEmpty ? subCycleTriggerFrames.GetValueAsInt() : 0);
                        int subCycleQuotaVal = (null != subCycleQuota && !subCycleQuota.IsEmpty ? subCycleQuota.GetValueAsInt() : 0);
                        var characterSpawnerTimeSeqStr = (null != characterSpawnerTimeSeq && !characterSpawnerTimeSeq.IsEmpty ? characterSpawnerTimeSeq.GetValueAsString() : "");
                        int publishingToEvtSubIdUponExhaustVal = (null != publishingToEvtSubIdUponExhaust && !publishingToEvtSubIdUponExhaust.IsEmpty ? publishingToEvtSubIdUponExhaust.GetValueAsInt() : MAGIC_EVTSUB_ID_NONE);
                        ulong publishingEvtMaskUponExhaustVal = (null != publishingEvtMaskUponExhaust && !publishingEvtMaskUponExhaust.IsEmpty ? (ulong)publishingEvtMaskUponExhaust.GetValueAsInt() : 0ul);
                        int subscriptionIdVal = (null != subscriptionId && !subscriptionId.IsEmpty ? subscriptionId.GetValueAsInt() : MAGIC_EVTSUB_ID_NONE);
                        int storyPointIdVal = (null != storyPointId && !storyPointId.IsEmpty ? storyPointId.GetValueAsInt() : STORY_POINT_NONE);

                        bool lockedVal = (null != locked && !locked.IsEmpty && 1 == locked.GetValueAsInt()) ? true : false;
                        int unlockSubscriptionIdVal = (null == unlockSubscriptionId || unlockSubscriptionId.IsEmpty ? MAGIC_EVTSUB_ID_NONE : unlockSubscriptionId.GetValueAsInt());
                        ulong supplementDemandedEvtMaskVal = (null != supplementDemandedEvtMask && !supplementDemandedEvtMask.IsEmpty ? (ulong)supplementDemandedEvtMask.GetValueAsInt() : 0ul);

                        var triggerConfig = triggerConfigs[speciesIdVal];
                        var trigger = new Trigger {
                            TriggerLocalId = triggerLocalId,
                            BulletTeamId = bulletTeamIdVal,
                            Quota = (TRIGGER_MASK_BY_CYCLIC_TIMER == triggerConfig.TriggerMask
                                    ?
                                    quotaVal - 1 // The first quota will be spent right at "delayedFramesVal"
                                    :
                                    quotaVal),
                            State = TriggerState.Tready,
                            SubCycleQuotaLeft = subCycleQuotaVal,
                            FramesToFire = (TRIGGER_MASK_BY_CYCLIC_TIMER == triggerConfig.TriggerMask ? delayedFramesVal : MAX_INT),
                            FramesToRecover = (TRIGGER_MASK_BY_CYCLIC_TIMER == triggerConfig.TriggerMask ? delayedFramesVal + recoveryFramesVal : 0),
                            Config = triggerConfig,
                            ConfigFromTiled = new TriggerConfigFromTiled {
                                SpeciesId = speciesIdVal,
                                ChCollisionTeamId = chCollisionTeamIdVal,
                                DelayedFrames = delayedFramesVal,
                                RecoveryFrames = recoveryFramesVal,
                                InitVelX = initVelXVal,
                                InitVelY = initVelYVal,
                                SubCycleTriggerFrames = subCycleTriggerFramesVal,
                                SubCycleQuota = subCycleQuotaVal,
                                QuotaCap = quotaVal,
                                PublishingToEvtSubIdUponExhaust = publishingToEvtSubIdUponExhaustVal,
                                PublishingEvtMaskUponExhaust = publishingEvtMaskUponExhaustVal,
                                SubscriptionId = subscriptionIdVal,
                                StoryPointId = storyPointIdVal,
                                UnlockSubscriptionId = unlockSubscriptionIdVal,
                                SupplementDemandedEvtMask = supplementDemandedEvtMaskVal,
                            },
                            Locked = lockedVal,
                        };

                        string[] trackingIdListStrParts = trackingIdListStr.Split(',');
                        foreach (var trackingIdListStrPart in trackingIdListStrParts) {
                            if (String.IsNullOrEmpty(trackingIdListStrPart)) continue;
                            trigger.ConfigFromTiled.TrackingIdList.Add(trackingIdListStrPart.ToInt());
                        }
                        string[] characterSpawnerTimeSeqStrParts = characterSpawnerTimeSeqStr.Split(';');
                        foreach (var part in characterSpawnerTimeSeqStrParts) {
                            if (String.IsNullOrEmpty(part)) continue;
                            string[] subParts = part.Split(':');
                            if (2 != subParts.Length) continue;
                            if (String.IsNullOrEmpty(subParts[0])) continue;
                            if (String.IsNullOrEmpty(subParts[1])) continue;
                            int cutoffRdfFrameId = subParts[0].ToInt();
                            var chSpawnerConfig = new CharacterSpawnerConfig {
                                CutoffRdfFrameId = cutoffRdfFrameId
                            };
                            string[] speciesIdParts = subParts[1].Split(',');
                            foreach (var speciesIdPart in speciesIdParts) {
                                chSpawnerConfig.SpeciesIdList.Add(speciesIdPart.ToInt());
                            }
                            trigger.ConfigFromTiled.CharacterSpawnerTimeSeq.Add(chSpawnerConfig);
                        }

                        var (tiledRectCx, tiledRectCy) = (StoryPoint.SpeciesId == speciesIdVal || WaveInducer.SpeciesId == speciesIdVal) ? (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y + tileObj.m_Height * 0.5f) : (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y - tileObj.m_Height * 0.5f);

                        var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                        var (rectCenterVx, rectCenterVy) = PolygonColliderCtrToVirtualGridPos(rectCx, rectCy);
                        trigger.VirtualGridX = rectCenterVx;
                        trigger.VirtualGridY = rectCenterVy;
                        var (wx, wy) = CollisionSpacePositionToWorldPosition(rectCx, rectCy, spaceOffsetX, spaceOffsetY);
                        triggerList.Add((trigger, wx, wy));

                        var triggerColliderAttr = new TriggerColliderAttr {
                            TriggerLocalId = triggerLocalId
                        };
                        var srcPolygon = NewRectPolygon(rectCx, rectCy, tileObj.m_Width, tileObj.m_Height, 0, 0, 0, 0);
                        serializedStaticTriggers.Add(new SerializedCompletelyStaticTriggerCollider {
                            Polygon = srcPolygon.Serialize(),
                            Attr = triggerColliderAttr,
                        });
                        ++triggerLocalId;
                    }
                    Destroy(child.gameObject); // Delete the whole "ObjectLayer"
                    break;
                case "Pickable":
                    foreach (Transform pickableChild in child) {
                        var tileObj = pickableChild.GetComponent<SuperObject>();
                        var tileProps = pickableChild.GetComponent<SuperCustomProperties>();
                        CustomProperty consumableSpeciesId, pickupType, takesGravity;
                        tileProps.TryGetCustomProperty("consumableSpeciesId", out consumableSpeciesId);
                        tileProps.TryGetCustomProperty("pickupType", out pickupType);
                        tileProps.TryGetCustomProperty("takesGravity", out takesGravity);

                        int consumableSpeciesIdVal = consumableSpeciesId.GetValueAsInt(); 
                        PickupType pickupTypeVal = (
                            null != pickupType && !pickupType.IsEmpty 
                            ?  
                            ("PutIntoInventory" == pickupType.GetValueAsString() ? PickupType.PutIntoInventory : PickupType.Immediate)  
                            : 
                            PickupType.Immediate
                        );
                        bool takesGravityVal = (
                            null != takesGravity && !takesGravity.IsEmpty 
                            ?  
                            (1 == takesGravity.GetValueAsInt())  
                            : 
                            false
                        );

                        var (tiledRectCx, tiledRectCy) = (tileObj.m_X + tileObj.m_Width * 0.5f, tileObj.m_Y - tileObj.m_Height * 0.5f);

                        var (rectCx, rectCy) = TiledLayerPositionToCollisionSpacePosition(tiledRectCx, tiledRectCy, spaceOffsetX, spaceOffsetY);
                        var (rectCenterVx, rectCenterVy) = PolygonColliderCtrToVirtualGridPos(rectCx, rectCy);
                        var pickable = new Pickable {
                            PickableLocalId = pickableLocalId,
                            VirtualGridX = rectCenterVx,
                            VirtualGridY = rectCenterVy, 
                            VelY = 0,
                            RemainingLifetimeRdfCount = MAX_INT, // TODO: Read from the map
                            ConfigFromTiled = new PickableConfigFromTiled {
                                TakesGravity = takesGravityVal,
                                InitVirtualGridX = rectCenterVx,
                                InitVirtualGridY = rectCenterVy, 
                                BuffSpeciesId = TERMINATING_BUFF_SPECIES_ID,
                                ConsumableSpeciesId = consumableSpeciesIdVal,
                                PickupType = pickupTypeVal,
                            },
                        };
                        var (wx, wy) = CollisionSpacePositionToWorldPosition(rectCx, rectCy, spaceOffsetX, spaceOffsetY);
                        pickableList.Add((pickable, wx, wy));

                        ++pickableLocalId;
                    }
                    Destroy(child.gameObject); // Delete the whole "ObjectLayer"
                    break;
                default:
                    break;
            }
        }

        // Sorting to make sure that if "roomCapacity" is smaller than the position counts in Tiled, we take only the smaller teamIds
        playerStartingCposList.Sort(delegate ((Vector, int, int) lhs, (Vector, int, int) rhs) {
            return Math.Sign(lhs.Item2 - rhs.Item2);
        });

        evtSubList.Sort(delegate (EvtSubscription lhs, EvtSubscription rhs) {
            return Math.Sign(lhs.Id - rhs.Id);
        });

        var startRdf = NewPreallocatedRoomDownsyncFrame(roomCapacity, preallocNpcCapacity, preallocBulletCapacity, preallocTrapCapacity, preallocTriggerCapacity, preallocEvtSubCapacity, preallocPickableCapacity);
        historyRdfHolder = NewPreallocatedRoomDownsyncFrame(roomCapacity, preallocNpcCapacity, preallocBulletCapacity, preallocTrapCapacity, preallocTriggerCapacity, preallocEvtSubCapacity, preallocPickableCapacity);

        startRdf.Id = DOWNSYNC_MSG_ACT_BATTLE_START;
        startRdf.ShouldForceResync = false;
        for (int i = 0; i < roomCapacity; i++) {
            int joinIndex = i + 1;
            var (cpos, teamId, dirX) = playerStartingCposList[i];
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cpos.X, cpos.Y, spaceOffsetX, spaceOffsetY);
            teamId = (DEFAULT_BULLET_TEAM_ID == teamId ? joinIndex : teamId);
            var playerInRdf = startRdf.PlayersArr[i];
            playerInRdf.JoinIndex = joinIndex;
            playerInRdf.BulletTeamId = teamId;
            playerInRdf.ChCollisionTeamId = teamId; // If we want to stand on certain teammates' shoulder, then this value should be tuned accordingly. 

            var (playerVposX, playerVposY) = PolygonColliderCtrToVirtualGridPos(cpos.X, cpos.Y); // World and CollisionSpace coordinates have the same scale, just translated
            playerInRdf.VirtualGridX = playerVposX;
            playerInRdf.VirtualGridY = playerVposY;
            playerInRdf.RevivalVirtualGridX = playerVposX;
            playerInRdf.RevivalVirtualGridY = playerVposY;
            playerInRdf.RevivalDirX = dirX;
            playerInRdf.RevivalDirY = 0;
            playerInRdf.CharacterState = CharacterState.InAirIdle1NoJump;
            playerInRdf.FramesToRecover = 0;
            playerInRdf.DirX = dirX;
            playerInRdf.DirY = 0;
            playerInRdf.VelX = 0;
            playerInRdf.VelY = 0;
            playerInRdf.InAir = true;
            playerInRdf.OnWall = false;

            if (SPECIES_NONE_CH == speciesIdList[i]) continue;

            // Species specific
            playerInRdf.SpeciesId = speciesIdList[i];
            var chConfig = Battle.characters[playerInRdf.SpeciesId];
            playerInRdf.Hp = chConfig.Hp;
            playerInRdf.Mp = chConfig.Mp; 
            playerInRdf.Speed = chConfig.Speed;
            playerInRdf.OmitGravity = chConfig.OmitGravity;
            playerInRdf.OmitSoftPushback = chConfig.OmitSoftPushback;
            playerInRdf.RepelSoftPushback = chConfig.RepelSoftPushback;
            if (null != chConfig.InitInventorySlots) {
                for (int t = 0; t < chConfig.InitInventorySlots.Count; t++) {
                    var initIvSlot = chConfig.InitInventorySlots[t];
                    if (InventorySlotStockType.NoneIv == initIvSlot.StockType) break;
                    AssignToInventorySlot(initIvSlot.StockType, initIvSlot.Quota, initIvSlot.FramesToRecover, initIvSlot.DefaultQuota, initIvSlot.DefaultFramesToRecover, initIvSlot.BuffSpeciesId, initIvSlot.SkillId, playerInRdf.Inventory.Slots[t]);
                }
            }
            spawnPlayerNode(joinIndex, playerInRdf.SpeciesId, wx, wy, playerInRdf.BulletTeamId);
        }

        int npcLocalId = 0;
        for (int i = 0; i < npcsStartingCposList.Count; i++) {
            int joinIndex = roomCapacity + i + 1;
            var (cpos, dirX, dirY, characterSpeciesId, teamId, isStatic, publishingEvtSubIdUponKilledVal, publishingEvtMaskUponKilledVal, subscriptionId, killedToDropConsumableSpeciesId, killedToDropBuffSpeciesId) = npcsStartingCposList[i];
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cpos.X, cpos.Y, spaceOffsetX, spaceOffsetY);
            var chConfig = Battle.characters[characterSpeciesId];
            var npcInRdf = startRdf.NpcsArr[i];
            var (vx, vy) = PolygonColliderCtrToVirtualGridPos(cpos.X, cpos.Y);
            npcInRdf.Id = npcLocalId; // Just for not being excluded 
            npcInRdf.JoinIndex = joinIndex;
            npcInRdf.VirtualGridX = vx;
            npcInRdf.VirtualGridY = vy;
            npcInRdf.RevivalVirtualGridX = vx;
            npcInRdf.RevivalVirtualGridY = vy;
            npcInRdf.RevivalDirX = dirX;
            npcInRdf.RevivalDirY = dirY;
            npcInRdf.Speed = chConfig.Speed;
            npcInRdf.CharacterState = (chConfig.AntiGravityWhenIdle && 0 != dirX) ? CharacterState.Walking : CharacterState.InAirIdle1NoJump;
            if (MAGIC_EVTSUB_ID_NONE != subscriptionId && chConfig.HasDimmedAnim) {
                npcInRdf.CharacterState = CharacterState.Dimmed;
            }
            npcInRdf.FramesToRecover = 0;
            npcInRdf.DirX = dirX;
            npcInRdf.DirY = dirY;
            npcInRdf.VelX = 0;
            npcInRdf.VelY = 0;
            npcInRdf.InAir = true;
            npcInRdf.OnWall = false;
            npcInRdf.Hp = chConfig.Hp;
            npcInRdf.Mp = chConfig.Mp;
            npcInRdf.SpeciesId = characterSpeciesId;
            npcInRdf.BulletTeamId = teamId;
            npcInRdf.ChCollisionTeamId = teamId;
            npcInRdf.WaivingSpontaneousPatrol = isStatic;
            npcInRdf.OmitGravity = chConfig.OmitGravity;
            npcInRdf.OmitSoftPushback = chConfig.OmitSoftPushback;
            npcInRdf.RepelSoftPushback = chConfig.RepelSoftPushback;
            npcInRdf.PublishingEvtSubIdUponKilled = publishingEvtSubIdUponKilledVal;
            npcInRdf.PublishingEvtMaskUponKilled = publishingEvtMaskUponKilledVal;
            npcInRdf.SubscriptionId = subscriptionId;
            npcInRdf.KilledToDropConsumableSpeciesId = killedToDropConsumableSpeciesId;
            npcInRdf.KilledToDropBuffSpeciesId = killedToDropBuffSpeciesId;
            startRdf.NpcsArr[i] = npcInRdf;
            npcLocalId++;
        }
        startRdf.NpcLocalIdCounter = npcLocalId;

        for (int i = 0; i < trapList.Count; i++) {
            var trap = trapList[i];
            startRdf.TrapsArr[i] = trap;
            if (trap.IsCompletelyStatic) continue;
            var (cx, cy) = VirtualGridToPolygonColliderCtr(trap.VirtualGridX, trap.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cx, cy, spaceOffsetX, spaceOffsetY);
            spawnDynamicTrapNode(trap.Config.SpeciesId, wx, wy);
        }

        for (int i = 0; i < triggerList.Count; i++) {
            var (trigger, wx, wy) = triggerList[i];
            startRdf.TriggersArr[i] = trigger;
            spawnTriggerNode(trigger.TriggerLocalId, trigger.Config.SpeciesId, wx, wy);
        }

        for (int i = 0; i < pickableList.Count; i++) {
            var (pickable, wx, wy) = pickableList[i];
            startRdf.Pickables[i] = pickable;
        }
        startRdf.PickableLocalIdCounter = pickableLocalId;

        for (int i = 0; i < evtSubList.Count; i++) {
            startRdf.EvtSubsArr[i] = evtSubList[i];
        }
        
        if (0ul == startRdf.EvtSubsArr[MAGIC_EVTSUB_ID_WAVER - 1].DemandedEvtMask) {
            // Initialize trigger for the first wave if attr "DemandedEvtMask" was not set explicitly!
            startRdf.EvtSubsArr[MAGIC_EVTSUB_ID_WAVER - 1].DemandedEvtMask = EVTSUB_NO_DEMAND_MASK + 1;
            startRdf.EvtSubsArr[MAGIC_EVTSUB_ID_WAVER - 1].FulfilledEvtMask = EVTSUB_NO_DEMAND_MASK + 1;
        }

        return (startRdf, serializedBarrierPolygons, serializedStaticPatrolCues, serializedCompletelyStaticTraps, serializedStaticTriggers, serializedTrapLocalIdToColliderAttrs, serializedTriggerTrackingIdToTrapLocalId, battleDurationSecondsVal);
    }

    protected void popupErrStackPanel(string msg) {
        if (null == errStackLogPanelObj) {
            errStackLogPanelObj = Instantiate(errStackLogPanelPrefab, new Vector3(canvas.transform.position.x, canvas.transform.position.y, +5), Quaternion.identity, canvas.transform);
        }
        var errStackLogPanel = errStackLogPanelObj.GetComponent<ErrStackLogPanel>();
        errStackLogPanel.content.text = msg;
    }

    protected Vector2 camSpeedHolder = new Vector2();
    protected Vector2 camDiffDstHolder = new Vector2();
    protected void clampToMapBoundary(ref Vector3 posHolder) {
        float newX = posHolder.x, newY = posHolder.y, newZ = posHolder.z;
        if (newX > cameraCapMaxX) newX = cameraCapMaxX;
        if (newX < cameraCapMinX) newX = cameraCapMinX;
        if (newY > cameraCapMaxY) newY = cameraCapMaxY;
        if (newY < cameraCapMinY) newY = cameraCapMinY;
        posHolder.Set(newX, newY, newZ);
    }

    protected void cameraTrack(RoomDownsyncFrame rdf, RoomDownsyncFrame prevRdf, bool battleResultIsSet) {
        if (null == selfPlayerInfo) return;
        int targetJoinIndex = battleResultIsSet ? confirmedBattleResult.WinnerJoinIndex : selfPlayerInfo.JoinIndex;

        var playerGameObj = playerGameObjs[targetJoinIndex - 1];
        var playerCharacterDownsync = rdf.PlayersArr[targetJoinIndex - 1];

        var (velCX, velCY) = VirtualGridToPolygonColliderCtr(playerCharacterDownsync.Speed, playerCharacterDownsync.Speed);
        camSpeedHolder.Set(velCX, velCY);
        var cameraSpeedInWorld = camSpeedHolder.magnitude * 100;

        var prevPlayerCharacterDownsync = (null == prevRdf || null == prevRdf.PlayersArr) ? null : prevRdf.PlayersArr[targetJoinIndex - 1];
        if ((null != prevPlayerCharacterDownsync && CharacterState.Dying == prevPlayerCharacterDownsync.CharacterState) || battleResultIsSet) {
            cameraSpeedInWorld *= 200;
        }

        var camOldPos = Camera.main.transform.position;
        var dst = playerGameObj.transform.position;
        camDiffDstHolder.Set(dst.x - camOldPos.x, dst.y - camOldPos.y);

        //Debug.Log(String.Format("cameraTrack, camOldPos={0}, dst={1}, deltaTime={2}", camOldPos, dst, Time.deltaTime));
        var stepLength = Time.deltaTime * cameraSpeedInWorld;
        if (DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START == rdf.Id || DOWNSYNC_MSG_ACT_BATTLE_START == rdf.Id || stepLength > camDiffDstHolder.magnitude) {
            // Immediately teleport
            newPosHolder.Set(dst.x, dst.y, camOldPos.z);
        } else {
            var newMapPosDiff2 = camDiffDstHolder.normalized * stepLength;
            newPosHolder.Set(camOldPos.x + newMapPosDiff2.x, camOldPos.y + newMapPosDiff2.y, camOldPos.z);
        }
        clampToMapBoundary(ref newPosHolder);
        Camera.main.transform.position = newPosHolder;
    }

    protected void resetLine(DebugLine line) {
        newPosHolder.x = 0;
        newPosHolder.y = 0;
        line.transform.position = newPosHolder;
        line.GetPositions(debugDrawPositionsHolder);
        (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = (0, 0);
        (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = (0, 0);
        (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = (0, 0);
        (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = (0, 0);
        line.SetPositions(debugDrawPositionsHolder);
    }

    public void toggleDebugDrawingEnabled() {
        debugDrawingEnabled = !debugDrawingEnabled;
    }

    protected void urpDrawDebug() {
        if (ROOM_STATE_IN_BATTLE != battleState) {
            return;
        }
        for (int i = cachedLineRenderers.vals.StFrameId; i < cachedLineRenderers.vals.EdFrameId; i++) {
            var (res, line) = cachedLineRenderers.vals.GetByFrameId(i);
            if (!res || null == line) throw new ArgumentNullException(String.Format("There's no line for i={0}, while StFrameId={1}, EdFrameId={2}", i, cachedLineRenderers.vals.StFrameId, cachedLineRenderers.vals.EdFrameId));

            resetLine(line);
        }
        if (!debugDrawingEnabled) {
            return;
        }
        var (_, rdf) = renderBuffer.GetByFrameId(playerRdfId);
        if (null == rdf) return;

        // Draw static colliders
        int lineIndex = 0;
        for (int s = 0; s < staticCollidersCnt; s++) {
            var collider = staticColliders[s];
            if (null == collider) {
                break;
            }
            if (null == collider.Shape) {
                throw new ArgumentNullException("barrierCollider.Shape is null when drawing staticRectangleColliders");
            }
            if (null == collider.Shape.Points) {
                throw new ArgumentNullException("barrierCollider.Shape.Points is null when drawing staticRectangleColliders");
            }

            var (wx, wy) = CollisionSpacePositionToWorldPosition(collider.X, collider.Y, spaceOffsetX, spaceOffsetY); ;
            newPosHolder.Set(wx, wy, 0);
            if (!isGameObjPositionWithinCamera(newPosHolder)) {
                continue; // To save memory
            }

            string key = "Static-" + lineIndex.ToString();
            lineIndex++;
            var line = cachedLineRenderers.PopAny(key);
            if (null == line) {
                line = cachedLineRenderers.Pop();
            }
            if (null == line) {
                throw new ArgumentNullException("Cached line is null for key:" + key);
            }
            line.SetColor(Color.white);
            if (null != collider.Data) {
#nullable enable
                TrapColliderAttr? trapColliderAttr = collider.Data as TrapColliderAttr;
                if (null != trapColliderAttr) {
                    if (trapColliderAttr.ProvidesHardPushback) {
                        line.SetColor(Color.green);
                    } else if (trapColliderAttr.ProvidesDamage) {
                        line.SetColor(Color.red);
                    }
                } else {
                    TriggerColliderAttr? triggerColliderAttr = collider.Data as TriggerColliderAttr;
                    if (null != triggerColliderAttr) {
                        var trigger = rdf.TriggersArr[triggerColliderAttr.TriggerLocalId];
                        if (0 < (TRIGGER_MASK_BY_MOVEMENT & trigger.Config.TriggerMask)) {
                            line.SetColor(Color.magenta);
                        } else if (0 < (TRIGGER_MASK_BY_ATK & trigger.Config.TriggerMask)) {
                            line.SetColor(Color.cyan);
                        }
                    }
                }
#nullable disable
            }
            int m = collider.Shape.Points.Cnt;
            line.GetPositions(debugDrawPositionsHolder);
            for (int i = 0; i < 4; i++) {
                int effI = (i >= m ? m - 1 : i);
                var (_, pi) = collider.Shape.Points.GetByOffset(i);
                (debugDrawPositionsHolder[i].x, debugDrawPositionsHolder[i].y) = CollisionSpacePositionToWorldPosition(collider.X + pi.X, collider.Y + pi.Y, spaceOffsetX, spaceOffsetY);
            }
            line.SetPositions(debugDrawPositionsHolder);
            line.score = rdf.Id;
            cachedLineRenderers.Put(key, line);
        }

        // Draw dynamic colliders
        for (int k = 0; k < roomCapacity; k++) {
            var currCharacterDownsync = rdf.PlayersArr[k];
            var chConfig = characters[currCharacterDownsync.SpeciesId];
            float boxCx, boxCy, boxCw, boxCh;
            calcCharacterBoundingBoxInCollisionSpace(currCharacterDownsync, chConfig, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);
            newPosHolder.Set(wx, wy, 0);
            if (!isGameObjPositionWithinCamera(newPosHolder)) {
                continue; // To save memory
            }

            string key = "Player-" + currCharacterDownsync.JoinIndex.ToString();
            var line = cachedLineRenderers.PopAny(key);
            if (null == line) {
                line = cachedLineRenderers.Pop();
            }
            if (null == line) {
                throw new ArgumentNullException("Cached line is null for key:" + key);
            }
            line.SetColor(Color.white);
            line.GetPositions(debugDrawPositionsHolder);

            // World space width and height are just the same as that of collision space.

            (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = ((wx - 0.5f * boxCw), (wy - 0.5f * boxCh));
            (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = ((wx + 0.5f * boxCw), (wy - 0.5f * boxCh));
            (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = ((wx + 0.5f * boxCw), (wy + 0.5f * boxCh));
            (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = ((wx - 0.5f * boxCw), (wy + 0.5f * boxCh));
            line.SetPositions(debugDrawPositionsHolder);
            line.score = rdf.Id;
            cachedLineRenderers.Put(key, line);
        }

        for (int k = 0; k < rdf.NpcsArr.Count; k++) {
            var currCharacterDownsync = rdf.NpcsArr[k];
            if (TERMINATING_PLAYER_ID == currCharacterDownsync.Id) break;
            var chConfig = characters[currCharacterDownsync.SpeciesId];
            float boxCx, boxCy, boxCw, boxCh;
            calcCharacterBoundingBoxInCollisionSpace(currCharacterDownsync, chConfig, currCharacterDownsync.VirtualGridX, currCharacterDownsync.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);

            var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);
            newPosHolder.Set(wx, wy, 0);
            if (!isGameObjPositionWithinCamera(newPosHolder)) {
                continue; // To save memory
            }

            string key = "Npc-" + currCharacterDownsync.JoinIndex.ToString();
            var line = cachedLineRenderers.PopAny(key);
            if (null == line) {
                line = cachedLineRenderers.Pop();
            }
            if (null == line) {
                throw new ArgumentNullException("Cached line is null for key:" + key);
            }
            line.SetColor(Color.gray);
            line.GetPositions(debugDrawPositionsHolder);

            (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = ((wx - 0.5f * boxCw), (wy - 0.5f * boxCh));
            (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = ((wx + 0.5f * boxCw), (wy - 0.5f * boxCh));
            (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = ((wx + 0.5f * boxCw), (wy + 0.5f * boxCh));
            (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = ((wx - 0.5f * boxCw), (wy + 0.5f * boxCh));
            line.SetPositions(debugDrawPositionsHolder);
            line.score = rdf.Id;
            cachedLineRenderers.Put(key, line);

            string keyVision = "NpcVision-" + currCharacterDownsync.JoinIndex.ToString();
            var lineVision = cachedLineRenderers.PopAny(keyVision);
            if (null == lineVision) {
                lineVision = cachedLineRenderers.Pop();
            }
            if (null == lineVision) {
                throw new ArgumentNullException("Cached line is null for keyVision:" + keyVision);
            }
            lineVision.SetColor(Color.yellow);
            lineVision.GetPositions(debugDrawPositionsHolder);
            float visionCx, visionCy, visionCw, visionCh;
            calcNpcVisionBoxInCollisionSpace(currCharacterDownsync, chConfig, out visionCx, out visionCy, out visionCw, out visionCh);
            (wx, wy) = CollisionSpacePositionToWorldPosition(visionCx, visionCy, spaceOffsetX, spaceOffsetY);

            (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = ((wx - 0.5f * visionCw), (wy - 0.5f * visionCh));
            (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = ((wx + 0.5f * visionCw), (wy - 0.5f * visionCh));
            (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = ((wx + 0.5f * visionCw), (wy + 0.5f * visionCh));
            (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = ((wx - 0.5f * visionCw), (wy + 0.5f * visionCh));
            lineVision.SetPositions(debugDrawPositionsHolder);
            lineVision.score = rdf.Id;
            cachedLineRenderers.Put(keyVision, lineVision);
        }

        for (int k = 0; k < rdf.Bullets.Count; k++) {
            var bullet = rdf.Bullets[k];
            if (TERMINATING_BULLET_LOCAL_ID == bullet.BattleAttr.BulletLocalId) break;
            var (cx, cy) = VirtualGridToPolygonColliderCtr(bullet.VirtualGridX, bullet.VirtualGridY);
            var (wx, wy) = CollisionSpacePositionToWorldPosition(cx, cy, spaceOffsetX, spaceOffsetY); ;
            newPosHolder.Set(wx, wy, 0);
            if (!isGameObjPositionWithinCamera(newPosHolder)) {
                continue; // To save memory
            }

            string key = "Bullet-" + bullet.BattleAttr.BulletLocalId.ToString();
            var line = cachedLineRenderers.PopAny(key);
            if (null == line) {
                line = cachedLineRenderers.Pop();
            }
            if (null == line) {
                throw new ArgumentNullException("Cached line is null for key:" + key);
            }
            if (!IsBulletActive(bullet, rdf.Id)) {
                cachedLineRenderers.Put(key, line);
                continue;
            }
            line.SetColor(Color.red);
            line.GetPositions(debugDrawPositionsHolder);

            var (boxCw, boxCh) = VirtualGridToPolygonColliderCtr(bullet.Config.HitboxSizeX + bullet.Config.HitboxSizeIncX * bullet.FramesInBlState, bullet.Config.HitboxSizeY + bullet.Config.HitboxSizeIncY * bullet.FramesInBlState);
            (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = ((wx - 0.5f * boxCw), (wy - 0.5f * boxCh));
            (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = ((wx + 0.5f * boxCw), (wy - 0.5f * boxCh));
            (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = ((wx + 0.5f * boxCw), (wy + 0.5f * boxCh));
            (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = ((wx - 0.5f * boxCw), (wy + 0.5f * boxCh));

            // Debug.Log("Active Bullet " + bullet.BattleAttr.BulletLocalId.ToString() + ": wx=" + wx.ToString() + ", wy=" + wy.ToString() + ", boxCw=" + boxCw.ToString() + ", boxCh=" + boxCh.ToString());
            line.SetPositions(debugDrawPositionsHolder);
            line.score = rdf.Id;
            cachedLineRenderers.Put(key, line);
        }

        for (int i = 0; i < rdf.TrapsArr.Count; i++) {
            var currTrap = rdf.TrapsArr[i];
            if (TERMINATING_TRAP_ID == currTrap.TrapLocalId) continue;
            if (currTrap.IsCompletelyStatic) continue;

            List<TrapColliderAttr> colliderAttrs = trapLocalIdToColliderAttrs[currTrap.TrapLocalId];
            foreach (var colliderAttr in colliderAttrs) {
                float boxCx, boxCy, boxCw, boxCh;
                calcTrapBoxInCollisionSpace(colliderAttr, currTrap.VirtualGridX, currTrap.VirtualGridY, out boxCx, out boxCy, out boxCw, out boxCh);
                var (wx, wy) = CollisionSpacePositionToWorldPosition(boxCx, boxCy, spaceOffsetX, spaceOffsetY);
                newPosHolder.Set(wx, wy, 0);
                if (!isGameObjPositionWithinCamera(newPosHolder)) {
                    continue; // To save memory
                }

                string key = "DynamicTrap-" + currTrap.TrapLocalId.ToString() + "-" + colliderAttr.ProvidesDamage; // TODO: Use a collider ID for the last part
                var line = cachedLineRenderers.PopAny(key);
                if (null == line) {
                    line = cachedLineRenderers.Pop();
                }
                if (null == line) {
                    throw new ArgumentNullException("Cached line is null for key:" + key);
                }
                if (colliderAttr.ProvidesHardPushback) {
                    line.SetColor(Color.green);
                } else if (colliderAttr.ProvidesDamage) {
                    line.SetColor(Color.red);
                }

                line.GetPositions(debugDrawPositionsHolder);

                (debugDrawPositionsHolder[0].x, debugDrawPositionsHolder[0].y) = ((wx - 0.5f * boxCw), (wy - 0.5f * boxCh));
                (debugDrawPositionsHolder[1].x, debugDrawPositionsHolder[1].y) = ((wx + 0.5f * boxCw), (wy - 0.5f * boxCh));
                (debugDrawPositionsHolder[2].x, debugDrawPositionsHolder[2].y) = ((wx + 0.5f * boxCw), (wy + 0.5f * boxCh));
                (debugDrawPositionsHolder[3].x, debugDrawPositionsHolder[3].y) = ((wx - 0.5f * boxCw), (wy + 0.5f * boxCh));

                line.SetPositions(debugDrawPositionsHolder);
                line.score = rdf.Id;
                cachedLineRenderers.Put(key, line);
            }
        }
    }

    public bool isGameObjPositionWithinCamera(Vector3 positionHolder) {
        var posInMainCamViewport = Camera.main.WorldToViewportPoint(positionHolder);
        return (0f <= posInMainCamViewport.x && posInMainCamViewport.x <= 1f && 0f <= posInMainCamViewport.y && posInMainCamViewport.y <= 1f && 0f < posInMainCamViewport.z);
    }

    public bool isGameObjWithinCamera(GameObject obj) {
        return isGameObjPositionWithinCamera(obj.transform.position);
    }

    public void showTeamRibbonAndInplaceHpBar(int rdfId, CharacterDownsync currCharacterDownsync, float wx, float wy, float halfBoxCw, float halfBoxCh, string lookupKey) {
        var teamRibbon = cachedTeamRibbons.PopAny(lookupKey);
        if (null == teamRibbon) {
            teamRibbon = cachedTeamRibbons.Pop();
        }

        if (null == teamRibbon) {
            throw new ArgumentNullException(String.Format("No available teamRibbon node for lookupKey={0}", lookupKey));
        }

        newPosHolder.Set(wx + teamRibbonOffset.x, wy + halfBoxCh + teamRibbonOffset.y, inplaceHpBarZ);
        teamRibbon.gameObject.transform.position = newPosHolder;
        teamRibbon.score = rdfId;
        teamRibbon.setBulletTeamId(currCharacterDownsync.BulletTeamId);
        cachedTeamRibbons.Put(lookupKey, teamRibbon);

        var hpBar = cachedHpBars.PopAny(lookupKey);
        if (null == hpBar) {
            hpBar = cachedHpBars.Pop();
        }

        if (null == hpBar) {
            throw new ArgumentNullException(String.Format("No available hpBar node for lookupKey={0}", lookupKey));
        }
        var chConfig = characters[currCharacterDownsync.SpeciesId];
        hpBar.score = rdfId;
        hpBar.updateHp((float)currCharacterDownsync.Hp / chConfig.Hp, (float)currCharacterDownsync.Mp / chConfig.Mp);
        newPosHolder.Set(wx + inplaceHpBarOffset.x, wy + halfBoxCh + inplaceHpBarOffset.y, inplaceHpBarZ);
        hpBar.gameObject.transform.position = newPosHolder;
        cachedHpBars.Put(lookupKey, hpBar);
    }

    public bool playCharacterDamagedVfx(CharacterDownsync currCharacterDownsync, CharacterConfig chConfig, CharacterDownsync prevCharacterDownsync, GameObject theGameObj, CharacterAnimController chAnimCtrl) {
        var spr = theGameObj.GetComponent<SpriteRenderer>();
        var material = spr.material;
        material.SetFloat("_CrackOpacity", 0f);

        if (null != currCharacterDownsync.DebuffList) {
            for (int i = 0; i < currCharacterDownsync.DebuffList.Count; i++) {
                Debuff debuff = currCharacterDownsync.DebuffList[i];
                if (TERMINATING_DEBUFF_SPECIES_ID == debuff.SpeciesId) break;
                var debuffConfig = debuffConfigs[debuff.SpeciesId];
                switch (debuffConfig.Type) {
                    case DebuffType.FrozenPositionLocked:
                        if (0 < debuff.Stock) {
                            material.SetFloat("_CrackOpacity", 0.75f);
                            CharacterState overwriteChState = currCharacterDownsync.CharacterState;
                            if (!noOpSet.Contains(overwriteChState)) {
                                overwriteChState = CharacterState.Atked1;
                            }
                            chAnimCtrl.updateCharacterAnim(currCharacterDownsync, overwriteChState, prevCharacterDownsync, false, chConfig);
                        }
                        break;
                }
            }
        }

        return true;
    }

    public bool playCharacterSfx(CharacterDownsync currCharacterDownsync, CharacterDownsync prevCharacterDownsync, CharacterConfig chConfig, float wx, float wy, int rdfId, float distanceAttenuationZ) {
        // Cope with footstep sounds first
        if (CharacterState.Walking == currCharacterDownsync.CharacterState || CharacterState.WalkingAtk1 == currCharacterDownsync.CharacterState) {
            bool usingSameAudSrc = true;
            string ftSfxLookupKey = "ch-ft-" + currCharacterDownsync.JoinIndex.ToString();
            var ftSfxSourceHolder = cachedSfxNodes.PopAny(ftSfxLookupKey);
            if (null == ftSfxSourceHolder) {
                ftSfxSourceHolder = cachedSfxNodes.Pop();
                usingSameAudSrc = false;
            }

            if (null == ftSfxSourceHolder) {
                return false;
                // throw new ArgumentNullException(String.Format("No available ftSfxSourceHolder node for ftSfxLookupKey={0}", ftSfxLookupKey));
            }

            try {
                var clipName = calcFootStepSfxName(currCharacterDownsync);
                if (null == clipName) {
                    return false;
                }
                if (!ftSfxSourceHolder.audioClipDict.ContainsKey(clipName)) {
                    return false;
                }

                float totAttZ = distanceAttenuationZ + footstepAttenuationZ;
                newPosHolder.Set(wx, wy, totAttZ);
                ftSfxSourceHolder.gameObject.transform.position = newPosHolder;
                if (!usingSameAudSrc || !ftSfxSourceHolder.audioSource.isPlaying) {
                    ftSfxSourceHolder.audioSource.volume = calcSfxVolume(ftSfxSourceHolder, totAttZ);
                    ftSfxSourceHolder.audioSource.PlayOneShot(ftSfxSourceHolder.audioClipDict[clipName]);
                }
                ftSfxSourceHolder.score = rdfId;
            } finally {
                cachedSfxNodes.Put(ftSfxLookupKey, ftSfxSourceHolder);
            }
        }

        bool isInitialFrame = (0 == currCharacterDownsync.FramesInChState);
        if (!isInitialFrame) {
            return false;
        }

        if (!skills.ContainsKey(currCharacterDownsync.ActiveSkillId)) return false;
        var currSkillConfig = skills[currCharacterDownsync.ActiveSkillId];
        if (0 > currCharacterDownsync.ActiveSkillHit || currSkillConfig.Hits.Count <= currCharacterDownsync.ActiveSkillHit) return false;
        var currBulletConfig = currSkillConfig.Hits[currCharacterDownsync.ActiveSkillHit];
        if (null == currBulletConfig || null == currBulletConfig.CharacterEmitSfxName || currBulletConfig.CharacterEmitSfxName.IsEmpty()) return false;

        string sfxLookupKey = "ch-emit-" + currCharacterDownsync.JoinIndex.ToString();
        var sfxSourceHolder = cachedSfxNodes.PopAny(sfxLookupKey);
        if (null == sfxSourceHolder) {
            sfxSourceHolder = cachedSfxNodes.Pop();
        }

        if (null == sfxSourceHolder) {
            return false;
            // throw new ArgumentNullException(String.Format("No available sfxSourceHolder node for sfxLookupKey={0}", sfxLookupKey));
        }

        try {
            string clipName = currBulletConfig.CharacterEmitSfxName;
            if (null == clipName) {
                return false;
            }
            if (!sfxSourceHolder.audioClipDict.ContainsKey(clipName)) {
                return false;
            }

            newPosHolder.Set(wx, wy, distanceAttenuationZ);
            sfxSourceHolder.gameObject.transform.position = newPosHolder;
            sfxSourceHolder.audioSource.volume = calcSfxVolume(sfxSourceHolder, distanceAttenuationZ);
            sfxSourceHolder.audioSource.PlayOneShot(sfxSourceHolder.audioClipDict[clipName]);
            sfxSourceHolder.score = rdfId;
        } finally {
            cachedSfxNodes.Put(sfxLookupKey, sfxSourceHolder);
        }

        return true;
    }

    public bool playCharacterVfx(CharacterDownsync currCharacterDownsync, CharacterDownsync prevCharacterDownsync, CharacterConfig chConfig, float wx, float wy, int rdfId) {
        return true;
    }

    public bool playBulletSfx(Bullet bullet, bool isExploding, float wx, float wy, int rdfId, float distanceAttenuationZ) {
        // Play "ActiveSfx" if configured
        bool shouldPlayActiveSfx = (0 < bullet.FramesInBlState && BulletState.Active == bullet.BlState && null != bullet.Config.ActiveSfxName);
        if (shouldPlayActiveSfx) {
            bool usingSameAudSrc = true;
            string atSfxLookupKey = "bl-at-" + bullet.BattleAttr.BulletLocalId.ToString();
            var atSfxSourceHolder = cachedSfxNodes.PopAny(atSfxLookupKey);
            if (null == atSfxSourceHolder) {
                atSfxSourceHolder = cachedSfxNodes.Pop();
                usingSameAudSrc = false;
            }

            if (null == atSfxSourceHolder) {
                return false;
                // throw new ArgumentNullException(String.Format("No available atSfxSourceHolder node for ftSfxLookupKey={0}", ftSfxLookupKey));
            }

            try {
                if (!atSfxSourceHolder.audioClipDict.ContainsKey(bullet.Config.ActiveSfxName)) {
                    return false;
                }

                float totAttZ = distanceAttenuationZ + footstepAttenuationZ; // Use footstep built-in attenuation for now
                newPosHolder.Set(wx, wy, totAttZ);
                atSfxSourceHolder.gameObject.transform.position = newPosHolder;
                if (!usingSameAudSrc || !atSfxSourceHolder.audioSource.isPlaying) {
                    atSfxSourceHolder.audioSource.volume = calcSfxVolume(atSfxSourceHolder, totAttZ);
                    atSfxSourceHolder.audioSource.PlayOneShot(atSfxSourceHolder.audioClipDict[bullet.Config.ActiveSfxName]);
                }
                atSfxSourceHolder.score = rdfId;
            } finally {
                cachedSfxNodes.Put(atSfxLookupKey, atSfxSourceHolder);
            }
        }

        // Play initla sfx for state
        bool isInitialFrame = (0 == bullet.FramesInBlState && (BulletState.Active != bullet.BlState || (BulletState.Active == bullet.BlState && 0 < bullet.BattleAttr.ActiveSkillHit)));
        if (!isInitialFrame) {
            return false;
        }
        string sfxLookupKey = "bl-" + bullet.BattleAttr.BulletLocalId.ToString();
        var sfxSourceHolder = cachedSfxNodes.PopAny(sfxLookupKey);
        if (null == sfxSourceHolder) {
            sfxSourceHolder = cachedSfxNodes.Pop();
        }

        if (null == sfxSourceHolder) {
            return false;
            // throw new ArgumentNullException(String.Format("No available sfxSourceHolder node for sfxLookupKey={0}", sfxLookupKey));
        }

        try {
            string clipName = isExploding ? bullet.Config.ExplosionSfxName : bullet.Config.FireballEmitSfxName;
            if (null == clipName) {
                return false;
            }
            if (!sfxSourceHolder.audioClipDict.ContainsKey(clipName)) {
                return false;
            }

            newPosHolder.Set(wx, wy, distanceAttenuationZ);
            sfxSourceHolder.gameObject.transform.position = newPosHolder;
            sfxSourceHolder.audioSource.volume = calcSfxVolume(sfxSourceHolder, distanceAttenuationZ);
            sfxSourceHolder.audioSource.PlayOneShot(sfxSourceHolder.audioClipDict[clipName]);
            sfxSourceHolder.score = rdfId;
        } finally {
            cachedSfxNodes.Put(sfxLookupKey, sfxSourceHolder);
        }


        return true;
    }

    public bool playBulletVfx(Bullet bullet, bool isExploding, float wx, float wy, RoomDownsyncFrame rdf) {
        var bulletConfig = bullet.Config;
        int vfxSpeciesId = isExploding ? bulletConfig.ExplosionVfxSpeciesId : bulletConfig.ActiveVfxSpeciesId;

        if (NO_VFX_ID == vfxSpeciesId) return false;
        // For convenience, bullet vfx is only pixelated from now on
        if (isExploding && !bulletConfig.IsPixelatedExplostionVfx) {
            return false;
        }
        if (!isExploding && !bulletConfig.IsPixelatedActiveVfx) {
            return false;
        }
        var vfxConfig = pixelatedVfxDict[vfxSpeciesId];
        var vfxAnimName = vfxConfig.Name;
        string vfxLookupKey = null;
        int framesInState = MAX_INT;
        int dirX = 0;
        newPosHolder.Set(effectivelyInfinitelyFar, effectivelyInfinitelyFar, fireballZ);
        if (vfxConfig.OnBullet) {
            if (!isExploding && !IsBulletActive(bullet, rdf.Id)) return false;
            vfxLookupKey = "bl-" + bullet.BattleAttr.BulletLocalId.ToString();
            framesInState = bullet.FramesInBlState;
            dirX = bullet.DirX;
            if (VfxMotionType.Tracing == vfxConfig.MotionType) {
                newPosHolder.Set(wx, wy, fireballZ);
            } else if (VfxMotionType.Dropped == vfxConfig.MotionType) {
                var (vfxCx, vfxCy) = VirtualGridToPolygonColliderCtr(bullet.OriginatedVirtualGridX, bullet.OriginatedVirtualGridY);
                var (vfxWx, vfxWy) = CollisionSpacePositionToWorldPosition(vfxCx, vfxCy, spaceOffsetX, spaceOffsetY);
                newPosHolder.Set(vfxWx, vfxWy, fireballZ);
            }
        } else if (vfxConfig.OnCharacter) {
            vfxLookupKey = "ch-bl-" + bullet.BattleAttr.BulletLocalId.ToString();
            var ch = (roomCapacity >= bullet.BattleAttr.OffenderJoinIndex ? rdf.PlayersArr[bullet.BattleAttr.OffenderJoinIndex - 1] : rdf.NpcsArr[bullet.BattleAttr.OffenderJoinIndex - roomCapacity - 1]);
            if (ch.ActiveSkillId != bullet.BattleAttr.SkillId) return false;
            framesInState = ch.FramesInChState;
            dirX = ch.DirX;
            if (VfxMotionType.Tracing == vfxConfig.MotionType) {
                var (vfxCx, vfxCy) = VirtualGridToPolygonColliderCtr(ch.VirtualGridX, ch.VirtualGridY);
                var (vfxWx, vfxWy) = CollisionSpacePositionToWorldPosition(vfxCx, vfxCy, spaceOffsetX, spaceOffsetY);
                newPosHolder.Set(vfxWx, vfxWy, fireballZ);
            } else if (VfxMotionType.Dropped == vfxConfig.MotionType) {
                var (vfxCx, vfxCy) = VirtualGridToPolygonColliderCtr(bullet.OriginatedVirtualGridX, bullet.OriginatedVirtualGridY);
                var (vfxWx, vfxWy) = CollisionSpacePositionToWorldPosition(vfxCx, vfxCy, spaceOffsetX, spaceOffsetY);
                newPosHolder.Set(vfxWx, vfxWy, fireballZ);
            }
        }

        if (!isGameObjPositionWithinCamera(newPosHolder)) {
            return false;
        }

        if (null != vfxLookupKey) {
            var pixelVfxHolder = cachedPixelVfxNodes.PopAny(vfxLookupKey);
            if (null == pixelVfxHolder) {
                pixelVfxHolder = cachedPixelVfxNodes.Pop();
                //Debug.Log(String.Format("@rdf.Id={0}, using a new pixel-vfx node for rendering for bulletLocalId={1} at wpos=({2}, {3})", rdf.Id, bullet.BattleAttr.BulletLocalId, wx, wy));
            } else {
                //Debug.Log(String.Format("@rdf.Id={0}, using a cached pixel-vfx node for rendering for bulletLocalId={1} at wpos=({2}, {3})", rdf.Id, bullet.BattleAttr.BulletLocalId, wx, wy));
            }

            if (null != pixelVfxHolder && null != pixelVfxHolder.lookUpTable) {
                if (pixelVfxHolder.lookUpTable.ContainsKey(vfxAnimName)) {
                    pixelVfxHolder.updateAnim(vfxAnimName, framesInState, dirX, false, rdf.Id);
                    pixelVfxHolder.gameObject.transform.position = newPosHolder;
                }
                pixelVfxHolder.score = rdf.Id;
                cachedPixelVfxNodes.Put(vfxLookupKey, pixelVfxHolder);
            }
        }
        
        return true;
    }

    public float calcSfxVolume(SFXSource sfxSource, float totAttZ) {
        if (totAttZ <= 0) return 1f;
        if (totAttZ >= sfxSource.maxDistanceInWorld) return 0f;
        return (float)Math.Pow((double)12f, (double)(-totAttZ / sfxSource.maxDistanceInWorld));
    }
    public string calcFootStepSfxName(CharacterDownsync currCharacterDownsync) {
        // TODO: Record the contacted barrier material ID in "CharacterDownsync" to achieve more granular footstep sound derivation!  
        return "FootStep1";
    }

    public void pauseAllAnimatingCharacters(bool toPause) {
        iptmgr.gameObject.SetActive(!toPause);

        for (int k = 0; k < roomCapacity; k++) {
            var playerGameObj = playerGameObjs[k];
            var chAnimCtrl = playerGameObj.GetComponent<CharacterAnimController>();
            chAnimCtrl.pause(toPause);
        }

        var (ok, playerRdf) = renderBuffer.GetByFrameId(playerRdfId);
        if (!ok || null == playerRdf) {
            Debug.LogWarning("Unable to get playerRdf by playerRdfId=" + playerRdfId);
            return;
        }
        for (int k = 0; k < playerRdf.NpcsArr.Count; k++) {
            var currNpcDownsync = playerRdf.NpcsArr[k];
            if (TERMINATING_PLAYER_ID == currNpcDownsync.Id) break;
            var speciesKvPq = cachedNpcs[currNpcDownsync.SpeciesId];
            string lookupKey = "npc-" + currNpcDownsync.Id;
            var npcAnimHolder = speciesKvPq.PopAny(lookupKey);
            if (null == npcAnimHolder) continue;
            npcAnimHolder.pause(toPause);
            speciesKvPq.Put(lookupKey, npcAnimHolder);
        }
    }
}
