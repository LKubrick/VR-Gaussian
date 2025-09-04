/*#############################################################################################
#           ----   MaterialRenderer   ----
# TARGET: (RenderTexture(PosMap), RenderTexture(Color)) --> Rendering with Materials
#
# ---------------------------------------------------------------------------------------------
# Project: IthacaLib
# Resp. : Quentin
# Author(s): 
#    - Mathieu Genoud (mathieu.ftf@gmail.com) and Quentin de Cagny (quentindecagny@gmail.com) - French Touch Factory (67%)
#    - Phil Hoffmann (phil.hoffmann@ipk.fraunhofer.de) (33%)
#   Note: apparently, according to the agreement, we need to find a percentage breakdown for 
#   each co-developed module. I'm proposing these numbers, just to start doing it, but it's 
#   open to discussion.
# Date : created in december 2023
# Description: Renders a point cloud using a ShaderGraph material. "Forked" from VFXGraphRenderer.
               renderingMaterial needs to have a "positions", a "colors" and a "alignMat" property
               to work properly. Initially made and tested with ShaderGraph and URP using the 
               "SurfaceHelper.hlsl" by Keijiro Takahashi for AKVFX.
               Might need testing with a hand-coded shader. 
# Parameters : Two RenderTextures, first contains the positions encoded in a ARGBFloat texture
#              and the second contains the colors in ARGB32 format.
#              See the CaptureToVfxConverter bridge for more details.
# Note : - 
# Tests : -
# Versions : 
#   (29/02/24) : merged normal and for android versions
#############################################################################################*/

using UnityEngine;
using Holo;
using System.Collections.Generic;

namespace IthacaLib
{
    public class MaterialRenderer : TargetModuleBase<GPUPointCloud>
    {
        [Tooltip("A Material like SurfaceMat.mat that exposes positions, colors and alignMat properties.")]
        public Material renderingMaterial = null;



        [Header(" - Alignment Matrix (.json file path) - ")]
        [Space(-2)]
        [Tooltip("Full local path of the Data directory, where the sequences are stored. " +
            "Example: D:\\Projects\\HoloUnity\\Data\\")]
        public string dataPath;

        [Space(-3)]
        [Tooltip("Name of the sequence. Will be used to compose the path of the alignment matrix. " +
            "Example: marcelmire720r .")]
        public string seqName;

        [Space(-3)]
        [Tooltip("Filename of the matrix with its path inside sequence directory structure. Will be used to compose " +
            "the path of the matrix. Example: align\\cams\\alignmatrixD_3.json (extension can be forgotten, it is added internaly)." +
            " - NO_CAM_INDEX_FOUND ? : add the user index of the cam to the gameObject's name.")]
        public string camAlignMatrix;


        private Matrix4x4 alignmentMatrix = Matrix4x4.identity;

        [Header(" - Common Matrix + coord system transformers - ")]
        [Space(-2)]
        [Tooltip("The alignment matrix is modified by this common Matrix. To be used for a global transformation of the subject.")]
        public Matrix4x4 commonMatrix = Matrix4x4.identity;

        //For future version
        //[Tooltip("Selector between Depth or Color sensor for the sensor on which the alignment matrix where calculated. Usually, the D or C at the end of the json matrix name is an indication about that.")]
        //public bool colorAlign = false;
        public int RezDivider = 1;
        [Tooltip("Mirror on Y axis. (for Unity, prefer true)")]
        public bool mirrorY = false;
        [Tooltip("Normals should be inverted when mirrorY is true, unless you know what you're doing.")]
        public bool inverseNormals = false;
        [Tooltip("Scale from millimeter (Kinect world) to meter (Unity world), thus scales everything by 0.001. " +
            "Unity also needs mirroring on Y axis compared to Kinect, so set mirrorY to true above if you want the " +
            "real truth.")]
        public bool meterScale = false;

        private MeshRenderer materialComponent = null;
        private MeshFilter meshComponent = null;
        private ShaderPositionAndColorBinder binderPC = null;
        private ShaderAlignMatrixBinder binderAlign = null;

        //DEBUG TOOLS
        [Space(10)]
        [Header("Debug tools")]
        [Tooltip("Useful debug option that transfer the alignment to the transform of this gameObject,"
            + "thus matching the (0,0,0) position of this GameObject to the camera used during recording.")]
        public bool directTransform = false;

        #region Unity framework logic
        protected override void Start()
        {
            if (settingsByContext)
                GetSettingsByContext();

            base.Start();
        }

        protected override void OnValidate()
        {
            if (settingsByContext)
                GetSettingsByContext();

            base.OnValidate();
        }

        //string GetMyCamIndex()
        //{
        //    var indexOfOpen = this.gameObject.name.LastIndexOf("(");
        //    var indexOfClose = this.gameObject.name.LastIndexOf(")");
        //    string number = "";
        //    if (indexOfOpen >= 0 && indexOfClose >= 0)
        //        number = this.gameObject.name.Substring(indexOfOpen + 1, indexOfClose - indexOfOpen - 1);

        //    return int.TryParse(number, out var index) ? index.ToString() : "NO_CAM_INDEX_FOUND";
        //}
        #endregion

        //TODO: mutualize this function with VfxGraphRenderer
        public override void GetSettingsByContext()
        {
            FindContextInstance();
            if(ContextManager.instance == null) return;
            var ctxt = ContextManager.instance.currentContext;
            if (ctxt.AppConfig.LocalDataFolder != "")
                dataPath = ctxt.AppConfig.LocalDataFolder;
            if (ctxt.AppConfig.Sequence != "")
                seqName = ctxt.AppConfig.Sequence;
            if (ctxt.AppConfig.AlignBaseName != "")
                camAlignMatrix = ctxt.AppConfig.AlignFolder + ctxt.AppConfig.AlignBaseName + Helper.GetCamIndexString(this.gameObject) + ".json";
            if (ctxt.AppConfig.CommonTransformMatrix != Matrix4x4.zero)
                commonMatrix = ctxt.AppConfig.CommonTransformMatrix;
        }

        private Mesh InitMesh(int _width, int _height)
        {
            _width = _width / RezDivider;
            _height = _height / RezDivider;
            List<Vector3> verts = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> tris = new List<int>();
            Mesh mesh = new Mesh();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            int trisIndex = 0;
            int vertsIndex = 0;
            for (int y = 0; y < _height + 1; y++)
            {
                for (int x = 0; x < _width + 1; x++, vertsIndex++, trisIndex += 6)
                {
                    verts.Add(new Vector3(x, 0, y));
                    uvs.Add(new Vector3((float)x / _width, (float)y / _height));
                }
            }

            int i = 0;
            if (!inverseNormals)
                for (int y = 0; y < _height; y++, i++)
                {
                    for (int x = 0; x < _width; x++, i++)
                    {
                        tris.Add(i);
                        tris.Add(i + 1);
                        tris.Add(i + _width + 1);

                        tris.Add(i + 1);
                        tris.Add(i + _width + 2);
                        tris.Add(i + _width + 1);
                    }
                }
            else
                for (int y = 0; y < _height; y++, i++)
                {
                    for (int x = 0; x < _width; x++, i++)
                    {
                        tris.Add(i);
                        tris.Add(i + _width + 1);
                        tris.Add(i + 1);

                        tris.Add(i + 1);
                        tris.Add(i + _width + 1);
                        tris.Add(i + _width + 2);
                    }
                }

            mesh.SetVertices(verts);
            mesh.SetIndices(tris, MeshTopology.Triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            //TODO: make a smarter bounds calculation. This trick is useful to avoid clipping of meshes when camera moves,
            //but the dimensions baked in these bounds are a bit dirty. When a convex hull will be calculated, it could be 
            //used to make proper bounds.
            float scale = 1f;
            if (meterScale) scale = 0.001f;
            mesh.bounds = new Bounds(Vector3.forward * 3000 * scale, new Vector3(12000, 12000, 12000) * scale);
            //mesh.RecalculateBounds();

            return mesh;
        }
        public override void HotInit(DataEventArgs<GPUPointCloud> e)
        {
            GPUPointCloud initPC = e.Output;
            if (initPC.positionMap == null || initPC.colorMap == null) return;

            alignmentMatrix = SetAlignment();

            ////DEBUG "directTransform"
            //if (directTransform)
            //{
            //    transform.localPosition = alignmentMatrix.GetPosition();
            //    transform.localRotation = alignmentMatrix.rotation;
            //    transform.localScale = alignmentMatrix.lossyScale;
            //    alignmentMatrix = Matrix4x4.identity;
            //}
            //else
            //{
            //    transform.localPosition = Vector3.zero;
            //    transform.localRotation = Quaternion.identity;
            //    transform.localScale = Vector3.one;
            //}
            //// END OF DEBUG "directTransform"

            if (materialComponent == null)
                materialComponent = gameObject.AddComponent<MeshRenderer>();

            materialComponent.material = renderingMaterial;

            if (meshComponent == null)
                meshComponent = gameObject.AddComponent<MeshFilter>();

            meshComponent.mesh = InitMesh(initPC.positionMap.width, initPC.positionMap.height);
            Debug.Log("Init mesh : " + initPC.positionMap.width + "x" + initPC.positionMap.height);


            if (binderPC == null)
            {
                binderPC = gameObject.AddComponent<ShaderPositionAndColorBinder>();
            }
            if (binderAlign == null)
            {
                binderAlign = gameObject.AddComponent<ShaderAlignMatrixBinder>();
            }

            binderPC.positionsProperty = "positions";
            binderPC.colorProperty = "colors";

            binderAlign.alignMatProperty = "alignMat";


            binderAlign.alignMat = alignmentMatrix;
            binderAlign.UpdateBinding(materialComponent.material);


            base.HotInit(e);
        }

        public override void ProcessData(object sender, DataEventArgs<GPUPointCloud> e)
        {
            if (!enabled || e == null) return;
            if (!IsHotInitComplete)
                HotInit(e);

            binderPC.positions = e.Output.positionMap;
            binderPC.colors = e.Output.colorMap;
            binderPC.UpdateBinding(materialComponent.material);

        }

        //TODO: this code is the same as in VfxGraphRenderer, a mutualisation has to be done
        private Matrix4x4 SetAlignment()
        {

            string path = "Not found";
            HoloMatrix4x4 mat = new HoloMatrix4x4();
            try
            {
                path = HoloMatrix4x4.GetFullPathFromDecomposedPath(dataPath, seqName, camAlignMatrix);
                mat.ReadFromDisk(path);
            }
            catch (System.Exception e)
            {
                camAlignMatrix = "NOT FOUND. Identity instead.";
                Debug.LogWarning("ShaderGraphRenderer: SetAlignment: generic exception, probably file does not exists, message= " + e.Message);
            }


            //Common transformation
            Matrix4x4 finalCommonMatrix = commonMatrix;
            if (meterScale)
            {
                Matrix4x4 scaleUnityMatrix = Matrix4x4.Scale(Vector3.one * 0.001f);
                finalCommonMatrix = scaleUnityMatrix * finalCommonMatrix;
            }
            finalCommonMatrix.m11 = mirrorY ? -finalCommonMatrix.m11 : finalCommonMatrix.m11;


            return finalCommonMatrix * mat.matrix4x4;
        }


    }


    public class ShaderPositionAndColorBinder : MonoBehaviour
    {
        public string positionsProperty;
        public string colorProperty;

        public RenderTexture positions;
        public RenderTexture colors;


        public void UpdateBinding(Material component)
        {
            component.SetTexture(positionsProperty, positions);
            component.SetTexture(colorProperty, colors);
        }
    }


    public class ShaderAlignMatrixBinder : MonoBehaviour
    {

        public string alignMatProperty;

        public Matrix4x4 alignMat;


        public void UpdateBinding(Material component)
        {
            component.SetMatrix(alignMatProperty, alignMat);
        }
    }
}

