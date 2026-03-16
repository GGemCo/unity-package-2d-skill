#if UNITY_EDITOR
using UnityEditor;
using GGemCo2DSkill;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Play Mode 진입 시 SkillTestRuntimeHub가 존재하도록 보장하는 Editor 유틸리티
    /// </summary>
    [InitializeOnLoad]
    internal static class SkillTestRuntimeHubAutoSpawner
    {
        static SkillTestRuntimeHubAutoSpawner()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;

            var settings = AssetDatabase.LoadAssetAtPath<GGemCoSkillSettings>(
                ConfigAddressableSettingSkill.SkillSettings.Path);

            if (settings != null && !settings.enableSkillTestRuntimeBridgeAutoSpawn)
                return;

            SkillTestRuntimeHubEditorFacade.EnsureHubExists();
        }
    }
}
#endif