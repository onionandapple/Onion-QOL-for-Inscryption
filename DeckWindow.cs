using System;
using System.Collections.Generic;
using System.Reflection;
using DiskCardGame;
using HarmonyLib;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OnionQOL
{
    public enum DeckWindowMode
    {
        Deck,
        DrawPile
    }

    /// <summary>
    /// 牌组展示窗口（3D 展示空间 + 专用相机 RenderTexture + UGUI 半透明窗口）。
    /// 卡面使用游戏原版 3D 卡牌 prefab，按原版布局排列。
    /// </summary>
    public class DeckWindow : MonoBehaviour
    {
        public static DeckWindow Instance;

        private const int DisplayLayer = 31;
        private const float BackgroundAlpha = 0.7f;

        private GameObject displayRoot;
        private Camera renderCamera;
        private RenderTexture renderTexture;
        private GameObject uiRoot;

        private static Vector2 cardSize = new Vector2(1.4f, 2f);
        private RectTransform rawRect;
        private Text hintText;
        private GameObject hoveredCard;
        private Vector3 hoveredOriginalPos;
        private Vector3 hoveredOriginalRot;
        private Vector3 hoveredOriginalScale;
        private readonly List<GameObject> spawnedCards = new List<GameObject>();
        private GameObject encyclopediaPanel;
        private Text encyclopediaTitle;
        private Text encyclopediaBody;

        private ViewLockState cachedLockState;
        private bool restoredLockOnce;
        private string openedScene = string.Empty;

        // 原版布局参数（从 DeckReviewSequencer.cardArray 反射读取，失败时用默认值）
        private static bool layoutLoaded;
        private static GameObject cardPrefab;
        private static DeckWindowMode windowMode = DeckWindowMode.Deck;
        private static int maxRows = 2;
        private static int maxCardsPerRow = 10;
        private static float rowSpacing = 2f;
        private static float arrayWidth = 7f;
        private static float leftAnchor = -3.25f;
        private static float curveFactor = 1f;

        public static void Open(DeckWindowMode mode)
        {
            if (Instance != null)
            {
                return;
            }
            windowMode = mode;
            GameObject go = new GameObject("OnionQOL_DeckWindow");
            UnityEngine.Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<DeckWindow>();
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

        private void Update()
        {
            bool sceneChanged = !string.IsNullOrEmpty(openedScene)
                && SceneManager.GetActiveScene().name != openedScene;
            if (sceneChanged || Singleton<ViewManager>.Instance == null || SaveManager.SaveFile == null
                || (windowMode == DeckWindowMode.DrawPile && Singleton<CardDrawPiles>.Instance == null))
            {
                OnionQOLPlugin.Log?.LogWarning("Onion QOL: 场景已切换，关闭牌组窗口");
                Close();
                return;
            }
            UpdateEncyclopediaInput();
            UpdateHover();

            if (hintText != null)
            {
                hintText.text = "[" + CloseKey() + "] 关闭";
            }
        }

        private static string CloseKey()
        {
            return windowMode == DeckWindowMode.DrawPile
                ? OnionQOLPlugin.DrawPileKey.Value.ToString()
                : OnionQOLPlugin.DeckViewerKey.Value.ToString();
        }

        private void OnDestroy()
        {
            Teardown();
        }

        private void Build()
        {
            try
            {
                openedScene = SceneManager.GetActiveScene().name;
                List<CardInfo> cards = GetCards(windowMode);
                if (cards == null || cards.Count == 0)
                {
                    OnionQOLPlugin.Log?.LogWarning("Onion QOL: 当前牌组/抽牌堆为空");
                    Close();
                    return;
                }

                LoadLayoutData();
                if (cardPrefab == null)
                {
                    OnionQOLPlugin.Log?.LogError("Onion QOL: 未能取得 3D 卡面 prefab，无法建立牌组窗口");
                    Close();
                    return;
                }

                BuildDisplaySpace(cards);
                BuildRenderCamera();
                BuildUi(cards.Count);
                LockPlayerInput();
                HideCursor();
            }
            catch (Exception e)
            {
                OnionQOLPlugin.Log?.LogError("Onion QOL: 建立牌组窗口失败: " + e);
                Close();
            }
        }

        private static List<CardInfo> GetCards(DeckWindowMode mode)
        {
            try
            {
                if (mode == DeckWindowMode.DrawPile)
                {
                    CardDrawPiles piles = Singleton<CardDrawPiles>.Instance;
                    if (piles == null || piles.Deck == null)
                    {
                        return null;
                    }
                    return new List<CardInfo>(piles.Deck.Cards);
                }
                if (SaveManager.SaveFile == null)
                {
                    return null;
                }
                return new List<CardInfo>(SaveManager.SaveFile.CurrentDeck.Cards);
            }
            catch (Exception e)
            {
                OnionQOLPlugin.Log?.LogError("Onion QOL: 读取牌组/抽牌堆失败: " + e);
                return null;
            }
        }

        private static void LoadLayoutData()
        {
            if (layoutLoaded)
            {
                return;
            }
            layoutLoaded = true;
            try
            {
                DeckReviewSequencer sequencer = Singleton<DeckReviewSequencer>.Instance;
                if (sequencer == null)
                {
                    OnionQOLPlugin.Log?.LogWarning("Onion QOL: 当前场景没有 DeckReviewSequencer，使用默认卡面布局参数");
                    return;
                }
                FieldInfo cardArrayField = AccessTools.Field(typeof(DeckReviewSequencer), "cardArray");
                SelectableCardArray cardArray = cardArrayField?.GetValue(sequencer) as SelectableCardArray;
                if (cardArray == null)
                {
                    return;
                }

                cardPrefab = AccessTools.Field(typeof(SelectableCardArray), "selectableCardPrefab")?.GetValue(cardArray) as GameObject;
                maxRows = ReadInt(cardArray, "maxRows", maxRows);
                maxCardsPerRow = ReadInt(cardArray, "maxCardsPerRow", maxCardsPerRow);
                rowSpacing = ReadFloat(cardArray, "rowSpacing", rowSpacing);
                arrayWidth = ReadFloat(cardArray, "arrayWidth", arrayWidth);
                leftAnchor = ReadFloat(cardArray, "leftAnchor", leftAnchor);
                curveFactor = ReadFloat(cardArray, "curveFactor", curveFactor);
            }
            catch (Exception e)
            {
                OnionQOLPlugin.Log?.LogError("Onion QOL: 读取原版布局参数失败: " + e);
            }
        }

        private static int ReadInt(object target, string field, int fallback)
        {
            try
            {
                object value = AccessTools.Field(target.GetType(), field)?.GetValue(target);
                return value is int i ? i : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private static float ReadFloat(object target, string field, float fallback)
        {
            try
            {
                object value = AccessTools.Field(target.GetType(), field)?.GetValue(target);
                return value is float f ? f : fallback;
            }
            catch
            {
                return fallback;
            }
        }

        private void BuildDisplaySpace(List<CardInfo> cards)
        {
            displayRoot = new GameObject("OnionQOL_DeckDisplay");
            displayRoot.transform.position = new Vector3(0f, 5000f, 0f);

            int numRows = Mathf.Min(cards.Count / (maxCardsPerRow + 1) + 1, Mathf.Max(1, maxRows));
            int cardsPerRow = Mathf.Max(1, cards.Count / numRows);

            for (int i = 0; i < cards.Count; i++)
            {
                Vector4 pos = GetCardPositionAndRotation(i, cards.Count, numRows, cardsPerRow);
                GameObject cardGo = UnityEngine.Object.Instantiate(cardPrefab, displayRoot.transform);
                cardGo.name = "OnionQOL_Card_" + i;
                cardGo.transform.localPosition = new Vector3(pos.x, pos.y, pos.z);

                if (i == 0)
                {
                    Bounds flatBounds = GetCardBounds(cardGo);
                    if (flatBounds.size.x > 0.01f && flatBounds.size.y > 0.01f)
                    {
                        cardSize = new Vector2(flatBounds.size.x, flatBounds.size.y);
                    }
                }

                cardGo.transform.localRotation = Quaternion.Euler(90f, 0f, pos.w);

                SelectableCard card = cardGo.GetComponent<SelectableCard>();
                if (card != null)
                {
                    card.Initialize(cards[i], null, null, false, null);
                    card.SetParticlesEnabled(false);
                    card.SetInteractionEnabled(false);
                }

                // 重新启用印记图标的碰撞体，供窗口内右键查询百科使用
                foreach (AbilityIconInteractable icon in cardGo.GetComponentsInChildren<AbilityIconInteractable>(true))
                {
                    icon.SetEnabled(true);
                }

                SetLayerRecursive(cardGo, DisplayLayer);
                SetCardRenderQueue(cardGo, 3000 + i * 10);
                spawnedCards.Add(cardGo);
            }
        }

        private static void SetCardRenderQueue(GameObject cardGo, int queue)
        {
            try
            {
                foreach (Renderer renderer in cardGo.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material material in renderer.materials)
                    {
                        material.renderQueue = queue;
                    }
                }
            }
            catch (Exception e)
            {
                OnionQOLPlugin.Log?.LogWarning("Onion QOL: 设置卡牌渲染顺序失败: " + e);
            }
        }

        private static Bounds GetCardBounds(GameObject go)
        {
            Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
            bool has = false;
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!has)
                {
                    bounds = renderer.bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }
            return bounds;
        }

        /// <summary>
        /// 抄自 SelectableCardArray.GetCardPositionAndRotation。
        /// </summary>
        private static Vector4 GetCardPositionAndRotation(int cardIndex, int numCards, int numRows, int cardsPerRow)
        {
            int row = Mathf.Min(numRows - 1, Mathf.FloorToInt((float)cardIndex / (float)cardsPerRow));
            int cardsInRow = ((row == numRows - 1) ? (numCards - row * cardsPerRow) : cardsPerRow);
            float spacing = arrayWidth / (float)cardsInRow;
            float firstCenter = leftAnchor + spacing * 0.5f;
            float x = firstCenter + spacing * (float)(cardIndex % cardsInRow);
            float normalized = Mathf.Abs(x - firstCenter) / arrayWidth;
            normalized = normalized * 2f - 1f;
            float rot = normalized * -11f * curveFactor;
            float rowOffset = rowSpacing * (float)row;
            float baseZ = 0f - rowSpacing * (float)(numRows - 1) * 0.5f;
            float z = baseZ + rowOffset + 0.25f * (0f - Mathf.Abs(normalized)) * curveFactor;

            float y = 0.01f - 0.00015f * (float)(numCards - 1 - cardIndex);
            return new Vector4(x, y, z, rot);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform)
            {
                SetLayerRecursive(child.gameObject, layer);
            }
        }

        private void BuildRenderCamera()
        {
            GameObject camGo = new GameObject("OnionQOL_DeckCamera");
            camGo.transform.SetParent(displayRoot.transform);
            camGo.transform.localPosition = new Vector3(0f, 9.5f, -2.6f);
            camGo.transform.LookAt(displayRoot.transform.position, Vector3.up);

            renderCamera = camGo.AddComponent<Camera>();
            renderCamera.clearFlags = CameraClearFlags.SolidColor;
            renderCamera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            renderCamera.cullingMask = 1 << DisplayLayer;
            renderCamera.fieldOfView = 45f;
            renderCamera.nearClipPlane = 0.1f;
            renderCamera.farClipPlane = 100f;
            renderCamera.depth = -10;
            renderCamera.enabled = true;

            renderTexture = new RenderTexture(1920, 1080, 16, RenderTextureFormat.ARGB32);
            renderTexture.filterMode = FilterMode.Bilinear;
            renderCamera.targetTexture = renderTexture;

            AddDisplayLight();
        }

        private void AddDisplayLight()
        {
            try
            {
                GameObject lightGo = new GameObject("OnionQOL_DeckLight");
                lightGo.transform.SetParent(displayRoot.transform);
                lightGo.transform.localPosition = new Vector3(0f, 12f, 0f);
                Light light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 80f;
                light.intensity = 1.0f;
                light.color = new Color(1f, 0.9f, 0.78f);

                GameObject lightGo2 = new GameObject("OnionQOL_DeckLight2");
                lightGo2.transform.SetParent(displayRoot.transform);
                lightGo2.transform.localPosition = new Vector3(0f, 4f, 8f);
                Light light2 = lightGo2.AddComponent<Light>();
                light2.type = LightType.Point;
                light2.range = 50f;
                light2.intensity = 0.45f;
                light2.color = new Color(1f, 0.85f, 0.7f);
            }
            catch (Exception e)
            {
                OnionQOLPlugin.Log?.LogWarning("Onion QOL: 添加展示灯光失败: " + e);
            }
        }

        private void BuildUi(int cardCount)
        {
            uiRoot = new GameObject("OnionQOL_DeckUI", typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(uiRoot);

            Canvas canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = uiRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            uiRoot.AddComponent<GraphicRaycaster>();

            Font font = GetChineseFont();

            // 半透明背景
            GameObject bg = CreateUiObject("Background", uiRoot.transform);
            Image bgImage = bg.AddComponent<Image>();
            bgImage.color = new Color(0f, 0f, 0f, BackgroundAlpha);
            Stretch(bg.GetComponent<RectTransform>());

            // 标题
            string titleString = windowMode == DeckWindowMode.DrawPile
                ? "抽牌堆（" + cardCount + " 张）"
                : "本局牌组（" + cardCount + " 张）";
            Text title = CreateText("Title", uiRoot.transform, font, 40, titleString);
            RectTransform titleRt = title.rectTransform;
            titleRt.anchorMin = new Vector2(0.5f, 1f);
            titleRt.anchorMax = new Vector2(0.5f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -40f);
            titleRt.sizeDelta = new Vector2(1200f, 80f);

            // 卡面区域
            GameObject rawGo = CreateUiObject("DeckView", uiRoot.transform);
            RawImage raw = rawGo.AddComponent<RawImage>();
            raw.texture = renderTexture;
            raw.color = Color.white;
            RectTransform rawRt = rawGo.GetComponent<RectTransform>();
            rawRt.anchorMin = new Vector2(0.5f, 0.5f);
            rawRt.anchorMax = new Vector2(0.5f, 0.5f);
            rawRt.pivot = new Vector2(0.5f, 0.5f);
            rawRt.anchoredPosition = new Vector2(0f, 0f);
            rawRt.sizeDelta = new Vector2(1760f, 990f);
            rawRect = rawRt;

            // 关闭提示（按键从设置读取）
            Text hint = CreateText("Hint", uiRoot.transform, font, 30, "[" + CloseKey() + "] 关闭");
            hintText = hint;
            RectTransform hintRt = hint.rectTransform;
            hintRt.anchorMin = new Vector2(0.5f, 0f);
            hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.anchoredPosition = new Vector2(0f, 40f);
            hintRt.sizeDelta = new Vector2(600f, 60f);

            BuildEncyclopediaPanel(font);
        }

        private void BuildEncyclopediaPanel(Font font)
        {
            encyclopediaPanel = CreateUiObject("Encyclopedia", uiRoot.transform);
            RectTransform panelRt = encyclopediaPanel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.5f, 0.5f);
            panelRt.anchorMax = new Vector2(0.5f, 0.5f);
            panelRt.pivot = new Vector2(0.5f, 0.5f);
            panelRt.anchoredPosition = Vector2.zero;
            panelRt.sizeDelta = new Vector2(1000f, 640f);
            Image panelBg = encyclopediaPanel.AddComponent<Image>();
            panelBg.color = new Color(0.04f, 0.04f, 0.04f, 0.97f);

            encyclopediaTitle = CreateText("EncyclopediaTitle", encyclopediaPanel.transform, font, 36, string.Empty);
            RectTransform titleRt = encyclopediaTitle.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -28f);
            titleRt.sizeDelta = new Vector2(-60f, 70f);

            encyclopediaBody = CreateText("EncyclopediaBody", encyclopediaPanel.transform, font, 26, string.Empty);
            encyclopediaBody.alignment = TextAnchor.UpperLeft;
            encyclopediaBody.horizontalOverflow = HorizontalWrapMode.Wrap;
            encyclopediaBody.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform bodyRt = encyclopediaBody.rectTransform;
            bodyRt.anchorMin = new Vector2(0f, 0f);
            bodyRt.anchorMax = new Vector2(1f, 1f);
            bodyRt.offsetMin = new Vector2(50f, 90f);
            bodyRt.offsetMax = new Vector2(-50f, -110f);

            Text closeHint = CreateText("EncyclopediaHint", encyclopediaPanel.transform, font, 24, "[右键] 关闭");
            RectTransform closeRt = closeHint.rectTransform;
            closeRt.anchorMin = new Vector2(0.5f, 0f);
            closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0f, 24f);
            closeRt.sizeDelta = new Vector2(500f, 50f);

            encyclopediaPanel.SetActive(false);
        }

        private void UpdateEncyclopediaInput()
        {
            if (!Input.GetMouseButtonDown(1))
            {
                return;
            }
            if (encyclopediaPanel != null && encyclopediaPanel.activeSelf)
            {
                encyclopediaPanel.SetActive(false);
                return;
            }
            AbilityInfo info = GetHoveredAbilityInfo();
            if (info != null)
            {
                ShowEncyclopedia(info);
            }
        }

        private AbilityInfo GetHoveredAbilityInfo()
        {
            if (renderCamera == null || rawRect == null || renderTexture == null)
            {
                return null;
            }
            if (!RectTransformUtility.RectangleContainsScreenPoint(rawRect, Input.mousePosition, null))
            {
                return null;
            }
            Vector2 localPoint;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rawRect, Input.mousePosition, null, out localPoint))
            {
                return null;
            }
            float u = (localPoint.x / rawRect.rect.width) + 0.5f;
            float v = (localPoint.y / rawRect.rect.height) + 0.5f;
            Ray ray = renderCamera.ScreenPointToRay(new Vector3(u * renderTexture.width, v * renderTexture.height, 0f));
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 300f, 1 << DisplayLayer))
            {
                AbilityIconInteractable icon = hit.collider.GetComponentInParent<AbilityIconInteractable>();
                if (icon != null && icon.Ability != Ability.None)
                {
                    return AbilitiesUtil.GetInfo(icon.Ability);
                }
            }
            return null;
        }

        private void ShowEncyclopedia(AbilityInfo info)
        {
            if (encyclopediaPanel == null || encyclopediaTitle == null || encyclopediaBody == null)
            {
                return;
            }
            encyclopediaPanel.SetActive(true);
            encyclopediaTitle.text = Localization.Translate(info.rulebookName);
            encyclopediaBody.text = info.LocalizedRulebookDescription;
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

        private void LockPlayerInput()
        {
            try
            {
                ViewManager viewManager = Singleton<ViewManager>.Instance;
                if (viewManager != null && viewManager.Controller != null)
                {
                    cachedLockState = viewManager.Controller.LockState;
                    viewManager.Controller.LockState = ViewLockState.Locked;
                    restoredLockOnce = true;
                }
            }
            catch (Exception e)
            {
                OnionQOLPlugin.Log?.LogWarning("Onion QOL: 锁定输入失败: " + e);
            }
        }

        private void RestorePlayerInput()
        {
            try
            {
                if (!restoredLockOnce)
                {
                    return;
                }
                restoredLockOnce = false;
                ViewManager viewManager = Singleton<ViewManager>.Instance;
                if (viewManager != null && viewManager.Controller != null)
                {
                    viewManager.Controller.LockState = cachedLockState;
                }
            }
            catch (Exception e)
            {
                OnionQOLPlugin.Log?.LogWarning("Onion QOL: 恢复输入失败: " + e);
            }
        }

        private void HideCursor()
        {
            try
            {
                // 隐藏游戏 3D 光标，改用系统鼠标光标以便悬停卡面
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

        private void UpdateHover()
        {
            if (displayRoot == null || renderCamera == null || rawRect == null || renderTexture == null)
            {
                return;
            }

            GameObject found = null;
            if (RectTransformUtility.RectangleContainsScreenPoint(rawRect, Input.mousePosition, null))
            {
                Vector2 localPoint;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rawRect, Input.mousePosition, null, out localPoint))
                {
                    float u = (localPoint.x / rawRect.rect.width) + 0.5f;
                    float v = (localPoint.y / rawRect.rect.height) + 0.5f;
                    Ray ray = renderCamera.ScreenPointToRay(new Vector3(u * renderTexture.width, v * renderTexture.height, 0f));
                    RaycastHit hit;
                    if (Physics.Raycast(ray, out hit, 300f, 1 << DisplayLayer))
                    {
                        SelectableCard hitCard = hit.collider.GetComponentInParent<SelectableCard>();
                        if (hitCard != null)
                        {
                            found = hitCard.gameObject;
                        }
                    }

                    if (found == null)
                    {
                        // 兜底：按卡牌局部矩形判定（考虑卡牌旋转）
                        Plane plane = new Plane(Vector3.up, displayRoot.transform.position + Vector3.up * 0.02f);
                        float distance;
                        if (plane.Raycast(ray, out distance))
                        {
                            Vector3 worldPoint = ray.GetPoint(distance);
                            foreach (GameObject cardGo in spawnedCards)
                            {
                                if (cardGo == null)
                                {
                                    continue;
                                }
                                Vector3 local = cardGo.transform.InverseTransformPoint(worldPoint);
                                if (Mathf.Abs(local.x) <= cardSize.x * 0.5f && Mathf.Abs(local.y) <= cardSize.y * 0.5f)
                                {
                                    found = cardGo;
                                }
                            }
                        }
                    }
                }
            }

            if (found == hoveredCard)
            {
                return;
            }
            RestoreHovered();
            if (found != null)
            {
                hoveredCard = found;
                hoveredOriginalPos = found.transform.localPosition;
                hoveredOriginalRot = found.transform.localEulerAngles;
                hoveredOriginalScale = found.transform.localScale;
                found.transform.localPosition = hoveredOriginalPos + new Vector3(0f, 0.4f, 0f);
                found.transform.localScale = hoveredOriginalScale * 1.12f;
                SetCardRenderQueue(found, 9000);
            }
        }

        private void RestoreHovered()
        {
            if (hoveredCard != null)
            {
                hoveredCard.transform.localPosition = hoveredOriginalPos;
                hoveredCard.transform.localEulerAngles = hoveredOriginalRot;
                hoveredCard.transform.localScale = hoveredOriginalScale;
                int index = spawnedCards.IndexOf(hoveredCard);
                SetCardRenderQueue(hoveredCard, index >= 0 ? 3000 + index * 10 : 3000);
                hoveredCard = null;
            }
        }

        private void Teardown()
        {
            RestorePlayerInput();
            ShowCursor();
            hoveredCard = null;
            spawnedCards.Clear();

            if (renderCamera != null)
            {
                renderCamera.targetTexture = null;
            }
            if (renderTexture != null)
            {
                renderTexture.Release();
                UnityEngine.Object.Destroy(renderTexture);
                renderTexture = null;
            }
            if (displayRoot != null)
            {
                UnityEngine.Object.Destroy(displayRoot);
                displayRoot = null;
            }
            renderCamera = null;
            if (uiRoot != null)
            {
                UnityEngine.Object.Destroy(uiRoot);
                uiRoot = null;
            }
        }
    }
}
