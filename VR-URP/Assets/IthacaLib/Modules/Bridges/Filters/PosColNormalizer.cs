/*#############################################################################################
# ---- PosColNormalizer.cs ----
# Project: IthacaEmetteurRecepteur by FrenchTouchFactory
# Resp. : Francois
# Author(s): Francois Bouille (f.bouille@frenchtouchfactory.com)
# Starting date : 2024/04/05
# Latest version date : 2024/04/06
# Problem : The fusion uses average weights on the alpha channel of the depth. In the end we display only superior to 0 alpah datas of Depth and Color. The resulting data has alpha values between 0 and 1. It's not clean. Alpha datas should be 0 or 1. Meaning we see it or not. 
# 
# Hypothesis : A position and color normalizer could solve this issue.
# 
# Description : Every pixel with a position value alpha at more than zero has its alpha value set to 1. Else position (w,y,z,w) and color (r,g,b,a) are set to zero  
# Limits : -
# Further improvements : 
# Parameters : 
# Note : Need a position and color texture (Tuple) input
# Compute shader needed : PosColNormalizerCompute
# Tests : Laurane 4 cams
#############################################################################################*/

using System;
using UnityEngine;


namespace IthacaLib
{
    /// <summary>
    /// From the separated position map and color map a compute shader filters the alpha values on depth and color and normalize them to 0 or 1
    /// The result is a depth and color Tuple with only alpha values to 1 when a value exists and color and depth to 0 when it doesn't exist. 
    /// </summary>
    public class PosColNormalizer : AbstractFilter
    {
        public override event EventHandler<DataEventArgs<GPUPointCloud>> OutputReady;



        protected class ID
        {
            public static int PosMapIn = Shader.PropertyToID("PosMapIn");
            public static int ColMapIn = Shader.PropertyToID("ColMapIn");

        }


        public override void HotInit(DataEventArgs<GPUPointCloud> e)
        {
            //Prepare  ComputeShader
            InitFilter(e.Output);

            base.HotInit(e);
        }

        public override void ProcessData(object sender, DataEventArgs<GPUPointCloud> e)
        {
            if (!enabled) return;

            if (!IsHotInitComplete) HotInit(e);

            if (isFilterEnabled)
            {
                SetFilterAndDispatch(e.Output);
            }
            OutputReady?.Invoke(this, e);

        }

        protected void SetFilterAndDispatch(GPUPointCloud pC)
        {
            filterInstance.SetTexture(kernelIndex, ID.PosMapIn, pC.positionMap);
            filterInstance.SetTexture(kernelIndex, ID.ColMapIn, pC.colorMap);

            filterInstance.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);
        }
    }
}
