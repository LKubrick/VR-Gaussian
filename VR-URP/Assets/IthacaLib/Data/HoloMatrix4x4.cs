/*#############################################################################################
# Project: Holocap3D by FrenchTouchFactory
# Resp. : Quentin
# Author(s): Quentin de Cagny ( quentincontact@gmail.com)
# Date : created around avril 2022
# Description: a wrapper for matrix4x4.
#   
#############################################################################################*/

using UnityEngine;
using System.IO;

#if USINGK4A
using K4AdotNet.Sensor;
using K4AdotNet;
#endif
using IthacaLib;

namespace Holo
{
    ///<summary>This is a matrix4x4data type encapsulation.</summary>
    public class HoloMatrix4x4 : HoloBaseData
    {
        public Matrix4x4 matrix4x4;
 
        public HoloMatrix4x4()
        {
            matrix4x4 = new Matrix4x4();
            setIdentity();
        }

        public HoloMatrix4x4(Matrix4x4 mat44)
        {
            matrix4x4 = mat44;
            //Q:: @jon ? on initialise la matrix4x4 en ram donc on devrait dire apply() non ? je comprends toujours pas le "c'est le dataproxy qui s'en charge"
            // Si je comprends bien on n'est pas concernés par le sync/apply ici car ce n'est pas une donnée synchronisée ici, elle est synchronisée seulement 
            // si elle est couplée à une version sur disque et donc si elle est dans le système DataManager/DataProxy. Bref, c'est pas à ce niveau qu'on gère 
            // la synchro.
        }

        public void setIdentity()
        {
            matrix4x4 = Matrix4x4.identity;
            //apply(); //Q:: @jon ? la donnée a été changée en RAM (réinitialisée) donc il faut bien dire que c'est la RAM qui a raison --> non, c'est le DataProxy qui s'en charge
        }

        public override void clear()
        {
            setIdentity();
        }

        ///<summary>Transforms position from local space to world space.</summary>
        public Vector3 TransformPoint(Vector3 p, bool fast=false)
        {
            /*
            GameObject go = new GameObject(); //!!!

            go.transform.localScale = matrix4x4.lossyScale;
            go.transform.rotation = matrix4x4.rotation;
            go.transform.position = matrix4x4.GetPosition();

            
            

            return go.transform.TransformPoint(p);
            */

            //OPTIMISATION : do this without creating a gameObject (and verify it is the same result of course)
            if (fast)
            {
                return matrix4x4.MultiplyPoint3x4(p);
            }
            else
            {
                return matrix4x4.MultiplyPoint(p);
            }
        }

        public override void ReadFromDisk(string path)
        {
            current_path = path; //FIXME
            if (!path.EndsWith(".json"))
                Debug.Log("not supported yet, please find a JSON instead");
            else
            {
                //Q:: RMQ : si le path est "IsPathRooted" (expl: C:/Machin/Bidul.json) le read se fait à cet endroit (s'il est absolu quoi), 
                // sinon il ajoute le rep de travail (du Unity), donc il manque "Data/", il faut donc rajouter HoloConfig.DataPath, mais du coup
                // ça ne marche plus avec les path absolus. il faudrait éventuellement séparer les cas, mais c'est pas super propre...
                // et surtout faudra le faire sur chaque sous-type de HoloBaseData, ce qui fait pas mal de répétition de code... bof bof
                // voir aussi IsFullyQualified et cette page : https://docs.microsoft.com/fr-fr/dotnet/api/system.io.path.ispathrooted?view=net-6.0
                // Bref, je l'ai fait ici (et dans le writeToDisk) pour voir si ça marche, mais j'aime pas trop (notamment l'overhead peut être méchant ? )
                string jsonString;
                jsonString = ItkFile.ReadAllText(path);  //Q:: je suis revenu à une version simple sans Data/ pour le scheduler, mais bon, ça devrait faire buguer des trucs d'avant...
                // if (Path.IsPathFullyQualified(path)) jsonString = File.ReadAllText (path);
                // else jsonString = File.ReadAllText (HoloConfig.DataPath + path);
                matrix4x4 = (Matrix4x4)JsonUtility.FromJson<Matrix4x4>(jsonString);
            }
            Debug.Log("HoloTransform read from JSON (at "+path+") : "
                + "\n pos: " + matrix4x4.GetPosition().ToString() 
                + "\n  rot: " + matrix4x4.rotation.ToString() 
                + "\n  scale: " + matrix4x4.lossyScale.ToString() 
                + "\n raw Matrix4x4: \n" + matrix4x4.ToString());

        }

        public override void WriteToDisk(string path)
        {
            current_path = path; //FIXME

            if (!path.EndsWith(".json"))
                Debug.Log("not supported yet, please write in a JSON instead");
            else
            {
                File.WriteAllText(path, JsonUtility.ToJson(matrix4x4));   //Q:: je suis revenu à une version simple sans Data/ pour le scheduler, mais bon, ça devrait faire buguer des trucs d'avant...
                // if (Path.IsPathFullyQualified(path)) File.WriteAllText(path, JsonUtility.ToJson(matrix4x4));
                // else File.WriteAllText(HoloConfig.DataPath + path, JsonUtility.ToJson(matrix4x4));
                Debug.Log("HoloTransform writen to JSON (path: " + path + " , matrix4x4 = " + matrix4x4.ToString());
            }
        }


#if USINGK4A
        ///<summary>Get the transformation matrix that convert x,y,z coordinates from one sensor position to another
        /// according to the Calibration of a specific Kinect Azure Camera.</summary>
        public static Matrix4x4 GetMatrixFromConvert3Dto3D(Calibration cal, CalibrationGeometry sourceSensor, CalibrationGeometry targetSensor)
        {

        //    Float3x3 colorRotMat = cal.ColorCameraCalibration.Extrinsics.Rotation;
        //    Float3 colorPosMat = cal.ColorCameraCalibration.Extrinsics.Translation;
        //    Debug.Log("colorPosMat - x : " + colorPosMat.X + " y : " + colorPosMat.Y + " z : " + colorPosMat.Z);
        //    Debug.Log("colorRotMat : " + colorRotMat.ToString());
        //    Matrix4x4 colorMat = new Matrix4x4(
        //        new Vector4(colorRotMat.M11, colorRotMat.M21, colorRotMat.M31, 0),
        //        new Vector4(colorRotMat.M12, colorRotMat.M22, colorRotMat.M32, 0),
        //        new Vector4(colorRotMat.M13, colorRotMat.M23, colorRotMat.M33, 0),
        //        new Vector4(colorPosMat.X, colorPosMat.Y, colorPosMat.Z, 1));

        //    //Note: with Kinect Azure, DepthCameraCalibrationExtrinsics seems to be Identity (as it is probably consider as
        //    //the center of the camera itself). Consequently, the code below has been commented out for simplification 
        //    // AND IT HAS NOT BEEN TESTED in the case of a non-identity depth extrinscics.
        //    //Float3x3 depthRotMat = cal.DepthCameraCalibration.Extrinsics.Rotation;
        //    //Float3 depthPosMat = cal.DepthCameraCalibration.Extrinsics.Translation;
        //    //Debug.Log("depthPosMat - x : " + depthPosMat.X + " y : " + depthPosMat.Y + " z : " + depthPosMat.Z);
        //    //Debug.Log("depthRotMat : " + depthRotMat.ToString());
        //    //Matrix4x4 depthMat = new Matrix4x4(
        //    //    new Vector4(depthRotMat.M11, depthRotMat.M21, depthRotMat.M31, 0),
        //    //    new Vector4(depthRotMat.M12, depthRotMat.M22, depthRotMat.M32, 0),
        //    //    new Vector4(depthRotMat.M13, depthRotMat.M23, depthRotMat.M33, 0),
        //    //    new Vector4(depthPosMat.X, depthPosMat.Y, depthPosMat.Z, 1));

        //    if (sourceSensor == CalibrationGeometry.Color && targetSensor == CalibrationGeometry.Depth)
        //        return Matrix4x4.Inverse(colorMat); // return depthMat * Matrix4x4.Inverse(colorMat);
        //    if (sourceSensor == CalibrationGeometry.Depth && targetSensor == CalibrationGeometry.Color)
        //        return colorMat; // * Matrix4x4.Inverse(depthMat);
        //    if (sourceSensor == targetSensor)
        //        return Matrix4x4.identity;

            Debug.LogWarning("GetMatrixFromConvert3Dto3D returned a Matrix4x4.zero because the source (" + sourceSensor.ToString() + ") and target (" + targetSensor.ToString() + ") sensors parameters are not handled.");
            return Matrix4x4.zero;
        }
#endif
            //TODO: this dirty function should not be in HoloMatrix
            public static string GetFullPathFromDecomposedPath(string dataPath, string seqName, string camAlignMatrix)
        {
            string path = "";
            if (dataPath != "")
                try
                {
                    DirectoryInfo dir = new DirectoryInfo(dataPath);
                    if (dir.Exists) path = dir.FullName;
                }
                catch { UnityEngine.Debug.LogWarning("dataPath is incorrect"); }

            if (seqName != "")
                try
                {
                    string seqDirPath = seqName;
                    //Get rid of "/" or "\" in first character if needed
                    if (seqName.Substring(0, 1) == "\\" || seqName.Substring(0, 1) == "/")
                        seqDirPath = seqName.Substring(1, seqName.Length - 1);
                    DirectoryInfo seqdir = new DirectoryInfo(Path.Combine(path, seqDirPath));
                    if (seqdir.Exists) 
                        path = seqdir.FullName;
                    else 
                        path = path + seqDirPath;
                }
                catch { UnityEngine.Debug.LogWarning("seqName is incorrect"); }


            string matrixPath = camAlignMatrix;

            //Get rid of "/" or "\" in first character if needed
            if (camAlignMatrix.Substring(0, 1) == "\\" || camAlignMatrix.Substring(0, 1) == "/")
                matrixPath = camAlignMatrix.Substring(1, seqName.Length - 1);

            //Add '.json" at the end if missing
            if (!(camAlignMatrix.Contains(".json") || camAlignMatrix.Contains(".JSON")))
                matrixPath = camAlignMatrix + ".json";

            FileInfo matrixFile = new FileInfo(Path.Combine(path, matrixPath));
            if (matrixFile.Exists)
                path = matrixFile.FullName;
            else
                path = path + matrixPath;
                //throw new FileNotFoundException("Matrix4x4 json files was not found. Path: " + path + matrixPath);

            return path;
        }

    }
    }
