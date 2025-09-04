/*!------- CTVMultiToMonoSource ----------------------------------------------------------------------
* @brief 
*   (officially)SOURCE: --> RenderTexture
*
*-----------------------------------------------------------------------------------------------
* @copyright (c) 2024 French Touch Factory. All rights reserved.
* @project IthacaLib by French Touch Factory
* @resp Quentin
* @author 
*       Quentin de Cagny (quentindecagny@gmail.com) - French Touch Factory(67%)
* @date Creation date : 2023/11/22
* @date Latest version date : 2024/02/29
* @details 
*   MultiSources dispatcher for CombinedMulticamUnpacker 
*   Problem: Some Modules like CombinedMulticamUnpacker need to output several streams, but only
*       one target is possible for now.
*   Hypothesis: Make a multi target structure
*   Description: We only did it for one module, CombinedMulticamUnpacker cf. \@details
*   Limits: we cannot generalize from this script
*   Further Improvements: make a real multi target (and multi sources) module structure
* @param[in] a CombinedMulticamUnpacker that calls invokeOutputReady
* @param[out] one RenderTexture (for each cam)
* @note
*   - this is a hack to simulate a multi-source module (waiting for a future version)
* @test
*   
*---------------------------------------------------------------------------------------------*/

using System;
using UnityEngine;

namespace IthacaLib
{
    /// <summary>
    /// MultiSources dispatcher for CombinedMulticamUnpacker
    /// </summary>
    public class CTVMultiToMonoSource : SourceModuleBase<RenderTexture>
    {
        public override event EventHandler<DataEventArgs<RenderTexture>> OutputReady;

        public void InvokeOutputReady(System.Object sender, DataEventArgs<RenderTexture> RT)
        {
            OutputReady?.Invoke(sender, RT);
        }
    }
}
