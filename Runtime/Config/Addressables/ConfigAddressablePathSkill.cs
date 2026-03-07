using GGemCo2DCore;

namespace GGemCo2DSkill
{
    public static class ConfigAddressablePathSkill
    {
        // -------------------------
        // Images (Icons, Parts, etc.)
        // -------------------------
        public static class Images
        {
            public static class Icon
            {
                public static string Skill => ConfigAddressablePath.Combine(ConfigAddressablePath.Images.Icon.RootIcon, "Skill");
                public static string SkillPassive => ConfigAddressablePath.Combine(ConfigAddressablePath.Images.Icon.RootIcon, "SkillPassive");
            }
        }

        public static class Skill
        {
            /// <summary>Assets/{SDK}/DataAddressable/Skill</summary>
            private static string RootSkill => ConfigAddressablePath.Combine(ConfigAddressablePath.Root, "Skill");
            public static string RuntimeSequences => ConfigAddressablePath.Combine(RootSkill, "RuntimeSequences");
        }

    }
}