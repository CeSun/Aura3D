  using System.Numerics;
using Aura3D.Core.Math;

namespace Aura3D.Core.Resources;

/// <summary>
/// Represents the animation type.
/// </summary>
public class Animation
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the duration.
    /// </summary>
    public float Duration { get; set; } // in seconds

    /// <summary>
    /// Gets the channels.
    /// </summary>
    public Dictionary<string, AnimationChannel> Channels { get; } = new();

    /// <summary>
    /// Gets or sets the skeleton.
    /// </summary>
    public Skeleton? Skeleton { get; set; }
    /// <summary>
    /// Samples the associated data.
    /// </summary>
    public Matrix4x4 Sample(string channelName, float time)
    {
        if (!Channels.TryGetValue(channelName, out var channel))
        {
            var bone = Skeleton!.Bones.Find(b => b.Name == channelName);

            return bone!.LocalMatrix;
        }

        var position = channel.PositionKeyframes.GetValueByTime(time, SamplerHelper.Lerp);

        var rotation = channel.RotationKeyframes.GetValueByTime(time, SamplerHelper.Slerp);

        var scale = channel.ScaleKeyframes.GetValueByTime(time, SamplerHelper.Lerp);

        return MatrixHelper.CreateTransform(position, rotation, scale);
    }
}

/// <summary>
/// Represents the animation channel type.
/// </summary>
public class AnimationChannel
{
    /// <summary>
    /// Gets the position keyframes.
    /// </summary>
    public List<Keyframe<Vector3>> PositionKeyframes { get; } = new();
    /// <summary>
    /// Gets the rotation keyframes.
    /// </summary>
    public List<Keyframe<Quaternion>> RotationKeyframes { get; } = new();
    /// <summary>
    /// Gets the scale keyframes.
    /// </summary>
    public List<Keyframe<Vector3>> ScaleKeyframes { get; } = new();

}
/// <summary>
/// Represents the keyframe type.
/// </summary>
public struct Keyframe<T> where T : struct
{
    /// <summary>
    /// Gets or sets the time.
    /// </summary>
    public float Time { get; set; }
    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public T Value { get; set; }
}


/// <summary>
/// Represents the sampler helper type.
/// </summary>
public static class SamplerHelper
{
    /// <summary>
    /// Represents the get value by time type.
    /// </summary>
    public static T GetValueByTime<T>(this IReadOnlyList<Keyframe<T>> list, float time, Func<Keyframe<T>, Keyframe<T>, float, T> lerpFunc) where T : struct
    {
        if (list.Count == 0)
            throw Aura3D.Core.Exceptions.AnimationErrors.EmptyKeyframeList();

        if (list.Count == 1)
            return list[0].Value;
        if (time <= list[0].Time)
            return list[0].Value;
        if (time >= list[^1].Time)
            return list[^1].Value;
        for (int i = 0; i < list.Count - 1; i++)
        {
            if (time >= list[i].Time && time <= list[i + 1].Time)
            {
                return lerpFunc(list[i], list[i + 1], time);
            }
        }

        // Fallback: find nearest keyframe (reached due to float precision gap)
        float minDist = float.MaxValue;
        T nearest = list[0].Value;
        for (int i = 0; i < list.Count; i++)
        {
            float dist = MathF.Abs(time - list[i].Time);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = list[i].Value;
            }
        }
        return nearest;
    }


    /// <summary>
    /// Performs the lerp operation.
    /// </summary>
    public static float Lerp(Keyframe<float> left, Keyframe<float> right, float time)
    {
        float t = (time - left.Time) / (right.Time - left.Time);
        float v0 = left.Value;
        float v1 = right.Value;
        return v0 + t * (v1 - v0);
    }

    /// <summary>
    /// Performs the lerp operation.
    /// </summary>
    public static Vector3 Lerp(Keyframe<Vector3> left, Keyframe<Vector3> right, float time)
    {
        float t = (time - left.Time) / (right.Time - left.Time);
        Vector3 v0 = left.Value;
        Vector3 v1 = right.Value;
        return Vector3.Lerp(v0, v1, t);
    }

    /// <summary>
    /// Performs the slerp operation.
    /// </summary>
    public static Quaternion Slerp(Keyframe<Quaternion> left, Keyframe<Quaternion> right, float time)
    {
        float t = (time - left.Time) / (right.Time - left.Time);
        Quaternion q0 = left.Value;
        Quaternion q1 = right.Value;
        return Quaternion.Slerp(q0, q1, t);
    }
}
