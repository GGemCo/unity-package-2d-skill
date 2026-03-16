using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Skill 패키지 설정 런타임 접근기입니다.
    /// AddressableLoaderSettingsRegist의 외부 설정 등록 지점을 통해 초기화됩니다.
    /// </summary>
    public static class SkillSettingsRuntime
    {
        public static GGemCoSkillSettings Current { get; private set; }

        public static bool IsSkillDebugEnabled => Current == null || Current.enableSkillDebug;
        public static bool IsDamageAreaGizmoEnabled => IsSkillDebugEnabled && (Current == null || Current.enableDamageAreaGizmo);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterExternalSetting()
        {
            Current = null;

            AddressableLoaderSettingsRegist.SettingsRegistry.Register(
                id: "skill.settings",
                key: ConfigAddressableSettingSkill.SkillSettings.Key,
                onLoaded: OnLoaded);
        }

        private static void OnLoaded(Object loaded)
        {
            Current = loaded as GGemCoSkillSettings;
        }

#if UNITY_EDITOR
        public static void SetForEditor(GGemCoSkillSettings settings)
        {
            Current = settings;
        }
#endif
    }
}
