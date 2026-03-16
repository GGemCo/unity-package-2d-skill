using System.Collections.Generic;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    public static class ConfigAddressableSettingSkill
    {
        public static readonly AddressableAssetInfo SkillSettings = ConfigAddressableSetting.Make(nameof(SkillSettings));

        public static readonly List<AddressableAssetInfo> NeedLoadInLoadingScene = new()
        {
            SkillSettings,
        };
    }
}
