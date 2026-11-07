using System;
using DiskCardGame;
using UnityEngine;
using UnityEngine.UI;

namespace OnionQOL
{
    /// <summary>
    /// 按 F1 打开的独立设置窗口：
    /// - 启用牌组查看 + 按键
    /// - 启用抽牌堆查看 + 按键
    /// 两个功能的按键互斥，配置值冲突时拒绝保存。
    /// </summary>
    public class SettingsWindow : MonoBehaviour
    {
        public static SettingsWindow Instance;

        private GameObject uiRoot;
        private Text enableDeckValueText;
        private Text deckKeyText;
        private Text enableDrawValueText;
        private Text drawKeyText;
        private Text messageText;
        private RectTransform enableDeckRow;
        private RectTransform deckKeyRow;
        private RectTransform enableDrawRow;
        private RectTransform drawKeyRow;

        private string message = string.Empty;
        private float messageUntil;

        public static void Open()
        {
            if (Instance != null)
            {
                return;
            }
            GameObject go = new GameObject("OnionQOL_SettingsWindow");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<SettingsWindow>();
            Instance.Build();
        }

        public static void Close()
        {
            if (Instance == null)
            {
                return;
            }
            Instance.Teardown();
            UnityEngine.Object.Destroy(Instance.gameObject);
            Instance = null;
        }

        public static void RefreshLabels()
        {
            if (Instance != null)
            {
                Instance.RefreshLabelsInternal();
            }
        }

        public static void ShowMessage(string text)
        {
            if (Instance != null)
            {
                Instance.ShowMessageInternal(text);
            }
        }

        private void Build()
        {
            uiRoot = new GameObject("OnionQOL_SettingsUI", typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(uiRoot);

            Canvas canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            Font font = GetChineseFont();

            GameObject bg = CreateUiObject("Background", uiRoot.transform);
            Image bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, 0.7f);
            Stretch(bg.GetComponent<RectTransform>());

            GameObject panel = CreateUiObject("Panel", uiRoot.transform);
            Image panelBg = panel.AddComponent<Image>();
            panelBg.color = new Color(0.05f, 0.05f, 0.05f, 0.97f);
            RectTransform panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.sizeDelta = new Vector2(900f, 720f);

            Text title = CreateText("Title", panel.transform, font, 40, "Onion QOL 设置");
            RectTransform titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -30f);
            titleRt.sizeDelta = new Vector2(800f, 60f);

            enableDeckRow = CreateRow(panel.transform, "EnableDeckRow", -110f, "启用牌组查看", out enableDeckValueText, font);
            deckKeyRow = CreateRow(panel.transform, "DeckKeyRow", -200f, "打开牌组按键", out deckKeyText, font);
            enableDrawRow = CreateRow(panel.transform, "EnableDrawRow", -290f, "启用抽牌堆查看", out enableDrawValueText, font);
            drawKeyRow = CreateRow(panel.transform, "DrawKeyRow", -380f, "查看抽牌堆按键", out drawKeyText, font);

            messageText = CreateText("Message", panel.transform, font, 26, string.Empty);
            messageText.color = new Color(1f, 0.75f, 0.4f, 1f);
            RectTransform messageRt = messageText.rectTransform;
            messageRt.anchorMin = new Vector2(0.5f, 1f);
            messageRt.anchorMax = new Vector2(0.5f, 1f);
            messageRt.pivot = new Vector2(0.5f, 1f);
            messageRt.anchoredPosition = new Vector2(0f, -470f);
            messageRt.sizeDelta = new Vector2(800f, 44f);

            Text hint = CreateText("Hint", panel.transform, font, 26, "按 F1 或 ESC 关闭");
            RectTransform hintRt = hint.rectTransform;
            hintRt.anchorMin = new Vector2(0.5f, 0f);
            hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.anchoredPosition = new Vector2(0f, 26f);
            hintRt.sizeDelta = new Vector2(700f, 50f);

            RefreshLabelsInternal();
            HideCursor();
        }

        private RectTransform CreateRow(Transform panel, string name, float y, string label, out Text valueText, Font font)
        {
            GameObject rowGo = CreateUiObject(name, panel);
            Image rowBg = rowGo.AddComponent<Image>();
            rowBg.color = new Color(0.16f, 0.16f, 0.18f, 0.95f);
            RectTransform rowRt = rowGo.GetComponent<RectTransform>();
            rowRt.anchorMin = new Vector2(0.5f, 1f);
            rowRt.anchorMax = new Vector2(0.5f, 1f);
            rowRt.pivot = new Vector2(0.5f, 1f);
            rowRt.anchoredPosition = new Vector2(0f, y);
            rowRt.sizeDelta = new Vector2(800f, 76f);

            Text labelText = CreateText("Label", rowGo.transform, font, 30, label);
            labelText.alignment = TextAnchor.MiddleLeft;
            RectTransform labelRt = labelText.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 0.5f);
            labelRt.anchorMax = new Vector2(0f, 0.5f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.anchoredPosition = new Vector2(28f, 0f);
            labelRt.sizeDelta = new Vector2(430f, 56f);

            valueText = CreateText("Value", rowGo.transform, font, 30, string.Empty);
            valueText.alignment = TextAnchor.MiddleRight;
            RectTransform valueRt = valueText.rectTransform;
            valueRt.anchorMin = new Vector2(1f, 0.5f);
            valueRt.anchorMax = new Vector2(1f, 0.5f);
            valueRt.pivot = new Vector2(1f, 0.5f);
            valueRt.anchoredPosition = new Vector2(-28f, 0f);
            valueRt.sizeDelta = new Vector2(320f, 56f);

            return rowRt;
        }

        private void Update()
        {
            try
            {
                if (Input.GetMouseButtonDown(0))
                {
                    if (RectContains(enableDeckRow))
                    {
                        ToggleBool(OnionQOLPlugin.EnableDeckViewer);
                    }
                    else if (RectContains(deckKeyRow))
                    {
                        OnionQOLPlugin.Instance?.StartKeyBind(false);
                    }
                    else if (RectContains(enableDrawRow))
                    {
                        ToggleBool(OnionQOLPlugin.EnableDrawPileViewer);
                    }
                    else if (RectContains(drawKeyRow))
                    {
                        OnionQOLPlugin.Instance?.StartKeyBind(true);
                    }
                }

                RefreshLabelsInternal();

                if (messageText != null)
                {
                    if (!string.IsNullOrEmpty(message) && Time.unscaledTime > messageUntil)
                    {
                        message = string.Empty;
                    }
                    messageText.text = message;
                }
            }
            catch (Exception e)
            {
                OnionQOLPlugin.Log?.LogError("Onion QOL: 设置窗口异常: " + e);
            }
        }

        private void ToggleBool(BepInEx.Configuration.ConfigEntry<bool> entry)
        {
            entry.Value = !entry.Value;
            if (OnionQOLPlugin.Instance != null)
            {
                OnionQOLPlugin.Instance.Config.Save();
            }
            if (entry == OnionQOLPlugin.EnableDeckViewer && !entry.Value && DeckWindow.Instance != null)
            {
                DeckWindow.Close();
            }
        }

        private void RefreshLabelsInternal()
        {
            if (enableDeckValueText != null)
            {
                enableDeckValueText.text = OnionQOLPlugin.EnableDeckViewer.Value ? "开" : "关";
            }
            if (enableDrawValueText != null)
            {
                enableDrawValueText.text = OnionQOLPlugin.EnableDrawPileViewer.Value ? "开" : "关";
            }

            bool binding = OnionQOLPlugin.Instance != null && OnionQOLPlugin.Instance.AwaitingKeyBind;
            bool bindingDraw = OnionQOLPlugin.Instance != null && OnionQOLPlugin.Instance.BindingDrawPile;

            if (deckKeyText != null)
            {
                deckKeyText.text = (binding && !bindingDraw)
                    ? "按任意键...（ESC 取消）"
                    : OnionQOLPlugin.DeckViewerKey.Value.ToString();
            }
            if (drawKeyText != null)
            {
                drawKeyText.text = (binding && bindingDraw)
                    ? "按任意键...（ESC 取消）"
                    : OnionQOLPlugin.DrawPileKey.Value.ToString();
            }
        }

        private void ShowMessageInternal(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 2.5f;
        }

        private static bool RectContains(RectTransform rt)
        {
            return rt != null && RectTransformUtility.RectangleContainsScreenPoint(rt, Input.mousePosition, null);
        }

        private void HideCursor()
        {
            try
            {
                InteractionCursor cursor = Singleton<InteractionCursor>.Instance;
                if (cursor != null)
                {
                    cursor.SetHidden(true);
                }
                Cursor.visible = true;
            }
            catch (Exception e)
            {
                OnionQOLPlugin.Log?.LogWarning("Onion QOL: 处理光标失败: " + e);
            }
        }

        private void ShowCursor()
        {
            try
            {
                Cursor.visible = false;
                InteractionCursor cursor = Singleton<InteractionCursor>.Instance;
                if (cursor != null)
                {
                    cursor.SetHidden(false);
                }
            }
            catch (Exception e)
            {
                OnionQOLPlugin.Log?.LogWarning("Onion QOL: 恢复光标失败: " + e);
            }
        }

        private void Teardown()
        {
            ShowCursor();
            if (uiRoot != null)
            {
                UnityEngine.Object.Destroy(uiRoot);
                uiRoot = null;
            }
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static Text CreateText(string name, Transform parent, Font font, int size, string content)
        {
            GameObject go = CreateUiObject(name, parent);
            Text text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = content;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static Font GetChineseFont()
        {
            try
            {
                FontReplacementData.Initialize();
                if (FontReplacementData.instance != null)
                {
                    FontReplacementData.LanguageFontReplacements zh = FontReplacementData.instance.languageFontReplacements.Find(
                        (FontReplacementData.LanguageFontReplacements x) => x.languages.Contains(Language.ChineseSimplified));
                    if (zh != null && zh.fontReplacements != null && zh.fontReplacements.Count > 0)
                    {
                        Font font = zh.fontReplacements[0].Font;
                        if (font != null)
                        {
                            return font;
                        }
                    }
                }
            }
            catch
            {
            }
            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}
