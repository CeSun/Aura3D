using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers;

internal class InternalQuad : IRuntimeGpuState
{
    public ulong Version { get; } = 1;
    public ulong SyncedVersion { get; private set; }

    public uint Vao;
    public uint Vbo;
    public uint Ebo;

    private struct QuadVertex
    {
        public Vector3 Location;
        public Vector2 TexCoord;
    }

    public void Destroy(GL gl)
    {
        if (Vao != 0)
        {
            gl.DeleteVertexArray(Vao);
            Vao = 0;
        }
        if (Vbo != 0)
        {
            gl.DeleteBuffer(Vbo);
            Vbo = 0;
        }
        if (Ebo != 0)
        {
            gl.DeleteBuffer(Ebo);
            Ebo = 0;
        }
        SyncedVersion = 0;
    }

    public unsafe void Upload(GL gl)
    {
        QuadVertex* vertices = stackalloc QuadVertex[4]
        {
            new () { Location = new Vector3(-1, 1, 0), TexCoord = new Vector2(0, 1) },
            new () { Location = new Vector3(-1, -1, 0), TexCoord = new Vector2(0, 0) },
            new () { Location = new Vector3(1, -1, 0), TexCoord = new Vector2(1, 0) },
            new () { Location = new Vector3(1, 1, 0), TexCoord = new Vector2(1, 1) },
        };

        uint* indices = stackalloc uint[6]
        {
            0, 1, 2, 2, 3, 0
        };

        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();
        Ebo = gl.GenBuffer();

        gl.BindVertexArray(Vao);
        gl.BindBuffer(GLEnum.ArrayBuffer, Vbo);
        gl.BufferData(GLEnum.ArrayBuffer, (nuint)(4 * sizeof(QuadVertex)), vertices, GLEnum.StaticDraw);

        gl.BindBuffer(GLEnum.ElementArrayBuffer, Ebo);
        gl.BufferData(GLEnum.ElementArrayBuffer, 6 * sizeof(uint), indices, GLEnum.StaticDraw);

        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)sizeof(QuadVertex), (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, GLEnum.Float, false, (uint)sizeof(QuadVertex), (void*)sizeof(Vector3));
        gl.BindVertexArray(0);
        SyncedVersion = Version;
    }
}
