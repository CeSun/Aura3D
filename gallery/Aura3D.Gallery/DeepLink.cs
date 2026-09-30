using System;
using System.Collections.Generic;

namespace Aura3D.Gallery;

/// <summary>
/// 启动参数到「进入哪个功能页」的解析。三个来源按优先级：
/// <list type="bullet">
/// <item><c>--page=xxx</c> / <c>--page xxx</c>：桌面宿主的命令行参数。</item>
/// <item>URL 查询串里的 <c>?page=xxx</c>：浏览器宿主。
/// <c>wwwroot/main.js</c> 把 <c>globalThis.location.href</c> 整串作为一个参数传给 <c>runMain</c>，
/// 所以这里直接从 URL 上取，不依赖 <c>withApplicationArgumentsFromQuery()</c> 的转换结果。</item>
/// <item>环境变量 <c>AURA3D_DEMO_PAGE</c>：桌面与 iOS/Android 模拟器的验证入口。</item>
/// </list>
/// 逐页验证因此可以直链加载，而不必靠像素点击导航。
/// </summary>
public static class DeepLink
{
    /// <summary>桌面/移动端用的环境变量名。</summary>
    public const string EnvironmentVariable = "AURA3D_DEMO_PAGE";

    private const string InlineSwitch = "--page=";

    /// <summary>
    /// 从启动参数与环境变量里取功能页标识。
    /// </summary>
    /// <param name="args">宿主 <c>Main</c> 收到的参数。</param>
    /// <returns>功能页标识或标题前缀；没有任何指定时返回 <c>null</c>。</returns>
    public static string? Resolve(IReadOnlyList<string> args)
    {
        for (int i = 0; i < args.Count; i++)
        {
            var arg = args[i];

            if (arg.StartsWith(InlineSwitch, StringComparison.OrdinalIgnoreCase))
                return Value(arg[InlineSwitch.Length..]);

            if ((arg == "--page" || arg == "-page") && i + 1 < args.Count)
                return Value(args[i + 1]);

            var fromQuery = QueryValue(arg, "page");

            if (fromQuery != null)
                return fromQuery;
        }

        return Value(Environment.GetEnvironmentVariable(EnvironmentVariable));
    }

    /// <summary>
    /// 取 URL 查询串里某个参数的值；<c>arg</c> 不是带查询串的 URL 时返回 <c>null</c>。
    /// </summary>
    public static string? QueryValue(string arg, string name)
    {
        var queryStart = arg.IndexOf('?');

        if (queryStart < 0 || queryStart == arg.Length - 1)
            return null;

        var query = arg[(queryStart + 1)..];
        var fragmentStart = query.IndexOf('#');

        if (fragmentStart >= 0)
            query = query[..fragmentStart];

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var equals = pair.IndexOf('=');

            if (equals <= 0)
                continue;

            if (string.Equals(pair[..equals], name, StringComparison.OrdinalIgnoreCase))
                return Value(Uri.UnescapeDataString(pair[(equals + 1)..]));
        }

        return null;
    }

    private static string? Value(string? raw) =>
        string.IsNullOrWhiteSpace(raw) ? null : raw;
}
