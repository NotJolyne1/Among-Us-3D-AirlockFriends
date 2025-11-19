using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Il2CppFusion;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Graphics;
using Il2CppSG.Airlock.Roles;
using Il2CppSG.Airlock.Sabotage;
using MelonLoader;
using AirlockFriends.Config;
using UnityEngine;
using System.Net.WebSockets;

namespace AirlockFriends.Managers
{
    public class AirlockFriendsOperations
    {
        public static string PrivateKey { get; private set; }
        public static string FriendCode { get; private set; } 

        private static ClientWebSocket socket;
        private static CancellationTokenSource _cts;

        public static bool IsConnected => socket != null && socket.State == WebSocketState.Open;

        public static async void Initialize()
        {
            PrivateKey = AirlockFriendsAuth.FriendshipAuthentication();
            MelonLogger.Msg($"[AirlockFriends] [DEBUG] Using PrivateKey: {PrivateKey}");

            await ConnectToServer();

            await AuthenticateWithServer();
        }

        private static async Task ConnectToServer()
        {
            if (IsConnected) return;

            socket = new ClientWebSocket();
            _cts = new CancellationTokenSource();
            socket.Options.SetRequestHeader("User-Agent", $"AirlockFriends/{Settings.Version}");

            try
            {
                var serverUri = new Uri("wss://lank-lucretia-timocratical.ngrok-free.dev/");
                await socket.ConnectAsync(serverUri, _cts.Token);
                MelonLogger.Msg("[AirlockFriends] Connected to server!");

                _ = Task.Run(ReceiveLoop);
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AirlockFriends] Connection failed: {ex.Message}");
            }
        }

        private static async Task AuthenticateWithServer()
        {
            if (!IsConnected) return;

            var authPayload = new
            {
                type = "authenticate",
                privateKey = PrivateKey
            };

            string json = System.Text.Json.JsonSerializer.Serialize(authPayload);
            await SendOperation(json);
            MelonLogger.Msg("[AirlockFriends] Sent authentication request to server.");
        }

        // receive main loop
        private static async Task ReceiveLoop()
        {
            var buffer = new byte[8192];

            while (IsConnected)
            {
                try
                {
                    var result = await socket.ReceiveAsync(buffer, _cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        MelonLogger.Msg($"[AirlockFriends] Server: {msg}");

                        try
                        {
                            var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(msg);

                            // Auth succeeded
                            if (data.TryGetProperty("authenticated", out var auth) && auth.GetBoolean())
                            {
                                string publicFriendCode = data.GetProperty("friendCode").GetString();
                                AssignFriendCode(publicFriendCode);

                                if (data.TryGetProperty("privateKey", out var PrivateToken))
                                {
                                    string newPrivateKey = PrivateToken.GetString();

                                    AirlockFriendsAuth.SaveNewPrivateKey(newPrivateKey);

                                    MelonLogger.Msg($"[AirlockFriends] [DEBUG] Saved new privateKey from server: {newPrivateKey}");
                                }
                                else
                                {
                                    MelonLogger.Warning("[AirlockFriends] Server authenticated but did NOT send a privateKey?!");
                                }

                                string message = data.TryGetProperty("message", out var m) ? m.GetString() : "Authenticated successfully!";

                                NotificationLib.QueueNotification($"[<color=magenta>AUTH</color>] {message} (FriendCode: <color=lime>{publicFriendCode}</color>)");
                                continue;
                            }


                            // Authentication Failed
                            if (data.TryGetProperty("authenticated", out var authFailed) && authFailed.ValueKind == System.Text.Json.JsonValueKind.False && data.TryGetProperty("action", out var actionProp) && actionProp.GetString() == "AuthViolation")
                            {
                                string message = data.TryGetProperty("error", out var error) ? error.GetString() : "AuthViolation";

                                MelonLogger.Error($"[AirlockFriends] AUTH FAILED: {message}");

                                NotificationLib.QueueNotification($"[<color=red>AUTH FAILED</color>] {message}"
                                );
                                continue;
                            }

                            if (data.TryGetProperty("status", out var statusProp) && statusProp.GetString() == "failed" && data.TryGetProperty("reason", out var reasonProp) && reasonProp.GetString() == "target offline")
                            {
                                string offlineFriend = data.TryGetProperty("toFriendCode", out var toCodeProp) ? toCodeProp.GetString() : "";

                                NotificationLib.QueueNotification($"[<color=red>OFFLINE</color>] <color=lime>{offlineFriend}</color> is currently offline!");
                                continue;
                            }

                            if (data.TryGetProperty("type", out var typeProp))
                            {
                                string type = typeProp.GetString();

                                switch (type)
                                {
                                    case "friendRequestReceived":
                                        if (data.TryGetProperty("fromFriendCode", out var fromFR))
                                        {
                                            string fromFriend = fromFR.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Friend request received from {fromFriend}");
                                            NotificationLib.QueueNotification($"[<color=magenta>FRIEND-REQUEST</color>] <color=lime>{fromFriend}</color> wants to friend you!"
                                            );
                                        }
                                        break;

                                    case "friendRequest":
                                        if (data.TryGetProperty("toFriendCode", out var sentFR))
                                        {
                                            string toPlayer = sentFR.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Friend request sent to {toPlayer}");
                                            NotificationLib.QueueNotification($"[<color=magenta>FRIEND-REQUEST</color>] Friend Request Sent to <color=lime>{toPlayer}</color>!"
                                            );
                                        }
                                        break;

                                    case "messageReceived":
                                        if (data.TryGetProperty("fromFriendCode", out var senderCode) &&
                                            data.TryGetProperty("message", out var messageProp))
                                        {
                                            string sender = senderCode.GetString();
                                            string text = messageProp.GetString();

                                            MelonLogger.Msg($"[AirlockFriends] Message from {sender}: {text}");
                                            NotificationLib.QueueNotification($"[<color=magenta>MESSAGE</color>] From: <color=lime>{sender}</color> {text}"
                                            );
                                        }
                                        break;

                                    case "sendMessage":
                                        if (data.TryGetProperty("targetFriendCode", out var toFriend))
                                        {
                                            string sentTo = toFriend.GetString();

                                            MelonLogger.Msg($"[AirlockFriends] Message sent to {sentTo}!");
                                            NotificationLib.QueueNotification($"[<color=magenta>SENT</color>] Sent message to <color=lime>{sentTo}</color>!"
                                            );
                                        }
                                        break;

                                    default:
                                        NotificationLib.QueueNotification($"[AirlockFriends] {msg}");
                                        break;
                                }
                            }
                            else
                            {
                                NotificationLib.QueueNotification($"[AirlockFriends] {msg}");
                            }
                        }
                        catch
                        {
                            NotificationLib.QueueNotification($"[AirlockFriends] {msg}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[AirlockFriends] WSS Receive loop error: {ex.Message}");
                    break;
                }
            }
            await Disconnect();
        }


        public static async Task SendOperation(string text)
        {
            if (!IsConnected) return;

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
        }

        public static void AssignFriendCode(string ServerPublicKey)
        {
            FriendCode = ServerPublicKey;
            MelonLogger.Msg($"[AirlockFriends] Server assigned FriendCode: {FriendCode}");
        }


        public static async Task RPC_FriendshipRequest(string targetFriendCode)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode)) return;

            var request = new
            {
                type = "friendRequest",
                fromPrivateKey = PrivateKey,
                fromFriendCode = FriendCode,
                toFriendCode = targetFriendCode
            };

            string json = System.Text.Json.JsonSerializer.Serialize(request);
            await SendOperation(json);
            MelonLogger.Msg($"[AirlockFriends] Sent friend request to {targetFriendCode}");
        }

        public static void RPC_OnRequestReceived(string fromFriendCode)
        {
            MelonLogger.Msg($"[AirlockFriends] Friend request received from {fromFriendCode}");
            NotificationLib.QueueNotification($"Friend request received from {fromFriendCode}");
        }

        public static async Task RPC_SendMessage(string targetFriendCode, string message)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(message) || string.IsNullOrEmpty(FriendCode))
                return;

            var payload = new
            {
                type = "sendMessage",
                targetFriendCode = targetFriendCode,
                fromPrivateKey = PrivateKey,
                fromFriendCode = FriendCode,
                message = message
            };

            string json = System.Text.Json.JsonSerializer.Serialize(payload);
            await SendOperation(json);
            MelonLogger.Msg($"[AirlockFriends] Sent message to {targetFriendCode}: {message}");
        }

        public static async Task Disconnect()
        {
            if (!IsConnected)
            {
                try
                {
                    await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", _cts.Token);
                    MelonLogger.Msg("[AirlockFriends] Disconnected from server.");
                }
                catch (Exception ex)
                {
                    MelonLogger.Error("[AirlockFriends] Disconnect failed: " + ex.Message);
                }
            }
        }

        // much easier to use => things lol
        public static string GetFriendCode() => FriendCode;
        public static string GetPrivateKey() => PrivateKey;
    }
}
