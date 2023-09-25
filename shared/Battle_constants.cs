﻿using System.Collections.Generic;
using static shared.CharacterState;

namespace shared {
    public partial class Battle {
        // Deliberately NOT using enum for "room states" to make use of "C# CompareAndExchange" 
        public const long ROOM_STATE_IMPOSSIBLE = 0;
        public const long ROOM_STATE_IDLE = 1;
        public const long ROOM_STATE_WAITING = 2;
        public const long ROOM_STATE_PREPARE = 3;
        public const long ROOM_STATE_IN_BATTLE = 4;
        public const long ROOM_STATE_IN_SETTLEMENT = 5;
        public const long ROOM_STATE_STOPPED = 6;

        // Deliberately NOT using enum for "player battle states" to make use of "C# CompareAndExchange" 
        public const long PLAYER_BATTLE_STATE_IMPOSSIBLE = -2;
        public const long PLAYER_BATTLE_STATE_ADDED_PENDING_BATTLE_COLLIDER_ACK = 0;
        public const long PLAYER_BATTLE_STATE_READDED_PENDING_BATTLE_COLLIDER_ACK = 1;
        public const long PLAYER_BATTLE_STATE_READDED_BATTLE_COLLIDER_ACKED = 2;
        public const long PLAYER_BATTLE_STATE_ACTIVE = 3;
        public const long PLAYER_BATTLE_STATE_DISCONNECTED = 4;
        public const long PLAYER_BATTLE_STATE_LOST = 5;
        public const long PLAYER_BATTLE_STATE_EXPELLED_DURING_GAME = 6;
        public const long PLAYER_BATTLE_STATE_EXPELLED_IN_DISMISSAL = 7;

        public const int UPSYNC_MSG_ACT_PLAYER_COLLIDER_ACK = 1;
        public const int UPSYNC_MSG_ACT_PLAYER_CMD = 2;
        public const int UPSYNC_MSG_ACT_HOLEPUNCH_BACKEND_UDP_TUNNEL = 3;
        public const int UPSYNC_MSG_ACT_HOLEPUNCH_PEER_UDP_ADDR = 4;

        public const int DOWNSYNC_MSG_ACT_BATTLE_COLLIDER_INFO = 1;
        public const int DOWNSYNC_MSG_ACT_INPUT_BATCH = 2;
        public const int DOWNSYNC_MSG_ACT_BATTLE_STOPPED = 3;
        public const int DOWNSYNC_MSG_ACT_FORCED_RESYNC = 4;
        public const int DOWNSYNC_MSG_ACT_PEER_INPUT_BATCH = 5;
        public const int DOWNSYNC_MSG_ACT_PEER_UDP_ADDR = 6;
        public const int DOWNSYNC_MSG_ACT_BATTLE_READY_TO_START = -1;
        public const int DOWNSYNC_MSG_ACT_BATTLE_START = 0;
        public const int DOWNSYNC_MSG_ACT_PLAYER_ADDED_AND_ACKED = -98;
        public const int DOWNSYNC_MSG_WS_CLOSED = -99;
        public const int DOWNSYNC_MSG_WS_OPEN = -100;

        public const int MAGIC_JOIN_INDEX_INVALID = -2;
        public const int MAGIC_JOIN_INDEX_DEFAULT = -1;
        public const int MAGIC_JOIN_INDEX_SRV_UDP_TUNNEL = 0;
        public const int MAGIC_QUOTA_INFINITE = -1;

        public const int MAGIC_LAST_SENT_INPUT_FRAME_ID_NORMAL_ADDED = -1;
        public const int MAGIC_LAST_SENT_INPUT_FRAME_ID_READDED = -2;

        public const int INVALID_DEFAULT_PLAYER_ID = 0;

        public static float MAX_FLOAT32 = float.MaxValue;
        public static int MAX_INT = 999999999;
        public static int PATTERN_ID_UNABLE_TO_OP = -2;
        public static int PATTERN_ID_NO_OP = -1;

        public static ulong COLLISION_NONE_INDEX = 0;
        public static ulong COLLISION_BARRIER_INDEX_PREFIX = (1 << 0);
        public static ulong COLLISION_CHARACTER_INDEX_PREFIX = (1 << 1);
        public static ulong COLLISION_TRAP_INDEX_PREFIX = (1 << 2);
        public static ulong COLLISION_PICKABLE_INDEX_PREFIX = (1 << 3);

        public static ulong COLLISION_MELEE_BULLET_INDEX_PREFIX = (1 << 4);
        public static ulong COLLISION_B_M_FIREBALL_INDEX_PREFIX = (1 << 5); // type of fireball that collides with both barrier and melee (and of course characters and traps)
        public static ulong COLLISION_B_FIREBALL_INDEX_PREFIX = (1 << 6); // type of fireball that collides with barrier but not melee
        public static ulong COLLISION_M_FIREBALL_INDEX_PREFIX = (1 << 7); // type of fireball that collides with melee but not barrier
        public static ulong COLLISION_FIREBALL_INDEX_PREFIX = (1 << 8); // type of fireball that doesn't collide with barrier or melee

        public static ulong COLLISION_NPC_PATROL_CUE_INDEX_PREFIX = (1 << 9);
        public static ulong COLLISION_TRAP_PATROL_CUE_INDEX_PREFIX = (1 << 10);
        public static ulong COLLISION_TRIGGER_INDEX_PREFIX = (1 << 11);

        public static ulong TRIGGER_MASK_NONE = 0;
        public static ulong TRIGGER_MASK_BY_MOVEMENT = (1 << 0);
        public static ulong TRIGGER_MASK_BY_ATK = (1 << 1);
        public static ulong TRIGGER_MASK_BY_CYCLIC_TIMER = (1 << 2);

        public static HashSet<ulong> COLLIDABLE_PAIRS = new HashSet<ulong>() {
            COLLISION_CHARACTER_INDEX_PREFIX, // such that characters collider with each other
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_PICKABLE_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_NPC_PATROL_CUE_INDEX_PREFIX,
            COLLISION_CHARACTER_INDEX_PREFIX | COLLISION_TRIGGER_INDEX_PREFIX,

            // Melee bullet, it wouldn't collide with barrier, specifically 
            COLLISION_MELEE_BULLET_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX,
            COLLISION_MELEE_BULLET_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX,
            COLLISION_MELEE_BULLET_INDEX_PREFIX | COLLISION_TRIGGER_INDEX_PREFIX,

            // Fireball bullets
            COLLISION_FIREBALL_INDEX_PREFIX | COLLISION_FIREBALL_INDEX_PREFIX,
            COLLISION_FIREBALL_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX,
            COLLISION_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX,
            COLLISION_FIREBALL_INDEX_PREFIX | COLLISION_TRIGGER_INDEX_PREFIX,

            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_FIREBALL_INDEX_PREFIX,
            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_B_FIREBALL_INDEX_PREFIX,
            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX,
            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX,
            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX,
            COLLISION_B_FIREBALL_INDEX_PREFIX | COLLISION_TRIGGER_INDEX_PREFIX,

            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_FIREBALL_INDEX_PREFIX,
            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_B_FIREBALL_INDEX_PREFIX,
            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_M_FIREBALL_INDEX_PREFIX,
            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX,
            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX,
            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_MELEE_BULLET_INDEX_PREFIX,
            COLLISION_M_FIREBALL_INDEX_PREFIX | COLLISION_TRIGGER_INDEX_PREFIX,

            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_FIREBALL_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_B_FIREBALL_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_M_FIREBALL_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_B_M_FIREBALL_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_CHARACTER_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_TRAP_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_MELEE_BULLET_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX,
            COLLISION_B_M_FIREBALL_INDEX_PREFIX | COLLISION_TRIGGER_INDEX_PREFIX,

            // Trap
            COLLISION_TRAP_INDEX_PREFIX | COLLISION_TRAP_PATROL_CUE_INDEX_PREFIX,
            COLLISION_TRAP_INDEX_PREFIX | COLLISION_BARRIER_INDEX_PREFIX | COLLISION_TRAP_PATROL_CUE_INDEX_PREFIX,
        };

        public static float COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO = 10.0f;
        public static float VIRTUAL_GRID_TO_COLLISION_SPACE_RATIO = 1.0f / COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO;

        public static int DEFAULT_PLAYER_RADIUS = (int)(12 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO);
        public static int DEFAULT_PREALLOC_NPC_CAPACITY = 16;
        public static int DEFAULT_PREALLOC_BULLET_CAPACITY = 64;
        public static int DEFAULT_PREALLOC_TRAP_CAPACITY = 24;
        public static int DEFAULT_PREALLOC_TRIGGER_CAPACITY = 8;

        public static int GRAVITY_X = 0;
        public static int GRAVITY_Y = -(int)(0.5 * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO); // makes all "playerCollider.Y" a multiple of 0.5 in all cases
        public static int INPUT_DELAY_FRAMES = 2; // in the count of render frames
        public static int DEFAULT_PATROL_CUE_WAIVING_FRAMES = 150; // in the count of render frames, should be big enough for any NPC to move across the largest patrol cue
        public static int NO_PATROL_CUE_ID = -1;
        public static int NO_VFX_ID = 0;

        /*
		   [WARNING]
		   Experimentally having an input rate > 15 (e.g., 60 >> 2) doesn't improve multiplayer smoothness, in fact higher input rate often results in higher packet loss (both TCP and UDP) thus higher wrong prediction rate!
		*/
        public static int INPUT_SCALE_FRAMES = 2; // inputDelayedAndScaledFrameId = ((originalFrameId - InputDelayFrames) >> InputScaleFrames)

        public static int SP_ATK_LOOKUP_FRAMES = 5;
        public static float SNAP_INTO_PLATFORM_OVERLAP = 0.1f;
        public static float SLIP_JUMP_THRESHOLD_BELOW_TOP_FACE = 8.0f; // Currently only supports rectilinear rectangle shape; kindly note that "8.0f" is half the minimum height in any feasible map of this game!
        public static int SLIP_JUMP_THRESHOLD_BELOW_TOP_FACE_VIRTUAL = (int)(SLIP_JUMP_THRESHOLD_BELOW_TOP_FACE * COLLISION_SPACE_TO_VIRTUAL_GRID_RATIO); 
        public static int SLIP_JUMP_CHARACTER_DROP_VIRTUAL = (SLIP_JUMP_THRESHOLD_BELOW_TOP_FACE_VIRTUAL << 1) + (SLIP_JUMP_THRESHOLD_BELOW_TOP_FACE_VIRTUAL >> 1); 

        // [WARNING] The "zero overlap collision" might be randomly detected/missed on either frontend or backend, to have deterministic result we added paddings to all sides of a characterCollider. As each velocity component of (velX, velY) being a multiple of 0.5 at any renderFrame, each position component of (x, y) can only be a multiple of 0.5 too, thus whenever a 1-dimensional collision happens between players from [player#1: i*0.5, player#2: j*0.5, not collided yet] to [player#1: (i+k)*0.5, player#2: j*0.5, collided], the overlap becomes (i+k-j)*0.5+2*s, and after snapping subtraction the effPushback magnitude for each player is (i+k-j)*0.5, resulting in 0.5-multiples-position for the next renderFrame.
        public static float SNAP_INTO_CHARACTER_OVERLAP = 2 * SNAP_INTO_PLATFORM_OVERLAP;
        public static float SNAP_INTO_PLATFORM_THRESHOLD = 0.5f;
        public static float VERTICAL_PLATFORM_THRESHOLD = 0.9f;
        public static int MAGIC_FRAMES_TO_BE_ON_WALL = 12;


        public static int DYING_FRAMES_TO_RECOVER = 180; // MUST BE SAME FOR EVERY CHARACTER FOR FAIRNESS!

        public static int NO_SKILL = -1;
        public static int NO_SKILL_HIT = -1;

        public static int NO_LOCK_VEL = -1;

        // Used in preallocated RoomDownsyncFrame to check termination
        public static int TERMINATING_BULLET_LOCAL_ID = (-1024);
        public static int TERMINATING_PLAYER_ID = (-1025);
        public static int TERMINATING_RENDER_FRAME_ID = (-1026);
        public static int TERMINATING_INPUT_FRAME_ID = (-1027);
        public static int TERMINATING_BULLET_TEAM_ID = (-1028);
        public static int TERMINATING_TRAP_ID = (-1029);
        public static int TERMINATING_TRIGGER_ID = (-1030);

        public static int DEFAULT_BULLET_TEAM_ID = (1028);

        // These directions are chosen such that when speed is changed to "(speedX+delta, speedY+delta)" for any of them, the direction is unchanged.
        public static int[,] DIRECTION_DECODER = new int[,] {
            {0, 0},
            {0, +2},
            {0, -2},
            {+2, 0},
            {-2, 0},
            {+1, +1},
            {-1, -1},
            {+1, -1},
            {-1, +1},
        };

        public static HashSet<CharacterState> inAirSet = new HashSet<CharacterState>() {
            InAirIdle1NoJump,
            InAirIdle1ByJump,
            InAirIdle1ByWallJump,
            InAirAtk1,
            InAirAtked1,
            BlownUp1,
            OnWallIdle1,
            Dashing // Yes dashing is an InAir state even if you dashed on the ground :)
        };

        public static HashSet<CharacterState> noOpSet = new HashSet<CharacterState>() {
            Atked1,
            InAirAtked1,
            CrouchAtked1,
            BlownUp1,
            LayDown1,
			// [WARNING] During the invinsible frames of GET_UP1, the player is allowed to take any action
			Dying
        };

        public static HashSet<CharacterState> invinsibleSet = new HashSet<CharacterState>() {
            BlownUp1,
            LayDown1,
            GetUp1,
            Dying
        };

        public static HashSet<CharacterState> nonAttackingSet = new HashSet<CharacterState>() {
            Idle1,
            Walking,
            Dashing,
            InAirIdle1NoJump,
            InAirIdle1ByJump,
            Atked1,
            InAirAtked1,
            CrouchAtked1,
            BlownUp1,
            LayDown1,
            GetUp1,
            Dying
        };
    }
}
