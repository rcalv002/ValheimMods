using HarmonyLib;
using UnityEngine;

namespace ValhallaMeter;

// Only active during explicit meter interaction. Valheim resumes its own cursor
// and gameplay input rules when F8/F7/Esc is pressed or the application loses focus.
[HarmonyPatch(typeof(GameCamera), nameof(GameCamera.UpdateMouseCapture))]
internal static class MeterCursorPatch
{
    private static void Postfix()
    {
        if (Plugin.Instance?.Interacting != true) return;
        ZCursor.LockState = CursorLockMode.None;
        ZCursor.Show();
        Cursor.visible = true;
    }
}

[HarmonyPatch(typeof(PlayerController), "TakeInput")]
internal static class MeterPlayerInputPatch
{
    private static bool Prefix(ref bool __result)
    {
        if (Plugin.Instance?.Interacting != true) return true;
        __result = false;
        return false;
    }
}
