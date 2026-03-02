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
    public sealed class StruckTableSkillPassiveOption
    {
        /// <summary>옵션 그룹 UID (TableSkill.OptionGroupUid)</summary>
        public int OptionGroupUid;

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

        public bool IsValid => OptionGroupUid > 0 && Kind != SkillOptionKind.None;
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
    public sealed class TableSkillPassiveOption : DefaultTable<StruckTableSkillPassiveOption>
    {
        public override string Key => ConfigAddressableTableSkill.SkillPassiveOption;

        // Cache: (groupUid, level) -> rows
        private readonly Dictionary<(int uid, int level), List<StruckTableSkillPassiveOption>> _cache = new();

        // NOTE: DefaultTable은 첫 번째 컬럼(int Uid)을 Dictionary Key로 사용하므로,
        // SkillOption 처럼 동일 Uid(또는 그룹)로 여러 줄이 존재하는 테이블은 마지막 줄만 남게 됩니다.
        // 따라서 이 테이블은 전용 저장소(_all)로 모든 Row를 보관합니다.
        private readonly Dictionary<int, StruckTableSkillPassiveOption> _all = new();


        public override void LoadData(string content)
        {
            // DefaultTable.LoadData는 첫 번째 컬럼(int uid)을 Key로 사용하여 중복 시 덮어씁니다.
            // SkillOption은 (OptionGroupUid, Level) 조합으로 여러 레코드가 존재하므로,
            // 여기서는 전용 파서를 사용해 모든 레코드를 _all 에 보관합니다.
            PreLoad();

            _all.Clear();

            if (string.IsNullOrWhiteSpace(content))
            {
                GcLogger.LogWarning($"[Table] Empty content: {GetType().Name}");
                return;
            }

            var lines = content.Split('\n');
            if (lines.Length == 0)
            {
                GcLogger.LogWarning($"[Table] No lines: {GetType().Name}");
                return;
            }

            // header
            var headerLine = lines[0].TrimEnd('\r');
            var headers = headerLine.Split('\t');
            if (headers.Length == 0)
            {
                GcLogger.LogWarning($"[Table] No headers: {GetType().Name}");
                return;
            }

            var rowId = 1; // synthetic key
            for (var i = 1; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) continue;

                var values = line.Split('\t');
                if (values.Length == 0) continue;

                // row dictionary
                var data = new Dictionary<string, string>(headers.Length, StringComparer.Ordinal);
                for (var c = 0; c < headers.Length; c++)
                {
                    var h = headers[c];
                    if (string.IsNullOrEmpty(h)) continue;
                    var v = c < values.Length ? values[c] : string.Empty;
                    data[h] = v;
                }

                try
                {
                    var row = BuildRow(data);
                    if (row == null) continue;
                    _all[rowId++] = row;
                    OnLoadedData(row);
                }
                catch (Exception e)
                {
                    GcLogger.LogError($"[Table] Parse error: {GetType().Name} line={i} : {e}");
                }
            }
        }

        public override IReadOnlyDictionary<int, StruckTableSkillPassiveOption> GetAll() => _all;

        public override StruckTableSkillPassiveOption GetDataByUid(int uid)
        {
            _all.TryGetValue(uid, out var row);
            return row;
        }

        public override bool TryGetDataByUid(int uid, out StruckTableSkillPassiveOption row)
        {
            return _all.TryGetValue(uid, out row);
        }

        protected override void PreLoad()
        {
            _cache.Clear();
        }

        protected override StruckTableSkillPassiveOption BuildRow(Dictionary<string, string> data)
        {
            return new StruckTableSkillPassiveOption
            {
                OptionGroupUid = MathHelper.ParseInt(data.GetValueOrDefault("OptionGroupUid", "0")),
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
        public List<StruckTableSkillPassiveOption> GetOptions(int optionGroupUid, int level)
        {
            if (optionGroupUid <= 0) return null;

            // 정확 레벨 캐시
            var key = (optionGroupUid, level);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var list = new List<StruckTableSkillPassiveOption>(8);

            // 공통(Level=0) + 해당 레벨
            foreach (var row in GetAll().Values)
            {
                if (row == null || !row.IsValid) continue;
                if (row.OptionGroupUid != optionGroupUid) continue;

                if (row.Level == 0 || row.Level == level)
                    list.Add(row);
            }

            _cache[key] = list;
            return list;
        }
    }
}