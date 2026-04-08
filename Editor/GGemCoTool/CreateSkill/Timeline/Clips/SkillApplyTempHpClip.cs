using System;
using Config;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 런타임 Temp HP(비저장 보호막/임시 하트)를 적용하는 스킬 이벤트 Authoring용 타임라인 클립입니다.
    /// </summary>
    [Serializable]
    public sealed class SkillApplyTempHpClip : SkillEventClipBase
    {
        [Header("Temp HP")]
        [SerializeField] private long tempHpValue = 0;

        [Tooltip("0이면 현재 skill uid를 source key로 사용합니다.")]
        [SerializeField] private int sourceKeyOverride = 0;

        [Header("Target")]
        [SerializeField] private ApplyAffectTarget applyTo = ApplyAffectTarget.Caster;

        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.ApplyTempHp;

        public long TempHpValue => tempHpValue;
        public int SourceKeyOverride => sourceKeyOverride;
        public ApplyAffectTarget ApplyTo => applyTo;
    }
}
