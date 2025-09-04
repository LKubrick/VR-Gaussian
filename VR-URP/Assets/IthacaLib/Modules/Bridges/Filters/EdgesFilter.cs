/*#############################################################################################
# ---- EdgesFilter ----
# BRIDGE: GPUPointCloud --> GPUPointCloud
# + Edge Thickness top (int) and bottom (int) left (int) and right (int) and depth distance from the edge to the background or foreground (int)
#
#----------------------------------------
# Project: IthacaLib
# Resp. : François
# Author(s): François Bouille (f.bouille@frenchtouchfactory.com - French Touch Factory
# Date : created in january 2024
# Description: Takes a Position Map RenderTexture from a ISource<GPUPointCloud>  
# and delete edges according to a defined thickness. The colMap is unchanged and tranfered to the next step.
#              It helps preparing for the fusion.
# Use it with PosMapEdgesFilterCompute compute shader
# Parameters : -
# Note : Important ! Por PC only !!! ALSO Visualize the filters separately. Set the visualizeFilter to false to have it used in the process.
# Tests : -
#############################################################################################*/

using System;
using UnityEngine;


namespace IthacaLib
{
    /// <summary>
    /// From the separated position map a compute shader filter the edges 
    /// The result is a filtered point cloud or can be used to prepare for fusion or display
    /// </summary>
    public class EdgesFilter : AbstractFilter
    {
        public override event EventHandler<DataEventArgs<GPUPointCloud>> OutputReady;


        public float depthThreshold_mm;
        public uint topThickness;
        public uint rightThickness;
        public uint bottomThickness;
        public uint leftThickness;
        public bool visualizeFilter;


        protected class ID
        {
            public static int PosMapRead = Shader.PropertyToID("PosMapRead");
            public static int ColMapRead = Shader.PropertyToID("ColMapRead");

            //public static int ColMapIn = Shader.PropertyToID("ColMapIn"); Pour éventuellement mettre en place un visualiseur de filtre

            public static int TransformMat = Shader.PropertyToID("TransformMat");

            public static int depththrshld = Shader.PropertyToID("depththrshld");

            public static int topthick = Shader.PropertyToID("topthick");
            public static int righthick = Shader.PropertyToID("rightthick");
            public static int bottomthick = Shader.PropertyToID("bottomthick");
            public static int leftthick = Shader.PropertyToID("leftthick");
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
        protected void SetFilterAndDispatch(GPUPointCloud pC)
        {
            filterInstance.SetTexture(kernelIndex, ID.PosMapRead, pC.positionMap);
            filterInstance.SetTexture(kernelIndex, ID.ColMapRead, pC.colorMap);

            filterInstance.SetFloat(ID.depththrshld, depthThreshold_mm);
            filterInstance.SetInt(ID.topthick, (int)topThickness);
            filterInstance.SetInt(ID.righthick, (int)rightThickness);
            filterInstance.SetInt(ID.bottomthick, (int)bottomThickness);
            filterInstance.SetInt(ID.leftthick, (int)leftThickness);
            filterInstance.SetBool(ID.visualize, visualizeFilter);
            filterInstance.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);
        }

    }
}
