using System;
using System.IO;
using UnityEngine;
using MelonLoader;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

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
                    var key = File.ReadAllText(FilePath).Trim();
                    if (!string.IsNullOrEmpty(key))
                    {
                        var other = FilePath.Contains("3D") ? FilePath.Replace("3D", "VR") : FilePath.Replace("VR", "3D");
                        if (!File.Exists(other))
                            File.WriteAllText(other, key);
                        return key;
                    }
                }

                var OtherGame = FilePath.Contains("3D") ? FilePath.Replace("3D", "VR") : FilePath.Replace("VR", "3D");
                if (File.Exists(OtherGame))
                {
                    var key = File.ReadAllText(OtherGame).Trim();
                    {
                        File.WriteAllText(FilePath, key);
                        return key;
                    }
                }

                var newKey = Guid.NewGuid().ToString("N");
                File.WriteAllText(FilePath, newKey);
                File.WriteAllText(OtherGame, newKey);
                return newKey;
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[AirlockFriends] Error reading: " + ex);
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

            if (Main.AFBanned)
                connectionStatus = ConnectionStatus.Rejected;


            int connectionAttempts = 0;
            if (!NotCrash && !Main.AFBanned)
            {
                MelonLogger.Warning("[AirlockFriends] Connection to the server was lost! Attempting to reconnect...");
                MelonLogger.Warning("[AirlockFriends] This can be due to a server restart, maintenance, or the server being updated.");
                MelonLogger.Warning("[AirlockFriends] No action is needed on your part, just allow the server to restart which will be quick if this was planned.");
            }

            while (!AirlockFriendsOperations.IsConnected)
            {
                connectionStatus = ConnectionStatus.Disconnected;
                Reconnecting = true;
                yield return new WaitForSeconds(1f);
                AirlockFriendsOperations.PrepareAuthentication();
                connectionAttempts++;
                yield return new WaitForSeconds(2f);
            }
            Reconnecting = false;
            MelonLogger.Msg("[AirlockFriends] Successfully reconnected!");
        }

        public static IEnumerator NotifyFriendGroup()
        {
            while (true)
            {
                if (AirlockFriendsOperations.IsConnected)
                {
                    _ = AirlockFriendsOperations.RPC_NotifyFriendGroup(updateSelf: false);
                }
                yield return new WaitForSeconds(4f);
            }
        }

        public static string Encrypt(string input)
        {
            using SHA256 Key = SHA256.Create();
            byte[] bytes = Encoding.UTF8.GetBytes(input);
            byte[] hash = Key.ComputeHash(bytes);
            StringBuilder sb = new StringBuilder();
            foreach (byte bit in hash)
                sb.Append(bit.ToString("x2"));
            return sb.ToString();
        }
    }
}
