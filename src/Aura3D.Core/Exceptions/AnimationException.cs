namespace Aura3D.Core.Exceptions;

/// <summary>
/// Identifies a stable animation failure independently of its display message.
/// </summary>
public enum AnimationError
{
    /// <summary>The animation contains no keyframes to sample.</summary>
    EmptyKeyframeList,

    /// <summary>An animation graph node references itself as a transition target.</summary>
    GraphSelfReference,
}

/// <summary>
/// Represents an animation invariant violation.
/// </summary>
public sealed class AnimationException : InvalidOperationException
{
    internal AnimationException(AnimationError code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Gets the language-independent error code.</summary>
    public AnimationError Code { get; }
}

internal static class AnimationErrors
{
    private const string EmptyKeyframeListMessage =
        "The keyframe list is empty.";

    private const string GraphSelfReferenceMessage =
        "An animation graph node cannot reference itself as a next node.";

    private const string BlendPointXOutOfRangeMessage =
        "The animation blend-space point X value must be in the range [-1, 1].";

    private const string BlendPointYOutOfRangeMessage =
        "The animation blend-space point Y value must be in the range [-1, 1].";

    private const string BlendAxisOutOfRangeMessage =
        "Animation blend-space axis values must be in the range [-1, 1].";

    public static AnimationException EmptyKeyframeList() =>
        new(AnimationError.EmptyKeyframeList, EmptyKeyframeListMessage);

    public static AnimationException GraphSelfReference() =>
        new(AnimationError.GraphSelfReference, GraphSelfReferenceMessage);

    public static ArgumentOutOfRangeException BlendPointXOutOfRange(string paramName) =>
        new(paramName, BlendPointXOutOfRangeMessage);

    public static ArgumentOutOfRangeException BlendPointYOutOfRange(string paramName) =>
        new(paramName, BlendPointYOutOfRangeMessage);

    public static ArgumentOutOfRangeException BlendAxisOutOfRange(string paramName) =>
        new(paramName, BlendAxisOutOfRangeMessage);
}
