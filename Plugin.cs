using System;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using DiskCardGame;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace OnionQOL
{
    public enum BindingTarget
    {
        Deck,
        DrawPile,
        Settings
    }

    [BepInPlugin(GUID, PluginDisplayName, PluginVersion)]
    public class OnionQOLPlugin : BaseUnityPlugin
    {
        public const string GUID = "onion.qol";
        public const string PluginDisplayName = "Onion QOL";
        public const string PluginVersion = "0.1.0";

        public static OnionQOLPlugin Instance;
        public static ManualLogSource Log;

        public static ConfigEntry<bool> EnableDeckViewer;
        public static ConfigEntry<KeyCode> DeckViewerKey;
        public static ConfigEntry<bool> EnableDrawPileViewer;
        public static ConfigEntry<KeyCode> DrawPileKey;
        public static ConfigEntry<KeyCode> SettingsKey;
        public static ConfigEntry<bool> EnableSkipButton;

        internal bool awaitingKeyBind;
        internal BindingTarget bindingTarget;
        public bool AwaitingKeyBind => awaitingKeyBind;
        public BindingTarget BindingTarget => bindingTarget;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            EnableDeckViewer = Config.Bind(
                "General",
                "EnableDeckViewer",
                true,
                "启用牌组查看：在凯西模组对局中按下快捷键打开本局牌组。");
            DeckViewerKey = Config.Bind(
                "General",
                "DeckViewerKey",
                KeyCode.Q,
                "打开牌组的按键（仅支持键盘键，可在设置面板中修改）。");
            EnableDrawPileViewer = Config.Bind(
                "General",
                "EnableDrawPileViewer",
                true,
                "启用抽牌堆查看：战斗中按下快捷键查看抽牌堆剩余牌。");
            DrawPileKey = Config.Bind(
                "General",
                "DrawPileKey",
                KeyCode.E,
                "查看抽牌堆的按键（仅支持键盘键，可在设置面板中修改）。");
            SettingsKey = Config.Bind(
                "General",
                "SettingsKey",
                KeyCode.F1,
                "打开设置面板的按键（仅支持键盘键，可在设置面板中修改）。");
            EnableSkipButton = Config.Bind(
                "General",
                "EnableSkipButton",
                true,
                "启用选牌/奖励界面的跳过按钮。");

            try
            {
                var harmony = new Harmony(GUID);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                RoomHint.Initialize();
                Logger.LogInfo("Onion QOL 已加载");
            }
            catch (Exception e)
            {
                Logger.LogError("Onion QOL Harmony 初始化失败: " + e);
            }
        }

        private void Update()
        {
            try
            {
                if (awaitingKeyBind)
                {
                    if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        awaitingKeyBind = false;
                        SettingsWindow.RefreshLabels();
                        return;
                    }

                    KeyCode pressed = GetPressedKeyboardKey();
                    if (pressed != KeyCode.None)
                    {
                        ApplyBinding(bindingTarget, pressed);
                        awaitingKeyBind = false;
                        SettingsWindow.RefreshLabels();
                    }
                    return;
                }

                if (Input.GetKeyDown(SettingsKey.Value))
                {
                    if (SettingsWindow.Instance != null)
                    {
                        SettingsWindow.Close();
                    }
                    else
                    {
                        SettingsWindow.Open();
                    }
                    return;
                }

                if (SettingsWindow.Instance != null && Input.GetKeyDown(KeyCode.Escape))
                {
                    SettingsWindow.Close();
                    return;
                }

                bool sameKey = DeckViewerKey.Value == DrawPileKey.Value;

                if (EnableDeckViewer.Value && Input.GetKeyDown(DeckViewerKey.Value))
                {
                    ToggleDeckWindow(DeckWindowMode.Deck);
                    return;
                }

                if (!sameKey && EnableDrawPileViewer.Value && Input.GetKeyDown(DrawPileKey.Value))
                {
                    ToggleDeckWindow(DeckWindowMode.DrawPile);
                    return;
                }

                RoomHint.Update();
                SkipButton.UpdateAttach();
            }
            catch (Exception e)
            {
                Logger.LogError("Onion QOL Update 异常: " + e);
            }
        }

        private void ApplyBinding(BindingTarget target, KeyCode key)
        {
            try
            {
                if (target == BindingTarget.Deck)
                {
                    if (key == DrawPileKey.Value || key == SettingsKey.Value)
                    {
                        SettingsWindow.ShowMessage("该按键已被其他功能占用");
                        return;
                    }
                    DeckViewerKey.Value = key;
                }
                else if (target == BindingTarget.DrawPile)
                {
                    if (key == DeckViewerKey.Value || key == SettingsKey.Value)
                    {
                        SettingsWindow.ShowMessage("该按键已被其他功能占用");
                        return;
                    }
                    DrawPileKey.Value = key;
                }
                else
                {
                    if (key == DeckViewerKey.Value || key == DrawPileKey.Value)
                    {
                        SettingsWindow.ShowMessage("该按键已被其他功能占用");
                        return;
                    }
                    SettingsKey.Value = key;
                }
                Config.Save();
                SettingsWindow.ShowMessage("已设置为 " + key);
            }
            catch (Exception e)
            {
                Logger.LogError("Onion QOL 设置按键失败: " + e);
            }
        }

        private void ToggleDeckWindow(DeckWindowMode mode)
        {
            if (DeckWindow.Instance != null)
            {
                DeckWindow.Close();
                return;
            }
            if (mode == DeckWindowMode.DrawPile)
            {
                CardDrawPiles piles = Singleton<CardDrawPiles>.Instance;
                if (piles == null || piles.Deck == null)
                {
                    return;
                }
            }
            DeckWindow.Open(mode);
        }

        private static KeyCode GetPressedKeyboardKey()
        {
            if (!Input.anyKeyDown)
            {
                return KeyCode.None;
            }

            foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
            {
                if (key == KeyCode.None) continue;
                if (key >= KeyCode.Mouse0 && key <= KeyCode.Mouse6) continue; // 鼠标键
                if (key >= KeyCode.JoystickButton0) continue;                 // 手柄键
                if (Input.GetKeyDown(key)) return key;
            }
            return KeyCode.None;
        }

        internal void StartKeyBind(BindingTarget target)
        {
            awaitingKeyBind = true;
            bindingTarget = target;
            SettingsWindow.RefreshLabels();
        }
    }
}
