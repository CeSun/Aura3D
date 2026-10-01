#!/usr/bin/env bash
# 构建 ANGLE 的 macOS dylib，连同 C shim 一起放进本目录的 native/macos-arm64/ 供 dotnet pack 使用。
#
# 前置：Xcode 与命令行工具、depot_tools（gn / ninja / gclient）在 PATH 里；
#       Xcode 26 起需要单独下载 Metal 工具链：xcodebuild -downloadComponent MetalToolchain。
#
# 用法：
#   ./build-angle-macos.sh              # 构建 arm64 切片
set -euo pipefail

ANGLE_REVISION=58f8882372e8a4e83da821ac5d16f0323c3fa1af
# 与 iOS 切片共用同一个 ANGLE 检出（revision 相同）；想隔离时 ANGLE_DIR=~/angle_macos 覆盖。
ANGLE_DIR=${ANGLE_DIR:-"$HOME/angle_ios"}
NATIVE_DIR="$(cd "$(dirname "$0")" && pwd)/native"
SHIM_SRC="$(cd "$(dirname "$0")" && pwd)/shim/aura3d_angle_macos.c"

if [ ! -d "$ANGLE_DIR/.git" ]; then
  git clone https://chromium.googlesource.com/angle/angle "$ANGLE_DIR"
  ( cd "$ANGLE_DIR" && gclient sync )
fi

cd "$ANGLE_DIR"
git checkout "$ANGLE_REVISION"
# gclient sync 需要父目录的 .gclient；ANGLE 独立检出的标准做法是 scripts/bootstrap.py，
# 这里显式写等价物，脚本在没 bootstrap 过的新机器上也能一次跑通。
if [ ! -f "$(dirname "$ANGLE_DIR")/.gclient" ]; then
  cat > "$(dirname "$ANGLE_DIR")/.gclient" <<GCLIENT
solutions = [
  {
    "name": "$(basename "$ANGLE_DIR")",
    "url": "https://chromium.googlesource.com/angle/angle@${ANGLE_REVISION}",
    "deps_file": "DEPS",
    "managed": False,
    "custom_deps": {},
    "custom_vars": {},
    "safety_check_site_path": None,
  },
]
GCLIENT
fi
gclient sync -D

out="out/macos-arm64"
echo "== macos-arm64 ($out) =="
gn gen "$out" --args="target_os = \"mac\"
target_cpu = \"arm64\"
is_component_build = false
angle_enable_metal = true
is_debug = false
enable_rust = false"
ninja -C "$out" libEGL libGLESv2

rm -rf "$NATIVE_DIR/macos-arm64"
mkdir -p "$NATIVE_DIR/macos-arm64"
cp "$out/libEGL.dylib" "$out/libGLESv2.dylib" "$NATIVE_DIR/macos-arm64/"

# IOSurface 的 C shim：源码在仓库里，随切片一起重编（clang 由 Xcode 提供）。
cc -shared -o "$NATIVE_DIR/macos-arm64/libAura3dAngleMacOSHelper.dylib" \
  "$SHIM_SRC" -framework CoreFoundation -framework IOSurface

echo
echo "切片就位：$(ls "$NATIVE_DIR/macos-arm64" | tr '\n' ' ')"
echo "打包：dotnet pack src/Aura3D.Angle.MacOS -c Release -o artifacts/nupkgs"
