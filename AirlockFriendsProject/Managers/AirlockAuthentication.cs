using System;
using System.IO;
using UnityEngine;
using MelonLoader;
using System.Collections;

namespace AirlockFriends.Managers
{
    public static class AirlockFriendsAuth
    {
        public static bool Reconnecting = false;
        private static string FilePath = Path.Combine(Application.persistentDataPath, "AirlockFriendsPrivateKey.txt");
        public static ConnectionStatus connectionStatus = ConnectionStatus.Disconnected;
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


        public static IEnumerator AttemptReconnection(bool NotCrash = false)
        {
            if (Reconnecting)
                yield break;
            int connectionAttempts = 0;
            if (!NotCrash)
            {
                MelonLogger.Warning("[AirlockFriends] Connection to the server was lost! Attempting to reconnect...");
                MelonLogger.Warning("[AirlockFriends] This can be due to a server restart, maintenance, or the server being updated.");
                MelonLogger.Warning("[AirlockFriends] No action is needed on your part, just allow the server to restart which will be quick if this was planned.");
            }

            while (!AirlockFriendsOperations.IsConnected)
            {
                Reconnecting = true;
                yield return new WaitForSeconds(1f);
                AirlockFriendsOperations.PrepareAuthentication();
                connectionAttempts++;
                yield return new WaitForSeconds(2f);
            }
            Reconnecting = false;
            MelonLogger.Msg("[AirlockFriends] Successfully reconnected!");
        }

    }
}
