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
using AirlockFriends.MenuPages;

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
            PrivateKey = AirlockFriendsAuth.AuthenticateOnce();
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
            await RaiseEvent(json);
            MelonLogger.Msg("[AirlockFriends] Sent authentication request to server.");
        }

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
                        MelonLogger.Msg($"[AirlockFriends] Server raw: {msg}");

                        try
                        {
                            var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(msg);

                            if (data.TryGetProperty("authenticated", out var authProp))
                            {
                                if (authProp.ValueKind == System.Text.Json.JsonValueKind.True)
                                {
                                    string publicFriendCode = data.GetProperty("friendCode").GetString();
                                    AssignFriendCode(publicFriendCode);

                                    if (data.TryGetProperty("privateKey", out var PrivateToken))
                                    {
                                        string newPrivateKey = PrivateToken.GetString();
                                        AirlockFriendsAuth.SaveNewPrivateKey(newPrivateKey);
                                        MelonLogger.Msg($"[AirlockFriends] [DEBUG] Saved new privateKey from server: {newPrivateKey}");
                                    }

                                    string message = data.TryGetProperty("message", out var m) ? m.GetString() : "Authenticated successfully!";
                                    NotificationLib.QueueNotification($"[<color=magenta>AUTH</color>] {message} (FriendCode: <color=lime>{publicFriendCode}</color>)");
                                    continue;
                                }

                                if (authProp.ValueKind == System.Text.Json.JsonValueKind.False &&
                                    data.TryGetProperty("action", out var actionProp) &&
                                    actionProp.GetString() == "AuthViolation")
                                {
                                    string message = data.TryGetProperty("error", out var error) ? error.GetString() : "AuthViolation";
                                    MelonLogger.Error($"[AirlockFriends] AUTH FAILED: {message}");
                                    NotificationLib.QueueNotification($"[<color=red>Unauthorized</color>] {message}");
                                    await Disconnect();
                                    continue;
                                }
                            }

                            if (data.TryGetProperty("EventIntercepted", out var interceptionProp))
                            {
                                string message = data.TryGetProperty("error", out var error) ? error.GetString() : "PostAuthKeyViolation";
                                MelonLogger.Error($"[AirlockFriends] AUTH FAILED: {message}");
                                NotificationLib.QueueNotification($"[<color=red>Unauthorized</color>] {message}");
                                await Disconnect();
                                continue;
                            }


                            if (data.TryGetProperty("status", out var statusProp))
                            {
                                string status = statusProp.GetString();

                                switch (status)
                                {
                                    case "failed":
                                        string reason = data.TryGetProperty("reason", out var reasonProp) ? reasonProp.GetString() : "Unknown reason";
                                        string toFriend = data.TryGetProperty("toFriendCode", out var toProp) ? toProp.GetString() : "";
                                        NotificationLib.QueueNotification($"[<color=red>FAILED</color>] {reason}");
                                        MenuPages.MenuPage1.ReceiveChatMessage(toFriend,
                                            $"Operation failed: {reason}", true, false);
                                        break;

                                    case "success":
                                        break;
                                }
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
                                            RPC_OnRequestReceived(fromFriend);
                                            NotificationLib.QueueNotification($"[<color=magenta>FRIEND-REQUEST</color>] <color=lime>{fromFriend}</color> wants to friend you!");
                                        }
                                        break;

                                    case "friendRequest":
                                        if (data.TryGetProperty("toFriendCode", out var sentFR))
                                        {
                                            string toPlayer = sentFR.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Friend request sent to {toPlayer}");
                                            NotificationLib.QueueNotification($"[<color=magenta>FRIEND-REQUEST</color>] Friend Request Sent to <color=lime>{toPlayer}</color>!");
                                        }
                                        break;

                                    case "friendAccepted":
                                        if (data.TryGetProperty("byFriendCode", out var acceptedBy))
                                        {
                                            string friend = acceptedBy.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Friend accepted by {friend}");
                                            NotificationLib.QueueNotification($"[<color=green>FRIEND ACCEPTED</color>] <color=lime>{friend}</color> is now your friend!");
                                            MenuPage1.AddFriend(friend, "Online", friend);
                                        }
                                        break;

                                    case "friendRejected":
                                        if (data.TryGetProperty("byFriendCode", out var rejectedBy))
                                        {
                                            string friend = rejectedBy.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Friend rejected by {friend}");
                                            NotificationLib.QueueNotification($"[<color=red>FRIEND REJECTED</color>] <color=lime>{friend}</color> rejected your friend request!");
                                        }
                                        break;

                                    case "friendRemoved":
                                    case "friendRemove":
                                        if (data.TryGetProperty("byFriendCode", out var removedBy))
                                        {
                                            string friend = removedBy.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Friend removed: {friend}");
                                            NotificationLib.QueueNotification($"[<color=red>FRIEND REMOVED</color>] <color=lime>{friend}</color> is no longer your friend!");
                                        }
                                        break;

                                    case "messageReceived":
                                        if (data.TryGetProperty("fromFriendCode", out var senderCode) &&
                                            data.TryGetProperty("message", out var MessageContent))
                                        {
                                            string sender = senderCode.GetString();
                                            string Message = MessageContent.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Message from {sender}: {Message}");
                                            NotificationLib.QueueNotification($"[<color=magenta>MESSAGE</color>] From: <color=lime>{sender}</color> {Message}");
                                            MenuPages.MenuPage1.ReceiveChatMessage(sender, Message);
                                        }
                                        break;

                                    case "sendMessage":
                                        if (data.TryGetProperty("targetFriendCode", out var toFriendCode))
                                        {
                                            string sentTo = toFriendCode.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Message sent to {sentTo}!");
                                            NotificationLib.QueueNotification($"[<color=magenta>SENT</color>] Sent message to <color=lime>{sentTo}</color>!");
                                        }
                                        break;

                                    default:
                                        MelonLogger.Msg($"[AirlockFriends] Unhandled event raised?! {msg}");
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Error($"[AirlockFriends] Failed to deserialize server event: {ex.Message}");
                            NotificationLib.SendNotification($"[AirlockFriends] Failed to deserialize server event: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[AirlockFriends] Server sent an exception: {ex.Message}");
                    break;
                }
            }
            await Disconnect();
        }

        public static async Task RaiseEvent(string OperationInfo)
        {
            if (!IsConnected) return;
            byte[] bytes = Encoding.UTF8.GetBytes(OperationInfo);
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
                toFriendCode = targetFriendCode
            };

            string json = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(json);
            MelonLogger.Msg($"[AirlockFriends] Sent friend request to {targetFriendCode}");
        }

        public static void RPC_OnRequestReceived(string fromFriendCode)
        {
            MelonLogger.Msg($"[AirlockFriends] Friend request received from {fromFriendCode}");
            NotificationLib.QueueNotification($"Friend request received from {fromFriendCode}");
            MenuPage1.friendRequests.Add(fromFriendCode);
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
            await RaiseEvent(json);
            MelonLogger.Msg($"[AirlockFriends] Sent message to {targetFriendCode}: {message}");
        }















        public static async Task RPC_FriendAccept(string acceptFriendCode)
        {
            if (string.IsNullOrEmpty(acceptFriendCode) || string.IsNullOrEmpty(FriendCode)) return;

            var request = new
            {
                type = "friendAccept",
                fromPrivateKey = PrivateKey,
                acceptFriendCode = acceptFriendCode
            };

            string json = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(json);
            MelonLogger.Msg($"[AirlockFriends] Accepted friend request from {acceptFriendCode}");
        }

        public static async Task RPC_FriendReject(string rejectFriendCode)
        {
            if (string.IsNullOrEmpty(rejectFriendCode) || string.IsNullOrEmpty(FriendCode)) return;

            var request = new
            {
                type = "friendReject",
                fromPrivateKey = PrivateKey,
                rejectFriendCode = rejectFriendCode
            };

            string json = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(json);
            MelonLogger.Msg($"[AirlockFriends] Rejected friend request from {rejectFriendCode}");
        }

        public static async Task RPC_FriendRemove(string removeFriendCode)
        {
            if (string.IsNullOrEmpty(removeFriendCode) || string.IsNullOrEmpty(FriendCode)) return;

            var request = new
            {
                type = "friendRemove",
                fromPrivateKey = PrivateKey,
                removeFriendCode = removeFriendCode
            };

            string json = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(json);
            MelonLogger.Msg($"[AirlockFriends] Removed friend {removeFriendCode}");
        }























        public static async Task Disconnect()
        {
            if (!IsConnected) return;
            try
            {
                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", _cts.Token);
                MelonLogger.Msg("[AirlockFriends] Disconnected from WS server.");
                NotificationLib.SendNotification("<color=magenta>Disconnected</color> from WS server.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[AirlockFriends] Disconnect failed: " + ex.Message);
            }
        }
    }
}
