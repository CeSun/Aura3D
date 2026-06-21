using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Renderers;

internal sealed class InstancedGeometryGpuState : GeometryGpuState
{
    private readonly List<uint> instanceVboIds = [];

    public InstancedGeometryGpuState(InstancedGeometry geometry)
        : base(geometry)
    {
    }

    private InstancedGeometry InstancedGeometry => (InstancedGeometry)Resource;

    public override void Destroy(GL gl)
    {
        foreach (var vbo in instanceVboIds)
        {
            gl.DeleteBuffer(vbo);
        }
        instanceVboIds.Clear();

        base.Destroy(gl);
    }

    public override unsafe void Upload(GL gl)
    {
        base.Upload(gl);

        foreach (var vbo in instanceVboIds)
        {
            gl.DeleteBuffer(vbo);
        }
        instanceVboIds.Clear();

        gl.BindVertexArray(Vao);

        foreach (var attr in InstancedGeometry.InstanceAttributes.Values)
        {
            if (!attr.Enabled)
                continue;

            uint vbo = gl.GenBuffer();
            instanceVboIds.Add(vbo);

            gl.BindBuffer(GLEnum.ArrayBuffer, vbo);

            fixed (float* p = CollectionsMarshal.AsSpan(attr.DataBuffer))
            {
                gl.BufferData(GLEnum.ArrayBuffer, (nuint)(attr.DataBuffer.Count * sizeof(float)), p, GLEnum.DynamicDraw);
            }

            foreach (var ptr in attr.PointersBuffer)
            {
                gl.EnableVertexAttribArray(ptr.Location);
                gl.VertexAttribPointer(ptr.Location, ptr.ComponentCount, GLEnum.Float, false, (uint)attr.Stride, (void*)ptr.Offset);
                gl.VertexAttribDivisor(ptr.Location, 1);
            }
        }

        gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        gl.BindVertexArray(0);
    }
}
