using UnityEngine;

#if USINGVFXGRAPH
using System;
using UnityEngine.VFX;

namespace IthacaLib.Common
{
    [Serializable]
    public struct VfxGraphCapacity
    {
        public int capacity;
        public VisualEffectAsset graph;

        public static VfxGraphCapacity Empty()
        {
            VfxGraphCapacity instance;
            instance.capacity = int.MaxValue;
            instance.graph = null;
            return instance;
        }
    }

    [CreateAssetMenu(fileName = "VfxAssetCollection", menuName = "ScriptableObjects/VfxAssetCollection")]
    public class VfxAssetCollection : ScriptableObject
    {
        public VfxGraphCapacity[] graphs;

        public VisualEffectAsset GetBestGraph(int capacity)
        {
            Debug.Log("graphs.Length= " + graphs.Length);
            if (graphs == null || graphs.Length == 0) return null;

            var bestInfo = VfxGraphCapacity.Empty();

            foreach (var graph in graphs)
            {
                if (graph.capacity >= capacity && graph.capacity < bestInfo.capacity)
                {
                    bestInfo = graph;
                    Debug.Log("VFX graph capacity : " + graph.capacity);
                }
                else
                {
                    Debug.Log("capacity " + capacity);
                    Debug.Log("graph.capacity " + graph.capacity);
                }
            }

            return bestInfo.graph;
        }
    }
}
#else
namespace IthacaLib.Common
{
    [CreateAssetMenu(fileName = "VfxAssetCollection", menuName = "ScriptableObjects/VfxAssetCollection")]
    public class VfxAssetCollection : ScriptableObject
    {
        [TextArea(2, 2)]
        [SerializeField]
        string ________moduleInfos = "Visual Effect Graph is NOT AVAILABLE on this environment,\r\n" +
            " please install it and define #USINGVFXGRAPH symbol. ";
    }
}
#endif