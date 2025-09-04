/*#############################################################################################
#  ----   AppConfig   ----
#
# Project: IthacaEmetteurRecepteur by FrenchTouchFactory
# Resp. : Quentin
# Author(s): 
#   - Quentin de Cagny ( quentindecagny@gmail.com) French Touch Factory (100%)
# Date : created in jan 2024
# Description: Metadata structure containing parameters and settings related to the application
#   such as pathes, name of the sequence, other settings.
# Parameters : -
# Note : -
# Tests : -
# Versions : 
#   - 1 : made the first ContextManager in Ithacalib. 
#############################################################################################*/




using System;
using UnityEngine;

namespace IthacaLib
{
    /// <summary>
    /// Metadata structure containing parameters and settings related to the application such as pathes, name of the sequence, other settings.
    /// </summary>
    [Serializable]
    public class AppConfig
    {
        [Tooltip("Local path of the Data folder(in which sequences are stored)")]
        public string LocalDataFolder;
        [Tooltip("Local path of the folder that contains XYTable files.")]
        public string XYTableFolder;
        [Tooltip("Name of the current sequence.")]
        public string Sequence;
        [Tooltip("Path to use to find the MKV files you want to play. If left empty the default path will be constructed using the LocalDataFolder and the Sequence fields.")]
        public string mkvPathOverride;
        [Tooltip("Folder of the alignment matrices. Exple: align/cams/ .")]
        public string AlignFolder;
        [Tooltip("Basename of the current alignment matrix of a sequence : exple: \"alignmatrixD_\" . Note that the number of the cam will be added.")]
        public string AlignBaseName;
        [Tooltip("The user indexes of the active cameras/mkv files that should be used in this context")]
        public int[] ActiveCameras;
        [Tooltip("Common transformation matrix that will transform the whole model together")]
        public Matrix4x4 CommonTransformMatrix;

        [Tooltip("If every CAMs use the same method and parameters to encode Depth in Color values, put it here, for simplicity. If defined, settings for each CAM in CamConfig below will overwrite this common setting.")]
        public DepthInColorEncoding CommonDepthInColorEncoding;
        //[Tooltip("Specifies which layout is used for Color and Depth side by side. ex: DepthLeftColorRight, which is default by the way.")]
        //public string CombinedLayout;

        [Tooltip("Version of the AppConfig definition. Useful for backwards compatibility, notably regarding its JSON format.")]
        public int Version = 1;
    }
}