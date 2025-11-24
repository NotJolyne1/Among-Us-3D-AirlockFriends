using System;
using System.IO;
using UnityEngine;
using MelonLoader;

namespace AirlockFriends.Managers
{
    public static class AirlockFriendsAuth
    {
        private static string FilePath = Path.Combine(Application.persistentDataPath, "AirlockFriendsPrivateKey.txt");
        public static ConnectionStatus connectionStatus;
        public static string PrepareAuthenticationKey()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string key = File.ReadAllText(FilePath);
                    key = key.Replace("\n", "").Replace("\r", "").Trim();

                    if (!string.IsNullOrEmpty(key))
                    {
                        MelonLogger.Msg($"[AirlockFriends] [DEBUG] Loaded PrivateKey: {key}");
                        return key;
                    }

                    return "";
                }
                else
                {
                    File.WriteAllText(FilePath, "");
                    return "";
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[AirlockFriends] Error reading PrivateKey: " + ex);
                return "";
            }
        }

        public static void SavePrivateKey(string privateKey)
        {
            try
            {
                File.WriteAllText(FilePath, privateKey);
                MelonLogger.Msg($"[AirlockFriends] [DEBUG] Saved new PrivateKey: {privateKey}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[AirlockFriends] [DEBUG] Failed to save PrivateKey: " + ex);
            }
        }

        public enum ConnectionStatus
        {
            Established,
            Disconnected,
            Rejected,
            Authenticating,
            Connecting,
            ForciblyClosed,
            Failed
        }




    }
}
