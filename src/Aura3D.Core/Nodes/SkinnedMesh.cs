using Aura3D.Core.Resources;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Nodes;

public class SkinnedMesh : Mesh
{
    public Skeleton? Skeleton { get; set; } = null;

    public override bool IsSkinnedMesh => true;

    public SkinnedModel? SkinnedModel { get; set; }


}
