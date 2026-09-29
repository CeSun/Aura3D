#!/usr/bin/env python3
"""
生成示例工程用的中文字体子集：example/Aura3D.Example.Shared/Fonts/NotoSansSC-Subset.otf

为什么必须内置：浏览器宿主没有系统字体可用（Avalonia.Browser 拿不到操作系统的字体表），
所以没注册中文字体时，界面上的中文一律是豆腐块。桌面/移动端有系统字体，看不出这个问题，
因此这条产物必须进仓库、并且由 CI 核对覆盖，不能靠人记得本机看一眼。

子集范围取 GB2312 全集（含其中的中文标点与符号）加上拉丁与常用排版符号，而不是
「把当前 UI 里出现过的字扫一遍」：动态拼接出来的文案、主题包自带的中文（确定/取消之类）
都不在源码里，按源码取字会漏，漏了就又是豆腐块。GB2312 覆盖不到的字（如 U+2717 ✗）
Noto Sans SC 本身也没有，遇到这种字符改用它画得出来的等价符号。

源字体：Noto Sans SC Regular（SIL Open Font License 1.1），8.3MB，不入库，
生成时按固定 URL 下载并核对 sha256，缓存在 tools/fonts/.cache/。

用法：
    python3 tools/fonts/generate-cjk-font.py            重新子集化（需要 fontTools）
    python3 tools/fonts/generate-cjk-font.py --check    只核对，不重生成（CI 用这条，纯标准库）
"""
import hashlib
import sys
import unicodedata
import urllib.request
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SRC_URL = ("https://raw.githubusercontent.com/notofonts/noto-cjk/main"
           "/Sans/SubsetOTF/SC/NotoSansSC-Regular.otf")
SRC_SHA256 = "faa6c9df652116dde789d351359f3d7e5d2285a2b2a1f04a2d7244df706d5ea9"
CACHE = Path(__file__).resolve().parent / ".cache" / "NotoSansSC-Regular.otf"
OUT = REPO_ROOT / "example" / "Aura3D.Example.Shared" / "Fonts" / "NotoSansSC-Subset.otf"
# 子集里真正落进了哪些码点，由生成时反读字体 cmap 写在这里。--check 拿它核对，
# 于是 CI 不必装 fontTools、也不必自己解析 cmap，同时杜绝「字符集里有但源字体本来没有」的假通过。
COVERED = Path(__file__).resolve().parent / "charset-covered.txt"

# 子集产物是要编进程序集、浏览器启动时下载的，体积必须盯住。
BUDGET_BYTES = 2_500_000

# GB2312 之外的补充区段：拉丁、常用标点与符号、箭头、几何图形、装饰符号、全角形式。
EXTRA_RANGES = [
    (0x0020, 0x007E), (0x00A0, 0x00FF), (0x0100, 0x017F), (0x0192, 0x0193),
    (0x2000, 0x206F), (0x20A0, 0x20BF), (0x2190, 0x21FF), (0x2200, 0x22FF),
    (0x2460, 0x24FF), (0x25A0, 0x27BF), (0x2EB0, 0x2EFF), (0x3000, 0x303F),
    (0x3200, 0x32FF), (0xFE10, 0xFE1F), (0xFE30, 0xFE4F), (0xFF01, 0xFF60),
    (0xFFE0, 0xFFE6),
]


def charset() -> set[str]:
    """子集应当覆盖的码点集合：GB2312 全集 + EXTRA_RANGES，去掉控制/格式字符。"""
    chars = set()
    for hi in range(0xA1, 0xF8):
        for lo in range(0xA1, 0xFF):
            try:
                chars.update(bytes([hi, lo]).decode("gb2312"))
            except UnicodeDecodeError:
                pass
    for start, end in EXTRA_RANGES:
        chars.update(chr(cp) for cp in range(start, end + 1))
    return {c for c in chars if unicodedata.category(c)[0] not in ("C", "M")}


def ui_chars() -> dict[str, list[str]]:
    """示例源码与 resx 文案里出现的、渲染时可能落到屏幕上的非 ASCII 字符 → 出处文件。"""
    found: dict[str, list[str]] = {}
    sources = [p for p in (REPO_ROOT / "example").rglob("*")
               if p.suffix in (".cs", ".axaml", ".resx")
               and "bin" not in p.parts and "obj" not in p.parts]
    for path in sources:
        for ch in set(path.read_text(encoding="utf-8", errors="replace")):
            if ord(ch) > 0x7F and not ch.isspace() and unicodedata.category(ch)[0] not in ("C", "F"):
                found.setdefault(ch, []).append(str(path.relative_to(REPO_ROOT)))
    return found


def source_font() -> Path:
    CACHE.parent.mkdir(parents=True, exist_ok=True)
    if CACHE.exists() and hashlib.sha256(CACHE.read_bytes()).hexdigest() == SRC_SHA256:
        return CACHE
    print(f"下载源字体 → {CACHE}")
    data = urllib.request.urlopen(SRC_URL, timeout=120).read()
    digest = hashlib.sha256(data).hexdigest()
    if digest != SRC_SHA256:
        raise SystemExit(f"源字体 sha256 不符：期望 {SRC_SHA256}，实际 {digest}（URL 内容变了，核对后更新常量）")
    CACHE.write_bytes(data)
    return CACHE


def generate() -> int:
    try:
        from fontTools import subset
    except ImportError:
        raise SystemExit("需要 fontTools：python3 -m venv /tmp/fontenv && /tmp/fontenv/bin/pip install fonttools")

    chars = sorted(charset())
    CACHE.parent.mkdir(parents=True, exist_ok=True)
    text_file = CACHE.parent / "charset.txt"
    text_file.write_text("".join(chars), encoding="utf-8")

    OUT.parent.mkdir(parents=True, exist_ok=True)
    subset.main([
        str(source_font()),
        f"--text-file={text_file}",
        f"--output-file={OUT}",
        # 示例界面不需要连字与字距替换，去掉 shaping 表能省下可观的体积。
        "--layout-features=",
        "--no-hinting",
        "--desubroutinize",
    ])

    from fontTools.ttLib import TTFont
    size = OUT.stat().st_size
    font = TTFont(OUT)
    COVERED.write_text("".join(chr(cp) for cp in sorted(font.getBestCmap())), encoding="utf-8")
    print(f"生成 {OUT.relative_to(REPO_ROOT)}：{size/1e6:.2f} MB，{font['maxp'].numGlyphs} 个字形，"
          f"{len(font.getBestCmap())} 个码点（写入 {COVERED.relative_to(REPO_ROOT)}）")
    if size > BUDGET_BYTES:
        raise SystemExit(f"超出预算 {BUDGET_BYTES/1e6:.1f} MB，需要收窄字符集")
    return 0


def check() -> int:
    if not OUT.exists():
        raise SystemExit(f"缺少 {OUT}：中文会全是豆腐块，先跑 generate-cjk-font.py")
    if not COVERED.exists():
        raise SystemExit(f"缺少 {COVERED}：跑一次 generate-cjk-font.py 生成")
    size = OUT.stat().st_size
    if size > BUDGET_BYTES:
        raise SystemExit(f"{OUT.name} 体积 {size/1e6:.2f} MB，超出预算 {BUDGET_BYTES/1e6:.1f} MB")

    covered = set(COVERED.read_text(encoding="utf-8"))
    used = ui_chars()
    missing = {ch: files for ch, files in used.items() if ch not in covered}
    print(f"字体 {OUT.name} {size/1e6:.2f} MB；覆盖 {len(covered)} 个码点；"
          f"UI 用到 {len(used)} 个非 ASCII 字符")
    if missing:
        print("以下字符子集里没有，浏览器上会是豆腐块（要么改字，要么扩字符集后重新生成）：")
        for ch, files in sorted(missing.items()):
            print(f"  U+{ord(ch):04X} {ch!r}  见 {', '.join(sorted(set(files))[:3])}")
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(check() if "--check" in sys.argv else generate())
