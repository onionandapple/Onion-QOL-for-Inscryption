import io

path = r"C:\Users\a1881\AppData\Roaming\r2modmanPlus-local\Inscryption\profiles\Default\BepInEx\config\Onion QOL\room_meanings.txt"

with open(path, "r", encoding="utf-8") as f:
    content = f.read()

replacements = [
    (
        "# —— 图标：黑色短柄锤子 → 加强一张牌\nCardStatBoostNodeData=你可以加强一张牌\n",
        "# —— 图标：黑色短柄锤子 → 加强一张牌（按幸存者状态分支）\nCardStatBoostNodeData.Alive=温暖的篝火……你可以强化一个造物的属性\nCardStatBoostNodeData.Dead=温暖的篝火……十分安静\n",
    ),
    (
        "# —— 图标：两朵黑色蘑菇（一大一小）→ 2 张合 1\nCardMergeNodeData=你需要献祭一个造物来把它的印记转移到另一个造物上\n",
        "# —— 图标：带符号的黑色图腾柱/石柱 → 附魔（献祭转移印记）\nCardMergeNodeData=你需要献祭一个造物来把它的印记转移到另一个造物上\n",
    ),
    (
        "# —— 图标：带符号的黑色图腾柱/石柱 → 附魔\nCopyCardNodeData=你可以把两张造物合成一张\n",
        "# —— 图标：黑色画架/画笔（画画房间）→ 复制一张牌\nCopyCardNodeData=我不建议你去那里\n",
    ),
    (
        "# —— 图标：堆叠的卡牌 → 复制合并\nDuplicateMergeNodeData=你可以复制一张造物并合并\n",
        "# —— 图标：两朵黑色蘑菇（一大一小）→ 2 张合 1\nDuplicateMergeNodeData=你可以把两张造物合成一张\n",
    ),
    (
        "# —— 兜底：未匹配的房间（如小屋）\n_default=……\n",
        "# —— 图标：黑色小屋 → 小屋\nCabinNodeData=很好，你来到了这里\n\n# —— 兜底：未匹配的房间\n_default=……\n",
    ),
]

for old, new in replacements:
    if old in content:
        content = content.replace(old, new)
        print("replaced ok")
    else:
        print("NOT FOUND:", old.splitlines()[1] if len(old.splitlines()) > 1 else old)

with open(path, "w", encoding="utf-8", newline="\n") as f:
    f.write(content)

print("done")
