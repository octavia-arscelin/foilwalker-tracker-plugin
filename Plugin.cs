using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FoilwalkerTracker.Windows;
using FoilwalkerTrackerLib.Model;
using FoilwalkerTrackerLib.Networking;
using System;
using System.IO;
using System.Threading.Tasks;

namespace FoilwalkerTracker;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/fwt";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("Foilwalker Tracker");
    private ConfigWindow ConfigWindow { get; init; }
    private GameListWindow GameListWindow { get; init; }
    private LoginWindow LoginWindow { get; init; }
    private GameWindow GameWindow { get; init; }
    private GameAdminWindow GameAdminWindow { get; init; }
    private GameRequestsWindow GameRequestsWindow { get; init; }
    private ClientNetworkingHandler ClientNetworkingHandler { get; init; }

    public ConnectionStatus connectionStatus = ConnectionStatus.OFFLINE;

    public FWTGame? currentGame;
    public long characterId;
    public long gameId;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        ClientNetworkingHandler = new ClientNetworkingHandler(this);

        // You might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        GameListWindow = new GameListWindow(this);
        LoginWindow = new LoginWindow(this);
        GameWindow = new GameWindow(this);
        GameAdminWindow = new GameAdminWindow(this);
        GameRequestsWindow = new GameRequestsWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(GameListWindow);
        WindowSystem.AddWindow(LoginWindow);
        WindowSystem.AddWindow(GameWindow);
        WindowSystem.AddWindow(GameAdminWindow);
        WindowSystem.AddWindow(GameRequestsWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the current context window."
        });

        // Tell the UI system that we want our windows to be drawn through the window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        // Adds another button doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        //Chat interaction
        ChatGui.ChatMessage += GameWindow.OnChatMessage;

        //Connection update
        ClientNetworkingHandler.OnConnectionUpdate += OnConnectionUpdate;
        ClientNetworkingHandler.OnConnectionUpdate += GameListWindow.OnConnectionUpdate;

        //Gamelist update
        ClientNetworkingHandler.OnGameListReceived += GameListWindow.OnGameListReceived;

        //Game join
        GameListWindow.OnGameCreateRequest += ClientNetworkingHandler.OnGameCreateRequest;
        GameListWindow.OnGameJoinRequest += ClientNetworkingHandler.OnGameJoinRequest;
        ClientNetworkingHandler.OnGameJoin += OnGameJoin;

        //Game update
        ClientNetworkingHandler.OnGameUpdate += GameWindow.OnGameUpdate;
        ClientNetworkingHandler.OnActionRequestReceived += GameRequestsWindow.OnActionRequestReceived;
        ClientNetworkingHandler.OnActionAcknowledgeReceived += GameRequestsWindow.OnActionAcknowledgeReceived;

        GameWindow.OnLocalCharacterUpdate += ClientNetworkingHandler.OnLocalCharacterUpdate;
        GameWindow.OnActionRequest += ClientNetworkingHandler.OnActionRequest;

        GameAdminWindow.OnMobCreated += ClientNetworkingHandler.OnMobCreated;
        GameAdminWindow.OnMobRemoved += ClientNetworkingHandler.OnMobRemoved;

        GameRequestsWindow.OnRequestAcknowledged += ClientNetworkingHandler.OnRequestAcknowledged;
        GameRequestsWindow.OnRequestDenied += ClientNetworkingHandler.OnRequestDenied;

        GameListWindow.OnGameLeaveRequest += ClientNetworkingHandler.OnGameLeaveRequest;
        ClientNetworkingHandler.OnGameLeaveResponseReceived += OnGameLeave;

        Log.Information($"=== Initialization complete ===");
    }

    public void Dispose()
    {
        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        GameListWindow.Dispose();
        LoginWindow.Dispose();
        GameWindow.Dispose();
        GameAdminWindow.Dispose();
        GameRequestsWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        //Chat interaction
        ChatGui.ChatMessage -= GameWindow.OnChatMessage;
    
        //Connection update
        ClientNetworkingHandler.OnConnectionUpdate -= OnConnectionUpdate;
        ClientNetworkingHandler.OnConnectionUpdate -= GameListWindow.OnConnectionUpdate;

        //Gamelist update
        ClientNetworkingHandler.OnGameListReceived -= GameListWindow.OnGameListReceived;

        //Game join
        GameListWindow.OnGameCreateRequest -= ClientNetworkingHandler.OnGameCreateRequest;
        GameListWindow.OnGameJoinRequest -= ClientNetworkingHandler.OnGameJoinRequest;
        ClientNetworkingHandler.OnGameJoin -= OnGameJoin;

        //Game update
        ClientNetworkingHandler.OnGameUpdate -= GameWindow.OnGameUpdate;
        ClientNetworkingHandler.OnActionRequestReceived -= GameRequestsWindow.OnActionRequestReceived;
        ClientNetworkingHandler.OnActionAcknowledgeReceived -= GameRequestsWindow.OnActionAcknowledgeReceived;

        GameWindow.OnLocalCharacterUpdate -= ClientNetworkingHandler.OnLocalCharacterUpdate;
        GameWindow.OnActionRequest -= ClientNetworkingHandler.OnActionRequest;

        GameAdminWindow.OnMobCreated -= ClientNetworkingHandler.OnMobCreated;
        GameAdminWindow.OnMobRemoved -= ClientNetworkingHandler.OnMobRemoved;

        GameRequestsWindow.OnRequestAcknowledged -= ClientNetworkingHandler.OnRequestAcknowledged;
        GameRequestsWindow.OnRequestDenied -= ClientNetworkingHandler.OnRequestDenied;

        GameListWindow.OnGameLeaveRequest -= ClientNetworkingHandler.OnGameLeaveRequest;
        ClientNetworkingHandler.OnGameLeaveResponseReceived -= OnGameLeave;

        //Final dispose
        ClientNetworkingHandler.Dispose();
    }

    private void OnCommand(string command, string sargs)
    {
        // In response to the slash command, toggle the display status of our main ui
        switch (command)
        {
            case "/fwt":
                if (sargs.Length == 0)
                {
                    if (connectionStatus != ConnectionStatus.AUTHENTICATION_SUCCESS) LoginWindow.Toggle();
                    else if (currentGame == null) GameListWindow.Toggle();
                    else if (!GameWindow.IsOpen && !GameRequestsWindow.IsOpen)
                    {
                        GameWindow.Toggle();
                        GameRequestsWindow.Toggle();
                    }
                    else
                    {
                        if (GameWindow.IsOpen) GameWindow.Toggle();
                        if (GameRequestsWindow.IsOpen) GameRequestsWindow.Toggle();
                    }
                }
                var args = sargs.Split(" ");
                switch (args[0])
                {
                    case "login":
                        LoginWindow.Toggle();
                        break;
                    case "gamelist":
                        GameListWindow.Toggle();
                        break;
                    case "game":
                        if (!GameWindow.IsOpen && !GameRequestsWindow.IsOpen)
                        {
                            GameWindow.Toggle();
                            GameRequestsWindow.Toggle();
                        }
                        else
                        {
                            if (GameWindow.IsOpen) GameWindow.Toggle();
                            if (GameRequestsWindow.IsOpen) GameRequestsWindow.Toggle();
                        }
                        break;
                    case "admin":
                        GameAdminWindow.Toggle();
                        break;
                }
                break;
            default:
                break;
        }
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => GameListWindow.Toggle();
    public void ToggleLoginUi() => LoginWindow.Toggle();

    public async Task<int> RequestServerConnection(string password)
    {
        OnConnectionUpdate(this, ConnectionStatus.CONNECTING);
        try
        {
            await ClientNetworkingHandler.ConnectToServerAsync(Configuration.ServerHost,Configuration.ServerPort);
            ClientNetworkingHandler.SendMessage(new FWTLoginRequest(getCharacterIdentifier(), password));
            return 0;
        }
        catch (Exception e)
        {
            Log.Error($"=== Connection error ===");
            Log.Error(e.ToString());
            return -1;
        }
    }

    public string getCharacterIdentifier()
    {
        if (PlayerState.IsLoaded)
            return PlayerState.CharacterName + "@" + PlayerState.HomeWorld.Value.Name.ToString();
        throw new Exception("Not logged in on a character.");
    }

    public string getCharacterName()
    {
        if(PlayerState.IsLoaded) return PlayerState.CharacterName;
        throw new Exception("Not logged in on a character.");
    }

    private void OnConnectionUpdate(object? sender, ConnectionStatus message)
    {
        connectionStatus = message;
        if(message != ConnectionStatus.AUTHENTICATION_SUCCESS)
        {
            currentGame = null;
            gameId = -1;
            characterId = -1;
        }
        else
        {
            if (LoginWindow.IsOpen) LoginWindow.Toggle();
            if (!GameListWindow.IsOpen) GameListWindow.Toggle();
        }
    }

    internal void RequestGameList()
    {
        if(connectionStatus == ConnectionStatus.AUTHENTICATION_SUCCESS)
        {
            ClientNetworkingHandler.SendMessage(new FWTGameListRequest());
        }
    }

    public void outputToLog(string message)
    {
        Log.Info(message);
    }

    internal void OnGameJoin(object? sender, ClientNetworkingHandler.GameJoinResponseEventArgs e)
    {
        currentGame = e.game;
        characterId = e.characterId;
        gameId = e.gameId;
        if (LoginWindow.IsOpen) LoginWindow.Toggle();
        if (GameListWindow.IsOpen) GameListWindow.Toggle();
        if (!GameWindow.IsOpen) GameWindow.Toggle();
        if (!GameRequestsWindow.IsOpen) GameRequestsWindow.Toggle();
        if (e.admin) GameAdminWindow.Toggle();
    }

    internal void Disconnect()
    {
        OnGameLeave(this,new FWTGameLeaveResponse(gameId));
        ClientNetworkingHandler.Disconnect();
    }

    private void OnGameLeave(object? sender, FWTGameLeaveResponse e)
    {
        currentGame = null;
        characterId = -1;
        gameId = -1;
        if (!GameListWindow.IsOpen) GameListWindow.Toggle();
        if (GameWindow.IsOpen) GameWindow.Toggle();
        if (GameRequestsWindow.IsOpen) GameRequestsWindow.Toggle();
        if (GameAdminWindow.IsOpen) GameAdminWindow.Toggle();
    }
}
