using Aura3D.Core.Renderers;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Resources;

public interface IGpuResource
{
    public  bool NeedsUpload { get; set; }

    public void Upload(GL gl);

    public void Destroy(GL gl);

}

public interface IClone<T> : IDeepClone<T> where T : IClone<T>
{
    public T Clone();
}


public interface IDeepClone<T> where T : IDeepClone<T>
{
    public T DeepClone();
}