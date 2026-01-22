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
                public static string Skill =>
                    ConfigAddressablePath.Combine(
                        ConfigAddressablePath.Images.Icon.RootIcon,
                        "Skill");
            }
        }
    }
}