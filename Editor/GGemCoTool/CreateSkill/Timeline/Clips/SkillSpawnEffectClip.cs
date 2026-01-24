using System;
using Config;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    [Serializable]
    public sealed class SkillSpawnEffectClip : SkillEventClipBase
    {
        public enum AnchorType { Caster = 0, Target = 1, Ground = 2 }

        [SerializeField] private int effectUid;
        [SerializeField] private AnchorType anchor = AnchorType.Caster;
        [SerializeField] private Vector2 offset;

        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.SpawnEffect;

        public int EffectUid => effectUid;
        public int Anchor => (int)anchor;
        public Vector2 Offset => offset;
    }
}
