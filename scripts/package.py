#!/usr/bin/env python3
"""把 GameVault 打包成 Playnite 扩展安装包（.pext）。

`.pext` 本质就是一个改了扩展名的 zip —— 把 extension.yaml、GameVault.dll、
icon.png 三个文件放在压缩包**根目录**即可，不需要额外的安装清单
（那是提交到官方扩展库时才需要的，见 manifests/）。

用法：
    python scripts/package.py                              # 用 bin/Release 的产物
    python scripts/package.py --dll GameVault.dll          # 指定 dll（CI 里从 artifact 下载）
    python scripts/package.py --out-dir dist               # 指定输出目录

版本号自动从 extension.yaml 读取，产物命名为 GameVault_<版本>.pext。
"""

import argparse
import os
import re
import sys
import zipfile

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))

# (仓库内相对路径, 压缩包内路径)
FILES = [
    ("extension.yaml", "extension.yaml"),
    ("icon.png", "icon.png"),
]


def read_version(manifest_path):
    with open(manifest_path, encoding="utf-8") as fopen:
        for line in fopen:
            match = re.match(r"^Version:\s*(\S+)\s*$", line)
            if match:
                return match.group(1)
    raise SystemExit("extension.yaml 里找不到 Version 字段")


def main():
    parser = argparse.ArgumentParser(description="打包 Playnite 扩展为 .pext")
    parser.add_argument("--dll", default=os.path.join("bin", "Release", "GameVault.dll"),
                        help="编译好的 GameVault.dll 路径（相对仓库根目录）")
    parser.add_argument("--out-dir", default=".", help="输出目录（相对仓库根目录）")
    args = parser.parse_args()

    version = read_version(os.path.join(ROOT, "extension.yaml"))

    dll_path = os.path.join(ROOT, args.dll)
    if not os.path.isfile(dll_path):
        raise SystemExit("找不到编译产物：%s\n请先执行：dotnet build -c Release" % dll_path)

    out_dir = os.path.join(ROOT, args.out_dir)
    os.makedirs(out_dir, exist_ok=True)
    out_path = os.path.join(out_dir, "GameVault_%s.pext" % version)

    with zipfile.ZipFile(out_path, "w", zipfile.ZIP_DEFLATED) as archive:
        for src, dest in FILES:
            archive.write(os.path.join(ROOT, src), dest)
        archive.write(dll_path, "GameVault.dll")

    print("已生成 %s（版本 %s）" % (out_path, version))
    with zipfile.ZipFile(out_path) as archive:
        for info in archive.infolist():
            print("  %-18s %8d bytes" % (info.filename, info.file_size))
    return 0


if __name__ == "__main__":
    sys.exit(main())
