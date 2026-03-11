namespace GGemCo2DSkill
{
    /// <summary>
    /// skill / skill_monster 테이블을 공통 런타임 정의로 조회합니다.
    /// </summary>
    public static class SkillDefinitionResolver
    {
        public static bool TryResolve(int skillUid, out RuntimeSkillDefinition definition)
        {
            return TryResolve(skillUid, false, out definition);
        }

        public static bool TryResolve(int skillUid, bool preferMonsterTable, out RuntimeSkillDefinition definition)
        {
            definition = null;

            var manager = TableLoaderManagerSkill.Instance;
            if (manager == null)
                return false;

            if (preferMonsterTable)
            {
                if (TryResolveMonster(manager, skillUid, out definition)) return true;
                if (TryResolvePlayer(manager, skillUid, out definition)) return true;
                return false;
            }

            if (TryResolvePlayer(manager, skillUid, out definition)) return true;
            if (TryResolveMonster(manager, skillUid, out definition)) return true;
            return false;
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
