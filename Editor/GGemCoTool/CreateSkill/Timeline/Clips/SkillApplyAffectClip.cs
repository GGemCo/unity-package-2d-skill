using System;
using Config;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Affect(상태/버프) 적용 이벤트 클립(Authoring).
    /// - Bake 시 <see cref="GGemCo2DSkill.ApplyStatusEventDefinition"/> Payload 로 변환된다.
    /// </summary>
    [Serializable]
    public sealed class SkillApplyAffectClip : SkillEventClipBase
    {
        [Header("Affect")]
        [SerializeField] private int affectUid = 0;
        [SerializeField] private float affectDuration = 0f;

        [Header("Target")]
        [SerializeField] private ApplyAffectTarget applyTo = ApplyAffectTarget.Caster;

        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.ApplyAffect;

        public int AffectUid => affectUid;
        public float AffectDuration => affectDuration;

        public ApplyAffectTarget ApplyTo => applyTo;
    }
}
