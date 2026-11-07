from PIL import Image, ImageDraw
import os

out_dir = r"C:\Users\a1881\AppData\Roaming\r2modmanPlus-local\dev\OnionQOL\dist"
os.makedirs(out_dir, exist_ok=True)

img = Image.new("RGBA", (256, 256), (24, 24, 28, 255))
d = ImageDraw.Draw(img)

# 洋葱身体
d.ellipse([54, 78, 202, 226], fill=(150, 108, 176, 255), outline=(232, 214, 240, 255), width=5)
# 顶部茎
d.rectangle([120, 36, 136, 82], fill=(112, 168, 92, 255))
d.line([(104, 56), (128, 42), (152, 58)], fill=(112, 168, 92, 255), width=9)
# 高光
d.ellipse([92, 112, 130, 150], fill=(190, 160, 210, 255))
# 文字 Q
d.text((112, 150), "Q", fill=(255, 255, 255, 255))

img.save(os.path.join(out_dir, "icon.png"))
print("saved:", os.path.join(out_dir, "icon.png"))
