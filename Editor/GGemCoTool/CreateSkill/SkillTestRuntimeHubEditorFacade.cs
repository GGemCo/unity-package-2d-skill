#if UNITY_EDITOR
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    internal static class SkillTestRuntimeHubEditorFacade
    {
        private const string HubObjectName = "SkillTestRuntimeHub";

        public static SkillTestRuntimeHub FindHub()
        {
            return SkillTestRuntimeHub.Instance != null
                ? SkillTestRuntimeHub.Instance
                : Object.FindFirstObjectByType<SkillTestRuntimeHub>();
        }

        public static bool TryGetHub(out SkillTestRuntimeHub hub, out string error)
        {
            hub = FindHub();
            if (hub != null)
            {
                error = null;
                return true;
            }

            error = "SkillTestRuntimeHub를 찾지 못했습니다. (Play Mode 진입 시 자동 생성되어야 합니다.)";
            return false;
        }

        public static SkillTestRuntimeHub EnsureHubExists()
        {
            var hub = FindHub();
            if (hub != null)
                return hub;

            var go = GameObject.Find(HubObjectName) ?? new GameObject(HubObjectName);
            hub = go.GetComponent<SkillTestRuntimeHub>();
            if (hub == null)
                hub = go.AddComponent<SkillTestRuntimeHub>();

            return hub;
        }
    }
}
#endif
