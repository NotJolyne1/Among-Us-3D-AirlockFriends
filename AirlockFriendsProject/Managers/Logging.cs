using AirlockFriends.Config;
using MelonLoader;

namespace AirlockFriends.Managers
{
    internal class Logging
    {
        public static void Msg(string message) => MelonLogger.Msg($"[AirlockFriends] {message}");

        public static void Warning(string warn) => MelonLogger.Warning($"[AirlockFriends] {warn}");

        public static void Error(string error) => MelonLogger.Warning($"[AirlockFriends] {error}");

        public static void DebugLog(string log)
        {
            if (Settings.DebugMode)
                MelonLogger.Msg($"[AirlockFriends] [DEBUG] {log}");
        }
    }
}
