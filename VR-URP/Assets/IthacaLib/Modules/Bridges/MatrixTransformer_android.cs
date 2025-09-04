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
*   A manipulator has beed added to the current script. I allows to add a transformation to the current alignment
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
    public class MatrixTransformer_android : MatrixTransformer, IBufferCreator
    {
        public override event EventHandler<DataEventArgs<GPUPointCloud>> OutputReady;

        private RenderTexture positionsOut;

        [Tooltip("Add a GameObject if you want to add a transformation to the existing alignment")]
        public GameObject manipulator;


        class ID_android : ID
        {
            public static int PosMapOut = Shader.PropertyToID("PosMapOut");
        }


        public override void HotInit(DataEventArgs<GPUPointCloud> e)
        {
            base.HotInit(e);
            //Prepare  ComputeShader
            IBufferCreator bufferCreator = this;
            positionsOut = bufferCreator.PreparePositionBuffer(containerRes);
            PosMapToMatPosMap.SetTexture(kernelIndex, ID_android.PosMapOut, positionsOut);
        }


        public override void ProcessData(object sender, DataEventArgs<GPUPointCloud> e)
        {
            if (!enabled) return;

            if (!IsHotInitComplete) HotInit(e);
            if (isFilterEnabled)
            {
                Matrix4x4 manipulatorMatrix = Matrix4x4.identity;
                if (manipulator != null) manipulatorMatrix = Matrix4x4.TRS(manipulator.transform.position, manipulator.transform.rotation, manipulator.transform.localScale);

                Matrix4x4 newTRS = manipulatorMatrix * alignmentMatrix;
                PosMapToMatPosMap.SetTexture(kernelIndex, ID_android.PosMapIn, e.Output.positionMap);
                PosMapToMatPosMap.SetMatrix(ID_android.TransformMat, newTRS); //Moved to ProcessData for realtime update
                PosMapToMatPosMap.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, threadGroupsZ);
                e.Output.positionMap = positionsOut;
                e.Output.appliedMatrix = newTRS;
            }
            OutputReady?.Invoke(this, e);

        }

    }
}
