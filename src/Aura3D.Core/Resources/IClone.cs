namespace Aura3D.Core.Resources;

public interface IClone<T> : IDeepClone<T> where T : IClone<T>
{
    public T Clone();
}

public interface IDeepClone<T> where T : IDeepClone<T>
{
    public T DeepClone();
}
