using SharpGLTF.Schema2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Model;

public abstract class MaterialExtensionBase
{
    public abstract string Name { get; }
    public abstract void LoadMaterialExtension(ModelRoot modelRoot, SharpGLTF.Schema2.Material modelMaterial, Core.Resources.Material LogicMaterial);

    public virtual void SaveMaterialExtension(
        Core.Resources.Material logicMaterial,
        SharpGLTF.Schema2.Material modelMaterial,
        ModelRoot modelRoot,
        Dictionary<Core.Resources.Texture, int> textureIndexMap)
    { }
}

