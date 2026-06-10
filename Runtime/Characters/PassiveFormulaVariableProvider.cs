using System;
using System.Collections.Generic;
using System.Text;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 패시브 스킬로 증가한 값을 Base*/Stat* 계산과 분리하여 Poly 데미지 공식 변수로만 제공하는 컴포넌트입니다.
    /// </summary>
    /// <remarks>
    /// - 이 컴포넌트는 <see cref="IDamageFormulaVariableProvider"/>를 구현하여 Core의 공식 계산 직전에만 값을 주입합니다.
    /// - 등록된 값은 캐릭터의 Base*, Stat*, TotalBase*, TotalStat* 항목에 영향을 주지 않습니다.
    /// - 패시브 스킬은 장착 목록을 기준으로 전체 리빌드되므로, 토큰 방식이 아니라 전체 교체 방식으로 관리합니다.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PassiveFormulaVariableProvider : MonoBehaviour, IDamageFormulaVariableProvider, IDamageFormulaVariableDebugProvider
    {
        private readonly Dictionary<string, List<PassiveFormulaVariableEntry>> _entriesById = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 기존 패시브 공식 변수를 모두 제거합니다.
        /// </summary>
        public void ClearVariables()
        {
            _entriesById.Clear();
        }

        /// <summary>
        /// 패시브 공식 변수 값을 등록합니다.
        /// </summary>
        /// <param name="variableId">공식에서 사용할 변수 ID입니다.</param>
        /// <param name="value">공식 변수에 반영할 값입니다.</param>
        /// <param name="valueType">값의 의미입니다. 실제 계산 변환은 공식 수식에서 처리합니다.</param>
        /// <param name="operation">동일 변수 ID의 누적 연산 방식입니다.</param>
        public void AddVariable(
            string variableId,
            float value,
            PassiveFormulaVariableValueType valueType,
            PassiveFormulaVariableOperation operation)
        {
            string normalizedId = NormalizeVariableId(variableId);
            if (string.IsNullOrWhiteSpace(normalizedId))
            {
                return;
            }

            if (!_entriesById.TryGetValue(normalizedId, out List<PassiveFormulaVariableEntry> entries))
            {
                entries = new List<PassiveFormulaVariableEntry>(2);
                _entriesById.Add(normalizedId, entries);
            }

            entries.Add(new PassiveFormulaVariableEntry(normalizedId, Sanitize(value), valueType, operation));
        }

        /// <summary>
        /// 현재 캐릭터가 보유한 패시브 공식 변수를 데미지 공식 변수 컨테이너에 등록합니다.
        /// </summary>
        /// <param name="attacker">공격자 캐릭터입니다.</param>
        /// <param name="target">피격 대상 캐릭터입니다.</param>
        /// <param name="variables">변수를 등록할 컨테이너입니다.</param>
        /// <remarks>
        /// 같은 컴포넌트가 공격자에게 붙어 있으면 <c>Attacker*</c>, 피격자에게 붙어 있으면 <c>Target*</c> 접두어 변수를 함께 등록합니다.
        /// 원본 변수 ID도 등록하므로, 단일 캐릭터 테스트 공식에서도 바로 사용할 수 있습니다.
        /// 같은 변수 ID가 다른 Provider에서 이미 등록된 경우에는 덮어쓰지 않고 합산합니다.
        /// </remarks>
        public void FillDamageFormulaVariables(CharacterBase attacker, CharacterBase target, DamageFormulaVariableBag variables)
        {
            if (variables == null || _entriesById.Count == 0)
            {
                return;
            }

            CharacterBase owner = GetComponent<CharacterBase>();
            string rolePrefix = ResolveRolePrefix(owner, attacker, target);

            foreach (KeyValuePair<string, List<PassiveFormulaVariableEntry>> pair in _entriesById)
            {
                double resolvedValue = ResolveValue(pair.Value);
                variables.Add(pair.Key, resolvedValue);

                string pascalName = ToPascalVariableName(pair.Key);
                if (!string.Equals(pair.Key, pascalName, StringComparison.OrdinalIgnoreCase))
                {
                    variables.Add(pascalName, resolvedValue);
                }

                if (!string.IsNullOrEmpty(rolePrefix))
                {
                    variables.Add(rolePrefix + pair.Key, resolvedValue);
                    variables.Add(rolePrefix + pascalName, resolvedValue);
                }
            }
        }

        /// <summary>
        /// 디버그 HUD와 마지막 데미지 스냅샷에서 사용할 패시브 공식 변수 기여도를 수집합니다.
        /// </summary>
        /// <param name="attacker">공격자 캐릭터입니다.</param>
        /// <param name="target">피격 대상 캐릭터입니다.</param>
        /// <param name="results">수집 결과를 추가할 목록입니다.</param>
        public void CollectDamageFormulaVariableDebugRecords(
            CharacterBase attacker,
            CharacterBase target,
            List<DamageFormulaVariableDebugRecord> results)
        {
            if (results == null || _entriesById.Count == 0)
            {
                return;
            }

            CharacterBase owner = GetComponent<CharacterBase>();
            string rolePrefix = ResolveRolePrefix(owner, attacker, target);

            foreach (KeyValuePair<string, List<PassiveFormulaVariableEntry>> pair in _entriesById)
            {
                double resolvedValue = ResolveValue(pair.Value);
                results.Add(new DamageFormulaVariableDebugRecord(
                    pair.Key,
                    resolvedValue,
                    StatModifierDebugSourceType.Skill,
                    "PassiveSkill",
                    rolePrefix));
            }
        }

        /// <summary>
        /// 등록된 패시브 공식 변수 목록을 하나의 공식 변수 값으로 계산합니다.
        /// </summary>
        /// <param name="entries">동일 변수 ID에 등록된 패시브 공식 변수 목록입니다.</param>
        /// <returns>공식에 주입할 최종 변수 값입니다.</returns>
        /// <remarks>
        /// Add는 합산, Multiply는 현재 값을 배율처럼 곱하고, Override는 마지막 Override 값을 우선합니다.
        /// 일반적인 패시브 누적값은 Add 사용을 권장합니다.
        /// </remarks>
        private static double ResolveValue(List<PassiveFormulaVariableEntry> entries)
        {
            if (entries == null || entries.Count == 0)
            {
                return 0d;
            }

            bool hasOverride = false;
            double overrideValue = 0d;
            double addValue = 0d;
            double multiplyValue = 1d;
            bool hasMultiply = false;

            for (int i = 0; i < entries.Count; i++)
            {
                PassiveFormulaVariableEntry entry = entries[i];

                switch (entry.Operation)
                {
                    case PassiveFormulaVariableOperation.Override:
                        hasOverride = true;
                        overrideValue = entry.Value;
                        break;
                    case PassiveFormulaVariableOperation.Multiply:
                        hasMultiply = true;
                        multiplyValue *= entry.Value;
                        break;
                    case PassiveFormulaVariableOperation.None:
                    case PassiveFormulaVariableOperation.Add:
                    default:
                        addValue += entry.Value;
                        break;
                }
            }

            if (hasOverride)
            {
                return overrideValue;
            }

            return hasMultiply ? addValue * multiplyValue : addValue;
        }

        /// <summary>
        /// 공식 변수 ID의 앞뒤 공백을 제거합니다.
        /// </summary>
        /// <param name="variableId">정규화할 공식 변수 ID입니다.</param>
        /// <returns>정규화된 공식 변수 ID입니다.</returns>
        private static string NormalizeVariableId(string variableId)
        {
            return string.IsNullOrWhiteSpace(variableId) ? string.Empty : variableId.Trim();
        }

        /// <summary>
        /// 공식 계산을 방해하지 않도록 NaN/Infinity 값을 0으로 보정합니다.
        /// </summary>
        /// <param name="value">검증할 값입니다.</param>
        /// <returns>공식에 안전하게 전달할 값입니다.</returns>
        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        /// <summary>
        /// 변수 제공 컴포넌트가 공격자/피격자 중 어디에 속하는지 판정합니다.
        /// </summary>
        /// <param name="owner">공식 변수 제공자 소유 캐릭터입니다.</param>
        /// <param name="attacker">공격자 캐릭터입니다.</param>
        /// <param name="target">피격 대상 캐릭터입니다.</param>
        /// <returns>공격자/피격자 접두어입니다.</returns>
        private static string ResolveRolePrefix(CharacterBase owner, CharacterBase attacker, CharacterBase target)
        {
            if (owner == null)
            {
                return string.Empty;
            }

            if (attacker != null && ReferenceEquals(owner, attacker))
            {
                return "Attacker";
            }

            if (target != null && ReferenceEquals(owner, target))
            {
                return "Target";
            }

            return string.Empty;
        }

        /// <summary>
        /// <c>FORMULA_FINAL_DAMAGE_BUFF</c> 같은 ID를 <c>FormulaFinalDamageBuff</c> 형식으로도 사용할 수 있게 변환합니다.
        /// </summary>
        /// <param name="variableId">변환할 공식 변수 ID입니다.</param>
        /// <returns>PascalCase 변수 이름입니다.</returns>
        private static string ToPascalVariableName(string variableId)
        {
            if (string.IsNullOrWhiteSpace(variableId))
            {
                return string.Empty;
            }

            string[] parts = variableId.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length <= 1)
            {
                return variableId;
            }

            var builder = new StringBuilder(variableId.Length);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].ToLowerInvariant();
                if (part.Length == 0)
                {
                    continue;
                }

                builder.Append(char.ToUpperInvariant(part[0]));
                if (part.Length > 1)
                {
                    builder.Append(part.Substring(1));
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// 패시브 공식 변수 1건의 적용 정보입니다.
        /// </summary>
        private readonly struct PassiveFormulaVariableEntry
        {
            public readonly string VariableId;
            public readonly double Value;
            public readonly PassiveFormulaVariableValueType ValueType;
            public readonly PassiveFormulaVariableOperation Operation;

            /// <summary>
            /// 패시브 공식 변수 적용 정보를 생성합니다.
            /// </summary>
            /// <param name="variableId">공식 변수 ID입니다.</param>
            /// <param name="value">공식 변수 값입니다.</param>
            /// <param name="valueType">공식 변수 값의 의미입니다.</param>
            /// <param name="operation">누적 연산 방식입니다.</param>
            public PassiveFormulaVariableEntry(
                string variableId,
                float value,
                PassiveFormulaVariableValueType valueType,
                PassiveFormulaVariableOperation operation)
            {
                VariableId = variableId;
                Value = value;
                ValueType = valueType;
                Operation = operation == PassiveFormulaVariableOperation.None
                    ? PassiveFormulaVariableOperation.Add
                    : operation;
            }
        }
    }
}
