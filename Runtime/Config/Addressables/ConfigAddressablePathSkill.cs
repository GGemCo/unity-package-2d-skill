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

            public static class RuntimeSequence
            {
                private static string Root => ConfigAddressablePath.Combine(RootSkill, "RuntimeSequences");
                public static string Player => ConfigAddressablePath.Combine(Root, "Player");
                public static string Monster => ConfigAddressablePath.Combine(Root, "Monster");
            }
        }

    }
}