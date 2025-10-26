using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Il2CppFusion;
using Il2CppPhoton.Realtime;
using Il2CppSG.Airlock;
using MelonLoader;
using ShadowsPublicMenu.Config;
using UnityEngine;
using UnityEngine.Networking;

namespace ShadowsPublicMenu.Managers
{
    public static class ConsoleManager
    {
        static readonly HttpClient client = new HttpClient();
        const string API = "https://shadowsmenu.jolyne108.workers.dev/console/identify";

        // Show yourself & others using the menu in your room
        public static async void Add() =>
            await SendRequest("add", GameReferences.Rig.PState.PlayerModerationID.Value);

        public static async void AddStm(string Steam) =>
            await SendRequest("add", Steam);

        public static async void Remove() =>
            await SendRequest("remove", GameReferences.Rig.PState.PlayerModerationID.Value);

        public static async Task<bool> Identify(PlayerState target)
        {
            if (target == null) return false;

            try
            {
                string url = $"{API}?action=has&id={Uri.EscapeDataString(target.PlayerModerationID.Value)}";
                var res = await client.GetAsync(url);
                var text = await res.Content.ReadAsStringAsync();
                return res.IsSuccessStatusCode && text.Contains("\"exists\":true");
            }
            catch { return false; }
        }

        static async Task SendRequest(string action, string id)
        {
            try
            {
                string url = $"{API}?action={action}&id={Uri.EscapeDataString(id)}";
                var res = await client.GetAsync(url);
                var text = await res.Content.ReadAsStringAsync();
                MelonLogger.Msg($"[Console] Sent request {action}");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"[Console] Failed: {e.Message}");
            }
        }
    }
}
