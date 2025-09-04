/*#############################################################################################
# ---- PosColNormalizer_android.cs ----
# Project: IthacaEmetteurRecepteur by FrenchTouchFactory
# Resp. : Francois
# Author(s): Francois Bouille (f.bouille@frenchtouchfactory.com)
# Starting date : 2024/04/05
# Latest version date : 2025/02/19
# Problem : The fusion uses average weights on the alpha channel of the depth. In the end we display only superior to 0 alpah datas of Depth and Color. The resulting data has alpha values between 0 and 1. It's not clean. Alpha datas should be 0 or 1. Meaning we see it or not. 
# 
# Hypothesis : A position and color normalizer could solve this issue.
# 
# Description : Every pixel with a position value alpha at more than zero has its alpha value set to 1. Else position (w,y,z,w) and color (r,g,b,a) are set to zero  
# Limits : -
# Further improvements : 
# Parameters : 
# Note : Need a position and color texture (Tuple) input
# Compute shader needed : PosColNormalizerCompute_android
# Tests : Laurane 4 cams
#############################################################################################*/

using System;
using UnityEngine;
using IthacaLib.Common;

namespace IthacaLib
{
    /// <summary>
    /// From the separated position map and color map a compute shader filters the alpha values on depth and color and normalize them to 0 or 1
    /// The result is a depth and color Tuple with only alpha values to 1 when a value exists and color and depth to 0 when it doesn't exist. 
    /// </summary>
    public class PosColNormalizer_android : PosColNormalizer, IBufferCreator
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
