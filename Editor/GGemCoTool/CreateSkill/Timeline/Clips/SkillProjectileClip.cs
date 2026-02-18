using System;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 프로젝타일(투사체) 발사 이벤트 클립(Authoring).
    /// - Bake 시 <see cref="GGemCo2DSkill.ProjectileEventDefinition"/> Payload 로 변환된다.
    /// </summary>
    [Serializable]
    public sealed class SkillProjectileClip : SkillEventClipBase
    {
        [Header("Projectile (Core Table)")]
        [SerializeField] private int projectileUid = 0;

        [Header("Combat")]
        [SerializeField] private ConfigCommon.DamageType damageType = ConfigCommon.DamageType.None;
        [SerializeField] private long damage = 0;

        [Header("Dynamic Multipliers")]
        [SerializeField] private float speedMultiplier = 1f;
        [SerializeField] private float scaleMultiplier = 1f;

        [Header("Visual Overrides (optional)")]
        [SerializeField] private ProjectileConstants.ProjectileVisualType visualType = ProjectileConstants.ProjectileVisualType.Default;
        [SerializeField] private Sprite visualSprite;
        [SerializeField] private RuntimeAnimatorController visualAnimatorController;
        [SerializeField] private int visualEffectUidOverride = 0;

        [Header("Targeting Overrides")]
        [SerializeField] private GGemCo2DSkill.TargetingOverride targetingOverride;

        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Projectile;

        public int ProjectileUid => projectileUid;
        public ConfigCommon.DamageType DamageType => damageType;
        public long Damage => damage;

        public float SpeedMultiplier => speedMultiplier;
        public float ScaleMultiplier => scaleMultiplier;

        public ProjectileConstants.ProjectileVisualType VisualType => visualType;
        public Sprite VisualSprite => visualSprite;
        public RuntimeAnimatorController VisualAnimatorController => visualAnimatorController;
        public int VisualEffectUidOverride => visualEffectUidOverride;

        public GGemCo2DSkill.TargetingOverride TargetingOverride => targetingOverride;
    }
}
