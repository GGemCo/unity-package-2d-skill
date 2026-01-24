using System;
using Config;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    [Serializable]
    public sealed class SkillApplyAffectClip : SkillEventClipBase
    {
        [SerializeField] private int affectUid = 0;
        [SerializeField] private float duration = 0f;

        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.ApplyAffect;

        public int AffectUid => affectUid;
        public float Duration => duration;
    }
}
