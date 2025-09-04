/*#############################################################################################
# ---- AbstractFilter ----
# BRIDGE: (RenderTexture(PosMap), RenderTexture(Colors)) --> (RenderTexture(PosMap), RenderTexture(Colors))
#
#----------------------------------------
# Project: IthacaLib
# Resp. : Quentin
# Author(s): Phil Hoffmann (phil.hoffmann@ipk.fraunhofer.de) - Fraunhofer IPK
# Date : created in february 2024
# Description: Base class for filters (CutPlane, AABB, BoundingSphere etc)
# Parameters : -
# Note : -
# Tests : -
#############################################################################################*/

using IthacaLib.Common;
using UnityEngine;

namespace IthacaLib
{
    public abstract class AbstractFilter : BridgeModuleBase<GPUPointCloud, GPUPointCloud>
    {
        public ComputeShader filter;
        protected ComputeShader filterInstance;

        public bool isFilterEnabled = true;

        protected int threadGroupsX;
        protected int threadGroupsY;
        protected const int threadGroupsZ = 1;
        protected int kernelIndex;

        protected void InitFilter(GPUPointCloud frame)
        {
            var res = new VideoResolution(frame.positionMap.width, frame.positionMap.height);

            filterInstance = Instantiate(filter);
            threadGroupsX = Mathf.CeilToInt(res.width / 8);
            threadGroupsY = Mathf.CeilToInt(res.height / 8);
            kernelIndex = filterInstance.FindKernel("CSMain");
        }
       
    }
}
