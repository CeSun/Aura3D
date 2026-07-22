using System.Globalization;

namespace Aura3D.Model.Exceptions;

/// <summary>
/// Identifies a stable model-import failure independently of its display message.
/// </summary>
public enum ModelImportError
{
    /// <summary>An embedded texture contains no usable data.</summary>
    InvalidEmbeddedTexture,

    /// <summary>An imported mesh references a bone absent from the skeleton.</summary>
    SkeletonBoneNotFound,
}

/// <summary>
/// Represents invalid or inconsistent data encountered while importing a model.
/// </summary>
public sealed class ModelImportException : Exception
{
    internal ModelImportException(ModelImportError code, string message, string? resourceName = null)
        : base(message)
    {
        Code = code;
        ResourceName = resourceName;
    }

    /// <summary>Gets the language-independent error code.</summary>
    public ModelImportError Code { get; }

    /// <summary>Gets the related resource name, when available.</summary>
    public string? ResourceName { get; }
}

internal static class ModelImportErrors
{
    private const string InvalidEmbeddedTextureMessage =
        "The embedded texture contains neither compressed nor uncompressed data.";

    private const string SkeletonBoneNotFoundMessage =
        "Skeleton bone '{0}' was not found while importing the model.";

    public static ModelImportException InvalidEmbeddedTexture() =>
        new(ModelImportError.InvalidEmbeddedTexture, InvalidEmbeddedTextureMessage);

    public static ModelImportException SkeletonBoneNotFound(string boneName) =>
        new(
            ModelImportError.SkeletonBoneNotFound,
            string.Format(CultureInfo.InvariantCulture, SkeletonBoneNotFoundMessage, boneName),
            boneName);
}
