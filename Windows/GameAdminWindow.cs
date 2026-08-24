using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Common.Math;
using FoilwalkerTrackerLib.Model;
using System;
using System.Linq;

namespace FoilwalkerTracker.Windows
{
    public class GameAdminWindow : Window, IDisposable
    {
        private Plugin plugin;
        private string mobName;
        private long selectedMob;

        public event EventHandler<FWTMob> OnMobCreated;
        public event EventHandler<FWTMob> OnMobRemoved;

        public GameAdminWindow(Plugin plugin) : base("Foilwalker Admin###FWTTrackerAdminWindow") 
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
            ImGui.InputText("Name",ref mobName);
            if(ImGui.Button("Add mob"))
            {
                var mob = new FWTMob(mobName);
                OnMobCreated.Invoke(this, mob);
            }
            if(plugin.currentGame.GetMobs().Length > 0)
            {
                ImGui.Separator();
                FWTMob? pres = plugin.currentGame.GetMobs().FirstOrDefault(x => x.id == selectedMob, null);
                if (pres == null) selectedMob = plugin.currentGame.GetMobs().First().id;
                FWTMob mob = plugin.currentGame.GetMobs().First(x => x.id == selectedMob);
                var i = plugin.currentGame.GetMobs().IndexOf(mob);
                if (ImGui.Combo("Mob", ref i, plugin.currentGame.GetMobs(), getMobName)) { selectedMob = plugin.currentGame.GetMobs()[i].id; }
                if(ImGui.Button("Remove mob")) OnMobRemoved.Invoke(this, mob);
            }
        }

        private string getMobName(FWTMob mob)
        {
            return mob.name;
        }
    }
}
