using System;
using System.Collections.Generic;
using DiskCardGame;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace OnionQOL
{
    /// <summary>
    /// 选牌/奖励界面屏幕右下角的"跳过"按钮：
    /// - 普通选牌（CardSingleChoicesSequencer，含 Tribe/Cost 选完后拿牌阶段）
    /// - 矿工巨石（BoulderChoiceSequencer，选石头和拿造物两个阶段）
    /// - 买皮（BuyPeltsSequencer）
    /// - 道具满时的林鼠奖励（GainConsumablesSequencer）
    /// 点击后停止序列、清理卡牌/三叶草/箱子/奖励物，回到地图。
    /// </summary>
    public class SkipButton : MonoBehaviour
    {
        private static SkipButton current;
        private CardChoicesSequencer sequence;
        private GainConsumablesSequencer consumables;
        private BuyPeltsSequencer buyPelts;
        private GameObject uiRoot;
        private RectTransform buttonRect;

        public static void UpdateAttach()
        {
            try
            {
                if (!OnionQOLPlugin.EnableSkipButton.Value)
                {
                    if (current != null)
                    {
                        current.DestroyButton();
                    }
                    return;
                }

                GameFlowManager flow = Singleton<GameFlowManager>.Instance;
                GameState state = flow != null ? flow.CurrentGameState : GameState.Map;
                bool inSpecial = state == GameState.SpecialCardSequence;
                bool inBattle = state == GameState.CardBattle;
                NodeData currentData = GetCurrentNodeData();

                CardChoicesSequencer cardSequencer = null;
                if (inBattle || (inSpecial && IsCardChoiceRoom(currentData)))
                {
                    cardSequencer = FindActiveCardSequencer();
                }

                BoulderChoiceSequencer boulderSequencer = (cardSequencer == null && inSpecial && currentData is BoulderChoiceNodeData) ? FindActiveBoulderSequencer() : null;
                BuyPeltsSequencer buySequencer = (cardSequencer == null && boulderSequencer == null && inSpecial && currentData is BuyPeltsNodeData) ? FindActiveBuyPelts() : null;
                GainConsumablesSequencer consumablesSequencer = (cardSequencer == null && boulderSequencer == null && buySequencer == null
                    && inSpecial && currentData is GainConsumablesNodeData && IsConsumablesFull()) ? FindActiveGainSequencer() : null;

                if (cardSequencer == null && boulderSequencer == null && buySequencer == null && consumablesSequencer == null)
                {
                    if (current != null)
                    {
                        current.DestroyButton();
                    }
                    return;
                }

                if (current != null)
                {
                    bool same = (cardSequencer != null && current.sequence == cardSequencer)
                        || (boulderSequencer != null && current.sequence == boulderSequencer)
                        || (buySequencer != null && current.buyPelts == buySequencer)
                        || (consumablesSequencer != null && current.consumables == consumablesSequencer);
                    if (same)
                    {
                        return;
                    }
                    current.DestroyButton();
                }
                LogTrigger(cardSequencer, boulderSequencer, buySequencer, consumablesSequencer, flow, currentData);
                current = Create((CardChoicesSequencer)cardSequencer ?? boulderSequencer, buySequencer, consumablesSequencer, currentData);
            }
            catch (Exception e)
            {
                OnionQOLPlugin.Log?.LogError("Onion QOL: 跳过按钮更新异常: " + e);
            }
        }

        private static NodeData GetCurrentNodeData()
        {
            try
            {
                MapNodeManager manager = Singleton<MapNodeManager>.Instance;
                if (manager == null || manager.nodes == null || RunState.Run == null)
                {
                    return null;
                }
                MapNode node = manager.nodes.Find(n => n != null && n.nodeId == RunState.Run.currentNodeId);
                return node != null ? node.Data : null;
            }
            catch
            {
                return null;
            }
        }

        private static bool IsCardChoiceRoom(NodeData data)
        {
            return data is CardChoicesNodeData || data is CardBundleChoicesNodeData || data is ChooseRareCardNodeData
                || data is BossBattleNodeData || data is CardBattleNodeData || data is DeckTrialNodeData;
        }

        /// <summary>
        /// 特殊房间的序列协程是由 SpecialNodeHandler 启动的，序列对象自身的 StopAllCoroutines 停不掉，
        /// 因此这里统一停止 SpecialNodeHandler 上的协程，避免跳过之后序列继续执行（比如林鼠继续出现）。
        /// </summary>
        private static void StopAllSequenceCoroutines()
        {
            try
            {
                SpecialNodeHandler handler = Singleton<SpecialNodeHandler>.Instance;
                if (handler != null)
                {
                    handler.StopAllCoroutines();
                }
            }
            catch
            {
            }
        }

        private static bool IsConsumablesFull()
        {
            return RunState.Run != null && RunState.Run.consumables != null
                && RunState.Run.consumables.Count >= RunState.Run.MaxConsumables;
        }

        private static CardChoicesSequencer FindActiveCardSequencer()
        {
            CardSingleChoicesSequencer[] sequencers = UnityEngine.Object.FindObjectsOfType<CardSingleChoicesSequencer>();
            foreach (CardSingleChoicesSequencer sequencer in sequencers)
            {
                if (sequencer == null || !sequencer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                // 交易卡等不需要跳过的序列
                if (sequencer is TradeCardsSequencer)
                {
                    continue;
                }
                if (GetCards(sequencer).Count > 0)
                {
                    return sequencer;
                }
                if (GetChosenReward(sequencer) != null)
                {
                    return sequencer;
                }
            }

            // 试炼房间：DeckTrialSequencer 自己实现选牌流程（不在 CardSingleChoicesSequencer 体系里）
            DeckTrialSequencer[] trials = UnityEngine.Object.FindObjectsOfType<DeckTrialSequencer>();
            foreach (DeckTrialSequencer trial in trials)
            {
                if (trial == null || !trial.gameObject.activeInHierarchy)
                {
                    continue;
                }
                if (GetCards(trial).Count > 0)
                {
                    return trial;
                }
            }
            return null;
        }

        private static BoulderChoiceSequencer FindActiveBoulderSequencer()
        {
            BoulderChoiceSequencer[] sequencers = UnityEngine.Object.FindObjectsOfType<BoulderChoiceSequencer>();
            foreach (BoulderChoiceSequencer sequencer in sequencers)
            {
                if (sequencer == null || !sequencer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                // 巨石房间进行中：候选卡里存在"石头"卡（选石头阶段 / 拿造物阶段都还有石头卡）
                List<SelectableCard> cards = GetCards(sequencer);
                if (cards.Exists(c => c != null && c.Info != null && c.Info.name == "Boulder"))
                {
                    return sequencer;
                }
            }
            return null;
        }

        private static BuyPeltsSequencer FindActiveBuyPelts()
        {
            BuyPeltsSequencer[] sequencers = UnityEngine.Object.FindObjectsOfType<BuyPeltsSequencer>();
            foreach (BuyPeltsSequencer sequencer in sequencers)
            {
                if (sequencer == null || !sequencer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                // 买皮进行中：陷阱被激活（开始时 SetActive(true)，结束时隐藏）
                GameObject trap = AccessTools.Field(typeof(BuyPeltsSequencer), "trap")?.GetValue(sequencer) as GameObject;
                if (trap != null && trap.activeSelf)
                {
                    return sequencer;
                }
            }
            return null;
        }

        private static GainConsumablesSequencer FindActiveGainSequencer()
        {
            GainConsumablesSequencer[] sequencers = UnityEngine.Object.FindObjectsOfType<GainConsumablesSequencer>();
            foreach (GainConsumablesSequencer sequencer in sequencers)
            {
                if (sequencer == null || !sequencer.gameObject.activeInHierarchy)
                {
                    continue;
                }
                // 只在场上确实出现了林鼠 3D 模型和林鼠卡牌时才显示按钮
                GameObject rat = AccessTools.Field(typeof(GainConsumablesSequencer), "rat")?.GetValue(sequencer) as GameObject;
                SelectableCard ratCard = AccessTools.Field(typeof(GainConsumablesSequencer), "ratCard")?.GetValue(sequencer) as SelectableCard;
                if (rat != null && rat.activeInHierarchy && ratCard != null && ratCard.gameObject.activeInHierarchy)
                {
                    return sequencer;
                }
            }
            return null;
        }

        private static List<SelectableCard> GetCards(CardChoicesSequencer sequencer)
        {
            try
            {
                List<SelectableCard> cards = AccessTools.Field(typeof(CardChoicesSequencer), "selectableCards")?.GetValue(sequencer) as List<SelectableCard>;
                return cards ?? new List<SelectableCard>();
            }
            catch
            {
                return new List<SelectableCard>();
            }
        }

        private static SelectableCard GetChosenReward(CardChoicesSequencer sequencer)
        {
            try
            {
                if (!(sequencer is CardSingleChoicesSequencer))
                {
                    return null;
                }
                return AccessTools.Field(typeof(CardSingleChoicesSequencer), "chosenReward")?.GetValue(sequencer) as SelectableCard;
            }
            catch
            {
                return null;
            }
        }

        private static SkipButton Create(CardChoicesSequencer cardSequencer, BuyPeltsSequencer buySequencer, GainConsumablesSequencer consumablesSequencer, NodeData currentData)
        {
            GameObject root = new GameObject("OnionQOL_SkipButton", typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(root);

            Canvas canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;

            CanvasScaler scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            GameObject button = new GameObject("Skip", typeof(RectTransform));
            button.transform.SetParent(root.transform, false);
            Image image = button.AddComponent<Image>();
            image.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = new Vector2(-40f, 40f);
            rect.sizeDelta = new Vector2(340f, 96f);

            Font font = GetChineseFont();
            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(button.transform, false);
            Text label = labelGo.AddComponent<Text>();
            label.font = font;
            label.fontSize = 34;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = "跳过（不拿牌）";
            label.raycastTarget = false;
            RectTransform labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            SkipButton component = root.AddComponent<SkipButton>();
            component.sequence = cardSequencer;
            component.buyPelts = buySequencer;
            component.consumables = consumablesSequencer;
            component.uiRoot = root;
            component.buttonRect = rect;

            try
            {
                InteractionCursor cursor = Singleton<InteractionCursor>.Instance;
                if (cursor != null)
                {
                    cursor.OSCursorShown = true;
                }
                Cursor.visible = true;
            }
            catch
            {
            }

            OnionQOLPlugin.Log?.LogInfo("Onion QOL: skip button shown target=" + DescribeTarget(cardSequencer, buySequencer, consumablesSequencer)
                + " room=" + (currentData != null ? currentData.GetType().Name : "null"));
            return component;
        }

        private static void LogTrigger(CardChoicesSequencer cardSequencer, BoulderChoiceSequencer boulderSequencer,
            BuyPeltsSequencer buySequencer, GainConsumablesSequencer consumablesSequencer, GameFlowManager flow, NodeData currentData)
        {
            try
            {
                string state = flow != null ? flow.CurrentGameState.ToString() : "null";
                string room = currentData != null ? currentData.GetType().Name : "null";
                if (cardSequencer != null)
                {
                    OnionQOLPlugin.Log?.LogInfo("Onion QOL: skip trigger=cardchoice"
                        + " object=" + cardSequencer.name
                        + " type=" + cardSequencer.GetType().Name
                        + " cards=" + GetCards(cardSequencer).Count
                        + " chosen=" + (GetChosenReward(cardSequencer) != null)
                        + " room=" + room
                        + " state=" + state);
                }
                else if (boulderSequencer != null)
                {
                    CardPile pile = AccessTools.Field(typeof(CardChoicesSequencer), "deckPile")?.GetValue(boulderSequencer) as CardPile;
                    OnionQOLPlugin.Log?.LogInfo("Onion QOL: skip trigger=boulder"
                        + " object=" + boulderSequencer.name
                        + " pileActive=" + (pile != null && pile.gameObject.activeSelf)
                        + " room=" + room
                        + " state=" + state);
                }
                else if (buySequencer != null)
                {
                    GameObject trap = AccessTools.Field(typeof(BuyPeltsSequencer), "trap")?.GetValue(buySequencer) as GameObject;
                    OnionQOLPlugin.Log?.LogInfo("Onion QOL: skip trigger=buypelts"
                        + " object=" + buySequencer.name
                        + " trapActive=" + (trap != null && trap.activeSelf)
                        + " room=" + room
                        + " state=" + state);
                }
                else if (consumablesSequencer != null)
                {
                    GameObject rat = AccessTools.Field(typeof(GainConsumablesSequencer), "rat")?.GetValue(consumablesSequencer) as GameObject;
                    OnionQOLPlugin.Log?.LogInfo("Onion QOL: skip trigger=consumables"
                        + " object=" + consumablesSequencer.name
                        + " ratActive=" + (rat != null && rat.activeSelf)
                        + " room=" + room
                        + " state=" + state);
                }
            }
            catch
            {
            }
        }

        private static string DescribeTarget(CardChoicesSequencer cardSequencer, BuyPeltsSequencer buySequencer, GainConsumablesSequencer consumablesSequencer)
        {
            if (cardSequencer is BoulderChoiceSequencer)
            {
                return "boulder";
            }
            if (cardSequencer is DeckTrialSequencer)
            {
                return "decktrial";
            }
            if (cardSequencer != null)
            {
                return "cardchoice";
            }
            if (buySequencer != null)
            {
                return "buypelts";
            }
            return "consumables";
        }

        private void Update()
        {
            try
            {
                if (buttonRect == null)
                {
                    return;
                }
                if (!Input.GetMouseButtonDown(0))
                {
                    return;
                }
                if (RectTransformUtility.RectangleContainsScreenPoint(buttonRect, Input.mousePosition, null))
                {
                    Skip();
                }
            }
            catch (Exception e)
            {
                OnionQOLPlugin.Log?.LogError("Onion QOL: skip button error: " + e);
            }
        }

        private void Skip()
        {
            // 先销毁按钮，避免跳过过程中被重复点击
            DestroyButton();
            try
            {
                if (buyPelts != null)
                {
                    SkipBuyPelts();
                }
                else if (consumables != null)
                {
                    SkipConsumables();
                }
                else if (sequence is BoulderChoiceSequencer boulder)
                {
                    SkipBoulder(boulder);
                }
                else if (sequence != null)
                {
                    SkipCardChoice();
                }
            }
            catch (Exception e)
            {
                OnionQOLPlugin.Log?.LogError("Onion QOL: skip failed: " + e);
            }
        }

        private void SkipCardChoice()
        {
            CardChoicesSequencer seq = sequence;
            StopAllSequenceCoroutines();
            seq.StopAllCoroutines();

            try
            {
                System.Reflection.MethodInfo cleanUpCards = AccessTools.Method(typeof(CardSingleChoicesSequencer), "CleanUpCards");
                cleanUpCards?.Invoke(seq, new object[] { true });
            }
            catch
            {
            }

            DestroyAllSelectableCards();

            foreach (GameObject pack in UnityEngine.Object.FindObjectsOfType<GameObject>())
            {
                if (pack == null)
                {
                    continue;
                }
                string packName = pack.name.ToLowerInvariant();
                if (packName.Contains("cardpack") || packName.Contains("rarecardbox"))
                {
                    // 这些是场景里复用的对象，只能隐藏不能销毁，否则下一次 Boss 奖励会没有箱子/卡牌
                    pack.SetActive(false);
                }
            }

            CleanUpClover();

            try
            {
                CardPile pile = AccessTools.Field(typeof(CardChoicesSequencer), "deckPile")?.GetValue(seq) as CardPile;
                if (pile != null)
                {
                    if (pile.gameObject.activeInHierarchy)
                    {
                        pile.StartCoroutine(pile.DestroyCards());
                    }
                    if (seq is DeckTrialSequencer)
                    {
                        pile.gameObject.SetActive(false);
                    }
                }
            }
            catch
            {
            }

            // 清空候选卡列表，避免序列的 OnViewChanged 回调访问已销毁的卡（空引用，导致无法回地图）
            GetCards(seq).Clear();

            if (seq is DeckTrialSequencer trial)
            {
                CleanUpDeckTrial(trial);
            }

            ReturnToMap();
            OnionQOLPlugin.Log?.LogInfo("Onion QOL: skipped card choice");
        }

        /// <summary>
        /// 试炼房间跳过时补上正常结束流程的现场清理（洞穴入口/灯光/莱西状态/视图）。
        /// </summary>
        private static void CleanUpDeckTrial(DeckTrialSequencer trial)
        {
            try
            {
                ViewManager viewManager = Singleton<ViewManager>.Instance;
                if (viewManager != null)
                {
                    viewManager.SwitchToView(View.Default);
                }
            }
            catch
            {
            }

            try
            {
                OpponentAnimationController opponent = Singleton<OpponentAnimationController>.Instance;
                if (opponent != null)
                {
                    opponent.ResetHeadOffset(false);
                }
            }
            catch
            {
            }

            try
            {
                LeshyAnimationController leshy = LeshyAnimationController.Instance;
                if (leshy != null)
                {
                    leshy.ResetEyesTexture();
                }
            }
            catch
            {
            }

            try
            {
                Animator cave = AccessTools.Field(typeof(DeckTrialSequencer), "caveEntranceAnim")?.GetValue(trial) as Animator;
                if (cave != null)
                {
                    cave.Play("exit", 0, 0f);
                    cave.gameObject.SetActive(false);
                }
            }
            catch
            {
            }

            try
            {
                Light caveLight = AccessTools.Field(typeof(DeckTrialSequencer), "caveLight")?.GetValue(trial) as Light;
                if (caveLight != null)
                {
                    caveLight.gameObject.SetActive(false);
                }
            }
            catch
            {
            }
        }

        private void SkipBoulder(BoulderChoiceSequencer seq)
        {
            StopAllSequenceCoroutines();
            seq.StopAllCoroutines();
            DestroyAllSelectableCards();
            GetCards(seq).Clear();

            try
            {
                CardPile pile = AccessTools.Field(typeof(CardChoicesSequencer), "deckPile")?.GetValue(seq) as CardPile;
                if (pile != null)
                {
                    if (pile.gameObject.activeInHierarchy)
                    {
                        pile.StartCoroutine(pile.DestroyCards());
                    }
                    pile.gameObject.SetActive(false);
                }
            }
            catch
            {
            }

            try
            {
                LeshyAnimationController leshy = LeshyAnimationController.Instance;
                if (leshy != null)
                {
                    leshy.RightArm.SetTrigger("prospector_hide");
                    leshy.ClearHeldObjects();
                    leshy.StartCoroutine(leshy.TakeOffMask());
                }
            }
            catch
            {
            }

            ReturnToMap();
            OnionQOLPlugin.Log?.LogInfo("Onion QOL: skipped boulder");
        }

        private void SkipBuyPelts()
        {
            BuyPeltsSequencer seq = buyPelts;
            StopAllSequenceCoroutines();
            seq.StopAllCoroutines();
            DestroyAllSelectableCards();

            HideObject(AccessTools.Field(typeof(BuyPeltsSequencer), "trap")?.GetValue(seq) as GameObject);
            HideObject(AccessTools.Field(typeof(BuyPeltsSequencer), "knife")?.GetValue(seq) as GameObject);
            HideObject(AccessTools.Field(typeof(BuyPeltsSequencer), "weightsBlocker")?.GetValue(seq) as GameObject);

            try
            {
                CardPile pile = AccessTools.Field(typeof(BuyPeltsSequencer), "deckPile")?.GetValue(seq) as CardPile;
                if (pile != null && pile.gameObject.activeInHierarchy)
                {
                    pile.StartCoroutine(pile.DestroyCards());
                }
            }
            catch
            {
            }

            try
            {
                LeshyAnimationController leshy = LeshyAnimationController.Instance;
                if (leshy != null)
                {
                    leshy.ClearHeldObjects();
                    leshy.StartCoroutine(leshy.TakeOffMask());
                }
            }
            catch
            {
            }

            ReturnToMap();
            OnionQOLPlugin.Log?.LogInfo("Onion QOL: skipped buypelts");
        }

        private static void HideObject(GameObject go)
        {
            if (go != null)
            {
                go.SetActive(false);
            }
        }

        private void SkipConsumables()
        {
            GainConsumablesSequencer seq = consumables;
            StopAllSequenceCoroutines();
            seq.StopAllCoroutines();

            GameObject rat = AccessTools.Field(typeof(GainConsumablesSequencer), "rat")?.GetValue(seq) as GameObject;
            if (rat != null)
            {
                rat.SetActive(false);
            }
            // 注意：不要禁用 ratCard。游戏下一次进入道具房时靠 rat.SetActive(true) 让整组林鼠显形，
            // 如果这里把林鼠卡牌本身 SetActive(false)，第二次进入时卡牌将无法恢复显示。
            GameObject backpack = AccessTools.Field(typeof(GainConsumablesSequencer), "backpack")?.GetValue(seq) as GameObject;
            if (backpack != null)
            {
                backpack.SetActive(false);
            }

            DestroyAllSelectableCards();
            ReturnToMap();
            OnionQOLPlugin.Log?.LogInfo("Onion QOL: skipped consumables");
        }

        private static void DestroyAllSelectableCards()
        {
            foreach (SelectableCard card in UnityEngine.Object.FindObjectsOfType<SelectableCard>())
            {
                if (card != null)
                {
                    card.SetInteractionEnabled(false);
                    UnityEngine.Object.Destroy(card.gameObject);
                }
            }
        }

        private static void CleanUpClover()
        {
            foreach (GameObject go in UnityEngine.Object.FindObjectsOfType<GameObject>())
            {
                if (go == null)
                {
                    continue;
                }
                string name = go.name.ToLowerInvariant();
                if (name.Contains("clover") || name.Contains("reroll"))
                {
                    // 场景复用对象，只隐藏不销毁，避免后续房间缺少三叶草
                    go.SetActive(false);
                }
            }
        }

        private static void ReturnToMap()
        {
            ViewManager viewManager = Singleton<ViewManager>.Instance;
            if (viewManager != null && viewManager.Controller != null)
            {
                viewManager.Controller.LockState = ViewLockState.Unlocked;
            }
            if (Singleton<GameFlowManager>.Instance != null)
            {
                Singleton<GameFlowManager>.Instance.TransitionToGameState(GameState.Map);
            }
        }

        private void DestroyButton()
        {
            if (current == this)
            {
                current = null;
            }
            if (uiRoot != null)
            {
                UnityEngine.Object.Destroy(uiRoot);
                uiRoot = null;
            }
            try
            {
                InteractionCursor cursor = Singleton<InteractionCursor>.Instance;
                if (cursor != null)
                {
                    cursor.OSCursorShown = false;
                }
                Cursor.visible = false;
            }
            catch
            {
            }
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
