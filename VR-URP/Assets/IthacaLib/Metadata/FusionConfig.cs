/*#############################################################################################
#  ----   ContextManager   ----
#
# Project: IthacaEmetteurRecepteur by FrenchTouchFactory
# Resp. : Quentin
# Author(s): 
#   - Quentin de Cagny ( quentindecagny@gmail.com) French Touch Factory (100%)
# Date : created in dec 2023
# Description: Manages all possible context data related to a camera, a sequence, the local
#   application, such as path of the Sequence(s), values of the alignment matrices, serial 
#   number of the cams, intrinsics, DepthMode, framerate, position of the floor... 
#   Those context data or metadata are stored in several config structures like AppConfig
#   or CamConfig.
#   
# Parameters : -
# Note : -
# Tests : -
# Versions : 
#   1: first version with application pathes in AppConfig and a list of CamConfigs (without
#      KinectConfig yet).
#############################################################################################*/



using System;
using System.Collections.Generic;



namespace IthacaLib
{
    /// <summary>
    /// Class containing all the metadata linked to an exported ITK file used for serialization to a json file.
    /// </summary>
    [Serializable]
    public class FusionConfig
    {
        //public string ExportName;
        public ExportType exportType;
        public List<CamConfig> virtualCamConfigs;
        public int nbOfCams => virtualCamConfigs.Count;
        
    }
}