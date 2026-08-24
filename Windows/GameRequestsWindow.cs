using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Math;
using FoilwalkerTrackerLib.Model;
using FoilwalkerTrackerLib.Networking;
using System;
using System.Collections.Generic;
using System.Linq;

namespace FoilwalkerTracker.Windows
{
    public class GameRequestsWindow : Window, IDisposable
    {
        private Plugin plugin;
        private List<FWTActionRequest> actionRequests = [];

        public event EventHandler<long> OnRequestAcknowledged;
        public event EventHandler<long> OnRequestDenied;

        public GameRequestsWindow(Plugin plugin) : base("Foilwalker Requests###FWTRequestsWindow", ImGuiWindowFlags.NoResize) 
        {
            this.plugin = plugin;
            SizeConstraints = new WindowSizeConstraints()
            {
                MinimumSize = new Vector2(200, 300)
            };
        }

        public void Dispose()
        {

        }

        public override void Draw()
        {
            if (plugin.currentGame == null) { ImGui.Text("Not connected to a game"); return; }
            foreach(FWTActionRequest request in actionRequests)
            {
                var instigator = plugin.currentGame.entities.First(x => x.id == request.instigatorId);
                ImGui.Text($"From {instigator.name}");
                ImGui.Text($"{Utils.actionNames[request.parameters.type]} {request.parameters.target.name} for {request.parameters.amount}");
                if(request.instigatorId != plugin.characterId)
                {
                    if (ImGui.Button($"Accept###Accept-{request.requestId}")) OnRequestAcknowledged.Invoke(this, request.requestId);
                    ImGui.SameLine();
                    if(ImGui.Button($"Decline###Decline-{request.requestId}")) OnRequestDenied.Invoke(this,request.requestId);
                }
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();
            }
        }

        internal void OnActionRequestReceived(object? sender, FWTActionRequest e)
        {
            if(!actionRequests.Exists(x => x.requestId == e.requestId)) actionRequests.Add(e);
        }

        internal void OnActionAcknowledgeReceived(object? sender, FWTActionAcknowledge e)
        {
            if (actionRequests.Exists(x => x.requestId == e.requestId)) actionRequests.Remove(actionRequests.First(x => x.requestId == e.requestId));
        }
    }
}
