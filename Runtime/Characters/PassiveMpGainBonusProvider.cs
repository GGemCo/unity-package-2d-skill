using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 패시브 스킬로 제공되는 MP 획득 보너스를 계산하는 컴포넌트입니다.
    /// </summary>
    /// <remarks>
    /// - 패시브 장착 목록이 변경될 때 <see cref="CharacterPassiveSkillController"/>가 옵션을 전체 교체합니다.
    /// - MP 획득 시점에는 테이블을 다시 조회하지 않고, 이 컴포넌트에 캐시된 값을 기준으로 최종 획득량을 계산합니다.
    /// - Plus/Minus는 고정값, Increase/Decrease는 기본 획득량 기준 퍼센트 보정으로 처리합니다.
    /// </remarks>
    [DisallowMultipleComponent]
    public sealed class PassiveMpGainBonusProvider : MonoBehaviour
    {
        private readonly List<PassiveMpGainBonusEntry> _entries = new(4);

        /// <summary>
        /// 현재 등록된 MP 획득 보너스 옵션 개수입니다.
        /// </summary>
        public int Count => _entries.Count;

        /// <summary>
        /// 기존 MP 획득 보너스 옵션을 모두 제거합니다.
        /// </summary>
        public void ClearBonuses()
        {
            _entries.Clear();
        }

        /// <summary>
        /// 패시브 옵션 테이블 행을 MP 획득 보너스로 등록합니다.
        /// </summary>
        /// <param name="option">등록할 패시브 옵션 행입니다.</param>
        public void AddBonus(StruckTableSkillPassiveOption option)
        {
            if (option == null || option.Kind != SkillOptionKind.MpGainBonus)
            {
                return;
            }

            float value = Sanitize(option.Value);
            if (Mathf.Approximately(value, 0f))
            {
                return;
            }

            _entries.Add(new PassiveMpGainBonusEntry(option.Op, value));
        }

        /// <summary>
        /// 기본 MP 획득량에 패시브 MP 획득 보너스를 적용한 최종 획득량을 반환합니다.
        /// </summary>
        /// <param name="baseAmount">패시브 보정 전 기본 MP 획득량입니다.</param>
        /// <returns>패시브 보정 후 실제 지급을 시도할 MP 획득량입니다.</returns>
        /// <remarks>
        /// Increase/Decrease는 기본 획득량을 기준으로 계산한 뒤 내림 처리합니다.
        /// 최종 결과가 0 이하가 되면 MP를 지급하지 않도록 0으로 보정합니다.
        /// </remarks>
        public int EvaluateBonusMp(int baseAmount)
        {
            if (baseAmount <= 0)
            {
                return 0;
            }

            if (_entries.Count == 0)
            {
                return baseAmount;
            }

            float flatBonus = 0f;
            float percentBonus = 0f;

            for (int i = 0; i < _entries.Count; i++)
            {
                PassiveMpGainBonusEntry entry = _entries[i];
                switch (entry.Op)
                {
                    case ConfigCommon.SuffixType.Minus:
                        flatBonus -= entry.Value;
                        break;
                    case ConfigCommon.SuffixType.Increase:
                        percentBonus += entry.Value;
                        break;
                    case ConfigCommon.SuffixType.Decrease:
                        percentBonus -= entry.Value;
                        break;
                    case ConfigCommon.SuffixType.None:
                    case ConfigCommon.SuffixType.Plus:
                    default:
                        flatBonus += entry.Value;
                        break;
                }
            }

            float percentAmount = baseAmount * percentBonus * 0.01f;
            int resolvedAmount = baseAmount + Mathf.FloorToInt(flatBonus + percentAmount);
            return Mathf.Max(0, resolvedAmount);
        }

        /// <summary>
        /// 계산을 방해하는 특수 부동소수점 값을 0으로 보정합니다.
        /// </summary>
        /// <param name="value">검증할 값입니다.</param>
        /// <returns>계산에 사용할 안전한 값입니다.</returns>
        private static float Sanitize(float value)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? 0f : value;
        }

        private readonly struct PassiveMpGainBonusEntry
        {
            public readonly ConfigCommon.SuffixType Op;
            public readonly float Value;

            public PassiveMpGainBonusEntry(ConfigCommon.SuffixType op, float value)
            {
                Op = op;
                Value = value;
            }
        }
    }
}
