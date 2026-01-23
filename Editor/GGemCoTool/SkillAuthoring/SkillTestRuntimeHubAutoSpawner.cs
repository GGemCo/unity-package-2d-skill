#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Play Mode 진입 시 SkillTestRuntimeHub가 씬에 없으면 자동 생성/배치합니다.
    /// </summary>
    [InitializeOnLoad]
    internal static class SkillTestRuntimeHubAutoSpawner
    {
        private const string HubObjectName = "SkillTestRuntimeHub";

        static SkillTestRuntimeHubAutoSpawner()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode)
                return;

            EnsureHubExists();
        }

        private static void EnsureHubExists()
        {
            var hub = Object.FindFirstObjectByType<SkillTestRuntimeHub>();
            if (hub != null) return;

            var go = GameObject.Find(HubObjectName);
            if (go == null) go = new GameObject(HubObjectName);

            if (go.GetComponent<SkillTestRuntimeHub>() == null)
                go.AddComponent<SkillTestRuntimeHub>();
        }
    }
}
#endif
