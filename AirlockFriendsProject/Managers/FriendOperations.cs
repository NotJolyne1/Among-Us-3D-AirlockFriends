using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using AirlockFriends.Config;
using AirlockFriends.UI;
using Il2CppFusion;
using Il2CppSG.Airlock;
using Il2CppSG.Airlock.Graphics;
using Il2CppSG.Airlock.Network;
using Il2CppSG.Airlock.Roles;
using Il2CppSG.Airlock.Sabotage;
using Il2CppSG.Airlock.UI.TitleScreen;
using Il2CppSG.Airlock.XR;
using Il2CppSteamworks;
using MelonLoader;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
        public static bool AllowMessages = true;
        public static bool AllowInvites = true;

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

                foreach (var check in checks)
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
                MelonLogger.Msg($"[AirlockFriends] Failed to check name: {ex}");
                return false;
            }
        }



        public static async void PrepareAuthentication()
        {
            if (!IsConnected)
            {
                PrivateKey = AirlockFriendsAuth.PrepareAuthenticationKey();
                MelonLogger.Msg($"[AirlockFriends] [DEBUG] Using PrivateKey: {PrivateKey}");

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
                    var serverUri = new Uri("wss://lank-lucretia-timocratical.ngrok-free.dev");
                    await socket.ConnectAsync(serverUri, cts.Token);
                    MelonLogger.Msg("[AirlockFriends] Connected to server!");
                    _ = Task.Run(ReceiveLoop);
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[AirlockFriends] Connection failed: {ex.Message}");
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

                string json = System.Text.Json.JsonSerializer.Serialize(authPayload);
                await RaiseEvent(json);
                MelonLogger.Msg("[AirlockFriends] Sent authentication request to server.");
            }
        }


        private static async Task ReceiveLoop()
        {
            var buffer = new byte[8192];

            while (IsConnected)
            {
                try
                {
                    var result = await socket.ReceiveAsync(buffer, cts.Token);

                    if (result.MessageType == WebSocketMessageType.Close)
                        break;

                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);
                        if (!msg.Contains("friendsList")) MelonLogger.Msg($"[AirlockFriends] [DEBUG] Server raw: {msg}");
                        if (msg.Contains("This account"))
                            Main.AFBanned = true;

                        try
                        {
                            var data = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(msg);

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
                                        MelonLogger.Msg($"[AirlockFriends] [DEBUG] Saved new privateKey from server: {newPrivateKey}");
                                    }

                                    string message = data.TryGetProperty("message", out var m) ? m.GetString() : "Authenticated successfully!";

                                    if (data.TryGetProperty("AllowFriendRequests", out var AllowFriendReq))
                                        AllowFriendRequests = AllowFriendReq.GetBoolean();

                                    if (data.TryGetProperty("JoinPrivacy", out var JoinRequests))
                                        JoinPrivacy = JoinRequests.GetString();

                                    if (data.TryGetProperty("AllowMessages", out var AllowMessaging))
                                        AllowMessages = AllowMessaging.GetBoolean();

                                    if (data.TryGetProperty("AllowInvites", out var AllowInvite))
                                        AllowInvites = AllowInvite.GetBoolean();

                                    if (data.TryGetProperty("Name", out var Name))
                                    {
                                        MyName = Name.GetString();
                                        if (MyName == FriendCode)
                                        {
                                            FriendGUI.NewName = true;
                                        }
                                    }

                                    if (Main.AFBanned)
                                    {
                                        Main.AFBanned = false;
                                        Main.HasShownBanMessage = false;
                                        Main.HasShownBanNoti = false;
                                        NotificationLib.QueueNotification("[<color=lime>UNBANNED</color>] You've been unbanned from Airlock Friends!");
                                    }

                                    connectionStatus = ConnectionStatus.Established;
                                    _ = RPC_NotifyFriendGroup(updateSelf: false);
                                    if (message.Contains("Welcome back"))
                                        MelonCoroutines.Start(GetUsername(publicFriendCode, name => NotificationLib.QueueNotification($"[<color=magenta>AUTH</color>] Welcome back, <color=lime>{name}</color>!")));
                                    continue;
                                }

                                if (Auth.ValueKind == System.Text.Json.JsonValueKind.False &&
                                    data.TryGetProperty("action", out var actionProp) &&
                                    actionProp.GetString() == "AuthViolation")
                                {
                                    string message = data.TryGetProperty("error", out var error) ? error.GetString() : "AuthViolation";
                                    MelonLogger.Error($"[AirlockFriends] AUTH FAILED: {message}");
                                    NotificationLib.QueueNotification($"[<color=red>Unauthorized</color>] {message}");

                                    if (message.ToLower().Contains("invalid") || message.ToLower().Contains("revoked"))
                                        File.Delete(Path.Combine(Application.persistentDataPath, "AirlockFriendsPrivateKey.txt"));

                                    await Disconnect(rejected: true);
                                    continue;
                                }

                            }

                            if (data.TryGetProperty("EventIntercepted", out var interception))
                            {
                                string message = data.TryGetProperty("error", out var error) ? error.GetString() : "PostAuthKeyViolation";
                                MelonLogger.Error($"[AirlockFriends] Connection intercepted: A request you made failed authentication and your connection was intercepted,\nThis could be caused when you use Airlock Friends for the first time or you attempted to bypass one or more restrictions, in these cases, you should not reattempt,\nSERVER: {message}.");
                                NotificationLib.QueueNotification($"[<color=red>Unauthorized</color>] Connection intercepted by server");
                                await Disconnect(true);
                                MelonCoroutines.Start(AttemptReconnection(true));
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
                                        if (!reason.Contains("already friends") && !reason.Contains("pending request")) ReceiveDirectMessage(toFriend, "System", $"Operation failed: {reason}", true, false);
                                        break;

                                    case "success":
                                        break;
                                }
                                continue;
                            }

                            if (data.TryGetProperty("type", out var FriendType))
                            {
                                string type = FriendType.GetString();

                                switch (type)
                                {
                                    case "friendRequestReceived":
                                        if (data.TryGetProperty("fromFriendCode", out var fromFR))
                                        {
                                            string Requester = fromFR.GetString();
                                            RPC_OnRequestReceived(Requester);
                                            MelonCoroutines.Start(GetUsername(Requester, name => NotificationLib.QueueNotification($"[<color=magenta>FRIEND REQUEST</color>] <color=lime>{name}</color> wants to friend you!")));
                                        }
                                        break;

                                    case "friendRequest":
                                        if (data.TryGetProperty("toFriendCode", out var SentTo))
                                        {
                                            string toPlayer = SentTo.GetString();
                                            MelonCoroutines.Start(GetUsername(toPlayer, name =>
                                            {
                                                MelonLogger.Msg($"[AirlockFriends] Friend request sent to {name}");
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
                                                UpdateFriend(friendName, "Online", AcceptingFriend, "");
                                                MelonLogger.Msg($"[AirlockFriends] Friend accepted: {friendName} ({AcceptingFriend})");
                                                NotificationLib.QueueNotification($"[<color=lime>FRIEND ACCEPTED</color>] <color=lime>{friendName} ({AcceptingFriend})</color> is now your friend!");
                                            }));
                                        }
                                        break;



                                    case "friendRejected":
                                        if (data.TryGetProperty("byFriendCode", out var RejectedBy))
                                        {
                                            string FriendToReject = RejectedBy.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Friend rejected by {FriendToReject}");
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
                                        if (data.TryGetProperty("fromFriendCode", out var SenderCode) &&
                                            data.TryGetProperty("message", out var MessageContent))
                                        {
                                            string sender = SenderCode.GetString();
                                            string Message = MessageContent.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Message from {sender}: {Message}");
                                            NotificationLib.QueueNotification($"[<color=magenta>MESSAGE</color>] From: <color=lime>{GetFriend(sender).Name}</color> {Message}");
                                            ReceiveDirectMessage(sender, GetFriend(sender).Name, Message);
                                        }
                                        break;

                                    case "sendMessage":
                                        if (data.TryGetProperty("targetFriendCode", out var ToFriendCode))
                                        {
                                            string target = ToFriendCode.GetString();
                                            MelonLogger.Msg($"[AirlockFriends] Message sent to {GetFriend(target).Name}!");
                                            NotificationLib.QueueNotification($"[<color=magenta>SENT</color>] Sent message to <color=lime>{GetFriend(target).Name}</color>!");
                                        }
                                        break;



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
                                            data.TryGetProperty("accepted", out var acceptedProp))
                                        {
                                            string JoiningFriend = FromReceivingJoin.GetString();
                                            bool accepted = acceptedProp.GetBoolean();
                                            string roomCode = data.TryGetProperty("roomID", out var roomProp) ? roomProp.GetString() : "";
                                            bool inGame = data.TryGetProperty("inGame", out var inGameProp) && inGameProp.GetBoolean();

                                            if (accepted)
                                            {
                                                MelonLogger.Msg($"[AirlockFriends] {GetFriend(JoiningFriend).Name} accepted your join request. Room: {roomCode}");
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
                                                    MelonLogger.Msg($"[AirlockFriends] {GetFriend(JoiningFriend).Name} is not in a game.");
                                                    NotificationLib.QueueNotification($"[<color=red>JOIN FAILED</color>] <color=lime>{GetFriend(JoiningFriend).Name}</color> is not in a game.");
                                                }
                                                else
                                                {
                                                    MelonLogger.Msg($"[AirlockFriends] {GetFriend(JoiningFriend).Name} rejected your join request.");
                                                    NotificationLib.QueueNotification($"[<color=red>JOIN REJECTED</color>] <color=lime>{GetFriend(JoiningFriend).Name}</color> did not accept your join request.");
                                                }
                                            }
                                        }
                                        break;




                                    case "inviteResponseResult":
                                        {
                                            if (!data.TryGetProperty("fromFriendCode", out var SenderFriend) ||
                                                !data.TryGetProperty("accepted", out var WasAccepted))
                                                break;

                                            string FriendID = SenderFriend.GetString();
                                            bool accepted = WasAccepted.GetBoolean();

                                            var friend = FriendGUI.GetFriend(FriendID);
                                            string FriendName = friend != null ? friend.Name : FriendID;

                                            if (accepted)
                                            {
                                                MelonLogger.Msg($"[AirlockFriends] {FriendName} accepted your invite!");
                                                NotificationLib.QueueNotification(
                                                    $"[<color=lime>JOIN ACCEPTED</color>] <color=lime>{FriendName}</color> accepted your invite!"
                                                );
                                            }
                                            else
                                            {
                                                MelonLogger.Msg($"[AirlockFriends] {FriendName} denied your invite!");
                                                NotificationLib.QueueNotification(
                                                    $"[<color=red>Denied</color>] <color=lime>{FriendName}</color> did not accept your invite!"
                                                );
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
                                        string name = data.TryGetProperty("name", out var nameProp) && nameProp.ValueKind != System.Text.Json.JsonValueKind.Null ? nameProp.GetString() : "UnknownName";
                                        SaveUsername(friendCode, name);
                                        break;


                                    default:
                                        MelonLogger.Msg($"[AirlockFriends] Unhandled event / reliable raised?! {msg}");
                                        break;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            MelonLogger.Error($"[AirlockFriends] Failed to deserialize reliable: {ex.Message}");
                            NotificationLib.QueueNotification($"[<color=red>ERROR</color>] Failed to deserialize reliable.\nPlease report this to us.\n: Check your console for for info");
                        }
                    }
                }
                catch (Exception ex)
                {
                    MelonLogger.Error($"[AirlockFriends] Server sent an exception: {ex.Message}");
                    if (!IsConnected)
                        MelonCoroutines.Start(AttemptReconnection());
                    break;
                }
            }
            await Disconnect();
        }
        

        // Raise reliable
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
            MelonCoroutines.Start(GetUsername(targetFriendCode, name => MelonLogger.Msg($"[AirlockFriends] Sent friend request to {name}")));
        }

        public static void RPC_OnRequestReceived(string fromFriendCode)
        {
            MelonCoroutines.Start(GetUsername(fromFriendCode, name =>
            {
                MelonLogger.Msg($"[AirlockFriends] Friend request received from {name}");
            }));
            NewFriendRequest = true;
            FriendRequests.Add(fromFriendCode);
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




        public static async Task RPC_FriendAccept(string AcceptingFriend)
        {
            if (string.IsNullOrEmpty(AcceptingFriend) || string.IsNullOrEmpty(FriendCode)) return;

            var request = new
            {
                type = "friendAccept",
                fromPrivateKey = PrivateKey,
                acceptFriendCode = AcceptingFriend
            };

            string json = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(json);
            MelonLogger.Msg($"[AirlockFriends] Accepted friend request from {AcceptingFriend}");
        }

        public static async Task RPC_FriendReject(string incomingFriendCode)
        {
            if (string.IsNullOrEmpty(incomingFriendCode) || string.IsNullOrEmpty(FriendCode))
                return;

            var payload = new
            {
                type = "friendReject",
                fromPrivateKey = PrivateKey,
                rejectFriendCode = incomingFriendCode
            };

            string json = System.Text.Json.JsonSerializer.Serialize(payload);
            await RaiseEvent(json);

            MelonLogger.Msg($"[AirlockFriends] Rejected friend request from {incomingFriendCode}");
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


            string json = System.Text.Json.JsonSerializer.Serialize(request);
            await RaiseEvent(json);
            MelonLogger.Msg($"[AirlockFriends] Removed friend {RemovingFriend}");
            MelonCoroutines.Start(AirlockFriendsOperations.GetUsername(RemovingFriend, name => NotificationLib.QueueNotification($"[<color=red>UNFRIEND</color>] Unfriended {name}")));
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
                if (!string.IsNullOrEmpty(name) && AirlockFriendsOperations.IsUnwantedName(name) && MyName == FriendCode)
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

            var payload = new
            {
                type = "getFriends",
                fromPrivateKey = PrivateKey,
                currentName = MyName
            };

            string json = System.Text.Json.JsonSerializer.Serialize(payload);
            await RaiseEvent(json);
        }





        public static async Task RPC_RequestJoin(string targetFriendCode)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode)) return;

            var payload = new
            {
                type = "joinRequest",
                fromPrivateKey = PrivateKey,
                targetFriendCode = targetFriendCode
            };

            string json = System.Text.Json.JsonSerializer.Serialize(payload);
            await RaiseEvent(json);
            MelonLogger.Msg($"[AirlockFriends] Sent join request to {targetFriendCode}");
        }

        public static async Task RPC_SendInvite(string targetFriendCode, string roomID)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode)) return;

            var payload = new
            {
                type = "sendInvite",
                fromPrivateKey = PrivateKey,
                targetFriendCode = targetFriendCode,
                roomID = roomID
            };

            string json = System.Text.Json.JsonSerializer.Serialize(payload);
            await RaiseEvent(json);
            MelonLogger.Msg($"[AirlockFriends] Sent join request to {targetFriendCode}");
        }


        public static async Task RPC_RespondToInvite(string targetFriendCode, bool accepted)
        {
            if (string.IsNullOrEmpty(targetFriendCode) || string.IsNullOrEmpty(FriendCode)) return;

            var payload = new
            {
                type = "inviteResponse",
                fromPrivateKey = PrivateKey,
                targetFriendCode = targetFriendCode,
                accepted = accepted
            };

            string json = System.Text.Json.JsonSerializer.Serialize(payload);
            await RaiseEvent(json);
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
                    MelonCoroutines.Start(AirlockFriendsOperations.GetUsername(senderFriendCode, name => NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] Accepted {name}'s join request")));
                }
            }
            else if (InGame)
                MelonCoroutines.Start(AirlockFriendsOperations.GetUsername(senderFriendCode, name => NotificationLib.QueueNotification($"[<color=lime>SUCCESS</color>] Rejected {name}'s join request")));

            var payload = new
                {
                    type = "joinRequestResponse",
                    fromPrivateKey = PrivateKey,
                    targetFriendCode = senderFriendCode,
                    accepted = accepted,
                    roomID = roomCode,
                    inGame = InGame
                };

            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            };

            string json = System.Text.Json.JsonSerializer.Serialize(payload, options);
            await RaiseEvent(json);

            MelonLogger.Msg($"[AirlockFriends] Responded to join request from {senderFriendCode}: {(accepted ? $"accepted (room {roomCode})" : "rejected")}");
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

                string json = System.Text.Json.JsonSerializer.Serialize(request);
                await RaiseEvent(json);
            }
        }





        // this took too long to make and understand lmao
        public static IEnumerator GetUsername(string friendCode, System.Action<string> Callback)
        {
            if (GetNameFromID.TryGetValue(friendCode, out var name))
            {
                Callback?.Invoke(name);
                yield break;
            }

            if (!GettingNames.Contains(friendCode))
            {
                GettingNames.Add(friendCode);
                _ = RPC_GetUsername(friendCode);
            }

            float AttemptTime = 0f;
            while (!GetNameFromID.TryGetValue(friendCode, out name) && AttemptTime < 5)
            {
                AttemptTime += 0.1f;
                yield return new WaitForSeconds(0.1f);
            }

            if (string.IsNullOrEmpty(name))
                name = friendCode;


            Callback?.Invoke(name);
        }





        public static void SaveUsername(string friendCode, string name)
        {
            if (string.IsNullOrEmpty(friendCode)) return;

            GetNameFromID[friendCode] = name;

            GettingNames.Remove(friendCode);

            OnUsernameUpdated?.Invoke(friendCode, name);
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

                await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnected", cts.Token);

                MelonLogger.Msg("[AirlockFriends] Disconnected from Websocket server.");
                NotificationLib.QueueNotification("[<color=magenta>Disconnect</color>] Disconnected from Websocket Server.");
            }
            catch (Exception ex)
            {
                MelonLogger.Error("[AirlockFriends] Disconnect failed: " + ex.Message);
            }
        }




    }
}
