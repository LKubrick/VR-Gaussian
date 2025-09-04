/*!------------- CombinedMulticamUnpacker ------------------------------------------------------
* @brief 
*     BRIDGE, but officially TARGET: RenderTexture --> nbcams x RenderTexture
*     (mono-TARGET, but nbCams SOURCE components are automatically added for outputs)
*
*-----------------------------------------------------------------------------------------------
* @copyright (c) 2024 French Touch Factory. All rights reserved.
* @project IthacaLib by French Touch Factory
* @resp Quentin
* @author           
*   Quentin de Cagny (quentindecagny@gmail.com) - French Touch Factory
* @date created in october 2023, last modified in early july 2024
* @details 
*    Takes a RenderTexture of multicam combined layout (with color encoded depth and color 
*    side by side for each cam) and converts it into nbcam RenderTextures containing 
*    one combined layout for each cam.
* @param
* @note
*   - a hack is used to have a multi-source : several mono-source components CTVMultiToMonoSource 
*     are added automatically. They can be used to link to the following targets.
*   - now handles ItkVideoLayouts
* @test
*---------------------------------------------------------------------------------------------*/

using IthacaLib.Common;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Collections.Generic;
using System.Collections;

namespace IthacaLib
{
    /// <summary>
    /// Takes a RenderTexture of multicam combined layout (with color encoded depth and color
    /// side by side for each cam) and converts it into nbcam RenderTextures containing
    /// one combined layout for each cam.
    /// </summary>
    [ExecuteInEditMode]
    public class CombinedMulticamUnpacker : TargetModuleBase<RenderTexture>
    {

        [Tooltip("Number of cameras packed in the source video (ONLY 2 and 4 ARE SUPPORTED IN THIS BETA VERSION)")]
        public int numberOfCams = 4;
        [Tooltip("List of the out sources. This is necessary to split this module into a multi-source module. Each CTVMultiToMonoSource is a mono source")]
        public List<CTVMultiToMonoSource> MOSlist;

        [Tooltip("Style of layout. Here for compatibility with future, more complex, layouts (that will take multi different entry resolutions for example). For now only UniZOrder exists.")]
        public ITKVideoLayouts.LayoutStyle layoutStyle = ITKVideoLayouts.LayoutStyle.UniZOrder;
        [Tooltip("If true, will attempt to determine out Resolution and ratioConstraint from inResolution and number of cams.")]
        public bool guessMonoCamResolution = true;
        [Tooltip("Use this to indicate that an EXTRA blank horizontal stripe was added to meet certain standard ratios.")]
        public ITKVideoLayouts.RatioConstraints ratioConstraint;

        private VideoResolution inResolution;
        public ITKVideoLayouts.CombinedLayout[] layouts;

        [Tooltip("Resolution of Depth (or Color) stream inside source video (ONLY 320x288 or 640x576 (NFOV, binned or unbinned, in ColorToDepth mode) ARE SUPPORTED IN THIS BETA VERSION)")]
        public VideoResolution outResolution = new VideoResolution(320, 288);

        RenderTexture[] outRT;

        // Define shader for WebGL copy without graphics.copytexture
        //Material copyMaterial;

#region Unity framework logic
        protected override void OnValidate()
        {
            if (settingsByContext)
                GetParamsFromContext();

            AdjustMultiSources();

            base.OnValidate();
        }

        public void AdjustMultiSources()
        {
            if (MOSlist == null) MOSlist = new List<CTVMultiToMonoSource>();

            if (numberOfCams > MOSlist.Count)
            {
                while (MOSlist.Count < numberOfCams)
                {
                    MOSlist.Add(gameObject.AddComponent<CTVMultiToMonoSource>());
                }
                return;
            }
            else  // numberOfCams < MOSlist.Count
            {
                int i = MOSlist.Count-1;

                while (i > numberOfCams-1)
                {
                    StartCoroutine(DestroyGO(MOSlist[i]));
                    MOSlist.RemoveAt(i);
                    i--;
                }
            }
            return;
        }

        // Special function for executing DestroyImmediate in a Unity's Coroutine
        IEnumerator DestroyGO(Component go)
        {
            yield return new WaitForSeconds(0);
            DestroyImmediate(go);
        }

        private void GetParamsFromContext()
        {
            Debug.Log("GetParamsFromContext");
            try { numberOfCams = ContextManager.instance.currentContext.CamCount; } 
            catch { Debug.Log("Context does not provide numberOfCams."); }
        }
#endregion Unity framework logic

        public override void HotInit(DataEventArgs<RenderTexture> e)
        {
            #region DEBUG CopyTexture capabilities
            //DEBUG: CopyTexture capabilities check on local system
            string modesUnsupported = "";
            if (!SystemInfo.copyTextureSupport.HasFlag(UnityEngine.Rendering.CopyTextureSupport.Basic))
                modesUnsupported += " Basic ";
            if (!SystemInfo.copyTextureSupport.HasFlag(UnityEngine.Rendering.CopyTextureSupport.TextureToRT))
                modesUnsupported += " TextureToRT ";
            if (!SystemInfo.copyTextureSupport.HasFlag(UnityEngine.Rendering.CopyTextureSupport.DifferentTypes))
                modesUnsupported += " DifferentTypes ";
            if (!SystemInfo.copyTextureSupport.HasFlag(UnityEngine.Rendering.CopyTextureSupport.RTToTexture))
                modesUnsupported += " RTToTexture ";
            if (!SystemInfo.copyTextureSupport.HasFlag(UnityEngine.Rendering.CopyTextureSupport.Copy3D))
                modesUnsupported += " Copy3D ";
            if (modesUnsupported != "") Debug.LogWarning("CopyTextureSupport for CombinedMulticamUnpacker does not support those modes on this device/machine : " + modesUnsupported);
            #endregion
            RenderTexture initRT = e.Output;
            RenderTextureDescriptor descriptor = initRT.descriptor;
            inResolution = new VideoResolution(descriptor.width, descriptor.height); //Set the inResolution automatically before modifying resolution of descriptor for output RT configuration

            ////ALPHA: Check only supported inResolution is correctly set
            //if (!(inResolution.width == 640 && inResolution.height == 640) &&
            //    !(inResolution.width == 1280 && inResolution.height == 720) &&
            //    !(inResolution.width == 1280 && inResolution.height == 1280) &&
            //    !(inResolution.width == 2560 && inResolution.height == 1440))
            //    Debug.LogError("CombinedMulticamUnpacker: ONLY 640x640 (2cam bined), 1280x720 (4cam bined), 1280x1280 (2cam unbin), 2560x1440 (4cam unbin, 1152 is temporarilly tolerated) FOR inResolution(s) ARE SUPPORTED IN THIS BETA VERSION (inResolution is currently :" + inResolution.width + "x" + inResolution.height);

            //Auto outResolution from ITKVideoLayout
            if (guessMonoCamResolution)
            {
                layouts = new ITKVideoLayouts.CombinedLayout[numberOfCams];
                int result = ITKVideoLayouts.GuessMonoCamResolution(in numberOfCams, in inResolution.width, in inResolution.height, in layoutStyle, out ratioConstraint, out outResolution);
                if (result == -2) Debug.LogError("Automatic guessing of monocam resolution inside multi combined ITKVideo: At least 2 different matching resolutions were found. It's impossible to decide. First one is proposed, but highly unsure !!");
            }

            for (int i = 0; i < layouts.Length; i++)
                layouts[i].color.res = layouts[i].depth.res = outResolution;

            ITKVideoLayouts.SetStreamingLayouts(in numberOfCams, in outResolution, in layoutStyle, in ratioConstraint, ref layouts, out int finalWidth, out int finalHeight);
        ///*OK:*/ Debug.Log("Q:: Check resolution match : inRes= " + inResolution.width + "x" + inResolution.height + " - calculated finalRes= " + finalWidth + "x" + finalHeight + "  (mono:" + outResolution.width + "x" + outResolution.height + ")");


            descriptor.width = outResolution.width * 2; descriptor.height = outResolution.height;
            outRT = new RenderTexture[numberOfCams];
            for (int i=0; i<numberOfCams; i++)
            {
                outRT[i] = new RenderTexture(descriptor);
            }

            base.HotInit(e);

        }



        public override void ProcessData(object sender, DataEventArgs<RenderTexture> e)
        {
            if (!enabled) return;

            if (!IsHotInitComplete) HotInit(e);



            for (int i = 0; i < numberOfCams; i++)
            {
                //Debug.Log("Process : " + layouts[i].depth.orig.x + ":" + layouts[i].depth.orig.y + "  WxH:" + layouts[i].depth.res.width * 2 + "x" + layouts[i].depth.res.height);



#if UNITY_STANDALONE_WIN || UNITY_ANDROID
                Graphics.CopyTexture(e.Output, 0, 0, layouts[i].depth.orig.x, layouts[i].depth.orig.y, layouts[i].depth.res.width * 2, layouts[i].depth.res.height, outRT[i], 0, 0, 0, 0);

#elif UNITY_WEBGL
                     
                Graphics.Blit(e.Output, outRT[i], new Vector2((float)outRT[i].width/ e.Output.width, (float)outRT[i].height / e.Output.height), new Vector2((float)layouts[i].depth.orig.x/ e.Output.width, (float)layouts[i].depth.orig.y/ e.Output.height));

#endif


            }

            for (int i = 0; i < numberOfCams; i++)
            {
                FrameInfo frameInfo = new(i, e.FrameInfo.frameID, e.FrameInfo.timestamp);
                MOSlist[i].InvokeOutputReady(this, new(outRT[i], frameInfo));

            }
        }

        public override void CleanUp()
        {
            if (outRT != null)
                foreach (RenderTexture rt in outRT)
                {
                    rt.Release();
                }
            outRT = null;

            base.CleanUp();
        }
    }
}
