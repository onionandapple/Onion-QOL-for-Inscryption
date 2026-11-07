using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using DiskCardGame;
using UnityEngine;

namespace OnionQOL
{
    /// <summary>
    /// 右键地图房间 → 莱西说出房间作用。
    /// 房间含义读取自 BepInEx\config\Onion QOL\room_meanings.txt（首次自动生成）。
    /// </summary>
    public static class RoomHint
    {
        private static readonly Dictionary<string, string> Meanings = new Dictionary<string, string>();
        private static string defaultMeaning = "……";
        private static bool initialized;

        public static void Initialize()
        {
            if (initialized)
            {
                return;
            }
            initialized = true;
            try
            {
                string dir = Path.Combine(Paths.ConfigPath, "Onion QOL");
                Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, "room_meanings.txt");
                if (!File.Exists(path))
                {
                    File.WriteAllText(path, DefaultConfig, new System.Text.UTF8Encoding(false));
                }
                Load(path);
                OnionQOLPlugin.Log?.LogInfo("Onion QOL: 房间含义配置已加载（" + Meanings.Count + " 条）");
            }
            catch (Exception e)
            {
                OnionQOLPlugin.Log?.LogError("Onion QOL: 房间含义配置加载失败: " + e);
            }
        }

        private static void Load(string path)
        {
            Meanings.Clear();
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    continue;
                }
                int idx = line.IndexOf('=');
                if (idx <= 0)
                {
                    continue;
                }
                string key = line.Substring(0, idx).Trim();
                string value = line.Substring(idx + 1).Trim();
                if (key == "_default")
                {
                    defaultMeaning = value;
                }
                else
                {
                    Meanings[key] = value;
                }
            }
        }

        public static void Update()
        {
            if (SettingsWindow.Instance != null || DeckWindow.Instance != null)
            {
                return;
            }
            if (Singleton<MapNodeManager>.Instance == null)
            {
                return;
            }
            // 只在地图摊在桌上时生效（排除造图腾、地图拿在手里等其它状态）
            ViewManager viewManager = Singleton<ViewManager>.Instance;
            if (viewManager == null)
            {
                return;
            }
            View view = viewManager.CurrentView;
            if (view != View.MapDefault && view != View.MapArial && view != View.MapDeckReview)
            {
                return;
            }
            if (viewManager.Controller != null && viewManager.Controller.LockState == ViewLockState.Locked)
            {
                return;
            }
            if (!Input.GetMouseButtonDown(1))
            {
                return;
            }

            MapNode node = FindNodeUnderMouse();
            if (node == null || node.Data == null)
            {
                return;
            }

            Speak(GetMeaning(node.Data));
        }

        /// <summary>
        /// 不依赖碰撞体（原版只有可进入的房间才启用 collider），
        /// 改为在屏幕空间找鼠标附近的节点。
        /// </summary>
        private static MapNode FindNodeUnderMouse()
        {
            MapNodeManager manager = Singleton<MapNodeManager>.Instance;
            if (manager == null || manager.nodes == null)
            {
                return null;
            }
            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = UnityEngine.Object.FindObjectOfType<Camera>();
            }
            if (cam == null)
            {
                return null;
            }

            Vector2 mouse = Input.mousePosition;
            MapNode best = null;
            float bestDistance = 110f;

            foreach (MapNode node in manager.nodes)
            {
                if (node == null)
                {
                    continue;
                }
                Vector3 screen = cam.WorldToScreenPoint(node.transform.position);
                if (screen.z < 0f)
                {
                    continue;
                }
                float distance = Vector2.Distance(mouse, new Vector2(screen.x, screen.y));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = node;
                }
            }
            return best;
        }

        private static string GetMeaning(NodeData data)
        {
            string key = data.GetType().Name;
            if (data is CardChoicesNodeData choices)
            {
                key = key + "." + choices.choicesType;
            }
            else if (data is CardStatBoostNodeData)
            {
                bool dead = RunState.Run != null && RunState.Run.survivorsDead;
                key = key + (dead ? ".Dead" : ".Alive");
            }
            if (Meanings.TryGetValue(key, out string text) && !string.IsNullOrEmpty(text))
            {
                return text;
            }

            // 回退到不带状态后缀的通用键
            string baseKey = data.GetType().Name;
            if (baseKey != key && Meanings.TryGetValue(baseKey, out string fallback) && !string.IsNullOrEmpty(fallback))
            {
                return fallback;
            }
            return defaultMeaning;
        }

        private static void Speak(string meaning)
        {
            try
            {
                TextDisplayer displayer = Singleton<TextDisplayer>.Instance;
                if (displayer == null)
                {
                    return;
                }
                if (displayer.Displaying)
                {
                    displayer.Clear();
                }
                displayer.StartCoroutine(displayer.ShowUntilInput(
                    meaning,
                    -0.65f,
                    0.4f,
                    Emotion.Neutral,
                    TextDisplayer.LetterAnimation.Jitter,
                    DialogueEvent.Speaker.Leshy));
            }
            catch (Exception e)
            {
                OnionQOLPlugin.Log?.LogError("Onion QOL: 莱西说话失败: " + e);
            }
        }

        private const string DefaultConfig =
@"# Onion QOL —— 地图房间含义配置
# 右键地图上的任意房间，莱西会直接说出该房间的作用。
# 格式：节点类型=莱西要说的话
# 修改后重启游戏生效；未列出的房间使用 _default 的文本。
# 卡牌选择类的节点类型带 .Random/.Cost/.Tribe/.Deathcard 后缀。

# —— 图标：黑色方框里的白色问号（右下角蹲坐小人）→ 选一张牌加入
CardChoicesNodeData.Random=你可以从三张随机造物中选择一张加入牌组

# —— 图标：黑色方框问号旁边的侧脸 → 选牌加入
CardChoicesNodeData.Cost=你可以获取一张特定花费的随机造物

# —— 图标：带角的黑色骷髅脸 → 选族牌加入
CardChoicesNodeData.Tribe=你可以获取一张指定族群的随机造物

# —— 图标：黑色方框里的白色问号（死亡卡）→ 死亡卡选择
CardChoicesNodeData.Deathcard=你可以获取一张死亡卡

# —— 图标：黑色短柄锤子 → 加强一张牌（按幸存者状态分支）
CardStatBoostNodeData.Alive=温暖的篝火……你可以强化一个造物的属性
CardStatBoostNodeData.Dead=温暖的篝火……十分安静

# —— 图标：黑色背包/布袋 → 增加一个道具
GainConsumablesNodeData=你可以增加一个道具

# —— 图标：黑色剪刀 → 删一张牌
CardRemoveNodeData=你可以删一张牌

# —— 图标：带符号的黑色图腾柱/石柱 → 附魔（献祭转移印记）
CardMergeNodeData=你需要献祭一个造物来把它的印记转移到另一个造物上

# —— 图标：黑色画架/画笔（画画房间）→ 复制一张牌
CopyCardNodeData=我不建议你去那里

# —— 图标：两朵黑色蘑菇（一大一小）→ 2 张合 1
DuplicateMergeNodeData=你可以把两张造物合成一张

# —— 图标：黑色骷髅头 → 普通敌人
CardBattleNodeData=这里是普通敌人

# —— 图标：带翅膀的黑色骷髅（蝙蝠状）→ 带 buff 的敌人
TotemBattleNodeData=这是自带雕像的敌人

# —— 图标：王冠骷髅 → Boss 战
BossBattleNodeData=这里是 Boss 战

# —— 图标：带问号的卡片立在台座上 → 皮毛换牌
TradePeltsNodeData=你可以用皮毛换取一张造物

# —— 图标：黑色月牙/镰刀 → 金牙换毛皮
BuyPeltsNodeData=你可以用金牙换取毛皮

# —— 图标：黑色拱形/门形 → 通过得牌
DeckTrialNodeData=如果你通过了这个试炼，就可以拿取一张带额外印记的造物

# —— 图标：黑色小人举着工具/锤子 → 组装好给自己加 buff
BuildTotemNodeData=你可以组装好给自己加 buff

# —— 图标：黑色小人举着镐子凿地 → 矿工凿巨石
BoulderChoiceNodeData=矿工会给你三块巨石，你选一块，他会凿开它，把一个带印记的造物交给你

# —— 图标：黑色小屋 → 小屋
CabinNodeData=很好，你来到了这里

# —— 兜底：未匹配的房间
_default=……
";
    }
}
