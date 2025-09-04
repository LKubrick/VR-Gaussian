/*#############################################################################################
# ---- EdgesFilter ---- Compatible with Android
# BRIDGE: (RenderTexture(PosMap),RenderTexture(ColMap)) --> (RenderTexture(PosMap),RenderTexture(ColMap))
#
#----------------------------------------
# Project: IthacaLib
# Resp. : François
# Author(s): François Bouille (f.bouille@frenchtouchfactory.com) 100% - French Touch Factory
# Date : created in january 2024
# @date last modified 2024/09/13
# Description: Takes a Position Map RenderTexture from The Combined to PosMap And ColMap separator (CombinedToVFX) with a Render Texture Selector  
# and delete edges according to a defined thickness. The colMap is unchanged and tranfered to the next step. Changed the Threshold to a float because of the meter scale
#              It helps preparing for the fusion
# Parameters : - Edge Thickness right/left/top/bottom (int) and depth distance from the edge to the background or foreground (int)
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
    /// From the separated position map a compute shader filters the edges.
    /// The result is a filtered point cloud or can be used to prepare for fusion
    /// </summary>
    public class EdgesFilter_android : EdgesFilter, IBufferCreator
    {
        public override event EventHandler<DataEventArgs<GPUPointCloud>> OutputReady;


        private VideoResolution containerRes;  //Replaces old globalWidth & globalHeight
                                               // Edge filtering parameters


        //ColMapOut and PosMapOut
        private RenderTexture colorsOut;
        private RenderTexture positionsOut;


        class ID_android : ID
        {
            public static int PosMapOut = Shader.PropertyToID("PosMapOut");
            public static int PosMapRead = Shader.PropertyToID("PosMapRead");
            //public static int ColMapOut = Shader.PropertyToID("ColMapOut");
            public static int ColMapOut = Shader.PropertyToID("ColMapOut");
            public static int ColMapRead = Shader.PropertyToID("ColMapRead");

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
            base.HotInit(e);
            filterInstance.SetTexture(kernelIndex, ID_android.ColMapOut, colorsOut);
            filterInstance.SetTexture(kernelIndex, ID_android.PosMapOut, positionsOut);
        }

        public override void ProcessData(object sender, DataEventArgs<GPUPointCloud> e)
        {
            if (!enabled) return;

            if (!IsHotInitComplete) HotInit(e);
            if (isFilterEnabled)
            {

                SetFilterAndDispatch(e.Output);
                e.Output.positionMap = positionsOut;
                e.Output.colorMap = colorsOut;
            }
            OutputReady?.Invoke(this, e); 
        }

    }

}
