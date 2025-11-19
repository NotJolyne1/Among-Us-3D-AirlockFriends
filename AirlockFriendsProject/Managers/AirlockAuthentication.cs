using System;
using System.IO;
using UnityEngine;
using MelonLoader;

namespace AirlockFriends.Managers
{
    public static class AirlockFriendsAuth
    {
        private static string FilePath = Path.Combine(Application.persistentDataPath, "AirlockFriendsPrivateKey.txt");

        public static string FriendshipAuthentication()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    string key = File.ReadAllText(FilePath);

                    key = key.Replace("\n", "").Replace("\r", "").Trim();

                    if (!string.IsNullOrEmpty(key))
                    {
                        MelonLogger.Msg($"[AirlockFriends] Loaded PrivateKey: {key}");
                        return key;
                    }

                    MelonLogger.Warning("[AirlockFriends] PrivateKey file empty. Sending empty key to server.");
                    return "";
                }
                else
                {
                    File.WriteAllText(FilePath, "");
                    MelonLogger.Warning("[AirlockFriends] No key file. Created empty key file. Sending empty key to server.");
                    return "";
                }
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[AirlockFriends] Error reading PrivateKey: " + ex);
                return "";
            }
        }

        public static void SaveNewPrivateKey(string privateKey)
        {
            try
            {
                File.WriteAllText(FilePath, privateKey);
                MelonLogger.Msg($"[AirlockFriends] [dEBUG] Saved new PrivateKey: {privateKey}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[AirlockFriends] [DEBUG] Failed to save PrivateKey: " + ex);
            }
        }
    }
}
