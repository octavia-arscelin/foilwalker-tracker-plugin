using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace FoilwalkerTracker.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    // We give this window a constant ID using ###.
    // This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("Foilwalker Tracker Config##FWTConfigWindow")
    {
        Flags = ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(262, 90);
        SizeCondition = ImGuiCond.Always;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var serverHost = configuration.ServerHost;
        if (ImGui.InputText("Hostname", ref serverHost))
        {
            configuration.ServerHost = serverHost;
            configuration.Save();
        }

        var serverPort = configuration.ServerPort;
        if (ImGui.InputInt("Port", ref serverPort))
        {
            configuration.ServerPort = serverPort;
            configuration.Save();
        }
    }
}
