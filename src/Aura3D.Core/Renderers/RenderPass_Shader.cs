using Aura3D.Core.Math;
using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;
using System.Text;
using ShaderType = Silk.NET.OpenGLES.ShaderType;

namespace Aura3D.Core.Renderers;

public partial class RenderPass
{
    /// <summary>
    /// Gets or sets the shader name.
    /// </summary>
    public string ShaderName { get; protected set; }

    /// <summary>
    /// Gets the vertex shader.
    /// </summary>
    protected string VertexShader = string.Empty;

    /// <summary>
    /// Gets the fragment shader.
    /// </summary>
    protected string FragmentShader = string.Empty;

    /// <summary>
    /// Gets the shaders.
    /// </summary>
    public Dictionary<string, Shader> Shaders { get; } = new Dictionary<string, Shader>();

    /// <summary>
    /// Gets or sets the current shader.
    /// </summary>
    public Shader? CurrentShader { get; private set; } = null;

    List<string> defines = [];

    /// <summary>
    /// Performs the use shader operation.
    /// </summary>
    public void UseShader(params string[] defines)
    {
        this.defines = new(defines);
    }

    /// <summary>
    /// Adds the defines.
    /// </summary>
    public void AddDefines(params string[] defines)
    {
        this.defines.AddRange(defines);
    }

    /// <summary>
    /// Removes the defines.
    /// </summary>
    public void RemoveDefines(params string[] defines)
    {
        foreach (var d in defines)
            this.defines.Remove(d);
    }


    /// <summary>
    /// Performs the use shader internal operation.
    /// </summary>
    protected void UseShader_Internal()
    {
        UseShader_Internal((Material?)null);
    }
    /// <summary>
    /// Performs the use shader internal operation.
    /// </summary>
    protected void UseShader_Internal(Mesh? mesh)
    {
        UseShader_Internal(mesh?.Material);
    }

    /// <summary>
    /// Performs the use shader internal operation.
    /// </summary>
    protected void UseShader_Internal(Material? material)
    {
        Shader? shader = null;

        var name = string.Join(";", defines);

        if (material != null && material.HasShader)
        {
            var (vertexShader, fragmentShader) = material.GetShaderSource(ShaderName);

            if (vertexShader != null || fragmentShader != null)
            {
                var gpuState = renderPipeline.GetMaterialGpuState(material);

                if (gpuState.Shaders.TryGetValue(ShaderName + ";" + name, out shader) == false)
                {
                    if (vertexShader == null)
                        vertexShader = VertexShader;
                    if (fragmentShader == null)
                        fragmentShader = FragmentShader;

                    shader = CreateShaderProgram(defines.ToArray(), vertexShader, fragmentShader);

                    gpuState.Shaders[ShaderName + ";" + name] = shader;
                }
            }
        }

        if (shader == null)
        {
            if (Shaders.TryGetValue(name, out shader) == false)
            {
                shader = CreateShaderProgram(defines.ToArray(), VertexShader, FragmentShader);
                Shaders[name] = shader;
            }
        }

        gl.UseProgram(shader.ProgramId);
        CurrentShader = shader;
    }

    private Shader CreateShaderProgram(string[] defines, string vertexShader, string fragmentShader)
    {
        var shader = new Shader();

        shader.Defines = defines;

        var definesText = string.Join("\n", defines.Select(d => $"#define {d}"));

        var vs = vertexShader.Replace("//{{defines}}", definesText);

        var fs = fragmentShader.Replace("//{{defines}}", definesText);

        var vertex = gl.CreateShader(ShaderType.VertexShader);

        if (System.OperatingSystem.IsMacOS())
        {
            vs = vs.Replace("#version 300 es", "#version 330 core");
            fs = fs.Replace("#version 300 es", "#version 330 core");
        }

        gl.ShaderSource(vertex, vs);
        gl.CompileShader(vertex);

        gl.GetShader(vertex, GLEnum.CompileStatus, out int code);

        if (code == 0)
        {
            var info = gl.GetShaderInfoLog(vertex);
            Console.WriteLine(vs);
            throw Aura3D.Core.Exceptions.RendererErrors.ShaderCompilationFailed(true, info);
        }

        var fragment = gl.CreateShader(ShaderType.FragmentShader);

        gl.ShaderSource(fragment, fs);
        gl.CompileShader(fragment);

        gl.GetShader(fragment, GLEnum.CompileStatus, out code);

        if (code == 0)
        {
            var info = gl.GetShaderInfoLog(fragment);
            Console.WriteLine(fs);
            throw Aura3D.Core.Exceptions.RendererErrors.ShaderCompilationFailed(false, info);
        }

        var programId = gl.CreateProgram();

        gl.AttachShader(programId, vertex);
        gl.AttachShader(programId, fragment);
        gl.LinkProgram(programId);

        gl.GetProgram(programId, GLEnum.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
        {
            var info = gl.GetProgramInfoLog(programId);
            throw Aura3D.Core.Exceptions.RendererErrors.ShaderProgramLinkFailed(info);
        }

        // GLES 3.0 不支持 shader 内 layout(binding=N)，link 后枚举所有
        // uniform block 并按索引自动绑定（block 0→binding 0, block 1→binding 1...）
        gl.GetProgram(programId, GLEnum.ActiveUniformBlocks, out int blockCount);
        for (uint i = 0; i < blockCount; i++)
        {
            gl.UniformBlockBinding(programId, i, i);
        }

        gl.DeleteShader(vertex);
        gl.DeleteShader(fragment);

        shader.ProgramId = programId;

        GetAllUniformLocations(gl, shader);

        return shader;
    }

    private unsafe void GetAllUniformLocations(GL gl, Shader shader)
    {
        gl.GetProgram(shader.ProgramId, GLEnum.ActiveUniforms, out int numUniforms);

        if (numUniforms <= 0)
            return;

        gl.GetProgram(shader.ProgramId, GLEnum.ActiveUniformMaxLength, out int maxNameLength);

        Span<byte> nameBuffer = stackalloc byte[maxNameLength];

        for (int i = 0; i < numUniforms; i++)
        {
            gl.GetActiveUniform(shader.ProgramId, (uint)i, out var length, out var size, out GLEnum uniformType, nameBuffer);

            string uniformName = Encoding.UTF8.GetString(nameBuffer.Slice(0, (int)length));

            int location = gl.GetUniformLocation(shader.ProgramId, uniformName);

            shader.UniformLocation[uniformName.Trim()] = location;
        }
    }

    private int currentTextureUnit = 0;

    /// <summary>
    /// Clears the texture unit.
    /// </summary>
    public void ClearTextureUnit()
    {
        currentTextureUnit = 0;
    }

    /// <summary>
    /// Performs the uniform texture operation.
    /// </summary>
    public void UniformTexture(string name, uint textureId)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        var textureUnit = GLEnum.Texture0 + currentTextureUnit;
        gl.Uniform1(location, currentTextureUnit);
        gl.ActiveTexture(textureUnit);
        gl.BindTexture(GLEnum.Texture2D, textureId);

        currentTextureUnit++;
    }

    /// <summary>
    /// Performs the uniform texture array operation.
    /// </summary>
    public void UniformTextureArray(string name, uint textureArrayId)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        var textureUnit = GLEnum.Texture0 + currentTextureUnit;
        gl.Uniform1(location, currentTextureUnit);
        gl.ActiveTexture(textureUnit);
        gl.BindTexture(GLEnum.Texture2DArray, textureArrayId);

        currentTextureUnit++;
    }

    /// <summary>
    /// Performs the uniform texture cube map operation.
    /// </summary>
    public void UniformTextureCubeMap(string name, uint textureId)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        var textureUnit = GLEnum.Texture0 + currentTextureUnit;
        gl.Uniform1(location, currentTextureUnit);
        gl.ActiveTexture(textureUnit);
        gl.BindTexture(GLEnum.TextureCubeMap, textureId);
        currentTextureUnit++;
    }

    /// <summary>
    /// Performs the uniform texture cube map operation.
    /// </summary>
    public void UniformTextureCubeMap(string name, Aura3D.Core.Resources.CubeTexture texture)
    {
        if (CurrentShader == null)
            return;
        uint textureId = renderPipeline.EnsureSynced(texture).TextureId;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        var textureUnit = GLEnum.Texture0 + currentTextureUnit;
        gl.Uniform1(location, currentTextureUnit);
        gl.ActiveTexture(textureUnit);
        gl.BindTexture(GLEnum.TextureCubeMap, textureId);
        currentTextureUnit++;
    }

    /// <summary>
    /// Performs the uniform texture operation.
    /// </summary>
    public void UniformTexture(string name, Aura3D.Core.Resources.Texture? texture)
    {
        if (texture == null)
            return;
        if (CurrentShader == null)
            return;

        uint textureId = renderPipeline.EnsureSynced(texture).TextureId;

        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        var textureUnit = GLEnum.Texture0 + currentTextureUnit;
        gl.ActiveTexture(textureUnit);
        gl.BindTexture(GLEnum.Texture2D, textureId);
        gl.Uniform1(location, currentTextureUnit);

        currentTextureUnit++;
    }

    /// <summary>
    /// Performs the uniform int operation.
    /// </summary>
    public void UniformInt(string name, int value)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        gl.Uniform1(location, value);
    }

    /// <summary>
    /// Performs the uniform float operation.
    /// </summary>
    public void UniformFloat(string name, float value)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        gl.Uniform1(location, value);
    }

    /// <summary>
    /// Performs the uniform vector3 operation.
    /// </summary>
    public unsafe void UniformVector3(string name, Vector3 value)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        gl.Uniform3(location, 1, (float*)&value);
    }

    /// <summary>
    /// Performs the uniform matrix4 operation.
    /// </summary>
    public unsafe void UniformMatrix4(string name, Matrix4x4 value)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        gl.UniformMatrix4(location, 1, false, (float*)&value);
    }

    /// <summary>
    /// Performs the uniform vector2 operation.
    /// </summary>
    public unsafe void UniformVector2(string name, Vector2 value)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        gl.Uniform2(location, 1, (float*)&value);
    }

    /// <summary>
    /// Performs the uniform vector4 operation.
    /// </summary>
    public unsafe void UniformVector4(string name, Vector4 value)
    {
        if (CurrentShader == null)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        gl.Uniform4(location, 1, (float*)&value);
    }

    /// <summary>
    /// Performs the uniform color operation.
    /// </summary>
    public unsafe void UniformColor(string name, Color color)
    {
        Vector4 vector4 = color.ToVector4();
        UniformVector3(name, new Vector3(vector4.X, vector4.Y, vector4.Z));
    }

    /// <summary>
    /// Performs the uniform matrix4 array operation.
    /// </summary>
    public unsafe void UniformMatrix4Array(string name, Span<Matrix4x4> values)
    {
        if (CurrentShader == null || values == null || values.Length == 0)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        fixed (Matrix4x4* ptr = values)
        {
            gl.UniformMatrix4(location, (uint)values.Length, false, (float*)ptr);
        }
    }

    /// <summary>
    /// Performs the uniform vector3 array operation.
    /// </summary>
    public unsafe void UniformVector3Array(string name, Span<Vector3> values)
    {
        if (CurrentShader == null || values == null || values.Length == 0)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        fixed (Vector3* ptr = values)
        {
            gl.Uniform3(location, (uint)values.Length, (float*)ptr);
        }
    }

    /// <summary>
    /// Performs the uniform vector4 array operation.
    /// </summary>
    public unsafe void UniformVector4Array(string name, Span<Vector4> values)
    {
        if (CurrentShader == null || values == null || values.Length == 0)
            return;
        var location = CurrentShader.GetUniformLocation(name, gl);
        if (location == -1)
            return;
        fixed (Vector4* ptr = values)
        {
            gl.Uniform4(location, (uint)values.Length, (float*)ptr);
        }
    }
}

/// <summary>
/// Represents the shader type.
/// </summary>
public class  Shader
{
    /// <summary>
    /// Gets or sets the defines.
    /// </summary>
    public string[] Defines { get; set; } = [];

    /// <summary>
    /// Gets or sets the program id.
    /// </summary>
    public uint ProgramId { get; set; } = 0;

    /// <summary>
    /// Gets the uniform location.
    /// </summary>
    internal Dictionary<string, int> UniformLocation { get; } = new Dictionary<string, int>();

    /// <summary>
    /// Gets the uniform location.
    /// </summary>
    public int GetUniformLocation(string name, GL gl)
    {
        if (UniformLocation.TryGetValue(name, out int location))
        {
            return location;
        }

        location = gl.GetUniformLocation(ProgramId, name);

        if (location >= 0)
        {
            UniformLocation[name] = location;
            return location;
        }

        return -1;
    }
}
