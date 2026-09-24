#!/usr/bin/env python3
"""校验扩展清单与 CI 配置是否自洽。

发布新版本、或向官方扩展库提交清单之前跑一下，能挡掉绝大多数字段缺失、
版本号对不上、ID 不一致这类低级错误——这些错误在 Playnite 里只表现为
"装不上"或"永远收不到更新"，排查起来很费时间。

    python scripts/check_manifests.py

依赖：pip install pyyaml
"""

import os
import re
import sys

try:
    import yaml
except ImportError:
    sys.exit("缺少依赖 pyyaml，请先执行：pip install pyyaml")

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# 官方扩展库允许的 Add-on 类型
VALID_TYPES = ("Generic", "GameLibrary", "MetadataProvider", "ThemeDesktop", "ThemeFullscreen")

_failures = []


def check(condition, message):
    print(("  [OK]   " if condition else "  [FAIL] ") + message)
    if not condition:
        _failures.append(message)


def load(relative_path):
    with open(os.path.join(ROOT, relative_path), encoding="utf-8") as handle:
        return yaml.safe_load(handle)


def find_addon_manifest():
    """manifests/ 下除了 installer.yaml，另一份就是要提交给官方库的 addon 清单。"""
    directory = os.path.join(ROOT, "manifests")
    for name in sorted(os.listdir(directory)):
        if name.endswith((".yaml", ".yml")) and "installer" not in name:
            return os.path.join("manifests", name)
    return None


def main():
    print("=== 1. extension.yaml（Playnite 直接读取）===")
    ext = load("extension.yaml")
    for key in ("Id", "Name", "Author", "Version", "Module", "Type", "Icon"):
        check(key in ext, "含必填字段 %s = %r" % (key, ext.get(key)))
    check(ext.get("Type") == "GenericPlugin", "Type 为 GenericPlugin")
    check(os.path.isfile(os.path.join(ROOT, ext.get("Icon", ""))),
          "Icon 指向的文件存在：%s" % ext.get("Icon"))

    print()
    print("=== 2. manifests/（提交给官方扩展库）===")
    addon_path = find_addon_manifest()
    check(addon_path is not None, "找到 addon 清单文件：%s" % addon_path)
    if addon_path is None:
        return 1
    addon = load(addon_path)
    installer = load("manifests/installer.yaml")

    for key in ("AddonId", "Type", "Name", "Author", "ShortDescription",
                "InstallerManifestUrl", "SourceUrl"):
        check(key in addon, "addon 清单含必填字段 %s" % key)
    check(addon.get("Type") in VALID_TYPES,
          "Type=%r 属于官方允许的取值" % addon.get("Type"))

    for key in ("Version", "RequiredApiVersion", "ReleaseDate", "PackageUrl"):
        check(key in installer["Packages"][0], "installer 清单含字段 %s" % key)
    check(bool(re.match(r"^\d{4}-\d{2}-\d{2}$", str(installer["Packages"][0]["ReleaseDate"]))),
          "ReleaseDate 为 YYYY-MM-DD 格式")
    check(bool(re.match(r"^\d+\.\d+\.\d+$", str(installer["Packages"][0]["RequiredApiVersion"]))),
          "RequiredApiVersion 为 .NET 版本串：%s" % installer["Packages"][0]["RequiredApiVersion"])

    print()
    print("=== 3. 交叉一致性（最容易出错的地方）===")
    check(addon.get("AddonId") == ext.get("Id"),
          "addon.AddonId 与 extension.yaml 的 Id 一致")
    check(installer.get("AddonId") == ext.get("Id"),
          "installer.AddonId 与 extension.yaml 的 Id 一致")

    version = str(installer["Packages"][0]["Version"])
    check(version == str(ext.get("Version")),
          "installer 版本 %s == extension.yaml 版本 %s" % (version, ext.get("Version")))
    check(("v%s" % version) in installer["Packages"][0]["PackageUrl"],
          "PackageUrl 里含 git tag（v%s）" % version)
    check(installer["Packages"][0]["PackageUrl"].endswith(".pext"), "PackageUrl 指向 .pext")

    print()
    print("=== 4. 仓库地址一致性 ===")
    urls = [addon.get("SourceUrl", ""), addon.get("InstallerManifestUrl", ""),
            addon.get("IconUrl", ""), installer["Packages"][0]["PackageUrl"]]
    urls += [link for link in (addon.get("Links") or {}).values()]
    urls += [link.get("Url", "") for link in (ext.get("Links") or [])]
    for shot in (addon.get("Screenshots") or []):
        urls += [shot.get("Thumbnail", ""), shot.get("Image", "")]
    users = set()
    for url in urls:
        # 同时覆盖 github.com/... 与 raw.githubusercontent.com/...
        match = re.search(r"(?:github\.com|githubusercontent\.com)/([^/]+)/", str(url))
        if match:
            users.add(match.group(1))
    check(len(users) == 1,
          "所有 GitHub 链接指向同一个账号：%s" % (", ".join(sorted(users)) or "无"))
    print("         如果这里列出了不止一个账号，说明有 URL 漏改（改用户名后最容易发生）")

    print()
    print("=== 5. CI 配置 ===")
    workflow = load(".github/workflows/release.yml")
    check("build" in workflow.get("jobs", {}), "含 build job")
    check("release" in workflow.get("jobs", {}), "含 release job")
    check(workflow.get("permissions", {}).get("contents") == "write",
          "permissions.contents=write（创建 Release 必需）")
    check(workflow["jobs"].get("release", {}).get("needs") == "build",
          "release job 依赖 build 产物")

    print()
    if _failures:
        print("存在 %d 项失败，请先修正。" % len(_failures))
        return 1
    print("全部通过。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
