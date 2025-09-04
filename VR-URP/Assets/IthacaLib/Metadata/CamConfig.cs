/*#############################################################################################
#  ----   CamConfig   ----
#
# Project: IthacaEmetteurRecepteur by FrenchTouchFactory
# Resp. : Quentin
# Author(s): 
#   - Kuba Siebeneichen & Quentin de Cagny ( quentindecagny@gmail.com) French Touch Factory (100%)
# Date : created in sept 2023
# Description: Metadata structure containing parameters and settings related to one camera
#   during a recording or an export. See also KinectConfig that derives from this.
# Parameters : -
# Note : -
# Tests : -
# Versions : 
#   - 1 : made for the Recorder in early 2023 (by Kuba)
#   - 2 : remodeled and completed for the first ContextManager in Ithacalib. 
        Added DepthInColorEncoding, Near and Far Clip and Version (01/2024)
#############################################################################################*/


using System;
using UnityEngine;

namespace IthacaLib
{
    /// <summary>
    /// Metadata structure containing parameters and settings related to one camera during a recording or an export. See also KinectConfig that derives from this.
    /// </summary>
    [Serializable]
    public class CamConfig
    {
        public int UserAssignedCameraIndex;
        public string SerialNumber;
        public DeviceStates DeviceState = DeviceStates.Unknown;
        public CamModel Model;
        [Tooltip("Method and parameters of encoding of 16bit Depth in 3x8bit Color data.")]
        public DepthInColorEncoding DepthInColorEncoding;
        public Matrix4x4 alignmentMatrix;
        public string alignmentMatrixPath;
        public ExportScale exportScale;
        public float projectonScreenWidth;
        public float projectonScreenHeight;
        public float screenCameraDistance;
        public Common.ITKVideoLayouts.CombinedLayout layout;
        [Tooltip("Version of this data structure and its file representation. Useful for backwards compatibility.")]
        public int Version = 3;
        [Tooltip("Index assigned by the system to the camera as a USB device.")]
        public int DeviceIndex;
        [Tooltip("Optional name given by user.")]
        public string CameraAlias;

        [Tooltip("Foreground clipping distance. In millimeter from the sensor of the camera.")]
        public int NearClip;
        [Tooltip("Background clipping distance. In millimeter from the sensor of the camera.")]
        public int FarClip;

        public CamConfig Copy()
        {
            var config = new CamConfig();
            config.UserAssignedCameraIndex = UserAssignedCameraIndex;
            config.SerialNumber = SerialNumber;
            config.DeviceState = DeviceState;
            config.Model = Model;
            config.DepthInColorEncoding = DepthInColorEncoding;
            config.alignmentMatrix = alignmentMatrix;
            config.alignmentMatrixPath = alignmentMatrixPath;
            config.exportScale = exportScale;
            config.projectonScreenHeight = projectonScreenHeight;
            config.projectonScreenWidth = projectonScreenWidth;
            config.screenCameraDistance = screenCameraDistance;
            config.layout = layout;
            config.Version = Version;
            config.CameraAlias = CameraAlias;
            config.NearClip = NearClip;
            config.FarClip = FarClip;
            return config;
        }

        public enum CamModel
        {
            AzureKinect,
            OrbbecFemtoMega,
            Virtual // virtual camera from a fusion
        }
        public enum ExportScale
        {
            UnityScale,
            KinectScale,
        }
        public enum DeviceStates
        {
            Connected,
            Opened,
            Started,
            Disconnected,
            Unknown,
            Error
        }
    }
}