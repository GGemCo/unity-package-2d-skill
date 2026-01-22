using System;
using System.Collections.Generic;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 테스트 툴 전용 오버라이드 자산.
    /// - 테이블에 아직 반영되지 않은 스킬(개발 중 SkillDefinition) 연결
    /// - 몬스터별 skillId 목록을 merge/replace 가능
    /// </summary>
    [CreateAssetMenu(menuName = "GGemCo/Skills/Tool/Skill Test Override", fileName = "SkillTestOverride")]
    public sealed class SkillTestOverrideAsset : ScriptableObject
    {
        public enum MergeMode
        {
            Merge,
            Replace
        }

        [Serializable]
        public sealed class MonsterOverride
        {
            public int monsterUid;
            public MergeMode mode = MergeMode.Merge;

            [Header("Skill IDs (string)")]
            public List<string> skillIds = new();

            [Header("Dev Skills (ScriptableObjects)")]
            public List<SkillDefinition> devSkills = new();
        }

        [SerializeField] private List<MonsterOverride> overrides = new();

        public bool TryGetOverride(int monsterUid, out MonsterOverride ov)
        {
            for (int i = 0; i < overrides.Count; i++)
            {
                var x = overrides[i];
                if (x != null && x.monsterUid == monsterUid)
                {
                    ov = x;
                    return true;
                }
            }
            ov = null;
            return false;
        }
    }
}