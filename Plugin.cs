using System;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Rewired;
using UnityEngine;

namespace PhoneAccess;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    internal static new ManualLogSource Logger;

    private void Awake()
    {
        Logger = base.Logger;
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
        var harmony = new Harmony(MyPluginInfo.PLUGIN_GUID);
        harmony.PatchAll();
        Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} patches done: {harmony.GetPatchedMethods().ToList().Count} methods");
    } 
}

[HarmonyPatch]
public class PluginPatches
{
    [HarmonyPatch(typeof(BetterPlayerControl), "Update")]
    [HarmonyPrefix]
    private static void BetterPlayerControlUpdatePatch(BetterPlayerControl __instance)
    {
        try
        {
            if (Singleton<GameController>.Instance.viewState == VIEW_STATE.TALKING)
            {
                Rewired.Player player = ReInput.players.GetPlayer(0);
                if ((player.GetButtonDown(5) || player.GetButtonDown(29)) && !__instance.isGameEndingOn && !Singleton<PhoneManager>.Instance.IsPhoneAnimating() && Singleton<PhoneManager>.Instance.CanCloseCommApp() && !Singleton<PhoneManager>.Instance.BlockPhoneOpening)
                {
                    if (!Singleton<PhoneManager>.Instance.IsPhoneMenuOpened())
                    {
                        OpenPhone(__instance);
                    }
                    else
                    {
                        ClosePhone(__instance);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Error in BetterPlayerControlUpdatePatch: {ex.Message}");
        }
    }

    private static bool IsOpenedInDialogue = false;

    private static void OpenPhone(BetterPlayerControl __instance)
    {
        IsOpenedInDialogue = true;
        Singleton<PhoneManager>.Instance.OpenPhoneAsync(null);
        __instance.ButtonPressed?.Invoke("Phone");
    }

    private static void ClosePhone(BetterPlayerControl __instance)
    {
        Singleton<PhoneManager>.Instance.ClosePhoneAsync(null, false);
        __instance.ButtonPressed?.Invoke("Phone");
        CursorLocker.Unlock();
        IsOpenedInDialogue = false;
    }

    [HarmonyPatch(typeof(PhoneManager), "ClosePhoneAsync")]
    [HarmonyPostfix]
    private static void PhoneManagerClosePhoneAsyncPatch()
    {
        IsOpenedInDialogue = false;
    }

    [HarmonyPatch(typeof(PhoneManager), "ClosePhoneMainMenuIfNotRequired")]
    [HarmonyPrefix]
    private static bool PhoneManagerClosePhoneMainMenuIfNotRequiredPatch(PhoneManager __instance)
    {
        if (IsOpenedInDialogue)
        {
            return false;
        }

        return true;
    }

    [HarmonyPatch(typeof(PhoneManager), "UpdateApps")]
    [HarmonyPostfix]
    private static void PhoneManagerUpdateAppsPatch(PhoneManager __instance, bool IsGlassesEquipped)
    {
        try
        {
            string[] appNames = ["phoneButtonThiscord", "phoneButtonWrkspace"];
            foreach (string appName in appNames)
            {
                GameObject obj = __instance.GetType().GetField(appName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(__instance) as GameObject;
                ChangeGameObjectEnabledState(obj, !IsOpenedInDialogue);
            }

            string[] appNamesOnlyDisabledInDialogue = ["phoneButtonCredits", "phoneButtonSkylar", "phoneButtonPhoenicia", "phoneButtonReggie"];
            if (IsOpenedInDialogue)
            {
                foreach (string appName in appNamesOnlyDisabledInDialogue)
                {
                    GameObject obj = __instance.GetType().GetField(appName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(__instance) as GameObject;
                    ChangeGameObjectEnabledState(obj, false);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError($"Error in PhoneManagerUpdateAppsPatch: {ex.Message}");
        }
    }

    private static void ChangeGameObjectEnabledState(GameObject obj, bool state)
    {
        if (obj != null)
        {
            if (obj.activeSelf == state) return;

            obj.SetActive(state);
        }
    }

    [HarmonyPatch(typeof(BetterPlayerControl), "HandleChargingUp")]
    [HarmonyPrefix]
    private static bool BetterPlayerControlHandleChargingUpPatch(BetterPlayerControl __instance)
    {
        if (IsOpenedInDialogue)
        {
            return false;
        }
        return true;
    }

    [HarmonyPatch(typeof(SaveScreenManager), nameof(SaveScreenManager.ShowAllTabs))]
    [HarmonyPrefix]
    private static bool SaveScreenManagerShowAllTabsPatch(SaveScreenManager __instance)
    {
        if (IsOpenedInDialogue)
        {
            __instance.ShowOnlyLoadTab();
            return false;
        }

        return true;
    }
}
