using System.Collections.Generic;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    public static class ConfigAddressableTableSkill
    {
        public const string Skill = "skill";
        public const string SkillPassive = "skill_passive";
        public const string SkillPassiveOption = "skill_passive_option";
        public const string SkillMonster = "skill_monster";

        public static readonly AddressableAssetInfo TableSkill =
            ConfigAddressableTable.Make(Skill);

        public static readonly AddressableAssetInfo TableSkillPassive =
            ConfigAddressableTable.Make(SkillPassive);

        public static readonly AddressableAssetInfo TableSkillPassiveOption =
            ConfigAddressableTable.Make(SkillPassiveOption);

        public static readonly AddressableAssetInfo TableSkillMonster =
            ConfigAddressableTable.Make(SkillMonster);

        public static readonly List<AddressableAssetInfo> All = new()
        {
            TableSkill,
            TableSkillPassive,
            TableSkillPassiveOption,
            TableSkillMonster,
        };

    }
}