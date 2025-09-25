using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Numerics;

namespace Aura3D.Core.Renderers;

public abstract partial class RenderPipeline
{
    public List<Mesh> Meshes { get; } = new List<Mesh>();

    public List<Camera> Cameras { get; } = new List<Camera>();

    public List<PointLight> PointLights { get; } = new List<PointLight>();

    public List<SpotLight> SpotLights { get; } = new List<SpotLight>();

    public List<DirectionalLight> DirectionalLights { get; } = new List<DirectionalLight>();

    public uint DefaultFramebuffer { get; set; }

    public GL? gl { get; protected set; }
    public List<RenderPass> EveryCameraRenderPasses { get; } = new List<RenderPass>();

    public List<RenderPass> OnceRenderPasses { get; } = new List<RenderPass>();

    public HashSet<IGpuResource> GpuResources { get; } = new HashSet<IGpuResource>();

    public HashSet<IGpuResource> NeedUpdateResources { get; } = new HashSet<IGpuResource>();


    public int DirectionalLightLimit { get; set; } = 4;

    public int PointLightLimit { get; set; } = 4;

    public int SpotLightLimit { get; set; } = 4;

    private int lastDirectionalLightLimit;

    private int lastPointLightLimit;

    private int lastSpotLightLimit;

    protected event Action<int, int, int>? lightLimitChangedEvent;

    protected void RegisterRenderPass(RenderPass renderPass, RenderPassGroup renderPassGroup)
    {
        if (renderPassGroup == RenderPassGroup.EveryCamera)
            EveryCameraRenderPasses.Add(renderPass);
        else if (renderPassGroup == RenderPassGroup.Once)
            OnceRenderPasses.Add(renderPass);
    }

    public enum RenderPassGroup
    {
        Once,
        EveryCamera,
    }

    public void Initialize(Func<string, nint> getProcAddressFunctionPtr)
    {
        gl = GL.GetApi(getProcAddressFunctionPtr);

        foreach(var renderPass in EveryCameraRenderPasses)
        {
            renderPass.Setup();
        }
        foreach (var renderPass in OnceRenderPasses)
        {
            renderPass.Setup();
        }
    }

    public void UpdateGpuResources()
    {
        foreach (var gpuResource in GpuResources)
        {
            if (gpuResource.NeedsUpload == true)
            {
                gpuResource.Upload(gl!);
                gpuResource.NeedsUpload = false;
            }
        }
    }


    public void AddGpuResource(IGpuResource gpuResource)
    {
        if (GpuResources.Contains(gpuResource))
            return;
        GpuResources.Add(gpuResource);
    }

    public void AddNode(Node node)
    {
        switch (node)
        {
            case Mesh mesh:
                Meshes.Add(mesh);
                break;
            case Camera camera:
                Cameras.Add(camera);
                break;
            case PointLight pointLight:
                PointLights.Add(pointLight);
                break;
            case SpotLight spotLight:
                SpotLights.Add(spotLight);
                break;
            case DirectionalLight directionalLight:
                DirectionalLights.Add(directionalLight);
                break;
        }

        foreach (var gpuResource in node.GetGpuResources())
        {
            AddGpuResource(gpuResource);
        }

    }

    public void RemoveNode(Node node)
    {
        switch (node)
        {
            case Mesh mesh:
                Meshes.Remove(mesh);
                break;
            case Camera camera:
                Cameras.Remove(camera);
                break;
            case PointLight pointLight:
                PointLights.Remove(pointLight);
                break;
            case SpotLight spotLight:
                SpotLights.Remove(spotLight);
                break;
            case DirectionalLight directionalLight:
                DirectionalLights.Remove(directionalLight);
                break;
        }
    }

    private void UpdateLightLimit()
    {
        if (lastPointLightLimit != PointLightLimit || lastSpotLightLimit != SpotLightLimit || lastDirectionalLightLimit != DirectionalLightLimit)
        {
            lastPointLightLimit = PointLightLimit;
            lastSpotLightLimit = SpotLightLimit;
            lastDirectionalLightLimit = DirectionalLightLimit;
            lightLimitChangedEvent?.Invoke(lastDirectionalLightLimit, lastPointLightLimit, lastSpotLightLimit);
        }
    }

    public virtual void Render()
    {
        UpdateRenderTargetsLRU();
        UpdateLightLimit();
        UpdateGpuResources();

        BeforeRender();
        foreach (var renderPass in OnceRenderPasses)
        {
            renderPass.BeforeRender();
            renderPass.Render();
            renderPass.AfterRender();
        }

        foreach (var camera in Cameras)
        {
            if (camera.Enable == false)
                continue;
            BeforeCameraRender(camera);
            foreach (var renderPass in EveryCameraRenderPasses)
            {
                renderPass.BeforeRender(camera);
                renderPass.Render(camera);
                renderPass.AfterRender(camera);
            }
            AfterRender();
        }
        AfterRender();
    }

    public virtual void BeforeRender()
    {

    }

    public virtual void AfterRender()
    {

    }


    public virtual void BeforeCameraRender(Camera camera)
    {


    }

    public virtual void AfterCameraRender(Camera camera)
    {
    }

    public virtual void SortMeshes(List<Mesh> Meshes, Camera camera)
    {
        var m = camera.View;

        Meshes.Sort((mesh1, mesh2) =>
        {
            var location1 = Vector3.Transform(mesh1.Position, mesh1.WorldTransform * m);

            var location2 = Vector3.Transform(mesh2.Position, mesh2.WorldTransform * m);

            var l1 = location1.Length();

            var l2 = location2.Length();

            return (l1).CompareTo(l2);

        });
    }

    private InternalCube? _internalCube;

    private InternalQuad? _internalQuad;
    public void RenderCube()
    {
        if (gl == null)
            return;
        if (_internalCube == null)
        {
            _internalCube = new InternalCube();
            _internalCube.Upload(gl);
            _internalCube.NeedsUpload = false;
            GpuResources.Add(_internalCube);
        }
        gl.BindVertexArray(_internalCube.Vao);
        gl.DrawArrays(GLEnum.Triangles, 0, 36);
    }


    public unsafe void RenderQuad()
    {
        if (gl == null)
            return;
        if (_internalQuad == null)
        {
            _internalQuad = new InternalQuad();
            _internalQuad.Upload(gl!);
            _internalQuad.NeedsUpload = false;
            GpuResources.Add(_internalQuad);
        }
        gl.BindVertexArray(_internalQuad.Vao);
        gl.DrawElements(GLEnum.Triangles, 6, GLEnum.UnsignedInt, (void*)0);
    }

    public void Destroy()
    {
        foreach(var gpuResource in GpuResources)
        {
            gpuResource.Destroy(gl!);
        }

        GpuResources.Clear();

        Meshes.Clear();

        Cameras.Clear();

        PointLights.Clear();

        SpotLights.Clear();

    }

}

class InternalCube : IGpuResource
{
    public uint Vao;

    public uint Vbo;

    public bool NeedsUpload { get; set; } = true;

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
    }

    public unsafe void Upload(GL gl)
    {
        float[] vertices =
            [
                // back face
                -1.0f, -1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 0.0f, 0.0f, // bottom-left
                 1.0f,  1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 1.0f, 1.0f, // top-right
                 1.0f, -1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 1.0f, 0.0f, // bottom-right         
                 1.0f,  1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 1.0f, 1.0f, // top-right
                -1.0f, -1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 0.0f, 0.0f, // bottom-left
                -1.0f,  1.0f, -1.0f,  0.0f,  0.0f, -1.0f, 0.0f, 1.0f, // top-left
                // front face
                -1.0f, -1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f, 0.0f, // bottom-left
                 1.0f, -1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f, 0.0f, // bottom-right
                 1.0f,  1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f, 1.0f, // top-right
                 1.0f,  1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 1.0f, 1.0f, // top-right
                -1.0f,  1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f, 1.0f, // top-left
                -1.0f, -1.0f,  1.0f,  0.0f,  0.0f,  1.0f, 0.0f, 0.0f, // bottom-left
                // left face
                -1.0f,  1.0f,  1.0f, -1.0f,  0.0f,  0.0f, 1.0f, 0.0f, // top-right
                -1.0f,  1.0f, -1.0f, -1.0f,  0.0f,  0.0f, 1.0f, 1.0f, // top-left
                -1.0f, -1.0f, -1.0f, -1.0f,  0.0f,  0.0f, 0.0f, 1.0f, // bottom-left
                -1.0f, -1.0f, -1.0f, -1.0f,  0.0f,  0.0f, 0.0f, 1.0f, // bottom-left
                -1.0f, -1.0f,  1.0f, -1.0f,  0.0f,  0.0f, 0.0f, 0.0f, // bottom-right
                -1.0f,  1.0f,  1.0f, -1.0f,  0.0f,  0.0f, 1.0f, 0.0f, // top-right
                // right face
                 1.0f,  1.0f,  1.0f,  1.0f,  0.0f,  0.0f, 1.0f, 0.0f, // top-left
                 1.0f, -1.0f, -1.0f,  1.0f,  0.0f,  0.0f, 0.0f, 1.0f, // bottom-right
                 1.0f,  1.0f, -1.0f,  1.0f,  0.0f,  0.0f, 1.0f, 1.0f, // top-right         
                 1.0f, -1.0f, -1.0f,  1.0f,  0.0f,  0.0f, 0.0f, 1.0f, // bottom-right
                 1.0f,  1.0f,  1.0f,  1.0f,  0.0f,  0.0f, 1.0f, 0.0f, // top-left
                 1.0f, -1.0f,  1.0f,  1.0f,  0.0f,  0.0f, 0.0f, 0.0f, // bottom-left     
                // bottom face
                -1.0f, -1.0f, -1.0f,  0.0f, -1.0f,  0.0f, 0.0f, 1.0f, // top-right
                 1.0f, -1.0f, -1.0f,  0.0f, -1.0f,  0.0f, 1.0f, 1.0f, // top-left
                 1.0f, -1.0f,  1.0f,  0.0f, -1.0f,  0.0f, 1.0f, 0.0f, // bottom-left
                 1.0f, -1.0f,  1.0f,  0.0f, -1.0f,  0.0f, 1.0f, 0.0f, // bottom-left
                -1.0f, -1.0f,  1.0f,  0.0f, -1.0f,  0.0f, 0.0f, 0.0f, // bottom-right
                -1.0f, -1.0f, -1.0f,  0.0f, -1.0f,  0.0f, 0.0f, 1.0f, // top-right
                // top face
                -1.0f,  1.0f, -1.0f,  0.0f,  1.0f,  0.0f, 0.0f, 1.0f, // top-left
                 1.0f,  1.0f , 1.0f,  0.0f,  1.0f,  0.0f, 1.0f, 0.0f, // bottom-right
                 1.0f,  1.0f, -1.0f,  0.0f,  1.0f,  0.0f, 1.0f, 1.0f, // top-right     
                 1.0f,  1.0f,  1.0f,  0.0f,  1.0f,  0.0f, 1.0f, 0.0f, // bottom-right
                -1.0f,  1.0f, -1.0f,  0.0f,  1.0f,  0.0f, 0.0f, 1.0f, // top-left
                -1.0f,  1.0f,  1.0f,  0.0f,  1.0f,  0.0f, 0.0f, 0.0f  // bottom-left        
            ];
        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();
        // fill buffer
        gl.BindVertexArray(Vao);
        gl.BindBuffer(GLEnum.ArrayBuffer, Vbo);
        fixed (void* p = vertices)
        {
            gl.BufferData(GLEnum.ArrayBuffer, (nuint)vertices.Length * sizeof(float), p, GLEnum.StaticDraw);
        }
        // link vertex attributes
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, GLEnum.Float, false, 8 * sizeof(float), (void*)0);
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 3, GLEnum.Float, false, 8 * sizeof(float), (void*)(3 * sizeof(float)));
        gl.EnableVertexAttribArray(2);
        gl.VertexAttribPointer(2, 2, GLEnum.Float, false, 8 * sizeof(float), (void*)(6 * sizeof(float)));
        gl.BindBuffer(GLEnum.ArrayBuffer, 0);
        gl.BindVertexArray(0);
    }
}

class InternalQuad : IGpuResource
{
    public uint Vao;

    public uint Vbo;

    public uint Ebo;

    public bool NeedsUpload { get; set; } = true;

    struct QuadVertex
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
    }
    public unsafe void Upload(GL gl)
    {
        QuadVertex* vertices = stackalloc QuadVertex[4] {
            new () {Location = new Vector3(-1, 1, 0), TexCoord = new Vector2(0, 1) },
            new () {Location = new Vector3(-1, -1, 0), TexCoord = new Vector2(0, 0) },
            new () {Location = new Vector3(1, -1, 0), TexCoord = new Vector2(1, 0) },
            new () {Location = new Vector3(1, 1, 0), TexCoord = new Vector2(1, 1) },
        };

        uint* indices = stackalloc uint[6]
        {
            0, 1, 2, 2, 3,0
        };

        Vao = gl.GenVertexArray();
        Vbo = gl.GenBuffer();
        Ebo = gl.GenBuffer();

        gl.BindVertexArray(Vao);
        gl.BindBuffer(GLEnum.ArrayBuffer, Vbo);
        gl.BufferData(GLEnum.ArrayBuffer, (nuint)(4 * sizeof(QuadVertex)), vertices, GLEnum.StaticDraw);
        
        gl.BindBuffer(GLEnum.ElementArrayBuffer, Ebo);
        gl.BufferData(GLEnum.ElementArrayBuffer, 6 * sizeof(uint), indices, GLEnum.StaticDraw);
        
        // Location
        gl.EnableVertexAttribArray(0);
        gl.VertexAttribPointer(0, 3, GLEnum.Float, false, (uint)sizeof(QuadVertex), (void*)0);
        // TexCoord
        gl.EnableVertexAttribArray(1);
        gl.VertexAttribPointer(1, 2, GLEnum.Float, false, (uint)sizeof(QuadVertex), (void*)sizeof(Vector3));
        gl.BindVertexArray(0);

    }
}
