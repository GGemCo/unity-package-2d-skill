using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 프로젝타일(투사체) 발사 이벤트 정의.
    /// - Core 패키지의 Projectile 시스템을 Skill Timeline 이벤트로 트리거하기 위한 Payload 입니다.
    /// - 발사 타이밍은 Timeline Clip 구간(Start~End)과 동기화됩니다.
    /// </summary>
    public sealed class ProjectileEventDefinition : ScriptableObject
    {
        [Header("Projectile (Core Table)")]
        [Tooltip("projectile.txt 의 Uid")]
        public int projectileUid;

        [Header("Combat")]
        public ConfigCommon.DamageType damageType = ConfigCommon.DamageType.None;
        public long damage = 0;

        [Header("Dynamic Multipliers")]
        [Tooltip("테이블 속도에 곱해지는 배율(최소 0.01)")]
        public float speedMultiplier = 1f;

        [Tooltip("비주얼 스케일 배율(최소 0.01)")]
        public float scaleMultiplier = 1f;

        [Header("Visual Overrides (optional)")]
        public ProjectileConstants.ProjectileVisualType visualType = ProjectileConstants.ProjectileVisualType.Default;
        public Sprite visualSprite;
        public RuntimeAnimatorController visualAnimatorController;
        public int visualVfxUidOverride = 0;

        [Header("Targeting Overrides")]
        [Tooltip("스킬 기본 TargetingMode 대신, 이벤트 별 TargetingMode를 강제할 수 있습니다.")]
        public TargetingOverride targetingOverride;
    }
}
