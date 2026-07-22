using System.Globalization;

namespace Aura3D.Pipeline.CelShading;

internal static class CelShadingResources
{
    private const string ResourcePrefix = "Aura3D.Pipeline.CelShading.Resources.";
    private const string MissingResourceMessage = "Embedded resource '{0}' was not found.";

    public static string MeshVertexShader { get; } = ReadText(nameof(MeshVertexShader));

    public static string CelFragmentShader { get; } = ReadText(nameof(CelFragmentShader));

    public static string OutlineVertexShader { get; } = ReadText(nameof(OutlineVertexShader));

    public static string OutlineFragmentShader { get; } = ReadText(nameof(OutlineFragmentShader));

    public static byte[] CelRampData { get; } = ReadBytes(nameof(CelRampData));

    public static byte[] CelRamp2Data { get; } = ReadBytes(nameof(CelRamp2Data));

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
        return typeof(CelShadingResources).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, MissingResourceMessage, resourceName));
    }
}
