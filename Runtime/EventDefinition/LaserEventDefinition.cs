using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 레이저 발사 이벤트 정의입니다.
    /// - Projectile 시스템과 분리된 Laser 시스템을 스킬 Timeline에서 호출하기 위한 Payload 입니다.
    /// - 정적 정의는 Core laser 테이블을 참조합니다.
    /// </summary>
    public sealed class LaserEventDefinition : ScriptableObject
    {
        [Header("Laser (Core Laser Table)")]
        [Tooltip("Core laser 테이블 UID입니다.")]
        public int laserUid;

        [Header("Combat")]
        public ConfigCommon.DamageType damageType = ConfigCommon.DamageType.None;
        public long damage = 0;

        [Header("Timing")]
        [Tooltip("레이저 유지 시간(초)입니다. 0 이하이면 1프레임성 레이저처럼 동작합니다.")]
        public float durationSeconds = 0.25f;

        [Tooltip("레이저가 같은 대상을 다시 때릴 수 있는 주기(초)입니다. 0이면 진입 시 1회만 적용합니다.")]
        public float tickIntervalSeconds = 0f;

        [Header("Range / Aim")]
        [Tooltip("최대 사거리 오버라이드입니다. 0 이하이면 타겟/좌표 기반 거리 또는 기본값을 사용합니다.")]
        public float maxDistance = 0f;

        [Tooltip("레이저 유지 시간 동안 타겟/조준 방향을 계속 갱신할지 여부입니다.")]
        public bool updateAimContinuously = false;

        [Header("Visual Overrides (optional)")]
        public float scaleMultiplier = 1f;
        public ProjectileConstants.ProjectileVisualType visualType = ProjectileConstants.ProjectileVisualType.Default;
        public Sprite visualSprite;
        public RuntimeAnimatorController visualAnimatorController;
        public int visualVfxUidOverride = 0;

        [Header("Chain Cancel")]
        [Tooltip("이 Laser 이벤트가 실제 데미지를 확정했을 때 다음 스킬 연계를 즉시 허용할지 여부입니다. GGemCoSkillSettings.enableSkillChainOnConfirmedDamage 가 함께 켜져 있어야 동작합니다.")]
        public bool allowSkillChainOnConfirmedDamage = false;

        [Header("OnHit Element Gauge")]
        public OnHitElementGaugeEntry[] onHitElementGauges;

        [Header("Targeting Overrides")]
        [Tooltip("스킬 기본 TargetingMode 대신, 이벤트 별 TargetingMode를 강제할 수 있습니다.")]
        public TargetingOverride targetingOverride;
    }
}
