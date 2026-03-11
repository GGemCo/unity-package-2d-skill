using Config;

namespace GGemCo2DSkill
{
    /// <summary>
    /// skill / skill_monster 테이블을 공통 런타임 정의로 조회합니다.
    /// </summary>
    public static class SkillDefinitionResolver
    {
        public static bool TryResolve(int skillUid, out RuntimeSkillDefinition definition)
        {
            return TryResolve(skillUid, ConfigCommonSkill.SkillTableSource.Player, out definition);
        }

        public static bool TryResolve(int skillUid, ConfigCommonSkill.SkillTableSource source, out RuntimeSkillDefinition definition)
        {
            definition = null;

            var manager = TableLoaderManagerSkill.Instance;
            if (manager == null)
                return false;

            return source == ConfigCommonSkill.SkillTableSource.Monster
                ? TryResolveMonster(manager, skillUid, out definition)
                : TryResolvePlayer(manager, skillUid, out definition);
        }

        private static bool TryResolvePlayer(TableLoaderManagerSkill manager, int skillUid, out RuntimeSkillDefinition definition)
        {
            definition = null;
            var playerTable = manager.TableSkill;
            if (playerTable == null || !playerTable.GetDatas().TryGetValue(skillUid, out var playerSkill) || playerSkill == null)
                return false;

            definition = RuntimeSkillDefinition.From(playerSkill);
            return true;
        }

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
