using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Renderers;

internal class GeometryGpuState : IResourceGpuState<Geometry>
{
    private readonly WeakReference<Geometry> geometry;
    private readonly List<uint> vboIds = [];

    public GeometryGpuState(Geometry geometry)
    {
        this.geometry = new WeakReference<Geometry>(geometry);
    }

    public Geometry Resource
    {
        get
        {
            if (geometry.TryGetTarget(out var value))
                return value;

            throw Aura3D.Core.Exceptions.RendererErrors.CpuResourceCollected(nameof(Geometry));
        }
    }

    public bool IsAlive => geometry.TryGetTarget(out _);
    public ulong Version => Resource.Version;
    public ulong SyncedVersion { get; protected set; }

    public uint Vao { get; protected set; }

    public uint Ebo { get; protected set; }

    public virtual void Destroy(GL gl)
    {
        foreach (var vbo in vboIds)
        {
            gl.DeleteBuffer(vbo);
        }
        vboIds.Clear();

        if (Ebo != 0)
        {
            gl.DeleteBuffer(Ebo);
            Ebo = 0;
        }

        if (Vao != 0)
        {
            gl.DeleteVertexArray(Vao);
            Vao = 0;
        }
    }

    public virtual unsafe void Upload(GL gl)
    {
        var geometry = Resource;

        if (Vao == 0)
        {
            Vao = gl.GenVertexArray();
        }
        else
        {
            foreach (var vbo in vboIds)
            {
                gl.DeleteBuffer(vbo);
            }
            vboIds.Clear();
        }

        gl.BindVertexArray(Vao);

        foreach (var (_, attribute) in geometry.VertexAttributes)
        {
            if (!attribute.Enabled)
                continue;

            uint vbo = gl.GenBuffer();
            vboIds.Add(vbo);

            gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);

            fixed (float* dataPtr = CollectionsMarshal.AsSpan(attribute.Data))
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(attribute.Data.Count * sizeof(float)), dataPtr, GLEnum.StaticDraw);
            }

            gl.EnableVertexAttribArray(attribute.Location);
            gl.VertexAttribPointer(attribute.Location, attribute.Size, GLEnum.Float, false, (uint)(sizeof(float) * attribute.Size), (void*)0);
        }

        var indices = geometry.GetIndicesBuffer();
        if (indices.Count > 0)
        {
            if (Ebo == 0)
            {
                Ebo = gl.GenBuffer();
            }

            gl.BindBuffer(GLEnum.ElementArrayBuffer, Ebo);

            fixed (uint* indexPtr = CollectionsMarshal.AsSpan(indices))
            {
                gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(indices.Count * sizeof(uint)), indexPtr, GLEnum.StaticDraw);
            }
        }

        SyncedVersion = geometry.Version;
    }
}
