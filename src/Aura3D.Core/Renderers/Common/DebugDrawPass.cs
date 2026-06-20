using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Aura3D.Core.Scenes;
using Silk.NET.OpenGLES;
using System.Drawing;
using System.Numerics;

namespace Aura3D.Core.Renderers;

/// <summary>
/// 调试绘制渲染通道，使用即时模式（Begin/Vertex/End）直接渲染
/// 坐标轴、网格等引擎内置调试可视化元素。
/// 渲染到自有 RenderTarget（带颜色+深度附件），支持从场景渲染目标拷贝深度缓冲以实现正确的遮挡关系，
/// 最后将结果写回相机 FBO。
/// 该通道在每帧最后执行。
/// </summary>
public class DebugDrawPass : RenderPass
{
    private const string DebugOutputName = "DebugOutput";
    private readonly RenderTargetHandle _debugOutput;
    private readonly RenderTargetHandle? _depthRenderTarget;

    /// <summary>
    /// 初始化 <see cref="DebugDrawPass"/> 类的新实例。
    /// </summary>
    /// <param name="renderPipeline">所属的渲染管线。</param>
    /// <param name="depthRenderTarget">
    /// 深度缓冲源渲染目标引用（如 BaseRenderTarget）。
    /// 传入非 null 值时，会将场景深度缓冲拷贝到调试 RenderTarget，
    /// 使网格等调试元素能被场景几何体正确遮挡。
    /// 传入 null 时仅清除深度缓冲。
    /// </param>
    public DebugDrawPass(RenderPipeline renderPipeline, RenderTargetHandle? depthRenderTarget = null)
        : base(renderPipeline)
    {
        _depthRenderTarget = depthRenderTarget;

        _debugOutput = renderPipeline.RegisterRenderTarget(DebugOutputName)
            .AddTexture("Color", TextureFormat.Rgba8)
            .SetDepthTexture(renderPipeline.Settings.DepthFormat);

        this.VertexShader = ShaderResource.DebugVert;
        this.FragmentShader = ShaderResource.DebugFrag;
        ShaderName = nameof(DebugDrawPass);
    }

    /// <inheritdoc />
    public override void BeforeRender(Camera camera)
    {
        int w = (int)camera.Width;
        int h = (int)camera.Height;

        var debugRT = GetRenderTarget(_debugOutput, new Size(w, h));
        gl.BindFramebuffer(GLEnum.Framebuffer, debugRT.FrameBufferId);

        // 1. 拷贝当前输出颜色到调试 RenderTarget（保留场景画面）
        gl.BindFramebuffer(GLEnum.ReadFramebuffer, GetOutputFramebufferId(camera));
        gl.BindFramebuffer(GLEnum.DrawFramebuffer, debugRT.FrameBufferId);
        gl.BlitFramebuffer(0, 0, w, h, 0, 0, w, h,
            ClearBufferMask.ColorBufferBit, GLEnum.Nearest);

        // 2. 拷贝场景深度到调试 RenderTarget
        if (_depthRenderTarget != null)
        {
            var sourceRT = GetRenderTarget(_depthRenderTarget, camera);
            gl.BindFramebuffer(GLEnum.ReadFramebuffer, sourceRT.FrameBufferId);
            gl.BindFramebuffer(GLEnum.DrawFramebuffer, debugRT.FrameBufferId);
            gl.BlitFramebuffer(0, 0, w, h, 0, 0, w, h,
                ClearBufferMask.DepthBufferBit, GLEnum.Nearest);
        }
        else
        {
            gl.BindFramebuffer(GLEnum.Framebuffer, debugRT.FrameBufferId);
            gl.Clear(ClearBufferMask.DepthBufferBit);
        }

        // 3. 重新绑定调试 RenderTarget（blit 可能改变了绑定）
        gl.BindFramebuffer(GLEnum.Framebuffer, debugRT.FrameBufferId);

        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Lequal);

        gl.Enable(EnableCap.Blend);
        gl.BlendFunc(GLEnum.SrcAlpha, GLEnum.OneMinusSrcAlpha);
    }

    /// <summary>
    /// 执行指定相机的调试绘制渲染。
    /// </summary>
    public override void Render(Camera camera)
    {
        UseShader();
        UseShader_Internal();

        UniformMatrix4("viewMatrix", camera.View);
        UniformMatrix4("projectionMatrix", camera.Projection);

        if (!renderPipeline.Settings.Debug.Enable)
            return;

        var axisGizmo = Scene.AxisGizmo;
        if (axisGizmo != null && axisGizmo.Enable)
        {
            DrawAxisGizmo(axisGizmo);
        }

        var grid = Scene.Grid;
        if (grid != null && grid.Enable)
        {
            DrawGrid(grid);
        }

        if (renderPipeline.Settings.Debug.ShowBoundingBox)
        {
            DrawBoundingBoxes();
        }

        if (renderPipeline.Settings.Debug.ShowParticleBounds)
        {
            DrawParticleBounds();
        }

        if (renderPipeline.Settings.Debug.ShowDirectionalLight)
        {
            DrawDirectionalLights();
        }

        if (renderPipeline.Settings.Debug.ShowPointLight)
        {
            DrawPointLights();
        }

        if (renderPipeline.Settings.Debug.ShowSpotLight)
        {
            DrawSpotLights();
        }

        if (renderPipeline.Settings.Debug.ShowCamera)
        {
            DrawCameras(camera);
        }

        if (renderPipeline.Settings.Debug.ShowBone)
        {
            DrawBones();
        }
    }

    private void DrawAxisGizmo(AxisGizmo axis)
    {
        UniformMatrix4("modelMatrix", Matrix4x4.Identity);
        gl.Disable(EnableCap.DepthTest);
        gl.DepthMask(false);

        DrawAxisLine(new Vector3(1, 0, 0), axis.AxisLength, axis.ArrowheadSize, System.Drawing.Color.Red);
        DrawAxisLine(new Vector3(0, 1, 0), axis.AxisLength, axis.ArrowheadSize, System.Drawing.Color.Green);
        DrawAxisLine(new Vector3(0, 0, 1), axis.AxisLength, axis.ArrowheadSize, System.Drawing.Color.Blue);

        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
    }

    private void DrawAxisLine(Vector3 direction, float length, float arrowheadSize, System.Drawing.Color color)
    {
        UniformVector3("uColor", new Vector3(color.R / 255f, color.G / 255f, color.B / 255f));

        var dir = Vector3.Normalize(direction);
        var tip = dir * length;

        Begin();
        Line(Vector3.Zero, tip);

        var (perp1, perp2) = GetPlaneBasis(dir);
        var basePt = tip - dir * arrowheadSize;
        float w = arrowheadSize * 0.35f;

        Line(tip, basePt + perp1 * w);
        Line(tip, basePt - perp1 * w);
        Line(tip, basePt + perp2 * w);
        Line(tip, basePt - perp2 * w);

        End();
    }

    private void DrawGrid(Grid grid)
    {
        UniformMatrix4("modelMatrix", Matrix4x4.Identity);

        float halfSize = grid.Size * 0.5f;
        float step = grid.Size / System.Math.Max(1, grid.Divisions);

        UniformVector3("uColor", new Vector3(
            grid.LineColor.R / 255f, grid.LineColor.G / 255f, grid.LineColor.B / 255f));

        Begin();
        for (int i = 0; i <= grid.Divisions; i++)
        {
            float t = -halfSize + i * step;

            if (MathF.Abs(t) < step * 0.01f)
                continue;

            Line(t, 0, -halfSize, t, 0, halfSize);
            Line(-halfSize, 0, t, halfSize, 0, t);
        }
        End();

        UniformVector3("uColor", new Vector3(
            grid.CenterLineColor.R / 255f, grid.CenterLineColor.G / 255f, grid.CenterLineColor.B / 255f));

        Begin();
        Line(-halfSize, 0, 0, halfSize, 0, 0);
        Line(0, 0, -halfSize, 0, 0, halfSize);
        End();
    }

    private void DrawBoundingBoxes()
    {
        UniformMatrix4("modelMatrix", Matrix4x4.Identity);

        // Meshes + InstancedMeshes (green)
        UniformVector3("uColor", new Vector3(0.25f, 1.0f, 0.25f));
        Begin();
        foreach (var mesh in Meshes)
        {
            if (!mesh.Enable)
                continue;
            var bb = mesh.BoundingBox;
            if (bb == null)
                continue;
            WireBox(bb.Min, bb.Max);
        }
        foreach (var im in renderPipeline.InstancedMeshes)
        {
            if (!im.Enable)
                continue;
            var bb = im.WorldBoundingBox;
            if (bb == null)
                continue;
            WireBox(bb.Min, bb.Max);
        }
        End();
    }

    private void DrawParticleBounds()
    {
        UniformMatrix4("modelMatrix", Matrix4x4.Identity);
        UniformVector3("uColor", new Vector3(1.0f, 0.5f, 0.1f)); // orange
        Begin();
        foreach (var ps in renderPipeline.ParticleSystems)
        {
            if (!ps.Enable)
                continue;
            if (!ps.IsPlaying || ps.ActiveCount == 0)
                continue;
            var bb = ps.WorldBoundingBox;
            if (bb == null)
                continue;
            WireBox(bb.Min, bb.Max);
        }
        End();
    }

    private void DrawDirectionalLights()
    {
        UniformMatrix4("modelMatrix", Matrix4x4.Identity);

        foreach (var light in renderPipeline.DirectionalLights)
        {
            if (!light.Enable)
                continue;

            var dir = light.Forward;
            var center = light.WorldTransform.Translation;
            var color = light.LightColor;
            UniformVector3("uColor", new Vector3(color.R / 255f, color.G / 255f, color.B / 255f));

            // 在光源位置绘制方向指示器（圆圈 + 箭头）
            float iconRadius = 0.4f;

            Begin();
            // 垂直于光照方向的圆盘
            Circle(center, dir, iconRadius, 24);
            // 箭头指示方向
            WireArrow(center - dir * iconRadius, center + dir * 1.2f, 0.2f);
            End();
        }
    }

    private void DrawPointLights()
    {
        UniformMatrix4("modelMatrix", Matrix4x4.Identity);

        foreach (var light in renderPipeline.PointLights)
        {
            if (!light.Enable)
                continue;

            var pos = light.WorldTransform.Translation;
            var color = light.LightColor;
            UniformVector3("uColor", new Vector3(color.R / 255f, color.G / 255f, color.B / 255f));

            // 衰减球范围
            float displayRadius = MathF.Min(light.AttenuationRadius, 50f);

            Begin();
            WireSphere(pos, displayRadius, 24);
            // 在光源位置绘制小十字
            float crossSize = displayRadius * 0.15f;
            Line(pos + new Vector3(-crossSize, 0, 0), pos + new Vector3(crossSize, 0, 0));
            Line(pos + new Vector3(0, -crossSize, 0), pos + new Vector3(0, crossSize, 0));
            Line(pos + new Vector3(0, 0, -crossSize), pos + new Vector3(0, 0, crossSize));
            End();
        }
    }

    private void DrawSpotLights()
    {
        UniformMatrix4("modelMatrix", Matrix4x4.Identity);

        foreach (var light in renderPipeline.SpotLights)
        {
            if (!light.Enable)
                continue;

            var pos = light.WorldTransform.Translation;
            var dir = light.Forward;
            var color = light.LightColor;
            UniformVector3("uColor", new Vector3(color.R / 255f, color.G / 255f, color.B / 255f));

            float outerAngleRad = light.OuterAngleDegree * MathF.PI / 180f;
            float coneLength = MathF.Min(light.AttenuationRadius, 50f);

            Begin();
            // 锥体
            WireCone(pos, dir, outerAngleRad, coneLength, 20);
            // 在光源位置绘制小十字
            float crossSize = coneLength * 0.05f;
            Line(pos + new Vector3(-crossSize, 0, 0), pos + new Vector3(crossSize, 0, 0));
            Line(pos + new Vector3(0, -crossSize, 0), pos + new Vector3(0, crossSize, 0));
            Line(pos + new Vector3(0, 0, -crossSize), pos + new Vector3(0, 0, crossSize));
            // 添加内部锥体（内锥角）
            float innerAngleRad = light.InnerConeAngleDegree * MathF.PI / 180f;
            if (innerAngleRad < outerAngleRad - 0.001f)
            {
                var innerColor = new Vector3(color.R / 255f * 0.6f, color.G / 255f * 0.6f, color.B / 255f * 0.6f);
                End();
                UniformVector3("uColor", innerColor);
                Begin();
                WireCone(pos, dir, innerAngleRad, coneLength * 0.5f, 16);
            }
            End();
        }
    }

    private void DrawCameras(Camera currentCamera)
    {
        UniformMatrix4("modelMatrix", Matrix4x4.Identity);

        // 使用淡蓝色绘制所有相机
        UniformVector3("uColor", new Vector3(0.2f, 0.5f, 1.0f));

        foreach (var cam in renderPipeline.Cameras)
        {
            if (!cam.Enable)
                continue;
            // 不绘制当前正在渲染的相机（避免视觉混乱）
            if (cam == currentCamera)
                continue;

            var camPos = cam.WorldTransform.Translation;
            var forward = cam.Forward;
            var up = cam.Up;
            var right = cam.Right;

            // 使用近平面和远平面的世界空间位置来绘制视锥体
            float nearDist = cam.NearPlane;
            float farDist = cam.FarPlane;

            // 计算近平面和远平面的半高/半宽
            float nearHalfHeight, farHalfHeight;
            float nearHalfWidth, farHalfWidth;

            if (cam.ProjectionType == ProjectionType.Perspective)
            {
                float fovRad = cam.FieldOfView * MathF.PI / 180f;
                float aspect = cam.Width / (float)cam.Height;
                nearHalfHeight = MathF.Tan(fovRad * 0.5f) * nearDist;
                farHalfHeight = MathF.Tan(fovRad * 0.5f) * farDist;
                nearHalfWidth = nearHalfHeight * aspect;
                farHalfWidth = farHalfHeight * aspect;
            }
            else
            {
                float aspect = cam.Width / (float)cam.Height;
                nearHalfHeight = cam.OrthographicSize * 0.5f;
                farHalfHeight = cam.OrthographicSize * 0.5f;
                nearHalfWidth = nearHalfHeight * aspect;
                farHalfWidth = farHalfHeight * aspect;
            }

            var nearCenter = camPos + forward * nearDist;
            var farCenter = camPos + forward * farDist;

            // 计算近平面四个角
            var ntl = nearCenter + up * nearHalfHeight - right * nearHalfWidth;
            var ntr = nearCenter + up * nearHalfHeight + right * nearHalfWidth;
            var nbl = nearCenter - up * nearHalfHeight - right * nearHalfWidth;
            var nbr = nearCenter - up * nearHalfHeight + right * nearHalfWidth;

            // 计算远平面四个角
            var ftl = farCenter + up * farHalfHeight - right * farHalfWidth;
            var ftr = farCenter + up * farHalfHeight + right * farHalfWidth;
            var fbl = farCenter - up * farHalfHeight - right * farHalfWidth;
            var fbr = farCenter - up * farHalfHeight + right * farHalfWidth;

            Begin();
            // 近平面矩形
            Line(ntl, ntr); Line(ntr, nbr); Line(nbr, nbl); Line(nbl, ntl);
            // 远平面矩形
            Line(ftl, ftr); Line(ftr, fbr); Line(fbr, fbl); Line(fbl, ftl);
            // 连接边
            Line(ntl, ftl); Line(ntr, ftr); Line(nbl, fbl); Line(nbr, fbr);
            // 相机位置到近平面四个角的连线（小金字塔）
            if (cam.ProjectionType == ProjectionType.Perspective)
            {
                Line(camPos, ntl); Line(camPos, ntr);
                Line(camPos, nbl); Line(camPos, nbr);
            }
            End();
        }
    }

    private void DrawBones()
    {
        UniformMatrix4("modelMatrix", Matrix4x4.Identity);

        var drawnModels = new HashSet<Model>();

        foreach (var mesh in Meshes)
        {
            if (!mesh.Enable)
                continue;
            var model = mesh.Model;
            if (model == null || model.Skeleton == null)
                continue;
            if (!drawnModels.Add(model))
                continue;

            var skeleton = model.Skeleton;
            var sampler = model.AnimationSampler;
            var meshWorld = mesh.WorldTransform;

            // 使用青色绘制骨骼
            UniformVector3("uColor", new Vector3(0f, 1f, 0.8f));
            gl.Disable(EnableCap.DepthTest);

            Begin();
            foreach (var bone in skeleton.Bones)
            {
                if (bone.Parent == null || bone.Parent.Index < 0)
                    continue;

                Vector3 childPos_modelSpace;
                Vector3 parentPos_modelSpace;

                if (sampler != null && sampler.BonesTransform.Count > bone.Index && sampler.BonesTransform.Count > bone.Parent.Index)
                {
                    // 使用动画数据计算骨骼位置
                    childPos_modelSpace = sampler.BonesTransform[bone.Index].Translation;
                    parentPos_modelSpace = sampler.BonesTransform[bone.Parent.Index].Translation;
                }
                else
                {
                    // 使用绑定姿态
                    childPos_modelSpace = bone.WorldMatrix.Translation;
                    parentPos_modelSpace = bone.Parent.WorldMatrix.Translation;
                }

                var childWorld = Vector3.Transform(childPos_modelSpace, meshWorld);
                var parentWorld = Vector3.Transform(parentPos_modelSpace, meshWorld);

                Line(parentWorld, childWorld);
            }
            End();
            gl.Enable(EnableCap.DepthTest);
        }
    }

    /// <inheritdoc />
    public override void AfterRender(Camera camera)
    {
        int w = (int)camera.Width;
        int h = (int)camera.Height;

        var debugRT = GetRenderTarget(_debugOutput, new Size(w, h));

        // 将调试 RenderTarget 颜色拷贝回当前输出
        gl.BindFramebuffer(GLEnum.ReadFramebuffer, debugRT.FrameBufferId);
        gl.BindFramebuffer(GLEnum.DrawFramebuffer, GetOutputFramebufferId(camera));
        gl.BlitFramebuffer(0, 0, w, h, 0, 0, w, h,
            ClearBufferMask.ColorBufferBit, GLEnum.Nearest);

        // 恢复状态
        gl.Enable(EnableCap.DepthTest);
        gl.DepthMask(true);
        gl.DepthFunc(DepthFunction.Less);
        gl.Disable(EnableCap.Blend);
    }
}
