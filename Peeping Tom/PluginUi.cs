using Dalamud.Bindings.ImGui;
using System;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;

namespace PeepingTom {
    internal class PluginUi : IDisposable {
        private Plugin Plugin { get; }

        private WindowSystem WindowSystem { get; } = new();
        public MainWindow MainWindow { get; }
        public SettingsWindow SettingsWindow { get; }
        
        public PluginUi(Plugin plugin) {
            Plugin = plugin;
            
            MainWindow = new MainWindow(Plugin, this);
            SettingsWindow = new SettingsWindow(Plugin);
            
            WindowSystem.AddWindow(MainWindow);
            WindowSystem.AddWindow(SettingsWindow);
        }

        public void Dispose() {
            WindowSystem.RemoveAllWindows();
        }

        public void Draw() {
            if (Plugin.InPvp) return;
            
            WindowSystem.Draw();
            
            if (Plugin.Config.MarkTargeted) {
                MarkPlayer(GetCurrentTarget(), Plugin.Config.TargetedColour, Plugin.Config.TargetedSize);
            }

            if (!Plugin.Config.MarkTargeting) return;

            var player = Service.ClientState.LocalPlayer;
            if (player == null) return;

            var targeting = Plugin.Watcher.CurrentTargeters
                .Select(targeter => Service.ObjectTable.FirstOrDefault(obj => obj.GameObjectId == targeter.GameObjectId))
                .Where(targeter => targeter is IPlayerCharacter)
                .Cast<IPlayerCharacter>()
                .ToArray();
            foreach (var targeter in targeting) {
                MarkPlayer(targeter, Plugin.Config.TargetingColour, Plugin.Config.TargetingSize);
            }
        }

        private void MarkPlayer(IGameObject? player, Vector4 colour, float size) {
            if (player == null) {
                return;
            }

            if (!Service.GameGui.WorldToScreen(player.Position, out var screenPos)) {
                return;
            }

            ImGui.GetBackgroundDrawList().PushClipRect(ImGuiHelpers.MainViewport.Pos, ImGuiHelpers.MainViewport.Pos + ImGuiHelpers.MainViewport.Size, false);

            ImGui.GetBackgroundDrawList().AddCircleFilled(
                new Vector2(screenPos.X, screenPos.Y),
                size,
                ImGui.GetColorU32(colour),
                100
            );

            ImGui.GetBackgroundDrawList().PopClipRect();
        }

        private IPlayerCharacter? GetCurrentTarget() {
            var player = Service.ClientState.LocalPlayer;
            if (player == null) {
                return null;
            }

            var targetId = player.TargetObjectId;
            if (targetId <= 0) {
                return null;
            }

            return Service.ObjectTable
                .Where(actor => actor.GameObjectId == targetId && actor is IPlayerCharacter)
                .Select(actor => actor as IPlayerCharacter)
                .FirstOrDefault();
        }
    }
}
