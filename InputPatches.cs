using HarmonyLib;

namespace OnionQOL
{
    /// <summary>
    /// 牌组窗口打开时拦截游戏的按钮输入（InputButtons 是游戏所有按键/手柄动作的统一入口），
    /// 防止在地图界面按 D/W 等仍触发地图操作。
    /// </summary>
    [HarmonyPatch(typeof(InputButtons))]
    internal static class InputPatches
    {
        private static bool Blocked => DeckWindow.Instance != null || SettingsWindow.Instance != null;

        [HarmonyPatch(nameof(InputButtons.GetButton))]
        [HarmonyPostfix]
        private static void GetButton_Postfix(ref bool __result)
        {
            if (Blocked)
            {
                __result = false;
            }
        }

        [HarmonyPatch(nameof(InputButtons.GetButtonDown))]
        [HarmonyPostfix]
        private static void GetButtonDown_Postfix(ref bool __result)
        {
            if (Blocked)
            {
                __result = false;
            }
        }

        [HarmonyPatch(nameof(InputButtons.GetButtonUp))]
        [HarmonyPostfix]
        private static void GetButtonUp_Postfix(ref bool __result)
        {
            if (Blocked)
            {
                __result = false;
            }
        }

        [HarmonyPatch(nameof(InputButtons.GetButtonRepeating))]
        [HarmonyPostfix]
        private static void GetButtonRepeating_Postfix(ref bool __result)
        {
            if (Blocked)
            {
                __result = false;
            }
        }

        [HarmonyPatch(nameof(InputButtons.GetAxis))]
        [HarmonyPostfix]
        private static void GetAxis_Postfix(ref float __result)
        {
            if (Blocked)
            {
                __result = 0f;
            }
        }

        [HarmonyPatch(nameof(InputButtons.AnyGamepadButton))]
        [HarmonyPostfix]
        private static void AnyGamepadButton_Postfix(ref bool __result)
        {
            if (Blocked)
            {
                __result = false;
            }
        }
    }
}
