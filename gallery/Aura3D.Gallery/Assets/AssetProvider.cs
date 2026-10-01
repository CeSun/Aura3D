using Aura3D.Gallery.Localization;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Aura3D.Gallery.Assets;

/// <summary>
/// 一次资产取用的进度快照。
/// </summary>
/// <param name="Key">正在取用的资产 Key。</param>
/// <param name="DownloadedBytes">该资产已取到的字节数。</param>
/// <param name="TotalBytes">该资产总字节数，未知时为 0。</param>
/// <param name="CompletedAssets">本次取用中已完成的资产数。</param>
/// <param name="TotalAssets">本次取用需要完成的资产总数。</param>
public readonly record struct AssetProgress(
    string Key, long DownloadedBytes, long TotalBytes, int CompletedAssets, int TotalAssets);

/// <summary>
/// 演示资产的取用后端。桌面/移动端读磁盘目录，浏览器走 HTTP 按需下载，
/// 演示代码只看到 <see cref="OpenAsync"/>，因此四个平台共用同一份场景构建代码。
/// </summary>
public interface IAssetProvider
{
    /// <summary>
    /// 打开一个资产供读取。实现可以先解码再解码线程池执行，调用方负责释放返回的流。
    /// </summary>
    /// <param name="asset">清单里的资产引用。</param>
    /// <param name="progress">取用过程中的进度回调，可为空。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    Task<Stream> OpenAsync(
        AssetRef asset,
        IProgress<AssetProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>当前是否在浏览器中运行（决定资产是否受 WebFriendly 约束）。</summary>
    bool IsWeb { get; }
}

/// <summary>
/// 从磁盘目录读取资产的实现。根目录按环境变量与若干相对位置探测，
/// 这样 `dotnet run`、IDE 起始目录、打包后的 app 目录都能找到 gallery/assets。
/// </summary>
public sealed class FileAssetProvider(string? rootOverride = null) : IAssetProvider
{
    private readonly string root = rootOverride ?? ResolveRoot();

    /// <inheritdoc />
    public bool IsWeb => false;

    /// <summary>
    /// 实际解析到的资产根目录，用于在 UI 上提示路径不对的情况。
    /// </summary>
    public string Root => root;

    /// <inheritdoc />
    public async Task<Stream> OpenAsync(
        AssetRef asset,
        IProgress<AssetProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var fullPath = Path.Combine(root, asset.Path.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                Strings.Keys.Error_AssetFileMissing.Format(asset.Key, asset.Path, root),
                fullPath);
        }

        progress?.Report(new AssetProgress(asset.Key, 0, asset.Bytes, 0, 1));

        // 预读整块再包成内存流：既让进度可信，也避免解码期间一直占着文件句柄。
        var bytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);

        progress?.Report(new AssetProgress(asset.Key, bytes.LongLength, bytes.LongLength, 1, 1));

        return new MemoryStream(bytes, writable: false);
    }

    private static string ResolveRoot()
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("AURA3D_ASSET_ROOT");

        if (!string.IsNullOrEmpty(fromEnvironment) && Directory.Exists(fromEnvironment))
            return fromEnvironment;

        string probe = Path.Combine(AppContext.BaseDirectory, "assets");

        if (Directory.Exists(probe))
            return probe;

        // 源码目录下各宿主的 bin 输出到 gallery/assets 的相对距离：
        // bin/Debug/net10.0 之上四层即仓库根，从仓库根进 gallery/assets。
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        for (int i = 0; i < 8 && directory != null; i++)
        {
            var candidate = Path.Combine(directory.FullName, "gallery", "assets");

            if (Directory.Exists(candidate))
                return candidate;

            var auraCandidate = Path.Combine(directory.FullName, "assets");

            if (Directory.Exists(auraCandidate) && File.Exists(Path.Combine(auraCandidate, "models", "lion_head_1k.glb")))
                return auraCandidate;

            directory = directory.Parent;
        }

        return probe;
    }
}

/// <summary>
/// 由宿主注入「按清单相对路径开流」委托的实现，给资产只存在于包内、没有可按文件路径
/// 访问目录的宿主用（安卓的 AssetManager）。共享工程不引平台包，平台差异全部留在宿主侧。
/// </summary>
public sealed class StreamAssetProvider(Func<string, Stream> openStream) : IAssetProvider
{
    /// <inheritdoc />
    public bool IsWeb => false;

    /// <inheritdoc />
    public async Task<Stream> OpenAsync(
        AssetRef asset,
        IProgress<AssetProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new AssetProgress(asset.Key, 0, asset.Bytes, 0, 1));

        // 与 FileAssetProvider 一样整块读进内存再返回：AssetManager 的流按序读取即可，
        // 压缩条目不支持 seek，清单里的 Bytes 只当预分配容量提示，可能与实际大小有出入。
        using var source = openStream(asset.Path);

        var buffered = new MemoryStream(new byte[(int)Math.Clamp(asset.Bytes, 0, int.MaxValue)]);

        await source.CopyToAsync(buffered, cancellationToken);

        progress?.Report(new AssetProgress(asset.Key, buffered.Length, buffered.Length, 1, 1));

        buffered.Position = 0;

        return buffered;
    }
}

/// <summary>
/// 通过 HTTP 按需下载资产的实现。字节内容进 <see cref="AssetCache"/>，
/// 因此在同一会话里重复进入同一功能页只会有一次网络开销（解码开销无法避免）。
/// </summary>
public sealed class WebAssetProvider(Uri baseUri, AssetCache cache) : IAssetProvider
{
    private static readonly HttpClient HttpClient = new() { Timeout = TimeSpan.FromMinutes(5) };

    /// <inheritdoc />
    public bool IsWeb => true;

    /// <inheritdoc />
    public async Task<Stream> OpenAsync(
        AssetRef asset,
        IProgress<AssetProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!asset.WebFriendly)
        {
            throw new InvalidOperationException(
                Strings.Keys.Error_AssetNotOnWeb.Format(asset.Key, asset.Bytes / 1024 / 1024));
        }

        if (cache.TryGet(asset.Key, out var cached))
        {
            progress?.Report(new AssetProgress(asset.Key, cached.LongLength, cached.LongLength, 1, 1));

            return new MemoryStream(cached, writable: false);
        }

        var uri = new Uri(baseUri, asset.Path);

        using (var response = await HttpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
        {
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength ?? asset.Bytes;

            await using var remote = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var buffered = new MemoryStream();

            var buffer = new byte[81920];
            long downloaded = 0;
            int read;

            while ((read = await remote.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await buffered.WriteAsync(buffer.AsMemory(0, read), cancellationToken);

                downloaded += read;

                progress?.Report(new AssetProgress(asset.Key, downloaded, total, 0, 1));
            }

            var bytes = buffered.ToArray();

            cache.Store(asset.Key, bytes);

            progress?.Report(new AssetProgress(asset.Key, bytes.LongLength, bytes.LongLength, 1, 1));

            return new MemoryStream(bytes, writable: false);
        }
    }
}

/// <summary>
/// 已下载资产字节的会话级缓存。上限是字节数，超出时先丢最小的那批以外全部旧项，
/// 目的是让来回切换功能页不重复付网络代价，同时不无上限吃内存。
/// </summary>
public sealed class AssetCache(long maxBytes = 96L * 1024 * 1024)
{
    private readonly ConcurrentDictionary<string, CachedItem> items = new(StringComparer.OrdinalIgnoreCase);
    private long currentBytes;

    private sealed record CachedItem(byte[] Bytes, long Stamp);

    /// <summary>取已缓存的字节。</summary>
    public bool TryGet(string key, out byte[] bytes)
    {
        if (items.TryGetValue(key, out var item))
        {
            bytes = item.Bytes;

            return true;
        }

        bytes = [];

        return false;
    }

    /// <summary>存入字节，必要时逐出旧项。</summary>
    public void Store(string key, byte[] bytes)
    {
        Interlocked.Add(ref currentBytes, bytes.LongLength);

        items[key] = new CachedItem(bytes, DateTime.UtcNow.Ticks);

        if (currentBytes <= maxBytes)
            return;

        foreach (var victim in items.ToArray()
                     .OrderBy(i => i.Value.Stamp)
                     .Take(Math.Max(1, items.Count / 2)))
        {
            if (items.TryRemove(victim.Key, out var removed))
            {
                Interlocked.Add(ref currentBytes, -removed.Bytes.LongLength);
            }
        }
    }

    /// <summary>当前缓存字节数。</summary>
    public long CachedBytes => Interlocked.Read(ref currentBytes);
}
