/*###########################################################################################
# Project: Holocap3D by FrenchTouchFactory
# Resp : Jonathan
# Author(s): Jonathan Tanant ( jonathan.tanant@gmail.com ), Quentin de Cagny ( quentincontact@gmail.com )
# Date : creation March 2022
# Description: a proxy for any Holo data - implements cache features (with the Data Manager)
# Output: 
###############################################################################################*/

using UnityEngine;

namespace Holo
{
    //the data proxy can be used instead of the data itself to take less memory
    public class HoloDataProxy
    {
        public string path = "";
        public HoloBaseData data; //polymorph data container (in RAM!!!)


        public HoloMatrix4x4 getMatrix4x4()
        {
            //ensure loaded
            if (data == null)
            {
                HoloMatrix4x4 mat = new HoloMatrix4x4(); //Q:: ici on a un setIdentity qui se fait à l'intérieur.
                try
                {
                    mat.ReadFromDisk(path); //we get the data from the file and overwrite the HoloBaseData content in RAM
                    // data = mat; //Q:: je l'ajoute ici car ça ne change rien, mais je préfère que cette valeur soit déjà okquand je fais :
                    //data.applyDisk(); //Q:: @jon : on vient de choper la valeur from disk et on l'a mise dans data par le data = mat plus bas, donc on peut dire que c'est le disque qui fait foi non ?
                    // data.sync(); //Q:: et je sync() histoire de dire qu'on est "insync"
                    // NON, le ReadFromDisk() synchronise par définition les 2 données.
                }
                catch (System.IO.FileNotFoundException)
                {
                    mat.apply();
                    Debug.Log("getMatrix4x4 mat.ReadFromDisk failed, catch : nothing, file does not exist, data is unchanged (path=" + path);
                }
                data = mat;
                //Q:: maintenant que data est effectivement updaté, ce serait bien de dire qu'on est "insync", sauf qu'il se peut que le fichier 
                // n'existe pas, auquel cas on n'est pas vraiment "insync".
            }
            return data as HoloMatrix4x4;
        }

        // DE-ACTIVATED because those holoData types from HoloUnity are not (yet) included in Ithacalib
        //public HoloPointCloud getPointCloud()
        //{
        //    //ensure loaded
        //    if (data == null)
        //    {
        //        HoloPointCloud pc = new HoloPointCloud();
        //        try
        //        {
        //            pc.ReadFromDisk(path);
        //        }
        //        catch (System.IO.FileNotFoundException)
        //        {
        //            pc.apply();
        //        }
        //        data = pc;
        //        data.current_path = path;
        //    }
        //    return data as HoloPointCloud;
        //}
        //public HoloTexture getTexture()
        //{
        //    //ensure loaded
        //    if (data == null)
        //    {
        //        HoloTexture tex = new HoloTexture();
        //        try
        //        {
        //            tex.ReadFromDisk(path);
        //        }
        //        catch (System.IO.FileNotFoundException)
        //        {
        //            tex.apply();

        //        }
        //        data = tex;
        //        data.current_path = path;
        //    }
        //    return data as HoloTexture;
        //}

        //public HoloMesh getMesh()
        //{
        //    //ensure loaded
        //    if (data == null)
        //    {
        //        HoloMesh m = new HoloMesh();
        //        //m.ReadFromDisk(path);  //TO DO
        //        data = m;
        //        data.current_path = path;
        //        m.apply();
        //    }
        //    return data as HoloMesh;
        //}

        //public HoloCameraPose getCameraPose()
        //{
        //    //ensure loaded
        //    if (data == null)
        //    {
        //        HoloCameraPose cp = new HoloCameraPose();
        //        //cp.ReadFromDisk(path);  //TO DO
        //        data = cp;
        //        data.current_path = path;
        //    }
        //    return data as HoloCameraPose;
        //}

        //public HoloTransform getTransform()
        //{
        //    //ensure loaded
        //    if (data == null)
        //    {
        //        HoloTransform t = new HoloTransform();
        //        //t.ReadFromDisk(path);  //TO DO
        //        data = t;
        //        data.current_path = path;
        //    }
        //    return data as HoloTransform;
        //}

        //public HoloVoxels getVoxels()
        //{
        //    //ensure loaded
        //    if (data == null)
        //    {
        //        HoloVoxels v = new HoloVoxels();
        //        //v.ReadFromDisk(path);  //TO DO
        //        data = v;
        //        data.current_path = path;
        //        v.apply();
        //    }
        //    return data as HoloVoxels;
        //}

        //public HoloFile getFile()
        //{
        //    //ensure loaded
        //    if (data == null)
        //    {
        //        HoloFile v = new HoloFile();
        //        data = v;
        //        data.current_path = path;
        //    }
        //    return data as HoloFile;
        //}

        
        

        public void unload()
        {
            //TO DO : write to disk
            //data.writeToDisk(path); //backup data on disk
            data = null; //garbage collector should take care of this
        }
    }
}
