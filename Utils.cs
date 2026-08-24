using FoilwalkerTrackerLib.Model;
using System;
using System.Collections.Generic;
using System.Text;

namespace FoilwalkerTracker
{
    internal class Utils
    {
        public readonly static Dictionary<FWTCharacterRole, string> roleNames = new() {
            {FWTCharacterRole.TANK,"Tank"},
            {FWTCharacterRole.DPS,"DPS"},
            {FWTCharacterRole.SUPPORT,"Support"}
        };

        public readonly static Dictionary<FWTActionType, string> actionNames = new() {
            {FWTActionType.DAMAGE,"Damage"},
            {FWTActionType.HEAL,"Heal"},
            {FWTActionType.STAGGER,"Stagger"}
        };

        public readonly static Dictionary<ConnectionStatus, string> connectionStrings = new()
        {
            {ConnectionStatus.OFFLINE,"Awaiting connection..."},
            {ConnectionStatus.CONNECTING, "Connecting..." },
            {ConnectionStatus.SOCKET_ESTABLISHED, "Socket established"},
            {ConnectionStatus.SOCKET_FAILURE, "Socket failed to open"},
            {ConnectionStatus.AUTHENTICATION_SUCCESS, "Connected"},
            {ConnectionStatus.AUTHENTICATION_FAILURE, "Authentication failed; check your password"},
            {ConnectionStatus.CONNECTION_LOST, "Connection lost"}
        };
    }
}
