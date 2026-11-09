import os
import zipfile

folder = r"C:\Users\a1881\AppData\Roaming\r2modmanPlus-local\dev\OnionQOL"
dll = r"C:\Users\a1881\AppData\Roaming\r2modmanPlus-local\dev\OnionQOL\bin\Release\OnionQOL.dll"
out = r"C:\Users\a1881\Desktop\Onion_QOL-1.0.2.zip"

assert os.path.exists(dll), "dll not found: " + dll
assert os.path.exists(folder), "folder not found: " + folder

if os.path.exists(out):
    os.remove(out)

files = ["manifest.json", "icon.png", "README.md", "CHANGELOG.md", "LICENSE"]
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as z:
    z.write(dll, "BepInEx/plugins/OnionQOL-Onion_QOL/OnionQOL.dll")
    for name in files:
        path = os.path.join(folder, name)
        assert os.path.exists(path), "missing: " + path
        z.write(path, name)

print("zip:", out, os.path.getsize(out))
with zipfile.ZipFile(out) as z:
    for item in z.infolist():
        print(item.filename, item.file_size)
