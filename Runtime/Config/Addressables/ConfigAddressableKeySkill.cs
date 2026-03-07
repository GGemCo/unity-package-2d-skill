using GGemCo2DCore;

namespace GGemCo2DSkill
{
    public static class ConfigAddressableKeySkill
    {
        public const string SkillIcon = ConfigDefine.NameSDK + "_Skill_Icon";
        public const string SkillPassiveIcon = ConfigDefine.NameSDK + "_SkillPassive_Icon";

        public static string GetRuntimeSequenceKey(int uid)
        {
            return $"{ConfigDefine.NameSDK}_Skill_RuntimeSequence_{uid}";
        }
    }
}