using Silk.NET.OpenGLES;
using System.Reflection.Metadata.Ecma335;

namespace Aura3D.Core.Nodes;

public class Model : Node
{
    public IReadOnlyList<Mesh> Meshes => GetNodesInChildren<Mesh>();

    public virtual Model Clone(CopyType copyType = CopyType.SharedResource)
    {
        var model = (Model)clone(this, null);

        foreach(var mesh in model.Meshes)
        {
            if (copyType == CopyType.SharedResourceData)
            {
                mesh.Geometry = mesh.Geometry?.Clone();
                mesh.Material = mesh.Material?.Clone();
            }
            else if (copyType == CopyType.FullCopy)
            {
                mesh.Geometry = mesh.Geometry?.DeepClone();
                mesh.Material = mesh.Material?.DeepClone();
            }
        }

        return model;
    }

    protected Node clone(Node node, Node? parentNode)
    {
        Node? cloneNode = null;
        if (node is SkinnedModel skinnedModel)
        {
            cloneNode = new SkinnedModel();
            ((SkinnedModel)cloneNode).Skeleton = skinnedModel.Skeleton;
            ((SkinnedModel)cloneNode).AnimationSampler = skinnedModel.AnimationSampler;

        }
        else if (node is Model model)
        {
            cloneNode = new Model();
        }
        else if (node is SkinnedMesh skinnedMesh)
        {
            cloneNode = new SkinnedMesh();
            ((SkinnedMesh)cloneNode).Skeleton = skinnedMesh.Skeleton;
            ((SkinnedMesh)cloneNode).SkinnedModel = skinnedMesh.SkinnedModel;
            ((SkinnedMesh)cloneNode).Geometry = skinnedMesh.Geometry;
            ((SkinnedMesh)cloneNode).Material = skinnedMesh.Material;
        }
        else if (node is Mesh mesh)
        {
            cloneNode = new Mesh();
            ((Mesh)cloneNode).Geometry = mesh.Geometry;
            ((Mesh)cloneNode).Material = mesh.Material;
        }
        else
        {
            cloneNode = new Node();
        }
        
        if (parentNode != null)
        {
            parentNode.AddChild(cloneNode);
        }

        cloneNode.Transform = node.Transform;
        cloneNode.Enable = node.Enable;
        cloneNode.Name = node.Name;

        foreach (var child in node.Children)
        {
            clone(child, cloneNode);
        }
        return cloneNode;

    }
}
