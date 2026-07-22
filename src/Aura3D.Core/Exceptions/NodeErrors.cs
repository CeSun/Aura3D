namespace Aura3D.Core.Exceptions;

internal static class NodeErrors
{
    private const string InstancedMeshRequiresGeometryMessage =
        "The source mesh must contain geometry.";

    private const string CameraMustBelongToSceneMessage =
        "The camera must belong to a scene before its output size can be queried without an output texture.";

    public static ArgumentException InstancedMeshRequiresGeometry(string paramName) =>
        new(InstancedMeshRequiresGeometryMessage, paramName);

    public static InvalidOperationException CameraMustBelongToScene() =>
        new(CameraMustBelongToSceneMessage);
}
