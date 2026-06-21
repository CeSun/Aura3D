using Silk.NET.OpenGLES;

namespace Aura3D.Core.Renderers;

/// <summary>
/// Shared quad geometry for particle billboard rendering.
/// Owned by each ParticlePass instance so multiple controls/render pipelines work correctly.
/// </summary>
internal unsafe class ParticleQuadGeometry : IRuntimeGpuState
{
    public ulong Version { get; } = 1;
    public ulong SyncedVersion { get; private set; }

    public uint QuadVbo { get; private set; }
    public uint QuadEbo { get; private set; }

    public void Upload(GL gl)
    {
        QuadVbo = gl.GenBuffer();
        QuadEbo = gl.GenBuffer();

        float[] vertices =
        [
            -0.5f, -0.5f, 0f,  0f, 1f,
             0.5f, -0.5f, 0f,  1f, 1f,
             0.5f,  0.5f, 0f,  1f, 0f,
            -0.5f,  0.5f, 0f,  0f, 0f,
        ];
        uint[] indices = [0, 1, 2, 2, 3, 0];

        gl.BindBuffer(GLEnum.ArrayBuffer, QuadVbo);
        fixed (float* p = vertices)
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)(vertices.Length * sizeof(float)), p, GLEnum.StaticDraw);

        // Use temp VAO so EBO binding doesn't pollute other VAOs
        uint tempVao = gl.GenVertexArray();
        gl.BindVertexArray(tempVao);
        gl.BindBuffer(GLEnum.ElementArrayBuffer, QuadEbo);
        fixed (uint* p = indices)
            gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(indices.Length * sizeof(uint)), p, GLEnum.StaticDraw);
        gl.BindVertexArray(0);
        gl.DeleteVertexArray(tempVao);

        gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        SyncedVersion = Version;
    }

    public void Destroy(GL gl)
    {
        if (QuadVbo != 0) { gl.DeleteBuffer(QuadVbo); QuadVbo = 0; }
        if (QuadEbo != 0) { gl.DeleteBuffer(QuadEbo); QuadEbo = 0; }
        SyncedVersion = 0;
    }
}
