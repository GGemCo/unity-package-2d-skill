using System;
using System.Collections.Generic;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 옵션(패시브/확장 스킬 효과) 테이블 Row.
    /// - 하나의 스킬은 여러 옵션 레코드를 가질 수 있다.
    /// - TableSkill.OptionGroupUid 로 그룹을 묶고, Level 별로 옵션을 분기한다.
    /// </summary>
    public sealed class StruckTableSkillOption
    {
        /// <summary>옵션 그룹 UID (TableSkill.OptionGroupUid)</summary>
        public string OptionGroupUid;

        /// <summary>옵션이 적용되는 스킬 레벨(0이면 모든 레벨)</summary>
        public int Level;

        /// <summary>옵션 종류</summary>
        public SkillOptionKind Kind;

        /// <summary>대상 ID. 예) STAT_HP, 1001(AffectUid)</summary>
        public string TargetId;

        /// <summary>연산/접미사(Stat 옵션에 사용)</summary>
        public ConfigCommon.SuffixType Op;

        /// <summary>값(Stat: 수치, Affect: 플래그/확률 등 확장 여지)</summary>
        public float Value;

        /// <summary>지속 시간(초). Affect/상태 등에 사용(0이면 기본 정책)</summary>
        public float Duration;

        public bool IsValid => !string.IsNullOrEmpty(OptionGroupUid) && Kind != SkillOptionKind.None;
    }

    /// <summary>
    /// 스킬 옵션이 영향을 주는 도메인.
    /// - Stat: CharacterStat(CharacterTotals)에 직접 반영되는 수치 스탯
    /// - Affect: Affect 시스템(상시/트리거형 등)은 Affect 패키지 정책에 따라 처리
    /// </summary>
    public enum SkillOptionKind
    {
        None = 0,
        Stat = 1,
        Affect = 2,
    }

    /// <summary>
    /// 스킬 옵션 테이블.
    /// </summary>
    public sealed class TableSkillOption : DefaultTable<StruckTableSkillOption>
    {
        public override string Key => ConfigAddressableTableSkill.SkillOption;

        // Cache: (groupUid, level) -> rows
        private readonly Dictionary<(string group, int level), List<StruckTableSkillOption>> _cache = new();

        protected override void PreLoad()
        {
            _cache.Clear();
        }

        protected override StruckTableSkillOption BuildRow(Dictionary<string, string> data)
        {
            return new StruckTableSkillOption
            {
                OptionGroupUid = data.GetValueOrDefault("OptionGroupUid"),
                Level = MathHelper.ParseInt(data.GetValueOrDefault("Level", "0")),
                Kind = EnumHelper.ConvertEnum<SkillOptionKind>(data.GetValueOrDefault("Kind", "None")),
                TargetId = data.GetValueOrDefault("TargetId"),
                Op = EnumHelper.ConvertEnum<ConfigCommon.SuffixType>(data.GetValueOrDefault("Op", "None")),
                Value = MathHelper.ParseFloat(data.GetValueOrDefault("Value", "0")),
                Duration = MathHelper.ParseFloat(data.GetValueOrDefault("Duration", "0")),
            };
        }

        /// <summary>
        /// 그룹/레벨에 해당하는 옵션 리스트를 반환합니다.
        /// - Level이 정확히 일치하는 레코드 + Level=0(공통) 레코드를 합쳐 반환합니다.
        /// </summary>
        public List<StruckTableSkillOption> GetOptions(string optionGroupUid, int level)
        {
            if (string.IsNullOrEmpty(optionGroupUid)) return null;

            // 정확 레벨 캐시
            var key = (optionGroupUid, level);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var list = new List<StruckTableSkillOption>(8);

            // 공통(Level=0) + 해당 레벨
            foreach (var row in GetAll().Values)
            {
                if (row == null || !row.IsValid) continue;
                if (!string.Equals(row.OptionGroupUid, optionGroupUid, StringComparison.Ordinal)) continue;

                if (row.Level == 0 || row.Level == level)
                    list.Add(row);
            }

            _cache[key] = list;
            return list;
        }
    }
}