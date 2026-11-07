import os
import json
import shutil
import zipfile
from PIL import Image, ImageDraw, ImageFont

src = r"C:\Users\a1881\AppData\Roaming\r2modmanPlus-local\dev\OnionQOL"
dll = os.path.join(src, "bin", "Release", "OnionQOL.dll")
desktop = r"C:\Users\a1881\Desktop"

assert os.path.exists(dll), "dll not found: " + dll

manifest = {
    "name": "Onion_QOL",
    "version_number": "0.1.0",
    "website_url": "https://github.com/13764946875/Onion-QOL-for-Inscryption",
    "description": "Inscryption 凯西模组实用工具：按 Q 查看本局牌组、按 E 查看抽牌堆、按 F1 打开设置；右键地图房间让莱西说明房间作用；选牌/奖励房间提供跳过按钮。",
    "dependencies": [
        "BepInEx-BepInExPack_Inscryption-5.4.2305",
        "BepInEx-MonoMod_Loader_Inscryption-1.0.0",
        "API_dev-API-2.24.1",
    ],
}

readme = """# Onion QOL for Inscryption

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
"""

changelog = """# 0.1.0

- 初始版本
- Q 查看本局牌组（3D 卡面、悬停放大、右键印记百科）
- E 查看抽牌堆（仅战斗中）
- 设置面板（默认 F1，可修改按键）：牌组/抽牌堆开关与按键绑定、按键冲突检测
- 跳过按钮开关（可在设置面板中关闭）
- 右键地图房间让莱西说明房间作用（可配置文本）
- 选牌/奖励房间跳过按钮（普通选牌、卡包、稀有卡、Boss 奖励、巨石、买皮、道具满林鼠）
"""

license_text = """MIT License

Copyright (c) 2026 13764946875

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
"""

def write(path, text):
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print("wrote", path)

write(os.path.join(src, "manifest.json"), json.dumps(manifest, ensure_ascii=False, indent=2) + "\n")
write(os.path.join(src, "README.md"), readme)
write(os.path.join(src, "CHANGELOG.md"), changelog)
write(os.path.join(src, "LICENSE"), license_text)

# icon 256x256
icon_path = os.path.join(src, "icon.png")
img = Image.new("RGBA", (256, 256), (18, 30, 18, 255))
draw = ImageDraw.Draw(img)
# 绿色洋葱形：底部圆 + 顶部尖
draw.ellipse((40, 70, 216, 236), fill=(96, 150, 72, 255), outline=(210, 235, 190, 255), width=6)
draw.polygon([(128, 24), (86, 96), (170, 96)], fill=(96, 150, 72, 255), outline=(210, 235, 190, 255))
draw.line([(128, 96), (128, 226)], fill=(60, 105, 45, 255), width=6)
draw.line([(76, 130), (112, 190)], fill=(60, 105, 45, 255), width=4)
draw.line([(180, 130), (144, 190)], fill=(60, 105, 45, 255), width=4)

font = None
for candidate in [r"C:\Windows\Fonts\arialbd.ttf", r"C:\Windows\Fonts\arial.ttf"]:
    if os.path.exists(candidate):
        try:
            font = ImageFont.truetype(candidate, 44)
            break
        except Exception:
            pass
if font is None:
    font = ImageFont.load_default()

draw.text((128, 168), "QOL", font=font, fill=(255, 255, 240, 255), anchor="mm")
img.save(icon_path)
print("wrote", icon_path)

# 打包 Thunderstore zip 到桌面
zip_path = os.path.join(desktop, "Onion_QOL-0.1.0.zip")
if os.path.exists(zip_path):
    os.remove(zip_path)

with zipfile.ZipFile(zip_path, "w", zipfile.ZIP_DEFLATED) as z:
    z.write(dll, "BepInEx/plugins/OnionQOL-Onion_QOL/OnionQOL.dll")
    z.write(os.path.join(src, "manifest.json"), "manifest.json")
    z.write(icon_path, "icon.png")
    z.write(os.path.join(src, "README.md"), "README.md")
    z.write(os.path.join(src, "CHANGELOG.md"), "CHANGELOG.md")
    z.write(os.path.join(src, "LICENSE"), "LICENSE")

print("zip:", zip_path)
print("dll size:", os.path.getsize(dll))
