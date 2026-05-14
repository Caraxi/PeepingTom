using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using PeepingTom.Ipc;
using PeepingTom.Resources;

namespace PeepingTom {
    internal class TargetWatcher : IDisposable {
        private Plugin Plugin { get; }

        private Stopwatch UpdateWatch { get; } = new();
        private Stopwatch? SoundWatch { get; set; }
        private int LastTargetAmount { get; set; }

        private Targeter[] Current { get; set; } = [];

        public IReadOnlyCollection<Targeter> CurrentTargeters => Current;

        private List<Targeter> Previous { get; } = [];

        public IReadOnlyCollection<Targeter> PreviousTargeters => Previous;

        public TargetWatcher(Plugin plugin) {
            Plugin = plugin;
            UpdateWatch.Start();

            Service.Framework.Update += OnFrameworkUpdate;
        }

        public void Dispose() {
            Service.Framework.Update -= OnFrameworkUpdate;
        }

        public void ClearPrevious() {
            Previous.Clear();
        }

        private void OnFrameworkUpdate(IFramework framework1) {
            if (Plugin.InPvp) {
                return;
            }

            if (UpdateWatch.Elapsed > TimeSpan.FromMilliseconds(Plugin.Config.PollFrequency)) {
                Update();
            }
        }

        private void Update() {
            var player = Service.ObjectTable.LocalPlayer;
            if (player == null) {
                return;
            }

            // get targeters and set a copy so we can release the mutex faster
            var newCurrent = GetTargeting(Service.ObjectTable, player);

            foreach (var newTargeter in newCurrent.Where(t => Current.All(c => c.GameObjectId != t.GameObjectId))) {
                try {
                    Plugin.IpcManager.SendNewTargeter(newTargeter);
                } catch (Exception ex) {
                    Service.Log.Error(ex, "Failed to send IPC message");
                }
            }

            foreach (var stopped in Current.Where(t => newCurrent.All(c => c.GameObjectId != t.GameObjectId))) {
                try {
                    Plugin.IpcManager.SendStoppedTargeting(stopped);
                } catch (Exception ex) {
                    Service.Log.Error(ex, "Failed to send IPC message");
                }
            }

            Current = newCurrent;

            HandleHistory(Current);

            // play sound if necessary
            if (CanPlaySound()) {
                SoundWatch?.Restart();
                PlaySound();
            }

            LastTargetAmount = Current.Length;
        }

        private void HandleHistory(Targeter[] targeting) {
            if (!Plugin.Config.KeepHistory || !Plugin.Config.HistoryWhenClosed && !Plugin.Ui.MainWindow.IsVisible) {
                return;
            }

            foreach (var targeter in targeting) {
                // add the targeter to the previous list
                if (Previous.Any(old => old.GameObjectId == targeter.GameObjectId)) {
                    Previous.RemoveAll(old => old.GameObjectId == targeter.GameObjectId);
                }

                Previous.Insert(0, targeter);
            }

            // only keep the configured number of previous targeters (ignoring ones that are currently targeting)
            while (Previous.Count(old => targeting.All(actor => actor.GameObjectId != old.GameObjectId)) > Plugin.Config.NumHistory) {
                Previous.RemoveAt(Previous.Count - 1);
            }
        }

        private Targeter[] GetTargeting(IEnumerable<IGameObject> objects, IGameObject player) {
            return objects
                .Where(obj => obj is IPlayerCharacter && (obj.ObjectIndex == 0 ? (Service.TargetManager.SoftTarget ?? Service.TargetManager.Target)?.GameObjectId == player.GameObjectId : obj.TargetObjectId == player.GameObjectId))
                // .Where(obj => Marshal.ReadByte(obj.Address + ActorOffsets.PlayerCharacterTargetActorId + 4) == 0)
                .Cast<IPlayerCharacter>()
                .Where(actor => Plugin.Config.LogParty || !InParty(actor))
                .Where(actor => Plugin.Config.LogAlliance || !InAlliance(actor))
                .Where(actor => Plugin.Config.LogInCombat || !InCombat(actor))
                .Where(actor => Plugin.Config.LogSelf || actor.GameObjectId != player.GameObjectId)
                .Where(actor => Plugin.Config.LogBlacklistedPlayers || !IsBlacklisted(actor))
                .Select(actor => new Targeter(actor))
                .ToArray();
        }

        private static bool InCombat(IGameObject actor) => actor is IPlayerCharacter pc && pc.StatusFlags.HasFlag(StatusFlags.InCombat);

        private static bool InParty(IGameObject actor) => actor is IPlayerCharacter pc && pc.StatusFlags.HasFlag(StatusFlags.PartyMember);

        private static bool InAlliance(IGameObject actor) => actor is IPlayerCharacter pc && pc.StatusFlags.HasFlag(StatusFlags.AllianceMember);

        private record BlockResult(DateTime Time, InfoProxyBlacklist.BlockResultType BlockResultType);
        private static Dictionary<ulong, BlockResult> _blockCache = [];
        
        private static unsafe bool IsBlacklisted(IGameObject gameObject) {
            if (gameObject is not IPlayerCharacter) return false;
            var battleChara = (BattleChara*)gameObject.Address;
            try {
                if (!_blockCache.TryGetValue(battleChara->ContentId, out var result)) {
                    result = new (DateTime.Now, InfoProxyBlacklist.Instance()->GetBlockResultType(battleChara->AccountId, battleChara->ContentId));
                    _blockCache.TryAdd(battleChara->ContentId, result);
                }

                if (DateTime.Now - result.Time > TimeSpan.FromSeconds(30)) _blockCache.Remove(battleChara->ContentId);
                return result.BlockResultType is InfoProxyBlacklist.BlockResultType.BlockedByAccountId or InfoProxyBlacklist.BlockResultType.BlockedByContentId;
            } catch (Exception ex) {
                Service.Log.Error(ex, "Error reading blacklist");
                _blockCache.TryAdd(battleChara->ContentId, new BlockResult(DateTime.Now, InfoProxyBlacklist.BlockResultType.NotBlocked));
                return false;
            }
        }
        
        private bool CanPlaySound() {
            if (!Plugin.Config.PlaySoundOnTarget) {
                return false;
            }

            if (Current.Length <= LastTargetAmount) {
                return false;
            }

            if (!Plugin.Config.PlaySoundWhenClosed && !Plugin.Ui.MainWindow.IsVisible) {
                return false;
            }

            if (SoundWatch == null) {
                SoundWatch = new Stopwatch();
                return true;
            }

            var secs = SoundWatch.Elapsed.TotalSeconds;
            return secs >= Plugin.Config.SoundCooldown;
        }

        private void PlaySound() {
            var soundDevice = DirectSoundOut.Devices.FirstOrDefault(d => d.Guid == Plugin.Config.SoundDeviceNew);
            if (soundDevice == null) {
                return;
            }

            new Thread(() => {
                WaveStream reader;
                try {
                    if (Plugin.Config.SoundPath == null) {
                        var wavLocation = Path.Join(Service.Interface.AssemblyLocation.Directory!.FullName, "target.wav");
                        reader = new WaveFileReader(new FileStream(wavLocation, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
                    } else {
                        reader = new MediaFoundationReader(Plugin.Config.SoundPath);
                    }
                } catch (Exception e) {
                    var error = string.Format(Language.SoundChatError, e.Message);
                    SendError(error);
                    return;
                }

                using var channel = new WaveChannel32(reader);
                channel.Volume = Plugin.Config.SoundVolume;
                channel.PadWithZeroes = false;

                using (reader) {
                    using var output = new DirectSoundOut(soundDevice.Guid);

                    try {
                        output.Init(channel);
                        output.Play();

                        while (output.PlaybackState == PlaybackState.Playing) {
                            Thread.Sleep(500);
                        }
                    } catch (Exception ex) {
                        Service.Log.Error(ex, "Exception playing sound");
                    }
                }
            }).Start();
        }

        private void SendError(string message) {
            Service.ChatGui.Print(new XivChatEntry {
                Message = $"[{Plugin.Name}] {message}",
                Type = XivChatType.ErrorMessage,
            });
        }
    }
}
