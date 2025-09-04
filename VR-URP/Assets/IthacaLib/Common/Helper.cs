/*#############################################################################################
# ---- Helper ----
# Project: IthacaEmetteurRecepteur by FrenchTouchFactory
# Resp. : Quentin
# Author(s): Quentin de Cagny ( quentindecagny@gmail.com)
# Date : created in january 2024
# Description: Helper class with some useful functions.
# Parameters : -
# Note : -
# Tests : -
#############################################################################################*/



using UnityEngine;

namespace IthacaLib
{
    public static class Helper
    {
        /// <summary>Tries to find the user assigned index of the camera from the name of the GameObject. Must be in parentheses like in "ITKtoVfx (3)".</summary>
        /// <param name="go">GameObject on which to find the cam index (usually this.gameObject)</param>
        public static int GetCamIndex(GameObject go)
        {
            int _myCamIndex = -1;
            var indexOfOpen = go.name.LastIndexOf("(");
            var indexOfClose = go.name.LastIndexOf(")");
            string number = "";
            if (indexOfOpen >= 0 && indexOfClose >= 0)
                number = go.name.Substring(indexOfOpen + 1, indexOfClose - indexOfOpen - 1);

            if (int.TryParse(number, out _myCamIndex))
                return _myCamIndex;
            else 
                throw new System.Exception("The camera index could not be deduced from the name of the gameObject, disable Settings by Context and set the parameters manually or add \"( index of the source camera) \" at the end of the game object's name");
          // throw new System.Exception("GetCamIndex: has not found any cam number in parentheses in the name of the GameObject.");
        }

        /// <summary>
        /// Tries to find the user assigned index of the camera from the name of the GameObject. Must be in parentheses like in "ITKtoVfx (3)".
        /// </summary>
        /// <param name="go"></param>
        /// <returns>CamIndex in string format or "NO_CAM_INDEX_FOUND"</returns>
        public static string GetCamIndexString(GameObject go)
        {
            int _myIndex = GetCamIndex(go);
            if (_myIndex != -1)
                return _myIndex.ToString();
            else
                return "NO_CAM_INDEX_FOUND";
        }
    }
}
