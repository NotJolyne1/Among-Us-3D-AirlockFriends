using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.Xml;
using Il2CppFusion;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Network;
using MelonLoader;
using ShadowsPublicMenu.Config;
using UnityEngine;

namespace ShadowsPublicMenu
{
    public class Helpers
    {
        public static string GetCurrentRoomCode()
        {
            try
            {
                var runner = UnityEngine.Object.FindObjectOfType<AirlockNetworkRunner>();
                if (runner == null)
                {
                    MelonLogger.Warning("[RoomHelper] AirlockNetworkRunner not found!");
                    return "null";
                }

                var session = runner.SessionInfo;
                if (session == null)
                {
                    MelonLogger.Warning("[RoomHelper] SessionInfo is null!");
                    return "null";
                }

                return session.Name ?? "Not in a room";
            }
            catch (System.Exception ex)
            {
                MelonLogger.Error("[RoomHelper] Failed to get room code: " + ex);
                Settings.ErrorCount += 1;
                return "null";
            }
        }

        public static PlayerState GetPlayerStateById(int playerId)
        {

            foreach (PlayerState player in GameReferences.Spawn.PlayerStates)
            {
                string playerName = player.NetworkName?.Value ?? "Unknown";

                if (player.PlayerId == playerId)
                {
                    return player;
                }
            }

            return null;
        }



        public static string GetColorHexFromID(int id)
        {
            return id switch
            {
                0 => "#FF0000",
                1 => "#0000FF",
                2 => "#00FF00",
                3 => "#FF66B3",
                4 => "#FF8000",
                5 => "#FFFF00",
                6 => "#000000",
                7 => "#FFFFFF",
                8 => "#800080",
                9 => "#996633",
                10 => "#87CEFA",
                11 => "#90EE90",
                _ => "#808080"
            };
        }

        public static Color GetColorCodeFromID(int id)
        {
            switch (id)
            {
                case 0: return new Color(1f, 0f, 0f);
                case 1: return new Color(0f, 0f, 1f);
                case 2: return new Color(0f, 1f, 0f);
                case 3: return new Color(1f, 0.4f, 0.7f);
                case 4: return new Color(1f, 0.5f, 0f);
                case 5: return new Color(1f, 1f, 0f);
                case 6: return new Color(0f, 0f, 0f);
                case 7: return new Color(1f, 1f, 1f);
                case 8: return new Color(0.5f, 0f, 0.5f);
                case 9: return new Color(0.6f, 0.3f, 0f);
                case 10: return new Color(0.53f, 0.81f, 0.98f);
                case 11: return new Color(0.56f, 0.93f, 0.56f);
                default: return Color.gray;
            }
        }

        public static string GetColorName(int colorIndex)
        {
            return colorIndex switch
            {
                0 => "Cyan",
                1 => "Blue",
                2 => "Magenta",
                3 => "Red",
                4 => "Yellow",
                5 => "Green",
                6 => "White",
                7 => "Gray",
                8 => "RGB",
                _ => "Unknown"
            };
        }


        public static PlayerRef GetPlayerRefFromID(int ID)
        {
            PlayerRef playerRef = (PlayerRef)ID;
            return playerRef;
        }

        public static PlayerState GetPlayerStateFromRef(PlayerRef playerRef)
        {
            foreach (PlayerState state in GameReferences.Spawn.PlayerStates)
            {
                if (state != null && state.PlayerId == playerRef.PlayerId)
                {
                    return state;
                }
            }

            return null;
        }

        public enum PlayerColor
        {
            Red = 0,
            Blue = 1,
            Green = 2,
            Pink = 3,
            Orange = 4,
            Yellow = 5,
            Black = 6,
            White = 7,
            Purple = 8,
            Brown = 9,
            LightBlue = 10,
            LightGreen = 11
        }


    }
}
