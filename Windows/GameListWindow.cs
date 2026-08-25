using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using FoilwalkerTrackerLib.Model;
using FoilwalkerTrackerLib.Networking;

namespace FoilwalkerTracker.Windows;

public class GameListWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private bool connected = false;
    private FWTGameWrapperSerializable[] wrappers = [];
    private FWTGameWrapperSerializable? selectedGame = null;

    public event EventHandler<GameJoinRequestEventArgs> OnGameJoinRequest;
    public event EventHandler<long> OnGameLeaveRequest;
    public event EventHandler<GameCreateRequestEventArgs> OnGameCreateRequest;

    private string getRoleName(FWTCharacterRole role) {  return Utils.roleNames[role]; }
    private string characterName = "";
    private string gameName = "";
    private int role;

    // We give this window a hidden ID using ##.
    // The user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public GameListWindow(Plugin plugin)
        : base("Foilwalker Tracker Game List##FWTrackerGL", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoResize)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(450, 400)
        };
        
        this.plugin = plugin;
    }

    public void Dispose() { }

    public void OnGameListReceived(object? sender, FWTGameWrapperSerializable[] wrappers) { this.wrappers = wrappers; }
    public string getGameString(FWTGameWrapperSerializable game)
    {
        return "[" + game.id + "] " + game.name + " (" + game.playerCount + " players)"; 
    }

    public override void Draw()
    {
        if(plugin.currentGame != null)
        {
            ImGui.Text($"Currently playing {gameName}");
            if(ImGui.Button("Leave game"))
            {
                OnGameLeaveRequest?.Invoke(this, plugin.gameId);
            }
        }
        else
        {
            var i = 0;
            ImGui.Text("Available games");
            using (var child = ImRaii.Child("gameListFrame", new Vector2(0, 150), true))
            {
                for (i = 0; i < wrappers.Length; i++)
                {
                    if (ImGui.Selectable($"[{wrappers[i].id.ToString("X16")}]  {wrappers[i].name} ({wrappers[i].playerCount} players)"))
                    {
                        selectedGame = wrappers[i];
                    }
                }
                if (connected) if (ImGui.Selectable("--- Create new game ---")) { selectedGame = new FWTGameWrapperSerializable("new", -1, 0); gameName = plugin.getCharacterName() + "'s game"; }
            }
            if (ImGui.Button("Refresh"))
            {
                plugin.RequestGameList();
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.InputText("Character name", ref characterName);
            ImGui.Combo("Role", ref role, Utils.roleNames.Keys.ToArray(), getRoleName, -1);

            if (selectedGame != null)
            {
                if (selectedGame.id != -1)
                {
                    ImGui.Text($"Ready to join {selectedGame.name}");
                    if (ImGui.Button("Join") && characterName.Length > 0)
                    {
                        var character = new FWTCharacter(characterName, Utils.roleNames.First(x => ((int)x.Key) == role).Key);
                        OnGameJoinRequest.Invoke(this,new GameJoinRequestEventArgs(selectedGame, character));
                    }
                }
                else
                {
                    ImGui.InputText("Game name", ref gameName);
                    ImGui.Text($"Ready to create new game {gameName}");
                    if (ImGui.Button("Join") && characterName.Length > 0 && gameName.Length > 0)
                    {
                        var character = new FWTCharacter(characterName, Utils.roleNames.First(x => ((int)x.Key) == role).Key);
                        OnGameCreateRequest.Invoke(this,new GameCreateRequestEventArgs(gameName, character));
                    }
                }
            }
        }
    }

    internal void OnConnectionUpdate(object? sender, ConnectionStatus v)
    {
        connected = v == ConnectionStatus.AUTHENTICATION_SUCCESS;
        if (!connected) { selectedGame = null; wrappers = []; }
    }

    public class GameJoinRequestEventArgs(FWTGameWrapperSerializable game, FWTCharacter character) : EventArgs
    {
        public FWTGameWrapperSerializable game = game;
        public FWTCharacter character = character;
    }
    public class GameCreateRequestEventArgs(string gameName, FWTCharacter character) : EventArgs
    {
        public string gameName = gameName;
        public FWTCharacter character = character;
    }
}

