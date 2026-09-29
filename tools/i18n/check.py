#!/usr/bin/env python3
"""
示例工程的双语不变量核对（CI 用，纯标准库）。

盯住三件事，缺一条双语就会在没人看见的地方烂掉：

1. Strings.resx 与 Strings.zh-Hans.resx 的键集合必须完全一致。
   少一边不会报错，只会静默回落到英文（Lingua 找不到文化就走 invariant）。
2. 代码里写的 Strings.Keys.X 必须存在，resx 里的键也必须被用到（不留死键）。
   键名拼错在 C# 侧本来就编译不过，这条查的是 XAML 的 x:Static 与两边集合的差。
3. .axaml 的属性值与 .cs 的字符串字面量里不许出现中日韩文字：界面文案一律走 resx。
   注释不算（注释本来就只写一种语言），GLSL/WGSL 源串里的注释也不算。
4. Format 的实参个数与串里的占位符必须一致，两份 resx 的占位符还得相同；带格式说明符的
   占位符（`{1:0.##}`）不能收文案。对不上不报编译错，只在页面刷读数时抛 FormatException，
   属于最晚发现的那类故障。
5. 下拉选项（`LinguaKey[]` 声明的那几个数组）在同一种语言里文案必须互不相同。
   选项身份是用显示文字反查出来的（IndexOf），两项撞字串就等于两个选项指向同一项。

用法：
    python3 tools/i18n/check.py
"""
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

REPO_ROOT = Path(__file__).resolve().parents[2]
SHARED = REPO_ROOT / "example" / "Aura3D.Example.Shared"
RESX_PAIR = (SHARED / "Localization" / "Strings.resx", SHARED / "Localization" / "Strings.zh-Hans.resx")

# CJK 统一表意文字 + 扩展 A + 中日韩符号与标点 + 全角形式。
CJK = re.compile(r"[\u3000-\u30ff\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff\uff00-\uffef]")


def has_cjk(text: str) -> bool:
    return bool(CJK.search(text))


def resx_keys(path: Path) -> list[str]:
    root = ET.parse(path).getroot()
    return [node.get("name") for node in root.findall("data")]


def resx_values(path: Path) -> dict[str, str]:
    root = ET.parse(path).getroot()
    values = {}
    for node in root.findall("data"):
        value = node.find("value")
        values[node.get("name")] = (value.text or "") if value is not None else ""
    return values


def placeholder_indices(text: str) -> list[int]:
    """串里用到的 string.Format 占位符下标，升序去重。"""
    return sorted({int(m.group(1)) for m in re.finditer(r"\{(\d+)", text)})


def split_args(text: str, open_index: int) -> tuple[list[str], int]:
    """从 `(` 的位置切开实参，返回 (实参列表, 右括号位置)。

    只管嵌套括号与字符串字面量：实参里出现的逗号在括号或引号内不算分隔符。
    """
    args, buf = [], []
    depth, i, quote = 0, open_index + 1, None
    while i < len(text):
        ch = text[i]
        if quote:
            buf.append(ch)
            if ch == "\\":
                buf.append(text[i + 1])
                i += 2
                continue
            if ch == quote:
                quote = None
        elif ch in "\"'":
            quote = ch
            buf.append(ch)
        elif ch in "([{":
            depth += 1
            buf.append(ch)
        elif ch in ")]}":
            if depth == 0 and ch == ")":
                args.append("".join(buf))
                return args, i
            depth -= 1
            buf.append(ch)
        elif ch == "," and depth == 0:
            args.append("".join(buf))
            buf = []
        else:
            buf.append(ch)
        i += 1
    raise ValueError("括号没闭合")


def placeholder_specs(text: str) -> dict[int, str]:
    """占位符下标 → 格式说明符（`{2:0.###}` 里的 `0.###`），没写说明符的是空串。"""
    return {int(m.group(1)): (m.group(2) or "")
            for m in re.finditer(r"\{(\d+)(?::([^{}]*))?\}", text)}


def looks_like_text(expr: str) -> bool:
    """实参写得像文案而不是数值：字符串字面量，或 .T() / resx 引用。"""
    expr = expr.strip()
    return expr.startswith('"') or expr.startswith("Strings.Keys.") or ".T()" in expr


def check_format_arity() -> list[str]:
    """每个 Format 调用传的实参个数，必须与串里的占位符严格对上。

    对不上不会编译报错，只会在页面刷读数时抛 FormatException——正是最晚发现的那种错。
    带说明符的占位符（`{1:0.##}`）收到字符串同样会抛，所以顺手核对实参的形状。
    """
    problems = []
    en, zh = resx_values(RESX_PAIR[0]), resx_values(RESX_PAIR[1])

    for key in sorted(set(en) & set(zh)):
        mine, theirs = placeholder_indices(en[key]), placeholder_indices(zh[key])
        if mine != theirs:
            problems.append(f"{key}: 两份 resx 的占位符不一致 en={mine} zh={theirs}")
        if theirs != list(range(len(theirs))):
            problems.append(f"{key}: 占位符不连续：{theirs}")

    for path in sources((".cs",)):
        text = path.read_text(encoding="utf-8", errors="replace")
        for m in re.finditer(r"Strings\.Keys\.(\w+)\.(Format|T)\(", text):
            key, method = m.group(1), m.group(2)
            if key not in en:
                continue
            args, _ = split_args(text, m.end() - 1)
            count = len([a for a in args if a.strip()])
            need = len(placeholder_indices(en[key]))
            where = f"{path.relative_to(REPO_ROOT)}:{text[:m.start()].count(chr(10)) + 1}"
            if method == "T" and need:
                problems.append(f"{where} {key}: 串里有 {need} 个占位符却用 .T()，界面会露出大括号")
            elif method == "Format":
                if count != need:
                    problems.append(f"{where} {key}: Format 传了 {count} 个实参，串里需要 {need} 个")
                    continue
                zh_specs = placeholder_specs(zh[key]) if key in zh else {}
                specs = placeholder_specs(en[key])
                for i, arg in enumerate(a for a in args if a.strip()):
                    # 两种语言各自写的说明符都要核对：只要有一边带数值格式，收到文案就抛。
                    spec = specs.get(i) or zh_specs.get(i)
                    if spec and looks_like_text(arg):
                        problems.append(
                            f"{where} {key}: 占位符 {{{i}:{spec}}} 收到的是文案 "
                            f"{arg.strip()[:48]}，跑起来会抛 FormatException")
    return problems


def sources(suffixes: tuple[str, ...]) -> list[Path]:
    return sorted(
        p for p in (REPO_ROOT / "example").rglob("*")
        if p.suffix in suffixes and "bin" not in p.parts and "obj" not in p.parts
    )


def check_resx_parity() -> list[str]:
    a, b = RESX_PAIR
    keys_a, keys_b = resx_keys(a), resx_keys(b)
    problems = []
    for name, keys in ((a.name, keys_a), (b.name, keys_b)):
        dupes = {k for k in keys if keys.count(k) > 1}
        if dupes:
            problems.append(f"{name}: 重复键 {sorted(dupes)}")
    only_a = sorted(set(keys_a) - set(keys_b))
    only_b = sorted(set(keys_b) - set(keys_a))
    if only_a:
        problems.append(f"只在 {a.name} 里的键（中文漏翻）：{only_a}")
    if only_b:
        problems.append(f"只在 {b.name} 里的键（英文漏翻）：{only_b}")
    return problems


def check_key_usage(declared: set[str]) -> list[str]:
    used = set()
    for path in sources((".cs", ".axaml")):
        text = path.read_text(encoding="utf-8", errors="replace")
        used.update(re.findall(r"Strings\.Keys\.(\w+)", text))
        used.update(re.findall(r"Strings\+Keys\.(\w+)", text))

    problems = []
    unknown = sorted(used - declared)
    if unknown:
        problems.append(f"代码引用了 resx 里没有的键：{unknown}")
    dead = sorted(declared - used)
    if dead:
        problems.append(f"resx 里没有任何引用方的死键：{dead}")
    return problems


def check_axaml() -> list[str]:
    problems = []
    for path in sources((".axaml",)):
        text = re.sub(r"<!--.*?-->", "", path.read_text(encoding="utf-8", errors="replace"), flags=re.S)
        for line_no, line in enumerate(text.splitlines(), 1):
            for attr, value in re.findall(r'([A-Za-z][\w.]*)="([^"]*)"', line):
                if has_cjk(value):
                    problems.append(f"{path.relative_to(REPO_ROOT)}:{line_no} {attr}= 写死了中文：{value[:60]}")
            for body in re.findall(r">([^<>]+)<", line):
                if has_cjk(body):
                    problems.append(f"{path.relative_to(REPO_ROOT)}:{line_no} 元素内容写死了中文：{body.strip()[:60]}")
    return problems


def cs_literals(text: str):
    """扫出 C# 文本里的字符串字面量，产出 (行号, 内容)。

    要绕开注释、字符字面量，以及 verbatim / raw string 三种写法。这里不追求完整解析 C#：
    只要不把注释里的中文当成文案、也不漏掉真正会显示出来的串就够。
    """
    i, n, line = 0, len(text), 1

    def advance_through(start: int, end: int):
        nonlocal line
        line += text.count("\n", start, end)

    while i < n:
        ch = text[i]

        if ch == "\n":
            line += 1
            i += 1
        elif text.startswith("//", i):
            i = text.find("\n", i)
            if i < 0:
                return
        elif text.startswith("/*", i):
            j = text.find("*/", i + 2)
            if j < 0:
                return
            advance_through(i, j)
            i = j + 2
        elif ch == "'":
            # 字符字面量：'\\'' 与 '\\\\' 里的转义会让 +1 落在引号之前，跳过反斜杠再找结尾。
            j = i + 1
            if text[j:j + 1] == "\\":
                j += 1
            i = text.find("'", j + 1) + 1 or n
        elif ch == '"':
            k = i - 1
            prefix = ""
            while k >= 0 and text[k] in "$@":
                prefix = text[k] + prefix
                k -= 1

            quotes = 0
            while text[i + quotes:i + quotes + 1] == '"':
                quotes += 1

            if quotes >= 3:
                # raw string：定界符是连续 3+ 个引号，内部不需要转义。
                close = text.find('"' * quotes, i + quotes)
                if close < 0:
                    return
                yield line, text[i + quotes:close]
                advance_through(i, close)
                i = close + quotes
                continue

            verbatim = "@" in prefix
            j = i + 1
            buf = []
            while j < n:
                c = text[j]
                if c == '"':
                    if verbatim and text[j + 1:j + 2] == '"':
                        buf.append('"')
                        j += 2
                        continue
                    break
                if c == "\\" and not verbatim:
                    if text[j + 1:j + 2] == "n":
                        buf.append("\n")
                    j += 2
                    continue
                if c == "\n":
                    if not verbatim:
                        break
                    line += 1
                buf.append(c)
                j += 1
            yield line, "".join(buf)
            i = j + 1
        else:
            i += 1


def check_cs() -> list[str]:
    problems = []
    for path in sources((".cs",)):
        text = path.read_text(encoding="utf-8", errors="replace")
        for line_no, literal in cs_literals(text):
            if has_cjk(literal):
                problems.append(
                    f"{path.relative_to(REPO_ROOT)}:{line_no} 字符串字面量里写死了中文：{literal.strip()[:60]}")
    return problems


def check_option_list_identity() -> list[str]:
    """`LinguaKey[]` 声明出来的下拉选项，两种语言里都不许出现同字串。

    选项的身份靠 IndexOf(显示文字) 从字符串反查，两项翻出来一样时它们会指向同一项，
    而这种重合在中文里比在英文里更容易出现（比如两个都叫「宽」的滑杆标签）。
    """
    problems = []
    en, zh = resx_values(RESX_PAIR[0]), resx_values(RESX_PAIR[1])
    for path in sources((".cs",)):
        text = path.read_text(encoding="utf-8", errors="replace")
        for m in re.finditer(r"LinguaKey\[\]\s+\w+\s*=\s*\[(.*?)\]", text, re.S):
            keys = re.findall(r"Strings\.Keys\.(\w+)", m.group(1))
            line = text[: m.start()].count("\n") + 1
            for lang, table in (("en", en), ("zh", zh)):
                by_text: dict[str, list[str]] = {}
                for key in keys:
                    by_text.setdefault(table.get(key, key), []).append(key)
                for value, group in by_text.items():
                    if len(group) > 1:
                        problems.append(
                            f"{path.relative_to(REPO_ROOT)}:{line} 选项列表里 {lang} 文案 {value!r} "
                            f"被 {group} 共用，选中项会反查错")
    return problems


def main() -> int:
    declared = set(resx_keys(RESX_PAIR[0]))
    problems = (check_resx_parity()
                + check_key_usage(declared)
                + check_axaml()
                + check_cs()
                + check_format_arity()
                + check_option_list_identity())

    if problems:
        print(f"双语核对失败，{len(problems)} 处：")
        for p in problems:
            print("  " + p)
        return 1

    print(f"双语核对通过：{len(declared)} 个键，两份 resx 键一致，无写死中文。")
    return 0


if __name__ == "__main__":
    sys.exit(main())
