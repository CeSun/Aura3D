using System.Drawing;


namespace Aura3D.Core.Renderers;

public  abstract partial class RenderPipeline
{
    Dictionary<string, Dictionary<Size, (RenderTarget, DateTime)>> RenderTargets = new();

    Dictionary<string, RenderTargetConf> RenderTargetConfs = new();

    private void UpdateRenderTargetsLRU()
    {
        foreach (var (name, rtMap) in RenderTargets)
        {
            foreach (var (rtSize, (rt, dateTime)) in rtMap)
            {
                if (DateTime.Now - dateTime > TimeSpan.FromSeconds(1))
                {
                    rt.Destroy(gl!);
                    rtMap.Remove(rtSize);
                    break;
                }
            }
        }
    }

    public RenderTargetConf RegisterRenderTarget(string name)
    {
        if (RenderTargetConfs.ContainsKey(name) == false)
        {
            var rtc = new RenderTargetConf();
            RenderTargetConfs.Add(name, rtc);
            return rtc;
        }
        return RenderTargetConfs[name];
    }


    public RenderTarget GetRenderTarget(string name, Size size)
    {
        if (RenderTargetConfs.TryGetValue(name, out var rtConf))
        {
            if (RenderTargets.TryGetValue(name, out var rtMap) == false)
            {

                rtMap = new Dictionary<Size, (RenderTarget, DateTime)>();

                RenderTargets.Add(name, rtMap);
            }

            if (rtMap.TryGetValue(size, out var rt) == false)
            {
                rt = (new RenderTarget()
                    .SetSize((uint)size.Width, (uint)size.Height)
                    .SetDepthTexture(rtConf.DepthTextureFormat), DateTime.Now);

                foreach(var (textureName, textureFormat) in rtConf.Textures)
                {
                    rt.Item1.AddRenderTexture(textureName, textureFormat);

                    rt.Item1.Upload(gl!);

                    rt.Item1.NeedsUpload = false;

                    AddGpuResource(rt.Item1);
                }
                rtMap.Add(size, rt);
            }
            else
            {
                rt.Item2 = DateTime.Now;
                rtMap[size] = rt;
            }
            return rt.Item1;
        }

        throw new Exception($"RenderTarget '{name}' not found");
    }

}



public class RenderTargetConf
{
    public List<(string, TextureFormat)> Textures = new ();

    HashSet<string> TextureNames = new HashSet<string>();

    public TextureFormat DepthTextureFormat;
    public RenderTargetConf AddTexture(string name, TextureFormat internalFormat)
    {
        if (TextureNames.Contains(name))
            throw new Exception();
        Textures.Add((name, internalFormat));
        TextureNames.Add(name);
        return this;
    }
    public RenderTargetConf SetDepthTexture(TextureFormat textureFormat)
    {
        DepthTextureFormat = textureFormat;
        return this;
    }
}
