using System.Globalization;

namespace Aura3D.Pipeline.PBR.Common;

internal static class PbrCommonResources
{
    private const string ResourcePrefix = "Aura3D.Pipeline.PBR.Common.Resources.";
    private const string MissingResourceMessage = "Embedded resource '{0}' was not found.";

    public static byte[] LutData { get; } = ReadBytes(nameof(LutData));

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
        return typeof(PbrCommonResources).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                string.Format(CultureInfo.InvariantCulture, MissingResourceMessage, resourceName));
    }
}