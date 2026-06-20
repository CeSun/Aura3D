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
    /// 获取或设置当前渲染通道使用的着色器名称。
    /// </summary>
    public string ShaderName { get; protected set; }

    protected string VertexShader = string.Empty;

    protected string FragmentShader = string.Empty;

    /// <summary>
    /// 获取当前渲染通道已编译的着色器程序字典，键为宏定义组合字符串。
    /// </summary>
    public Dictionary<string, Shader> Shaders { get; } = new Dictionary<string, Shader>();

    /// <summary>
    /// 获取当前正在使用的着色器程序。
    /// </summary>
    public Shader? CurrentShader { get; private set; } = null;

    List<string> defines = [];

    /// <summary>
    /// 指定后续着色器编译时要使用的宏定义。
    /// </summary>
    /// <param name="defines">要启用的宏定义名称列表。</param>
    public void UseShader(params string[] defines)
    {
        this.defines = new(defines);
    }

    /// <summary>
    /// 向当前宏定义列表追加额外的宏定义。
    /// </summary>
    /// <param name="defines">要追加的宏定义名称列表。</param>
    public void AddDefines(params string[] defines)
    {
        this.defines.AddRange(defines);
    }

    /// <summary>
    /// 从当前宏定义列表移除指定的宏定义。
    /// </summary>
    public void RemoveDefines(params string[] defines)
    {
        foreach (var d in defines)
            this.defines.Remove(d);
    }


    protected void UseShader_Internal()
    {
        UseShader_Internal((Material?)null);
    }
    protected void UseShader_Internal(Mesh? mesh)
    {
        UseShader_Internal(mesh?.Material);
    }

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
            throw new InvalidOperationException($"Vertex shader compilation failed: {info}");
        }

        var fragment = gl.CreateShader(ShaderType.FragmentShader);

        gl.ShaderSource(fragment, fs);
        gl.CompileShader(fragment);

        gl.GetShader(fragment, GLEnum.CompileStatus, out code);

        if (code == 0)
        {
            var info = gl.GetShaderInfoLog(fragment);
            Console.WriteLine(fs);
            throw new InvalidOperationException($"Fragment shader compilation failed: {info}");
        }

        var programId = gl.CreateProgram();

        gl.AttachShader(programId, vertex);
        gl.AttachShader(programId, fragment);
        gl.LinkProgram(programId);

        gl.GetProgram(programId, GLEnum.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
        {
            var info = gl.GetProgramInfoLog(programId);
            throw new InvalidOperationException($"Shader program link failed: {info}");
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
    /// 重置纹理单元计数器为 0，通常在绑定新的一批纹理前调用。
    /// </summary>
    public void ClearTextureUnit()
    {
        currentTextureUnit = 0;
    }

    /// <summary>
    /// 向当前着色器的指定 Uniform 变量绑定一个 2D 纹理。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="textureId">OpenGL 纹理 ID。</param>
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
    /// 向当前着色器的指定 sampler2DArray Uniform 绑定一个 2D 纹理数组。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="textureArrayId">OpenGL 纹理数组 ID。</param>
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
    /// 向当前着色器的指定 Uniform 变量绑定一个立方体贴图。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="textureId">OpenGL 立方体贴图纹理 ID。</param>
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
    /// 向当前着色器的指定 Uniform 变量绑定一个立方体贴图。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="texture">立方体贴图资源。</param>
    public void UniformTextureCubeMap(string name, Aura3D.Core.Resources.CubeTexture texture)
    {
        if (CurrentShader == null)
            return;
        uint textureId = renderPipeline.EnsureUploaded(texture);
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
    /// 向当前着色器的指定 Uniform 变量绑定一个 2D 纹理。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="texture">2D 纹理资源。</param>
    public void UniformTexture(string name, Aura3D.Core.Resources.Texture texture)
    {
        if (texture == null)
            return;
        if (CurrentShader == null)
            return;

        uint textureId = renderPipeline.EnsureUploaded(texture);

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
    /// 向当前着色器设置一个整型 Uniform 变量。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="value">整型值。</param>
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
    /// 向当前着色器设置一个浮点型 Uniform 变量。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="value">浮点值。</param>
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
    /// 向当前着色器设置一个三维向量 Uniform 变量。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="value">三维向量值。</param>
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
    /// 向当前着色器设置一个 4x4 矩阵 Uniform 变量。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="value">矩阵值。</param>
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
    /// 向当前着色器设置一个二维向量 Uniform 变量。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="value">二维向量值。</param>
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
    /// 向当前着色器设置一个四维向量 Uniform 变量。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="value">四维向量值。</param>
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
    /// 向当前着色器设置一个颜色 Uniform 变量（仅传递 RGB 分量）。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="color">颜色值。</param>
    public unsafe void UniformColor(string name, Color color)
    {
        Vector4 vector4 = color.ToVector4();
        UniformVector3(name, new Vector3(vector4.X, vector4.Y, vector4.Z));
    }

    /// <summary>
    /// 向当前着色器设置一个 4x4 矩阵数组 Uniform 变量。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="values">矩阵数组。</param>
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
    /// 向当前着色器设置一个三维向量数组 Uniform 变量。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="values">三维向量数组。</param>
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
    /// 向当前着色器设置一个四维向量数组 Uniform 变量。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="values">四维向量数组。</param>
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
/// 表示一个已编译链接着色器程序，包含程序 ID 和 Uniform 变量位置缓存。
/// </summary>
public class  Shader
{
    /// <summary>
    /// 获取或设置当前着色器使用的宏定义列表。
    /// </summary>
    public string[] Defines { get; set; } = [];

    /// <summary>
    /// 获取或设置 OpenGL 着色器程序 ID。
    /// </summary>
    public uint ProgramId { get; set; } = 0;

    /// <summary>
    /// 获取 Uniform 变量名称到其位置索引的字典缓存。
    /// </summary>
    public Dictionary<string, int> UniformLocation = new Dictionary<string, int>();

    /// <summary>
    /// 获取指定 Uniform 变量在当前着色器程序中的位置，若未缓存则向 OpenGL 查询。
    /// </summary>
    /// <param name="name">Uniform 变量名称。</param>
    /// <param name="gl">OpenGL 上下文。</param>
    /// <returns>Uniform 位置索引，若不存在则返回 -1。</returns>
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
