/*#############################################################################################
# ---- EdgePointWeigthing_android ----
# BRIDGE: (RenderTexture(PosMap),RenderTexture(ColMap)) --> (RenderTexture(PosMap),RenderTexture(ColMap))
# + Edge Thickness (int) and top/bottom (int) and depth distance from the edge to the background or foreground (int) + power (float) + vizualize weigh (bool)
#
#----------------------------------------
# Project: IthacaLib
# Resp. : François
# Author(s): François Bouille (f.bouille@frenchtouchfactory.com - French Touch Factory
# Starting date : 2024/02/05
# Latest version date : V1.0 : 2024/02/08,  V1.1 : 2024/05/03
# Problem : To prepare fusion we need to weight the pixels according to their realiability
# Hypothesis : The realiabilty could be evaluated according to the distance to the edges
# Description: Takes a Position Map RenderTexture from a combined PosMap And ColMap (CombinedToVFX or any filter) 
# and give a weight (float) to each vertex calculated with a power (float) from the edge - 0 to the inside - 1 applyed to the posMap alpha channel. The colMap is unchanged and transfered to the next step. We use the colMap to vizulaize the weight. 
#              It helps preparing for the fusion. It takes into account the alpha values previously set to 0 by the filters. The weighting only begins after the last 0 values. 
# Use it with PosMapPointWeightingCompute compute shader
# V1.1 : Adding a priority coefficient to the considred camera
# Limits : Doesn't consider the data with a strong tangency to the camera
# Further improvements : 
# Note : Important ! Visualize the filters separately. Set the visualizeWeigh to false to have it used in the process. 
# Tests : -
#############################################################################################*/

using System;
using UnityEngine;
using IthacaLib.Common;

namespace IthacaLib
{
    /// <summary>
    /// From the separated position map a compute shader set a weighted value of the posMal alpha based on a power function.
    /// The result is a weighted point cloud, it's used just before the transform by matrix
    /// </summary>
    public class EdgesPointWeighting_android : EdgesPointWeighting, IBufferCreator
    {
        public override event EventHandler<DataEventArgs<GPUPointCloud>> OutputReady;

        private RenderTexture colorsOut;
        private RenderTexture positionsOut;

        private VideoResolution containerRes;
        class ID_android : ID
        {
            public static int PosMapOut = Shader.PropertyToID("PosMapOut");
            public static int ColMapOut = Shader.PropertyToID("ColMapOut");
        }


        public override void HotInit(DataEventArgs<GPUPointCloud> e)
        {
            GPUPointCloud initRT = e.Output;
            containerRes.width = initRT.positionMap.width;
            containerRes.height = initRT.positionMap.height;

            IBufferCreator bufferCreator = this;
            positionsOut = bufferCreator.PreparePositionBuffer(containerRes);
            colorsOut = bufferCreator.PrepareColorBuffer(containerRes);

            base.HotInit(e);

            //Prepare  ComputeShader
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
