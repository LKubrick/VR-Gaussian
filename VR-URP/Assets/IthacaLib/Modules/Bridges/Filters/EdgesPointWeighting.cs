/*#############################################################################################
# ---- PosMapTransformer ----
# BRIDGE: (RenderTexture(PosMap),RenderTexture(ColMap)) --> (RenderTexture(PosMap),RenderTexture(ColMap))
# + Edge Thickness (int) and top/bottom (int) and depth distance from the edge to the background or foreground (int) + power (float) + vizualize weigh (bool)
#
#----------------------------------------
# Project: IthacaLib
# Resp. : François
# Author(s): François Bouille (f.bouille@frenchtouchfactory.com - French Touch Factory
# Starting date : 2024/02/05
# Latest version date : V1.0 : 2024/02/08,  V1.1 : 2024/05/03
# Problem : To prepared fusion we need to wieth the pixels according to their realiability
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


namespace IthacaLib
{
    /// <summary>
    /// From the separated position map a compute shader set a weighted value of the posMal alpha based on a power function.
    /// The result is a weighted point cloud, it's used just before the transform by matrix
    /// </summary>
    public class EdgesPointWeighting : AbstractFilter
    {
        public override event EventHandler<DataEventArgs<GPUPointCloud>> OutputReady;

        public float depthThreshold_mm;
        public uint WeightingTopThickness;
        public uint WeightingRightThickness;
        public uint WeightingBottomThickness;
        public uint WeightingLeftThickness;
        public float Power=1f; //Speed to go from 0 to 1 weighting value
        public float priorityCoef=1f; //1 is the basic value, more than 1 gives a priority coefficient to the considered camera
        public bool visualizeWeigh;


        protected class ID
        {
            public static int PosMapIn = Shader.PropertyToID("PosMapIn");

            public static int ColMapIn = Shader.PropertyToID("ColMapIn"); // Voir la pondération

            public static int TransformMat = Shader.PropertyToID("TransformMat");

            public static int depththrshld = Shader.PropertyToID("depththrshld");

            public static int topthick = Shader.PropertyToID("topthick");
            public static int righthick = Shader.PropertyToID("rightthick");
            public static int bottomthick = Shader.PropertyToID("bottomthick");
            public static int leftthick = Shader.PropertyToID("leftthick");
            public static int power = Shader.PropertyToID("power");
            public static int priorCoef = Shader.PropertyToID("priorCoef");
            public static int visualize = Shader.PropertyToID("visualize");

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
        protected void SetFilterAndDispatch(GPUPointCloud PC)
        {
            filterInstance.SetTexture(kernelIndex, ID.PosMapIn, PC.positionMap);
            filterInstance.SetTexture(kernelIndex, ID.ColMapIn, PC.colorMap);  //Voir la pondération
            filterInstance.SetFloat(ID.depththrshld, depthThreshold_mm);
            filterInstance.SetInt(ID.topthick, (int)WeightingTopThickness);
            filterInstance.SetInt(ID.righthick, (int)WeightingRightThickness);
            filterInstance.SetInt(ID.bottomthick, (int)WeightingBottomThickness);
            filterInstance.SetInt(ID.leftthick, (int)WeightingLeftThickness);
            filterInstance.SetFloat(ID.power, Power);
            filterInstance.SetFloat(ID.priorCoef, priorityCoef);
            filterInstance.SetBool(ID.visualize, visualizeWeigh);
            filterInstance.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);
        }
    }
}
