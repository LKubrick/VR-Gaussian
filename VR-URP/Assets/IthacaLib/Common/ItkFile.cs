/*#############################################################################################
#  ----   ItkFile   ----
#
# Project: IthacaEmetteurRecepteur by FrenchTouchFactory
# Resp. : Quentin
# Author(s): 
#   - Quentin de Cagny ( quentindecagny@gmail.com) French Touch Factory (100%)
# Date : created in fev 2024
# Description: a wrapper for System.IO.File basic reading functions that hides the use of
#       BetterStreamingAssets (BSA) when it is needed (in Android cases) and directly uses
#       System.IO.File when BSA is not needed. Consequently, ItkFile should be used instead
#       of System.IO.File in any module that could be used in Android environement.
# Parameters : -
# Note : - To use BetterStreamingAssets in this script, the define symbol USINGBETTERSTREAMINGASSETS 
#       must be set in the project.
# Tests : - 
#############################################################################################*/

using System;
using System.IO;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace IthacaLib
{

    public static class ItkFile
    {
#if USINGBETTERSTREAMINGASSETS && UNITY_ANDROID

        static ItkFile()
        {
            BetterStreamingAssets.Initialize();
            UnityEngine.Debug.Log("initialized");
        }

        public static bool FileExists(string path)
        {
            path = MakeRelativeIfStreamingAssets(path);
            return BetterStreamingAssets.FileExists(path);
        }

        public static bool DirectoryExists(string path)
        {
            path = MakeRelativeIfStreamingAssets(path);
            return BetterStreamingAssets.DirectoryExists(path);
        }

        public static string ReadAllText(string path)
        {
            Debug.Log("ReadAllText BSA");
            path = MakeRelativeIfStreamingAssets(path);


            return BetterStreamingAssets.ReadAllText(path);
        }

        public static string[] ReadAllLines(string path)
        {
            path = MakeRelativeIfStreamingAssets(path);
            return BetterStreamingAssets.ReadAllLines(path);
        }

        public static byte[] ReadAllBytes(string path)
        {
            if (path == null)
                throw new ArgumentNullException("path");
            if (path.Length == 0)
                throw new ArgumentException("Empty path", "path");

            path = MakeRelativeIfStreamingAssets(path);
            return BetterStreamingAssets.ReadAllBytes(path);
        }

        public static string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
            path = MakeRelativeIfStreamingAssets(path);
            return BetterStreamingAssets.GetFiles(path, searchPattern, searchOption);
        }

        public static string[] GetFiles(string path)
        {
            return GetFiles(path, null);
        }

        public static string[] GetFiles(string path, string searchPattern)
        {
            return GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
        }

        #region Not implemented BetterStreamingAssets style functions
        //Some disabled  functions coming from BetterStreamingAssets, but not useful for now... until further notice
        //public static AssetBundleCreateRequest LoadAssetBundleAsync(string path, uint crc = 0)
        //public static AssetBundle LoadAssetBundle(string path, uint crc = 0)
        //public static System.IO.Stream OpenRead(string path)
        //public static System.IO.StreamReader OpenText(string path)
        //private static ReadInfo GetInfoOrThrow(string path)
        //private static void ThrowFileNotFound(string path)

        //public static System.IO.Stream OpenRead(string path)
        //{
        //    if (path == null)
        //        throw new ArgumentNullException("path");
        //    if (path.Length == 0)
        //        throw new ArgumentException("Empty path", "path");

        //    return BetterStreamingAssets.OpenRead(path);
        //}

        //public static System.IO.StreamReader OpenText(string path)
        //{
        //    Stream str = OpenRead(path);
        //    try
        //    {
        //        return new StreamReader(str);
        //    }
        //    catch (System.Exception)
        //    {
        //        if (str != null)
        //            str.Dispose();
        //        throw;
        //    }
        //}
        #endregion


#else

        public static bool FileExists(string path)
        {
#if !UNITY_EDITOR
            path = MakeRelativeIfStreamingAssets(path);
#endif
            return System.IO.File.Exists(path);
        }

        public static bool DirectoryExists(string path)
        {
#if !UNITY_EDITOR
            path = MakeRelativeIfStreamingAssets(path);
#endif
            return System.IO.Directory.Exists(path);
        }


        public static string ReadAllText(string path)
        {
#if !UNITY_EDITOR
            path = MakeRelativeIfStreamingAssets(path);
#endif
            return System.IO.File.ReadAllText(path);
        }

        public static string[] ReadAllLines(string path)
        {
#if !UNITY_EDITOR
            path = MakeRelativeIfStreamingAssets(path);
#endif
            return System.IO.File.ReadAllLines(path);
        }

        public static byte[] ReadAllBytes(string path)
        {
            if (path == null)
                throw new ArgumentNullException("path");
            if (path.Length == 0)
                throw new ArgumentException("Empty path", "path");
#if !UNITY_EDITOR
            path = MakeRelativeIfStreamingAssets(path);
#endif
            return System.IO.File.ReadAllBytes(path);
        }

        public static string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
        {
#if !UNITY_EDITOR
            path = MakeRelativeIfStreamingAssets(path);
#endif
            return System.IO.Directory.GetFiles(path, searchPattern, searchOption);
        }

        public static string[] GetFiles(string path)
        {
            return GetFiles(path, null);
        }

        public static string[] GetFiles(string path, string searchPattern)
        {
            return GetFiles(path, searchPattern, SearchOption.TopDirectoryOnly);
        }


#endif
        //Ithaca context specific
        /// <summary>
        /// Cleans up path from unwanted absolute part if "StreamingAssets" is found inside.
        /// </summary>
        /// <param name="path"></param>
        /// <returns><paramref name="path"/> but relative to StreamingAssets folder.</returns>
        private static string MakeRelativeIfStreamingAssets(string path)
        {
            if (path.Contains("StreamingAssets"))
            {
#if USINGBETTERSTREAMINGASSETS
                return path.Substring(path.IndexOf("StreamingAssets") + 16);
#else
                return Path.Combine(Application.dataPath, path.Substring(path.IndexOf("StreamingAssets")));
#endif
            }
            return path;
        }
    }

}