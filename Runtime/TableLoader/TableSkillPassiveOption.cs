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

        /// <summary>
        /// 옵션 대상 ID입니다.
        /// - <see cref="SkillOptionKind.Stat"/>: stat 테이블의 ID를 사용합니다. 예) BASE_ATK, STAT_ATK
        /// - <see cref="SkillOptionKind.Affect"/>: AffectUid 문자열을 사용합니다. 예) 1001
        /// - <see cref="SkillOptionKind.FormulaVariable"/>: Poly 공식 변수 ID를 사용합니다. 예) FORMULA_FINAL_DAMAGE_BUFF
        /// - <see cref="SkillOptionKind.MpGainBonus"/>: 비워두거나 MP를 사용합니다.
        /// </summary>
        public string TargetId;

        /// <summary>
        /// Stat 옵션에서 사용하는 stat 테이블 ID입니다.
        /// </summary>
        /// <remarks>
        /// TargetId 컬럼을 유지하면서 Stat 옵션의 의미를 명확히 드러내기 위한 읽기 전용 별칭입니다.
        /// </remarks>
        public string StatId => TargetId;

        /// <summary>
        /// FormulaVariable 옵션에서 사용하는 Poly 공식 변수 ID입니다.
        /// </summary>
        /// <remarks>
        /// 기본적으로 TargetId 컬럼을 사용하며, 선택 컬럼 FormulaVariableId가 있으면 해당 값을 TargetId로 정규화합니다.
        /// </remarks>
        public string FormulaVariableId => TargetId;

        /// <summary>연산/접미사(Stat 옵션에 사용)</summary>
        public ConfigCommon.SuffixType Op;

        /// <summary>값(Stat: 수치, Affect: 플래그/확률 등 확장 여지)</summary>
        public float Value;

        /// <summary>FormulaVariable 옵션에서 공식 변수에 주입할 값입니다.</summary>
        public float FormulaVariableValue => Value;

        /// <summary>FormulaVariable 옵션에서 값의 의미를 표시하는 분류입니다.</summary>
        public PassiveFormulaVariableValueType FormulaVariableValueType;

        /// <summary>FormulaVariable 옵션에서 동일 변수 ID가 여러 번 적용될 때의 누적 방식입니다.</summary>
        public PassiveFormulaVariableOperation FormulaVariableOperation;

        /// <summary>지속 시간(초). Affect/상태 등에 사용(0이면 기본 정책)</summary>
        public float Duration;

        public bool IsValid => Uid > 0 && SkillPassiveUid > 0 && Kind != SkillOptionKind.None;
    }

    /// <summary>
    /// 패시브 옵션이 영향을 주는 도메인.
    /// - Stat: CharacterStat(CharacterTotals)에 직접 반영되는 수치 스탯
    /// - Affect: Affect 시스템(상시/트리거형 등)은 Affect 패키지 정책에 따라 처리
    /// - FormulaVariable: Poly 데미지 공식에만 사용할 변수를 제공
    /// - MpGainBonus: MP 획득 시 추가 획득량 또는 획득 배율을 제공
    /// </summary>
    public enum SkillOptionKind
    {
        None = 0,
        Stat = 1,
        Affect = 2,
        FormulaVariable = 4,
        MpGainBonus = 5,
    }

    /// <summary>
    /// 패시브 공식 변수 값의 의미입니다.
    /// </summary>
    /// <remarks>
    /// 현재 값 변환은 공식 수식에서 직접 처리하므로, 이 enum은 데이터 의미와 설명 확장을 위한 분류로 사용합니다.
    /// </remarks>
    public enum PassiveFormulaVariableValueType
    {
        None = 0,
        Flat = 1,
        Percent = 2,
    }

    /// <summary>
    /// 동일한 패시브 공식 변수 ID가 여러 번 적용될 때의 누적 방식입니다.
    /// </summary>
    public enum PassiveFormulaVariableOperation
    {
        None = 0,
        Add = 1,
        Multiply = 2,
        Override = 3,
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
            TableRowReader reader = ReadRow(data);
            int uid = reader.Int("Uid", 0);
            string name = reader.String("Name");
            if (string.IsNullOrWhiteSpace(name))
            {
                name = $"SkillPassiveOption_{uid}";
            }

            string targetId = reader.String("TargetId");
            if (!string.IsNullOrWhiteSpace(targetId))
            {
                targetId = targetId.Trim();
            }

            SkillOptionKind kind = reader.Enum<SkillOptionKind>("Kind", EnumHelper.ConvertEnum<SkillOptionKind>("None"));
            string rawOp = reader.String("Op");
            ConfigCommon.SuffixType op = ResolveSuffixType(rawOp);
            float value = reader.Float("Value", 0f);

            if (kind == SkillOptionKind.FormulaVariable)
            {
                targetId = NormalizeOptionalFormulaVariableId(reader, targetId);
                value = NormalizeOptionalFormulaVariableValue(reader, value, op);
            }

            return new StruckTableSkillPassiveOption
            {
                Uid = uid,
                Name = name,
                SkillPassiveUid = reader.Int("SkillPassiveUid", 0),
                Order = reader.Int("Order", 0),
                Level = reader.Int("Level", 0),
                Kind = kind,
                TargetId = targetId,
                Op = op,
                Value = value,
                FormulaVariableValueType = ResolveFormulaVariableValueType(reader, op),
                FormulaVariableOperation = ResolveFormulaVariableOperation(reader, rawOp),
                Duration = reader.Float("Duration", 0f),
            };
        }


        /// <summary>
        /// Op 컬럼을 기존 SuffixType으로 해석합니다.
        /// </summary>
        /// <param name="rawOp">테이블에서 읽은 Op 원본 문자열입니다.</param>
        /// <returns>Stat 옵션에서 사용할 접미사 연산입니다.</returns>
        /// <remarks>
        /// FormulaVariable 옵션은 Op에 Add/Multiply/Override를 입력할 수 있으므로, SuffixType에 없는 값은 None으로 처리합니다.
        /// </remarks>
        private static ConfigCommon.SuffixType ResolveSuffixType(string rawOp)
        {
            if (string.IsNullOrWhiteSpace(rawOp))
            {
                return ConfigCommon.SuffixType.None;
            }

            return Enum.TryParse(rawOp.Trim(), ignoreCase: true, out ConfigCommon.SuffixType op)
                ? op
                : ConfigCommon.SuffixType.None;
        }

        /// <summary>
        /// 선택 컬럼 FormulaVariableId가 있으면 TargetId 대신 사용할 공식 변수 ID를 반환합니다.
        /// </summary>
        /// <param name="reader">테이블 행 파서입니다.</param>
        /// <param name="fallbackTargetId">TargetId 컬럼에서 읽은 기본 공식 변수 ID입니다.</param>
        /// <returns>정규화된 공식 변수 ID입니다.</returns>
        private static string NormalizeOptionalFormulaVariableId(TableRowReader reader, string fallbackTargetId)
        {
            string formulaVariableId = reader.String("FormulaVariableId", fallbackTargetId);
            return string.IsNullOrWhiteSpace(formulaVariableId) ? fallbackTargetId : formulaVariableId.Trim();
        }

        /// <summary>
        /// 선택 컬럼 FormulaVariableValue가 있으면 Value 대신 사용할 공식 변수 값을 반환합니다.
        /// </summary>
        /// <param name="reader">테이블 행 파서입니다.</param>
        /// <param name="fallbackValue">Value 컬럼에서 읽은 기본 값입니다.</param>
        /// <param name="op">기존 Op 컬럼의 접미사 연산입니다.</param>
        /// <returns>공식 변수에 등록할 값입니다.</returns>
        /// <remarks>
        /// FormulaVariableOperation 컬럼이 없는 기존 테이블에서는 Minus/Decrease를 음수 Add 값으로 해석합니다.
        /// </remarks>
        private static float NormalizeOptionalFormulaVariableValue(
            TableRowReader reader,
            float fallbackValue,
            ConfigCommon.SuffixType op)
        {
            float value = reader.Float("FormulaVariableValue", fallbackValue);
            if (reader.HasColumn("FormulaVariableOperation") && !reader.IsEmpty("FormulaVariableOperation"))
            {
                return value;
            }

            return op == ConfigCommon.SuffixType.Minus || op == ConfigCommon.SuffixType.Decrease
                ? -value
                : value;
        }

        /// <summary>
        /// FormulaVariableValueType 선택 컬럼 또는 Op 컬럼을 기준으로 공식 변수 값의 의미를 해석합니다.
        /// </summary>
        /// <param name="reader">테이블 행 파서입니다.</param>
        /// <param name="op">기존 Op 컬럼의 접미사 연산입니다.</param>
        /// <returns>공식 변수 값의 의미입니다.</returns>
        private static PassiveFormulaVariableValueType ResolveFormulaVariableValueType(
            TableRowReader reader,
            ConfigCommon.SuffixType op)
        {
            if (reader.HasColumn("FormulaVariableValueType") && !reader.IsEmpty("FormulaVariableValueType"))
            {
                return reader.Enum<PassiveFormulaVariableValueType>(
                    "FormulaVariableValueType",
                    PassiveFormulaVariableValueType.None);
            }

            return op == ConfigCommon.SuffixType.Increase || op == ConfigCommon.SuffixType.Decrease
                ? PassiveFormulaVariableValueType.Percent
                : PassiveFormulaVariableValueType.Flat;
        }

        /// <summary>
        /// FormulaVariableOperation 선택 컬럼 또는 Op 컬럼을 기준으로 공식 변수 누적 방식을 해석합니다.
        /// </summary>
        /// <param name="reader">테이블 행 파서입니다.</param>
        /// <param name="rawOp">테이블에서 읽은 Op 원본 문자열입니다.</param>
        /// <returns>공식 변수 누적 방식입니다.</returns>
        private static PassiveFormulaVariableOperation ResolveFormulaVariableOperation(
            TableRowReader reader,
            string rawOp)
        {
            if (reader.HasColumn("FormulaVariableOperation") && !reader.IsEmpty("FormulaVariableOperation"))
            {
                return reader.Enum<PassiveFormulaVariableOperation>(
                    "FormulaVariableOperation",
                    PassiveFormulaVariableOperation.Add);
            }

            if (!string.IsNullOrWhiteSpace(rawOp) &&
                Enum.TryParse(rawOp.Trim(), ignoreCase: true, out PassiveFormulaVariableOperation operation))
            {
                return operation == PassiveFormulaVariableOperation.None
                    ? PassiveFormulaVariableOperation.Add
                    : operation;
            }

            return PassiveFormulaVariableOperation.Add;
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
