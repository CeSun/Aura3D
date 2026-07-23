using System.Globalization;

namespace Aura3D.Pipeline.PBRForward;

internal static class PbrForwardResources
{
    private const string ResourcePrefix = "Aura3D.Pipeline.PBRForward.Resources.";
    private const string MissingResourceMessage = "Embedded resource '{0}' was not found.";

    public static string MeshVertexShader { get; } = ReadText(nameof(MeshVertexShader));

    public static string LightingFragmentShader { get; } = ReadText(nameof(LightingFragmentShader));

    public static string IblAmbientFragmentShader { get; } = ReadText(nameof(IblAmbientFragmentShader));

    private static string ReadText(string name)
    {
        using var reader = new StreamReader(Open(name));
        return reader.ReadToEnd();
    }

    private static Stream Open(string name)
    {
        var resourceName = ResourcePrefix + name;
        return typeof(PbrForwardResources).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, MissingResourceMessage, resourceName));
    }
}