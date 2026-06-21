using Aura3D.Core.Resources;
using Aura3D.Core.Renderers;
using Silk.NET.OpenGLES;

namespace Aura3D.Core.Particles;

/// <summary>
/// Manages GPU buffers for particle instanced rendering.
/// Single interleaved VBO per ParticleEmitter, with grow-only capacity.
/// Implements IGpuState and is uploaded lazily on first use.
/// </summary>
public unsafe class ParticleGpuBuffer : IRuntimeGpuState
{
    public ulong Version { get; private set; } = 1;
    public ulong SyncedVersion { get; private set; }

    /// <summary>Floats per instance: posRot(4) + color(4) + sizeAge(2) = 10</summary>
    private const int FloatsPerInstance = 10;
    private const int BytesPerInstance = FloatsPerInstance * sizeof(float);

    private uint _instanceVbo;
    private int _bufferCapacityInstances;
    private int _activeCount;
    private bool _dataDirty;

    /// <summary>
    /// Shared quad geometry for billboard rendering. Set by ParticlePass before upload.
    /// Each ParticlePass instance owns its own quad geometry so multiple controls work correctly.
    /// </summary>
    internal ParticleQuadGeometry? QuadGeometry { get; set; }

    /// <summary>
    /// Per-emitter VAO that combines the shared quad geometry with this emitter's instance VBO.
    /// </summary>
    private uint _vao;

    /// <summary>
    /// Pack particle CPU data into internal buffer. No GL calls.
    /// Called by ParticleSystem.Simulate() after simulation and by ParticlePass before rendering.
    /// </summary>
    public void SetParticleData(ParticleData[] particles, int activeCount)
    {
        _activeCount = activeCount;

        if (activeCount > 0)
        {
            PackInstanceData(particles, activeCount);
            _dataDirty = true;
        }
        Version++;
    }

    /// <summary>
    /// Upload — called by the render pipeline to upload data to GPU.
    /// All GL operations happen here.
    /// </summary>
    public void Upload(GL gl)
    {
        EnsureBuffers(gl, _activeCount);
        EnsureVao(gl);

        if (_dataDirty && _activeCount > 0)
        {
            UploadInstanceData(gl);
            _dataDirty = false;
        }
        SyncedVersion = Version;
    }

    /// <summary>
    /// Draw the particles. Called by ParticlePass during rendering.
    /// Assumes Upload has already been called by the pipeline.
    /// </summary>
    public void Draw(GL gl)
    {
        if (_activeCount == 0 || _vao == 0) return;
        gl.BindVertexArray(_vao);
        gl.DrawElementsInstanced(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0, (uint)_activeCount);
        gl.BindVertexArray(0);
    }

    public void Destroy(GL gl)
    {
        if (_vao != 0) { gl.DeleteVertexArray(_vao); _vao = 0; }
        if (_instanceVbo != 0) { gl.DeleteBuffer(_instanceVbo); _instanceVbo = 0; }
        _bufferCapacityInstances = 0;
        _activeCount = 0;
        SyncedVersion = 0;
    }

    // ---- Internal ----

    private float[]? _packedData;

    private void PackInstanceData(ParticleData[] particles, int activeCount)
    {
        if (_packedData == null || _packedData.Length < activeCount * FloatsPerInstance)
            _packedData = new float[activeCount * FloatsPerInstance];

        for (int i = 0; i < activeCount; i++)
        {
            ref var p = ref particles[i];
            int o = i * FloatsPerInstance;
            _packedData[o + 0] = p.Position.X;
            _packedData[o + 1] = p.Position.Y;
            _packedData[o + 2] = p.Position.Z;
            _packedData[o + 3] = p.Rotation;
            _packedData[o + 4] = p.CurrentColor.X;
            _packedData[o + 5] = p.CurrentColor.Y;
            _packedData[o + 6] = p.CurrentColor.Z;
            _packedData[o + 7] = p.CurrentColor.W;
            _packedData[o + 8] = p.CurrentSize;
            _packedData[o + 9] = p.AgeRatio;
        }
    }

    private void UploadInstanceData(GL gl)
    {
        if (_packedData == null) return;
        int count = _activeCount * FloatsPerInstance;
        gl.BindBuffer(GLEnum.ArrayBuffer, _instanceVbo);
        fixed (float* ptr = _packedData)
        {
            gl.BufferSubData(GLEnum.ArrayBuffer, 0, (nuint)(count * sizeof(float)), ptr);
        }
        gl.BindBuffer(GLEnum.ArrayBuffer, 0);
    }

    private void EnsureBuffers(GL gl, int requiredInstances)
    {
        if (requiredInstances <= _bufferCapacityInstances && _instanceVbo != 0) return;

        int newCapacity = System.Math.Max(requiredInstances, 256);
        while (newCapacity < requiredInstances) newCapacity *= 2;

        if (_instanceVbo == 0)
            _instanceVbo = gl.GenBuffer();

        gl.BindBuffer(GLEnum.ArrayBuffer, _instanceVbo);
        gl.BufferData(GLEnum.ArrayBuffer, (nuint)(newCapacity * BytesPerInstance), (void*)0, GLEnum.DynamicDraw);
        gl.BindBuffer(GLEnum.ArrayBuffer, 0);

        _bufferCapacityInstances = newCapacity;
        if (_vao != 0) { gl.DeleteVertexArray(_vao); _vao = 0; }
    }

    private void EnsureVao(GL gl)
    {
        if (_vao != 0 || QuadGeometry == null) return;

        _vao = gl.GenVertexArray();
        gl.BindVertexArray(_vao);

        // Bind shared quad geometry
        gl.BindBuffer(GLEnum.ArrayBuffer, QuadGeometry.QuadVbo);
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, GLEnum.Float, false, sizeof(float) * 5, (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, GLEnum.Float, false, sizeof(float) * 5, (void*)(3 * sizeof(float)));

        gl.BindBuffer(GLEnum.ElementArrayBuffer, QuadGeometry.QuadEbo);

        // Bind instance VBO with interleaved attributes
        gl.BindBuffer(GLEnum.ArrayBuffer, _instanceVbo);

        // location 2: vec4 instancePosRot
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 4, GLEnum.Float, false, BytesPerInstance, (void*)0);
        gl.VertexAttribDivisor(2, 1);

        // location 3: vec4 instanceColor
        gl.EnableVertexAttribArray(3);
        gl.VertexAttribPointer(3, 4, GLEnum.Float, false, BytesPerInstance, (void*)(4 * sizeof(float)));
        gl.VertexAttribDivisor(3, 1);

        // location 4: vec2 instanceSizeAge
        gl.EnableVertexAttribArray(4);
        gl.VertexAttribPointer(4, 2, GLEnum.Float, false, BytesPerInstance, (void*)(8 * sizeof(float)));
        gl.VertexAttribDivisor(4, 1);

        gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        gl.BindVertexArray(0);
    }

}
