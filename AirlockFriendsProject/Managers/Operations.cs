using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AirlockFriends.Managers;
using MelonLoader;

public static class AirlockFriendsOperations2
{
    private static ClientWebSocket socket;
    private static CancellationTokenSource _cts;
    public static bool IsConnected => socket != null && socket.State == WebSocketState.Open;

    private const string Server = "wss://lank-lucretia-timocratical.ngrok-free.dev/";

    // Send messages to a specific friend
    private static string messagingFriend = "";


    public static async Task SendRawAsync(string json)
    {
        if (!IsConnected) return;
        try
        {
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
        }
        catch (Exception ex)
        {
            MelonLogger.Error("[AirlockFriends] Send failed: " + ex.Message);
        }
    }


}
