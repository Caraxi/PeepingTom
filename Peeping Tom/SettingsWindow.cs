using System;
using System.Linq;
using System.Numerics;
using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using NAudio.Wave;
using PeepingTom.Resources;

namespace PeepingTom;

public class SettingsWindow : Window {
    
    private Plugin Plugin { get; }
    private FileDialogManager FileDialogManager { get; }
    
    public SettingsWindow(Plugin plugin) : base(string.Format(Language.SettingsTitle, Plugin.Name) + "###ptom-settings") {
        Plugin = plugin;
        
        Size = new Vector2(700, 300);
        SizeCondition = ImGuiCond.Always;
        Flags = ImGuiWindowFlags.NoResize;
        
        FileDialogManager = new FileDialogManager();
        
    }

    public override void PreDraw() {
        FileDialogManager.Draw();
        base.PreDraw();
    }

    public override void Draw() {
        if (!ImGui.BeginTabBar("##settings-tabs")) return;
        
        if (ImGui.BeginTabItem($"{Language.SettingsMarkersTab}###markers-tab")) {
            var markTargeted = Plugin.Config.MarkTargeted;
            if (ImGui.Checkbox(Language.SettingsMarkersMarkTarget, ref markTargeted)) {
                Plugin.Config.MarkTargeted = markTargeted;
                Plugin.Config.Save();
            }

            var targetedColour = Plugin.Config.TargetedColour;
            if (ImGui.ColorEdit4(Language.SettingsMarkersMarkTargetColour, ref targetedColour)) {
                Plugin.Config.TargetedColour = targetedColour;
                Plugin.Config.Save();
            }

            var targetedSize = Plugin.Config.TargetedSize;
            if (ImGui.DragFloat(Language.SettingsMarkersMarkTargetSize, ref targetedSize, 0.01f, 0f, 15f)) {
                targetedSize = Math.Max(0f, targetedSize);
                Plugin.Config.TargetedSize = targetedSize;
                Plugin.Config.Save();
            }

            ImGui.Spacing();

            var markTargeting = Plugin.Config.MarkTargeting;
            if (ImGui.Checkbox(Language.SettingsMarkersMarkTargeting, ref markTargeting)) {
                Plugin.Config.MarkTargeting = markTargeting;
                Plugin.Config.Save();
            }

            var targetingColour = Plugin.Config.TargetingColour;
            if (ImGui.ColorEdit4(Language.SettingsMarkersMarkTargetingColour, ref targetingColour)) {
                Plugin.Config.TargetingColour = targetingColour;
                Plugin.Config.Save();
            }

            var targetingSize = Plugin.Config.TargetingSize;
            if (ImGui.DragFloat(Language.SettingsMarkersMarkTargetingSize, ref targetingSize, 0.01f, 0f, 15f)) {
                targetingSize = Math.Max(0f, targetingSize);
                Plugin.Config.TargetingSize = targetingSize;
                Plugin.Config.Save();
            }

            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem($"{Language.SettingsFilterTab}###filters-tab")) {
            var showParty = Plugin.Config.LogParty;
            if (ImGui.Checkbox(Language.SettingsFilterLogParty, ref showParty)) {
                Plugin.Config.LogParty = showParty;
                Plugin.Config.Save();
            }

            var logAlliance = Plugin.Config.LogAlliance;
            if (ImGui.Checkbox(Language.SettingsFilterLogAlliance, ref logAlliance)) {
                Plugin.Config.LogAlliance = logAlliance;
                Plugin.Config.Save();
            }

            var logInCombat = Plugin.Config.LogInCombat;
            if (ImGui.Checkbox(Language.SettingsFilterLogCombat, ref logInCombat)) {
                Plugin.Config.LogInCombat = logInCombat;
                Plugin.Config.Save();
            }

            var logSelf = Plugin.Config.LogSelf;
            if (ImGui.Checkbox(Language.SettingsFilterLogSelf, ref logSelf)) {
                Plugin.Config.LogSelf = logSelf;
                Plugin.Config.Save();
            }

            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem($"{Language.SettingsBehaviourTab}###behaviour-tab")) {
            var focusTarget = Plugin.Config.FocusTargetOnHover;
            if (ImGui.Checkbox(Language.SettingsBehaviourFocusHover, ref focusTarget)) {
                Plugin.Config.FocusTargetOnHover = focusTarget;
                Plugin.Config.Save();
            }

            var openExamine = Plugin.Config.OpenExamine;
            if (ImGui.Checkbox(Language.SettingsBehaviourExamineEnabled, ref openExamine)) {
                Plugin.Config.OpenExamine = openExamine;
                Plugin.Config.Save();
            }

            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem($"{Language.SettingsSoundTab}###sound-tab")) {
            var playSound = Plugin.Config.PlaySoundOnTarget;
            if (ImGui.Checkbox(Language.SettingsSoundEnabled, ref playSound)) {
                Plugin.Config.PlaySoundOnTarget = playSound;
                Plugin.Config.Save();
            }

            ImGui.TextUnformatted(Language.SettingsSoundPath);
            Vector2 buttonSize;
            ImGui.PushFont(UiBuilder.IconFont);
            try {
                buttonSize = ImGuiHelpers.GetButtonSize(FontAwesomeIcon.Folder.ToIconString());
            } finally {
                ImGui.PopFont();
            }

            var path = Plugin.Config.SoundPath ?? "";
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - buttonSize.X);
            if (ImGui.InputText("###sound-path", ref path, 1_000)) {
                path = path.Trim();
                Plugin.Config.SoundPath = path.Length == 0 ? null : path;
                Plugin.Config.Save();
            }

            ImGui.SameLine();

            ImGui.PushFont(UiBuilder.IconFont);
            try {
                if (ImGui.Button(FontAwesomeIcon.Folder.ToIconString())) {
                    FileDialogManager.OpenFileDialog(Language.SettingsSoundPath, ".wav,.mp3,.aif,.aiff,.wma,.aac", (selected, selectedPath) => {
                        if (!selected) {
                            return;
                        }

                        path = selectedPath.Trim();
                        Plugin.Config.SoundPath = path.Length == 0 ? null : path;
                        Plugin.Config.Save();
                    });
                }
            } finally {
                ImGui.PopFont();
            }

            ImGui.Text(Language.SettingsSoundPathHelp);

            var volume = Plugin.Config.SoundVolume * 100f;
            if (ImGui.DragFloat(Language.SettingsSoundVolume, ref volume, .1f, 0f, 100f, "%.1f%%")) {
                Plugin.Config.SoundVolume = Math.Max(0f, Math.Min(1f, volume / 100f));
                Plugin.Config.Save();
            }

            var devices = DirectSoundOut.Devices.ToList();
            var soundDevice = devices.FirstOrDefault(d => d.Guid == Plugin.Config.SoundDeviceNew);
            var name = soundDevice != null ? soundDevice.Description : Language.SettingsSoundInvalidDevice;

            if (ImGui.BeginCombo($"{Language.SettingsSoundOutputDevice}###sound-output-device-combo", name)) {
                for (var deviceNum = 0; deviceNum < devices.Count; deviceNum++) {
                    var info = devices[deviceNum];
                    if (!ImGui.Selectable($"{info.Description}##{deviceNum}")) {
                        continue;
                    }

                    Plugin.Config.SoundDeviceNew = info.Guid;
                    Plugin.Config.Save();
                }

                ImGui.EndCombo();
            }

            var soundCooldown = Plugin.Config.SoundCooldown;
            if (ImGui.DragFloat(Language.SettingsSoundCooldown, ref soundCooldown, .01f, 0f, 30f)) {
                soundCooldown = Math.Max(0f, soundCooldown);
                Plugin.Config.SoundCooldown = soundCooldown;
                Plugin.Config.Save();
            }

            var playWhenClosed = Plugin.Config.PlaySoundWhenClosed;
            if (ImGui.Checkbox(Language.SettingsSoundPlayWhenClosed, ref playWhenClosed)) {
                Plugin.Config.PlaySoundWhenClosed = playWhenClosed;
                Plugin.Config.Save();
            }

            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem($"{Language.SettingsWindowTab}###window-tab")) {
            var openOnLogin = Plugin.Config.OpenOnLogin;
            if (ImGui.Checkbox(Language.SettingsWindowOpenLogin, ref openOnLogin)) {
                Plugin.Config.OpenOnLogin = openOnLogin;
                Plugin.Config.Save();
            }

            var showSettingsButton = Plugin.Config.ShowSettingsButton;
            if (ImGui.Checkbox(Language.SettingsWindowShowConfigButton, ref showSettingsButton)) {
                Plugin.Config.ShowSettingsButton = showSettingsButton;
                Plugin.Config.Save();
                Plugin.Ui.MainWindow.UpdateConfig();
            }

            var allowMovement = Plugin.Config.AllowMovement;
            if (ImGui.Checkbox(Language.SettingsWindowAllowMovement, ref allowMovement)) {
                Plugin.Config.AllowMovement = allowMovement;
                Plugin.Config.Save();
            }

            var allowResizing = Plugin.Config.AllowResize;
            if (ImGui.Checkbox(Language.SettingsWindowAllowResize, ref allowResizing)) {
                Plugin.Config.AllowResize = allowResizing;
                Plugin.Config.Save();
            }

            var allowCloseWithEscape = Plugin.Config.AllowCloseWithEscape;
            if (ImGui.Checkbox(Language.SettingsWindowAllowCloseWithEscape, ref allowCloseWithEscape)) {
                Plugin.Config.AllowCloseWithEscape = allowCloseWithEscape;
                Plugin.Config.Save();
                Plugin.Ui.MainWindow.UpdateConfig();
            }

            ImGui.Spacing();

            var showInCombat = Plugin.Config.ShowInCombat;
            if (ImGui.Checkbox(Language.SettingsWindowShowCombat, ref showInCombat)) {
                Plugin.Config.ShowInCombat = showInCombat;
                Plugin.Config.Save();
            }

            var showInInstance = Plugin.Config.ShowInInstance;
            if (ImGui.Checkbox(Language.SettingsWindowShowInstance, ref showInInstance)) {
                Plugin.Config.ShowInInstance = showInInstance;
                Plugin.Config.Save();
            }

            var showInCutscenes = Plugin.Config.ShowInCutscenes;
            if (ImGui.Checkbox(Language.SettingsWindowShowCutscene, ref showInCutscenes)) {
                Plugin.Config.ShowInCutscenes = showInCutscenes;
                Plugin.Config.Save();
            }

            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem($"{Language.SettingsHistoryTab}###history-tab")) {
            var keepHistory = Plugin.Config.KeepHistory;
            if (ImGui.Checkbox(Language.SettingsHistoryEnabled, ref keepHistory)) {
                Plugin.Config.KeepHistory = keepHistory;
                Plugin.Config.Save();
            }

            var historyWhenClosed = Plugin.Config.HistoryWhenClosed;
            if (ImGui.Checkbox(Language.SettingsHistoryRecordClosed, ref historyWhenClosed)) {
                Plugin.Config.HistoryWhenClosed = historyWhenClosed;
                Plugin.Config.Save();
            }

            var numHistory = Plugin.Config.NumHistory;
            if (ImGui.InputInt(Language.SettingsHistoryAmount, ref numHistory)) {
                numHistory = Math.Max(0, Math.Min(50, numHistory));
                Plugin.Config.NumHistory = numHistory;
                Plugin.Config.Save();
            }

            var showTimestamps = Plugin.Config.ShowTimestamps;
            if (ImGui.Checkbox(Language.SettingsHistoryTimestamps, ref showTimestamps)) {
                Plugin.Config.ShowTimestamps = showTimestamps;
                Plugin.Config.Save();
            }

            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem($"{Language.SettingsAdvancedTab}###advanced-tab")) {
            var pollFrequency = Plugin.Config.PollFrequency;
            if (ImGui.DragInt(Language.SettingsAdvancedPollFrequency, ref pollFrequency, .1f, 1, 1600)) {
                Plugin.Config.PollFrequency = pollFrequency;
                Plugin.Config.Save();
            }

            ImGui.EndTabItem();
        }

        ImGui.EndTabBar();
    }
}
