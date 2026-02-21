using System;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Damage 이벤트가 히트된 대상에게 추가로 적용할 Affect 정의.
    /// - SkillApplyAffectClip(시전자 버프)와 역할을 분리하기 위해, 피격 대상 디버프/상태는 Damage 이벤트에 포함한다.
    /// </summary>
    [Serializable]
    public struct OnHitAffectEntry
    {
        [Tooltip("Affect 테이블 Uid")]
        public int affectUid;

        [Tooltip("적용 확률(0~1). 1이면 항상 적용")]
        public float chance;

        [Tooltip("지속 시간 오버라이드(초). 0 이하면 Affect 기본 지속시간을 따른다.")]
        public float durationOverrideSeconds;

        [Tooltip("스택(중첩) 횟수. 1 미만이면 1로 보정된다.")]
        public int stacks;

        [Tooltip("실제로 데미지가 적용된 경우에만 Affect를 적용할지 여부. 현재 프로젝트의 데미지 모델/가드/회피 정책에 따라 의미가 달라질 수 있다.")]
        public bool requireDamageDealt;

        [Tooltip("적용 시점")]
        public OnHitAffectTiming timing;
    }

    public enum OnHitAffectTiming
    {
        AfterDamage = 0,
        BeforeDamage = 1,
    }
}
