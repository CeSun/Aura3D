  using System.Numerics;
using Aura3D.Core.Math;

namespace Aura3D.Core.Resources;

public class Animation
{
    public string Name = string.Empty;
    
    public float Duration; // in seconds
    
    public Dictionary<string, AnimationChannel> Channels = new();

    public Skeleton? Skeleton;
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

public class AnimationChannel
{
    public List<Keyframe<Vector3>> PositionKeyframes = new();
    public List<Keyframe<Quaternion>> RotationKeyframes = new();
    public List<Keyframe<Vector3>> ScaleKeyframes = new();

}
public struct Keyframe<T> where T : struct
{
    public float Time;
    public T Value;
}


public static class SamplerHelper
{
    public static T GetValueByTime<T>(this IReadOnlyList<Keyframe<T>> list, float time, Func<Keyframe<T>, Keyframe<T>, float, T> lerpFunc) where T : struct
    {
        if (list.Count == 0)
            throw new Exception("The keyframe list is empty.");

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

        throw new Exception("Time value is out of range.");
    }


    public static float Lerp(Keyframe<float> left, Keyframe<float> right, float time)
    {
        float t = (time - left.Time) / (right.Time - left.Time);
        float v0 = left.Value;
        float v1 = right.Value;
        return v0 + t * (v1 - v0);
    }

    public static Vector3 Lerp(Keyframe<Vector3> left, Keyframe<Vector3> right, float time)
    {
        float t = (time - left.Time) / (right.Time - left.Time);
        Vector3 v0 = left.Value;
        Vector3 v1 = right.Value;
        return Vector3.Lerp(v0, v1, t);
    }

    public static Quaternion Slerp(Keyframe<Quaternion> left, Keyframe<Quaternion> right, float time)
    {
        float t = (time - left.Time) / (right.Time - left.Time);
        Quaternion q0 = left.Value;
        Quaternion q1 = right.Value;
        return Quaternion.Slerp(q0, q1, t);
    }
}