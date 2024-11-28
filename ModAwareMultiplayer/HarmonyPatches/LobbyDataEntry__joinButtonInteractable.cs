﻿using HarmonyLib;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using UnityEngine;

namespace Marioalexsan.ModAwareMultiplayer.HarmonyPatches;

[HarmonyPatch]
static class VersionPatches
{
    static IEnumerable<MethodInfo> TargetMethods()
    {
        yield return AccessTools.GetDeclaredMethods(typeof(LobbyDataEntry)).FirstOrDefault(x => x.Name.Contains("_joinButtonInteractable"));
        yield return AccessTools.Method(typeof(LobbyListManager), nameof(LobbyListManager.Iterate_SteamLobbies));
        yield return AccessTools.Method(typeof(MainMenuManager), nameof(MainMenuManager.Set_MenuCondition));
        yield return AccessTools.Method(typeof(ChatBehaviour), nameof(ChatBehaviour.OnStartAuthority));
        yield return AccessTools.Method(typeof(ChatBehaviour), nameof(ChatBehaviour.New_ChatMessage));
        yield return AccessTools.Method(typeof(SteamLobby), nameof(SteamLobby.OnLobbyCreated));
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> code)
    {
        var matcher = new CodeMatcher(code)
            .MatchForward(false, [
                new CodeMatch(OpCodes.Call, AccessTools.Property(typeof(Application), nameof(Application.version)).GetGetMethod())
                ]);

        var labels = matcher.Instruction.labels.ToList();
        matcher.Instruction.labels.Clear();

        matcher
            .RemoveInstruction()
            .Insert([
                new CodeInstruction(OpCodes.Call, AccessTools.Property(typeof(ModAwareMultiplayer), nameof(ModAwareMultiplayer.ModdedNetworkApplicationVersion)).GetGetMethod())
                ]);

        matcher.Instruction.labels = labels;

        return matcher.InstructionEnumeration();
    }
}