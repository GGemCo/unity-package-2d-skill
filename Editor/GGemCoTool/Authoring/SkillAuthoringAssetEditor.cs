// Assets/GGemCo/Skills/Editor/Authoring/SkillAuthoringAssetEditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    [CustomEditor(typeof(SkillAuthoringAsset))]
    public sealed class SkillAuthoringAssetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var authoring = (SkillAuthoringAsset)target;

            GUILayout.Space(10f);
            using (new EditorGUI.DisabledScope(authoring == null))
            {
                if (GUILayout.Button("Bake Timeline → SkillDefinition", GUILayout.Height(28)))
                {
                    try
                    {
                        SkillTimelineBaker.Bake(authoring);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogException(ex);
                    }
                }
            }
        }
    }
}
#endif