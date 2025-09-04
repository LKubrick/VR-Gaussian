/*#############################################################################################
# ---- PosColMapCombinatorToTexture3DVoxelFusion ----
# Project: IthacaEmetteurRecepteur by FrenchTouchFactory
# Resp. : François
# Author(s): François Bouille (f.bouille@frenchtouchfactory.com)
# Starting date : 2024/05/01
# Latest version date : created on 2024/07/05
# Problem : For the moment we used fusion methods to display our hologram on planes. This approach induce crenelated or flickering edges. 
# Hypothesis : We experiment here a method to display our full body holograms in a 3D voxel space. The fused edges between the different registrates point cloud in the voxel space should prevent the edges from flickering.
# Description: V1.0 : We use as input several registered point clouds. The PCs are placed in a cube. We fill a 3D texture with those datas and average every point according to their weight. 
# At the end fusion the alpha used by the weighting process and average is set to 0 or 1. 
# The result is a 3D texture that can be displayed by a shader. We provide here 7 shaders as examples. We test here a Raymarching method to display the volume stored in the 3D Texture. 
# This displaying method is light. It attach every adjoining voxel.
# Limits : Displaying with a resolution more than a 512x512x512 = 134 217 728 voxels starts slowing down the system. Adjoining voxels really need to be filled or a hole is generated. The Raymarching step size need to be adjusted according to the container cube size.
# Further improvements : The fusion method can be improved (Average, other weithing methods). More shaders should be developed. Also the texture3D should be combined with VFX graphs to create even more astonishing effects. The step size can automatically be adjusted according to the container cube size.
# Adding our opposite point cloud occlusion method is possible for further optimisation improvements.
# Parameters : PosColMap sources list, FinalWidth, FinalHeight, FinalDepth, A Box containing the fusion, A box to display the fused volume.
# Note : Inputs are weithed and transformed PointClouds
# Compute shader needed : 3DTextureVoxelFusionCompute
# Tests : Tested on 4 cams MP4, Double buffer logically already added for MKV and direct live sensors, To be tested on a full 8 cam character and on Android/VR/AR headsets
#############################################################################################*/

using System;
using System.Linq;
using UnityEngine;
//using K4AdotNet.Sensor;
using IthacaLib.Common;


namespace IthacaLib
{

    ///<Summary>It produces a Fused PosColMap with Depth and Color images from several filtered and Weighted PocColMaps. Ortho projection of the PC datas on the Final Depth and Color. 
    ///The final PosColMap is defined proportionnaly by the front side of the bounding box, the FinalWidth and the FinalHeight. For a perfect result the FinalPosColMap data alpha channel should be defined at 1 after fusion in the future.
    ///But it already works with an alpha > 0 cos the display method we use in shader and VFX graph only takes the alpha = 0 or alpha >0 into account</summary>
    public class PosColMapCombinatorToTexture3DVoxelFusion : SourceModuleBase<RenderTexture>
    {
        [Tooltip("SingleOrthoFusionCompute is the choice")]
        public ComputeShader OrthoFusionCompute;
        public RenderTexture VoxelsOut;
        //private RenderTexture BlackVoxelsOut;

        private VideoResolution containerRes;

        int[] posColMapFrameID;

        [Tooltip("A source of colMap Image, i.e. a IImageProducer. Will automatically plug to a ColorToDepthTransformer nearby for convenience, but it can be changed of course.")]
        public Component[] posColMapSources;

        [Tooltip("Resolution of the Final Ortho Fusionned PosColMap, cannot the more dense than the point clouds")]
        public int finalWidth;
        public int finalHeight;
        public int finalDepth;

        bool[] posColMapIsHotInitComplete;

        bool[] posColMapSourcesFinished;

        public override event EventHandler<DataEventArgs<RenderTexture>> OutputReady;
        public override event EventHandler HotInitialized;
        public override event EventHandler HasFinished;

        bool[] colorIsHotInitComplete;

        bool[] colorSourcesFinished;

        bool fusionDone;

        //A cube whose faces will be used to dimension the orthoPosColMap. Should be the bounding box
        public GameObject Box;
        //Each relevant corner of the cube corresponds to the relevant corners of the ortho images.
        Vector4 Origin;
        Vector4 Xcorner;
        Vector4 Ycorner;
        Vector4 Zcorner;

        //Compute shader parameters
        private int threadGroupsX;
        private int threadGroupsY;
        private int threadGroupsZ = 1;
        private int kernelIndex;

        public GameObject DisplayCube;
        //public float depthThreshold_ZpixelUnit;
        private int fusionCount;
        private bool lastFusion; 
        #region Unity framework Inspector logic
        void OnValidate()
        {
            //By default, we automatically search for those Producers but we can plug any other compatible Producers     
            if (posColMapSources == null) posColMapSources = FindObjectsOfType<SourceModuleBase<GPUPointCloud>>();
        }

        private void Start()
        {
            //RGBok = new bool[colorSources.Length]; 
            //DepthOk = new bool[colorSources.Length]; 
            colorIsHotInitComplete = new bool[posColMapSources.Length]; 

            colorSourcesFinished = new bool[posColMapSources.Length];

            posColMapFrameID = new int[posColMapSources.Length];


            //Do the Events connections in Start as the GameObjects need to exist
            Connect();
        }
        #endregion

        private void Connect()
        {
            foreach (var colorSource in posColMapSources)
            {
                ((ISourceModule<GPUPointCloud>)colorSource).OutputReady += ColorsRGBReady;
                ((ISourceModule<GPUPointCloud>)colorSource).HotInitialized += OnHotInitFromSource;
                ((ISourceModule<GPUPointCloud>)colorSource).HasFinished += OnHasFinishedFromSource;

            }
        }

        private void Disconnect()
        {
            foreach (var colorSource in posColMapSources)
            {
                ((ISourceModule<GPUPointCloud>)colorSource).OutputReady -= ColorsRGBReady;
                ((ISourceModule<GPUPointCloud>)colorSource).HotInitialized -= OnHotInitFromSource;
                ((ISourceModule<GPUPointCloud>)colorSource).HasFinished -= OnHasFinishedFromSource;

            }
        }

        void OnHotInitFromSource(object sender, EventArgs e)
        {
            //Debug.Log("OnHotInitFromSource in MULTICOMBINATOR " + this.GetType().Name + " (" + this.name + ")");
            IsHotInitComplete = false;
            for (int i = 0; i < colorIsHotInitComplete.Length; ++i)
            {
                colorIsHotInitComplete[i] = false;
  
                colorSourcesFinished[i] = false;
 
            }
        }

        void OnHasFinishedFromSource(object sender, EventArgs e)
        {
            int indexColor = Array.IndexOf(posColMapSources, sender);

            if (indexColor != -1) { colorSourcesFinished[indexColor] = true; }

            if (AllTrue(colorSourcesFinished))
            {
                FinishProcess();
                HasFinished?.Invoke(this, EventArgs.Empty);
                for (int i = 0; i < posColMapSources.Length; ++i)
                {
                    colorSourcesFinished[i] = false;
                }
            }
        }

        bool AllTrue(bool[] _boolArray)
        {
            bool retvalue = true;
            foreach (bool value in _boolArray)
            { retvalue &= value; }
            return retvalue;
        }

        public override void FinishProcess()
        {
            Debug.Log("SPECIAL --- FinishProcess in " + this.GetType().Name);
        }

        void InitByColor(int streamIndex, int _colorWidth, int _colorHeight)
        {
            containerRes.width = _colorWidth;
            containerRes.height = _colorHeight;
            colorIsHotInitComplete[streamIndex] = true;
            if (colorIsHotInitComplete.All(x => x)) HotInit();
        }

        public override void HotInit()
        {
            //Debug.Log("HotInit in MULTICOMBINATOR " + this.GetType().Name + " (" + this.name + ")");

            //PrepareOutPositionBuffer();
            //PrepareOutColorBuffer();
            PrepareOutVoxels();
            PrepareComputeShader();

            //base.HotInit();
            IsHotInitComplete = true;
            HotInitialized?.Invoke(this, EventArgs.Empty);
        }

        ///<summary>Creates or recreates the Texture2D, sets some parameters and gives to tba the byte[] representing the raw texture data


        private void PrepareOutVoxels()
        {
            VoxelsOut = new RenderTexture(finalWidth, finalHeight, 0, RenderTextureFormat.ARGB32) // depth refers to the depth buffer
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                enableRandomWrite = true,
                useMipMap = false,
                filterMode = FilterMode.Trilinear,
                graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm,// not to have the srgb gamma correction
                volumeDepth = finalDepth // Strange but it's like that
            };

            //VoxelsOut.volumeDepth = finalDepth; 
            VoxelsOut.Create();

            /*BlackVoxelsOut = new RenderTexture(finalWidth, finalHeight, 0, RenderTextureFormat.ARGBFloat)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                enableRandomWrite = true,
                useMipMap = false,
                filterMode = FilterMode.Point
            };
            BlackVoxelsOut.volumeDepth = finalDepth; 
            BlackVoxelsOut.Create();*/
        }
        

        static class ID
        {
            public static int PCVoxelsOut = Shader.PropertyToID("PCVoxelsOut");
            //public static int PosMapOut = Shader.PropertyToID("PosMapOut");
            public static int PosMapRead = Shader.PropertyToID("PosMapRead");
            //public static int ColMapOut = Shader.PropertyToID("ColMapOut");
            public static int ColMapRead = Shader.PropertyToID("ColMapRead");
            public static int O = Shader.PropertyToID("O");
            public static int X = Shader.PropertyToID("X");
            public static int Y = Shader.PropertyToID("Y");
            public static int Z = Shader.PropertyToID("Z");
            public static int orthoWidth = Shader.PropertyToID("orthoWidth");
            public static int orthoHeight = Shader.PropertyToID("orthoHeight");
            public static int orthoDepth = Shader.PropertyToID("orthoDepth");
            public static int LastFusion = Shader.PropertyToID("LastFusion");
            public static int FusionCount = Shader.PropertyToID("FusionCount");
            //public static int depthThrsld = Shader.PropertyToID("depthThrsld");
        }
        private void PrepareComputeShader()
        {
            

            OrthoFusionCompute = Instantiate(OrthoFusionCompute); //If we don't instantiate, the same CS is used by several modules. Intéressant
            threadGroupsX = Mathf.CeilToInt(containerRes.width / 8);
            threadGroupsY = Mathf.CeilToInt(containerRes.height / 8);
            kernelIndex = OrthoFusionCompute.FindKernel("CSMain");
            OrthoFusionCompute.SetTexture(kernelIndex, ID.PCVoxelsOut, VoxelsOut);
            //OrthoFusionCompute.SetTexture(kernelIndex, ID.ColMapOut, colorsOut);
            //OrthoFusionCompute.SetTexture(kernelIndex, ID.PosMapOut, positionsOut);

            //Debug.Log("X distance : " + (Xcorner.x - Origin.x));

            //Debug.Log("Y distance : " + (Ycorner.y - Origin.y));

        }

        private void PrepareOrthoCorners()
        {
            //Vector3[] vertices = new Vector3[24];

            /*for(int i = 0; i < vertices.Length; i++) 
            {
                vertices[i] = Box.GetComponent<MeshFilter>().mesh.vertices[i];
            }

            Box.GetComponent<MeshFilter>().mesh.vertices = vertices;

            Box.GetComponent<MeshFilter>().mesh.RecalculateBounds();*/

            //cf le schéma de la création d'un cube dans Unity 24 vertex : https://forum.unity.com/threads/cube-mesh-order-of-vertices-and-triangles.873901/ 

            //Front face of the cube
            //Origin.w = 1 valeur inutile mise à 1 pour homogeineité, mais le vector4 est bien pratique pour envoyer un vecteur3 dans un compute shader sans avoir à créer un computeBuffer.
            Origin = Box.transform.TransformPoint(new Vector4(Box.GetComponent<MeshFilter>().mesh.vertices[7].x, Box.GetComponent<MeshFilter>().mesh.vertices[7].y, Box.GetComponent<MeshFilter>().mesh.vertices[7].z, 1));
            Xcorner = Box.transform.TransformPoint(new Vector4(Box.GetComponent<MeshFilter>().mesh.vertices[6].x, Box.GetComponent<MeshFilter>().mesh.vertices[6].y, Box.GetComponent<MeshFilter>().mesh.vertices[6].z, 1));
            Ycorner = Box.transform.TransformPoint(new Vector4(Box.GetComponent<MeshFilter>().mesh.vertices[5].x, Box.GetComponent<MeshFilter>().mesh.vertices[5].y, Box.GetComponent<MeshFilter>().mesh.vertices[5].z, 1));
            Zcorner = Box.transform.TransformPoint(new Vector4(Box.GetComponent<MeshFilter>().mesh.vertices[14].x, Box.GetComponent<MeshFilter>().mesh.vertices[14].y, Box.GetComponent<MeshFilter>().mesh.vertices[14].z, 1));
            //Repère correspondant au dénombrement du compute shader
            //Origin = Box.transform.TransformPoint(new Vector4(Box.GetComponent<MeshFilter>().mesh.vertices[5].x, Box.GetComponent<MeshFilter>().mesh.vertices[5].y, Box.GetComponent<MeshFilter>().mesh.vertices[5].z, 1));
            //Xcorner = Box.transform.TransformPoint(new Vector4(Box.GetComponent<MeshFilter>().mesh.vertices[4].x, Box.GetComponent<MeshFilter>().mesh.vertices[4].y, Box.GetComponent<MeshFilter>().mesh.vertices[4].z, 1));
            //Ycorner = Box.transform.TransformPoint(new Vector4(Box.GetComponent<MeshFilter>().mesh.vertices[7].x, Box.GetComponent<MeshFilter>().mesh.vertices[7].y, Box.GetComponent<MeshFilter>().mesh.vertices[7].z, 1));
            //Zcorner = Box.transform.TransformPoint(new Vector4(Box.GetComponent<MeshFilter>().mesh.vertices[3].x, Box.GetComponent<MeshFilter>().mesh.vertices[3].y, Box.GetComponent<MeshFilter>().mesh.vertices[3].z, 1));
            
            //Debug.Log("Origin : " + Origin);
            //Debug.Log("Xcorner : " + Xcorner);
            //Debug.Log("Ycorner : " + Ycorner);
            //Debug.Log("Zcorner : " + Zcorner);

        }

            private void ColorsRGBReady(object sender, DataEventArgs<GPUPointCloud> e)
        {
            if (e.Output.positionMap != null && e.Output.colorMap != null)
            {
                int index = Array.IndexOf(posColMapSources, sender);
                
                if (!colorIsHotInitComplete[index])
                {
                    InitByColor(index, e.Output.positionMap.width, e.Output.positionMap.height);
                }
                else if (IsHotInitComplete)
                {
                    
                    //Ortho Fusion for each posColMapSource
                    
                    
                    OrthoFusionDataProcess(e.Output.positionMap, e.Output.colorMap);

                    //CopyColorIntoTexture(e.Output, index, posColMapSources.Length);
                    //Debug.Log("e.FrameID : " + e.FrameID);

                    //Debug.Log("index VoxelFusion : " + index + ", e.FrameID : " + e.FrameID);
                    
                    posColMapFrameID[index] = e.FrameInfo.frameID;

                    if (posColMapFrameID.All(x => x == e.FrameInfo.frameID))
                    {
                        for (int i = 0; i < posColMapSources.Length; ++i)
                        {
                            posColMapFrameID[i] = -1;
                        }
                        //Debug.Log($"ColorReady for {e.FrameID}");
                        OutputReady?.Invoke(this, new DataEventArgs<RenderTexture>(VoxelsOut, e.FrameInfo));
                        //Debug.Log("Frame sent : " + e.FrameID);
                        fusionDone = true;
                        Material material = DisplayCube.GetComponent<Renderer>().material;
                        material.SetTexture("_MainTex", VoxelsOut);
                    }

                }
            }
        }

        private void OrthoFusionDataProcess(RenderTexture posMapRead, RenderTexture colMapRead)
        {
            //Debug.Log("Fusion Done ? : "+fusionDone);
            if (fusionDone)
            {
                
                Graphics.SetRenderTarget(VoxelsOut, 0, CubemapFace.Unknown, -1); //-1 target all 3D render texture slices at ones
                GL.Clear(true, true, Color.clear);

                //reinitialse the fusion count
                fusionCount = 0;
                lastFusion = false;
            }
            fusionCount += 1;

            if (fusionCount == posColMapSources.Length) lastFusion = true;
            //PosMap Read
            OrthoFusionCompute.SetTexture(kernelIndex, ID.PosMapRead, posMapRead);
            //ColMap Read
            OrthoFusionCompute.SetTexture(kernelIndex, ID.ColMapRead, colMapRead);

            OrthoFusionCompute.SetBool(ID.LastFusion, lastFusion);

            //Could be real time like here or only done once in the prepareComputeShader()
            PrepareOrthoCorners();
            OrthoFusionCompute.SetVector(ID.O, Origin);
            OrthoFusionCompute.SetVector(ID.X, Xcorner);
            OrthoFusionCompute.SetVector(ID.Y, Ycorner);
            OrthoFusionCompute.SetVector(ID.Z, Zcorner);
            //OrthoFusionCompute.SetFloat(ID.depthThrsld, depthThreshold_ZpixelUnit);
            OrthoFusionCompute.SetInt(ID.orthoWidth, finalWidth);
            OrthoFusionCompute.SetInt(ID.orthoHeight, finalHeight);
            OrthoFusionCompute.SetInt(ID.orthoDepth, finalDepth);
            OrthoFusionCompute.SetInt(ID.FusionCount, fusionCount);

            OrthoFusionCompute.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);

            if (fusionDone) fusionDone = false;

            
        }

        /*private void CopyColorIntoTexture(Image imageToCopy, int imageIndex, int nbOfStreams)
        {
            int lineRowSize;
            if (nbOfStreams > 9)
            {
                Debug.LogError("this nb of streams is not yet taken into account");
                return;
            }
            if (nbOfStreams <= 4) lineRowSize = 2;
            else lineRowSize = 3;
            if (nbOfStreams == 2) lineRowSize = 1;


            for (int i = 0; i < colorHeight[imageIndex]; i++)
            {
                try
                {
                    System.Runtime.InteropServices.Marshal.Copy(imageToCopy.Buffer + (i * colorWidth[imageIndex] * 4), tba,
                        (i * finalWidth + (imageIndex % lineRowSize + 1) * depthWidth[imageIndex] + imageIndex % lineRowSize * colorWidth[imageIndex] + (imageIndex / lineRowSize) * depthHeight[imageIndex] * finalWidth) * 4, // will need to be changed if we allow different resolutions at the same time
                        colorWidth[imageIndex] * 4);
                }
                catch(Exception e)
                {
                    Debug.LogError($"Copying color went wrong: {e}");
                }
            }


        }*/

        /*private static int MyMax(int a, int b)
        {
            return a > b ? a : b;
        }*/

        public override void CleanUp()
        {
            VoxelsOut = null;
            //positionsOut = null;
            //colorsOut = null;
        }
    }
}