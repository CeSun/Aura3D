using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Resources;

public interface ITexture
{
    public uint TextureId { get; }
    
    public uint Width { get; }

    public uint Height { get; }
}

public interface ICubeTexture
{
    public uint TextureId { get; }

    public uint Width { get; }

    public uint Height { get; }
}