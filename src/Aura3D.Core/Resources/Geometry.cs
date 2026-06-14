using Aura3D.Core.Math;
using Silk.NET.OpenGLES;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Aura3D.Core.Resources;

/// <summary>
/// 几何体类，存储顶点数据和索引数据
/// </summary>
public class Geometry : IGpuResource, IClone<Geometry>
{

    /// <summary>
    /// 是否需要上传到GPU
    /// </summary>
    public bool NeedsUpload { get; set; } = true;

    protected Dictionary<string, VertexAttribute> VertexAttributes = new();

    private BoundingBox? boundingBox;

    /// <summary>
    /// 轴对齐包围盒，由顶点位置数据计算得出。
    /// </summary>
    public BoundingBox? BoundingBox
    {
        get
        {
            if (boundingBox == null)
                CalcBoundingBox();
            return boundingBox;
        }
    }

    /// <summary>
    /// 索引列表
    /// </summary>
    public List<uint> Indices { get; protected set; } = [];

    protected HashSet<uint> VertexAttributeLocations = new();

    protected List<uint> VboIds = new();

    /// <summary>
    /// 索引数量
    /// </summary>
    public int IndicesCount => Indices.Count;

    /// <summary>
    /// 顶点数量，由 Position 属性的数据长度计算得出。
    /// </summary>
    public int VertexCount
    {
        get
        {
            if (VertexAttributes.TryGetValue("Position", out var attr))
                return attr.Data.Count / attr.Size;
            return 0;
        }
    }

    /// <summary>
    /// 顶点数组对象ID
    /// </summary>
    public uint Vao;

    /// <summary>
    /// 元素缓冲对象ID
    /// </summary>
    public uint Ebo;

    /// <summary>
    /// 图元类型，默认为 Triangles。
    /// </summary>
    public PrimitiveType PrimitiveType { get; set; } = PrimitiveType.Triangles;

    public void SetVertexAttribute(string name, uint location, int size, List<float> data)
    {
        if (data.Count % size != 0)
            throw new ArgumentException($"The length of vertex attribute data must be a multiple of its size. Data length: {data.Count}, Size: {size}");

        if (VertexAttributes.TryGetValue(name, out var vertexAttribute))
        {
            VertexAttributes.Remove(name);
            VertexAttributeLocations.Remove(vertexAttribute.Location);
        }

        VertexAttributes.Add(name, new VertexAttribute
        {
            Name = name,
            Location = location,
            Size = size,
            Data = data,
            Enabled = (location <= 7)
        });
        VertexAttributeLocations.Add(location);

        NeedsUpload = true;

        // Position 属性变更时清空局部包围盒缓存，下次访问时重建
        if (name == BuildInVertexAttribute.Position.ToString())
            boundingBox = null;
    }

    public void SetVertexAttribute(BuildInVertexAttribute attribute, uint size, List<float> data)
    {
        SetVertexAttribute(attribute.ToString(), (uint)attribute, (int)size, data);
    }

    public void SetIndices(List<uint> indices)
    {
        Indices = indices;
    }

    public List<float>? GetAttributeData(string name)
    {
        if (!VertexAttributes.ContainsKey(name))
            return null;
        return VertexAttributes[name].Data;
    }

    public List<float>? GetAttributeData(BuildInVertexAttribute attribute)
    {
        return GetAttributeData(attribute.ToString());
    }

    /// <summary>
    /// 从顶点位置数据计算局部空间包围盒，零堆分配。
    /// </summary>
    private void CalcBoundingBox()
    {
        var positionData = GetAttributeData(BuildInVertexAttribute.Position);
        if (positionData == null || positionData.Count < 3)
        {
            boundingBox = null;
            return;
        }

        Vector3 min = new(float.MaxValue);
        Vector3 max = new(float.MinValue);

        for (int i = 0; i + 2 < positionData.Count; i += 3)
        {
            var v = new Vector3(positionData[i], positionData[i + 1], positionData[i + 2]);
            min = Vector3.Min(min, v);
            max = Vector3.Max(max, v);
        }

        boundingBox = new BoundingBox(min, max);
    }

    /// <summary>
    /// 开启或关闭指定顶点属性的上传。
    /// </summary>
    /// <param name="attribute">内置顶点属性枚举。</param>
    /// <param name="enabled">是否启用上传。</param>
    public void SetAttributeEnabled(BuildInVertexAttribute attribute, bool enabled)
    {
        var name = attribute.ToString();
        if (VertexAttributes.TryGetValue(name, out var attr))
        {
            attr.Enabled = enabled;
            VertexAttributes[name] = attr;
            NeedsUpload = true;
        }
    }

    public void Destroy(GL gl)
    {
        foreach (var vbo in VboIds)
        {
            gl.DeleteBuffer(vbo);
        }
        VboIds.Clear();
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

    public unsafe void Upload(GL gl)
    {
        if (Vao == 0)
        {
            Vao = gl.GenVertexArray();
        }
        else
        {
            // 重新上传时清理旧 VBO，避免重复申请导致泄漏
            foreach (var vbo in VboIds)
            {
                gl.DeleteBuffer(vbo);
            }
            VboIds.Clear();
        }

        gl.BindVertexArray(Vao);

        foreach (var(_, attribute) in VertexAttributes)
        {
            if (!attribute.Enabled)
                continue;

            uint vbo = gl.GenBuffer();
            VboIds.Add(vbo);
            gl.BindBuffer(BufferTargetARB.ArrayBuffer, vbo);
            unsafe
            {
                fixed (float* dataPtr = CollectionsMarshal.AsSpan(attribute.Data))
                {
                    gl.BufferData(GLEnum.ArrayBuffer, (nuint)(attribute.Data.Count * sizeof(float)), dataPtr, GLEnum.StaticDraw);
                }
            }
            gl.EnableVertexAttribArray(attribute.Location);
            gl.VertexAttribPointer(attribute.Location, attribute.Size, GLEnum.Float, false, (uint)(sizeof(float) * attribute.Size), (void*)0);
        }

        if (Indices.Count > 0)
        {
            if (Ebo == 0)
            {
                Ebo = gl.GenBuffer();
            }
            gl.BindBuffer(GLEnum.ElementArrayBuffer, Ebo);

            fixed (uint* indexPtr = CollectionsMarshal.AsSpan(Indices))
            {
                // 上传索引数据到 GPU
                gl.BufferData(GLEnum.ElementArrayBuffer, (nuint)(Indices.Count * sizeof(uint)), indexPtr, GLEnum.StaticDraw);
            }
        }


    }

    public Geometry Clone()
    {
        return new Geometry
        {
            Indices = Indices,
            VertexAttributes = VertexAttributes,
            VertexAttributeLocations = VertexAttributeLocations,
            PrimitiveType = PrimitiveType
        };
    }

    public Geometry DeepClone()
    {
        return new Geometry
        {
            Indices = new List<uint>(Indices),
            VertexAttributes = VertexAttributes.ToDictionary(
                kv => kv.Key,
                kv => new VertexAttribute
                {
                    Name = kv.Value.Name,
                    Location = kv.Value.Location,
                    Size = kv.Value.Size,
                    Data = new List<float>(kv.Value.Data),
                    Enabled = kv.Value.Enabled
                }),
            VertexAttributeLocations = new HashSet<uint>(VertexAttributeLocations),
            PrimitiveType = PrimitiveType
        };
    }
}


/// <summary>
/// 顶点属性结构体
/// </summary>
public struct VertexAttribute
{
    /// <summary>
    /// 属性名称
    /// </summary>
    public string Name;
    /// <summary>
    /// 属性位置
    /// </summary>
    public uint Location;
    /// <summary>
    /// 属性大小（分量数）
    /// </summary>
    public int Size;
    /// <summary>
    /// 属性数据
    /// </summary>
    public List<float> Data;
    /// <summary>
    /// 是否启用上传。默认只有 location 0~7 (Position 到 Weights_0) 为 true。
    /// </summary>
    public bool Enabled;
}

/// <summary>
/// 实例化属性指针，描述一个 VBO 内某个 location 的绑定方式。
/// </summary>
public struct InstanceAttributePointer
{
    /// <summary>
    /// 着色器中 layout(location) 的位置。
    /// </summary>
    public uint Location;
    /// <summary>
    /// 分量数：1=float, 2=vec2, 3=vec3, 4=vec4。
    /// </summary>
    public int ComponentCount;
    /// <summary>
    /// 在实例步长内的字节偏移。
    /// </summary>
    public int Offset;
}

/// <summary>
/// 实例化属性，一个 VBO 可包含多个 <see cref="InstanceAttributePointer"/>（如 mat4 = 4 个 vec4 指针）。
/// </summary>
public class InstanceAttribute
{
    /// <summary>
    /// 属性名称。
    /// </summary>
    public string Name = string.Empty;
    /// <summary>
    /// 逐实例打包的浮点数据。
    /// </summary>
    public List<float> Data = new();
    /// <summary>
    /// 单个实例的字节步长。
    /// </summary>
    public int Stride;
    /// <summary>
    /// GPU 缓冲区 ID。
    /// </summary>
    public uint Vbo;
    /// <summary>
    /// 是否启用上传。
    /// </summary>
    public bool Enabled = true;
    /// <summary>
    /// 该 VBO 上的顶点属性指针列表。
    /// </summary>
    public List<InstanceAttributePointer> Pointers = new();
}

/// <summary>
/// 内置顶点属性枚举
/// </summary>
public enum BuildInVertexAttribute
{
    /// <summary>
    /// 位置
    /// </summary>
    Position = 0,
    /// <summary>
    /// 第一套纹理坐标
    /// </summary>
    TexCoord_0 = 1,
    /// <summary>
    /// 顶点颜色
    /// </summary>
    Color_0 = 2,
    /// <summary>
    /// 法线
    /// </summary>
    Normal = 3,
    /// <summary>
    /// 切线
    /// </summary>
    Tangent = 4,
    /// <summary>
    /// 副切线
    /// </summary>
    Bitangent = 5,
    /// <summary>
    /// 第一套关节索引
    /// </summary>
    Joints_0 = 6,
    /// <summary>
    /// 第一套权重
    /// </summary>
    Weights_0 = 7,

    InstancedTransformColumn0 = 8,
    InstancedTransformColumn1 = 9,
    InstancedTransformColumn2 = 10,
    InstancedTransformColumn3 = 11,

    InstancedNormalTransformColumn0 = 12,
    InstancedNormalTransformColumn1 = 13,
    InstancedNormalTransformColumn2 = 14,
    InstancedNormalTransformColumn3 = 15,

    /// <summary>
    /// 第二套纹理坐标
    /// </summary>
    TexCoord_1 = 16,
    /// <summary>
    /// 第三套纹理坐标
    /// </summary>
    TexCoord_2 = 17,
    /// <summary>
    /// 第四套纹理坐标
    /// </summary>
    TexCoord_3 = 18,
    /// <summary>
    /// 第二套关节索引
    /// </summary>
    Joints_1 = 19,
    /// <summary>
    /// 第二套权重
    /// </summary>
    Weights_1 = 20,
}

/// <summary>
/// 图元类型枚举。
/// </summary>
public enum PrimitiveType
{
    /// <summary>
    /// 三角形。
    /// </summary>
    Triangles,
    /// <summary>
    /// 点。
    /// </summary>
    Points,
    /// <summary>
    /// 线段。
    /// </summary>
    Lines,
    /// <summary>
    /// 线段带。
    /// </summary>
    LineStrip,
    /// <summary>
    /// 线段环。
    /// </summary>
    LineLoop,
    /// <summary>
    /// 三角形带。
    /// </summary>
    TriangleStrip,
    /// <summary>
    /// 三角形扇。
    /// </summary>
    TriangleFan,
}