using System;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Damage 이벤트가 히트된 대상에게 추가로 적용할 Crowd Control 정의입니다.
    /// 실제 적용은 직접 CC 컨트롤러를 호출하지 않고 MetadataDamage.crowdControlUid를 통해
    /// Core 데미지 파이프라인으로 전달하여 가드/피격 반응과 일관되게 처리합니다.
    /// </summary>
    [Serializable]
    public struct OnHitCrowdControlEntry
    {
        [Tooltip("crowd_control 테이블 Uid")]
        public int crowdControlUid;

        [Tooltip("적용 확률(0~1). 1이면 항상 적용")]
        [Range(0f, 1f)]
        public float chance;

        [Tooltip("실제로 데미지가 적용된 경우에만 CC 후보로 사용할지 여부")]
        public bool requireDamageDealt;

        [Tooltip("적용 시점. 현재 구조에서는 AfterDamage 사용을 권장합니다.")]
        public OnHitCrowdControlTiming timing;
    }

    public enum OnHitCrowdControlTiming
    {
        AfterDamage = 0,
        BeforeDamage = 1,
    }
}
