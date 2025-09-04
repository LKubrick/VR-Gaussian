using UnityEngine;
using System;
using System.IO;
using System.Linq;
using Holo;
using System.Collections.Generic;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using IthacaLib.Common;
using UnityEditor;



#if USINGAVPRO
using RenderHeads.Media.AVProVideo;
#else
using UnityEngine.Video;
#endif

namespace IthacaLib
{
    public class IthacaPipelineManager : MonoBehaviour
    {
        public enum RenderType
        {
            pointCloud,
            Mesh,
            MeshAndroid,
            RayMarching,
            PerspectiveFusion,
            PerspectiveFusionAndroid
        }
        [Tooltip("If this is checked, the metadata file will be read to get all the parameters needed for the playback")]
        public bool importSettings = true;
        [HideInInspector] //experimental
        [SerializeField, Tooltip("If this is checked, you can playback the video while in editor")]
        private bool useInEditor;
#if USINGAVPRO
        public MediaReference mediaReference;
#else
        public VideoClip clip;
#endif
        [SerializeField, Tooltip("If true, the file will be read automatically when unity plays")]
        private bool autoPlay;
        [SerializeField, Tooltip("Select which type of pipeline shoud be created")]
        private RenderType renderType;
        [SerializeField, Tooltip("If true, the playback will loop")]
        private bool loop;
        [SerializeField, Tooltip("Settings of the cameras that will be played. Either imported from the metadata file or set by hand")]
        private CamConfig[] camConfigs;
        [SerializeField, Tooltip("Reference of the JSON file containg all the metadata needed for the playback of the ithaca file")]
        private TextAsset metadata;
        [SerializeField, Tooltip("The location of the XYTables folder necessary to undistord non fused recordings. In case of a fused export this field can remain empty")]
        private string xyTablePath;
#if USINGAVPRO
        private MediaPlayer player;
        private AVProReceptorRT receptor;
#else
        private VideoPlayer player;
        private UnityVideoPlayerRT receptor;
#endif
        private AppConfig appConfig;
        private PosColMapCombinatorToTexture3DVoxelFusion texture3Dfuser;
        private PosColMapCombinatorForPerspectiveFusion perspFuser;
        private MatrixTransformer[] matrixTransformers;
        private CombinedMulticamUnpacker unpacker;
        private CombinedToVfx[] cToVfxs;
        private VfxGraphRenderer[] renderers;
        private Dictionary<string, AsyncOperationHandle> asyncOperationHandles;
        private const string combinedToVFXComputeAddress = "combinedToPCForVfx-androidgamma";
        private const string posMapToMatPosMapComputeAddress = "PosMapToMatPosMap";
        private const string posMapToMatPosMapComputeAddressAndroid = "PosMapToMatPosMapAndroid";
        private const string weightFromEdgesAddress = "PosMapPointWeightingCompute";
        private const string weightFromEdgesAddressAndroid = "PosMapPointWeightingComputeAndroid";
        private const string Texture3DVoxelFusionComputeComputeAddress = "Texture3DVoxelFusionCompute";
        private const string vfxAssetCollectionAddress = "PointCloudGraphs-FullOptions";
        private const string texture3DAddress = "Texture3D";
        private const string texture3DMaterialAddress = "Texture3DMaterial";
        private const string surfaceMaterialAddress = "SurfaceMat";
        private const string perspDenisfierComputeAddress = "PerspDensifier";
        private const string perspDenisfierComputeAddressAndroid = "PerspDensifierAndroid";
        private const string temporalNoiseRemoverComputeAddress= "TemporalNoiseRemoverCompute";
        private const string perspfusionComputeAddress = "PerspFusion";
        private const string perspfusionComputeAddressAndroid = "PerspFusionAndroid";
        private const string normalizerComputeAddress = "PosColNormalizer";
        private const string normalizerComputeAddressAndroid = "PosColNormalizerAndroid";
        private const string edgesFilterComputeAddress = "PosMapEdgesFilter";
        private const string edgesFilterComputeAddressAndroid = "PosMapEdgesFilterAndroid";
        private const string antiAliasingFilterComputeAddress = "AntiAliasingFilter";
        private int operationsToComplete;
        private int operationsCompleted;

        private bool isOrthoProjected = false;
        private bool isPerspProjected = false;
        private MaterialRenderer[] materialRenderers;

        public bool isPipelineCreated => GetComponent<CombinedMulticamUnpacker>() != null;

#if UNITY_EDITOR
        private void OnValidate()
        {

            var modules = GetComponentsInChildren<ModuleBase>();


            if (useInEditor && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                foreach (var camConfig in camConfigs)
                {
                    var mat = HoloDataManager.getMatrix4x4(camConfig.alignmentMatrixPath);
                    mat.matrix4x4 = camConfig.alignmentMatrix;
                    mat.apply();
                }
                foreach (var module in modules)
                {
#if USINGAVPRO
                    if (module is AVProReceptorRT avProReceptor)
                    {
                        UnityEditor.EditorApplication.update -= avProReceptor.Update;
                        UnityEditor.EditorApplication.update += avProReceptor.Update;
                    }
#else
                    if (module is UnityVideoPlayerRT unityPlayerRT)
                    {
                        UnityEditor.EditorApplication.update -= unityPlayerRT.Update;
                        UnityEditor.EditorApplication.update += unityPlayerRT.Update;
                    }
#endif
                    else if (module is TargetModuleBase<GPUPointCloud> targetModulePC)
                    {
                        targetModulePC.Disconnect();
                        targetModulePC.Connect();
                    }
                    else if (module is TargetModuleBase<RenderTexture> targetModuleRT)
                    {
                        targetModuleRT.Disconnect();
                        targetModuleRT.Connect();
                    }
                }

            }
            else
            {
                foreach (var module in modules)
                {
#if USINGAVPRO

                    if (module is AVProReceptorRT avProReceptor)
                    {
                        UnityEditor.EditorApplication.update -= avProReceptor.Update;
                    }
#else
                    if (module is UnityVideoPlayerRT unityPlayerRT)
                    {
                        UnityEditor.EditorApplication.update -= unityPlayerRT.Update;
                    }
#endif
                    else if (module is TargetModuleBase<GPUPointCloud> targetModulePC)
                    {
                        targetModulePC.Disconnect();
                    }
                    else if (module is TargetModuleBase<RenderTexture> targetModuleRT)
                    {
                        targetModuleRT.Disconnect();
                    }
                }
            }
#if USINGAVPRO

            if (player != null)
            {
                player.Loop = loop;
                player.AutoStart = autoPlay;
            }
#else
            if (player!=null)
            {
                player.isLooping = loop;
                player.playOnAwake = autoPlay;
            }
#endif
            if (!importSettings) return;
            if (metadata != null)
            {
                try
                {
                    var ctxt = JsonUtility.FromJson<ItkContext>(metadata.text);
                    appConfig = ctxt.AppConfig;
                    List<CamConfig> tempconfigs = new();
                    if (ctxt.FusionConfig.exportType != ExportType.Undefined) // fusion case
                    {
                        isOrthoProjected = ctxt.FusionConfig.exportType == ExportType.OrthoProjected;
                        isPerspProjected = ctxt.FusionConfig.exportType == ExportType.PerspectiveProjected;
                        for(int i = 0; i< ctxt.FusionConfig.nbOfCams; ++i)
                        {
                            tempconfigs.Add(ctxt.FusionConfig.virtualCamConfigs[i]);
                        }
                    }
                    else // side-by-side video case
                    {
                        for (int i = 0; i < ctxt.CamCount; ++i)
                        {
                            if (ctxt.AppConfig.ActiveCameras.Contains(ctxt.CamConfigs[i].UserAssignedCameraIndex))
                            {
                                tempconfigs.Add(ctxt.CamConfigs[i]);
                            }
                        }
                    }
                    //camConfigs = ctxt.FusionConfig?.virtualCamConfigs.ToArray() ??
                    //    ctxt.CamConfigs.Where(c => ctxt.AppConfig.ActiveCameras.Contains(c.UserAssignedCameraIndex))
                    //    .ToArray();
                    camConfigs = tempconfigs.ToArray();
                    if (isPerspProjected || isOrthoProjected)
                    {
                        xyTablePath = "No XYTables needed as the export is a projection";
                    }
                    else if (string.IsNullOrEmpty(xyTablePath))
                    {
                        xyTablePath = Path.Combine(ctxt.AppConfig.LocalDataFolder, ctxt.AppConfig.XYTableFolder);
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("Importing metadata failed, make sure that you use a valid ITKContext file.\n" +  ex.Message);
                }
            }
        }
#endif
        private void Awake()
        {
            foreach(var camConfig in camConfigs)
            {
                var mat = HoloDataManager.getMatrix4x4(camConfig.alignmentMatrixPath);
                mat.matrix4x4 = camConfig.alignmentMatrix;
                mat.apply();
            }

            var modules = GetComponentsInParent<ModuleBase>();
            foreach (var module in modules)
            {
#if UNITY_EDITOR
#if USINGAVPRO
                if (module is AVProReceptorRT avProReceptor)
                {
                    UnityEditor.EditorApplication.update -= avProReceptor.Update;
                }
#else
                if (module is UnityVideoPlayerRT unityPlayerRT)
                {
                    UnityEditor.EditorApplication.update -= unityPlayerRT.Update;
                }
#endif
                else if (module is TargetModuleBase<GPUPointCloud> targetModule)

#else
                if (module is TargetModuleBase<GPUPointCloud> targetModule)
#endif
                {
                    targetModule.Disconnect();
                }
                else if (module is TargetModuleBase<RenderTexture> targetModuleRT)
                {
                    targetModuleRT.Disconnect();
                }
            }

        }
        private void LoadAddressablesAsync()
        {
            asyncOperationHandles = new();
            asyncOperationHandles[combinedToVFXComputeAddress] = Addressables.LoadAssetAsync<ComputeShader>(combinedToVFXComputeAddress);
            asyncOperationHandles[posMapToMatPosMapComputeAddress] = Addressables.LoadAssetAsync<ComputeShader>(posMapToMatPosMapComputeAddress);
            asyncOperationHandles[posMapToMatPosMapComputeAddressAndroid] = Addressables.LoadAssetAsync<ComputeShader>(posMapToMatPosMapComputeAddressAndroid);
            asyncOperationHandles[Texture3DVoxelFusionComputeComputeAddress] = Addressables.LoadAssetAsync<ComputeShader>(Texture3DVoxelFusionComputeComputeAddress);
            asyncOperationHandles[perspDenisfierComputeAddress] = Addressables.LoadAssetAsync<ComputeShader>(perspDenisfierComputeAddress);
            asyncOperationHandles[perspDenisfierComputeAddressAndroid] = Addressables.LoadAssetAsync<ComputeShader>(perspDenisfierComputeAddressAndroid);
            asyncOperationHandles[perspfusionComputeAddress] = Addressables.LoadAssetAsync<ComputeShader>(perspfusionComputeAddress);
            asyncOperationHandles[perspfusionComputeAddressAndroid] = Addressables.LoadAssetAsync<ComputeShader>(perspfusionComputeAddressAndroid);
            asyncOperationHandles[weightFromEdgesAddress] = Addressables.LoadAssetAsync<ComputeShader>(weightFromEdgesAddress);
            asyncOperationHandles[weightFromEdgesAddressAndroid] = Addressables.LoadAssetAsync<ComputeShader>(weightFromEdgesAddressAndroid);
            asyncOperationHandles[normalizerComputeAddress] = Addressables.LoadAssetAsync<ComputeShader>(normalizerComputeAddress);
            asyncOperationHandles[normalizerComputeAddressAndroid] = Addressables.LoadAssetAsync<ComputeShader>(normalizerComputeAddressAndroid);
            asyncOperationHandles[edgesFilterComputeAddress] = Addressables.LoadAssetAsync<ComputeShader>(edgesFilterComputeAddress);
            asyncOperationHandles[edgesFilterComputeAddressAndroid] = Addressables.LoadAssetAsync<ComputeShader>(edgesFilterComputeAddressAndroid);
            asyncOperationHandles[temporalNoiseRemoverComputeAddress] = Addressables.LoadAssetAsync<ComputeShader>(temporalNoiseRemoverComputeAddress);
            asyncOperationHandles[antiAliasingFilterComputeAddress] = Addressables.LoadAssetAsync<ComputeShader>(antiAliasingFilterComputeAddress);

            asyncOperationHandles[vfxAssetCollectionAddress] = Addressables.LoadAssetAsync<VfxAssetCollection>(vfxAssetCollectionAddress);
            asyncOperationHandles[texture3DAddress] = Addressables.LoadAssetAsync<RenderTexture>(texture3DAddress);
            asyncOperationHandles[texture3DMaterialAddress] = Addressables.LoadAssetAsync<Material>(texture3DMaterialAddress);
            asyncOperationHandles[surfaceMaterialAddress] = Addressables.LoadAssetAsync<Material>(surfaceMaterialAddress);

            operationsToComplete = asyncOperationHandles.Count;
            operationsCompleted = 0;
            foreach (var operation in asyncOperationHandles.Values)
            {
                operation.Completed += SetBool;
            }

        }

        private void SetBool(AsyncOperationHandle handle)
        {
            if(handle.Status == AsyncOperationStatus.Succeeded)
            {
                operationsCompleted++;
            }
            else
            {
                Debug.LogException(handle.OperationException);
            }

            if(operationsCompleted == operationsToComplete) 
            {
                CreatePipeline();
            }
        }
        
        private void CreatePipeline()
        {
#if USINGAVPRO
            player = gameObject.AddComponent<MediaPlayer>();
            player.Loop = loop;
            player.OpenMedia(mediaReference, autoPlay);
            receptor = gameObject.AddComponent<AVProReceptorRT>();
#else
            player = gameObject.AddComponent<VideoPlayer>();
            player.isLooping = true;
            player.source = VideoSource.VideoClip;
            player.clip = clip;
            player.playOnAwake = true;
            receptor = gameObject.AddComponent<UnityVideoPlayerRT>();
#endif
            //player.hideFlags = HideFlags.HideInInspector;
            //receptor.hideFlags = HideFlags.HideInInspector;

            unpacker = gameObject.AddComponent<CombinedMulticamUnpacker>();
            //unpacker.hideFlags = HideFlags.HideInInspector;
            unpacker.Source = receptor;
            if(renderType == RenderType.PerspectiveFusion || renderType == RenderType.PerspectiveFusionAndroid)
            {
                var projectionScreen = GameObject.CreatePrimitive(PrimitiveType.Cube);
                projectionScreen.name = "ProjectionScreen";
                DestroyImmediate(projectionScreen.GetComponent<MeshRenderer>());
                DestroyImmediate(projectionScreen.GetComponent<BoxCollider>());
                projectionScreen.transform.parent = Camera.main.transform;
                projectionScreen.transform.localScale = new Vector3(0.15f, 0.15f, 0);
                projectionScreen.transform.localPosition = new Vector3(0, 0, 0.1f);
            }

            unpacker.numberOfCams = camConfigs.Length;
            unpacker.guessMonoCamResolution = false;
            unpacker.outResolution = camConfigs[0].layout.depth.res;
            unpacker.layouts = camConfigs.Select(c => c.layout).ToArray();

            unpacker.AdjustMultiSources();
            //var renderer = gameObject.AddComponent<RenderTextureRenderer>();
            //renderer.source = unpacker.MOSlist[0];
            cToVfxs = new CombinedToVfx[camConfigs.Length];
            switch (renderType)

            {
                case RenderType.RayMarching:
                    texture3Dfuser = gameObject.AddComponent<PosColMapCombinatorToTexture3DVoxelFusion>();
                    //fuser.hideFlags = HideFlags.HideInInspector;
                    texture3Dfuser.posColMapSources = new Component[camConfigs.Length];
                    texture3Dfuser.OrthoFusionCompute = asyncOperationHandles[Texture3DVoxelFusionComputeComputeAddress].Result as ComputeShader;
                    texture3Dfuser.VoxelsOut = asyncOperationHandles[texture3DAddress].Result as RenderTexture;
                    texture3Dfuser.Box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    texture3Dfuser.Box.name = "Voxelization space";
                    texture3Dfuser.Box.transform.localScale = Vector3.one * (camConfigs[0].projectonScreenHeight != 0 ? camConfigs[0].projectonScreenHeight : 2);
                    //fuser.Box.hideFlags = HideFlags.HideInHierarchy;
                    texture3Dfuser.Box.transform.position = Vector3.forward * 0.57f;
                    texture3Dfuser.Box.GetComponent<MeshRenderer>().enabled = false;
                    texture3Dfuser.finalDepth = texture3Dfuser.finalWidth = texture3Dfuser.finalHeight = 512;
                    texture3Dfuser.DisplayCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    texture3Dfuser.DisplayCube.name = "Display Cube";
                    texture3Dfuser.DisplayCube.GetComponent<MeshRenderer>().material = asyncOperationHandles[texture3DMaterialAddress].Result as Material;
                    texture3Dfuser.DisplayCube.transform.position += new Vector3(0.01f, 0.01f, 0.01f);
                    //fuser.DisplayCube.hideFlags = HideFlags.HideInHierarchy;

                    matrixTransformers = new MatrixTransformer[camConfigs.Length];
                    break;
                case RenderType.PerspectiveFusion:
                case RenderType.PerspectiveFusionAndroid:
                    perspFuser = gameObject.AddComponent<PosColMapCombinatorForPerspectiveFusion_android>();
                    perspFuser.posColMapSources = new Component[camConfigs.Length];
                    perspFuser.PerspFusionCompute = asyncOperationHandles[perspfusionComputeAddressAndroid].Result as ComputeShader;
                    perspFuser.finalWidth = renderType == RenderType.PerspectiveFusion ? 1024 : 512;
                    perspFuser.finalHeight = renderType == RenderType.PerspectiveFusion ? 1024 : 512;
                    perspFuser.finalDepth = renderType == RenderType.PerspectiveFusion ? 1024 : 512;
                    perspFuser.fusion = true;
                    perspFuser.depthThreshold_Z_meter = 0.03f;
                    perspFuser.fusionDisplay = new GameObject("PerspectiveFusionDisplay");
                    var renderer = perspFuser.fusionDisplay.AddComponent<MaterialRenderer>();
                    renderer.renderingMaterial = asyncOperationHandles[surfaceMaterialAddress].Result as Material;
                    renderer.inverseNormals = true;
                    perspFuser.cam = Camera.main;
                    perspFuser.Box = Camera.main.GetComponentInChildren<MeshFilter>().gameObject;
                    renderers = new VfxGraphRenderer[camConfigs.Length];
                    matrixTransformers = new MatrixTransformer[camConfigs.Length];
                    var normalizer = renderType == RenderType.PerspectiveFusion ? gameObject.AddComponent<PosColNormalizer>() : gameObject.AddComponent<PosColNormalizer_android>();
                    normalizer.source = perspFuser;
                    normalizer.filter = asyncOperationHandles[renderType == RenderType.PerspectiveFusion ? normalizerComputeAddress : normalizerComputeAddressAndroid].Result as ComputeShader;
                    var antiAliasingFilter = gameObject.AddComponent<AntiAliasing>();
                    antiAliasingFilter.filter = asyncOperationHandles[antiAliasingFilterComputeAddress].Result as ComputeShader;
                    antiAliasingFilter.source = normalizer;
                    antiAliasingFilter.depthThreshold = 0.5f;
                    antiAliasingFilter.thickness_pixel = 2;
                    antiAliasingFilter.blurDensity = 5;
                    var edgesFilter = renderType == RenderType.PerspectiveFusion ? gameObject.AddComponent<EdgesFilter>() : gameObject.AddComponent<EdgesFilter_android>();
                    edgesFilter.source = antiAliasingFilter;
                    edgesFilter.filter = asyncOperationHandles[renderType == RenderType.PerspectiveFusion ? edgesFilterComputeAddress : edgesFilterComputeAddressAndroid].Result as ComputeShader;
                    edgesFilter.topThickness = 1;
                    edgesFilter.bottomThickness = 1;
                    edgesFilter.leftThickness = 1;
                    edgesFilter.rightThickness = 1;
                    edgesFilter.depthThreshold_mm = 0.8f;
                    renderer.source = edgesFilter;

                    break;
                case RenderType.pointCloud:
                    renderers = new VfxGraphRenderer[camConfigs.Length];
                    break;
                case RenderType.Mesh:
                    case RenderType.MeshAndroid:
                    materialRenderers = new MaterialRenderer[camConfigs.Length];
                    break;
            }

            for (int i = 0; i < cToVfxs.Length; i++)
            {
                var go = new GameObject("Pipeline (" + camConfigs[i].UserAssignedCameraIndex + ")");
                go.transform.localPosition = transform.position;
                go.transform.rotation = transform.rotation;
                go.transform.parent = transform;
                //unpacker.MOSlist[i].hideFlags = HideFlags.HideInInspector;
                cToVfxs[i] = go.AddComponent<CombinedToVfx>();
                cToVfxs[i].settingsByContext = false;
                cToVfxs[i].unityScale = true;
                cToVfxs[i].perspProjected = isPerspProjected;
                cToVfxs[i].orthoProjected = isOrthoProjected;
                cToVfxs[i].orthoProjectionCubeScale = camConfigs[i].projectonScreenHeight;
                cToVfxs[i].projectionScreenHeight = camConfigs[i].projectonScreenHeight;
                cToVfxs[i].projectionScreenWidth = camConfigs[i].projectonScreenWidth;
                cToVfxs[i].screenCameraDistance = camConfigs[i].screenCameraDistance;
                cToVfxs[i].xytFolder = xyTablePath;
                cToVfxs[i].myKinectSerial = camConfigs[i].SerialNumber;

                //cToVfxs[i].hideFlags = HideFlags.HideInInspector;
                cToVfxs[i].Source = unpacker.MOSlist[i];
                cToVfxs[i].combinedToPCUnpacker = asyncOperationHandles[combinedToVFXComputeAddress].Result as ComputeShader;
                cToVfxs[i].depthInColorEncoding = camConfigs[i].DepthInColorEncoding;
                EdgesPointWeighting edgesWeighting = null;
                if (renderType == RenderType.PerspectiveFusion || renderType == RenderType.PerspectiveFusionAndroid)
                {
                    edgesWeighting = renderType == RenderType.PerspectiveFusion ?  go.AddComponent<EdgesPointWeighting>() : go.AddComponent<EdgesPointWeighting_android>();
                    edgesWeighting.source = cToVfxs[i];
                    edgesWeighting.filter = asyncOperationHandles[renderType == RenderType.PerspectiveFusion ? weightFromEdgesAddress : weightFromEdgesAddressAndroid].Result as ComputeShader;
                    edgesWeighting.depthThreshold_mm = .5f;
                    edgesWeighting.WeightingTopThickness = 10;
                    edgesWeighting.WeightingBottomThickness = 10;
                    edgesWeighting.WeightingLeftThickness = 10;
                    edgesWeighting.WeightingRightThickness = 10;
                }
                if (renderType == RenderType.RayMarching || renderType == RenderType.PerspectiveFusion || renderType == RenderType.PerspectiveFusionAndroid)
                {
                    matrixTransformers[i] = (renderType == RenderType.PerspectiveFusion || renderType == RenderType.RayMarching) ? go.AddComponent<MatrixTransformer>() : go.AddComponent<MatrixTransformer_android>();
                    if (renderType == RenderType.RayMarching)
                    {
                        matrixTransformers[i].source = cToVfxs[i];
                    }
                    else
                    {
                        matrixTransformers[i].source = edgesWeighting;
                    }
                    matrixTransformers[i].camAlignMatrix = camConfigs[i].alignmentMatrixPath;
                    HoloMatrix4x4 alignMatrix = HoloDataManager.getMatrix4x4(HoloMatrix4x4.GetFullPathFromDecomposedPath(matrixTransformers[i].dataPath, matrixTransformers[i].seqName, matrixTransformers[i].camAlignMatrix));
                    if (camConfigs[i].alignmentMatrix != Matrix4x4.zero) { alignMatrix.matrix4x4 = camConfigs[i].alignmentMatrix; alignMatrix.apply(); }
                    matrixTransformers[i].PosMapToMatPosMap = asyncOperationHandles[renderType.ToString().Contains("Android") ? posMapToMatPosMapComputeAddressAndroid : posMapToMatPosMapComputeAddress].Result as ComputeShader;
                    if (renderType == RenderType.RayMarching)
                    {
                        texture3Dfuser.posColMapSources[i] = matrixTransformers[i];
                    }
                    else
                    {
                        var densifier = renderType == RenderType.PerspectiveFusion ? go.AddComponent<PosColMapCombinatorForPerspectiveFusion>() : go.AddComponent<PosColMapCombinatorForPerspectiveFusion_android>();
                        densifier.posColMapSources = new Component[1] { matrixTransformers[i] };
                        densifier.PerspFusionCompute = asyncOperationHandles[renderType == RenderType.PerspectiveFusion ? perspDenisfierComputeAddress : perspDenisfierComputeAddressAndroid].Result as ComputeShader;
                        densifier.finalWidth = renderType == RenderType.PerspectiveFusion ? 1024 : 512;
                        densifier.finalHeight = renderType == RenderType.PerspectiveFusion ? 1024 : 512;
                        densifier.finalDepth = renderType == RenderType.PerspectiveFusion ? 1024 : 512;
                        densifier.cam = Camera.main;
                        densifier.Box = Camera.main.GetComponentInChildren<MeshFilter>().gameObject;
                        densifier.TaaActivated = renderType == RenderType.PerspectiveFusion ? true : false;
                        densifier.TaaDelay = 2;
                        densifier.TaaDepthThreshold = .5f;
                        densifier.TaaThickness = 2;
                        densifier.motionThreshold = 1;
                        densifier.TemporalNoiseRemoverCompute = asyncOperationHandles[temporalNoiseRemoverComputeAddress].Result as ComputeShader;
                        perspFuser.posColMapSources[i] = densifier;
                    }
                }
                else if(renderType == RenderType.pointCloud)
                {
                    renderers[i] = go.AddComponent<VfxGraphRenderer>();
                    renderers[i].Source = cToVfxs[i];
                    renderers[i].settingsByContext = false;
                    renderers[i].vfxAssetCollection = asyncOperationHandles[vfxAssetCollectionAddress].Result as VfxAssetCollection;
                    renderers[i].pointSize = 0.003f;
                    renderers[i].camAlignMatrix = camConfigs[i].alignmentMatrixPath;
                    HoloMatrix4x4 alignMatrix = HoloDataManager.getMatrix4x4(HoloMatrix4x4.GetFullPathFromDecomposedPath(renderers[i].dataPath, renderers[i].seqName, renderers[i].camAlignMatrix));
                    if (camConfigs[i].alignmentMatrix != Matrix4x4.zero) { alignMatrix.matrix4x4 = camConfigs[i].alignmentMatrix; alignMatrix.apply(); }
                }
                else
                {
                    var matTrans = renderType == RenderType.Mesh ? go.AddComponent<MatrixTransformer>() : go.AddComponent<MatrixTransformer_android>();
                    matTrans.PosMapToMatPosMap = asyncOperationHandles[renderType.ToString().Contains("Android") ? posMapToMatPosMapComputeAddressAndroid : posMapToMatPosMapComputeAddress].Result as ComputeShader;
                    matTrans.camAlignMatrix = camConfigs[i].alignmentMatrixPath;
                    materialRenderers[i] = go.AddComponent<MaterialRenderer>();
                    materialRenderers[i].Source = matTrans;
                    materialRenderers[i].settingsByContext = false;
                    materialRenderers[i].renderingMaterial = asyncOperationHandles[surfaceMaterialAddress].Result as Material;
                    materialRenderers[i].inverseNormals = true;
                    HoloMatrix4x4 alignMatrix = HoloDataManager.getMatrix4x4(HoloMatrix4x4.GetFullPathFromDecomposedPath(materialRenderers[i].dataPath, materialRenderers[i].seqName, materialRenderers[i].camAlignMatrix));
                    if (camConfigs[i].alignmentMatrix != Matrix4x4.zero) { alignMatrix.matrix4x4 = camConfigs[i].alignmentMatrix; alignMatrix.apply(); }
                }
                    //go.hideFlags = HideFlags.HideInHierarchy;
            }
        }

        public void ConstructPipeline()
        {
            LoadAddressablesAsync();
        }
    }
}