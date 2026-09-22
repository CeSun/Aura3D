using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Specifies the GLSL dialect family accepted by the active GL context.
/// </summary>
public enum ShaderDialect
{
    /// <summary>
    /// OpenGL ES. All bundled shaders are authored in this dialect.
    /// </summary>
    GlEs,
    /// <summary>
    /// Desktop OpenGL. Requires translation from the authored ES source.
    /// </summary>
    DesktopGl,
}

/// <summary>
/// Translates the authored OpenGL ES 3.0 shader sources into the desktop GLSL dialect.
/// </summary>
internal static class ShaderDialectConverter
{
    internal const string EsVersionDirective = "#version 300 es";

    internal const string DesktopVersionDirective = "#version 410 core";

    private static readonly Regex PrecisionDeclarationRegex = new(
        @"^[ \t]*precision[ \t]+(?:lowp|mediump|highp)[ \t]+\w+[ \t]*;[ \t]*(?://[^\r\n]*)?\r?$",
        RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// Detects the dialect of the current context from its reported GL version string.
    /// </summary>
    public static unsafe ShaderDialect Detect(GL gl)
    {
        var pointer = (nint)gl.GetString(GLEnum.Version);
        var version = pointer == 0 ? string.Empty : (Marshal.PtrToStringAnsi(pointer) ?? string.Empty);

        return version.StartsWith("OpenGL ES", StringComparison.OrdinalIgnoreCase)
            ? ShaderDialect.GlEs
            : ShaderDialect.DesktopGl;
    }

    /// <summary>
    /// Rewrites an ES source into desktop GLSL: swaps the version directive, removes the
    /// precision declarations desktop GLSL rejects, and drops the byte order mark so the
    /// version directive stays the first token in the source.
    /// </summary>
    public static string ToDesktopGlsl(string source)
    {
        if (source.Length == 0)
            return source;

        var text = source.StartsWith('\uFEFF') ? source[1..] : source;

        text = PrecisionDeclarationRegex.Replace(text, string.Empty);

        var start = 0;
        while (start < text.Length && char.IsWhiteSpace(text[start]))
            start++;

        if (text.AsSpan(start).StartsWith(EsVersionDirective, StringComparison.Ordinal))
        {
            text = $"{text[..start]}{DesktopVersionDirective}{text[(start + EsVersionDirective.Length)..]}";
        }
        else if (!text.AsSpan(start).StartsWith("#version", StringComparison.Ordinal))
        {
            text = $"{DesktopVersionDirective}\n{text}";
        }

        return text;
    }
}
