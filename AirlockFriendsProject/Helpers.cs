using AirlockFriends.Config;
using Il2CppFusion;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Network;
using Il2CppSteamworks;

namespace AirlockFriends
{
    public class Helpers
    {
        public static PlayerState GetPlayerStateById(int playerId)
        {

            foreach (PlayerState player in UnityEngine.Object.FindObjectOfType<SpawnManager>().ActivePlayerStates)
            {
                string playerName = player.NetworkName?.Value ?? "Unknown";

                if (player.PlayerId == playerId)
                {
                    return player;
                }
            }

            return null;
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

        public static string GetSelfSteamID()
        {
            try
            {
                return SteamUser.GetSteamID().ToString();
            }
            catch
            {
                return "null";
            }
        }
    }
}
