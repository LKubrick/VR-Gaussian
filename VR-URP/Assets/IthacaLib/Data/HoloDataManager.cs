/*###########################################################################################
# Project: Holocap3D by FrenchTouchFactory
# Resp : Jonathan
# Author(s): Jonathan Tanant ( jonathan.tanant@gmail.com ), Quentin de Cagny ( quentincontact@gmail.com )
# Date : creation March 2022
# Description: the class responsible for all data management
# Output: 
###############################################################################################*/

using System.Collections.Generic;
using UnityEngine;

namespace Holo
{

    //deprecated
    public enum HoloDataFlag
    {
        overwritewithempty,
        createifnotexists,
        keepexisting
    }

    /*
     * 
     * 
     * This is our class responsible for all data management
     * 
     */
    public static class HoloDataManager
    {
        //this is our cache : (a simple string to Data Proxy dictionary)
        //each key is unique
        //this is a hashtable
        public static Dictionary<string, HoloDataProxy> cache = new Dictionary<string, HoloDataProxy>();

        public static string getStats()
        {
            return "entries: " + cache.Keys.Count;
        }
        //types de données
        //HoloPointCloud OK
        //HoloTexture OK
        //HoloMesh OK
        //HoloMatrix4x4 OK
        //HoloVoxels OK
        //HoloCameraPose //Q:: j'ai l'impression qu'une matrix4x4 contient tout ce qu'il faut pour être une pose caméra --> non, finalement, il n'y a pas de fov (et le scale ne sert à rien, du coup, gardons cameraPose)
        //HoloFile

        //TO DO
              //HoloTransform DEPRECATED?   
    
        //Q:: @Jon , ça te paraît un bon résumé (ci-dessous) ?
        ///<summary>Gets the HoloMatrix4x4 at this <paramref name="path"/> if it exists or create one if not.</summary>
        public static HoloMatrix4x4 getMatrix4x4(string path)
        {
            return getDataProxyFromCache(path).getMatrix4x4();
        }

        //public static HoloPointCloud getPointCloud(string path, bool loadinmemory=true)
        //{
        //    return getDataProxyFromCache(path).getPointCloud();
        //}

        //public static HoloTexture getTexture(string path, bool loadinmemory=true)
        //{
        //    return getDataProxyFromCache(path).getTexture();
        //}

        //public static HoloMesh getMesh(string path, bool loadinmemory=true)
        //{
        //    return getDataProxyFromCache(path).getMesh();
        //}

        //public static HoloVoxels getVoxels(string path, bool loadinmemory=true)
        //{
        //    return getDataProxyFromCache(path).getVoxels();
        //}

        //public static HoloCameraPose getCameraPose(string path)
        //{
        //    return getDataProxyFromCache(path).getCameraPose();
        //}

        //public static HoloFile getFile(string path, bool loadinmemory=false)
        //{
        //    return getDataProxyFromCache(path).getFile();
        //}


        public static HoloDataProxy getDataProxyFromCache(string path)
        {
            //1. data is already in cache and exists as a proxy
            //-1a : loaded              CACHE OUI   RAM OUI
            //-2b : unloaded            CACHE OUI   RAM NON
            //2. data is not in cache, file exists - data is created and added to the cache
            //3. data is not in cache, file does not exist - empty data is created
            
            HoloDataProxy dataproxy = null;
            try
            {
                dataproxy = cache[path]; //get the dataproxy instance from the cache
            }
#pragma warning disable CS0168
            catch (KeyNotFoundException e)
            {
#pragma warning restore CS0168
                //data is not in cache, we have to create the proxy data from the path
                Debug.Log("not in cache, PROXY DATA CREATED at path=" + path);
                dataproxy = createDataProxyAndAddToCache(path);
            }
            return dataproxy;
        }

        public static HoloDataProxy createDataProxyAndAddToCache(string path)
        {
            HoloDataProxy dataproxy = new HoloDataProxy();
            dataproxy.path = path;
            cache.Add(path, dataproxy);
            return dataproxy;
        }

        ///<summary>Provides a unique file path (with its name and extension) for temp uses. Begins with [DataPath]/temp/".</summary>
        ///<param name="extension">The extension of the file (without "."). Example : "json" . Default: passing empty string will result in a generic ".tmp" extension.</param> 
        //public static string getUniqueTempPath(string extension = "")
        //{
        //    System.DateTime d = System.DateTime.Now;
        //    if (extension == "")
        //    {
        //        extension = "tmp";
        //    }
        //    return HoloConfig.DataPath + "temp/" + d.Year + "_" + d.Month + "_" + d.Day + "_" + d.Hour + "_" + d.Minute + "_" + d.Second + "_" + (int)(UnityEngine.Random.value * 10000f) + "." + extension;

        //}

        public static void syncAllToDisk()
        {
            //we force a sync Disk for all data in cache
            //this will ensure that all files on disk are in sync
            foreach (HoloDataProxy p in cache.Values)
            {
                if (p.data != null)
                {
                    p.data.syncDisk();
                    //LogManager.Log(p.path + " has been synced to disk.");
                }
            }

        }
    }
}
