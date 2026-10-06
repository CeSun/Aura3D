using System.Globalization;

namespace Aura3D.Model.Exceptions;

/// <summary>
/// Identifies a stable FBX-import failure independently of its display message.
/// </summary>
/// <remarks>
/// Named apart from AssimpLoader's <c>ModelImportError</c>: both packages can legitimately be
/// referenced by the same application, and identical type names in one namespace would make every
/// reference ambiguous for the compiler.
/// </remarks>
public enum FbxImportError
{
    /// <summary>The file could not be parsed as FBX.</summary>
    FailedToLoadFbx,

    /// <summary>An imported mesh references a bone absent from the skeleton.</summary>
    SkeletonBoneNotFound,
}

/// <summary>
/// Represents invalid or inconsistent data encountered while importing an FBX model.
/// </summary>
public sealed class FbxImportException : Exception
{
    internal FbxImportException(FbxImportError code, string message, string? resourceName = null)
        : base(message)
    {
        Code = code;
        ResourceName = resourceName;
    }

    /// <summary>Gets the language-independent error code.</summary>
    public FbxImportError Code { get; }

    /// <summary>Gets the related resource name, when available.</summary>
    public string? ResourceName { get; }
}

internal static class FbxImportErrors
{
    private const string FailedToLoadFbxMessage = "Failed to load the FBX file: {0}";

    private const string SkeletonBoneNotFoundMessage =
        "Skeleton bone '{0}' was not found while importing the model.";

    public static FbxImportException FailedToLoadFbx(string description) =>
        new(
            FbxImportError.FailedToLoadFbx,
            string.Format(CultureInfo.InvariantCulture, FailedToLoadFbxMessage, description));

    public static FbxImportException SkeletonBoneNotFound(string boneName) =>
        new(
            FbxImportError.SkeletonBoneNotFound,
            string.Format(CultureInfo.InvariantCulture, SkeletonBoneNotFoundMessage, boneName),
            boneName);
}
