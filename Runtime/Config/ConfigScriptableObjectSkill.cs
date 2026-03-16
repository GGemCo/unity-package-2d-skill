using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Skill 패키지 ScriptableObject 메뉴 정의입니다.
    /// </summary>
    public static class ConfigScriptableObjectSkill
    {
        public enum SkillLocalOrder
        {
            SkillSettings = 0,
        }

        public const string BasePath = ConfigDefine.NameSDK + "/Settings/";
        public const string BaseName = ConfigDefine.NameSDK;

        public static class Skill
        {
            public const string FileName = BaseName + "SkillSettings";
            public const string MenuName = BasePath + FileName;
            public const int Ordering =
                (int)ConfigScriptableObjectCommon.PackageOrder.Skill +
                (int)SkillLocalOrder.SkillSettings;
        }
    }
}
