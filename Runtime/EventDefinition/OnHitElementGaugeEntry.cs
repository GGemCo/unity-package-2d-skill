using System;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Damage/Projectile 적중 시 대상에게 누적할 속성 게이지 정의입니다.
    /// </summary>
    [Serializable]
    public struct OnHitElementGaugeEntry
    {
        [Tooltip("누적할 속성 타입입니다.")]
        public ConfigCommon.DamageType damageType;

        [Tooltip("누적할 게이지 값입니다.")]
        public float gaugeValue;

        [Tooltip("적용 확률(0~1). 1이면 항상 적용됩니다.")]
        [Range(0f, 1f)]
        public float chance;

        [Tooltip("실제로 데미지가 적용된 경우에만 게이지를 누적할지 여부입니다.")]
        public bool requireDamageDealt;
    }
}
