using System;
using System.Collections.Generic;
using System.Linq;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 패시브 옵션 테이블 Row.
    /// - 1행 = 1개 옵션
    /// - SkillPassiveUid를 통해 skill_passive(Uid)와 연결
    /// </summary>
    public  class StruckTableSkillPassiveOption : IUidName
    {
        public int Uid { get; set; }
        public string Name { get; set; }

        /// <summary>부모 패시브 스킬 UID (skill_passive.Uid)</summary>
        public int SkillPassiveUid;

        /// <summary>적용 순서</summary>
        public int Order;

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

        public bool IsValid => Uid > 0 && SkillPassiveUid > 0 && Kind != SkillOptionKind.None;
    }

    /// <summary>
    /// 패시브 옵션이 영향을 주는 도메인.
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
    /// 패시브 옵션 테이블.
    /// </summary>
    public class TableSkillPassiveOption : DefaultTable<StruckTableSkillPassiveOption>
    {
        public override string Key => ConfigAddressableTableSkill.SkillPassiveOption;

        private readonly Dictionary<(int skillPassiveUid, int level), List<StruckTableSkillPassiveOption>> _cache = new();
        private readonly Dictionary<int, List<StruckTableSkillPassiveOption>> _bySkillPassiveUid = new();

        protected override void PreLoad()
        {
            base.PreLoad();
            _cache.Clear();
            _bySkillPassiveUid.Clear();
        }

        protected override StruckTableSkillPassiveOption BuildRow(Dictionary<string, string> data)
        {
            int uid = MathHelper.ParseInt(data.GetValueOrDefault("Uid", "0"));
            string name = data.GetValueOrDefault("Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"SkillPassiveOption_{uid}";
            }

            return new StruckTableSkillPassiveOption
            {
                Uid = uid,
                Name = name,
                SkillPassiveUid = MathHelper.ParseInt(data.GetValueOrDefault("SkillPassiveUid", "0")),
                Order = MathHelper.ParseInt(data.GetValueOrDefault("Order", "0")),
                Level = MathHelper.ParseInt(data.GetValueOrDefault("Level", "0")),
                Kind = EnumHelper.ConvertEnum<SkillOptionKind>(data.GetValueOrDefault("Kind", "None")),
                TargetId = data.GetValueOrDefault("TargetId"),
                Op = EnumHelper.ConvertEnum<ConfigCommon.SuffixType>(data.GetValueOrDefault("Op", "None")),
                Value = MathHelper.ParseFloat(data.GetValueOrDefault("Value", "0")),
                Duration = MathHelper.ParseFloat(data.GetValueOrDefault("Duration", "0")),
            };
        }

        protected override void OnLoadedData(StruckTableSkillPassiveOption row)
        {
            base.OnLoadedData(row);

            if (row == null || row.SkillPassiveUid <= 0)
                return;

            if (!_bySkillPassiveUid.TryGetValue(row.SkillPassiveUid, out var list))
            {
                list = new List<StruckTableSkillPassiveOption>();
                _bySkillPassiveUid[row.SkillPassiveUid] = list;
            }

            list.Add(row);
        }

        /// <summary>
        /// 패시브 UID/레벨에 해당하는 옵션 리스트를 반환합니다.
        /// - Level이 정확히 일치하는 레코드 + Level=0(공통) 레코드를 합쳐 반환합니다.
        /// - Order 기준으로 정렬합니다.
        /// </summary>
        public IReadOnlyList<StruckTableSkillPassiveOption> GetOptions(int skillPassiveUid, int level)
        {
            if (skillPassiveUid <= 0)
                return Array.Empty<StruckTableSkillPassiveOption>();

            int normalizedLevel = Math.Max(0, level);
            var key = (skillPassiveUid, normalizedLevel);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            if (!_bySkillPassiveUid.TryGetValue(skillPassiveUid, out var sourceList) || sourceList == null || sourceList.Count == 0)
            {
                var empty = Array.Empty<StruckTableSkillPassiveOption>();
                _cache[key] = new List<StruckTableSkillPassiveOption>(empty);
                return empty;
            }

            var result = sourceList
                .Where(static row => row != null && row.IsValid)
                .Where(row => row.Level == 0 || row.Level == normalizedLevel)
                .OrderBy(row => row.Order)
                .ThenBy(row => row.Uid)
                .ToList();

            _cache[key] = result;
            return result;
        }
    }
}
