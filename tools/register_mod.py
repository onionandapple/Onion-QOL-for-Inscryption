import time

path = r"C:\Users\a1881\AppData\Roaming\r2modmanPlus-local\Inscryption\profiles\Default\mods.yml"

with open(path, "r", encoding="utf-8") as f:
    content = f.read()

if "name: OnionQOL-Onion_QOL" in content:
    print("already registered")
    raise SystemExit(0)

entry = """- manifestVersion: 1
  name: OnionQOL-Onion_QOL
  authorName: OnionQOL
  websiteUrl: ''
  displayName: Onion QOL
  description: 在凯西模组对局中按 Q 查看本局牌组，并在游戏设置界面提供配置。
  gameVersion: '0'
  networkMode: both
  packageType: other
  installMode: managed
  installedAtTime: %d
  loaders: []
  dependencies:
    - BepInEx-BepInExPack_Inscryption-5.4.2305
    - BepInEx-MonoMod_Loader_Inscryption-1.0.0
    - API_dev-API-2.24.1
  incompatibilities: []
  optionalDependencies: []
  versionNumber:
    major: 0
    minor: 1
    patch: 0
  enabled: true
  onlineSource: false
  trustedPackage: false
""" % int(time.time() * 1000)

if not content.endswith("\n"):
    content += "\n"

content += entry

with open(path, "w", encoding="utf-8", newline="\n") as f:
    f.write(content)

print("registered")
