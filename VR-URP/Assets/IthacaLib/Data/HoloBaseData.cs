/*###########################################################################################
# Project: Holocap3D by FrenchTouchFactory
# Resp : Jonathan
# Author(s): Jonathan Tanant ( jonathan.tanant@gmail.com ), Quentin de Cagny ( quentincontact@gmail.com )
# Date : creation March 2022
# Description: base class for all Holo types
# Output: 
###############################################################################################*/
using UnityEngine;

namespace Holo
{
    public enum HoloSyncState
    {
        insync,
        diskdirty,
        ramdirty
    }
    //this is the base class for all data types that can be used as input/outputs in the dataflow
    public class HoloBaseData
    {
        public string current_path; //has to be private FIXME
        //public HoloGroundTruth groundTruth = HoloGroundTruth.disk;
        HoloSyncState syncState = HoloSyncState.insync;
        //public bool ram_dirty = false;
        //public bool disk_dirty = false;

        public virtual void ReadFromDisk(string path){}
        //Q:: @Jon : je viens d'écrire cette fonction (et la même en Write plus bas) qui me permet de ne pas avoir à redonner/retrouver le bon 
        // le path lorsqu'on appelle Read ou WriteToDisk mais de simplement prendre celui qui est défini dans l'HoloBaseData
        // ça te paraît ok ?
        public virtual void ReadFromDisk(){ ReadFromDisk(getPathOnDisk()); }  //Q:: @Jon : mais du coup elle doit vraiment être virtual celle-là ? elle est déjà écrite du coup
        public virtual void WriteToDisk(string path){}
        public virtual void WriteToDisk(){ WriteToDisk(getPathOnDisk()); }

        //Q:: Réflexion sur sync/apply :
        // quand est-ce qu'on fait des sync ou apply par exemple ? :
        // - sync() AVANT un HoloMatrix.matrix4x4 (un get quoi, lorsqu'on veut lire cette valeur)
        // - apply() APRES un HoloMatrix.matrix4x4 = ... (un set en somme, lorsqu'on veut écrire cette valeur)
        // - applyDisk() APRES HDMgr.getMatrix4x4(path), bref, une commande qui va chercher un fichier sur le disque. A priori, on veut que ce soit cette valeur (sur disk) qui soit valide.
        //          Sauf que dans le cas ou l'HoloBaseData en question existe déjà dans la hashtable, et que c'est la RAM qui est dirty qu'est-ce qu'il se passe ?
        //          --> on tombe sur la Debug.LogError, donc on n'est pas snesé se retrouver dans ce cas. 
        // - syncDisk() AVANT un script python à qui l'on envoie les fichiers en entrée
        // - applyDisk() APRES un script python qui a modifié des fichiers (en output)

        //Version Jon :
// 1. avant d'utiliser la donnée en mémoire, il faut appeler sync() .           Exple (Q) : AVANT coucou = MonHoloMatrice.matrix4x4
// 2. avant d'utiliser la donnée sur le disque, il faut appeler syncDisk() .    Exple (Q) : AVANT hpy.CallPython("scriptsuper.py" arglist-avec-un-path-de-fichier-INPUT-correspondant-à-une-holodata-dedans)
// 3. après avoir modifié la donnée en mémoire, il faut appeler apply()  .      Exple (Q) : APRES MonHoloMatrice.matrix4x4 = matrix1 * matrix2
// 4. après avoir modifié la donnée sur le disque, il faut appeler applyDisk(). Exple (Q) : APRES hpy.CallPython("scriptsuper.py" arglist-avec-un-path-de-fichier-OUTPUT-correspondant-à-une-holodata-dedans)


        //MANTRAS du apply() et sync()
        //1. avant d'utiliser la donnée en mémoire, il faut appeler sync()
        //2. avant d'utiliser la donnée sur le disque, il faut appeler syncDisk()
        //3. après avoir modifié la donnée en mémoire, il faut appeler apply()
        //4. après avoir modifié la donnée sur le disque, il faut appeler applyDisk()



// Q:: proposition de 5. : quelque part il faut bien appeler apply() puis syncDisk() avant de sortir d'un programme, 
// d'un module... histoire de ne pas perdre les données qui sont en RAM (et dirty) non ?



        ///<summary>Makes sure that the data on disk is valid. This will usually copy the data from RAM to disk if needed. To call BEFORE using disk version of a data.</summary>
        public void syncDisk()
        {
            if (syncState == HoloSyncState.ramdirty)
            {
                WriteToDisk(current_path);
                syncState = HoloSyncState.insync;
            }
            else if (syncState == HoloSyncState.insync) //nothing to do we are already in sync
                Debug.Log("syncDisk() report : this data is already insync so nothing to do. (path=" + current_path + ")");
            else if (syncState == HoloSyncState.diskdirty) //nothing to do disk is already the most recent version of data
                Debug.Log("syncDisk() report : this data is already diskdirty so nothing to do. (path=" + current_path + ")");
        }

        ///<summary>Makes sure that the data in RAM is valid. This will usually copy the data from disk to RAM if needed. To call BEFORE using RAM version of a data.</summary>
        public void sync()
        {
            if (syncState == HoloSyncState.diskdirty)
            {
                Debug.Log("current_path = " + current_path);
                ReadFromDisk(current_path);
                syncState = HoloSyncState.insync;
            }
            else if (syncState == HoloSyncState.insync)
                //nothing to do we are already in sync
                Debug.Log("sync() report : this data is already insync so nothing to do. (path=" + current_path + ")");
            else if (syncState == HoloSyncState.ramdirty)
                //nothing to do disk is already the most recent version of data
                Debug.Log("sync() report : this data is already insync so nothing to do. (path=" + current_path + ")");
        }

        ///<summary>Notify the system that data on disk has been updated without updating data in RAM (yet). To call AFTER having modified the disk version of a data.</summary>
        ///<param name="force"> if true, forces the data to become diskdirty, even if it was ramdirty. To use if the previous value and sync state was not important, when we want to create a new data over an old one of the same name for example.</param>
        public void applyDisk(bool force=false)
        {
            if (syncState == HoloSyncState.ramdirty && !force) 
                Debug.LogError("applyDisk() report : ram is dirty, missing sync. (path=" + current_path + ")");
            else 
                syncState = HoloSyncState.diskdirty;
        }

        ///<summary>Notify the system that data in RAM has been updated without updating data on disk (yet). To call AFTER having modified a data in RAM.</summary>
        ///<param name="force"> if true, forces the data to become ramdirty, even if it was diskdirty. To use if the previous value and sync state was not important, when we want to create a new data over an old one of the same name for example.</param>
        public void apply(bool force=false)
        {
            if (syncState == HoloSyncState.diskdirty && !force)
                Debug.LogError("apply() report : disk is dirty, missing sync. (path=" + current_path + ")");
            else
                syncState = HoloSyncState.ramdirty;
        }

        //get the current path
        public string getPathOnDisk()
        {
            return current_path;
        }

        public virtual void clear() {}
    }
}
