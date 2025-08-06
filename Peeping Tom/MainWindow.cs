using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Dalamud.Bindings.ImGui;
using PeepingTom.Ipc;
using PeepingTom.Resources;

namespace PeepingTom;

internal class MainWindow : Window {
    private PluginUi Ui { get; }
    private Plugin Plugin { get; }
    
    public bool IsVisible { get; private set; }
    
    private ulong? PreviousFocus { get; set; } = new();
    
    private static void HelpMarker(string text) {
        ImGui.TextDisabled("(?)");
        if (!ImGui.IsItemHovered()) {
            return;
        }

        ImGui.BeginTooltip();
        ImGui.PushTextWrapPos(ImGui.GetFontSize() * 20f);
        ImGui.TextUnformatted(text);
        ImGui.PopTextWrapPos();
        ImGui.EndTooltip();
    }
    
    internal MainWindow(Plugin plugin, PluginUi ui) : base(Plugin.Name) {
        Ui = ui;
        Plugin = plugin;

        Size = new Vector2(290, 195);
        SizeCondition = ImGuiCond.FirstUseEver;
        UpdateConfig();
    }

    public override void Update() {
        IsVisible = false;
        base.Update();
    }

    public override void PreDraw() {
        Flags = ImGuiWindowFlags.None;
        if (!Plugin.Config.AllowMovement) {
            Flags |= ImGuiWindowFlags.NoMove;
        }

        if (!Plugin.Config.AllowResize) {
            Flags |= ImGuiWindowFlags.NoResize;
        }
        
        base.PreDraw();
    }

    public override void Draw() {
        IsVisible = true;
        var targeting = Plugin.Watcher.CurrentTargeters;
        var previousTargeters = Plugin.Config.KeepHistory ? Plugin.Watcher.PreviousTargeters : null;

        // to prevent looping over a subset of the actors repeatedly when multiple people are targeting,
        // create a dictionary for O(1) lookups by actor id
        Dictionary<ulong, IGameObject>? objects = null;
        if (targeting.Count + (previousTargeters?.Count ?? 0) > 1) {
            var dict = new Dictionary<ulong, IGameObject>();
            foreach (var obj in Service.ObjectTable) {
                if (dict.ContainsKey(obj.GameObjectId) || obj.ObjectKind != ObjectKind.Player) {
                    continue;
                }

                dict.Add(obj.GameObjectId, obj);
            }

            objects = dict;
        }
        
        ImGui.Text(Language.MainTargetingYou);
        ImGui.SameLine();
        HelpMarker(Plugin.Config.OpenExamine
            ? Language.MainHelpExamine
            : Language.MainHelpNoExamine);

        var height = ImGui.GetContentRegionAvail().Y;
        height -= ImGui.GetStyle().ItemSpacing.Y;

        var anyHovered = false;
        if (ImGui.BeginListBox("##targeting", new Vector2(-1, height))) {
            // add the two first players for testing
            // foreach (var p in this.Plugin.Interface.ClientState.Actors
            //     .Where(actor => actor is PlayerCharacter)
            //     .Skip(1)
            //     .Select(actor => actor as PlayerCharacter)
            //     .Take(2)) {
            //     this.AddEntry(new Targeter(p), p, ref anyHovered);
            // }

            foreach (var targeter in targeting) {
                IGameObject? obj = null;
                objects?.TryGetValue(targeter.GameObjectId, out obj);
                AddEntry(targeter, obj, ref anyHovered);
            }

            if (Plugin.Config.KeepHistory) {
                // get a list of the previous targeters that aren't currently targeting
                var previous = (previousTargeters ?? new List<Targeter>())
                    .Where(old => targeting.All(actor => actor.GameObjectId != old.GameObjectId))
                    .Take(Plugin.Config.NumHistory);
                // add previous targeters to the list
                foreach (var oldTargeter in previous) {
                    IGameObject? obj = null;
                    objects?.TryGetValue(oldTargeter.GameObjectId, out obj);
                    AddEntry(oldTargeter, obj, ref anyHovered, ImGuiSelectableFlags.Disabled);
                }
            }

            ImGui.EndListBox();
        }

        var previousFocus = PreviousFocus;
        if (Plugin.Config.FocusTargetOnHover && !anyHovered && previousFocus != null) {
            if (previousFocus == uint.MaxValue) {
                Service.TargetManager.FocusTarget = null;
            } else {
                var actor = Service.ObjectTable.FirstOrDefault(a => a.GameObjectId == previousFocus);
                // either target the actor if still present or target nothing
                Service.TargetManager.FocusTarget = actor;
            }

            PreviousFocus = null;
        }
    }
    
     private void AddEntry(Targeter targeter, IGameObject? obj, ref bool anyHovered, ImGuiSelectableFlags flags = ImGuiSelectableFlags.None) {
            ImGui.BeginGroup();

            ImGui.Selectable(targeter.Name.TextValue, false, flags);

            if (Plugin.Config.ShowTimestamps) {
                var time = DateTime.UtcNow - targeter.When >= TimeSpan.FromDays(1)
                    ? targeter.When.ToLocalTime().ToString("dd/MM")
                    : targeter.When.ToLocalTime().ToString("t");
                var windowWidth = ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMin().X;
                ImGui.SameLine(windowWidth - ImGui.CalcTextSize(time).X);

                if (flags.HasFlag(ImGuiSelectableFlags.Disabled)) {
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int) ImGuiCol.TextDisabled]);
                }

                ImGui.TextUnformatted(time);

                if (flags.HasFlag(ImGuiSelectableFlags.Disabled)) {
                    ImGui.PopStyleColor();
                }
            }

            ImGui.EndGroup();

            var hover = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled);
            var left = hover && ImGui.IsMouseClicked(ImGuiMouseButton.Left);
            var right = hover && ImGui.IsMouseClicked(ImGuiMouseButton.Right);

            obj ??= Service.ObjectTable.FirstOrDefault(a => a.GameObjectId == targeter.GameObjectId);

            // don't count as hovered if the actor isn't here (clears focus target when hovering missing actors)
            if (obj != null) {
                anyHovered |= hover;
            }

            if (Plugin.Config.FocusTargetOnHover && hover && obj != null) {
                PreviousFocus ??= Service.TargetManager.FocusTarget?.GameObjectId ?? uint.MaxValue;
                Service.TargetManager.FocusTarget = obj;
            }

            if (left) {
                if (Plugin.Config.OpenExamine && ImGui.GetIO().KeyAlt) {
                    if (obj != null) {
                        unsafe {
                            AgentInspect.Instance()->ExamineCharacter(obj.EntityId);
                        }
                    } else {
                        var error = string.Format(Language.ExamineErrorToast, targeter.Name);
                        Service.ToastGui.ShowError(error);
                    }
                } else {
                    var payload = new PlayerPayload(targeter.Name.TextValue, targeter.HomeWorldId);
                    Payload[] payloads = [payload];
                    Service.ChatGui.Print(new XivChatEntry {
                        Message = new SeString(payloads),
                    });
                }
            } else if (right && obj != null) {
                Service.TargetManager.Target = obj;
            }
     }
    
     public override bool DrawConditions() {
         var inCombat = Service.Condition[ConditionFlag.InCombat];
         var inInstance = Service.Condition[ConditionFlag.BoundByDuty]
                          || Service.Condition[ConditionFlag.BoundByDuty56]
                          || Service.Condition[ConditionFlag.BoundByDuty95];
         var inCutscene = Service.Condition[ConditionFlag.WatchingCutscene]
                          || Service.Condition[ConditionFlag.WatchingCutscene78]
                          || Service.Condition[ConditionFlag.OccupiedInCutSceneEvent];
        
         if (inCombat && !Plugin.Config.ShowInCombat) return false;
         if (inInstance && !Plugin.Config.ShowInInstance) return false;
         if (inCutscene && !Plugin.Config.ShowInCutscenes) return false;
        
         return true;
     }

     internal void UpdateConfig() {
         RespectCloseHotkey = Plugin.Config.AllowCloseWithEscape;
        
         TitleBarButtons.Clear();
         if (Plugin.Config.ShowSettingsButton) {
             TitleBarButtons.Add(new TitleBarButton() {
                 AvailableClickthrough = false,
                 Click = _ => Ui.SettingsWindow.Toggle(),
                 Icon = FontAwesomeIcon.Cog
             });
         }
     }
}
