namespace Aura3D.Core.Renderers;

/// <summary>
/// 默认输出面的描述信息，用于透传窗口/控件提供的 FBO 与尺寸。
/// </summary>
public class RenderSurface
{
    public uint FrameBufferId { get; set; }

    public uint Width { get; set; }

    public uint Height { get; set; }

    public float Scale { get; set; } = 1f;
}
