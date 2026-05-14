using GGemCo2DCore;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Skill 패키지용 에디터 테이블 로더.
    /// - Addressables 경로 기준으로 txt 를 로드합니다. (Editor 전용)
    /// - 런타임 TableLoaderManagerSkill(씬 오브젝트)와는 별개입니다.
    /// </summary>
    public static class TableLoaderManagerSkill
    {
        public static TableSkill LoadTableSkill(bool forceReload = true)
        {
            return TableLoaderManagerBase.LoadTable<TableSkill>(ConfigAddressableTableSkill.TableSkill.Path, forceReload);
        }
        public static TableSkillPassive LoadTableSkillPassive(bool forceReload = true)
        {
            return TableLoaderManagerBase.LoadTable<TableSkillPassive>(ConfigAddressableTableSkill.TableSkillPassive.Path, forceReload);
        }

        public static TableSkillPassiveOption LoadTableSkillPassiveOption(bool forceReload = true)
        {
            return TableLoaderManagerBase.LoadTable<TableSkillPassiveOption>(ConfigAddressableTableSkill.TableSkillPassiveOption.Path, forceReload);
        }
        public static TableSkillMonster LoadTableSkillMonster(bool forceReload = true)
        {
            return TableLoaderManagerBase.LoadTable<TableSkillMonster>(ConfigAddressableTableSkill.TableSkillMonster.Path, forceReload);
        }

        /// <summary>
        /// 스킬 차징 단계 테이블을 로드합니다.
        /// </summary>
        /// <param name="forceReload">기존 캐시를 무시하고 다시 로드할지 여부입니다.</param>
        /// <returns>로드된 차징 단계 테이블입니다.</returns>
        public static TableSkillChargeStage LoadTableSkillChargeStage(bool forceReload = true)
        {
            return TableLoaderManagerBase.LoadTable<TableSkillChargeStage>(ConfigAddressableTableSkill.TableSkillChargeStage.Path, forceReload);
        }

        /// <summary>
        /// Core 패키지 테이블을 논리 이름으로 로드합니다. (예: "stat", "state", "damage_type")
        /// </summary>
        public static TTable LoadCoreTable<TTable>(string tableName, bool forceReload = false)
            where TTable : class, ITableParser, new()
        {
            var info = ConfigAddressableTable.Make(tableName);
            return TableLoaderManagerBase.LoadTable<TTable>(info.Path, forceReload);
        }
    }
}
