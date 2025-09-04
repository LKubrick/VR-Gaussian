/*#############################################################################################
#           ----   VfxGraphRenderer   ----
# TARGET: (RenderTexture(PosMap), RenderTexture(Color)) --> Rendering with VFX graph
#
# ---------------------------------------------------------------------------------------------
# Project: IthacaLib
# Resp. : Quentin
# Author(s): 
#    - Phil Hoffmann (phil.hoffmann@ipk.fraunhofer.de) (67%)
#    - Quentin de Cagny (quentindecagny@gmail.com) - French Touch Factory (33%)
#   Note: apparently, according to the agreement, we need to find a percentage breakdown for 
#   each co-developed module. I'm proposing these numbers, just to start doing it, but it's 
#   open to discussion.
# Date : created in october 2023
# Description: Renders a point cloud using the visual effect graph.
# Parameters : Two RenderTextures, first contains the positions encoded in a ARGBFloat texture
#              and the second contains the colors in ARGB32 format.
#              See the CaptureToVfxConverter bridge for more details.
# Note : - 
# Tests : -
#############################################################################################*/

using UnityEngine;
using Holo;
using System.Collections.Generic;


#if USINGVFXGRAPH
using UnityEngine.VFX;
using UnityEngine.VFX.Utility;
using IthacaLib.Common;

namespace IthacaLib
{
    public class VfxGraphRendererWithOverride : TargetModuleBase<GPUPointCloud>
    {
        public VfxAssetCollection vfxAssetCollection = null;
        [Tooltip("Set this field to the VFX graph you want to use for rendering, otherwise the best matching vfx graph from the asset collection will be used")]
        public VisualEffectAsset vfxAssetOverride = null;
        [Tooltip("Size of each point of the point cloud rendered by the VFX Graph.")]
        public float pointSize = 10f;


        [Header(" - Alignment Matrix (.json file path) - ")]
        [Space(-2)]
        [Tooltip("Full local path of the Data directory, where the sequences are stored. " +
            "Example: D:\\Projects\\HoloUnity\\Data\\")]
        public string dataPath;

        [Space(-3)]
        [Tooltip("Name of the sequence. Will be used to compose the path of the alignment matrix. " +
            "Example: marcelmire720r .")]
        public string seqName;

        [Space(-3)]
        [Tooltip("Filename of the matrix with its path inside sequence directory structure. Will be used to compose " +
            "the path of the matrix. Example: align\\cams\\alignmatrixD_3.json (extension can be forgotten, it is added internaly)." +
            " - NO_CAM_INDEX_FOUND ? : add the user index of the cam to the gameObject's name.")]
        public string camAlignMatrix;

        private Matrix4x4 alignmentMatrix = Matrix4x4.identity;

        [Header(" - Common Matrix + coord system transformers - ")]
        [Space(-2)]
        [Tooltip("The alignment matrix is modified by this common Matrix. To be used for a global transformation of the subject.")]
        public Matrix4x4 commonMatrix = Matrix4x4.identity;

        //For future version
        //[Tooltip("Selector between Depth or Color sensor for the sensor on which the alignment matrix where calculated. Usually, the D or C at the end of the json matrix name is an indication about that.")]
        //public bool colorAlign = false;
        [Tooltip("Mirror on Y axis. (for Unity, prefer true)")]
        public bool mirrorY = false;
        [Tooltip("Scale from millimeter (Kinect world) to meter (Unity world), thus scales everything by 0.001. " +
            "Unity also needs mirroring on Y axis compared to Kinect, so set mirrorY to true above if you want the " +
            "real truth.")]
        public bool meterScale = false;
        [Tooltip("(DEPRECATED) Replaces mirrorY + meterScale using a constant .json matrix4x4. ")]
        public bool KinectToUnity = false;
        [Tooltip("(DEPRECATED) Inverse of KinectToUnity, using a constant .json matrix4x4. ")]
        public bool UnityToKinect = false;

        private VisualEffect vfxComponent = null;
        private PointCloudPositionAndColorBinder binderPC = null;
        private bool bindAlign = false; //Active flag for this optional property
        private PointCloudAlignMatrixBinder binderAlign = null;

        //DEBUG TOOLS
        [Space(10)]
        [Header("Debug tools")]
        [Tooltip("Useful debug option that transfer the alignment to the transform of this gameObject,"
            + "thus matching the (0,0,0) position of this GameObject to the camera used during recording.")]
        public bool directTransform = false;

        #region Unity framework logic
        protected override void Start()
        {
            if (settingsByContext)
                GetSettingsByContext();

            base.Start();
        }

        protected override void OnValidate()
        {
            if (settingsByContext)
                GetSettingsByContext();

            base.OnValidate();
        }
        #endregion

        //TODO: mutualize this function with MaterialRenderer (for the shaderGraph mesh rendering)
        // Or maybe make an interface IContextSetable (bad name)
        public override void GetSettingsByContext()
        {
            FindContextInstance();
            var ctxt = ContextManager.instance.currentContext;

            if (ctxt.AppConfig.LocalDataFolder != "")
                dataPath = ctxt.AppConfig.LocalDataFolder;
            if (ctxt.AppConfig.Sequence != "")
                seqName = ctxt.AppConfig.Sequence;
            if (ctxt.AppConfig.AlignBaseName != "")
                camAlignMatrix = ctxt.AppConfig.AlignFolder + ctxt.AppConfig.AlignBaseName + Helper.GetCamIndexString(this.gameObject) + ".json";
            if (ctxt.AppConfig.CommonTransformMatrix != Matrix4x4.zero)
                commonMatrix = ctxt.AppConfig.CommonTransformMatrix;
        }

        public override void PropagateToContext()
        {
            var ctxt = ContextManager.instance.currentContext;

            if (dataPath != "")
                ctxt.AppConfig.LocalDataFolder = dataPath;
            if (seqName != "")
                ctxt.AppConfig.Sequence = seqName;
            if (camAlignMatrix != "")
            {
                int lastIndex1 = camAlignMatrix.LastIndexOf('/');
                int lastIndex2 = camAlignMatrix.LastIndexOf('\\');
                ctxt.AppConfig.AlignFolder = camAlignMatrix.Substring(0, Mathf.Max(lastIndex1,lastIndex2));
                ctxt.AppConfig.AlignBaseName = camAlignMatrix.Substring(Mathf.Max(lastIndex1, lastIndex2))[..^(Helper.GetCamIndexString(this.gameObject) + ".json").Length] ;
            }
            if (ctxt.AppConfig.CommonTransformMatrix != Matrix4x4.zero)
                ctxt.AppConfig.CommonTransformMatrix = commonMatrix;
        }

        public override void HotInit(DataEventArgs<GPUPointCloud> e)
        {
            GPUPointCloud initPC = e.Output;
            if (initPC.positionMap == null || initPC.colorMap == null) return;

            alignmentMatrix = SetAlignment();

            ////DEBUG "directTransform"
            //if (directTransform)
            //{
            //    transform.localPosition = alignmentMatrix.GetPosition();
            //    transform.localRotation = alignmentMatrix.rotation;
            //    transform.localScale = alignmentMatrix.lossyScale;
            //    alignmentMatrix = Matrix4x4.identity;
            //}
            //else
            //{
            //    transform.localPosition = Vector3.zero;
            //    transform.localRotation = Quaternion.identity;
            //    transform.localScale = Vector3.one;
            //}
            //// END OF DEBUG "directTransform"


            if (vfxComponent == null)
                vfxComponent = gameObject.AddComponent<VisualEffect>();

            if (vfxAssetOverride != null)
            {
                vfxComponent.visualEffectAsset = vfxAssetOverride;
            }
            else
            {
                //Get the right VfxGraph in the collection
                vfxComponent.visualEffectAsset = vfxAssetCollection.GetBestGraph(initPC.positionMap.width * initPC.positionMap.height);
            }
            //Find which properties are exposed and bind them (PC is mandatory exposed, others are optional)
            BindExposedProperties(vfxComponent.visualEffectAsset, initPC);
            base.HotInit(e);
        }

        public override void ProcessData(object sender, DataEventArgs<GPUPointCloud> e)
        {
            if (!enabled || e == null) return;

            if (!IsHotInitComplete)
                HotInit(e);
            //if (vfxComponent != null)

            if (IsHotInitComplete)
            {
                binderPC.positions = e.Output.positionMap; 
                binderPC.colors = e.Output.colorMap;
                binderPC.UpdateBinding(vfxComponent); //FB : Condition added because the component can be null and the vfxGraphRenderer keeps on failing (vfxComponent always null)
            }
        }

        //TODO: this code is the same as in MaterialRenderer, a mutualisation has to be done
        private Matrix4x4 SetAlignment()
        {
            string path = "Not found";
            HoloMatrix4x4 mat = new HoloMatrix4x4();
            try
            {
                path = HoloMatrix4x4.GetFullPathFromDecomposedPath(dataPath, seqName, camAlignMatrix);
                mat = HoloDataManager.getMatrix4x4(path);
            }
            catch (System.Exception e)
            {
                camAlignMatrix = "NOT FOUND. Identity instead.";
                Debug.LogWarning("VfxGraphRenderer: SetAlignment: generic exception, probably file does not exists, " +
                    "Identity has been used instead. Message= " + e.Message);
            }

            //Common transformation
            Matrix4x4 finalCommonMatrix = commonMatrix;
            if (meterScale)
            {
                Matrix4x4 scaleUnityMatrix = Matrix4x4.Scale(Vector3.one * 0.001f);
                finalCommonMatrix = scaleUnityMatrix * finalCommonMatrix;
            }
            finalCommonMatrix.m11 = mirrorY ? -finalCommonMatrix.m11 : finalCommonMatrix.m11;

            //DEPRECATED: KinectToUnity and UnityToKinect are redondant with meterScale+mirrorY but are here for
            //example of correct matrix multiplication. Will disappear soon.
            if (KinectToUnity)
            {
                HoloMatrix4x4 K2U = new HoloMatrix4x4();
                K2U.ReadFromDisk("C:\\Users\\franc\\Desktop\\Ithaca\\Git\\IthacaEmetteurRecepteur\\Assets\\IthacaLib\\Common\\Constants\\Matrices\\Kinect2Unity.json");
                finalCommonMatrix = K2U.matrix4x4 * finalCommonMatrix;
            }
            if (UnityToKinect)
            {
                HoloMatrix4x4 U2K = new HoloMatrix4x4();
                U2K.ReadFromDisk("C:\\Users\\franc\\Desktop\\Ithaca\\Git\\IthacaEmetteurRecepteur\\Assets\\IthacaLib\\Common\\Constants\\Matrices\\Unity2Kinect.json");
                finalCommonMatrix = U2K.matrix4x4 * finalCommonMatrix;
            }

            return finalCommonMatrix * mat.matrix4x4;
        }
        public override void CleanUp()
        {
            base.CleanUp();
            Destroy(GetComponent<PointCloudPositionAndColorBinder>());
            Destroy(GetComponent<PointCloudAlignMatrixBinder>());
            Destroy(GetComponent<VFXPropertyBinder>());
            Destroy(GetComponent<VisualEffect>());
        }
        void BindExposedProperties(VisualEffectAsset vfxa, GPUPointCloud initPC)
        {
            var exposedProperties = new List<VFXExposedProperty>();
            vfxa.GetExposedProperties(exposedProperties);
            foreach (var property in exposedProperties)
            {
                if (property.name == "alignMat" && property.type == typeof(Matrix4x4))
                    bindAlign = true;
            }

            if (binderPC == null)
            {
                binderPC = gameObject.AddComponent<PointCloudPositionAndColorBinder>();
                binderPC.positionsProperty = "positions";
                binderPC.colorProperty = "colors";
                binderPC.pointSizeProperty = "pointSize";
            }
            if (bindAlign && binderAlign == null)
            {
                binderAlign = gameObject.AddComponent<PointCloudAlignMatrixBinder>();
                binderAlign.alignMatProperty = "alignMat";
            }

            binderPC.positions = initPC.positionMap;  //Note: as the RTs sent by the events are always the sames, it works 
            binderPC.colors = initPC.colorMap;     // perfectly fine to bind only once at (hot)initialization.
            binderPC.pointSize = pointSize;

            if (bindAlign)
            {
                binderAlign.alignMat = alignmentMatrix;
                binderAlign.UpdateBinding(vfxComponent);
            }
        }
    }
}

#else
namespace IthacaLib
{
    public class VfxGraphRendererWithOverride : MonoBehaviour
    {
        [TextArea(2, 2)]
        [SerializeField]
        string ________moduleInfos = "Visual Effect Graph is NOT AVAILABLE on this environment,\r\n" +
            " please install it and define #USINGVFXGRAPH symbol. ";
    }
}
#endif