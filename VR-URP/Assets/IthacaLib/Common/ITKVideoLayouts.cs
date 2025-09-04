/*#############################################################################################
#  ----   ITKVideoLayouts   ----
#
# Project: IthacaLib
# Resp. : Quentin
# Author(s): 
#   - Quentin de Cagny ( quentindecagny@gmail.com) French Touch Factory (100%)
# Date : created in May 2024
# Description: a helper class that calculates the best layout of videos to be packed or unpacked
#       from the ITK format
# Parameters : -
# Note : - 
# Tests : - Tests of packing and unpacking were done with up to 10 side-by-side videos
#############################################################################################*/
using System;
using UnityEngine;

namespace IthacaLib.Common
{
    public static class ITKVideoLayouts
    {
        public enum LayoutStyle
        {
            ///<summary>First multicam ITKVideo layout. Uni for: every resolutions must be equal. ZOrder: for order of the cams (by ascending number) following a Z layout inside final frame.</summary>
            UniZOrder
        }
        [Serializable]
        public struct ChannelLayout
        {
            public Vector2Int orig;
            public VideoResolution res;
        }
        [Serializable]
        public struct CombinedLayout
        {
            public ChannelLayout color;
            public ChannelLayout depth;
        }

        public static readonly VideoResolution[] KnownResolutions = {
            new VideoResolution(64, 64),
            new VideoResolution(80, 72),
            new VideoResolution(128, 128),
            new VideoResolution(160, 144),
            new VideoResolution(256, 256),
            new VideoResolution(320, 288),
            new VideoResolution(512, 512),
            new VideoResolution(640, 576),
            new VideoResolution(1280, 720),
            new VideoResolution(1024, 1024),
            new VideoResolution(1920, 1080),
            new VideoResolution(2048, 1536),
            new VideoResolution(2048, 2048),
            new VideoResolution(2560, 1440),
            new VideoResolution(3840, 2160),
            new VideoResolution(4096, 3072) };

        /// <summary>
        /// Stores a configuration of monocam and encapsulating frame resolutions. Made for UniZOrder layout type, ie with monocam res being homogeneous.
        /// </summary>
        public struct SimpleItkLayoutPack
        {
            public int finalWidth, finalHeight, nbCams, monoWidth, monoHeight;
            public RatioConstraints ratioConstraint;
        }

        public static SimpleItkLayoutPack[] _possibleResolutions;

        /// <summary>
        /// Enumeration of all the possible associations of resolutions:  final frame <--> monocam channel resolution. Used to guess to resolution of one cam inside an ITKVideo.
        /// </summary>
        public static SimpleItkLayoutPack[] PossibleResolutions
        {
            get
            {
                if (_possibleResolutions == null)
                {
                    _possibleResolutions = new SimpleItkLayoutPack[KnownResolutions.Length * 12 * 2];
                    int i = 0;
                    foreach (var ratioConstraint in (RatioConstraints[])Enum.GetValues(typeof(RatioConstraints)))
                        foreach (var resolution in KnownResolutions)
                        {
                            for (int nbCams = 1; nbCams <= 12; nbCams++)
                            {
                                CombinedLayout[] tmpLayouts = new CombinedLayout[nbCams];
                                for (int j = 0; j < tmpLayouts.Length; j++)
                                {
                                    tmpLayouts[j].color.res = resolution;
                                    tmpLayouts[j].depth.res = resolution;
                                }
                                _possibleResolutions[i] = new SimpleItkLayoutPack
                                {
                                    monoWidth = resolution.width,
                                    monoHeight = resolution.height,
                                    nbCams = nbCams,
                                    ratioConstraint = ratioConstraint
                                };
                                SetStreamingLayouts(
                                    nbCams,
                                    resolution,
                                    ITKVideoLayouts.LayoutStyle.UniZOrder, ratioConstraint,
                                    ref tmpLayouts,
                                    out _possibleResolutions[i].finalWidth, out _possibleResolutions[i].finalHeight);
                                //Debug.Log("Q:::there is a new possibleRes : " + _possibleResolutions[i].finalWidth +"x"+ _possibleResolutions[i].finalHeight +" cams:" + _possibleResolutions[i].nbCamsMin +" --> mono: " + _possibleResolutions[i].monoWidth + "x" + _possibleResolutions[i].monoHeight);
                                i++;
                            }
                        }

                }
                return _possibleResolutions;
            }
        }

        public enum RatioConstraints
        {
            /// <summary>No modification of resolution to fit some ratio with blank stripe. It is what it is.</summary>
            NoExtra,
            /* ForceSquareOr16_9, */
            /// <summary>Forces ratio to most common ratios, Square (1:1), 4:3 or 16:9. Like Youtube.</summary>
            ForceSquare4_3or16_9
            /*, ForceStandardsRatios*/
        }

        ///<summary>Sets the origin position of each channel and camera within the combinated multicam frame.</summary>
        public static void SetStreamingLayouts(
            in int nbCams,
            in VideoResolution vrin,
            in LayoutStyle style,
            in RatioConstraints ratioConstraint,
            ref CombinedLayout[] layouts,
            out int finalWidth, out int finalHeight)
        {
            finalWidth = finalHeight = -1; // Error code
            switch (style)
            {
                case LayoutStyle.UniZOrder:
                    (int, int) colXrow = DetermineBestColumnRowLayout(new VideoResolution(layouts[0].color.res.width, layouts[0].color.res.height), nbCams);
                    (int, int) currColXrow = (1, 1);

                    for (int c = 0; c < nbCams; c++)
                    {
                        layouts[c].depth.orig = new Vector2Int((currColXrow.Item1 - 1) * layouts[0].color.res.width * 2, (currColXrow.Item2 - 1) * layouts[0].color.res.height);
                        //layouts[c].depth.res = new VideoResolution(depthWidth[0], depthHeight[0]);
                        layouts[c].color.orig = new Vector2Int((((currColXrow.Item1 - 1) * 2) + 1) * layouts[0].color.res.width, (currColXrow.Item2 - 1) * layouts[0].color.res.height);
                        //layouts[c].color.res = new VideoResolution(colorWidth[0], colorHeight[0]);

                        currColXrow.Item1++;
                        if (currColXrow.Item1 > colXrow.Item1) { currColXrow.Item1 = 1; currColXrow.Item2++; }
                    }

                    finalWidth = layouts[0].color.res.width * 2 * colXrow.Item1;
                    finalHeight = layouts[0].color.res.height * colXrow.Item2;

                    break;
            }

            if (finalWidth < finalHeight) { Debug.Log("Houston on a un pbm : ncams=" + nbCams + "   vrin=" + vrin.width + "x" + vrin.height + "   rConstr=" + ratioConstraint); }
            //Debug.Log("ratio " + ratioConstraint);
            //Debug.Log("finalWidth " + finalWidth);
            //Debug.Log("finalHeight " + finalHeight);
            if (ratioConstraint != RatioConstraints.NoExtra)
            {
                AddStripeToFitRatio(ratioConstraint, ref finalWidth, ref finalHeight);
            }
        }

        /// <summary>
        /// Will propose a probable resolution for monocam frame (ex: 320x288 for a depth channel) given the resolution of the ITKVideo and the number of cams that should be found inside.
        /// </summary>
        /// <param name="nbCams">Number of cam in the multicam video.</param>
        /// <param name="finalWidth">Width of the global frame encapsulating all the cameras.</param>
        /// <param name="finalHeight">Height of the global frame encapsulating all the cameras.</param>
        /// <param name="style">Method used to determine the layout. For now only UniZOrder exists.</param>
        /// <param name="ratioConstraint">Constraint on ratio. Not really useful in this version but future versions could need it.</param>
        /// <param name="monoRes">Resolution proposed by this function for a monocam channel.</param>
        /// <returns></returns>
        public static int GuessMonoCamResolution(
            in int nbCams,
            in int finalWidth, in int finalHeight,
            in LayoutStyle style,
            out RatioConstraints ratioConstraint,
            out VideoResolution monoRes
            )
        {
            int toReturn = -1;
            if (style != LayoutStyle.UniZOrder) { Debug.LogError("Only UniZOrder layout style is taken into account in this version."); }
            if (finalHeight > finalWidth) Debug.LogWarning("TryGuessMonoCamResolution is not sure with portrait resolutions... (yet)");

            monoRes = new VideoResolution(0, 0);
            ratioConstraint = RatioConstraints.NoExtra;

            foreach (var ps in PossibleResolutions)
            {
                if (finalWidth == ps.finalWidth && finalHeight == ps.finalHeight && nbCams == ps.nbCams)
                {
                    if (toReturn >= 0)
                        if (monoRes.width != ps.monoWidth || monoRes.height != ps.monoHeight)
                            return -2; //At least 2 different matching resolutions were found. It's impossible to decide. First one is proposed but return value is -2.
                    monoRes.width = ps.monoWidth;
                    monoRes.height = ps.monoHeight;
                    ratioConstraint = ps.ratioConstraint;

                    toReturn++;
                    //Debug.Log("Q:: toreturn= " + toReturn + " with monores=" + monoRes.width + "x" + monoRes.height);
                }
            }
            //Debug.Log("Q:: toreturn= " + toReturn + " with monores=" + vrout.width +"x"+ vrout.height);

            return toReturn;
        }


        static (int, int) DetermineBestColumnRowLayout(VideoResolution _chRes, int nbCams)
        {
            int nbColumns;
            int nbRows;
            int allCamWidth;
            int allCamHeight;

            //Lets determine all ratios for all number of rows possible:
            float[] ratios = new float[nbCams];
            int[] scoresR = new int[nbCams];
            int[] scoresW = new int[nbCams];
            int best = 0;
            for (int i = 0; i < nbCams; i++)
            {
                nbRows = i + 1;
                nbColumns = (int)Mathf.Ceil((float)nbCams / (float)nbRows);

                allCamWidth = _chRes.width * 2 * nbColumns;
                allCamHeight = _chRes.height * nbRows;
                ratios[i] = (float)allCamWidth / (float)allCamHeight;

                scoresR[i] = ScoreByRatio(ratios[i]);
                int wasted = nbRows * nbColumns - nbCams;
                scoresW[i] = ScoreByWasted(wasted, nbCams);
                if (scoresR[i] + scoresW[i] >= scoresR[best] + scoresW[best]) best = i;
            }

            nbRows = best + 1;
            nbColumns = (int)Mathf.Ceil((float)nbCams / (float)nbRows);

            return (nbColumns, nbRows);
        }

        /// <summary> Sets a weight depending of the ratio of the tested layout. For example a ratio near 16/9 will be prefered. </summary>
        static int ScoreByRatio(float ratio)
        {
            if (ratio < 0.8f) return 0;
            if (ratio < 1f) return 1;
            if (ratio < (4f / 3f)) return 7;
            if (ratio < (16f / 9f)) return 8;
            if (ratio < (2048f / 1080f)) return 10;
            if (ratio < 2f) return 8;
            if (ratio < 2.5f) return 7;
            if (ratio < 3f) return 4;
            if (ratio >= 3f) return 0;
            return 0;  //Impossible
        }

        /// <summary> Determines a weight depending of the number of unused "cells" in the rowXcolumn table tested.</summary>
        /// <param name="wasted">Number of empty cells</param>
        /// <param name="nbCams">Number of cams</param>
        static int ScoreByWasted(int wasted, int nbCams)
        {
            if (wasted == 0) return 12; //Slight bonus for zero wasted space
            return (nbCams - wasted) * 10 / nbCams;
        }

        static void AddStripeToFitRatio(in RatioConstraints rc, ref int finalWidth, ref int finalHeight)
        {
            float origRatio = (float)finalWidth / (float)finalHeight;
            int dbgFWidth = finalWidth, dbgFHeight = finalHeight; //For debug
            switch (rc)
            {
                //Only Square 1:1, 4:3 or 16:9 for now (Youtube's most common ratios)
                //case RatioConstraints.ForceSquareOr16_9:
                //    if (origRatio < 1.0f)
                //        Debug.LogError("Trying to add blank horizontal stripe: Unable to fit in square ratio because best determined ratio is < 1");
                //    if (origRatio < (16.0f / 9.0f))
                //        finalHeight = finalWidth;
                //    if (origRatio >= (16.0f / 9.0f))
                //        finalHeight = finalWidth * 9 / 16;
                //    break;
                case RatioConstraints.ForceSquare4_3or16_9:
                    if (origRatio >= (16.0f / 9.0f))
                        finalHeight = finalWidth * 9 / 16;
                    if (origRatio < (16.0f / 9.0f))
                        finalHeight = finalWidth * 3 / 4;
                    if (origRatio < (4.0f / 3.0f))
                        finalHeight = finalWidth;
                    if (origRatio < 1.0f)
                        Debug.LogWarning("Trying to add blank horizontal stripe: Unable to fit in square ratio because best determined ratio ("+origRatio+") is < 1 (finalRes=" + dbgFWidth +"x"+ dbgFHeight);
                    break;
            }
        }

        ///<summary>Check if all sources have the same resolution.</summary>
        public static bool CheckHomogeneousResolutions(in int nbCams, in CombinedLayout[] layouts)
        {
            VideoResolution vr = new VideoResolution(layouts[0].color.res.width, layouts[0].color.res.height);
            for (int i = 1; i < nbCams; i++)
            {
                if (vr != new VideoResolution(layouts[i].color.res.width, layouts[i].color.res.height) || vr != new VideoResolution(layouts[i].depth.res.width, layouts[i].depth.res.height))
                {
                    Debug.LogWarning("Resolution of the camera " + i + " (color:" + layouts[i].color.res.width + "x" + layouts[i].color.res.height + ",depth:" + layouts[i].depth.res.width + "x" + layouts[i].depth.res.height
                        + ") doesn't match the resolution of the color of the camera 0 (color:" + layouts[0].color.res.width + "x" + layouts[0].color.res.height + ",depth:" + layouts[0].depth.res.width + "x" + layouts[0].depth.res.height
                        + "). In this version of this module, all cameras - color AND depth channels - must be in the same resolution");
                    return false;
                }
            }
            return true;
        }


        static void TestUniZOrder()
        {
            float rat = (float)1920 / (float)1080;
            Debug.Log("TEST ratio 1920x1080: " + ScoreByRatio(rat));

            VideoResolution testRes = new VideoResolution(320, 288);
            Debug.Log("320,288");
            for (int c = 1; c <= 10; c++) DetermineBestColumnRowLayout(testRes, c);
            DetermineBestColumnRowLayout(testRes, 77);

            testRes = new VideoResolution(512, 512);
            Debug.Log("512,512");
            for (int c = 1; c <= 10; c++) DetermineBestColumnRowLayout(testRes, c);
            DetermineBestColumnRowLayout(testRes, 77);

            testRes = new VideoResolution(256, 512);
            Debug.Log("256,512");
            for (int c = 1; c <= 10; c++) DetermineBestColumnRowLayout(testRes, c);
            DetermineBestColumnRowLayout(testRes, 77);
        }

    }
}
