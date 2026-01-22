using System.Collections.Generic;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    public static class ConfigAddressableTableSkill
    {
        public const string Skill = "Skill";

        public static readonly AddressableAssetInfo TableSkill =
            ConfigAddressableTable.Make(Skill);

        public static readonly List<AddressableAssetInfo> All = new()
        {
            TableSkill,
        };
    }
}