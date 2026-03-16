#if UNITY_EDITOR
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Unity Editor에서 Play Mode 진입 시 SkillTestRuntimeHub가 필요하면 자동 생성되도록 보장합니다.
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

            var settings = AssetDatabase.LoadAssetAtPath<GGemCoSkillSettings>(ConfigAddressableSettingSkill.SkillSettings.Path);
            SkillSettingsRuntime.SetForEditor(settings);

            if (settings != null && !settings.enableSkillTestRuntimeBridgeAutoSpawn)
                return;

            SkillTestRuntimeHubEditorFacade.EnsureHubExists();
        }
    }
}
#endif
