#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace IthacaLib
{

    [CustomEditor(typeof(IthacaPipelineManager))]
    class IthacaPipelineManagerEditor : Editor
    {
        IthacaPipelineManager ithacaPlayer;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            ithacaPlayer = (IthacaPipelineManager)target;
            
            List<string> toExclude = new()
            {
                "m_Script"
            };
            if (ithacaPlayer.isPipelineCreated)
            {
                toExclude.Add("importSettings");
                toExclude.Add("loop");
                toExclude.Add("autoPlay");
                toExclude.Add("renderType");
                toExclude.Add("camConfigs");
                //toExclude.Add("metadata");
                toExclude.Add("useTexture3DFusion");
                toExclude.Add("xyTablePath");

            }
            else
            {
                if (ithacaPlayer.importSettings)
                {
                    toExclude.Add("camConfigs");
                }
                else
                {
                    toExclude.Add("metadata");
                }
            }
            DrawPropertiesExcluding(serializedObject, toExclude.ToArray());
            if(!ithacaPlayer.isPipelineCreated)
                if (GUILayout.Button("Create playback pipeline"))
                {
                ithacaPlayer.ConstructPipeline();
                }

            serializedObject.ApplyModifiedProperties();

        }
    }
}
#endif
