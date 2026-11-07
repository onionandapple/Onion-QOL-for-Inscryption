# Onion QOL for Inscryption

Inscryption 凯西模组（Kaycee's Mod）实用工具模组。

## 功能

- **Q：查看本局牌组** —— 凯西模组地图上按 Q，弹出 3D 牌组窗口（原版地图看牌组同款卡面），支持鼠标悬停放大、右键查看印记百科
- **E：查看抽牌堆** —— 战斗中按 E，查看抽牌堆剩余卡牌
- **F1：设置窗口** —— 开关牌组查看/抽牌堆查看、重新绑定按键（两个功能的按键互斥）
- **右键地图房间** —— 莱西直接说出该房间的作用（房间含义可在配置文件中修改）
- **跳过按钮** —— 选牌/卡包/稀有卡/巨石/Boss 奖励/买皮/道具满给林鼠等房间，屏幕右下角提供"跳过（不拿牌）"按钮

## 配置文件

- `BepInEx/config/Onion QOL/room_meanings.txt` —— 房间含义文本（UTF-8，`节点类型=莱西要说的话`，`#` 为注释，未匹配使用 `_default`）
- `BepInEx/config/onion.qol.cfg` —— 功能开关与按键

## 安装

- 通过 Thunderstore / r2modman 安装
- 或手动把 `BepInEx/plugins/OnionQOL-Onion_QOL/OnionQOL.dll` 放入游戏 `BepInEx/plugins/` 目录

## 依赖

- BepInExPack_Inscryption 5.4.2305
- BepInEx_MonoMod_Loader_Inscryption 1.0.0
- InscryptionAPI 2.24.1
