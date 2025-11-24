using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AirlockFriends.Config;
using AirlockFriends.UI;
using Il2CppFusion;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Graphics;
using Il2CppSG.Airlock.Network;
using Il2CppSG.Airlock.Roles;
using Il2CppSG.Airlock.Sabotage;
using Il2CppSG.Airlock.XR;
using MelonLoader;
using Newtonsoft.Json;
using UnityEngine;
using static AirlockFriends.Managers.AirlockFriendsAuth;
using static AirlockFriends.UI.FriendGUI;

namespace AirlockFriends.Managers
{
    public class AirlockFriendsOperations
    {
        public static string PrivateKey { get; private set; }
        public static string FriendCode { get; private set; }
        private static string MyName = FriendCode;


        private static ClientWebSocket socket;
        private static CancellationTokenSource _cts;
        public static bool AllowFriendRequests = true;
        public static string JoinPrivacy = "Joinable";
        public static bool AllowMessages = true;
        public static bool AllowInvites = true;

        public static bool IsConnected => socket != null && socket.State == WebSocketState.Open;

        public static async void Initialize()
        {
            PrivateKey = AirlockFriendsAuth.PrepareAuthenticationKey();
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
                var serverUri = new Uri("wss://lank-lucretia-timocratical.ngrok-free.dev");
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

            connectionStatus = ConnectionStatus.Authenticating;
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
                                        AirlockFriendsAuth.SavePrivateKey(newPrivateKey);
                                        MelonLogger.Msg($"[AirlockFriends] [DEBUG] Saved new privateKey from server: {newPrivateKey}");
                                    }

                                    string message = data.TryGetProperty("message", out var m) ? m.GetString() : "Authenticated successfully!";
                                    NotificationLib.QueueNotification($"[<color=magenta>AUTH</color>] {message} (FriendCode: <color=lime>{publicFriendCode}</color>)");

                                    if (data.TryGetProperty("AllowFriendRequests", out var allowFR))
                                        AllowFriendRequests = allowFR.GetBoolean();

                                    if (data.TryGetProperty("JoinPrivacy", out var joinPriv))
                                        JoinPrivacy = joinPriv.GetString();

                                    if (data.TryGetProperty("AllowMessages", out var allowMsg))
                                        AllowMessages = allowMsg.GetBoolean();

                                    if (data.TryGetProperty("AllowInvites", out var allowInv))
                                        AllowInvites = allowInv.GetBoolean();

                                    connectionStatus = ConnectionStatus.Established;
                                    MelonLogger.Msg($"[AirlockFriends] Values, Friend Requests: {AllowFriendRequests}, Join: {JoinPrivacy}, Allow Messging: {AllowMessages}, AllowInvites: {AllowInvites}");
                                    continue;
                                }

                                if (authProp.ValueKind == System.Text.Json.JsonValueKind.False &&
                                    data.TryGetProperty("action", out var actionProp) &&
                                    actionProp.GetString() == "AuthViolation")
                                {
                                    string message = data.TryGetProperty("error", out var error) ? error.GetString() : "AuthViolation";
                                    MelonLogger.Error($"[AirlockFriends] AUTH FAILED: {message}");
                                    NotificationLib.QueueNotification($"[<color=red>Unauthorized</color>] {message}");
                                    await Disconnect(false, true);
                                    continue;
                                }
                            }

                            if (data.TryGetProperty("EventIntercepted", out var interceptionProp))
                            {
                                string message = data.TryGetProperty("error", out var error) ? error.GetString() : "PostAuthKeyViolation";
                                MelonLogger.Error($"[AirlockFriends] AUTH FAILED: {message}");
                                NotificationLib.QueueNotification($"[<color=red>Unauthorized</color>] {message}");
                                await Disconnect(true);
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
                                        ReceiveDirectMessage(toFriend, "System", $"Operation failed: {reason}", true, false);
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
                                            string friendCode = acceptedBy.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Friend accepted by {friendCode}");
                                            NotificationLib.QueueNotification($"[<color=green>FRIEND ACCEPTED</color>] <color=lime>{friendCode}</color> is now your friend!");
                                            UpdateFriend(friendCode, "Loading..", friendCode, "", "Loading..");


                                        }
                                        break;


                                    case "friendRejected":
                                        if (data.TryGetProperty("byFriendCode", out var rejectedBy))
                                        {
                                            string FriendToReject = rejectedBy.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Friend rejected by {FriendToReject}");
                                            NotificationLib.QueueNotification($"[<color=red>FRIEND REJECTED</color>] <color=lime>{FriendToReject}</color> rejected your friend request!");
                                        }
                                        break;

                                    case "friendRemoved":
                                    case "friendRemove":
                                        if (data.TryGetProperty("byFriendCode", out var removedBy))
                                        {
                                            string FriendPlayer = removedBy.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Friend removed: {FriendPlayer}");
                                            NotificationLib.QueueNotification($"[<color=red>FRIEND REMOVED</color>] <color=lime>{FriendPlayer}</color> is no longer your friend!");
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
                                            ReceiveDirectMessage(sender, GetFriend(sender).Name, Message);
                                        }
                                        break;

                                    case "sendMessage":
                                        if (data.TryGetProperty("targetFriendCode", out var toFriendCode))
                                        {
                                            string target = toFriendCode.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Message sent to {target}!");
                                            NotificationLib.QueueNotification($"[<color=magenta>SENT</color>] Sent message to <color=lime>{target}</color>!");
                                        }
                                        break;

                                    case "statusUpdate":
                                        string sentTo = data.TryGetProperty("sentTo", out var sentProp) ? sentProp.GetInt32().ToString() : "0";
                                        string friendCount = data.TryGetProperty("friendCount", out var fcProp) ? fcProp.GetInt32().ToString() : "0";
                                        string privacy = data.TryGetProperty("privacy", out var privProp) ? privProp.GetString() : "unknown";

                                        MelonLogger.Msg($"[AirlockFriends] StatusUpdate confirmed: {sentTo}/{friendCount} friends, privacy={privacy}");
                                        break;

                                    case "friendStatusUpdate":
                                        {
                                            string friendCode = data.GetProperty("friendCode").GetString();
                                            string status = data.GetProperty("status").GetString();

                                            string name = data.TryGetProperty("name", out var n) ? n.GetString() : "";
                                            string room = data.TryGetProperty("roomID", out var r) ? r.GetString() : "";
                                            string privacyMode = data.GetProperty("privacy").GetString();
                                            UpdateFriend(name, status, friendCode, room, privacyMode);



                                            MelonLogger.Msg($"[STATUS] {friendCode}: {status}, {name}, Room {room}, privacy={privacyMode}");

                                            UpdateFriend(name, status, friendCode, room, privacyMode);
                                            break;
                                        }

                                    case "inviteRequestForward":
                                        {
                                            if (data.TryGetProperty("fromFriendCode", out var Sender))
                                            {
                                                string fromFriend = Sender.GetString();
                                                string roomID = data.TryGetProperty("roomID", out var roomProp) ? roomProp.GetString() : "";
                                                MelonLogger.Msg($"[AirlockFriends] Invite request received from {fromFriend}, room: {roomID}");
                                                NotificationLib.QueueNotification($"[<color=magenta>INVITE REQUEST</color>] From: <color=lime>{fromFriend}</color>, Room: {roomID}");
                                            }
                                            break;
                                        }





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
            MyName = ServerPublicKey;
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
            NewFriendRequest = true;
            friendRequests.Add(fromFriendCode);
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



        public static async Task SendStatusUpdate(string status, string roomCode, string name, string privacy)
        {
            if (!IsConnected || socket == null)
                return;

            try
            {
                var payload = new
                {
                    type = "statusUpdate",
                    fromPrivateKey = PrivateKey,
                    status = status ?? "Unknown",
                    roomID = roomCode ?? "",
                    name = name ?? "",
                    privacy = privacy ?? "Public"
                };

                string json = JsonConvert.SerializeObject(payload);

                await socket.SendAsync(
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
                    WebSocketMessageType.Text,
                    true,
                    _cts.Token
                );
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[AirlockFriends] Failed to send status update: " + ex);
            }
        }



        public static void UpdateFriendStatus(string friendCode, string status, string name, string roomID, string privacy)
        {
            var friend = GetFriend(friendCode);
            if (friend == null)
            {
                MelonLogger.Warning($"[AirlockFriends] Status update for unknown friend {friendCode}");
                return;
            }

            friend.Status = status;
            friend.Name = name;
            friend.RoomCode = roomID;
            friend.Privacy = privacy;

            UpdateFriend(name, status, friendCode, roomID, privacy);

            MelonLogger.Msg($"[AirlockFriends] Updated friend: {friendCode} | {name} | {status} | room={roomID} | privacy={privacy}");
        }






        public static async Task RPC_RequestData()
        {
            if (!IsConnected || socket == null)
                return;

            try
            {
                var payload = new
                {
                    type = "requestFriendStatuses",
                    fromPrivateKey = PrivateKey
                };

                string json = JsonConvert.SerializeObject(payload);

                await socket.SendAsync(
                    new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
                    WebSocketMessageType.Text,
                    true,
                    _cts.Token
                );

                MelonLogger.Msg("[AirlockFriends] Sent friend status request for all friends.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[AirlockFriends] Failed to send RPC_RequestData: " + ex);
            }
        }











        public static async Task RPC_RequestInviteInfo(string targetFriendCode)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(PrivateKey))
                return;

            string roomID = UnityEngine.Object.FindObjectOfType<AirlockNetworkRunner>().SessionInfo.Name ?? "NoRoom";

            var payload = new
            {
                type = "inviteRequest",
                fromPrivateKey = PrivateKey,
                fromFriendCode = FriendCode,
                targetFriendCode = targetFriendCode,
                roomID = roomID
            };

            string json = System.Text.Json.JsonSerializer.Serialize(payload);
            await RaiseEvent(json);
        }









        public static async Task RPC_UpdateSettings(bool? allowFriendRequests = null, string joinPrivacy = null, bool? allowMessages = null, bool? allowInvites = null)
        {
            if (!IsConnected || socket == null) return;

            if (allowFriendRequests.HasValue) AllowFriendRequests = allowFriendRequests.Value;
            if (!string.IsNullOrEmpty(joinPrivacy)) JoinPrivacy = joinPrivacy;
            if (allowMessages.HasValue) AllowMessages = allowMessages.Value;
            if (allowInvites.HasValue) AllowInvites = allowInvites.Value;

            var payload = new
            {
                type = "updateSettings",
                fromPrivateKey = PrivateKey,
                AllowFriendRequests = AllowFriendRequests,
                JoinPrivacy = JoinPrivacy,
                AllowMessages = AllowMessages,
                AllowInvites = AllowInvites
            };

            try
            {
                string json = System.Text.Json.JsonSerializer.Serialize(payload);
                await RaiseEvent(json);
                MelonLogger.Msg($"[AirlockFriends debug] Sent updated settings to server: FriendRequests {AllowFriendRequests}, JoinPrivacy {JoinPrivacy}, Messages {AllowMessages}, Invites {AllowInvites}");
            }
            catch (Exception ex)
            {
                MelonLogger.Error($"[AirlockFriends] Failed to send updated settings: {ex}");
            }
        }










        public static async Task Disconnect(bool force = false, bool rejected = false)
        {
            if (!IsConnected) return;
            try
            {
                if (rejected)
                    AirlockFriendsAuth.connectionStatus = AirlockFriendsAuth.ConnectionStatus.Rejected;
                else if (force)
                    AirlockFriendsAuth.connectionStatus = AirlockFriendsAuth.ConnectionStatus.ForciblyClosed;
                else
                    AirlockFriendsAuth.connectionStatus = AirlockFriendsAuth.ConnectionStatus.Disconnected;

                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", _cts.Token);
                MelonLogger.Msg("[AirlockFriends] Disconnected from WS server.");
                NotificationLib.QueueNotification("[<color=magenta>Disconnect</color>] Disconnected from WS server.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[AirlockFriends] Disconnect failed: " + ex.Message);
            }
        }




    }
}
