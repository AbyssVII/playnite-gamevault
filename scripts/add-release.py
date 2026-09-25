#!/usr/bin/env python3
"""往 manifests/installer.yaml 顶部插入一条新版本记录。

为什么需要这个脚本：
  官方扩展库靠 installer.yaml 判断「有没有新版本、去哪里下载」。而 CI 只会把
  extension.yaml 的 Version 改成 git tag 的版本号，**不会**动 installer.yaml。
  所以每次发新版本都要跑一次这个脚本，否则已安装的用户收不到更新提示。

用法：
    python scripts/add-release.py 1.0.1
    python scripts/add-release.py 1.0.1 --changelog "修复 xxx" --changelog "新增 yyy"

不加 --changelog 会写一条占位说明。
"""

import argparse
import datetime
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
INSTALLER = os.path.join(ROOT, "manifests", "installer.yaml")

# 与 Playnite.SDK.dll 的 AssemblyVersion 一致（Playnite 10.56 → 6.16.0）
DEFAULT_API_VERSION = "6.16.0"


def find_source_url():
    """从 addon 清单里取 SourceUrl，用来拼出 PackageUrl。"""
    manifest_dir = os.path.join(ROOT, "manifests")
    for name in sorted(os.listdir(manifest_dir)):
        if name.endswith((".yaml", ".yml")) and "installer" not in name:
            with open(os.path.join(manifest_dir, name), encoding="utf-8") as handle:
                for line in handle:
                    match = re.match(r"^SourceUrl:\s*(\S+)\s*$", line)
                    if match:
                        return match.group(1).rstrip("/")
    raise SystemExit("在 manifests/ 里找不到 addon 清单的 SourceUrl")


def main():
    parser = argparse.ArgumentParser(description="往安装清单里加一条新版本记录")
    parser.add_argument("version", help="版本号，例如 1.0.1")
    parser.add_argument("--date", default=datetime.date.today().isoformat(),
                        help="发布日期，默认取今天")
    parser.add_argument("--api-version", default=DEFAULT_API_VERSION,
                        help="最低 SDK API 版本，默认 " + DEFAULT_API_VERSION)
    parser.add_argument("--changelog", action="append", default=[],
                        help="一条更新说明，可重复传入；不给则写占位文字")
    args = parser.parse_args()

    if not re.match(r"^\d+\.\d+\.\d+$", args.version):
        raise SystemExit("版本号要形如 1.0.1（三段数字）")

    with open(INSTALLER, encoding="utf-8") as handle:
        content = handle.read()

    if re.search(r"^\s*-\s*Version:\s*%s\s*$" % re.escape(args.version), content, re.M):
        raise SystemExit("清单里已经有 %s 了，不必重复添加" % args.version)

    source = find_source_url()
    package_url = "%s/releases/download/v%s/GameVault_%s.pext" % (
        source, args.version, args.version)

    changes = args.changelog or ["见 Release 说明 / See the release notes."]
    entry_lines = [
        "  - Version: %s" % args.version,
        "    RequiredApiVersion: %s" % args.api_version,
        "    ReleaseDate: %s" % args.date,
        "    PackageUrl: %s" % package_url,
        "    Changelog:",
    ]
    entry_lines += ["      - %s" % item for item in changes]
    entry = "\n".join(entry_lines) + "\n"

    if not re.search(r"^Packages:\s*$", content, re.M):
        raise SystemExit("installer.yaml 里找不到 Packages: 这一行")

    # 新版本必须排在最前面——自动更新取第一条作为最新版
    updated = re.sub(r"^Packages:\s*$", "Packages:\n" + entry, content,
                     count=1, flags=re.M)

    with open(INSTALLER, "w", encoding="utf-8", newline="\n") as handle:
        handle.write(updated)

    print("已把 %s 写入 %s" % (args.version, os.path.relpath(INSTALLER, ROOT)))
    print("  PackageUrl: %s" % package_url)
    print()
    print("接下来：")
    print("  1. git add manifests/installer.yaml && git commit")
    print("  2. push 到 main")
    print("  3. 打标签 v%s，或在 Actions 页面手动运行并填 %s" % (args.version, args.version))
    return 0


if __name__ == "__main__":
    sys.exit(main())
