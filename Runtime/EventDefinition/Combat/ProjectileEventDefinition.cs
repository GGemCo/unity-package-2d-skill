using Config;
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
        [Tooltip("Core projectile_linear/projectile_arc/projectile_path 테이블의 Uid")]
        public int projectileUid;

        [Header("Combat")]
        public ConfigCommon.DamageType damageType = ConfigCommon.DamageType.None;
        public long damage = 0;

        /// <summary>
        /// Damage 오버라이드 값을 해석할 방식입니다.
        /// </summary>
        /// <remarks>
        /// damage가 0 이하이면 skill/skill_monster 테이블의 DamageValueType을 사용하므로 이 값은 무시됩니다.
        /// </remarks>
        public ConfigCommonSkill.SkillDamageValueType damageValueType = ConfigCommonSkill.SkillDamageValueType.Fixed;

        /// <summary>
        /// 프로젝타일 기본 피해량에 적용할 이벤트 단위 배율입니다.
        /// </summary>
        /// <remarks>
        /// 1은 기본값이며, 2는 2배, 0.5는 절반의 피해를 의미합니다.
        /// </remarks>
        public float multiplier = 1.0f;

        [Header("OnHit Crowd Control (Target)")]
        [Tooltip("프로젝타일 적중 시 대상에게 적용할 Crowd Control 후보 목록입니다.")]
        public OnHitCrowdControlEntry[] onHitCrowdControls;

        [Header("Guard")]
        [Tooltip("GGemCoPlayerGuardSettings에서 가드 성공/브레이크/추가 CC를 결정할 때 사용하는 공격 방어 타입입니다.")]
        public GuardAttackType guardAttackType = GuardAttackType.Normal;

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

        [Header("Hit VFX Position")]
        [Tooltip("발사체가 타겟에 적중했을 때 Hit VFX를 출력할 위치 정책입니다. enum 0번은 기존 동작을 보존하는 CollisionPoint입니다.")]
        public ProjectileConstants.HitVfxPositionPolicy hitVfxPositionPolicy = ProjectileConstants.HitVfxPositionPolicy.CollisionPoint;

        [Tooltip("Hit VFX 위치 계산 후 더할 월드 오프셋입니다. TargetOffset 정책에서는 타겟 중심 기준 보정값으로 사용됩니다.")]
        public Vector2 hitVfxOffset = Vector2.zero;

        [Tooltip("hitVfxPositionPolicy가 TargetHitAreaNormalized일 때, 타겟 HitArea 내부에서 사용할 정규화된 지점입니다. (0,0)=좌측 하단, (1,1)=우측 상단")]
        public Vector2 hitVfxHitAreaNormalized = new(0.5f, 0.5f);

        [Header("Projectile Hit Behavior Override")]
        [Tooltip("프로젝타일의 적중 생명 주기와 데미지 방식을 이벤트 단위로 덮어쓸지 여부입니다.")]
        public bool useProjectileHitBehaviorOverride = false;

        [Tooltip("프로젝타일이 타겟/지형과 충돌했을 때 발사체를 언제 제거할지 결정합니다.")]
        public ProjectileConstants.HitLifetimeMode hitLifetimeMode = ProjectileConstants.HitLifetimeMode.DestroyOnTargetHit;

        [Tooltip("프로젝타일이 데미지를 적용하는 방식을 이벤트 단위로 덮어씁니다.")]
        public ProjectileConstants.DamageApplyMode damageApplyMode = ProjectileConstants.DamageApplyMode.OnHit;

        [Tooltip("PeriodicOverlap일 때 몇 초 간격으로 데미지를 적용할지 설정합니다.")]
        public float tickDamageIntervalSeconds = 0.25f;

        [Header("Projectile Arrival Override")]
        [Tooltip("프로젝타일이 목표 지점 도달 시 제거 정책을 이벤트 단위로 덮어쓸지 여부입니다.")]
        public bool useArrivalPolicyOverride = false;

        [Tooltip("프로젝타일이 목표 지점에 도달했을 때 제거할지, 계속 이동할지 결정합니다.")]
        public ProjectileConstants.ArrivalPolicy arrivalPolicy = ProjectileConstants.ArrivalPolicy.DestroyOnArrived;

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
