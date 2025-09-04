/*!----------- XYTableParser -------------------------------------------------------------------
* @brief 
*     Parser of text XYTable file to float array
*
*-----------------------------------------------------------------------------------------------
* @copyright (c) 2024 French Touch Factory. All rights reserved.
* @project IthacaLib by French Touch Factory
* @resp Quentin
* @author 
*    Francois Bouille, Quentin de Cagny ( quentindecagny@gmail.com) - French Touch Factory (100%)
*    derived from Keijiro's akvfx (https://github.com/keijiro/Akvfx)
* @date created june 2023, 
* @details 
*   Receiver side version of the XYTable. Essentially creates a buffer (ReadOnlySpan) from a .xyt file
*   and holds it.
* @param[in] folder, serial, depth resolution, bottom up/top town flag
* @param[out] ReadOnlySpan<float> Data
* @note
*   - july2024: added the bottomUp flag and memory inversion if true
* @test 
*---------------------------------------------------------------------------------------------*/

using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;

using UnityEngine.Networking; //FB: if XYtable is somewhere online
using UnityEngine;
using System.Collections;

namespace IthacaLib
{

    ///<summary>Receiver side version of the XYTable. Essentially creates a buffer (ReadOnlySpan) from a .xyt file
    /// and holds it.</summary>
    class XYTableParser
    {
        // Public property: Table data
        public ReadOnlySpan<float> Data => _data;
        private float[] _data;
        private UnityWebRequest www;
        ///<summary>Constructor from a .xyt file. Serial, width and height must be specified to select the right file in the <paramref name="XYTableFolder"/>. </summary>
        ///<param name="XYTableFolder">Folder in which are store the XYTables locally.</param>
        ///<param name="kinectSerial">Serial of the kinect used.</param>
        ///<param name="width">Width resolution of the Depth data.</param>
        ///<param name="height">Height resolution of the Depth data.</param>
        ///<param name="bottomUp">Generate XYTable with inversed line order, toggling from top down to bottom up memory addressing convention. Bottom up = with first value in memory being the down left pixel. Should only be used with care.</param>
        ///<returns>Data will be null if the file has not been found.</returns>
        private string _XYTableFolder;
        private string _kinectSerial;
        private int _width;
        private int _height;
        private bool _bottomUp;

        public XYTableParser(int width, int height, string XYTableFolder, string kinectSerial, bool bottomUp = true)
        {
            _XYTableFolder = XYTableFolder;
            _kinectSerial = kinectSerial;
            _width = width;
            _height = height;
            _bottomUp = bottomUp;
            string xytPath = Path.Combine(XYTableFolder, kinectSerial + "_" + width + "x" + height + ".xyt").Replace("\\","/");
            UnityEngine.Debug.Log("Searching for file : " + xytPath);
            if (ItkFile.FileExists(xytPath))
            {
                UnityEngine.Debug.Log("XYTable file found. Now trying to read it...");
                string xytText = ItkFile.ReadAllText(xytPath);
                UnityEngine.Debug.Log("XYTable: Parsing a string of length : " + xytText.Length);
                float[] fValues = new float[width * height * 2];

                // Determine if top down memory addressing is used or not
                bool addrConventionTopDown = true; //Wether addressing convention of the file read is top down (true) or bottom up (false)
                int indexConv = xytText.IndexOf("addressing convention:") + 23;
                if (indexConv >= 0) 
                {
                    string xytConvention = xytText.Substring(indexConv, indexConv + 10);
                    if (xytConvention.Contains("top down")) addrConventionTopDown = true;
                    if (xytConvention.Contains("bottom up")) addrConventionTopDown = false;
                }
                else
                    addrConventionTopDown = true;  //Probably an old XYTable without mention of addressing and, thus, assuming it is top down.

                //Split all values in a string array
                char[] separators = { ' ', '\n' };
                string xytTextWithoutHeader = xytText.Substring(xytText.IndexOf("end_header") + 12); //12=length of "end_header" + 2 characters (but why 2??)
                string[] strValues = xytTextWithoutHeader.Split(separators);


                bool botUp = bottomUp ^ !addrConventionTopDown;
                int xyi; //index for direct addressing
                int xyibu; //index for bottom up addressing
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        xyi = (x + y * width) * 2 ;
                        xyibu = (x + (height - 1 - y) * width) * 2 ;
                        float val = 0f;
                        if (float.TryParse(strValues[xyi], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val))
                            fValues[botUp ? xyibu : xyi] = val;
                        xyi++;
                        xyibu++;
                        val = 0f;
                        if (float.TryParse(strValues[xyi], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val))
                            fValues[botUp ? xyibu : xyi] = val;
                    }

                // Old simple direct version, working correctly, but unable to handle bottomUp option
                //for (int i = 0; i < fValues.Length; i++)
                //{
                //    float val = 0f;
                //    string valfloat = strValues[i].Replace(",", "."); //OPTIM: can be commented out, it's just a useless precaution now.
                //    if (float.TryParse(strValues[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val))
                //        fValues[i] = val;
                //}
                _data = fValues;
            }
            // Case of a XYtable online URL begins with "http"
            else if (xytPath.Substring(0, 4)=="http")
            {
                //ContextManager.instance.StartCoroutine(GetRequest(xytPath));
                www = UnityWebRequest.Get(xytPath);
                //www.SetRequestHeader("Access-Control-Allow-Origin", "*");
                www.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:69.0) Gecko/20100101 Firefox/69.0");//without this lign we get a 403 forbidden access to the online XYtable
                www.SendWebRequest();

            }

            else
            {
                _data = null;
                UnityEngine.Debug.LogError($"File at {xytPath} not found");
            }


        }
        //IEnumerator GetRequest(string xytPath)
        //{
        //    using (UnityWebRequest www = UnityWebRequest.Get(xytPath)) // NE MARCHE PAS !!!
        //    {
        //        UnityEngine.Debug.Log("JE SUIS LA");
        //        www.SetRequestHeader("Access-Control-Allow-Origin", "*");
        //        yield return www.SendWebRequest();
                
        //            /*if (www.result != UnityWebRequest.Result.Success)
        //            {
        //                UnityEngine.Debug.LogError(www.error);
        //                UnityEngine.Debug.LogError("Error reading UnityWebRequest with a result of: " + www.result);
        //            }*/
        //            if (www.result == UnityWebRequest.Result.ConnectionError)
        //        {
        //            UnityEngine.Debug.Log(www.error);
        //            UnityEngine.Debug.LogError("Error reading UnityWebRequest with a result of: " + www.result);
        //        }
        //        else
        //        {
        //            // Show results as text
        //            UnityEngine.Debug.Log("XYTable file found online. Now trying to read it...");
        //            UnityEngine.Debug.Log("www.ToString() : " + www.url); // VIDE !!!
        //            string xytText = ItkFile.ReadAllText(www.downloadHandler.text);
        //            UnityEngine.Debug.Log("XYTable: Parsing a string of length : " + xytText.Length);
        //            float[] fValues = new float[width * height * 2];

        //            // Determine if top down memory addressing is used or not
        //            bool addrConventionTopDown = true; //Wether addressing convention of the file read is top down (true) or bottom up (false)
        //            int indexConv = xytText.IndexOf("addressing convention:") + 23;
        //            if (indexConv >= 0)
        //            {
        //                string xytConvention = xytText.Substring(indexConv, indexConv + 10);
        //                if (xytConvention.Contains("top down")) addrConventionTopDown = true;
        //                if (xytConvention.Contains("bottom up")) addrConventionTopDown = false;
        //            }
        //            else
        //                addrConventionTopDown = true;  //Probably an old XYTable without mention of addressing and, thus, assuming it is top down.

        //            //Split all values in a string array
        //            char[] separators = { ' ', '\n' };
        //            string xytTextWithoutHeader = xytText.Substring(xytText.IndexOf("end_header") + 12); //12=length of "end_header" + 2 characters (but why 2??)
        //            string[] strValues = xytTextWithoutHeader.Split(separators);


        //            bool botUp = bottomUp ^ !addrConventionTopDown;
        //            int xyi; //index for direct addressing
        //            int xyibu; //index for bottom up addressing
        //            for (int y = 0; y < height; y++)
        //                for (int x = 0; x < width; x++)
        //                {
        //                    xyi = (x + y * width) * 2;
        //                    xyibu = (x + (height - 1 - y) * width) * 2;
        //                    float val = 0f;
        //                    if (float.TryParse(strValues[xyi], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val))
        //                        fValues[botUp ? xyibu : xyi] = val;
        //                    xyi++;
        //                    xyibu++;
        //                    val = 0f;
        //                    if (float.TryParse(strValues[xyi], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val))
        //                        fValues[botUp ? xyibu : xyi] = val;
        //                }

        //            // Old simple direct version, working correctly, but unable to handle bottomUp option
        //            //for (int i = 0; i < fValues.Length; i++)
        //            //{
        //            //    float val = 0f;
        //            //    string valfloat = strValues[i].Replace(",", "."); //OPTIM: can be commented out, it's just a useless precaution now.
        //            //    if (float.TryParse(strValues[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val))
        //            //        fValues[i] = val;
        //            //}
        //            _data = fValues;
        //        }
        //    }
        //}

//FOR TEST DEBUG
        public static void WriteXYTableFile(XYTableParser _xytable, string XYTableFolder, string kinectSerial = "unknownserial")
        {
            string xytPath = XYTableFolder + kinectSerial +"_"+ _xytable.Data.Length + ".xyt";
            string xytContent = "xyt\nformat ascii 1.0\nkinect serial: " + kinectSerial + "\ncomment Generated by Ithaca" + "\nwidth: " + "not available" + "\nheight: " + "not available" + "\nproperty float x\nproperty float y\nend_header";

            //if (!AssetDatabase.IsValidFolder(@"Assets\directlive\" + this.name)) AssetDatabase.CreateFolder(@"Assets\directlive", this.name);

            List<string> lines = new List<string>();
            lines.Add(xytContent);
            for (int i = 0; i < _xytable.Data.Length; i+=2)
            {
                string xytContentAdd = _xytable.Data[i] + " " + _xytable.Data[i+1];
                lines.Add(xytContentAdd);
            }
            File.WriteAllLines(xytPath, lines.ToArray());
        }
        public void ParseWWWresult()
        {
            if (www != null)
            {
                /*UnityEngine.Debug.Log("www.uploadProgress==0 : " + www.uploadProgress);
                UnityEngine.Debug.Log("communicating = "+ communicating);
                UnityEngine.Debug.Log("www.isDone = " + www.isDone);*/

                //www.SetRequestHeader("Access-Control-Allow-Origin", "*");
                //UnityWebRequestAsyncOperation asyncOperation = www.SendWebRequest();
                //while (asyncOperation.isDone == false || asyncOperation==null) UnityEngine.Debug.Log("not yet");

                if (www.result != UnityWebRequest.Result.Success)
                {
                    UnityEngine.Debug.LogError(www.error);
                    UnityEngine.Debug.LogError("Error reading UnityWebRequest with a result of: " + www.result);
                }
                /*if (www.result == UnityWebRequest.Result.ConnectionError)
                {
                    UnityEngine.Debug.Log(www.error);
                    UnityEngine.Debug.LogError("Error reading UnityWebRequest with a result of: " + www.result);
                }*/
                else if (www.isDone)
                {
                    // Show results as text
                    UnityEngine.Debug.Log("XYTable file found online. Now trying to read it...");
                    UnityEngine.Debug.Log("www.ToString() : " + www.url);
                    //UnityEngine.Debug.Log("www.downloadHandler.text : " + www.downloadHandler.text);
                    //byte[] XYbyteArray = www.downloadHandler.data;
                    //string xytText = Convert.ToBase64String(XYbyteArray);
                    //UnityEngine.Debug.Log("xytText : " + xytText);
                    //string xytText = ItkFile.ReadAllText(www.downloadHandler.text);
                    string xytText = www.downloadHandler.text;
                    UnityEngine.Debug.Log("xytText : " + xytText);
                    UnityEngine.Debug.Log("XYTable: Parsing a string of length : " + xytText.Length);
                    float[] fValues = new float[_width * _height * 2];

                    // Determine if top down memory addressing is used or not
                    bool addrConventionTopDown = true; //Wether addressing convention of the file read is top down (true) or bottom up (false)
                    int indexConv = xytText.IndexOf("addressing convention:") + 23;
                    if (indexConv >= 0)
                    {
                        string xytConvention = xytText.Substring(indexConv, indexConv + 10);
                        if (xytConvention.Contains("top down")) addrConventionTopDown = true;
                        if (xytConvention.Contains("bottom up")) addrConventionTopDown = false;
                    }
                    else
                        addrConventionTopDown = true;  //Probably an old XYTable without mention of addressing and, thus, assuming it is top down.

                    //Split all values in a string array
                    char[] separators = { ' ', '\n' };
                    string xytTextWithoutHeader = xytText.Substring(xytText.IndexOf("end_header") + 12); //12=length of "end_header" + 2 characters (but why 2??)
                    string[] strValues = xytTextWithoutHeader.Split(separators);


                    bool botUp = _bottomUp ^ !addrConventionTopDown;
                    int xyi; //index for direct addressing
                    int xyibu; //index for bottom up addressing
                    for (int y = 0; y < _height; y++)
                        for (int x = 0; x < _width; x++)
                        {
                            xyi = (x + y * _width) * 2;
                            xyibu = (x + (_height - 1 - y) * _width) * 2;
                            float val = 0f;
                            if (float.TryParse(strValues[xyi], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val))
                                fValues[botUp ? xyibu : xyi] = val;
                            xyi++;
                            xyibu++;
                            val = 0f;
                            if (float.TryParse(strValues[xyi], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val))
                                fValues[botUp ? xyibu : xyi] = val;
                        }

                    // Old simple direct version, working correctly, but unable to handle bottomUp option
                    //for (int i = 0; i < fValues.Length; i++)
                    //{
                    //    float val = 0f;
                    //    string valfloat = strValues[i].Replace(",", "."); //OPTIM: can be commented out, it's just a useless precaution now.
                    //    if (float.TryParse(strValues[i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out val))
                    //        fValues[i] = val;
                    //}
                    _data = fValues;
                }
            }
        }

    }

    
}
