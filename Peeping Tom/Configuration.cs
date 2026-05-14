using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Numerics;

namespace PeepingTom {
    [Serializable]
    internal class Configuration : IPluginConfiguration {
        public int Version { get; set; } = 1;

        private IDalamudPluginInterface Interface { get; set; } = null!;

        public bool MarkTargeted { get; set; }

        public Vector4 TargetedColour { get; set; } = new(0f, 1f, 0f, 1f);
        public float TargetedSize { get; set; } = 2f;

        public bool MarkTargeting { get; set; }
        public Vector4 TargetingColour { get; set; } = new(1f, 0f, 0f, 1f);
        public float TargetingSize { get; set; } = 2f;

        public bool DebugMarkers { get; set; } = false;

        public bool KeepHistory { get; set; } = true;
        public bool HistoryWhenClosed { get; set; } = true;
        public int NumHistory { get; set; } = 5;
        public bool ShowTimestamps { get; set; } = true;

        public bool LogParty { get; set; } = true;
        public bool LogAlliance { get; set; }
        public bool LogInCombat { get; set; }
        public bool LogSelf { get; set; }
        public bool LogBlacklistedPlayers { get; set; }

        public bool FocusTargetOnHover { get; set; } = true;
        public bool OpenExamine { get; set; }

        public bool PlaySoundOnTarget { get; set; }
        public string? SoundPath { get; set; }
        public float SoundVolume { get; set; } = 1f;
        [Obsolete("use new", true)]
        public int SoundDevice { get; set; } = -1;
        public Guid SoundDeviceNew { get; set; } = Guid.Empty;
        public float SoundCooldown { get; set; } = 10f;
        public bool PlaySoundWhenClosed { get; set; }

        public bool OpenOnLogin { get; set; }
        public bool AllowMovement { get; set; } = true;
        public bool AllowResize { get; set; } = true;
        public bool ShowInCombat { get; set; }
        public bool ShowInInstance { get; set; }
        public bool ShowInCutscenes { get; set; }
        public bool ShowSettingsButton { get; set; } = true;
        public bool AllowCloseWithEscape { get; set; }

        public int PollFrequency { get; set; } = 100;

        public void Initialize(IDalamudPluginInterface pluginInterface) {
            Interface = pluginInterface;
        }

        public void Save() {
            Interface.SavePluginConfig(this);
        }
    }
}
