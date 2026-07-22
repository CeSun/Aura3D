using System.Globalization;

namespace Aura3D.Pipeline.PBR;

internal static class PbrResources
{
    private const string ResourcePrefix = "Aura3D.Pipeline.PBR.Resources.";
    private const string MissingResourceMessage = "Embedded resource '{0}' was not found.";

    public static string MeshVertexShader { get; } = ReadText(nameof(MeshVertexShader));

    public static string DeferredMeshFragmentShader { get; } = ReadText(nameof(DeferredMeshFragmentShader));

    public static string LightingVertexShader { get; } = ReadText(nameof(LightingVertexShader));

    public static string LightingFragmentShader { get; } = ReadText(nameof(LightingFragmentShader));

    public static string ConstantAmbientFragmentShader { get; } = ReadText(nameof(ConstantAmbientFragmentShader));

    public static string IblAmbientFragmentShader { get; } = ReadText(nameof(IblAmbientFragmentShader));

    public static byte[] LutData { get; } = ReadBytes(nameof(LutData));

    private static string ReadText(string name)
    {
        using var reader = new StreamReader(Open(name));
        return reader.ReadToEnd();
    }

    private static byte[] ReadBytes(string name)
    {
        using var stream = Open(name);
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    private static Stream Open(string name)
    {
        var resourceName = ResourcePrefix + name;
        return typeof(PbrResources).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, MissingResourceMessage, resourceName));
    }
}
