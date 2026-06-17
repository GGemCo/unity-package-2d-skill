using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 타격 시 부가로 적용되는 원소 게이지 데이터를 공통 규칙으로 계산합니다.
    /// </summary>
    internal static class SkillOnHitEffectUtility
    {
        /// <summary>
        /// OnHit 원소 게이지 설정을 실제 전투 메타데이터에 전달할 적용 목록으로 변환합니다.
        /// </summary>
        /// <param name="entries">스킬 이벤트에 설정된 OnHit 원소 게이지 항목입니다.</param>
        /// <param name="caster">조건 확인에 사용할 캐스터 오브젝트입니다.</param>
        /// <param name="damageApplied">이번 타격에서 실제 데미지가 적용되었는지 여부입니다.</param>
        /// <param name="deferDamageDealtRequirement">
        /// 실제 데미지 적용 여부를 아직 확정할 수 없어 <see cref="ElementGaugeApplication.requireDamageDealt"/>로 전달할지 여부입니다.
        /// </param>
        /// <returns>적용 가능한 원소 게이지 목록입니다. 적용할 항목이 없으면 <see langword="null"/>입니다.</returns>
        public static ElementGaugeApplication[] BuildElementGaugeApplications(
            OnHitElementGaugeEntry[] entries,
            GameObject caster,
            bool damageApplied,
            bool deferDamageDealtRequirement = false)
        {
            if (entries == null || entries.Length == 0)
                return null;

            List<ElementGaugeApplication> results = null;

            for (int i = 0; i < entries.Length; i++)
            {
                OnHitElementGaugeEntry entry = entries[i];
                if (entry.damageType == ConfigCommon.DamageType.None || entry.damageType == ConfigCommon.DamageType.Physic)
                    continue;
                if (entry.gaugeValue <= 0f)
                    continue;
                if (entry.requireDamageDealt && !damageApplied && !deferDamageDealtRequirement)
                    continue;
                if (entry.requireAffectUid > 0 && !AffectApi.HasAttached(caster, entry.requireAffectUid))
                    continue;

                float chance = entry.chance <= 0f ? 1f : Mathf.Clamp01(entry.chance);
                if (chance <= 0f)
                    continue;
                if (chance < 0.9999f && Random.value > chance)
                    continue;

                results ??= new List<ElementGaugeApplication>(4);
                results.Add(new ElementGaugeApplication(
                    entry.damageType,
                    entry.gaugeValue,
                    deferDamageDealtRequirement && entry.requireDamageDealt));
            }

            return results != null && results.Count > 0 ? results.ToArray() : null;
        }
    }
}
