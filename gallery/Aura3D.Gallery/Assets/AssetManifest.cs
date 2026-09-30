using Aura3D.Gallery.Localization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Aura3D.Gallery.Assets;

/// <summary>
/// 一个演示资产在清单中的登记项。
/// </summary>
/// <param name="Key">代码里用来取该资产的短标识。</param>
/// <param name="Path">相对资产根目录的路径，用 '/' 分隔。</param>
/// <param name="Bytes">磁盘上的字节数，仅用于 UI 预估下载量。</param>
/// <param name="WebFriendly">浏览器端是否适合取用（大资产或仅桌面用到的登记为 false）。</param>
public readonly record struct AssetRef(string Key, string Path, long Bytes, bool WebFriendly = true);

/// <summary>
/// 全部演示资产的登记表。演示页通过 <see cref="AssetRef"/> 取资产，外壳据此汇总体积，
/// 并在浏览器端过滤掉含 <c>WebFriendly=false</c> 资产的功能页。
/// 资产表本身（<c>All</c>）不在这里：它由 <c>tools/assets/generate.mjs</c> 按 gallery/assets 的磁盘实测
/// 字节生成到 AssetManifest.Generated.cs，所以登记内容不可能和磁盘漂移。
/// </summary>
public static partial class AssetManifest
{
    private static Dictionary<string, AssetRef>? byPath;

    private static Dictionary<string, AssetRef> ByPath =>
        byPath ??= All.ToDictionary(asset => asset.Path, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// 取用一组资产：按 Key 查 <see cref="All"/>，并把它们合并成一个可显示的总量。
    /// </summary>
    /// <param name="keys">要用的资产 Key。</param>
    public static AssetSet RequireSet(params string[] keys)
    {
        var items = new List<AssetRef>(keys.Length);

        foreach (var key in keys)
        {
            items.Add(Find(key));
        }

        return new AssetSet(items);
    }

    /// <summary>
    /// 按路径查已登记资产，未登记返回 <c>null</c>。
    /// </summary>
    public static AssetRef? FindByPath(string path) =>
        ByPath.GetValueOrDefault(path.Replace('\\', '/'));

    private static AssetRef Find(string key)
    {
        foreach (var asset in All)
        {
            if (asset.Key == key)
                return asset;
        }

        throw new InvalidOperationException(Strings.Keys.Error_AssetKeyUnregistered.Format(key));
    }

    /// <summary>
    /// 立方图的六面，按 CubeMapFace 顺序（+X -X +Y -Y +Z -Z）。
    /// </summary>
    public static IReadOnlyList<string> SkyboxKeys { get; } =
        ["SkyboxPx", "SkyboxNx", "SkyboxPy", "SkyboxNy", "SkyboxPz", "SkyboxNz"];

    /// <summary>
    /// 在浏览器端可用的全部资产字节数合计。
    /// </summary>
    public static long WebTotalBytes => All.Where(asset => asset.WebFriendly).Sum(asset => asset.Bytes);
}

/// <summary>
/// 一个演示所需资产的组合，携带总量与平台可用性判断。
/// </summary>
public sealed class AssetSet(IReadOnlyList<AssetRef> items)
{
    /// <summary>零资产集合：程序化场景的功能页用它声明「不需要下载任何东西」。</summary>
    public static AssetSet Empty { get; } = new([]);

    /// <summary>组成该集合的资产。</summary>
    public IReadOnlyList<AssetRef> Items { get; } = items;

    /// <summary>所需字节数合计。</summary>
    public long TotalBytes => Items.Sum(item => item.Bytes);

    /// <summary>是否全部资产都适合在浏览器端下载。</summary>
    public bool WebFriendly => Items.All(item => item.WebFriendly);

    /// <summary>按 Key 取资产路径。</summary>
    public string PathOf(string key) => Items.First(item => item.Key == key).Path;
}
