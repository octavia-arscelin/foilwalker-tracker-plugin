using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Chat;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FoilwalkerTrackerLib.Model;

namespace FoilwalkerTracker.Windows;

public class GameWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    private int amount = 0;
    private long targetId = 0;

    public EventHandler<FWTCharacter> OnLocalCharacterUpdate;
    public EventHandler<FWTActionParameters> OnActionRequest;


    public FWTGame? GetGame() {  return plugin.currentGame; }

    // We give this window a hidden ID using ##.
    // The user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public GameWindow(Plugin plugin)
        : base("Foilwalker Game##FWTrackerGame")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(700, 350),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if(plugin.currentGame == null) { ImGui.Text("Not connected to a game"); return; }
        FWTEntity? target = plugin.currentGame.entities.FirstOrDefault(x => x?.id == targetId,null);
        using (ImRaii.Child("###FWTTrackerMainLayout", Vector2.Zero, false, ImGuiWindowFlags.NoScrollbar))
        {

            ImGui.Text($"Targetting {(target!=null?target.name:"...")}");
            if (ImGui.Button("Damage")) OnActionRequest.Invoke(this, new FWTActionParameters(FWTActionType.DAMAGE, target, amount));
            ImGui.SameLine();
            if (ImGui.Button("Stagger")) OnActionRequest.Invoke(this, new FWTActionParameters(FWTActionType.STAGGER, target, amount));
            ImGui.SameLine();
            if (ImGui.Button("Heal")) OnActionRequest.Invoke(this, new FWTActionParameters(FWTActionType.HEAL, target, amount));
            ImGui.SameLine();
            ImGui.InputInt("Amount", ref amount);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            using (ImRaii.Table("###FWTrackerEntitiesLayout", 2, ImGuiTableFlags.SizingFixedSame))
            {
                ImGui.TableSetupColumn("Characters", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Enemies", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableHeadersRow();

                ImGui.TableNextColumn();
                using (var child = ImRaii.Child("###FWTTrackerCharacters", Vector2.Zero, true))
                {
                    using (ImRaii.Table("###FWTTrackerCharactersTable", 4, ImGuiTableFlags.SizingFixedFit))
                    {
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("HP");
                        ImGui.TableSetupColumn("Role", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableSetupColumn("Roll", ImGuiTableColumnFlags.WidthStretch);
                        ImGui.TableHeadersRow();

                        foreach (FWTCharacter character in plugin.currentGame.GetCharacters())
                        {
                            ImGui.TableNextColumn();
                            if (character.id == plugin.characterId) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.4f, 1f, 0.4f, 1f));
                            if(ImGui.Selectable($"{character.name}###{character.name}-{character.id}")) targetId=character.id;
                            if (character.id == plugin.characterId) ImGui.PopStyleColor();
                            ImGui.TableNextColumn();
                            ImGui.ProgressBar(character.health / (float)character.maxHealth, new Vector2(150, 0), $"{character.health}/{character.maxHealth}");
                            ImGui.TableNextColumn();
                            ImGui.Text(Utils.roleNames[character.role]);
                            ImGui.TableNextColumn();
                            ImGui.Text(string.Format("{0:d3}",character.lastRoll));
                            ImGui.TableNextRow();
                        }
                    }
                }

                ImGui.TableNextColumn();
                using (var child = ImRaii.Child("###FWTTrackerMobs", Vector2.Zero, true))
                {
                    using (ImRaii.Table("###FWTTrackerMobsTable", 3, ImGuiTableFlags.SizingStretchProp))
                    {
                        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 2);
                        ImGui.TableSetupColumn("Damage taken", ImGuiTableColumnFlags.WidthStretch, 2);
                        ImGui.TableSetupColumn("Stagger", ImGuiTableColumnFlags.WidthStretch, 1);
                        ImGui.TableHeadersRow();
                        foreach (FWTMob mob in plugin.currentGame.GetMobs())
                        {
                            ImGui.TableNextColumn();
                            //if (targetId == mob.id) ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1f, 0.4f, 0.4f, 1f));
                            if (ImGui.Selectable($"{mob.name}###{mob.name}-{mob.id}")) targetId = mob.id;
                            //if (targetId == mob.id) ImGui.PopStyleColor();
                            ImGui.TableNextColumn();
                            ImGui.Text($"{mob.damageTaken}");
                            ImGui.TableNextColumn();
                            ImGui.Text($"{mob.stagger}");
                            ImGui.TableNextRow();
                        }
                    }
                }
            }
        }

    }

    internal void OnChatMessage(IHandleableChatMessage message)
    {
        var mes = message.Message.TextValue;
        bool selfRollMessage=false;
        if (mes.Contains("Random! You roll a")) selfRollMessage=true;
        else if(message.Sender.TextValue.Length > 0 && message.Sender.TextValue[1..] == plugin.getCharacterName()) selfRollMessage=true;
        if (plugin.currentGame == null || !selfRollMessage) return;

        int roll;
        if (mes.Contains("Random! You roll a")) roll = int.Parse(mes.Split(" ").Last().Split(".").First());
        else roll = int.Parse(mes.Split(" ").Last());
        FWTCharacter character = plugin.currentGame.GetCharacters().First(x => x.id == plugin.characterId);
        character.lastRoll = roll;
        OnLocalCharacterUpdate.Invoke(this, character);
    }

    internal void OnGameUpdate(object? sender, FWTGame e)
    {
        if (plugin.currentGame == null) return;
        plugin.currentGame = e;
    }

    internal long getGameId()
    {
        return plugin.gameId;
    }
}
