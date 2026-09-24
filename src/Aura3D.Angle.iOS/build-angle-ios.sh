#!/usr/bin/env bash
# 构建 ANGLE 的 iOS framework，放进本目录的 native/<切片>/ 供 dotnet pack 使用。
#
# 前置：Xcode 与命令行工具、depot_tools（gn / ninja / gclient）在 PATH 里。
# 结论出处：ANGLE 只暴露 ES 3.0、无 GL_EXT_float_blend，详见 Aura3D.Avalonia 的 iOS 后端注释。
#
# 用法：
#   ./build-angle-ios.sh              # 只构建模拟器切片（已验证）
#   ./build-angle-ios.sh --device     # 追加真机切片（尚未在设备上验证）
set -euo pipefail

ANGLE_REVISION=58f8882372e8a4e83da821ac5d16f0323c3fa1af
ANGLE_DIR=${ANGLE_DIR:-"$HOME/angle_ios"}
NATIVE_DIR="$(cd "$(dirname "$0")" && pwd)/native"
WITH_DEVICE=0
[ "${1:-}" = "--device" ] && WITH_DEVICE=1

if [ ! -d "$ANGLE_DIR/.git" ]; then
  git clone https://chromium.googlesource.com/angle/angle "$ANGLE_DIR"
  ( cd "$ANGLE_DIR" && gclient sync )
fi

cd "$ANGLE_DIR"
git checkout "$ANGLE_REVISION"
gclient sync -D

build_slice() { # $1 = out 子目录, $2 = target_environment, $3 = native 切片目录名
  local out="out/$1" env_name="$2" slice="$3"
  echo "== $slice ($out) =="
  gn gen "$out" --args="target_os = \"ios\"
target_environment = \"$env_name\"
target_cpu = \"arm64\"
is_component_build = false
angle_enable_metal = true
is_debug = false
enable_rust = false"
  ninja -C "$out" libEGL libGLESv2

  rm -rf "$NATIVE_DIR/$slice"
  mkdir -p "$NATIVE_DIR/$slice"
  cp -R "$out/libEGL.framework" "$out/libGLESv2.framework" "$NATIVE_DIR/$slice/"
}

build_slice ios_simulator simulator iossimulator-arm64
if [ "$WITH_DEVICE" = 1 ]; then
  # 真机切片只能证明编译与打包通过；能不能跑需要一台设备，目前未验证。
  build_slice ios_device device ios-arm64
fi

echo
echo "切片就位：$(ls "$NATIVE_DIR" | tr '\n' ' ')"
echo "打包：dotnet pack src/Aura3D.Angle.iOS -c Release -o artifacts/nupkgs"
