using System;
using Config;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    [Serializable]
    public sealed class SkillDamageClip : SkillEventClipBase
    {
        [SerializeField] private float coef = 1.0f;
        [SerializeField] private int damageTypeUid = 0;
        [SerializeField] private int areaUid = 0;

        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Damage;

        public float Coef => coef;
        public int DamageTypeUid => damageTypeUid;
        public int AreaUid => areaUid;
    }
}
