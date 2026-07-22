using System.Globalization;

namespace Aura3D.Core.Exceptions;

/// <summary>
/// Identifies a stable renderer failure independently of its display message.
/// </summary>
public enum RendererError
{
    /// <summary>OpenGL failed to create a complete framebuffer.</summary>
    FramebufferCreationFailed,

    /// <summary>An instanced mesh has no instanced geometry.</summary>
    MissingInstancedGeometry,

    /// <summary>A framebuffer was used before it was created.</summary>
    FramebufferNotCreated,

    /// <summary>A camera output texture is required but was not set.</summary>
    CameraOutputTextureNotSet,

    /// <summary>The scene has no default output surface.</summary>
    DefaultOutputSurfaceNotSet,

    /// <summary>A render-target handle belongs to another pipeline.</summary>
    RenderTargetOwnershipMismatch,

    /// <summary>The requested render target is not registered.</summary>
    RenderTargetNotFound,

    /// <summary>A render pass has no output target.</summary>
    RenderPassOutputNotSet,

    /// <summary>A vertex shader failed to compile.</summary>
    VertexShaderCompilationFailed,

    /// <summary>A fragment shader failed to compile.</summary>
    FragmentShaderCompilationFailed,

    /// <summary>A shader program failed to link.</summary>
    ShaderProgramLinkFailed,

    /// <summary>The CPU resource associated with a GPU state was collected.</summary>
    CpuResourceCollected,

    /// <summary>A render output does not refer to a registered render target.</summary>
    InvalidRenderOutput,

    /// <summary>The requested texture is not registered in a render target.</summary>
    TextureNotRegistered,
}

/// <summary>
/// Represents the renderer exception type.
/// </summary>
public sealed class RendererException : InvalidOperationException
{
    internal RendererException(
        RendererError code,
        string message,
        string? resourceName = null,
        string? details = null)
        : base(message)
    {
        Code = code;
        ResourceName = resourceName;
        Details = details;
    }

    /// <summary>Gets the language-independent error code.</summary>
    public RendererError Code { get; }

    /// <summary>Gets the related resource name, when available.</summary>
    public string? ResourceName { get; }

    /// <summary>Gets backend diagnostic details, when available.</summary>
    public string? Details { get; }
}

internal static class RendererErrors
{
    private const string GpuStateTypeMismatchMessage =
        "GPU state '{0}' is of an incompatible type and cannot be cast to '{1}'.";

    private const string FramebufferCreationFailedMessage =
        "Framebuffer creation failed with status '{0}'.";

    private const string MissingInstancedGeometryMessage =
        "The instanced mesh does not contain instanced geometry.";

    private const string FramebufferNotCreatedMessage =
        "The framebuffer for '{0}' was not created.";

    private const string CameraOutputTextureNotSetMessage =
        "The camera output texture is not set.";

    private const string DefaultOutputSurfaceNotSetMessage =
        "The scene default output surface is not set.";

    private const string RenderTargetOwnershipMismatchMessage =
        "The render target handle does not belong to the current render pipeline.";

    private const string RenderTargetNotFoundMessage =
        "Render target '{0}' is not registered in the current render pipeline.";

    private const string RenderPassOutputNotSetMessage =
        "The render pass output target is not set.";

    private const string ShaderCompilationFailedMessage =
        "The {0} shader failed to compile: {1}";

    private const string ShaderProgramLinkFailedMessage =
        "The shader program failed to link: {0}";

    private const string CpuResourceCollectedMessage =
        "The CPU resource for '{0}' has already been collected.";

    private const string InvalidRenderOutputMessage =
        "The current render output is not a registered render target.";

    private const string TextureAlreadyRegisteredMessage =
        "Texture '{0}' is already registered in the render target configuration.";

    private const string TextureNotRegisteredMessage =
        "Texture '{0}' is not registered in render target '{1}'.";

    private const string UnsupportedTextureFormatMessage =
        "Texture format '{0}' is not supported.";

    public static InvalidCastException GpuStateTypeMismatch(string name, Type targetType) =>
        new(Format(GpuStateTypeMismatchMessage, name, targetType.Name));

    public static RendererException FramebufferCreationFailed(object status, string? resourceName = null) =>
        Create(
            RendererError.FramebufferCreationFailed,
            Format(FramebufferCreationFailedMessage, status),
            resourceName,
            status.ToString());

    public static RendererException MissingInstancedGeometry() =>
        Create(RendererError.MissingInstancedGeometry, MissingInstancedGeometryMessage);

    public static RendererException FramebufferNotCreated(string resourceName) =>
        Create(
            RendererError.FramebufferNotCreated,
            Format(FramebufferNotCreatedMessage, resourceName),
            resourceName);

    public static RendererException CameraOutputTextureNotSet() =>
        Create(RendererError.CameraOutputTextureNotSet, CameraOutputTextureNotSetMessage);

    public static RendererException DefaultOutputSurfaceNotSet() =>
        Create(RendererError.DefaultOutputSurfaceNotSet, DefaultOutputSurfaceNotSetMessage);

    public static RendererException RenderTargetOwnershipMismatch() =>
        Create(RendererError.RenderTargetOwnershipMismatch, RenderTargetOwnershipMismatchMessage);

    public static RendererException RenderTargetNotFound(string name) =>
        Create(RendererError.RenderTargetNotFound, Format(RenderTargetNotFoundMessage, name), name);

    public static RendererException RenderPassOutputNotSet() =>
        Create(RendererError.RenderPassOutputNotSet, RenderPassOutputNotSetMessage);

    public static RendererException ShaderCompilationFailed(bool vertexShader, string details) =>
        Create(
            vertexShader ? RendererError.VertexShaderCompilationFailed : RendererError.FragmentShaderCompilationFailed,
            Format(ShaderCompilationFailedMessage, vertexShader ? "vertex" : "fragment", details),
            details: details);

    public static RendererException ShaderProgramLinkFailed(string details) =>
        Create(
            RendererError.ShaderProgramLinkFailed,
            Format(ShaderProgramLinkFailedMessage, details),
            details: details);

    public static RendererException CpuResourceCollected(string resourceName) =>
        Create(
            RendererError.CpuResourceCollected,
            Format(CpuResourceCollectedMessage, resourceName),
            resourceName);

    public static RendererException InvalidRenderOutput() =>
        Create(RendererError.InvalidRenderOutput, InvalidRenderOutputMessage);

    public static ArgumentException TextureAlreadyRegistered(string name, string paramName) =>
        new(Format(TextureAlreadyRegisteredMessage, name), paramName);

    public static RendererException TextureNotRegistered(string textureName, string renderTargetName) =>
        Create(
            RendererError.TextureNotRegistered,
            Format(TextureNotRegisteredMessage, textureName, renderTargetName),
            textureName);

    public static ArgumentOutOfRangeException UnsupportedTextureFormat(string paramName, object format) =>
        new(paramName, format, Format(UnsupportedTextureFormatMessage, format));

    private static RendererException Create(
        RendererError code,
        string message,
        string? resourceName = null,
        string? details = null) =>
        new(code, message, resourceName, details);

    private static string Format(string format, params object?[] args) =>
        string.Format(CultureInfo.InvariantCulture, format, args);
}
