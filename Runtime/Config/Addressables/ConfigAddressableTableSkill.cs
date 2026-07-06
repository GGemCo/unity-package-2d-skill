using System.Collections.Generic;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Skill 패키지에서 사용하는 Addressables 테이블 리소스와 런타임 pack 정의를 관리합니다.
    /// </summary>
    public static class ConfigAddressableTableSkill
    {
        /// <summary>
        /// Skill 런타임 테이블 pack 식별자입니다.
        /// </summary>
        public const string PackageId = "skill";

        public const string Skill = "skill";
        public const string SkillPassive = "skill_passive";
        public const string SkillPassiveOption = "skill_passive_option";
        public const string SkillMonster = "skill_monster";
        public const string SkillChargeStage = "skill_charge_stage";

        public static readonly AddressableAssetInfo TableSkill =
            ConfigAddressableTable.Make(Skill);

        public static readonly AddressableAssetInfo TableSkillPassive =
            ConfigAddressableTable.Make(SkillPassive);

        public static readonly AddressableAssetInfo TableSkillPassiveOption =
            ConfigAddressableTable.Make(SkillPassiveOption);

        public static readonly AddressableAssetInfo TableSkillMonster =
            ConfigAddressableTable.Make(SkillMonster);

        public static readonly AddressableAssetInfo TableSkillChargeStage =
            ConfigAddressableTable.Make(SkillChargeStage);

        /// <summary>
        /// Skill 패키지 런타임 테이블 pack Addressables 자산 정보입니다.
        /// </summary>
        public static readonly AddressableAssetInfo TablePackSkill =
            ConfigAddressableTablePack.Make(PackageId);

        /// <summary>
        /// Skill 패키지에서 로드해야 하는 개별 테이블 목록입니다.
        /// </summary>
        public static readonly List<AddressableAssetInfo> All = new()
        {
            TableSkill,
            TableSkillPassive,
            TableSkillPassiveOption,
            TableSkillMonster,
            TableSkillChargeStage,
        };
    }
}
