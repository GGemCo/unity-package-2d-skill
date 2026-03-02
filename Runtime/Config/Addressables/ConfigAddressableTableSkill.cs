using System.Collections.Generic;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    public static class ConfigAddressableTableSkill
    {
        public const string Skill = "skill";
        public const string SkillOption = "skill_option";

        public static readonly AddressableAssetInfo TableSkill =
            ConfigAddressableTable.Make(Skill);


        public static readonly AddressableAssetInfo TableSkillOption =
            ConfigAddressableTable.Make(SkillOption);

        public static readonly List<AddressableAssetInfo> All = new()
        {
            TableSkill,
            TableSkillOption,
        };
    }
}