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



using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace IthacaLib
{

    [Serializable]
    public enum DepthinColorMethod
    {
        Undefined,
        Encode255,
        BiTriangle,
        HSVHue,
        Encode255plus
    }

    [Serializable]
    public struct DepthInColorEncoding
    {
        public DepthinColorMethod Method;
        [Tooltip("Parameters for the encoding method. ex: -encodeshift 750")]
        public string Parameters;
        public int GetEncodeShift()
        {
            string[] paramarray = Parameters.Split(" ");
            for (int i = 0; i < paramarray.Length; i++)
            {
                if (paramarray[i] == "-encodeshift" || paramarray[i] == "-es")
                    return int.Parse(paramarray[i + 1], System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
            }
            return 0; // if not found, default value of 0 is returned
        }
        public float GetEncodeMultiplier()
        {
            string[] paramarray = Parameters.Split(" ");
            for (int i = 0; i < paramarray.Length; i++)
            {
                if (paramarray[i] == "-encodeMultiplier" || paramarray[i] == "-em")
                    return float.Parse(paramarray[i + 1], System.Globalization.CultureInfo.InvariantCulture.NumberFormat);
            }
            return 1; // if not found, default value of 1 is returned as it is a multiplier
        }
    }

    [Serializable]
    public enum DeviceState
    {
        Connected, Opened, Started, Unknown
    }
    [Serializable]
    public enum ExportType
    {
        Undefined,
        SideBySide,
        OrthoProjected,
        PerspectiveProjected
    }
    [Serializable]
    public class ItkContext
    {
        #region Serialized fields
        [TextArea]
        public string warning = "ContextManager is in an early ALPHA version. All parameters could be lost when a new version arrives. A JSON copy of the parameters of a scene is automatically saved (at Start) in LocalDataFolder/SavedContext/ for backup, just in case. But backwards compatibility is not guaranteed at this stage.";
        [Tooltip("Settings that are related to the application.")]
        [SerializeField]
        public AppConfig AppConfig;
        [Tooltip("Settings that are specific for each cam.")]
        [SerializeField]
        public CamConfig[] CamConfigs;

        [Space(40)]
        [Header("DEPRECATED - Report all those settings in AppConfig above, they will be lost in next version -----------------------------------------------------------")]
        [Tooltip("DEPRECATED - Version of the ItkContext struct definition. Useful for backwards compatibility, notably regarding its JSON format.")]
        public int Version = 1;
        [Tooltip("DEPRECATED - Local path of the Data folder(in which sequences are stored)")]
        public string LocalDataFolder;
        [Tooltip("DEPRECATED - Local path of the folder that contains XYTable files.")]
        public string XYTableFolder;
        [Tooltip("DEPRECATED - Name of the current sequence.")]
        public string Sequence;
        [Tooltip("DEPRECATED - Folder of the alignment matrices. Exple: align/cams/ .")]
        public string AlignFolder;
        [Tooltip("DEPRECATED - Basename of the current alignment matrix of a sequence : exple: alignmatrixD_[%cam]. Note that %cam will be determined by the number between parenthesis in the GameObject's name")]
        public string AlignBaseName;
        [Tooltip("DEPRECATED - Common transformation matrix that will transform the whole model together.")]
        public Matrix4x4 CommonTransformMatrix;

        [Tooltip("DEPRECATED - If every CAMs use the same method and parameters to encode Depth in Color values, put it here, for simplicity. If defined, settings for each CAM in CamConfig below will overwrite this common setting.")]
        public DepthInColorEncoding CommonDepthInColorEncoding;
        //[Tooltip("Specifies which layout is used for Color and Depth side by side. ex: DepthLeftColorRight, which is default by the way.")]
        //public string CombinedLayout;
        [HideInInspector]
        public FusionConfig FusionConfig = null;

        #endregion

        #region Public accessors

        public int CamCount
        { get => CamConfigs.Length; }
        public int ActiveCamCount => FusionConfig?.virtualCamConfigs.Count ?? AppConfig.ActiveCameras.Length;
        /// <summary>
        /// Gets the DepthInColorEncoding for the cam specified by its UserAssignedCameraIndex <paramref name="cam"/>. If specific method is not defined for this cam, the common one is returned.
        /// </summary>
        /// <param name="cam">UserAssignedCameraIndex (and not index in the camConfig's list in Context)</param>
        public DepthInColorEncoding DepthInColorEncoding(int cam)
        {
            //Find the index in list of the corresponding UserAssignedCameraIndex cam.
            int index = IndexInListFromUserIndex(cam);

            if (index != -1 && CamConfigs[index].DepthInColorEncoding.Method != DepthinColorMethod.Undefined)
                return CamConfigs[index].DepthInColorEncoding;
            else
                return AppConfig.CommonDepthInColorEncoding;
        }

        /// <summary>
        /// Access a camConfig by the UserAssignedCameraIndex (instead of the index in camConfigs list, which is not the same)
        /// </summary>
        /// <param name="cam">UserAssignedCameraIndex</param>
        /// <returns>The camConfig corresponding to the UserAssignedCameraIndex, or null if not found.</returns>
        public CamConfig CamConfig(int cam)
        {
            int index = IndexInListFromUserIndex(cam);
            if (index != -1 && index < CamConfigs.Length)
                return CamConfigs[index];

            return null;
        }
        #endregion


        /// <summary>
        /// Find the index in list of the corresponding UserAssignedCameraIndex cam.
        /// </summary>
        /// <param name="userIndex">UserAssignedCameraIndex. i.e. index assigned mannualy by the user during recording. Cam (0) is often the master.</param>
        /// <returns>The index in camConfigs list, or -1 if nothing is found.</returns>
        public int IndexInListFromUserIndex(int userIndex)
        {
            for (int i = 0; i < CamConfigs.Length; i++)
                if (CamConfigs[i].UserAssignedCameraIndex == userIndex)
                    return i;
            return -1; ;
        }

        public string GetMKVPath()
        {
            return AppConfig.mkvPathOverride != "" ? AppConfig.mkvPathOverride : AppConfig.LocalDataFolder + AppConfig.Sequence + "\\cap\\mkv\\" + AppConfig.Sequence;
        }
    }


    /// <summary>
    /// Manager for different config data that can be available in a centralized manner.
    /// </summary>
    public class ContextManager : MonoBehaviour
    {
        public static ContextManager instance;
        [Tooltip("The current context (in future versions, there could be a list of different contexts, and this one will be the active and current one).")]
        public ItkContext currentContext;

        private List<IInjectable> modules = new(); 

        #region Unity framework logic

        void Start()
        {
            AutoSaveToJSON();
        }
        public static void SaveExoprtDataToJSON(string baseName, string dirPath)
        {
            try
            {
                DirectoryInfo dirInfo = new DirectoryInfo(dirPath);
                if (!dirInfo.Exists) dirInfo.Create();

                string path = Path.Combine(dirPath, baseName) + ".json";
                Debug.Log(JsonUtility.ToJson(instance.currentContext));
                File.WriteAllText(path, JsonUtility.ToJson(instance.currentContext));
            }
            catch 
            {
                Debug.LogWarning("No export metadata was created as no ContextManager was found in the scene.");
            }
        }

        void OnValidate()
        {
            if (currentContext != null && currentContext.CamConfigs != null)
            {
                CamConfig newcc = new CamConfig();

                foreach (CamConfig cc in currentContext.CamConfigs)
                {
                    cc.Version = newcc.Version;//Dirty trick to keep default value when an element is added in Inspector. Unity weirdly sets default value of each type instead
                                               //of default value defined in the class of the element when this element is part of an array and we click + in Inspector. This
                                               //trick is made to overcome this problem. Thanks Unity...
                    if (string.IsNullOrEmpty(cc.alignmentMatrixPath) && !string.IsNullOrEmpty(currentContext.AppConfig.LocalDataFolder) && !string.IsNullOrEmpty(currentContext.AppConfig.AlignFolder) && !string.IsNullOrEmpty(currentContext.AppConfig.AlignBaseName))
                        {
                            cc.alignmentMatrixPath = Path.Combine(currentContext.AppConfig.LocalDataFolder, currentContext.AppConfig.Sequence, currentContext.AppConfig.AlignFolder, currentContext.AppConfig.AlignBaseName) + (currentContext.AppConfig.AlignBaseName[^1] != '_' ? "_": "") + cc.UserAssignedCameraIndex + ".json";
                        }
                    if (currentContext.AppConfig.ActiveCameras.Contains(cc.UserAssignedCameraIndex) && !string.IsNullOrEmpty(cc.alignmentMatrixPath))
                    {
                        try
                        {
                            cc.alignmentMatrix = JsonUtility.FromJson<Matrix4x4>(File.ReadAllText(cc.alignmentMatrixPath));
                        }
                        catch (Exception ex) { Debug.LogException(ex); }
                    }
                }
            }
            foreach(var module in modules)
            {
                module.GetSettingsByContext();
            }

        }
        #endregion

        /// <summary> Call this to force create the static instance of ContextManager.</summary>
        public static void TryFindInstance()
        {
            if (instance == null)
                instance = FindObjectOfType<ContextManager>();

            if (instance == null) throw new Exception("No ContextManager found. Create an empty object and give him a ContextManager component. Or uncheck all \"Settings By Context\" on your modules.");
        }

        /// <summary>
        /// Alpah version solution for keeping a backup copy of Context metadatas of a scene in case of settings loss when implementation changes.
        /// </summary>
        public void AutoSaveToJSON()
        {
            Debug.Log(JsonUtility.ToJson(this));

            DirectoryInfo dirInfo = new DirectoryInfo(currentContext.AppConfig.LocalDataFolder + "/SavedContext/");
            if (!dirInfo.Exists) dirInfo.Create();

            string tag = dirInfo.FullName + UnityEngine.SceneManagement.SceneManager.GetActiveScene().name + "_Context_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ".json";

            //System.IO.FileInfo fileInfo = new System.IO.FileInfo(tag);
            //if (fileInfo.Exists) { }
            
            Debug.Log("Context save path : " + tag);

            File.WriteAllText(tag, JsonUtility.ToJson(this));
        }

        public static void AddToInjectables(IInjectable injectable)
        {
            instance.modules.Add(injectable);
        }

        public static void RemoveInjectable(IInjectable injectable)
        {
            instance?.modules?.Remove(injectable);
        }
    }

    ////Experiment of attribute that could give any property of any class the ability to take its value from context. Not implemented yet
    //// the idea is that a GetSettingsByContext function oof a module will always be the same : list all filed that have this attribute and update them from context
    //public class SetByContextAttribute : Attribute
    //{
    //    public string path;

    //    public SetByContextAttribute(string path)
    //    {
    //        this.path = path;
    //    }
    //}


}