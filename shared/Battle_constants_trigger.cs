using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace shared {
    public partial class Battle {
        public static TriggerConfig NSwitch = new TriggerConfig {
            SpeciesId = 1,
            SpeciesName = "NSwitch",
            TriggerMask = TRIGGER_MASK_BY_MOVEMENT,
            CollisionTypeMask = COLLISION_TRIGGER_INDEX_PREFIX
        };

        public static TriggerConfig PSwitch = new TriggerConfig {
            SpeciesId = 2,
            SpeciesName = "PSwitch",
            TriggerMask = TRIGGER_MASK_BY_ATK,
            CollisionTypeMask = COLLISION_TRIGGER_INDEX_PREFIX
        };

        public static TriggerConfig TimedDoor1 = new TriggerConfig {
            SpeciesId = 3,
            SpeciesName = "TimedDoor1",
            TriggerMask = TRIGGER_MASK_BY_CYCLIC_TIMER,
            CollisionTypeMask = COLLISION_NONE_INDEX
        };

        public static ImmutableDictionary<int, TriggerConfig> triggerConfigs = ImmutableDictionary.Create<int, TriggerConfig>().AddRange(
                new[]
                {
                    new KeyValuePair<int, TriggerConfig>(NSwitch.SpeciesId, NSwitch),
                    new KeyValuePair<int, TriggerConfig>(PSwitch.SpeciesId, PSwitch),
                    new KeyValuePair<int, TriggerConfig>(TimedDoor1.SpeciesId, TimedDoor1),
                }
        );
    }
}
