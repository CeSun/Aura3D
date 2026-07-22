namespace Aura3D.Core.Resources;

/// <summary>
/// Defines the contract for clone.
/// </summary>
public interface IClone<T> : IDeepClone<T> where T : IClone<T>
{
    /// <summary>
    /// Clones the associated data.
    /// </summary>
    public T Clone();
}

/// <summary>
/// Defines the contract for deep clone.
/// </summary>
public interface IDeepClone<T> where T : IDeepClone<T>
{
    /// <summary>
    /// Deep-clones the associated data.
    /// </summary>
    public T DeepClone();
}
