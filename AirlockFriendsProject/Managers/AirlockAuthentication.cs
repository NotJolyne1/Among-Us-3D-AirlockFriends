using System;
using System.Collections;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AirlockFriends.Config;
using Il2CppFusion;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppSG.Airlock;
using Il2CppSystem.IO;
using Newtonsoft.Json.Linq;
using ShadowsMenu.Managers;
using UnityEngine;

namespace AirlockFriends.Managers
{
    public static class AirlockFriendsAuth
    {
        public static bool Reconnecting = false;
        private static string FilePath = System.IO.Path.Combine(Application.persistentDataPath, "AirlockFriendsPrivateKey.txt");
        public static ConnectionStatus connectionStatus = ConnectionStatus.Disconnected;




        public static string PrepareAuthenticationKey()
        {
            try
            {
                if (System.IO.File.Exists(FilePath))
                {
                    var key = System.IO.File.ReadAllText(FilePath).Trim();
                    if (!string.IsNullOrEmpty(key))
                    {
                        string OtherGamePath = FilePath.Contains("3D") ? FilePath.Replace("3D", "VR") : FilePath.Replace("VR", "3D");
                        if (!System.IO.File.Exists(OtherGamePath))
                            System.IO.File.WriteAllText(OtherGamePath, key);
                        return key;
                    }
                }

                string OtherGame = FilePath.Contains("3D") ? FilePath.Replace("3D", "VR") : FilePath.Replace("VR", "3D");
                if (System.IO.File.Exists(OtherGame))
                {
                    var key = System.IO.File.ReadAllText(OtherGame).Trim();
                    {
                        System.IO.File.WriteAllText(FilePath, key);
                        return key;
                    }
                }

                // unused, client no longer handles key generation
                string newKey = Guid.NewGuid().ToString("N");
                System.IO.File.WriteAllText(FilePath, newKey);
                System.IO.File.WriteAllText(OtherGame, newKey);
                return newKey;
            }
            catch (Exception ex)
            {
                Logging.Error("Error reading: " + ex);
                return "";
            }
        }

        public static void SavePrivateKey(string privateKey)
        {
            try
            {
                System.IO.File.WriteAllText(FilePath, privateKey);
                Logging.DebugLog($"Saved new private key: {privateKey}");
            }
            catch (Exception ex)
            {
                Logging.Error($"Failed to save private token: {ex.Message}");
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
                Logging.Warning("Connection to the server was lost! Attempting to reconnect...");
                Logging.Warning("This can be due to a server restart, maintenance, or the server being updated.");
                Logging.Warning("No action is needed on your part, just allow the server to restart which will be quick if this was planned.");
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
            Logging.Msg("Successfully reconnected!");
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

        // Not used, yet.
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

        public static void RPC_SendReliableData(PlayerRef target, string type)
        {
            if (GameReferences.Runner == null) return;
            try
            {
                JObject messageObject = new JObject
                {
                    ["Type"] = type,
                    ["FriendCode"] = AirlockFriendsOperations.FriendCode,
                    ["Actor"] = GameReferences.Rig.PState.PlayerId
                };

                string messageString = messageObject.ToString();
                byte[] messageBytes = Encoding.UTF8.GetBytes(messageString);
                Il2CppStructArray<byte> byteArray = new Il2CppStructArray<byte>(messageBytes.Length);

                for (int i = 0; i < messageBytes.Length; i++)
                    byteArray[i] = messageBytes[i];

                GameReferences.Runner.SendReliableDataToPlayer(target, byteArray);

            }
            catch (Exception ex)
            {
                Logging.Error($"Failed to send reliable data to Actor #{target.PlayerId}, please report this to the discord or github issues tab!: {ex.Message}");
            }
        }

        public static void OperationReceived(PlayerRef sender, Il2CppStructArray<byte> data)
        {
            string messageString;
            try
            {
                messageString = Encoding.UTF8.GetString(data.ToArray());
            }
            catch
            {
                Logging.Msg($"Failed to decode reliable data from Actor#{sender.PlayerId}");
                return;
            }

            try
            {
                JObject messageObject = JObject.Parse(messageString);
                string type = (string)messageObject["Type"];
                string friendCode = (string)messageObject["FriendCode"];
                int actor = messageObject["Actor"] != null ? (int)messageObject["Actor"] : sender.PlayerId;

                if (type == "IsUsing")
                {
                    var runner = GameReferences.Runner;
                    if (runner == null || runner.ActivePlayers == null) return;

                    for (int i = 0; i < runner.ActivePlayers.ToArray().Count; i++)
                        RPC_SendReliableData(runner.ActivePlayers.ToArray()[i], "ConfirmUsing");
                }
                else if (type == "ConfirmUsing")
                {
                    PlayerState senderState = Helpers.GetPlayerStateById(actor);
                    string name = senderState != null ? senderState.NetworkName.Value : $"Actor#{actor}";
                    ModUserVisuals.CleanupAll();
                    ModUserVisuals.TryAdd(senderState, friendCode);
                }
            }
            catch
            {
                Logging.Error($"Failed to parse data from Actor#{sender.PlayerId}, please report this!");
            }
        }
    }
}
