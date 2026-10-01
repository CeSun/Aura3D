namespace Aura3D.Avalonia.Angle;

/// <summary>
/// 形态 B 的总开关：macOS 宿主保持 OpenGL 合成器，3D 视口改由自持的 ANGLE(Metal)
/// 会话出图（原生 GL-on-Metal 驱动连硬件点图元都剔除，见 Aura3D.Avalonia 的 macOS 后端注释）。
/// 宿主在任何 3D 视口挂树之前置 <see cref="Enabled"/> 为 true；非 macOS 平台置了也不生效。
/// </summary>
public static class MacAngleBackend
{
    /// <summary>宿主声明：macOS 上把 3D 视口从原生 GL 切到自持 ANGLE 会话。</summary>
    public static bool Enabled { get; set; }

    /// <summary>本进程当前是否处于 ANGLE 接管状态。</summary>
    public static bool IsActive => Enabled && OperatingSystem.IsMacOS();
}
