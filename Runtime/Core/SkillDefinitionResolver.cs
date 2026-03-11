using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// <c>skill</c> 및 <c>skill_monster</c> 테이블의 스킬 데이터를 공통 런타임 정의로 변환해 조회하는 해석기입니다.
    /// 테이블 출처에 따라 플레이어 또는 몬스터 스킬 정의를 선택적으로 반환합니다.
    /// </summary>
    public static class SkillDefinitionResolver
    {
        /// <summary>
        /// 플레이어 스킬 테이블을 기본 출처로 사용하여 스킬 정의 조회를 시도합니다.
        /// </summary>
        /// <param name="skillUid">조회할 스킬의 고유 식별자입니다.</param>
        /// <param name="definition">조회에 성공한 런타임 스킬 정의입니다.</param>
        /// <returns>스킬 정의를 성공적으로 조회하면 <see langword="true"/>, 실패하면 <see langword="false"/>를 반환합니다.</returns>
        public static bool TryResolve(int skillUid, out RuntimeSkillDefinition definition)
        {
            return TryResolve(skillUid, ConfigCommon.SkillTableSource.Player, out definition);
        }

        /// <summary>
        /// 지정한 테이블 출처를 기준으로 스킬 정의 조회를 시도합니다.
        /// </summary>
        /// <param name="skillUid">조회할 스킬의 고유 식별자입니다.</param>
        /// <param name="source">스킬을 조회할 테이블 출처입니다.</param>
        /// <param name="definition">조회에 성공한 런타임 스킬 정의입니다.</param>
        /// <returns>스킬 정의를 성공적으로 조회하면 <see langword="true"/>, 실패하면 <see langword="false"/>를 반환합니다.</returns>
        public static bool TryResolve(int skillUid, ConfigCommon.SkillTableSource source, out RuntimeSkillDefinition definition)
        {
            definition = null;

            var manager = TableLoaderManagerSkill.Instance;
            if (manager == null)
                return false;

            return source == ConfigCommon.SkillTableSource.Monster
                ? TryResolveMonster(manager, skillUid, out definition)
                : TryResolvePlayer(manager, skillUid, out definition);
        }

        /// <summary>
        /// 플레이어 스킬 테이블에서 스킬 UID에 해당하는 항목을 조회하고 런타임 스킬 정의로 변환합니다.
        /// </summary>
        /// <param name="manager">스킬 테이블 접근에 사용할 테이블 로더 매니저입니다.</param>
        /// <param name="skillUid">조회할 플레이어 스킬의 고유 식별자입니다.</param>
        /// <param name="definition">조회에 성공한 런타임 스킬 정의입니다.</param>
        /// <returns>플레이어 스킬 정의를 성공적으로 변환하면 <see langword="true"/>, 실패하면 <see langword="false"/>를 반환합니다.</returns>
        private static bool TryResolvePlayer(TableLoaderManagerSkill manager, int skillUid, out RuntimeSkillDefinition definition)
        {
            definition = null;
            var playerTable = manager.TableSkill;
            if (playerTable == null || !playerTable.GetDatas().TryGetValue(skillUid, out var playerSkill) || playerSkill == null)
                return false;

            definition = RuntimeSkillDefinition.From(playerSkill);
            return true;
        }

        /// <summary>
        /// 몬스터 스킬 테이블에서 스킬 UID에 해당하는 항목을 조회하고 런타임 스킬 정의로 변환합니다.
        /// </summary>
        /// <param name="manager">스킬 테이블 접근에 사용할 테이블 로더 매니저입니다.</param>
        /// <param name="skillUid">조회할 몬스터 스킬의 고유 식별자입니다.</param>
        /// <param name="definition">조회에 성공한 런타임 스킬 정의입니다.</param>
        /// <returns>몬스터 스킬 정의를 성공적으로 변환하면 <see langword="true"/>, 실패하면 <see langword="false"/>를 반환합니다.</returns>
        private static bool TryResolveMonster(TableLoaderManagerSkill manager, int skillUid, out RuntimeSkillDefinition definition)
        {
            definition = null;
            var monsterTable = manager.TableSkillMonster;
            if (monsterTable == null || !monsterTable.GetDatas().TryGetValue(skillUid, out var monsterSkill) || monsterSkill == null)
                return false;

            definition = RuntimeSkillDefinition.From(monsterSkill);
            return true;
        }
    }
}