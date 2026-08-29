using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FoilwalkerTracker.Windows;

public class LoginWindow : Window, IDisposable
{
    private readonly Plugin plugin;
    private string password = "";

    // We give this window a hidden ID using ##.
    // The user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public LoginWindow(Plugin plugin)
        : base("Foilwalker Tracker Login##FWTrackerLogin", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse 
            | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse)
    {
        Size = new Vector2(350, 150);
        SizeCondition = ImGuiCond.Always;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text($"Logging in as {plugin.getCharacterIdentifier()}");
        ImGui.InputText("Password", ref password, flags:ImGuiInputTextFlags.Password);

        ImGui.Spacing();
        if(plugin.connectionStatus == ConnectionStatus.LOGIN_SUCCESS)
        {
            if (ImGui.Button("Disconnect"))
            {
                plugin.Disconnect();
            }
        }
        else
        {
            if (ImGui.Button("Connect"))
            {
                plugin.RequestServerConnection(password);
                password = "mysupersecretpassword";
            }
        }
        ImGui.SameLine();
        if (ImGui.Button("Settings")) plugin.ToggleConfigUi();

        ImGui.Text($"Status: {Utils.connectionStrings[plugin.connectionStatus]}");
    }
}
