using System;
using System.Collections.Generic;
using System.Globalization;

namespace Aura3D.Examples.Localization;

/// <summary>
/// 界面语言的判定与切换。整个应用只有这一处决定用哪种文化，四个宿主共用：
/// <list type="bullet">
/// <item><c>--lang=zh|en</c> 或 URL 上的 <c>?lang=zh|en</c>：显式指定，验证脚本与深链用。</item>
/// <item>环境变量 <c>AURA3D_LANG</c>：桌面与模拟器上的验证入口，与 <see cref="DeepLink"/> 同一套约定。</item>
/// <item><see cref="CultureInfo.CurrentUICulture"/>：操作系统/浏览器的界面语言。</item>
/// </list>
/// 只有「中文」与「英文」两种，判定按两字母语言前缀匹配，其余一律落到英文（invariant 那份 resx）。
/// </summary>
public static class AppLanguage
{
    /// <summary>桌面/移动端用的环境变量名。</summary>
    public const string EnvironmentVariable = "AURA3D_LANG";

    private const string InlineSwitch = "--lang=";

    /// <summary>简体中文的文化标识，对应 <c>Strings.zh-Hans.resx</c>。</summary>
    public static readonly CultureInfo Chinese = CultureInfo.GetCultureInfo("zh-Hans");

    /// <summary>英文即 invariant：默认那份 <c>Strings.resx</c>。</summary>
    public static readonly CultureInfo English = CultureInfo.InvariantCulture;

    /// <summary>当前语言。启动时由 <see cref="ApplyStartup"/> 定下，之后只经 <see cref="Toggle"/> 改变。</summary>
    public static CultureInfo Current { get; private set; } = English;

    /// <summary>语言改变后触发；外壳订阅它来重建目录并重挂当前功能页。</summary>
    public static event EventHandler? Changed;

    /// <summary>当前是否中文。</summary>
    public static bool IsChinese => Current.TwoLetterISOLanguageName == "zh";

    /// <summary>宿主启动时调用一次，按优先级定下界面语言。</summary>
    /// <param name="args">宿主 <c>Main</c> 收到的参数（浏览器端是整串 URL）。</param>
    public static void ApplyStartup(IReadOnlyList<string> args) => Apply(Resolve(args));

    /// <summary>在中英之间切换。</summary>
    public static void Toggle() => Apply(IsChinese ? English : Chinese);

    /// <summary>直接指定语言，重复设为同一语言时不触发 <see cref="Changed"/>。</summary>
    public static void Apply(CultureInfo culture)
    {
        if (Equals(Current, culture))
            return;

        Current = culture;
        Strings.Instance.UpdateCulture(culture);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    private static CultureInfo Resolve(IReadOnlyList<string> args)
    {
        string? raw = null;

        for (int i = 0; i < args.Count && raw == null; i++)
        {
            var arg = args[i];

            if (arg.StartsWith(InlineSwitch, StringComparison.OrdinalIgnoreCase))
                raw = arg[InlineSwitch.Length..];
            else if ((arg == "--lang" || arg == "-lang") && i + 1 < args.Count)
                raw = args[i + 1];
            else
                raw = DeepLink.QueryValue(arg, "lang");
        }

        return FromName(raw ?? Environment.GetEnvironmentVariable(EnvironmentVariable) ?? CultureUICultureName());
    }

    private static string CultureUICultureName()
    {
        var name = CultureInfo.CurrentUICulture.Name;
        return string.IsNullOrEmpty(name) ? CultureInfo.CurrentUICulture.TwoLetterISOLanguageName : name;
    }

    private static CultureInfo FromName(string? name) =>
        name?.Trim().ToLowerInvariant().StartsWith("zh") == true ? Chinese : English;
}
