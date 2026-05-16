using GGemCo2DCore;
using UnityEngine;
using UnityEngine.Serialization;

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
        [Tooltip("Core projectile_linear/projectile_arc/projectile_path 테이블의 Uid")]
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

        [Header("타겟 지점 정책")]
        [Tooltip("프로젝타일 조준에 사용할 고정 타겟 지점을 계산하는 방식입니다. UseDefaultTargeting은 기존 동작을 유지합니다.")]
        public ProjectileTargetPointPolicy targetPointPolicy = ProjectileTargetPointPolicy.UseDefaultTargeting;

        [Tooltip("targetPointPolicy가 FixedOffsetFromTargetCenter일 때, 타겟 중심을 기준으로 적용할 오프셋입니다.")]
        public Vector2 fixedTargetOffset = Vector2.zero;

        [Tooltip("targetPointPolicy가 FixedNormalizedPointInTargetHitArea일 때, 타겟 HitArea 내부에서 사용할 정규화된 지점입니다. (0,0)=좌측 하단, (1,1)=우측 상단")]
        public Vector2 fixedTargetHitAreaNormalized = new(0.5f, 0.5f);

        [Header("Projectile Hit Behavior Override")]
        [Tooltip("프로젝타일의 적중 생명 주기와 데미지 방식을 이벤트 단위로 덮어쓸지 여부입니다.")]
        public bool useProjectileHitBehaviorOverride = false;

        [Tooltip("프로젝타일이 타겟/지형과 충돌했을 때 발사체를 언제 제거할지 결정합니다.")]
        public ProjectileConstants.HitLifetimeMode hitLifetimeMode = ProjectileConstants.HitLifetimeMode.DestroyOnTargetHit;

        [Tooltip("프로젝타일이 데미지를 적용하는 방식을 이벤트 단위로 덮어씁니다.")]
        public ProjectileConstants.DamageApplyMode damageApplyMode = ProjectileConstants.DamageApplyMode.OnHit;

        [Tooltip("PeriodicOverlap일 때 몇 초 간격으로 데미지를 적용할지 설정합니다.")]
        public float tickDamageIntervalSeconds = 0.25f;

        [Header("Environment Hit Effect")]
        [Tooltip("타겟이 아닌 Ground/Wall 환경 Collider와 충돌했을 때 Hit VFX를 출력할지 여부입니다.")]
        public bool useEnvironmentHitPolicyOverride = false;

        [Tooltip("환경 Collider와 충돌했을 때 Hit VFX 출력 및 발사체 수명을 어떻게 처리할지 결정합니다.")]
        public ProjectileConstants.EnvironmentHitPolicy environmentHitPolicy = ProjectileConstants.EnvironmentHitPolicy.Ignore;

        [Tooltip("기본 환경 충돌 레이어로 GGemCo_TileMapGround, GGemCo_TileMapWall을 사용합니다. 끄면 customEnvironmentHitLayerMask를 사용합니다.")]
        public bool useDefaultGroundWallEnvironmentLayers = true;

        [Tooltip("useDefaultGroundWallEnvironmentLayers가 꺼져 있을 때 사용할 커스텀 환경 충돌 레이어입니다.")]
        public LayerMask customEnvironmentHitLayerMask = 0;

        [Header("Chain Cancel")]
        [Tooltip("이 Projectile 이벤트가 실제 데미지를 확정했을 때 다음 스킬 연계를 즉시 허용할지 여부입니다. GGemCoSkillSettings.enableSkillChainOnConfirmedDamage 가 함께 켜져 있어야 동작합니다.")]
        public bool allowSkillChainOnConfirmedDamage = false;

        [Header("OnHit Element Gauge")]
        public OnHitElementGaugeEntry[] onHitElementGauges;

        [Header("Targeting Overrides")]
        [Tooltip("스킬 기본 TargetingMode 대신, 이벤트 별 TargetingMode를 강제할 수 있습니다.")]
        public TargetingOverride targetingOverride;
    }
}
