/*!----------------- CombinedToVfx -------------------------------------------------------------
* @brief 
*     BRIDGE: RenderTexture(combined) --> GpuPointCloud
*
*-----------------------------------------------------------------------------------------------
* @copyright (c) 2024 French Touch Factory. All rights reserved.
* @project IthacaLib by French Touch Factory
* @resp Quentin
* @author 
*   Quentin de Cagny (quentindecagny@gmail.com ), François Bouille (f.bouille@frenchtouchfactory.com) - French Touch Factory
* @date created in october 2023
* @date last modified july the 22nd 2024
* @details 
*   Takes a RenderTexture of monocam combined layout (with color-encoded depth and color 
*    side by side) and converts it into two RenderTextures (positions and colors) in a 
*    GpuPointCloud, using an XYTable and a DepthInColorDecoder (found in Ithacodex), which 
*    can be used to render point cloud using the Visual Effect Graph, for example.
*    FB added a gamma correction system: if the incoming RT is sRGB a 1/2.2 pow is applied in the compute shader, if not no pow is applied
*    Problem: now we have a common format for Point Cloud in graphic memory, we lack something 
*       to convert our combined Color+depth format to this GPU friendly representation.
*    Hypothesis: directly convert pixels of a combined frame into the structure we need for 
*       Point Cloud rendering
*    Description: cf. \@details
*    Limits: still needs an XYTable, handles only a few resolutions
*    Further Improvements: slighter solution than XYTable, integrate IR stream possibility,
*       handling of every possible resolutions
* @param[in] XYTable, RenderTexture(combined)
* @note
* @test
*---------------------------------------------------------------------------------------------*/


#if USINGAGORA 
using Agora.Rtc;
#endif
using IthacaLib.Common;
using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;


namespace IthacaLib
{
    /// <summary>
    ///  Takes a RenderTexture of monocam combined layout (with color-encoded depth and color 
    /// side by side) and converts it into two RenderTextures(positions and colors) in a
    /// GpuPointCloud, which can be used to render it using the Visual Effect Graph, for example.
    /// </summary>
    public class CombinedToVfx : BridgeModuleBase<RenderTexture, GPUPointCloud>, IInjectable, IBufferCreator
    {
        public override event EventHandler<DataEventArgs<GPUPointCloud>> OutputReady;

        [Tooltip("combinedToPCForVfx is a good choice for this computeShader.")]
        public ComputeShader combinedToPCUnpacker;
        private VideoResolution containerRes;


        public bool unityScale;

        [Tooltip("Method and parameters used for DECODING colors to depth values. See Ithacodex.")]
        public DepthInColorEncoding depthInColorEncoding;

        public bool orthoProjected;
        public bool perspProjected;
        public float projectionScreenWidth;
        public float projectionScreenHeight;
        public float screenCameraDistance;
        public float orthoProjectionCubeScale;
        [Tooltip("Parameter used for encoding depth into colors with Encode255 or Encode255Plus method. WIP. See Ithacodex.")]
        int encodeShift;  //Now private because the param is taken from DepthInColorEncoding.Parameters
        [Tooltip("KEEP THIS TO FALSE unless you know what you're doing. This will flip from TOP DOWN to BOTTOM UP memory " +
            "address convention the XYTable parser. We just keep this parameter for backward compatibility because unmatching " +
            "conventions between Microsoft and Unity were not taken into account in the past (before 27/06/2024).")]
        public bool noFlipY = false;

        [Header("   - XYTable -")]
        [Space(-1)]
        [Tooltip("Path of the folder containing the XYTable JSON files.")]
        public string xytFolder = null;
        [Space(-3)]
        [Tooltip("Serial number of the camera used for this recording. Will be used, " +
            "along with the resolution, to find the right XYTable file to read. " +
            "Exple: 000237220312 will generate this kind of filename : 000237220312_320x288.xyt")]
        public string myKinectSerial = null;
        bool XYTableParsing = false;
        public ReadOnlySpan<float> XYTableParser
                => _XYtableParser != null ? _XYtableParser.Data : null;


        XYTableParser _XYtableParser;
        ComputeBuffer _xyTable;

        //Out of ComputeShader buffers
        private RenderTexture colorsOut;
        private VideoResolution colorsRes;

        private RenderTexture positionsOut;
        private VideoResolution positionsRes;

        private GPUPointCloud gpuPointCloud;
        //Compute shader parameters
        private int threadGroupsX;
        private int threadGroupsY;
        private int threadGroupsZ = 1;
        private int kernelIndex;

        //For Gamma correction
        private bool isSRGB;
        static class ID
        {
            public static int CombinedIn = Shader.PropertyToID("CombinedIn");

            public static int ColorsOut = Shader.PropertyToID("ColorsOut");
            public static int PositionsOut = Shader.PropertyToID("PositionsOut");

            public static int XYTable = Shader.PropertyToID("XYTable");
            //public static int MaxDepth = Shader.PropertyToID("MaxDepth");

            public static int Width = Shader.PropertyToID("Width");
            public static int Height = Shader.PropertyToID("Height");
            public static int ContainerHeight = Shader.PropertyToID("GlobalHeight");
            public static int EncodeShift = Shader.PropertyToID("EncodeShift");
            public static int EncodeMulti = Shader.PropertyToID("EncodeMulti");
            public static int UnitySc = Shader.PropertyToID("UnitySc");
            public static int MethodIndex = Shader.PropertyToID("MethodIndex");
            public static int OrthoProjected = Shader.PropertyToID("OrthoProjected");
            public static int PerspProjected = Shader.PropertyToID("PerspProjected");
            public static int ProjectionScreenWidth = Shader.PropertyToID("ProjectionScreenWidth");
            public static int ProjectionScreenHeight = Shader.PropertyToID("ProjectionScreenHeight");
            public static int ScreenCameraDistance = Shader.PropertyToID("ScreenCameraDistance");
            public static int OrthoProjectionCubeScale = Shader.PropertyToID("OrthoProjectionCubeScale");
            public static int IsSRGB = Shader.PropertyToID("IsSRGB");
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

        ////TODO: mutualize this with other modules (for now MaterialRenderer and CombinedToVfx)
        ///// <summary>Tries to find the user index of the camera from the name of the GameObject.</summary>
        //int GetMyCamIndex()
        //{
        //    int _myCamIndex = -1;
        //    var indexOfOpen = this.gameObject.name.LastIndexOf("(");
        //    var indexOfClose = this.gameObject.name.LastIndexOf(")");
        //    string number = "";
        //    if (indexOfOpen >= 0 && indexOfClose >= 0)
        //        number = this.gameObject.name.Substring(indexOfOpen + 1, indexOfClose - indexOfOpen - 1);

        //    if (!int.TryParse(number, out _myCamIndex))
        //        throw new Exception("GetMyCamIndex: has not found any cam number between parenthesis int he nam of the GameObject.");

        //    return _myCamIndex;
        //}
        #endregion

        //TODO: mutualize this function with MaterialRenderer and VfxGraphRenderer
        // Or maybe make an interface IContextSetable (bad name)
        public override void GetSettingsByContext()
        {
            FindContextInstance();
            if(ContextManager.instance == null) return;
            var ctxt = ContextManager.instance.currentContext;
            int _camIndex = Helper.GetCamIndex(this.gameObject);
            var camConfig = ctxt.CamConfig(_camIndex);
            if (ctxt.DepthInColorEncoding(_camIndex).Method != DepthinColorMethod.Undefined)
                depthInColorEncoding = ctxt.DepthInColorEncoding(_camIndex);

            unityScale = camConfig.exportScale == CamConfig.ExportScale.UnityScale;
            orthoProjected = ctxt.FusionConfig.exportType == ExportType.OrthoProjected;
            perspProjected = ctxt.FusionConfig.exportType == ExportType.PerspectiveProjected;
            orthoProjectionCubeScale = camConfig.projectonScreenHeight;
            screenCameraDistance = camConfig.screenCameraDistance;
            projectionScreenHeight = camConfig.projectonScreenHeight;
            projectionScreenWidth = camConfig.projectonScreenWidth;
            xytFolder = Path.Combine(ctxt.AppConfig.LocalDataFolder, ctxt.AppConfig.XYTableFolder);
            myKinectSerial = camConfig.SerialNumber;
         }
        public override void PropagateToContext()
        {
            base.PropagateToContext();
            var ctxt = ContextManager.instance.currentContext;
            int _camIndex = Helper.GetCamIndex(this.gameObject);
            var camConfig = ctxt.CamConfig(_camIndex);
            camConfig.DepthInColorEncoding = depthInColorEncoding;

            if (unityScale) camConfig.exportScale = CamConfig.ExportScale.UnityScale;
            else camConfig.exportScale = CamConfig.ExportScale.KinectScale;

            if (orthoProjected) camConfig.projectonScreenWidth = camConfig.projectonScreenHeight = orthoProjectionCubeScale;
            else if (perspProjected)
            {
                camConfig.projectonScreenWidth = projectionScreenWidth;
                camConfig.projectonScreenHeight = projectionScreenHeight;
            }
            ctxt.AppConfig.XYTableFolder = xytFolder.Substring(ctxt.AppConfig.LocalDataFolder.Length);
            camConfig.SerialNumber = myKinectSerial;
        }

        public override void HotInit(DataEventArgs<RenderTexture> e)
        {
            RenderTexture initRT = e.Output;
            containerRes.width = initRT.width;
            containerRes.height = initRT.height;
            

            //Very basic resolution choosing system
            //switch (containerRes.width)
            //{
            //    case 640:
            //        colorsRes = positionsRes = new VideoResolution(320, 288);
            //        break;
            //    case 1280:
            //        colorsRes = positionsRes = new VideoResolution(640, 640);
            //        break;
            //    case 2560:
            //        colorsRes = positionsRes = new VideoResolution(1280, 720);
            //        break;
            //    default: 
            colorsRes = positionsRes = new VideoResolution(containerRes.width / 2,containerRes.height);
            //        break;
            //}
            if (!orthoProjected && !perspProjected)

            {
                //Launch XYTable parsing
                if (_xyTable == null && !XYTableParsing)
                {

                    XYTableParsing = true;
                    Task.Run(() =>
                    {
                        _XYtableParser = new XYTableParser(positionsRes.width, positionsRes.height, xytFolder, myKinectSerial, !noFlipY);
                        XYTableParsing = false;
                    }
                    );
                }


                //Prepare output buffers for ComputeShader


                if (XYTableParser == null)
                {
                    Debug.LogWarning("XYTable for Kinect " + myKinectSerial + " not ready (yet). Parsing is probably still in process, should not take long, be patient..." + " FOLDER : "+ xytFolder);
                    return; // Table is not ready.
                }
                else // Allocate and initialize the XY table ComputeBuffer.
                {
                    _xyTable = new ComputeBuffer(XYTableParser.Length, sizeof(float));
                    _xyTable.SetData(XYTableParser.ToArray());
                    // _xyTable.SetData(XYTableParser);  //unsafe version (with an extension)
                }
            }
            else
            {
                float[] arr = new float[positionsRes.width * positionsRes.height];
                for (int i = 0; i < arr.Length; i++)
                {
                    arr[i] = 1;
                }
                _xyTable = new ComputeBuffer(arr.Length, sizeof(float));
                _xyTable.SetData(arr);
            }

            //Detect if the texture format is linear or sRGB and set isSRGB bool
            isSRGB = initRT.sRGB;
            Debug.Log("Init RT Format is sRGB in Combined to VFX : " + isSRGB);

            gpuPointCloud = new GPUPointCloud();
            IBufferCreator bufferCreator = this;
            positionsOut = bufferCreator.PreparePositionBuffer(positionsRes);
            colorsOut = bufferCreator.PrepareColorBuffer(colorsRes);
            PrepareComputeShader(positionsRes);

            base.HotInit(e);
        }

        public override void ProcessData(object sender, DataEventArgs<RenderTexture> e)
        {
            if (!enabled) return;

            if (!IsHotInitComplete) HotInit(e);
            if(!orthoProjected && !perspProjected)
            {
                if (XYTableParser == null) return;
            }

            combinedToPCUnpacker.SetTexture(kernelIndex, ID.CombinedIn, e.Output);

            combinedToPCUnpacker.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);
            gpuPointCloud.positionMap = positionsOut;
            gpuPointCloud.colorMap = colorsOut;
            OutputReady?.Invoke(this, new(gpuPointCloud, e.FrameInfo));
        }

        public override void CleanUp()
        {
            if (_xyTable != null) _xyTable.Dispose();
            if (positionsOut != null) positionsOut.Release();
            if (colorsOut != null) colorsOut.Release();

            base.CleanUp();
        }

        private void PrepareComputeShader(VideoResolution res)
        {
            combinedToPCUnpacker = Instantiate(combinedToPCUnpacker); //If we don't instantiate, the same CS is used by several modules.
            threadGroupsX = Mathf.CeilToInt(colorsOut.width / 8);
            threadGroupsY = Mathf.CeilToInt(colorsOut.height / 8);

            kernelIndex = combinedToPCUnpacker.FindKernel("CSMain");

            combinedToPCUnpacker.SetBuffer(kernelIndex, ID.XYTable, _xyTable);
            
            combinedToPCUnpacker.SetTexture(kernelIndex, ID.PositionsOut, positionsOut);
            combinedToPCUnpacker.SetTexture(kernelIndex, ID.ColorsOut, colorsOut);
            combinedToPCUnpacker.SetInt(ID.Width, res.width);
            combinedToPCUnpacker.SetInt(ID.Height, res.height);
            combinedToPCUnpacker.SetInt(ID.ContainerHeight, containerRes.height);
            encodeShift = depthInColorEncoding.GetEncodeShift();
            combinedToPCUnpacker.SetInt(ID.EncodeShift, encodeShift);
            Debug.Log("Combined to VFX Decoding Method : " + (int)depthInColorEncoding.Method);
            combinedToPCUnpacker.SetInt(ID.MethodIndex, (int)depthInColorEncoding.Method);
            combinedToPCUnpacker.SetFloat(ID.EncodeMulti, depthInColorEncoding.GetEncodeMultiplier());
            combinedToPCUnpacker.SetBool(ID.UnitySc, unityScale);
            combinedToPCUnpacker.SetBool(ID.OrthoProjected, orthoProjected);
            combinedToPCUnpacker.SetBool(ID.PerspProjected, perspProjected);
            combinedToPCUnpacker.SetFloat(ID.OrthoProjectionCubeScale, orthoProjectionCubeScale);
            combinedToPCUnpacker.SetFloat(ID.ProjectionScreenWidth, projectionScreenWidth);
            combinedToPCUnpacker.SetFloat(ID.ProjectionScreenHeight, projectionScreenHeight);
            combinedToPCUnpacker.SetFloat(ID.ScreenCameraDistance, screenCameraDistance);
            combinedToPCUnpacker.SetBool(ID.IsSRGB, isSRGB);
        }



    }
}
