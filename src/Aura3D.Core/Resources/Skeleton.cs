using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Aura3D.Core.Resources;

public class Skeleton
{
    public List<Bone> Bones = new List<Bone>();

    public Bone Root = new Bone();
}

public class Bone
{
    public string Name = string.Empty;

    public int Index = -1;

    public Matrix4x4 InverseWorldMatrix = Matrix4x4.Identity;

    public Matrix4x4 LocalMatrix = Matrix4x4.Identity;

    public Matrix4x4 WorldMatrix = Matrix4x4.Identity;

    public Bone? Parent = null;

    public List<Bone> Children = new List<Bone>();
}

