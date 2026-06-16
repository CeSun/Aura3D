using Aura3D.Core.Nodes;
using Aura3D.Core.Resources;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Numerics;
using DrawingColor = System.Drawing.Color;

namespace Example.ViewModels;

public partial class SceneEditorViewModel : ViewModelBase
{
    private Node? _selectedNode;
    private SceneNodeItem? _selectedOutlineItem;
    private Camera? _primaryCamera;
    private bool _isSyncingFromNode;

    public SceneEditorViewModel()
    {
        PropertyChanged += OnViewModelPropertyChanged;
        MaterialChannels.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasMaterialChannels));
        MaterialParameters.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasMaterialParameters));
    }

    [ObservableProperty]
    private ObservableCollection<SceneNodeItem> _rootNodes = [];

    [ObservableProperty]
    private string _statusMessage = "Use the outliner or click in the viewport to inspect and edit nodes.";

    [ObservableProperty]
    private string _selectedNodeTypeName = "No selection";

    [ObservableProperty]
    private string _selectedNodePath = "-";

    [ObservableProperty]
    private string _selectionSummary = "Select a node from the scene outliner or viewport.";

    [ObservableProperty]
    private string _modelInfoText = "-";

    [ObservableProperty]
    private string _meshInfoText = "-";

    [ObservableProperty]
    private string _particleInfoText = "-";

    [ObservableProperty]
    private string _instancedMeshInfoText = "-";

    [ObservableProperty]
    private string _instancedMeshGroupInfoText = "-";

    [ObservableProperty]
    private string _boneAttachmentInfoText = "-";

    [ObservableProperty]
    private string _nodeName = string.Empty;

    [ObservableProperty]
    private bool _nodeEnabled = true;

    [ObservableProperty]
    private double _positionX;

    [ObservableProperty]
    private double _positionY;

    [ObservableProperty]
    private double _positionZ;

    [ObservableProperty]
    private double _rotationX;

    [ObservableProperty]
    private double _rotationY;

    [ObservableProperty]
    private double _rotationZ;

    [ObservableProperty]
    private double _scaleX = 1;

    [ObservableProperty]
    private double _scaleY = 1;

    [ObservableProperty]
    private double _scaleZ = 1;

    [ObservableProperty]
    private bool _castShadow;

    [ObservableProperty]
    private Color _lightColor = Colors.White;

    [ObservableProperty]
    private double _directionalIrradiance = 80000;

    [ObservableProperty]
    private double _directionalShadowWidth = 50;

    [ObservableProperty]
    private double _directionalShadowHeight = 50;

    [ObservableProperty]
    private double _directionalShadowNearPlane = 0.1;

    [ObservableProperty]
    private double _directionalShadowFarPlane = 50;

    [ObservableProperty]
    private double _pointLuminousIntensity = 1000;

    [ObservableProperty]
    private double _pointAttenuationRadius = 10;

    [ObservableProperty]
    private double _pointSoftRatio = 0.9;

    [ObservableProperty]
    private double _pointShadowNearPlane = 1;

    [ObservableProperty]
    private double _pointShadowFarPlane = 100;

    [ObservableProperty]
    private double _spotLuminousIntensity = 1000;

    [ObservableProperty]
    private double _spotInnerConeAngle = 10;

    [ObservableProperty]
    private double _spotOuterConeAngle = 15;

    [ObservableProperty]
    private double _spotAttenuationRadius = 10;

    [ObservableProperty]
    private double _spotSoftRatio = 0.9;

    [ObservableProperty]
    private double _spotShadowNearPlane = 1;

    [ObservableProperty]
    private double _spotShadowFarPlane = 100;

    [ObservableProperty]
    private int _cameraProjectionIndex;

    [ObservableProperty]
    private double _cameraFieldOfView = 75;

    [ObservableProperty]
    private double _cameraOrthographicSize = 5;

    [ObservableProperty]
    private double _cameraNearPlane = 1;

    [ObservableProperty]
    private double _cameraFarPlane = 100;

    [ObservableProperty]
    private bool _cameraRenderBackground = true;

    [ObservableProperty]
    private double _modelBoundingBoxPadding;

    [ObservableProperty]
    private Color _materialBaseColor = Colors.White;

    [ObservableProperty]
    private int _materialBlendModeIndex;

    [ObservableProperty]
    private bool _materialDoubleSided;

    [ObservableProperty]
    private double _materialAlphaCutoff = 0.5;

    [ObservableProperty]
    private double _particleMaxParticles = 10000;

    [ObservableProperty]
    private bool _particleEnableVisibilityCulling;

    [ObservableProperty]
    private bool _instancedMeshEnableFrustumCulling = true;

    [ObservableProperty]
    private double _instancedMeshGroupMaxInstancesPerGroup = 1024;

    [ObservableProperty]
    private double _instancedMeshGroupMaxDepth = 6;

    [ObservableProperty]
    private string _boneAttachmentBoneName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<MaterialChannelEditorItem> _materialChannels = [];

    [ObservableProperty]
    private ObservableCollection<MaterialParameterEditorItem> _materialParameters = [];

    [ObservableProperty]
    private string _newMaterialChannelName = "BaseColor";

    [ObservableProperty]
    private string _newMaterialParameterKey = string.Empty;

    [ObservableProperty]
    private int _newMaterialParameterTypeIndex = (int)MaterialParameterValueType.Float;

    [ObservableProperty]
    private string _newMaterialParameterValue = "0";

    public Node? SelectedNode => _selectedNode;

    public bool HasSelection => _selectedNode != null;

    public bool CanDeleteSelectedNode => _selectedNode != null && !ReferenceEquals(_selectedNode, _primaryCamera);

    public bool CanExportSelectedNode => _selectedNode != null;

    public bool CanFrameSelectedNode => _selectedNode != null;

    public bool IsLightSelected => _selectedNode is Light;

    public bool IsDirectionalLightSelected => _selectedNode is DirectionalLight;

    public bool IsPointLightSelected => _selectedNode is PointLight;

    public bool IsSpotLightSelected => _selectedNode is SpotLight;

    public bool IsCameraSelected => _selectedNode is Camera;

    public bool IsModelSelected => _selectedNode is Model;

    public bool IsMeshSelected => _selectedNode is Mesh;

    public bool IsParticleSystemSelected => _selectedNode is ParticleSystem;

    public bool IsInstancedMeshSelected => _selectedNode is InstancedMesh;

    public bool IsInstancedMeshGroupSelected => _selectedNode is InstancedMeshGroup;

    public bool IsBoneAttachmentSelected => _selectedNode is BoneAttachment;

    public bool IsMaterialEditable => _selectedNode is Mesh or InstancedMesh;

    public bool HasMaterialChannels => MaterialChannels.Count > 0;

    public bool HasMaterialParameters => MaterialParameters.Count > 0;

    public bool CanAddMaterialChannel => IsMaterialEditable && !string.IsNullOrWhiteSpace(NewMaterialChannelName);

    public bool CanAddMaterialParameter => IsMaterialEditable && !string.IsNullOrWhiteSpace(NewMaterialParameterKey);

    public bool CanRebuildSelectedNode => _selectedNode is InstancedMeshGroup;

    public void SetPrimaryCamera(Camera camera)
    {
        _primaryCamera = camera;
        OnPropertyChanged(nameof(CanDeleteSelectedNode));
    }

    public void SelectNode(Node? node, SceneNodeItem? outlineItem = null)
    {
        _selectedNode = node;
        _selectedOutlineItem = outlineItem;
        SyncSelectionFromNode();
        NotifySelectionStateChanged();
    }

    public void SyncSelectionFromNode()
    {
        _isSyncingFromNode = true;

        if (_selectedNode == null)
        {
            SelectedNodeTypeName = "No selection";
            SelectedNodePath = "-";
            SelectionSummary = "Select a node from the scene outliner or viewport.";
            ModelInfoText = "-";
            MeshInfoText = "-";
            ParticleInfoText = "-";
            InstancedMeshInfoText = "-";
            InstancedMeshGroupInfoText = "-";
            BoneAttachmentInfoText = "-";
            NodeName = string.Empty;
            NodeEnabled = true;
            PositionX = 0;
            PositionY = 0;
            PositionZ = 0;
            RotationX = 0;
            RotationY = 0;
            RotationZ = 0;
            ScaleX = 1;
            ScaleY = 1;
            ScaleZ = 1;
            CastShadow = false;
            LightColor = Colors.White;
            DirectionalIrradiance = 80000;
            DirectionalShadowWidth = 50;
            DirectionalShadowHeight = 50;
            DirectionalShadowNearPlane = 0.1;
            DirectionalShadowFarPlane = 50;
            PointLuminousIntensity = 1000;
            PointAttenuationRadius = 10;
            PointSoftRatio = 0.9;
            PointShadowNearPlane = 1;
            PointShadowFarPlane = 100;
            SpotLuminousIntensity = 1000;
            SpotInnerConeAngle = 10;
            SpotOuterConeAngle = 15;
            SpotAttenuationRadius = 10;
            SpotSoftRatio = 0.9;
            SpotShadowNearPlane = 1;
            SpotShadowFarPlane = 100;
            CameraProjectionIndex = 0;
            CameraFieldOfView = 75;
            CameraOrthographicSize = 5;
            CameraNearPlane = 1;
            CameraFarPlane = 100;
            CameraRenderBackground = true;
            ModelBoundingBoxPadding = 0;
            MaterialBaseColor = Colors.White;
            MaterialBlendModeIndex = 0;
            MaterialDoubleSided = false;
            MaterialAlphaCutoff = 0.5;
            ParticleMaxParticles = 10000;
            ParticleEnableVisibilityCulling = false;
            InstancedMeshEnableFrustumCulling = true;
            InstancedMeshGroupMaxInstancesPerGroup = 1024;
            InstancedMeshGroupMaxDepth = 6;
            BoneAttachmentBoneName = string.Empty;
            NewMaterialChannelName = "BaseColor";
            NewMaterialParameterKey = string.Empty;
            NewMaterialParameterTypeIndex = (int)MaterialParameterValueType.Float;
            NewMaterialParameterValue = "0";
            RefreshMaterialEditorCollections(null);

            _isSyncingFromNode = false;
            return;
        }

        NodeName = _selectedNode.Name;
        NodeEnabled = _selectedNode.Enable;
        PositionX = _selectedNode.Position.X;
        PositionY = _selectedNode.Position.Y;
        PositionZ = _selectedNode.Position.Z;
        RotationX = _selectedNode.RotationDegrees.X;
        RotationY = _selectedNode.RotationDegrees.Y;
        RotationZ = _selectedNode.RotationDegrees.Z;
        ScaleX = _selectedNode.Scale.X;
        ScaleY = _selectedNode.Scale.Y;
        ScaleZ = _selectedNode.Scale.Z;

        if (_selectedNode is Light light)
        {
            CastShadow = light.CastShadow;
            LightColor = ToAvaloniaColor(light.LightColor);
        }

        if (_selectedNode is DirectionalLight directionalLight)
        {
            DirectionalIrradiance = directionalLight.Irradiance;
            DirectionalShadowWidth = directionalLight.ShadowConfig.Width;
            DirectionalShadowHeight = directionalLight.ShadowConfig.Height;
            DirectionalShadowNearPlane = directionalLight.ShadowConfig.NearPlane;
            DirectionalShadowFarPlane = directionalLight.ShadowConfig.FarPlane;
        }

        if (_selectedNode is PointLight pointLight)
        {
            PointLuminousIntensity = pointLight.LuminousIntensity;
            PointAttenuationRadius = pointLight.AttenuationRadius;
            PointSoftRatio = pointLight.SoftRatio;
            PointShadowNearPlane = pointLight.ShadowConfig.NearPlane;
            PointShadowFarPlane = pointLight.ShadowConfig.FarPlane;
        }

        if (_selectedNode is SpotLight spotLight)
        {
            SpotLuminousIntensity = spotLight.LuminousIntensity;
            SpotInnerConeAngle = spotLight.InnerConeAngleDegree;
            SpotOuterConeAngle = spotLight.OuterAngleDegree;
            SpotAttenuationRadius = spotLight.AttenuationRadius;
            SpotSoftRatio = spotLight.SoftRatio;
            SpotShadowNearPlane = spotLight.ShadowConfig.NearPlane;
            SpotShadowFarPlane = spotLight.ShadowConfig.FarPlane;
        }

        if (_selectedNode is Camera camera)
        {
            CameraProjectionIndex = camera.ProjectionType == ProjectionType.Perspective ? 0 : 1;
            CameraFieldOfView = camera.FieldOfView;
            CameraOrthographicSize = camera.OrthographicSize;
            CameraNearPlane = camera.NearPlane;
            CameraFarPlane = camera.FarPlane;
            CameraRenderBackground = camera.IsRenderBackground;
        }

        if (_selectedNode is Model model)
        {
            ModelBoundingBoxPadding = model.BoundingBoxPadding;
        }

        var material = GetEditableMaterial();
        if (material != null)
        {
            MaterialBaseColor = GetMaterialBaseColor(material);
            MaterialBlendModeIndex = (int)material.BlendMode;
            MaterialDoubleSided = material.DoubleSided;
            MaterialAlphaCutoff = material.AlphaCutoff;
        }
        else
        {
            MaterialBaseColor = Colors.White;
            MaterialBlendModeIndex = 0;
            MaterialDoubleSided = false;
            MaterialAlphaCutoff = 0.5;
        }

        if (_selectedNode is ParticleSystem particleSystem)
        {
            ParticleMaxParticles = particleSystem.MaxParticles;
            ParticleEnableVisibilityCulling = particleSystem.EnableVisibilityCulling;
        }

        if (_selectedNode is InstancedMesh instancedMesh)
        {
            InstancedMeshEnableFrustumCulling = instancedMesh.EnableFrustumCulling;
        }

        if (_selectedNode is InstancedMeshGroup instancedMeshGroup)
        {
            InstancedMeshGroupMaxInstancesPerGroup = instancedMeshGroup.MaxInstancesPerGroup;
            InstancedMeshGroupMaxDepth = instancedMeshGroup.MaxDepth;
        }

        if (_selectedNode is BoneAttachment boneAttachment)
        {
            BoneAttachmentBoneName = boneAttachment.BoneName;
        }

        RefreshMaterialEditorCollections(material);
        RefreshSelectionRuntimeInfo();

        _isSyncingFromNode = false;
    }

    public void RefreshSelectionRuntimeInfo()
    {
        if (_selectedNode == null)
            return;

        _isSyncingFromNode = true;

        SelectedNodeTypeName = _selectedNode.GetType().Name;
        SelectedNodePath = BuildNodePath(_selectedNode);
        SelectionSummary = $"{_selectedNode.Children.Count} child nodes | Enabled: {_selectedNode.Enable}";

        if (_selectedNode is Model model)
        {
            ModelInfoText = $"Meshes: {model.Meshes.Count} | Skinned: {model.IsSkinnedModel}";
        }
        else
        {
            ModelInfoText = "-";
        }

        if (_selectedNode is Mesh mesh)
        {
            var vertexCount = mesh.Geometry?.VertexCount ?? 0;
            var primitiveType = mesh.Geometry?.PrimitiveType.ToString() ?? "None";
            var hasMaterial = mesh.Material != null ? "Yes" : "No";
            MeshInfoText = $"Vertices: {vertexCount} | Primitive: {primitiveType} | Material: {hasMaterial}";
        }
        else
        {
            MeshInfoText = "-";
        }

        if (_selectedNode is ParticleSystem particleSystem)
        {
            ParticleInfoText = $"Emitters: {particleSystem.Emitters.Count} | Active: {particleSystem.ActiveCount} | Playing: {particleSystem.IsPlaying}";
        }
        else
        {
            ParticleInfoText = "-";
        }

        if (_selectedNode is InstancedMesh instancedMesh)
        {
            InstancedMeshInfoText = $"Instances: {instancedMesh.InstanceCount} | Culling: {instancedMesh.EnableFrustumCulling} | Material: {(instancedMesh.Material != null ? "Yes" : "No")}";
        }
        else
        {
            InstancedMeshInfoText = "-";
        }

        if (_selectedNode is InstancedMeshGroup instancedMeshGroup)
        {
            InstancedMeshGroupInfoText = $"Groups: {instancedMeshGroup.GroupCount} | Instances: {instancedMeshGroup.InstanceCount} | In-place Updates: {instancedMeshGroup.InPlaceUpdateCount} | Rebuilds: {instancedMeshGroup.RebuildCount}";
        }
        else
        {
            InstancedMeshGroupInfoText = "-";
        }

        if (_selectedNode is BoneAttachment boneAttachment)
        {
            var meshName = boneAttachment.Mesh?.Name;
            BoneAttachmentInfoText = $"Target Mesh: {(string.IsNullOrWhiteSpace(meshName) ? "-" : meshName)} | Bone: {boneAttachment.BoneName}";
        }
        else
        {
            BoneAttachmentInfoText = "-";
        }

        _isSyncingFromNode = false;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isSyncingFromNode || _selectedNode == null)
            return;

        switch (e.PropertyName)
        {
            case nameof(NodeName):
                _selectedNode.Name = NodeName;
                _selectedOutlineItem?.Refresh();
                RefreshSelectionRuntimeInfo();
                break;
            case nameof(NodeEnabled):
                _selectedNode.Enable = NodeEnabled;
                RefreshSelectionRuntimeInfo();
                break;
            case nameof(PositionX):
            case nameof(PositionY):
            case nameof(PositionZ):
                _selectedNode.Position = new Vector3((float)PositionX, (float)PositionY, (float)PositionZ);
                break;
            case nameof(RotationX):
            case nameof(RotationY):
            case nameof(RotationZ):
                _selectedNode.RotationDegrees = new Vector3((float)RotationX, (float)RotationY, (float)RotationZ);
                break;
            case nameof(ScaleX):
            case nameof(ScaleY):
            case nameof(ScaleZ):
                _selectedNode.Scale = new Vector3(
                    Math.Max(0.001f, (float)ScaleX),
                    Math.Max(0.001f, (float)ScaleY),
                    Math.Max(0.001f, (float)ScaleZ));
                break;
            case nameof(CastShadow):
                if (_selectedNode is Light light)
                    light.CastShadow = CastShadow;
                break;
            case nameof(LightColor):
                if (_selectedNode is Light coloredLight)
                    coloredLight.LightColor = ToDrawingColor(LightColor);
                break;
            case nameof(DirectionalIrradiance):
                if (_selectedNode is DirectionalLight directionalLight)
                    directionalLight.Irradiance = (float)DirectionalIrradiance;
                break;
            case nameof(DirectionalShadowWidth):
                if (_selectedNode is DirectionalLight directionalWidthLight)
                    directionalWidthLight.ShadowConfig.Width = Math.Max(1, (int)Math.Round(DirectionalShadowWidth));
                break;
            case nameof(DirectionalShadowHeight):
                if (_selectedNode is DirectionalLight directionalHeightLight)
                    directionalHeightLight.ShadowConfig.Height = Math.Max(1, (int)Math.Round(DirectionalShadowHeight));
                break;
            case nameof(DirectionalShadowNearPlane):
                if (_selectedNode is DirectionalLight directionalNearLight)
                    directionalNearLight.ShadowConfig.NearPlane = Math.Max(0.001f, (float)DirectionalShadowNearPlane);
                break;
            case nameof(DirectionalShadowFarPlane):
                if (_selectedNode is DirectionalLight directionalFarLight)
                    directionalFarLight.ShadowConfig.FarPlane = Math.Max((float)DirectionalShadowNearPlane + 0.001f, (float)DirectionalShadowFarPlane);
                break;
            case nameof(PointLuminousIntensity):
                if (_selectedNode is PointLight pointIntensityLight)
                    pointIntensityLight.LuminousIntensity = Math.Max(0, (float)PointLuminousIntensity);
                break;
            case nameof(PointAttenuationRadius):
                if (_selectedNode is PointLight pointRadiusLight)
                    pointRadiusLight.AttenuationRadius = Math.Max(0.001f, (float)PointAttenuationRadius);
                break;
            case nameof(PointSoftRatio):
                if (_selectedNode is PointLight pointSoftLight)
                    pointSoftLight.SoftRatio = Math.Clamp((float)PointSoftRatio, 0f, 1f);
                break;
            case nameof(PointShadowNearPlane):
                if (_selectedNode is PointLight pointNearLight)
                    pointNearLight.ShadowConfig.NearPlane = Math.Max(0.001f, (float)PointShadowNearPlane);
                break;
            case nameof(PointShadowFarPlane):
                if (_selectedNode is PointLight pointFarLight)
                    pointFarLight.ShadowConfig.FarPlane = Math.Max((float)PointShadowNearPlane + 0.001f, (float)PointShadowFarPlane);
                break;
            case nameof(SpotLuminousIntensity):
                if (_selectedNode is SpotLight spotIntensityLight)
                    spotIntensityLight.LuminousIntensity = Math.Max(0, (float)SpotLuminousIntensity);
                break;
            case nameof(SpotInnerConeAngle):
                if (_selectedNode is SpotLight spotInnerLight)
                    spotInnerLight.InnerConeAngleDegree = Math.Max(0, (float)SpotInnerConeAngle);
                break;
            case nameof(SpotOuterConeAngle):
                if (_selectedNode is SpotLight spotOuterLight)
                    spotOuterLight.OuterAngleDegree = Math.Max((float)SpotInnerConeAngle, (float)SpotOuterConeAngle);
                break;
            case nameof(SpotAttenuationRadius):
                if (_selectedNode is SpotLight spotRadiusLight)
                    spotRadiusLight.AttenuationRadius = Math.Max(0.001f, (float)SpotAttenuationRadius);
                break;
            case nameof(SpotSoftRatio):
                if (_selectedNode is SpotLight spotSoftLight)
                    spotSoftLight.SoftRatio = Math.Clamp((float)SpotSoftRatio, 0f, 1f);
                break;
            case nameof(SpotShadowNearPlane):
                if (_selectedNode is SpotLight spotNearLight)
                    spotNearLight.ShadowConfig.NearPlane = Math.Max(0.001f, (float)SpotShadowNearPlane);
                break;
            case nameof(SpotShadowFarPlane):
                if (_selectedNode is SpotLight spotFarLight)
                    spotFarLight.ShadowConfig.FarPlane = Math.Max((float)SpotShadowNearPlane + 0.001f, (float)SpotShadowFarPlane);
                break;
            case nameof(CameraProjectionIndex):
                if (_selectedNode is Camera projectionCamera)
                    projectionCamera.ProjectionType = CameraProjectionIndex == 0 ? ProjectionType.Perspective : ProjectionType.Orthographic;
                break;
            case nameof(CameraFieldOfView):
                if (_selectedNode is Camera fovCamera)
                    fovCamera.FieldOfView = Math.Clamp((float)CameraFieldOfView, 1f, 179f);
                break;
            case nameof(CameraOrthographicSize):
                if (_selectedNode is Camera orthoCamera)
                    orthoCamera.OrthographicSize = Math.Max(0.001f, (float)CameraOrthographicSize);
                break;
            case nameof(CameraNearPlane):
                if (_selectedNode is Camera nearCamera)
                    nearCamera.NearPlane = Math.Max(0.001f, (float)CameraNearPlane);
                break;
            case nameof(CameraFarPlane):
                if (_selectedNode is Camera farCamera)
                    farCamera.FarPlane = Math.Max((float)CameraNearPlane + 0.001f, (float)CameraFarPlane);
                break;
            case nameof(CameraRenderBackground):
                if (_selectedNode is Camera backgroundCamera)
                    backgroundCamera.IsRenderBackground = CameraRenderBackground;
                break;
            case nameof(ModelBoundingBoxPadding):
                if (_selectedNode is Model model)
                    model.BoundingBoxPadding = Math.Max(0, (float)ModelBoundingBoxPadding);
                break;
            case nameof(MaterialBaseColor):
                EnsureEditableMaterial().BaseColor = Texture.CreateFromColor(ToDrawingColor(MaterialBaseColor));
                break;
            case nameof(MaterialBlendModeIndex):
                EnsureEditableMaterial().BlendMode = (BlendMode)Math.Clamp(MaterialBlendModeIndex, 0, 2);
                break;
            case nameof(MaterialDoubleSided):
                EnsureEditableMaterial().DoubleSided = MaterialDoubleSided;
                break;
            case nameof(MaterialAlphaCutoff):
                EnsureEditableMaterial().AlphaCutoff = Math.Clamp((float)MaterialAlphaCutoff, 0f, 1f);
                break;
            case nameof(ParticleMaxParticles):
                if (_selectedNode is ParticleSystem particleSystem)
                    particleSystem.MaxParticles = Math.Max(0, (int)Math.Round(ParticleMaxParticles));
                break;
            case nameof(ParticleEnableVisibilityCulling):
                if (_selectedNode is ParticleSystem particleVisibilitySystem)
                    particleVisibilitySystem.EnableVisibilityCulling = ParticleEnableVisibilityCulling;
                break;
            case nameof(InstancedMeshEnableFrustumCulling):
                if (_selectedNode is InstancedMesh instancedMesh)
                {
                    instancedMesh.EnableFrustumCulling = InstancedMeshEnableFrustumCulling;
                    RefreshSelectionRuntimeInfo();
                }
                break;
            case nameof(InstancedMeshGroupMaxInstancesPerGroup):
                if (_selectedNode is InstancedMeshGroup instancedMeshGroupMax)
                {
                    instancedMeshGroupMax.MaxInstancesPerGroup = Math.Max(1, (int)Math.Round(InstancedMeshGroupMaxInstancesPerGroup));
                    instancedMeshGroupMax.Build();
                }
                break;
            case nameof(InstancedMeshGroupMaxDepth):
                if (_selectedNode is InstancedMeshGroup instancedMeshGroupDepth)
                {
                    instancedMeshGroupDepth.MaxDepth = Math.Max(1, (int)Math.Round(InstancedMeshGroupMaxDepth));
                    instancedMeshGroupDepth.Build();
                }
                break;
            case nameof(BoneAttachmentBoneName):
                if (_selectedNode is BoneAttachment boneAttachment)
                {
                    boneAttachment.BoneName = BoneAttachmentBoneName;
                    RefreshSelectionRuntimeInfo();
                }
                break;
            case nameof(NewMaterialChannelName):
                OnPropertyChanged(nameof(CanAddMaterialChannel));
                break;
            case nameof(NewMaterialParameterKey):
                OnPropertyChanged(nameof(CanAddMaterialParameter));
                break;
        }
    }

    private void NotifySelectionStateChanged()
    {
        OnPropertyChanged(nameof(SelectedNode));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanDeleteSelectedNode));
        OnPropertyChanged(nameof(CanExportSelectedNode));
        OnPropertyChanged(nameof(CanFrameSelectedNode));
        OnPropertyChanged(nameof(IsLightSelected));
        OnPropertyChanged(nameof(IsDirectionalLightSelected));
        OnPropertyChanged(nameof(IsPointLightSelected));
        OnPropertyChanged(nameof(IsSpotLightSelected));
        OnPropertyChanged(nameof(IsCameraSelected));
        OnPropertyChanged(nameof(IsModelSelected));
        OnPropertyChanged(nameof(IsMeshSelected));
        OnPropertyChanged(nameof(IsParticleSystemSelected));
        OnPropertyChanged(nameof(IsInstancedMeshSelected));
        OnPropertyChanged(nameof(IsInstancedMeshGroupSelected));
        OnPropertyChanged(nameof(IsBoneAttachmentSelected));
        OnPropertyChanged(nameof(IsMaterialEditable));
        OnPropertyChanged(nameof(CanAddMaterialChannel));
        OnPropertyChanged(nameof(CanAddMaterialParameter));
        OnPropertyChanged(nameof(HasMaterialChannels));
        OnPropertyChanged(nameof(HasMaterialParameters));
        OnPropertyChanged(nameof(CanRebuildSelectedNode));
    }

    public bool TryAddMaterialChannel(out string message)
    {
        var name = NewMaterialChannelName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            message = "Texture channel name cannot be empty.";
            return false;
        }

        var material = EnsureEditableMaterial();
        if (material.Channels.Any(channel => string.Equals(channel.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            message = $"Texture channel {name} already exists.";
            return false;
        }

        material.Channels.Add(new Channel { Name = name });
        RefreshMaterialEditorCollections(material);
        NewMaterialChannelName = string.Empty;
        message = $"Added texture channel {name}.";
        return true;
    }

    public bool TrySetMaterialChannelTexture(MaterialChannelEditorItem item, ITexture? texture, out string message)
    {
        var material = GetEditableMaterial();
        if (material == null)
        {
            message = "No editable material is selected.";
            return false;
        }

        material.SetTexture(item.Name, texture);
        RefreshMaterialEditorCollections(material);
        message = texture == null
            ? $"Cleared texture for channel {item.Name}."
            : $"Updated texture for channel {item.Name}.";
        return true;
    }

    public bool TryRemoveMaterialChannel(MaterialChannelEditorItem item, out string message)
    {
        var material = GetEditableMaterial();
        if (material == null)
        {
            message = "No editable material is selected.";
            return false;
        }

        var removed = material.Channels.RemoveAll(channel => string.Equals(channel.Name, item.Name, StringComparison.OrdinalIgnoreCase));
        if (removed == 0)
        {
            message = $"Texture channel {item.Name} was not found.";
            return false;
        }

        RefreshMaterialEditorCollections(material);
        message = $"Removed texture channel {item.Name}.";
        return true;
    }

    public bool TryAddMaterialParameter(out string message)
    {
        var key = NewMaterialParameterKey.Trim();
        if (string.IsNullOrWhiteSpace(key))
        {
            message = "Material parameter key cannot be empty.";
            return false;
        }

        var material = EnsureEditableMaterial();
        if (!TryParseMaterialParameterValue((MaterialParameterValueType)NewMaterialParameterTypeIndex, NewMaterialParameterValue, out var value, out message))
            return false;

        material.SetParameterValue(key, value);
        RefreshMaterialEditorCollections(material);
        NewMaterialParameterKey = string.Empty;
        NewMaterialParameterValue = "0";
        message = $"Added material parameter {key}.";
        return true;
    }

    public bool TryApplyMaterialParameter(MaterialParameterEditorItem item, out string message)
    {
        var material = GetEditableMaterial();
        if (material == null)
        {
            message = "No editable material is selected.";
            return false;
        }

        if (!TryParseMaterialParameterValue(item.ValueType, item.ValueText, out var value, out message))
            return false;

        material.SetParameterValue(item.Key, value);
        item.ValueText = FormatMaterialParameterValue(value);
        message = $"Updated material parameter {item.Key}.";
        return true;
    }

    public bool TryRemoveMaterialParameter(MaterialParameterEditorItem item, out string message)
    {
        var material = GetEditableMaterial();
        if (material == null)
        {
            message = "No editable material is selected.";
            return false;
        }

        material.RemoveParameterValue(item.Key);
        RefreshMaterialEditorCollections(material);
        message = $"Removed material parameter {item.Key}.";
        return true;
    }

    private static string BuildNodePath(Node node)
    {
        var names = new Stack<string>();
        Node? current = node;
        while (current != null)
        {
            names.Push(string.IsNullOrWhiteSpace(current.Name) ? current.GetType().Name : current.Name);
            current = current.Parent;
        }

        return string.Join(" / ", names);
    }

    private static Color ToAvaloniaColor(DrawingColor color)
    {
        return Color.FromArgb(color.A, color.R, color.G, color.B);
    }

    private static DrawingColor ToDrawingColor(Color color)
    {
        return DrawingColor.FromArgb(color.A, color.R, color.G, color.B);
    }

    private void RefreshMaterialEditorCollections(Material? material)
    {
        MaterialChannels.Clear();
        MaterialParameters.Clear();

        if (material == null)
            return;

        foreach (var channel in material.Channels.OrderBy(static channel => channel.Name, StringComparer.OrdinalIgnoreCase))
        {
            MaterialChannels.Add(new MaterialChannelEditorItem(channel.Name, DescribeTexture(channel.Texture)));
        }

        foreach (var parameter in material.EnumerateParameters().OrderBy(static parameter => parameter.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (TryGetMaterialParameterValueType(parameter.Value, out var valueType))
            {
                MaterialParameters.Add(new MaterialParameterEditorItem(parameter.Key, valueType, FormatMaterialParameterValue(parameter.Value)));
            }
        }
    }

    private Material? GetEditableMaterial()
    {
        return _selectedNode switch
        {
            Mesh mesh => mesh.Material,
            InstancedMesh instancedMesh => instancedMesh.Material,
            _ => null
        };
    }

    private Material EnsureEditableMaterial()
    {
        var material = GetEditableMaterial();
        if (material != null)
            return material;

        material = new Material
        {
            BaseColor = Texture.CreateFromColor(DrawingColor.White)
        };

        switch (_selectedNode)
        {
            case Mesh mesh:
                mesh.Material = material;
                break;
            case InstancedMesh instancedMesh:
                instancedMesh.Material = material;
                break;
            default:
                throw new InvalidOperationException("Selected node does not support editable material.");
        }

        return material;
    }

    private static Color GetMaterialBaseColor(Material material)
    {
        if (material.BaseColor is Texture texture && texture.LdrData.Count >= 3)
        {
            var alpha = texture.LdrData.Count >= 4 ? texture.LdrData[3] : (byte)255;
            return Color.FromArgb(alpha, texture.LdrData[0], texture.LdrData[1], texture.LdrData[2]);
        }

        return Colors.White;
    }

    private static string DescribeTexture(ITexture? texture)
    {
        return texture switch
        {
            Texture t => $"{t.Width}x{t.Height} {t.ColorFormat}",
            null => "No texture",
            _ => texture.GetType().Name
        };
    }

    private static bool TryGetMaterialParameterValueType(object value, out MaterialParameterValueType valueType)
    {
        switch (value)
        {
            case bool:
                valueType = MaterialParameterValueType.Bool;
                return true;
            case int:
                valueType = MaterialParameterValueType.Int;
                return true;
            case uint:
                valueType = MaterialParameterValueType.UInt;
                return true;
            case float:
                valueType = MaterialParameterValueType.Float;
                return true;
            case double:
                valueType = MaterialParameterValueType.Double;
                return true;
            case long:
                valueType = MaterialParameterValueType.Long;
                return true;
            case ulong:
                valueType = MaterialParameterValueType.ULong;
                return true;
            case string:
                valueType = MaterialParameterValueType.String;
                return true;
            case Vector2:
                valueType = MaterialParameterValueType.Vector2;
                return true;
            case Vector3:
                valueType = MaterialParameterValueType.Vector3;
                return true;
            case Vector4:
                valueType = MaterialParameterValueType.Vector4;
                return true;
            case DrawingColor:
                valueType = MaterialParameterValueType.Color;
                return true;
            default:
                valueType = default;
                return false;
        }
    }

    private static string FormatMaterialParameterValue(object value)
    {
        return value switch
        {
            bool boolValue => boolValue ? "true" : "false",
            int intValue => intValue.ToString(CultureInfo.InvariantCulture),
            uint uintValue => uintValue.ToString(CultureInfo.InvariantCulture),
            float floatValue => floatValue.ToString("0.####", CultureInfo.InvariantCulture),
            double doubleValue => doubleValue.ToString("0.####", CultureInfo.InvariantCulture),
            long longValue => longValue.ToString(CultureInfo.InvariantCulture),
            ulong ulongValue => ulongValue.ToString(CultureInfo.InvariantCulture),
            string stringValue => stringValue,
            Vector2 vector2Value => $"{vector2Value.X.ToString("0.####", CultureInfo.InvariantCulture)}, {vector2Value.Y.ToString("0.####", CultureInfo.InvariantCulture)}",
            Vector3 vector3Value => $"{vector3Value.X.ToString("0.####", CultureInfo.InvariantCulture)}, {vector3Value.Y.ToString("0.####", CultureInfo.InvariantCulture)}, {vector3Value.Z.ToString("0.####", CultureInfo.InvariantCulture)}",
            Vector4 vector4Value => $"{vector4Value.X.ToString("0.####", CultureInfo.InvariantCulture)}, {vector4Value.Y.ToString("0.####", CultureInfo.InvariantCulture)}, {vector4Value.Z.ToString("0.####", CultureInfo.InvariantCulture)}, {vector4Value.W.ToString("0.####", CultureInfo.InvariantCulture)}",
            DrawingColor colorValue => $"#{colorValue.A:X2}{colorValue.R:X2}{colorValue.G:X2}{colorValue.B:X2}",
            _ => value.ToString() ?? string.Empty
        };
    }

    private static bool TryParseMaterialParameterValue(MaterialParameterValueType valueType, string text, out object value, out string errorMessage)
    {
        var normalizedText = text.Trim();
        switch (valueType)
        {
            case MaterialParameterValueType.Bool:
                if (bool.TryParse(normalizedText, out var boolValue))
                {
                    value = boolValue;
                    errorMessage = string.Empty;
                    return true;
                }
                break;
            case MaterialParameterValueType.Int:
                if (int.TryParse(normalizedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    value = intValue;
                    errorMessage = string.Empty;
                    return true;
                }
                break;
            case MaterialParameterValueType.UInt:
                if (uint.TryParse(normalizedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uintValue))
                {
                    value = uintValue;
                    errorMessage = string.Empty;
                    return true;
                }
                break;
            case MaterialParameterValueType.Float:
                if (float.TryParse(normalizedText, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var floatValue))
                {
                    value = floatValue;
                    errorMessage = string.Empty;
                    return true;
                }
                break;
            case MaterialParameterValueType.Double:
                if (double.TryParse(normalizedText, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out var doubleValue))
                {
                    value = doubleValue;
                    errorMessage = string.Empty;
                    return true;
                }
                break;
            case MaterialParameterValueType.Long:
                if (long.TryParse(normalizedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
                {
                    value = longValue;
                    errorMessage = string.Empty;
                    return true;
                }
                break;
            case MaterialParameterValueType.ULong:
                if (ulong.TryParse(normalizedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ulongValue))
                {
                    value = ulongValue;
                    errorMessage = string.Empty;
                    return true;
                }
                break;
            case MaterialParameterValueType.String:
                value = normalizedText;
                errorMessage = string.Empty;
                return true;
            case MaterialParameterValueType.Vector2:
                if (TryParseFloatSequence(normalizedText, 2, out var vector2Values))
                {
                    value = new Vector2(vector2Values[0], vector2Values[1]);
                    errorMessage = string.Empty;
                    return true;
                }
                break;
            case MaterialParameterValueType.Vector3:
                if (TryParseFloatSequence(normalizedText, 3, out var vector3Values))
                {
                    value = new Vector3(vector3Values[0], vector3Values[1], vector3Values[2]);
                    errorMessage = string.Empty;
                    return true;
                }
                break;
            case MaterialParameterValueType.Vector4:
                if (TryParseFloatSequence(normalizedText, 4, out var vector4Values))
                {
                    value = new Vector4(vector4Values[0], vector4Values[1], vector4Values[2], vector4Values[3]);
                    errorMessage = string.Empty;
                    return true;
                }
                break;
            case MaterialParameterValueType.Color:
                if (TryParseDrawingColor(normalizedText, out var colorValue))
                {
                    value = colorValue;
                    errorMessage = string.Empty;
                    return true;
                }
                break;
        }

        value = string.Empty;
        errorMessage = $"Failed to parse {valueType} value: {text}";
        return false;
    }

    private static bool TryParseFloatSequence(string text, int count, out float[] values)
    {
        values = [];
        var parts = text
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (parts.Length != count)
            return false;

        values = new float[count];
        for (var index = 0; index < count; index++)
        {
            if (!float.TryParse(parts[index], NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out values[index]))
                return false;
        }

        return true;
    }

    private static bool TryParseDrawingColor(string text, out DrawingColor color)
    {
        color = DrawingColor.Empty;

        if (text.StartsWith('#'))
        {
            var hex = text[1..];
            if (hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            {
                color = DrawingColor.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
                return true;
            }

            if (hex.Length == 8 && uint.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var argb))
            {
                color = DrawingColor.FromArgb(
                    (byte)((argb >> 24) & 0xFF),
                    (byte)((argb >> 16) & 0xFF),
                    (byte)((argb >> 8) & 0xFF),
                    (byte)(argb & 0xFF));
                return true;
            }
        }

        var parts = text.Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is 3 or 4)
        {
            var numbers = new byte[4];
            numbers[0] = 255;
            var startIndex = parts.Length == 4 ? 0 : 1;
            for (var index = 0; index < parts.Length; index++)
            {
                if (!byte.TryParse(parts[index], NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[startIndex + index]))
                {
                    color = DrawingColor.Empty;
                    return false;
                }
            }

            color = DrawingColor.FromArgb(numbers[0], numbers[1], numbers[2], numbers[3]);
            return true;
        }

        return false;
    }
}

public partial class SceneNodeItem(Node node) : ObservableObject
{
    [ObservableProperty]
    private string _displayName = BuildDisplayName(node);

    public Node Node { get; } = node;

    public ObservableCollection<SceneNodeItem> Children { get; } = [];

    public void Refresh()
    {
        DisplayName = BuildDisplayName(Node);
    }

    private static string BuildDisplayName(Node node)
    {
        var name = string.IsNullOrWhiteSpace(node.Name) ? "Unnamed" : node.Name;
        return $"{name} ({node.GetType().Name})";
    }
}

public sealed class MaterialChannelEditorItem(string name, string textureInfo)
{
    public string Name { get; } = name;

    public string TextureInfo { get; } = textureInfo;
}

public partial class MaterialParameterEditorItem(string key, MaterialParameterValueType valueType, string valueText) : ObservableObject
{
    public string Key { get; } = key;

    public MaterialParameterValueType ValueType { get; } = valueType;

    public string TypeName => ValueType.ToString();

    [ObservableProperty]
    private string _valueText = valueText;
}

public enum MaterialParameterValueType
{
    Bool,
    Int,
    UInt,
    Float,
    Double,
    Long,
    ULong,
    String,
    Vector2,
    Vector3,
    Vector4,
    Color,
}
