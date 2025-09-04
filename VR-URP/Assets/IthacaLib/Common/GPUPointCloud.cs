using UnityEngine;

namespace IthacaLib
{
    public class GPUPointCloud
    {
        //public SortedDictionary<RenderTextureType, RenderTexture> RenderTextures;
        //public List<RenderStream> RenderTextures;
        public RenderTexture colorMap;
        public RenderTexture positionMap;
        public RenderTexture irMap;
        public RenderTexture normalMap;
        public RenderTexture weightMap;
        public Matrix4x4 appliedMatrix = Matrix4x4.identity;


        public GPUPointCloud() { }
        public GPUPointCloud(GPUPointCloud gPUPointCloud)
        {
            colorMap = new(gPUPointCloud.colorMap.descriptor)
            {
                graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm, // copy by descriptor forces the graphics format to R8G8B8A8_SRGB for some reason
            };
            Graphics.CopyTexture(gPUPointCloud.colorMap, colorMap);
            positionMap = new(gPUPointCloud.positionMap.descriptor);
            Graphics.CopyTexture(gPUPointCloud.positionMap, positionMap);
            if (gPUPointCloud.irMap != null)
            {
                irMap = new(gPUPointCloud.irMap.descriptor);
                Graphics.CopyTexture(gPUPointCloud.irMap, irMap);

            }
            if (gPUPointCloud.normalMap != null)
            {
                normalMap = new(gPUPointCloud.normalMap.descriptor);
                Graphics.CopyTexture(gPUPointCloud.normalMap, normalMap);

            }
            if (gPUPointCloud.weightMap != null)
            {
                weightMap = new(gPUPointCloud.weightMap.descriptor);
                Graphics.CopyTexture(gPUPointCloud.weightMap, weightMap);

            }
            if(gPUPointCloud.appliedMatrix != Matrix4x4.identity)
            {
                appliedMatrix = gPUPointCloud.appliedMatrix;
            }
        }
        public void Dispose()
        {
            if (colorMap != null)
            {
                Object.Destroy(colorMap);
            }
            if(positionMap != null)
            {
                Object.Destroy(positionMap);
            }
            if (irMap != null)
            {
                Object.Destroy(irMap);
            }
            if (normalMap != null)
            {
                Object.Destroy(normalMap);
            }
            if (weightMap != null)
            {
                Object.Destroy(weightMap);
            }
        }
    }

    //public struct RenderStream
    //{
    //    public RenderTextureType type;
    //    public RenderTexture rt;
    //    //more metadata can be added here
    //}
    //public enum RenderTextureType
    //{
    //    ColorMap,
    //    DepthMap,
    //    IRMap
    //    //... this can and will be made longer as different types of render textures will be deemed necessary
    //}
}
