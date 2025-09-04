/*!------ MatrixTransformer ---- (candidate for a new name : PointCloudTransformer)-------------
* @brief 
*   BRIDGE: GpuPointCloud --> GpuPointCloud
*
*-----------------------------------------------------------------------------------------------
* @copyright (c) 2024 French Touch Factory. All rights reserved.
* @project IthacaLib by French Touch Factory
* @resp Francois
* @author 
*       Francois Bouille (f.bouille@frenchtouchfactory.com) - French Touch Factory
* @date Starting date : january 2024
* @details 
*   Transforms XYZ positions of a PosMap RenderTexture from a Point Cloud by the alignment matrix.
*   New coordinates overwrite the old one in the same RenderTexture.
*   The result can display the registrated point cloud or can be used to prepare for fusion.
* @param
* @note
* @test 
*---------------------------------------------------------------------------------------------*/

using IthacaLib.Common;
using System;
using UnityEngine;
using Holo;
using Debug = UnityEngine.Debug;


namespace IthacaLib
{
    /// <summary>
    /// Transforms XYZ positions of a PosMap RenderTexture from a Point Cloud by the alignment matrix.
    /// New coordinates overwrite the old one in the same RenderTexture.
    /// The result can display the registrated point cloud or can be used to prepare for fusion.
    /// </summary>
    public class MatrixTransformer : BridgeModuleBase<GPUPointCloud, GPUPointCloud>
    {
        public override event EventHandler<DataEventArgs<GPUPointCloud>> OutputReady;

        [Tooltip("PosMapToMatPosMap is a good choice for this computeShader")]
        public ComputeShader PosMapToMatPosMap;
        public bool isFilterEnabled = true;

        protected VideoResolution containerRes;  //Replaces old globalWidth & globalHeight

        #region Matrix management
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
            "the path of the matrix. Example: align\\cams\\alignmatrixD_3.json (extension can be forgotten, it is added internaly).")]
        public string camAlignMatrix;

        public Matrix4x4 alignmentMatrix = Matrix4x4.identity;

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

        protected Matrix4x4 SetAlignment()
        {
            string path = "Not found";
            HoloMatrix4x4 mat; 
            try
            {
                Debug.Log("MATRIXTRANSF- AVANT GetFullPath " + dataPath + seqName + camAlignMatrix);
                path = HoloMatrix4x4.GetFullPathFromDecomposedPath(dataPath, seqName, camAlignMatrix);
                Debug.Log("Path matrix : " + path);
                //Debug.Log("File read:" + System.IO.File.ReadAllText(path));
                mat = HoloDataManager.getMatrix4x4(path);

            }
            catch (System.Exception e)
            {
                mat = new HoloMatrix4x4();
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

            return finalCommonMatrix * mat.matrix4x4;
        }
        #endregion

        //Compute shader parameters
        protected int threadGroupsX;
        protected int threadGroupsY;
        protected int threadGroupsZ = 1;
        protected int kernelIndex;


        protected class ID
        {
            public static int PosMapIn = Shader.PropertyToID("PosMapIn");

            public static int TransformMat = Shader.PropertyToID("TransformMat");

        }

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

        //TODO: Will have to be done in a cleaner way... independant of Unity, but easy to use with Unity.
        /// <summary>
        /// Unity dependant way of getting in a simple way the cam index (by adding (n) in the name of the gameObject, n being the cam user assigned index).
        /// </summary>
        string GetMyCamIndex()
        {
            var indexOfOpen = this.gameObject.name.LastIndexOf("(");
            var indexOfClose = this.gameObject.name.LastIndexOf(")");
            string number = "";
            if (indexOfOpen >= 0 && indexOfClose >= 0)
                number = this.gameObject.name.Substring(indexOfOpen + 1, indexOfClose - indexOfOpen - 1);

            return int.TryParse(number, out var index) ? index.ToString() : "NO_CAM_INDEX_FOUND";
        }
        #endregion


        public override void HotInit(DataEventArgs<GPUPointCloud> e)
        {
            GPUPointCloud initPC = e.Output;
            containerRes.width = initPC.positionMap.width;
            containerRes.height = initPC.positionMap.height;

            if(alignmentMatrix == Matrix4x4.identity)
            {
                alignmentMatrix = SetAlignment();
            }
            Debug.Log("Matrix : " + alignmentMatrix);

            //Prepare  ComputeShader
            PrepareComputeShader();

            base.HotInit(e);
        }


        public override void ProcessData(object sender, DataEventArgs<GPUPointCloud> e)
        {
            if (!enabled) return;

            if (!IsHotInitComplete) HotInit(e);


            if (isFilterEnabled)
            {
                PosMapToMatPosMap.SetTexture(kernelIndex, ID.PosMapIn, e.Output.positionMap);

                PosMapToMatPosMap.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);
            }
            OutputReady?.Invoke(this, e);

        }


        private void PrepareComputeShader()
        {
            PosMapToMatPosMap = Instantiate(PosMapToMatPosMap); //If we don't instantiate, the same CS is used by several modules. Int?ressant
            threadGroupsX = Mathf.CeilToInt(containerRes.width / 8);
            threadGroupsY = Mathf.CeilToInt(containerRes.height / 8);

            kernelIndex = PosMapToMatPosMap.FindKernel("CSMain");

            PosMapToMatPosMap.SetMatrix(ID.TransformMat, alignmentMatrix); //A mettre dans Process Data le jour o? nos matrices sont interpol?es spatialement
        }

        public override void GetSettingsByContext()
        {
            FindContextInstance();
            if (ContextManager.instance == null) return;
            var ctxt = ContextManager.instance.currentContext;

            if (ctxt.AppConfig.LocalDataFolder != "")
                dataPath = ctxt.AppConfig.LocalDataFolder;
            if (ctxt.AppConfig.Sequence != "")
                seqName = ctxt.AppConfig.Sequence;
            if (ctxt.AppConfig.AlignBaseName != "")
                camAlignMatrix = ctxt.AppConfig.AlignFolder + ctxt.AppConfig.AlignBaseName + Helper.GetCamIndexString(this.gameObject) + ".json";
        }


    }
}
