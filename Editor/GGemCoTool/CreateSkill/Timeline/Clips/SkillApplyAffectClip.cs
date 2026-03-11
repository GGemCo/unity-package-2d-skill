using System;
using Config;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Affect(상태 효과, 버프/디버프 등)를 적용하는 스킬 이벤트 Authoring용 타임라인 클립입니다.
    /// Bake 과정에서 <see cref="GGemCo2DSkill.ApplyStatusEventDefinition"/> Payload로 변환되어
    /// 런타임 스킬 시스템에서 실제 상태 효과 적용 이벤트로 사용됩니다.
    /// </summary>
    [Serializable]
    public sealed class SkillApplyAffectClip : SkillEventClipBase
    {
        [Header("Affect")]

        [Tooltip("적용할 Affect(상태 효과)의 UID입니다. affect 테이블에 정의된 데이터를 참조합니다.")]
        [SerializeField] private int affectUid = 0;

        [Tooltip("Affect가 유지되는 지속 시간(초)입니다. 0이면 기본 테이블 값을 사용합니다.")]
        [SerializeField] private float affectDuration = 0f;

        [Header("Target")]

        [Tooltip("Affect를 적용할 대상입니다. (Caster / Target 등)")]
        [SerializeField] private ApplyAffectTarget applyTo = ApplyAffectTarget.Caster;

        /// <summary>
        /// 이 클립이 생성하는 스킬 이벤트 타입입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.ApplyAffect;

        /// <summary>
        /// 적용할 Affect(상태 효과)의 고유 식별자입니다.
        /// Config 시스템에서 정의된 Affect 데이터를 참조합니다.
        /// </summary>
        public int AffectUid => affectUid;

        /// <summary>
        /// Affect가 유지되는 지속 시간(초)입니다.
        /// </summary>
        public float AffectDuration => affectDuration;

        /// <summary>
        /// Affect를 적용할 대상입니다.
        /// </summary>
        public ApplyAffectTarget ApplyTo => applyTo;
    }
}