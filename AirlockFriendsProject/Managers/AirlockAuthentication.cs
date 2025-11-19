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
            string privateKey;

            if (File.Exists(FilePath))
            {
                var lines = File.ReadAllLines(FilePath);
                if (lines.Length >= 1 && !string.IsNullOrEmpty(lines[0]))
                {
                    privateKey = lines[0];
                    MelonLogger.Msg($"[AirlockFriends] Loaded existing PrivateKey: {privateKey}");
                    return privateKey;
                }
            }

            privateKey = Guid.NewGuid().ToString("N");
            File.WriteAllText(FilePath, privateKey);
            MelonLogger.Msg($"[AirlockFriends] Generated new PrivateKey: {privateKey}");
            return privateKey;
        }
    }
}
