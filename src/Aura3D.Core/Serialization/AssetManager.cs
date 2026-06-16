namespace Aura3D.Core.Serialization;

/// <summary>
/// 资源归档入口。当前仅支持资源图序列化与反序列化，不支持场景。
/// </summary>
public class AssetManager
{
    public static void SaveNode<T>(T node, string path) where T : Nodes.Node
    {
        ArgumentNullException.ThrowIfNull(node);

        using var fileStream = File.Create(path);
        SaveNode(node, fileStream);
    }

    public static void SaveNode<T>(T node, Stream stream) where T : Nodes.Node
    {
        ArgumentNullException.ThrowIfNull(node);

        var collector = new NodeCollector();
        collector.Collect(node);

        var writer = new AuraNodeFileWriter(collector);
        writer.Write(stream);
    }

    public static T LoadNode<T>(string path) where T : Nodes.Node
    {
        using var fileStream = File.OpenRead(path);
        return LoadNode<T>(fileStream);
    }

    public static T LoadNode<T>(Stream stream) where T : Nodes.Node
    {
        var reader = new AuraNodeFileReader(stream);
        if (reader.RootNode is T node)
            return node;

        throw new InvalidDataException($"Root node type mismatch. Expected {typeof(T).FullName}, actual chunk type {reader.RootChunkType}.");
    }

    public static void SaveResource<T>(T resource, string path) where T : class
    {
        ArgumentNullException.ThrowIfNull(resource);

        using var fileStream = File.Create(path);
        SaveResource(resource, fileStream);
    }

    public static void SaveResource<T>(T resource, Stream stream) where T : class
    {
        ArgumentNullException.ThrowIfNull(resource);

        var collector = new ResourceCollector();
        collector.Collect(resource);

        var writer = new AuraFileWriter(collector);
        writer.Write(stream);
    }

    public static T LoadResource<T>(string path) where T : class
    {
        using var fileStream = File.OpenRead(path);
        return LoadResource<T>(fileStream);
    }

    public static T LoadResource<T>(Stream stream) where T : class
    {
        var reader = new AuraFileReader(stream);
        if (reader.RootResource is T resource)
            return resource;

        throw new InvalidDataException($"Root resource type mismatch. Expected {typeof(T).FullName}, actual chunk type {reader.RootChunkType}.");
    }

    public static void Save(Scenes.Scene scene, string path)
    {
        throw new NotSupportedException("Scene serialization is not supported yet. Use SaveResource for standalone resources.");
    }

    public static void Save(Scenes.Scene scene, Stream stream)
    {
        throw new NotSupportedException("Scene serialization is not supported yet. Use SaveResource for standalone resources.");
    }

    public static Scenes.Scene Load(string path, Func<Scenes.Scene, Renderers.RenderPipeline> createRenderPipeline)
    {
        throw new NotSupportedException("Scene deserialization is not supported yet. Use LoadResource for standalone resources.");
    }

    public static Scenes.Scene Load(Stream stream, Func<Scenes.Scene, Renderers.RenderPipeline> createRenderPipeline)
    {
        throw new NotSupportedException("Scene deserialization is not supported yet. Use LoadResource for standalone resources.");
    }
}
