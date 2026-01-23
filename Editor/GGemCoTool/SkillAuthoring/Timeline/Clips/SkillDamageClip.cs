using System;
using Config;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    [Serializable]
    public sealed class SkillDamageClip : SkillEventClipBase
    {
        [SerializeField] private float multiplier = 1.0f;
        [SerializeField] private int damageTypeUid = 0;
        [SerializeField] private ConfigCommonSkill.SkillAreaShape areaShape = ConfigCommonSkill.SkillAreaShape.Circle;
        [SerializeField] private float radius = 2f;
        [SerializeField] private float length = 3f;
        [SerializeField] private float width = 2f;
        [SerializeField] private float angle = 60f;
        [SerializeField] private Vector2 offset = Vector2.zero;

        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Damage;

        public float Multiplier => multiplier;
        public int DamageTypeUid => damageTypeUid;
        public ConfigCommonSkill.SkillAreaShape AreaShape => areaShape;
        public float Radius => radius;
        public float Length => length;
        public float Width => width;
        public float Angle => angle;
        public Vector2 Offset => offset;
    }
}
