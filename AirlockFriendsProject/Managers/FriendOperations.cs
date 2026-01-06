using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AirlockFriends.Config;
using AirlockFriends.UI;
using Il2CppSG.Airlock.Network;
using Il2CppSG.Airlock.XR;
using MelonLoader;
using UnityEngine;
using static AirlockFriends.Managers.AirlockFriendsAuth;
using static AirlockFriends.UI.FriendGUI;

namespace AirlockFriends.Managers
{
    public class AirlockFriendsOperations
    {
        protected static string PrivateKey { get; private set; }
        public static string FriendCode { get; private set; }
        public static string MyName { get; private set; } = FriendCode;
        public static bool AllowFriendRequests = true;
        public static string JoinPrivacy = "Joinable";
        public static string ModTags = "Everyone";
        public static bool AllowMessages = true;
        public static bool AllowInvites = true;
        public static bool FriendslistFetched = false;
        public static ClientWebSocket socket;
        private static CancellationTokenSource cts;
        public static readonly string[] UnwantedNames = {
            "Red", "Blue", "Green", "Pink", "Orange",
            "Yellow", "Black", "White", "Purple", "Brown",
            "Cyan", "Lime", "Spectator", "Color###"
        };
        private static Dictionary<string, string> GetNameFromID = new Dictionary<string, string>();
        public static HashSet<string> GettingNames = new();
        public static event Action<string, string> OnUsernameUpdated;

        public static bool IsConnected => socket != null && socket.State == WebSocketState.Open;

        public static bool IsUnwantedName(string name)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                    return false;

                var checks = new List<string> { name };
                checks.Add(new string(name.Where(char.IsLetter).ToArray()));

                foreach (string check in checks)
                {
                    foreach (string block in UnwantedNames)
                    {
                        var b = block.ToLowerInvariant();
                        if (check.ToLowerInvariant().Contains(b))
                            return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                Logging.Error($"Failed to check name: {ex}");
                return false;
            }
        }


        public static async void PrepareAuthentication()
        {
            if (!IsConnected)
            {
                PrivateKey = AirlockFriendsAuth.PrepareAuthenticationKey();
                Logging.DebugLog($"Authenticating with private token: {PrivateKey}");

                await ConnectToServer();
                await AuthenticateWithServer();
            }
        }

        private static async Task ConnectToServer()
        {
            if (!IsConnected)
            {
                connectionStatus = ConnectionStatus.Connecting;
                socket = new ClientWebSocket();
                cts = new CancellationTokenSource();
                socket.Options.SetRequestHeader("User-Agent", $"AirlockFriends/{Settings.Version}");

                try
                {
                    var serverUri = new Uri("wss://airlockfriends.xyz");
                    await socket.ConnectAsync(serverUri, cts.Token);
                    Logging.Msg("Connected to friends websocket server");
                    _ = Task.Run(ReceiveLoop);
                }
                catch (Exception ex)
                {
                    Logging.Error($"Connection failed: {ex.Message}");
                    MelonCoroutines.Start(AttemptReconnection());
                }
            }
        }

        private static async Task AuthenticateWithServer()
        {
            if (IsConnected)
            {
                connectionStatus = ConnectionStatus.Authenticating;
                var authPayload = new
                {
                    type = "authenticate",
                    privateKey = PrivateKey,
                    steamID = Helpers.GetSelfSteamID(),
                };

                string EventData = System.Text.Json.JsonSerializer.Serialize(authPayload);
                await RaiseEvent(EventData);
                Logging.Msg("Authenticating with server!");
            }
        }


        private static async Task ReceiveLoop()
        {
            var buffer = new byte[8192];

            while (IsConnected)
            {
                try
                {
                    WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        if (!msg.Contains("friendsList")) Logging.DebugLog($"[DEBUG] Server sent reliable data: {msg}");
                        if (msg.Contains("This account"))
                            Main.AFBanned = true;

                        try
                        {
                            JsonElement data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(msg);

                            if (data.TryGetProperty("authenticated", out var Auth))
                            {
                                if (Auth.ValueKind == System.Text.Json.JsonValueKind.True)
                                {
                                    string publicFriendCode = data.GetProperty("friendCode").GetString();
                                    AssignFriendCode(publicFriendCode);

                                    if (data.TryGetProperty("privateKey", out var PrivateToken))
                                    {
                                        string newPrivateKey = PrivateToken.GetString();
                                        AirlockFriendsAuth.SavePrivateKey(newPrivateKey);
                                        Logging.Msg($"Authenticated!");
                                    }

                                    string message = data.TryGetProperty("message", out var Message) ? Message.GetString() : "Authenticated successfully!";

                                    if (data.TryGetProperty("AllowFriendRequests", out var AllowFriendReq))
                                        AllowFriendRequests = AllowFriendReq.GetBoolean();

                                    if (data.TryGetProperty("JoinPrivacy", out var JoinRequests))
                                        JoinPrivacy = JoinRequests.GetString();

                                    if (data.TryGetProperty("AllowMessages", out var AllowMessaging))
                                        AllowMessages = AllowMessaging.GetBoolean();

                                    if (data.TryGetProperty("AllowInvites", out var AllowInvite))
                                        AllowInvites = AllowInvite.GetBoolean();

                                    if (data.TryGetProperty("ColorMode", out var ColorMode))
                                    {
                                        string mode = ColorMode.GetString();
                                        Settings.GUIColor = mode == "light" ? Color.cyan : Color.blue;
                                    }

                                    if (data.TryGetProperty("Name", out var Name))
                                    {
                                        MyName = Name.GetString();
                                        if (MyName == FriendCode)
                                        {
                                            NewName = true;
                                        }
                                    }

                                    if (data.TryGetProperty("friendsList", out var FriendsList) && FriendsList.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        foreach (JsonElement friend in FriendsList.EnumerateArray())
                                        {
                                            string friendCode = friend.GetProperty("friendCode").GetString();
                                            MelonCoroutines.Start(GetUsername(friendCode, name =>
                                            {
                                                Logging.DebugLog($"Friend loaded: {name} ({friendCode})");
                                            }));
                                        }
                                    }

                                    if (data.TryGetProperty("incomingRequests", out var Incoming) && Incoming.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        int RequestCount = 0;
                                        foreach (JsonElement request in Incoming.EnumerateArray())
                                        {
                                            string friendCode = request.GetString();
                                            OnRequestReceived(friendCode);
                                            RequestCount++;
                                        }
                                        if (RequestCount > 0)
                                            NotificationLib.QueueNotification($"<color=magenta>REQUESTS</color> you have <color=lime>{RequestCount}</color> friend request(s)");
                                    }

                                    if (data.TryGetProperty("blocked", out var BlockedList) && BlockedList.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        BlockedUsers.Clear();
                                        foreach (var blockedFriendCode in BlockedList.EnumerateArray())
                                        {
                                            string targetFriendCode = blockedFriendCode.GetString();
                                            if (!string.IsNullOrEmpty(targetFriendCode))
                                            {
                                                MelonCoroutines.Start(GetUsername(targetFriendCode, name =>
                                                {
                                                    BlockedUsers[targetFriendCode] = name;
                                                }));
                                            }
                                        }
                                    }

                                    if (Main.AFBanned)
                                    {
                                        Main.AFBanned = false;
                                        Main.HasShownBanNoti = false;
                                        NotificationLib.QueueNotification("[<color=lime>UNBANNED</color>] You've been unbanned from Airlock Friends!");
                                    }

                                    connectionStatus = ConnectionStatus.Established;
                                    _ = RPC_NotifyFriendGroup();

                                    if (message.Contains("New user"))
                                        NotificationLib.QueueNotification("[<color=magenta>WELCOME</color>] Welcome to Airlock Friends!");
                                    continue;
                                }

                                if (Auth.ValueKind == System.Text.Json.JsonValueKind.False && data.TryGetProperty("action", out var action) && action.GetString() == "AuthViolation")
                                {
                                    string message = data.TryGetProperty("error", out var error) ? error.GetString() : "AuthViolation";
                                    Logging.Error($"AUTH FAILED: {message}");
                                    NotificationLib.QueueNotification($"[<color=red>Unauthorized</color>] {message}");

                                    if (message.ToLower().Contains("invalid") || message.ToLower().Contains("revoked"))
                                        File.Delete(Path.Combine(Application.persistentDataPath, "AirlockFriendsPrivateKey.txt"));

                                    await Disconnect(rejected: true);
                                    continue;
                                }

                            }

                            if (data.TryGetProperty("type", out var Event))
                            {
                                string type = Event.GetString();

                                switch (type)
                                {
                                    case "friendRequestReceived":
                                        if (data.TryGetProperty("fromFriendCode", out var Requesting))
                                        {
                                            string Requester = Requesting.GetString();
                                            OnRequestReceived(Requester);
                                            MelonCoroutines.Start(GetUsername(Requester, name => NotificationLib.QueueNotification($"[<color=magenta>FRIEND REQUEST</color>] <color=lime>{name}</color> wants to friend you!")));
                                        }
                                        break;

                                    case "friendRequest":
                                        if (data.TryGetProperty("status", out var FriendReqStatus) && FriendReqStatus.GetString() == "failed")
                                        {
                                            if (data.TryGetProperty("reason", out var FriendFailReason))
                                                NotificationLib.QueueNotification($"[<color=red>ERROR</color>] {FriendFailReason.GetString()}");
                                            else
                                                NotificationLib.QueueNotification("[<color=red>ERROR</color>] Friend request failed");
                                            break;
                                        }

                                        if (data.TryGetProperty("toFriendCode", out var SentTo))
                                        {
                                            string toPlayer = SentTo.GetString();
                                            MelonCoroutines.Start(GetUsername(toPlayer, name =>
                                            {
                                                Logging.DebugLog($"Friend request sent to {name}");
                                                NotificationLib.QueueNotification($"[<color=magenta>FRIEND REQUEST</color>] Friend Request Sent to <color=lime>{name}</color>!");
                                            }));
                                        }
                                        break;



                                    case "friendAccepted":
                                        if (data.TryGetProperty("byFriendCode", out var Sender))
                                        {
                                            string AcceptingFriend = Sender.GetString();
                                            MelonCoroutines.Start(GetUsername(AcceptingFriend, friendName =>
                                            {
                                                UpdateFriend(friendName, "Loading..", AcceptingFriend, "");
                                                _ = RPC_NotifyFriendGroup(updateSelf: false);
                                                Logging.DebugLog($"Friend accepted: {friendName} ({AcceptingFriend})");
                                                NotificationLib.QueueNotification($"[<color=lime>FRIEND ACCEPTED</color>] <color=lime>{friendName} ({AcceptingFriend})</color> is now your friend!");
                                            }));
                                        }
                                        break;


                                    case "friendReject":
                                    case "friendRejected":
                                        if (data.TryGetProperty("byFriendCode", out var RejectedBy))
                                        {
                                            string FriendToReject = RejectedBy.GetString();
                                            Logging.Msg($"Friend request rejected by {FriendToReject}");
                                            MelonCoroutines.Start(GetUsername(FriendToReject, name => NotificationLib.QueueNotification($"[<color=red>FRIEND REJECTED</color>] <color=lime>{name}</color> rejected your friend request!")));
                                        }
                                        break;

                                    case "friendRemoved":
                                    case "friendRemove":
                                        if (data.TryGetProperty("byFriendCode", out var RemovedBy))
                                        {
                                            string FriendPlayer = RemovedBy.GetString();
                                            MelonCoroutines.Start(GetUsername(FriendPlayer, name => NotificationLib.QueueNotification($"[<color=red>FRIEND REMOVED</color>] <color=lime>{name}</color> is no longer your friend!")));
                                            var f = GetFriend(FriendPlayer);
                                            if (f != null)
                                                friends.Remove(f);
                                        }
                                        break;

                                    case "messageReceived":
                                        {
                                            if (data.TryGetProperty("fromFriendCode", out var Messager) &&
                                                data.TryGetProperty("message", out var MessageText))
                                            {
                                                string MessageSender = Messager.GetString();
                                                string message = MessageText.GetString();
                                                MelonCoroutines.Start(GetUsername(MessageSender, name =>
                                                {
                                                    ReceiveDirectMessage(MessageSender, name, message);
                                                    NotificationLib.QueueNotification($"[<color=magenta>MESSAGE</color>] Message from: <color=lime>{name}</color>: {message}");
                                                }));
                                            }
                                            break;
                                        }


                                    case "sendMessage":
                                        {
                                            if (data.TryGetProperty("status", out var messageStatus) && data.TryGetProperty("toFriendCode", out var SendingTo))
                                            {
                                                string MessageStatus = messageStatus.GetString();

                                                if (MessageStatus == "success")
                                                {
                                                    string target = data.TryGetProperty("toFriendCode", out var toFriendCode) ? toFriendCode.GetString() : "Unknown";
                                                    NotificationLib.QueueNotification($"[<color=lime>SENT</color>] Sent message to {GetFriend(toFriendCode.GetString()).Name}!");
                                                }
                                                else
                                                {
                                                    string reason = data.TryGetProperty("reason", out var MessageFailReason) ? MessageFailReason.GetString() : "Failed to send message";
                                                    NotificationLib.QueueNotification($"[<color=red>ERROR</color>] {reason}");
                                                    ReceiveDirectMessage(SendingTo.GetString(), "System", reason, true, true);
                                                }
                                            }
                                            break;
                                        }





                                    case "friendsList":
                                        if (data.TryGetProperty("friends", out var Friend) && Friend.ValueKind == System.Text.Json.JsonValueKind.Array)
                                        {
                                            if (data.TryGetProperty("myName", out var myName))
                                            {
                                                string updatedName = myName.GetString();
                                                if (!string.IsNullOrEmpty(updatedName))
                                                    MyName = updatedName;
                                            }

                                            foreach (var friend in Friend.EnumerateArray())
                                            {
                                                string code = friend.GetProperty("friendCode").GetString();
                                                string LatestName = friend.GetProperty("name").GetString();
                                                string online = friend.GetProperty("status").GetString();

                                                UpdateFriend(LatestName, online, code, "");
                                            }
                                            if (!FriendslistFetched)
                                            {
                                                int onlineFriends = 0;
                                                foreach (FriendInfo friend in friends)
                                                {
                                                    if (friend.IsOnline)
                                                    {
                                                        onlineFriends++;
                                                    }
                                                }
                                                MelonCoroutines.Start(GetUsername(FriendCode, name => NotificationLib.QueueNotification($"[<color=magenta>SERVER</color>] Welcome back, <color=lime>{name}</color>! You have <color=lime>{onlineFriends}</color> friend(s) online!")));
                                                FriendslistFetched = true;
                                            }
                                        }
                                        break;



                                    case "joinRequestReceived":
                                        if (data.TryGetProperty("fromFriendCode", out var FromReceiving))
                                        {
                                            string senderFriendCode = FromReceiving.GetString();
                                            ReceiveJoinRequest(senderFriendCode);
                                        }
                                        break;


                                    case "joinRequestResponse":
                                        if (data.TryGetProperty("targetFriendCode", out var FromReceivingJoin) &&
                                            data.TryGetProperty("accepted", out var WasAccepted))
                                        {
                                            string JoiningFriend = FromReceivingJoin.GetString();
                                            bool accepted = WasAccepted.GetBoolean();
                                            string roomCode = data.TryGetProperty("roomID", out var InRoomID) ? InRoomID.GetString() : "";
                                            bool inGame = data.TryGetProperty("inGame", out var FriendInGame) && FriendInGame.GetBoolean();

                                            if (accepted)
                                            {
                                                Logging.DebugLog($"{GetFriend(JoiningFriend).Name} accepted your join request. Room: {roomCode}");
                                                NotificationLib.QueueNotification($"[<color=lime>JOIN ACCEPTED</color>] <color=lime>{GetFriend(JoiningFriend).Name}</color> is in room {roomCode}!");

                                                AcceptedJoinRequests.Add(new AcceptedJoinRequestData
                                                {
                                                    FriendCode = JoiningFriend,
                                                    TimeAccepted = Time.time,
                                                    RoomID = roomCode
                                                });
                                            }
                                            else
                                            {
                                                if (!inGame)
                                                {
                                                    Logging.DebugLog($"{GetFriend(JoiningFriend).Name} is not in a game.");
                                                    NotificationLib.QueueNotification($"[<color=red>JOIN FAILED</color>] <color=lime>{GetFriend(JoiningFriend).Name}</color> is not in a game.");
                                                }
                                                else
                                                {
                                                    Logging.DebugLog($"{GetFriend(JoiningFriend).Name} rejected your join request.");
                                                    NotificationLib.QueueNotification($"[<color=red>JOIN REJECTED</color>] <color=lime>{GetFriend(JoiningFriend).Name}</color> did not accept your join request.");
                                                }
                                            }
                                        }
                                        break;




                                    case "inviteResponseResult":
                                        {
                                            if (!data.TryGetProperty("fromFriendCode", out var SenderFriend) || !data.TryGetProperty("accepted", out var InviteAccepted))
                                                break;

                                            string FriendID = SenderFriend.GetString();
                                            bool accepted = InviteAccepted.GetBoolean();

                                            var friend = FriendGUI.GetFriend(FriendID);
                                            string FriendName = friend != null ? friend.Name : FriendID;

                                            if (accepted)
                                            {
                                                Logging.DebugLog($"{FriendName} accepted your invite!");
                                                NotificationLib.QueueNotification($"[<color=lime>JOIN ACCEPTED</color>] <color=lime>{FriendName}</color> accepted your invite!");
                                            }
                                            else
                                            {
                                                Logging.DebugLog($"{FriendName} denied your invite!");
                                                NotificationLib.QueueNotification($"[<color=red>Denied</color>] <color=lime>{FriendName}</color> did not accept your invite!");
                                            }
                                            break;
                                        }





                                    case "inviteReceived":
                                        if (data.TryGetProperty("fromFriendCode", out var fromWho) && data.TryGetProperty("roomID", out var RoomID))
                                        {
                                            string FriendID = fromWho.GetString();
                                            string roomCode = RoomID.GetString();

                                            ReceiveInvite(FriendID, roomCode);
                                        }
                                        break;



                                    case "GetPlayerName":
                                        string friendCode = data.TryGetProperty("friendCode", out var friendcode) ? friendcode.GetString() : "UnknownCode";
                                        string name = data.TryGetProperty("name", out var PlayerName) && PlayerName.ValueKind != System.Text.Json.JsonValueKind.Null ? PlayerName.GetString() : "UnknownName";
                                        SaveUsername(friendCode, name);
                                        break;


                                    case "BlockUserFail":
                                        if (data.TryGetProperty("reason", out var blockFailReason))
                                        {
                                            string reason = blockFailReason.GetString();
                                            NotificationLib.QueueNotification($"[<color=red>ERROR</color>] Failed to block user: {reason}");
                                        }
                                        break;

                                    case "UnblockUserFail":
                                        if (data.TryGetProperty("reason", out var UnblockFailReason))
                                        {
                                            string reason = UnblockFailReason.GetString();
                                            NotificationLib.QueueNotification($"[<color=red>ERROR</color>] Failed to unblock user: {reason}");
                                        }
                                        break;

                                    case "updateSettingsResponce":
                                        if (data.TryGetProperty("reason", out var SettingsResponce) && data.TryGetProperty("status", out var UpdatedStatus))
                                        {
                                            string reason = SettingsResponce.GetString();
                                            string status = UpdatedStatus.GetString();

                                            if (status == "failed")
                                                NotificationLib.QueueNotification($"[<color=red>ERROR</color>] Failed to update settings! {reason}");
                                            else
                                                NotificationLib.QueueNotification("[<color=lime>SUCCESS</color>] Your settings have been updated.");
                                        }
                                        break;

                                    default:
                                        Logging.Warning($"Unhandled event / reliable data raised?! Please report this. {msg}");
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Logging.Error($"Failed to deserialize reliable data: {ex.Message}");
                            NotificationLib.QueueNotification($"[<color=red>ERROR</color>] Failed to deserialize reliable.\nPlease report this to us.\n: Check your console for info");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.Error($"Server sent an exception: {ex.Message}");
                    if (!IsConnected)
                        MelonCoroutines.Start(AttemptReconnection());
                    break;
                }
            }
            await Disconnect();
        }


        // Send reliable event to server
        public static async Task RaiseEvent(string OperationInfo)
        {
            if (IsConnected)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(OperationInfo);
                await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cts.Token);
            }
        }

        public static void AssignFriendCode(string ServerPublicKey)
        {
            FriendCode = ServerPublicKey;
            if (string.IsNullOrEmpty(MyName)) MyName = ServerPublicKey;
            Logging.DebugLog($"Server assigned FriendCode: {FriendCode}");
        }

        public static async Task RPC_FriendshipRequest(string targetFriendCode)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode)) return;

            if (FriendRequests.Contains(targetFriendCode))
            {
                await RPC_FriendAccept(targetFriendCode);
                FriendRequests.Remove(targetFriendCode);
                return;
            }

            var request = new
            {
                type = "friendRequest",
                fromPrivateKey = PrivateKey,
                toFriendCode = targetFriendCode
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(EventData);

            MelonCoroutines.Start(GetUsername(targetFriendCode, name => Logging.DebugLog($"Sent friend request to {name}")));
        }

        public static void OnRequestReceived(string fromFriendCode)
        {
            MelonCoroutines.Start(GetUsername(fromFriendCode, name => { Logging.DebugLog($"Friend request received from {name}"); }));
            NewFriendRequest = true;
            FriendRequests.Add(fromFriendCode);
        }

        public static async Task RPC_SendMessage(string targetFriendCode, string message)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(message) || string.IsNullOrEmpty(FriendCode))
                return;

            var request = new
            {
                type = "sendMessage",
                targetFriendCode = targetFriendCode,
                fromPrivateKey = PrivateKey,
                fromFriendCode = FriendCode,
                message = message
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(EventData);
            Logging.DebugLog($"Sent message to {targetFriendCode}: {message}");
        }




        public static async Task RPC_FriendAccept(string AcceptingFriend)
        {
            if (string.IsNullOrEmpty(AcceptingFriend) || string.IsNullOrEmpty(FriendCode)) return;

            var request = new
            {
                type = "friendAccept",
                fromPrivateKey = PrivateKey,
                acceptFriendCode = AcceptingFriend
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(EventData);
            Logging.DebugLog($"Accepted friend request from {AcceptingFriend}");

            FriendRequests.Remove(AcceptingFriend);
        }
        public static async Task RPC_FriendReject(string incomingFriendCode)
        {
            if (string.IsNullOrEmpty(incomingFriendCode) || string.IsNullOrEmpty(FriendCode))
                return;

            var request = new
            {
                type = "friendReject",
                fromPrivateKey = PrivateKey,
                rejectFriendCode = incomingFriendCode
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(EventData);
            FriendRequests.Remove(incomingFriendCode);
            Logging.DebugLog($"Rejected friend request from {incomingFriendCode}");
        }


        public static async Task RPC_FriendRemove(string RemovingFriend)
        {
            if (string.IsNullOrEmpty(RemovingFriend) || string.IsNullOrEmpty(FriendCode)) return;

            var request = new
            {
                type = "friendRemove",
                fromPrivateKey = PrivateKey,
                removeFriendCode = RemovingFriend
            };


            string EventData = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(EventData);
            Logging.DebugLog($"Removed friend {RemovingFriend}");
            MelonCoroutines.Start(GetUsername(RemovingFriend, name => NotificationLib.QueueNotification($"[<color=red>UNFRIEND</color>] Unfriended <color=lime>{name}</color>")));
        }
























        public static async Task RPC_UpdateSettings(bool? allowFriendRequests = null, string joinPrivacy = null, bool? allowMessages = null, bool? allowInvites = null)
        {
            if (!IsConnected || socket == null) return;

            if (allowFriendRequests.HasValue) AllowFriendRequests = allowFriendRequests.Value;
            if (!string.IsNullOrEmpty(joinPrivacy)) JoinPrivacy = joinPrivacy;
            if (allowMessages.HasValue) AllowMessages = allowMessages.Value;
            if (allowInvites.HasValue) AllowInvites = allowInvites.Value;

            var request = new
            {
                type = "updateSettings",
                fromPrivateKey = PrivateKey,
                AllowFriendRequests = AllowFriendRequests,
                JoinPrivacy = JoinPrivacy,
                AllowMessages = AllowMessages,
                AllowInvites = AllowInvites,
                ColorMode = Settings.GUIColor == Color.cyan ? "light" : "dark"
            };

            try
            {
                string EventData = System.Text.Json.JsonSerializer.Serialize(request);
                await RaiseEvent(EventData);
                Logging.DebugLog($"Sent updated settings to server: FriendRequests {AllowFriendRequests}, JoinPrivacy {JoinPrivacy}, Messages {AllowMessages}, Invites {AllowInvites}");
            }
            catch (Exception ex)
            {
                Logging.Error($"Failed to send updated settings: {ex}");
            }
        }






        public static async Task RPC_NotifyFriendGroup(string name = "", bool updateSelf = true, string ModName = "", string customName = "")
        {
            if (!IsConnected)
                return;

            if (!string.IsNullOrEmpty(customName))
            {
                MyName = customName;
            }
            else if (updateSelf)
            {
                if (!string.IsNullOrEmpty(name) && IsUnwantedName(name) && MyName == FriendCode)
                    name = ModName;

                if (string.IsNullOrEmpty(name) && MyName == FriendCode)
                {
                    var rig = UnityEngine.Object.FindObjectOfType<XRRig>();
                    if (rig != null && !rig.PState.NetworkName.Value.Contains("#") && !rig.PState.IsSpectating)
                        MyName = rig.PState.NetworkName.Value ?? "";
                    else
                        MyName = "";
                }
                else if (MyName == FriendCode)
                {
                    MyName = name;
                }
            }

            var request = new
            {
                type = "getFriends",
                fromPrivateKey = PrivateKey,
                currentName = MyName
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(EventData);
        }



        private static float JoinRequestTime = -999f;
        public static async Task RPC_RequestJoin(string targetFriendCode)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode)) return;
            if (Time.realtimeSinceStartup - JoinRequestTime < 1f)
                return;
            JoinRequestTime = Time.realtimeSinceStartup;

            var request = new
            {
                type = "joinRequest",
                fromPrivateKey = PrivateKey,
                targetFriendCode = targetFriendCode
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(EventData);
            NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] Sent join request to <color=lime>{GetFriend(targetFriendCode).Name}</color>");
        }


        private static float InviteTime = -999f;
        public static async Task RPC_SendInvite(string targetFriendCode, string roomID)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode)) return;

            if (Time.realtimeSinceStartup - InviteTime < 1f)
                return;
            InviteTime = Time.realtimeSinceStartup;

            var request = new
            {
                type = "sendInvite",
                fromPrivateKey = PrivateKey,
                targetFriendCode = targetFriendCode,
                roomID = roomID
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(EventData);
            NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] Sent join request to <color=lime>{GetFriend(targetFriendCode).Name}</color>");
        }



        public static async Task RPC_RespondToInvite(string targetFriendCode, bool accepted)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode)) return;

            var request = new
            {
                type = "inviteResponse",
                fromPrivateKey = PrivateKey,
                targetFriendCode = targetFriendCode,
                accepted = accepted
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(EventData);
        }

        public static async Task RPC_RespondToJoin(string senderFriendCode, bool accepted, bool InGame = true)
        {
            string roomCode = "";

            if (accepted)
            {
                var runner = UnityEngine.Object.FindObjectOfType<AirlockNetworkRunner>();
                if (runner != null)
                {
                    roomCode = runner.SessionInfo.Name;
                    MelonCoroutines.Start(GetUsername(senderFriendCode, name => NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] Accepted {name}'s join request")));
                }
            }
            else if (InGame)
                MelonCoroutines.Start(GetUsername(senderFriendCode, name => NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] Rejected {name}'s join request")));

            var request = new
            {
                type = "joinRequestResponse",
                fromPrivateKey = PrivateKey,
                targetFriendCode = senderFriendCode,
                accepted = accepted,
                roomID = roomCode,
                inGame = InGame
            };

            JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            string EventData = System.Text.Json.JsonSerializer.Serialize(request, options);
            await RaiseEvent(EventData);

            Logging.DebugLog($"Responded to join request from {senderFriendCode}: {(accepted ? $"accepted room {roomCode}" : "rejected")}");
        }


        public static async Task RPC_GetUsername(string friendCode)
        {
            if (!string.IsNullOrEmpty(friendCode) && IsConnected)
            {
                var request = new
                {
                    type = "getUserName",
                    fromPrivateKey = PrivateKey,
                    friendCode = friendCode
                };

                string EventData = System.Text.Json.JsonSerializer.Serialize(request);
                await RaiseEvent(EventData);
            }
        }


        public static async Task RPC_BlockUser(string targetFriendCode)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode))
                return;

            var request = new
            {
                type = "blockUser",
                fromPrivateKey = PrivateKey,
                targetFriendCode = targetFriendCode
            };

            string data = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(data);
            MelonCoroutines.Start(GetUsername(targetFriendCode, name => NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] You've blocked <color=lime>{name}</color>")));
        }

        public static async Task RPC_UnblockUser(string targetFriendCode)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode))
                return;

            var request = new
            {
                type = "unblockUser",
                fromPrivateKey = PrivateKey,
                targetFriendCode = targetFriendCode
            };

            string data = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(data);
            MelonCoroutines.Start(GetUsername(targetFriendCode, name => NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] You've unblocked <color=lime>{name}</color>")));
        }


        public static IEnumerator GetUsername(string friendCode, System.Action<string> Callback)
        {
            if (GetNameFromID.TryGetValue(friendCode, out var name) && !string.IsNullOrEmpty(name))
            {
                Callback?.Invoke(name);
                yield break;
            }

            if (!GettingNames.Contains(friendCode))
            {
                GettingNames.Add(friendCode);
                _ = RPC_GetUsername(friendCode);
            }

            float elapsedTime = 0f;
            while ((!GetNameFromID.TryGetValue(friendCode, out name) || string.IsNullOrEmpty(name)) && elapsedTime < 5f)
            {
                elapsedTime += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            if (string.IsNullOrEmpty(name))
                name = friendCode;

            Callback?.Invoke(name);
        }




        public static void SaveUsername(string friendCode, string name)
        {
            if (!string.IsNullOrEmpty(friendCode))
            {
                GetNameFromID[friendCode] = name;
                GettingNames.Remove(friendCode);
                OnUsernameUpdated?.Invoke(friendCode, name);
            }
        }


        public static async Task Disconnect(bool force = false, bool rejected = false)
        {
            if (!IsConnected) return;
            try
            {
                if (rejected)
                    connectionStatus = ConnectionStatus.Rejected;
                else if (force)
                    connectionStatus = ConnectionStatus.ForciblyClosed;
                else
                    connectionStatus = ConnectionStatus.Disconnected;

                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", cts.Token);

                Logging.Msg("Disconnected from friends server.");
                NotificationLib.QueueNotification("[<color=magenta>Disconnect</color>] Disconnected from friends websocket Server.");
            }
            catch (Exception ex)
            {
                Logging.Error($"Disconnect failed: {ex.Message}");
            }
        }
    }
}
