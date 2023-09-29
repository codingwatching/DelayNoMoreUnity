﻿using Google.Protobuf.Collections;
using System;

namespace shared {
    public partial class Battle {
        public static Collider NewConvexPolygonCollider(ConvexPolygon srcPolygon, float spaceOffsetX, float spaceOffsetY, int aMaxTouchingCellsCnt, object data) {
            if (null == srcPolygon) throw new ArgumentNullException("Null srcPolygon is not allowed in `NewConvexPolygonCollider`");
            AlignPolygon2DToBoundingBox(srcPolygon);
            float w = 0, h = 0;

            for (int i = 0; i < srcPolygon.Points.Cnt; i++) {
                for (int j = 0; j < srcPolygon.Points.Cnt; j++) {
                    if (i == j) {
                        continue;
                    }
                    Vector? pi = srcPolygon.GetPointByOffset(i);
                    if (null == pi) {
                        throw new ArgumentNullException("Null pi is not allowed in `NewConvexPolygonCollider`!");
                    }
                    Vector? pj = srcPolygon.GetPointByOffset(j);
                    if (null == pj) {
                        throw new ArgumentNullException("Null pj is not allowed in `NewConvexPolygonCollider`!");
                    }
                    w = Math.Max(w, Math.Abs(pj.X - pi.X));
                    h = Math.Max(h, Math.Abs(pj.Y - pi.Y));
                }
            }

            return new Collider(srcPolygon.X + spaceOffsetX, srcPolygon.Y + spaceOffsetY, w, h, srcPolygon, aMaxTouchingCellsCnt, data);
        }

        public static Collider NewRectCollider(float wx, float wy, float w, float h, float topPadding, float bottomPadding, float leftPadding, float rightPadding, float spaceOffsetX, float spaceOffsetY, int aMaxTouchingCellsCnt, object data) {
            // [WARNING] (spaceOffsetX, spaceOffsetY) are taken into consideration while calling "NewConvexPolygonCollider" -- because "NewConvexPolygonCollider" might also be called for "polylines extracted from Tiled", it's more convenient to organized the codes this way.
            var (blX, blY) = PolygonColliderCtrToBL(wx, wy, w * 0.5f, h * 0.5f, topPadding, bottomPadding, leftPadding, rightPadding, 0, 0);
            float effW = leftPadding + w + rightPadding, effH = bottomPadding + h + topPadding;
            var srcPolygon = new ConvexPolygon(blX, blY, new float[] {
                0, 0,
                0 + effW, 0,
                0 + effW, 0 + effH,
                0, 0 + effH
            });

            return NewConvexPolygonCollider(srcPolygon, spaceOffsetX, spaceOffsetY, aMaxTouchingCellsCnt, data);
        }

        public static void UpdateRectCollider(Collider collider, float wx, float wy, float w, float h, float topPadding, float bottomPadding, float leftPadding, float rightPadding, float spaceOffsetX, float spaceOffsetY, object data) {
            var (blX, blY) = PolygonColliderCtrToBL(wx, wy, w * 0.5f, h * 0.5f, topPadding, bottomPadding, leftPadding, rightPadding, spaceOffsetX, spaceOffsetY);

            float effW = leftPadding + w + rightPadding;
            float effH = bottomPadding + h + topPadding;
            (collider.X, collider.Y, collider.W, collider.H) = (blX, blY, effW, effH);
            collider.Shape.UpdateAsRectangle(0, 0, effW, effH);

            collider.Data = data;
        }

        public static void AssignToCharacterDownsync(int id, int speciesId, int virtualGridX, int virtualGridY, int dirX, int dirY, int velX, int frictionVelX, int velY, int framesToRecover, int framesInChState, int activeSkillId, int activeSkillHit, int framesInvinsible, int speed, CharacterState characterState, int joinIndex, int hp, int maxHp, bool inAir, bool onWall, int onWallNormX, int onWallNormY, int framesCapturedByInertia, int bulletTeamId, int chCollisionTeamId, int revivalVirtualGridX, int revivalVirtualGridY, int revivalDirX, int revivalDirY, bool jumpTriggered, bool slipJumpTriggered, bool primarilyOnSlippableHardPushback, bool capturedByPatrolCue, int framesInPatrolCue, int beatsCnt, int beatenCnt, int mp, int maxMp, ulong collisionMask, bool omitGravity, bool omitSoftPushback, bool waivingSpontaneousPatrol, int waivingPatrolCueId, bool onSlope, bool forcedCrouching, bool newBirth, int lowerPartFramesInChState, bool jumpStarted, CharacterDownsync dst) {
            dst.Id = id;
            dst.SpeciesId = speciesId;
            dst.VirtualGridX = virtualGridX;
            dst.VirtualGridY = virtualGridY;
            dst.DirX = dirX;
            dst.DirY = dirY;
            dst.VelX = velX;
            dst.FrictionVelX = frictionVelX;
            dst.VelY = velY;
            dst.FramesToRecover = framesToRecover;
            dst.FramesInChState = framesInChState;
            dst.ActiveSkillId = activeSkillId;
            dst.ActiveSkillHit = activeSkillHit;
            dst.FramesInvinsible = framesInvinsible;
            dst.Speed = speed;
            dst.CharacterState = characterState;
            dst.JoinIndex = joinIndex;
            dst.Hp = hp;
            dst.MaxHp = maxHp;
            dst.InAir = inAir;
            dst.OnWall = onWall;
            dst.OnWallNormX = onWallNormX;
            dst.OnWallNormY = onWallNormY;
            dst.FramesCapturedByInertia = framesCapturedByInertia;
            dst.BulletTeamId = bulletTeamId;
            dst.ChCollisionTeamId = chCollisionTeamId;
            dst.RevivalVirtualGridX = revivalVirtualGridX;
            dst.RevivalVirtualGridY = revivalVirtualGridY;
            dst.RevivalDirX = revivalDirX;
            dst.RevivalDirY = revivalDirY;
            dst.JumpTriggered = jumpTriggered;
            dst.SlipJumpTriggered = slipJumpTriggered;
            dst.PrimarilyOnSlippableHardPushback = primarilyOnSlippableHardPushback; 

            dst.CapturedByPatrolCue = capturedByPatrolCue;
            dst.FramesInPatrolCue = framesInPatrolCue;

            dst.BeatsCnt = beatsCnt;
            dst.BeatenCnt = beatenCnt;

            dst.Mp = mp;
            dst.MaxMp = maxMp;

            dst.CollisionTypeMask = collisionMask;

            dst.OmitGravity = omitGravity;
            dst.OmitSoftPushback = omitSoftPushback;
            dst.WaivingSpontaneousPatrol = waivingSpontaneousPatrol;
            dst.WaivingPatrolCueId = waivingPatrolCueId;

            dst.OnSlope = onSlope;
            dst.ForcedCrouching = forcedCrouching;
            
            dst.NewBirth = newBirth;
            dst.LowerPartFramesInChState = lowerPartFramesInChState;

            dst.JumpStarted = jumpStarted;
        }

        public static Bullet NewBullet(int bulletLocalId, int originatedRenderFrameId, int offenderJoinIndex, int teamId, BulletState blState, int framesInBlState) {
            return new Bullet {
                BlState = blState,
                FramesInBlState = framesInBlState,
                BattleAttr = new BulletBattleAttr {
                    BulletLocalId = bulletLocalId,
                    OriginatedRenderFrameId = originatedRenderFrameId,
                    OffenderJoinIndex = offenderJoinIndex,
                    TeamId = teamId
                },
                VirtualGridX = 0,
                VirtualGridY = 0,
                DirX = 0,
                DirY = 0,
                VelX = 0,
                VelY = 0
            };
        }

        public static void AssignToBullet(int bulletLocalId, int originatedRenderFrameId, int offenderJoinIndex, int teamId, BulletState blState, int framesInBlState, int vx, int vy, int dirX, int dirY, int velX, int velY, int activeSkillHit, int skillId, BulletConfig staticBulletConfig, Bullet dst) {
            dst.BlState = blState;
            dst.FramesInBlState = framesInBlState;
            dst.Config = staticBulletConfig;

            dst.BattleAttr.BulletLocalId = bulletLocalId;
            dst.BattleAttr.OriginatedRenderFrameId = originatedRenderFrameId;
            dst.BattleAttr.OffenderJoinIndex = offenderJoinIndex;
            dst.BattleAttr.TeamId = teamId;
            dst.BattleAttr.ActiveSkillHit = activeSkillHit;
            dst.BattleAttr.SkillId = skillId;

            dst.VirtualGridX = vx;
            dst.VirtualGridY = vy;
            dst.DirX = dirX;
            dst.DirY = dirY;
            dst.VelX = velX;
            dst.VelY = velY;
        }

        public static void AssignToTrap(int trapLocalId, TrapConfig config, TrapConfigFromTiled configFromTiled, TrapState trapState, int framesInTrapState, int virtualGridX, int virtualGridY, int dirX, int dirY, int velX, int velY, bool isCompletelyStatic, bool capturedByPatrolCue, int framesInPatrolCue, bool waivingSpontaneousPatrol, int waivingPatrolCueId, Trap dst) {
            dst.TrapLocalId = trapLocalId;
            dst.Config = config;
            dst.ConfigFromTiled = configFromTiled;

            // Only the fields below would change w.r.t. time
            dst.TrapState = trapState;
            dst.FramesInTrapState = framesInTrapState;
            dst.VirtualGridX = virtualGridX;
            dst.VirtualGridY = virtualGridY;
            dst.DirX = dirX;
            dst.DirY = dirY;
            dst.VelX = velX;
            dst.VelY = velY;
            dst.IsCompletelyStatic = isCompletelyStatic;

            dst.CapturedByPatrolCue = capturedByPatrolCue;
            dst.FramesInPatrolCue = framesInPatrolCue;
            dst.WaivingSpontaneousPatrol = waivingSpontaneousPatrol;
            dst.WaivingPatrolCueId = waivingPatrolCueId;
        }

        public static void AssignToTrigger(int triggerLocalId, int framesToFire, int framesToRecover, int quota, int bulletTeamId, int subCycleQuotaLeft, TriggerState state, int framesInState, int virtualGridX, int virtualGridY, TriggerConfig config, TriggerConfigFromTiled configFromTiled, Trigger dst) {
            dst.TriggerLocalId = triggerLocalId;
            dst.FramesToFire = framesToFire;
            dst.FramesToRecover = framesToRecover;
            dst.Quota = quota;
            dst.BulletTeamId = bulletTeamId;
            dst.SubCycleQuotaLeft = subCycleQuotaLeft;
            dst.State = state;
            dst.FramesInState = framesInState;
            dst.Config = config;
            dst.ConfigFromTiled = configFromTiled;
            dst.VirtualGridX = virtualGridX;
            dst.VirtualGridY = virtualGridY;
        }

        public static RoomDownsyncFrame NewPreallocatedRoomDownsyncFrame(int roomCapacity, int preallocNpcCount, int preallocBulletCount, int preallocateTrapCount, int preallocateTriggerCount) {
            var ret = new RoomDownsyncFrame();
            ret.Id = TERMINATING_RENDER_FRAME_ID;
            ret.BulletLocalIdCounter = 0;

            for (int i = 0; i < roomCapacity; i++) {
                var single = new CharacterDownsync();
                single.Id = TERMINATING_PLAYER_ID;
                single.LowerPartFramesInChState = INVALID_FRAMES_IN_CH_STATE;
                single.CollisionTypeMask = COLLISION_CHARACTER_INDEX_PREFIX;
                ret.PlayersArr.Add(single);
            }

            for (int i = 0; i < preallocNpcCount; i++) {
                var single = new CharacterDownsync();
                single.Id = TERMINATING_PLAYER_ID;
                single.CollisionTypeMask = COLLISION_CHARACTER_INDEX_PREFIX;
                ret.NpcsArr.Add(single);
            }

            for (int i = 0; i < preallocBulletCount; i++) {
                var single = NewBullet(TERMINATING_BULLET_LOCAL_ID, 0, 0, 0, BulletState.StartUp, 0);
                ret.Bullets.Add(single);
            }

            for (int i = 0; i < preallocateTrapCount; i++) {
                var single = new Trap {
                    TrapLocalId = TERMINATING_TRAP_ID
                };
                ret.TrapsArr.Add(single);
            }

            for (int i = 0; i < preallocateTriggerCount; i++) {
                var single = new Trigger {
                    TriggerLocalId = TERMINATING_TRIGGER_ID,  
                    Config = new TriggerConfig {}, 
                };
                ret.TriggersArr.Add(single);
            }

            return ret;
        }

        public static InputFrameDownsync NewPreallocatedInputFrameDownsync(int roomCapacity) {
            var ret = new InputFrameDownsync();
            ret.InputFrameId = TERMINATING_INPUT_FRAME_ID;
            ret.ConfirmedList = 0;
            for (int i = 0; i < roomCapacity; i++) {
                ret.InputList.Add(0);
            }

            return ret;
        }
    }

    public sealed partial class BulletConfig {
        
        public BulletConfig UpsertCancelTransit(int patternId, int skillId) {
            this.CancelTransit.Add(patternId, skillId);
            return this;
        }
    }

    public sealed partial class Skill {
    
        public Skill AddHit(BulletConfig val) {
            Hits.Add(val);
            return this;
        }
    }
}
