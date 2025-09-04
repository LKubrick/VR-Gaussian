/*#############################################################################################
# ---- AntiAliasingFilter ---- Compatible with Android
# BRIDGE: (RenderTexture(PosMap),RenderTexture(ColMap)) --> (RenderTexture(PosMap),RenderTexture(ColMap))
#
#----------------------------------------
# Project: IthacaLib
# Resp. : François
# Author(s): François Bouille (f.bouille@frenchtouchfactory.com) 100% - French Touch Factory
# Date : created in january 2025
# @date last modified 
# Problem: The depth data in general and the rasterization process can generate aliased edges
# Hypothesis : Some Antialising filters based on depth or Luminosity (Value in HSV) or even temporal could help smoothing the edges
# Description : We present here an Anti Aliasing Method inspired by the MSAA. It's based on the depth. We detect an edge and apply a
# blur algorithm for depth and color in a given thickness starting from the edges of the PC. The blur is calculated with a weithed average
# between the center value and the cube values (non zero) identified as part of the PC. 
# Parameters : - depthThreshold - thickness_pixel - smoothingFactor - blurDensity - depthOnly
# Note : -
# Tests : -
# IMPORTANT NOTICE : this module and its associated compute shader are made to be compatible with Android. For example, the posMap (in its RT form) 
#   is duplicated by the module because compute shaders, in Android, apparently cannot read and write in the same RT. For optimisation, it could be 
#   useful to implement a non-android version that overwrite the same PosMap RT instead of creating a new one (coming with the risk of overwriting
#   something we still need...).
#############################################################################################*/

using IthacaLib.Common;
using System;
using UnityEngine;


namespace IthacaLib
{
    /// <summary>
    /// </summary>
    public class AntiAliasing : AbstractFilter, IBufferCreator
    {
        public override event EventHandler<DataEventArgs<GPUPointCloud>> OutputReady;


        private VideoResolution containerRes;  //Replaces old globalWidth & globalHeight
                                               // Edge filtering parameters
        public float depthThreshold = .5f;
        //public int samplingRate;
        [Tooltip("zone of influence of the blur starting from the edge (in pixels) / -1 value the blur is on all the point cloud")]
        public int thickness_pixel =2;
        [Tooltip("between 0 and 1 - 0 strong blur, 1 smooth blur")]
        public float smoothingFactor = 0;
        [Tooltip("How many pixels around the central pixel are used to blur the data")]
        public int blurDensity =5;
        [Tooltip("With a Depth Threshold = 0 this filter can be used as a depth smoother for all the surface od the point cloud")]
        public bool depthOnly;

        //ColMapOut and PosMapOut
        private RenderTexture colorsOut;
        private RenderTexture positionsOut;


        static class ID
        {
            public static int PosMapOut = Shader.PropertyToID("PosMapOut");
            public static int PosMapRead = Shader.PropertyToID("PosMapRead");
            public static int ColMapOut = Shader.PropertyToID("ColMapOut");
            public static int ColMapRead = Shader.PropertyToID("ColMapRead");

            public static int depththrshld = Shader.PropertyToID("depththrshld");
            //public static int SamplingRate = Shader.PropertyToID("SamplingRate");
            public static int thickness = Shader.PropertyToID("thickness");
            public static int smoothingFactor = Shader.PropertyToID("smoothingFactor");
            public static int blurDensity = Shader.PropertyToID("blurDensity");
            public static int depthOnly = Shader.PropertyToID("depthOnly");
        }


        public override void HotInit(DataEventArgs<GPUPointCloud> e)
        {
            GPUPointCloud initRT = e.Output;
            containerRes.width = initRT.positionMap.width;
            containerRes.height = initRT.positionMap.height;

            IBufferCreator bufferCreator = this;
            positionsOut = bufferCreator.PreparePositionBuffer(containerRes);
            colorsOut = bufferCreator.PrepareColorBuffer(containerRes);
            //Prepare  ComputeShader
            InitFilter();

            base.HotInit(e);
        }

        public override void ProcessData(object sender, DataEventArgs<GPUPointCloud> e)
        {
            if (!enabled) return;

            if (!IsHotInitComplete) HotInit(e);

            if (isFilterEnabled)
            {
                //PosMap Read
                filterInstance.SetTexture(kernelIndex, ID.PosMapRead, e.Output.positionMap);
                //ColMap Read
                filterInstance.SetTexture(kernelIndex, ID.ColMapRead, e.Output.colorMap);
                filterInstance.SetFloat(ID.depththrshld, depthThreshold);
                //filterInstance.SetInt(ID.SamplingRate, samplingRate);
                filterInstance.SetInt(ID.thickness, thickness_pixel);
                filterInstance.SetInt(ID.blurDensity, blurDensity);
                filterInstance.SetFloat(ID.smoothingFactor, smoothingFactor);
                filterInstance.SetBool(ID.depthOnly, depthOnly);
                filterInstance.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);
                e.Output.positionMap = positionsOut;
                e.Output.colorMap = colorsOut;
            }
            OutputReady?.Invoke(this, e); //positionsOut only
        }

        private void InitFilter()
        {
            filterInstance = Instantiate(filter); //If we don't instantiate, the same CS is used by several modules. Intéressant
            threadGroupsX = Mathf.CeilToInt(containerRes.width / 8);
            threadGroupsY = Mathf.CeilToInt(containerRes.height / 8);
            kernelIndex = filterInstance.FindKernel("CSMain");

            
            filterInstance.SetTexture(kernelIndex, ID.ColMapOut, colorsOut);
            filterInstance.SetTexture(kernelIndex, ID.PosMapOut, positionsOut);

        }
    }

}
