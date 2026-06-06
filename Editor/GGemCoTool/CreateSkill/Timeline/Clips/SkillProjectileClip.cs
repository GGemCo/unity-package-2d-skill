using System;
using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 프로젝타일(투사체) 발사 이벤트를 정의하는 스킬 Timeline 클립입니다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이 클립은 스킬 제작 단계의 Authoring 데이터이며,
    /// Bake 시 <see cref="GGemCo2DSkill.ProjectileEventDefinition"/> Payload로 변환됩니다.
    /// </para>
    /// <para>
    /// 프로젝타일 기본 정보, 전투 수치, 시각적 오버라이드, 타게팅 오버라이드를 함께 정의하여
    /// 런타임 투사체 동작을 구성합니다.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class SkillProjectileClip : SkillEventClipBase
    {
        [Header("Projectile (Core Table)")]
        [Tooltip("Core projectile 테이블에 정의된 프로젝타일 UID입니다.")]
        [SerializeField] private int projectileUid = 0;

        [Header("Combat")]
        [Tooltip("[피해 배율] 프로젝타일 기본 피해량에 곱해지는 계수입니다. 1=기본, 2=2배, 0.5=절반")]
        [SerializeField] private float multiplier = 1.0f;
        
        [Header("Combat-Override")]
        [Tooltip("프로젝타일 적중 시 적용할 피해 유형 오버라이드입니다. None이면 skill/skill_monster 테이블의 DamageType을 사용합니다.")]
        [SerializeField] private ConfigCommon.DamageType damageType = ConfigCommon.DamageType.None;

        [Tooltip("프로젝타일 적중 시 적용할 피해량 오버라이드입니다. 0이면 skill/skill_monster 테이블의 Damage를 사용합니다.")]
        [SerializeField] private long damage = 0;

        [Tooltip("Damage 오버라이드 값이 0보다 클 때, 해당 값을 어떤 기준으로 해석할지 지정합니다.")]
        [SerializeField] private ConfigCommonSkill.SkillDamageValueType damageValueType = ConfigCommonSkill.SkillDamageValueType.Fixed;


        [Header("OnHit Crowd Control (Target)")]
        [Tooltip("프로젝타일 적중 시 대상에게 적용할 Crowd Control 후보 목록입니다.")]
        [SerializeField] private OnHitCrowdControlEntry[] onHitCrowdControls;

        [Header("Guard")]
        [Tooltip("[공격 방어 타입] GGemCoPlayerGuardSettings에서 가드 성공/브레이크/추가 CC를 결정할 때 사용하는 타입입니다.")]
        [SerializeField] private GuardAttackType guardAttackType = GuardAttackType.Normal;

        [Header("Dynamic Multipliers")]
        [Tooltip("프로젝타일 기본 속도에 곱해지는 배율입니다.")]
        [SerializeField] private float speedMultiplier = 1f;

        [Tooltip("프로젝타일 기본 스케일에 곱해지는 배율입니다.")]
        [SerializeField] private float scaleMultiplier = 1f;

        [Header("Visual Overrides (optional)")]
        [Tooltip("프로젝타일의 비주얼 표현 방식입니다.")]
        [SerializeField] private ProjectileConstants.ProjectileVisualType visualType = ProjectileConstants.ProjectileVisualType.Default;

        [Tooltip("프로젝타일 표시용 스프라이트 오버라이드입니다.")]
        [SerializeField] private Sprite visualSprite;

        [Tooltip("프로젝타일 표시용 Animator Controller 오버라이드입니다.")]
        [SerializeField] private RuntimeAnimatorController visualAnimatorController;

        [Tooltip("기본 이펙트 대신 사용할 Effect UID 오버라이드입니다.")]
        [SerializeField] private int visualVfxUidOverride = 0;

        [Header("Targeting Overrides")]
        [Tooltip("프로젝타일의 타게팅 규칙을 보정하기 위한 오버라이드 설정입니다.")]
        [SerializeField] private TargetingOverride targetingOverride;

        [Header("타겟 지점 정책")]
        [Tooltip("프로젝타일 조준에 사용할 고정 타겟 지점을 계산하는 방식입니다. UseDefaultTargeting은 기존 동작을 유지합니다.")]
        [SerializeField] private ProjectileTargetPointPolicy targetPointPolicy = ProjectileTargetPointPolicy.UseDefaultTargeting;

        [Tooltip("targetPointPolicy가 FixedOffsetFromTargetCenter일 때, 타겟 중심을 기준으로 적용할 오프셋입니다.")]
        [SerializeField] private Vector2 fixedTargetOffset = Vector2.zero;

        [Tooltip("targetPointPolicy가 FixedNormalizedPointInTargetHitArea일 때, 타겟 HitArea 내부에서 사용할 정규화된 지점입니다. (0,0)=좌측 하단, (1,1)=우측 상단")]
        [SerializeField] private Vector2 fixedTargetHitAreaNormalized = new(0.5f, 0.5f);

        [Header("Hit VFX Position")]
        [Tooltip("발사체가 타겟에 적중했을 때 Hit VFX를 출력할 위치 정책입니다. enum 0번은 기존 동작을 보존하는 CollisionPoint입니다.")]
        [SerializeField] private ProjectileConstants.HitVfxPositionPolicy hitVfxPositionPolicy = ProjectileConstants.HitVfxPositionPolicy.CollisionPoint;

        [Tooltip("Hit VFX 위치 계산 후 더할 월드 오프셋입니다. TargetOffset 정책에서는 타겟 중심 기준 보정값으로 사용됩니다.")]
        [SerializeField] private Vector2 hitVfxOffset = Vector2.zero;

        [Tooltip("hitVfxPositionPolicy가 TargetHitAreaNormalized일 때, 타겟 HitArea 내부에서 사용할 정규화된 지점입니다. (0,0)=좌측 하단, (1,1)=우측 상단")]
        [SerializeField] private Vector2 hitVfxHitAreaNormalized = new(0.5f, 0.5f);

        [Header("Projectile Hit Behavior Override")]
        [Tooltip("프로젝타일의 적중 생명 주기와 데미지 방식을 이벤트 단위로 덮어쓸지 여부입니다.")]
        [SerializeField] private bool useProjectileHitBehaviorOverride = false;

        [Tooltip("프로젝타일이 타겟/지형과 충돌했을 때 발사체를 언제 제거할지 결정합니다.")]
        [SerializeField] private ProjectileConstants.HitLifetimeMode hitLifetimeMode = ProjectileConstants.HitLifetimeMode.DestroyOnTargetHit;

        [Tooltip("프로젝타일이 데미지를 적용하는 방식을 이벤트 단위로 지정합니다.")]
        [SerializeField] private ProjectileConstants.DamageApplyMode damageApplyMode = ProjectileConstants.DamageApplyMode.OnHit;

        [Tooltip("PeriodicOverlap일 때 몇 초 간격으로 데미지를 적용할지 설정합니다.")]
        [SerializeField] private float tickDamageIntervalSeconds = 0.25f;

        [Header("Projectile Arrival Override")]
        [Tooltip("프로젝타일이 목표 지점 도달 시 제거 정책을 이벤트 단위로 덮어쓸지 여부입니다.")]
        [SerializeField] private bool useArrivalPolicyOverride = false;

        [Tooltip("프로젝타일이 목표 지점에 도달했을 때 제거할지, 계속 이동할지 결정합니다.")]
        [SerializeField] private ProjectileConstants.ArrivalPolicy arrivalPolicy = ProjectileConstants.ArrivalPolicy.DestroyOnArrived;

        [Header("Environment Hit Effect")]
        [Tooltip("타겟이 아닌 Ground/Wall 환경 Collider와 충돌했을 때 Hit VFX를 출력할지 여부입니다.")]
        [SerializeField] private bool useEnvironmentHitPolicyOverride = false;

        [Tooltip("환경 Collider와 충돌했을 때 Hit VFX 출력 및 발사체 수명을 어떻게 처리할지 결정합니다.")]
        [SerializeField] private ProjectileConstants.EnvironmentHitPolicy environmentHitPolicy = ProjectileConstants.EnvironmentHitPolicy.Ignore;

        [Tooltip("기본 환경 충돌 레이어로 GGemCo_TileMapGround, GGemCo_TileMapWall을 사용합니다. 끄면 customEnvironmentHitLayerMask를 사용합니다.")]
        [SerializeField] private bool useDefaultGroundWallEnvironmentLayers = true;

        [Tooltip("useDefaultGroundWallEnvironmentLayers가 꺼져 있을 때 사용할 커스텀 환경 충돌 레이어입니다.")]
        [SerializeField] private LayerMask customEnvironmentHitLayerMask = 0;

        [Header("Chain Cancel")]
        [Tooltip("이 Projectile 이벤트가 실제 데미지를 확정했을 때 다음 스킬 연계를 즉시 허용할지 여부입니다. GGemCoSkillSettings.enableSkillChainOnConfirmedDamage 가 함께 켜져 있어야 동작합니다.")]
        [SerializeField] private bool allowSkillChainOnConfirmedDamage = false;

        /// <summary>
        /// 이 클립이 표현하는 스킬 이벤트 유형입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Projectile;

        /// <summary>
        /// 사용할 프로젝타일 정의의 UID입니다.
        /// </summary>
        public int ProjectileUid => projectileUid;

        /// <summary>
        /// 프로젝타일이 적용할 피해 유형 오버라이드입니다.
        /// </summary>
        /// <remarks>
        /// None이면 skill/skill_monster 테이블의 DamageType 값을 사용합니다.
        /// </remarks>
        public ConfigCommon.DamageType DamageType => damageType;

        /// <summary>
        /// 프로젝타일이 적용할 피해량 오버라이드입니다.
        /// </summary>
        /// <remarks>
        /// 0이면 skill/skill_monster 테이블의 Damage 값을 사용합니다.
        /// </remarks>
        public long Damage => damage;

        /// <summary>
        /// Damage 오버라이드 값을 해석할 방식입니다.
        /// </summary>
        /// <remarks>
        /// Damage가 0 이하이면 스킬 테이블의 DamageValueType을 사용하므로 이 값은 무시됩니다.
        /// </remarks>
        public ConfigCommonSkill.SkillDamageValueType DamageValueType => damageValueType;

        /// <summary>
        /// 프로젝타일 기본 피해량에 적용할 이벤트 단위 배율입니다.
        /// </summary>
        public float Multiplier => multiplier;

        /// <summary>
        /// 프로젝타일 적중 시 적용될 Crowd Control 후보 목록입니다.
        /// </summary>
        public OnHitCrowdControlEntry[] OnHitCrowdControls => onHitCrowdControls;

        /// <summary>
        /// 이 프로젝타일 공격이 가드 설정에서 어떤 공격 방어 타입으로 처리될지 반환합니다.
        /// </summary>
        public GuardAttackType GuardAttackType => guardAttackType;

        /// <summary>
        /// 프로젝타일 속도에 적용할 배율 값입니다.
        /// </summary>
        public float SpeedMultiplier => speedMultiplier;

        /// <summary>
        /// 프로젝타일 크기에 적용할 배율 값입니다.
        /// </summary>
        public float ScaleMultiplier => scaleMultiplier;

        /// <summary>
        /// 프로젝타일 비주얼 표현 방식입니다.
        /// </summary>
        public ProjectileConstants.ProjectileVisualType VisualType => visualType;

        /// <summary>
        /// 프로젝타일에 사용할 스프라이트 오버라이드입니다.
        /// </summary>
        public Sprite VisualSprite => visualSprite;

        /// <summary>
        /// 프로젝타일에 사용할 애니메이터 컨트롤러 오버라이드입니다.
        /// </summary>
        public RuntimeAnimatorController VisualAnimatorController => visualAnimatorController;

        /// <summary>
        /// 프로젝타일 비주얼 이펙트를 대체할 UID입니다.
        /// </summary>
        public int VisualVfxUidOverride => visualVfxUidOverride;

        /// <summary>
        /// 프로젝타일의 타게팅 규칙을 보정하는 오버라이드 설정입니다.
        /// </summary>
        public TargetingOverride TargetingOverride => targetingOverride;

        /// <summary>
        /// 프로젝타일 목표점 고정 정책입니다.
        /// </summary>
        public ProjectileTargetPointPolicy TargetPointPolicy => targetPointPolicy;

        /// <summary>
        /// 타겟 중심점 기준 고정 오프셋입니다.
        /// </summary>
        public Vector2 FixedTargetOffset => fixedTargetOffset;

        /// <summary>
        /// 타겟 HitArea 정규화 좌표(0~1)입니다.
        /// </summary>
        public Vector2 FixedTargetHitAreaNormalized => fixedTargetHitAreaNormalized;

        /// <summary>
        /// 타겟 적중 시 Hit VFX를 출력할 위치 정책입니다.
        /// </summary>
        public ProjectileConstants.HitVfxPositionPolicy HitVfxPositionPolicy => hitVfxPositionPolicy;

        /// <summary>
        /// Hit VFX 위치 계산 후 더할 월드 오프셋입니다.
        /// </summary>
        public Vector2 HitVfxOffset => hitVfxOffset;

        /// <summary>
        /// Hit VFX용 타겟 HitArea 정규화 좌표(0~1)입니다.
        /// </summary>
        public Vector2 HitVfxHitAreaNormalized => hitVfxHitAreaNormalized;

        /// <summary>
        /// 프로젝타일 적중 생명 주기와 데미지 방식을 덮어쓸지 여부입니다.
        /// </summary>
        public bool UseProjectileHitBehaviorOverride => useProjectileHitBehaviorOverride;

        /// <summary>
        /// 프로젝타일이 타겟/지형과 충돌했을 때 발사체를 언제 제거할지 결정합니다.
        /// </summary>
        public ProjectileConstants.HitLifetimeMode HitLifetimeMode => hitLifetimeMode;

        /// <summary>
        /// 프로젝타일에서 사용할 데미지 적용 방식입니다.
        /// </summary>
        public ProjectileConstants.DamageApplyMode DamageApplyMode => damageApplyMode;

        /// <summary>
        /// PeriodicOverlap일 때 사용할 틱 데미지 간격(초)입니다.
        /// </summary>
        public float TickDamageIntervalSeconds => tickDamageIntervalSeconds;

        /// <summary>
        /// 프로젝타일 도착 정책을 이벤트 단위로 덮어쓸지 여부입니다.
        /// </summary>
        public bool UseArrivalPolicyOverride => useArrivalPolicyOverride;

        /// <summary>
        /// 프로젝타일이 목표 지점에 도달했을 때 적용할 제거/지속 정책입니다.
        /// </summary>
        public ProjectileConstants.ArrivalPolicy ArrivalPolicy => arrivalPolicy;

        /// <summary>
        /// 환경 Collider Hit VFX 정책을 이벤트 단위로 덮어쓸지 여부입니다.
        /// </summary>
        public bool UseEnvironmentHitPolicyOverride => useEnvironmentHitPolicyOverride;

        /// <summary>
        /// 환경 Collider와 충돌했을 때 적용할 Hit VFX 및 수명 정책입니다.
        /// </summary>
        public ProjectileConstants.EnvironmentHitPolicy EnvironmentHitPolicy => environmentHitPolicy;

        /// <summary>
        /// 기본 Ground/Wall 환경 레이어를 사용할지 여부입니다.
        /// </summary>
        public bool UseDefaultGroundWallEnvironmentLayers => useDefaultGroundWallEnvironmentLayers;

        /// <summary>
        /// 커스텀 환경 충돌 레이어 마스크입니다.
        /// </summary>
        public LayerMask CustomEnvironmentHitLayerMask => customEnvironmentHitLayerMask;

        /// <summary>
        /// 이 Projectile 이벤트가 실제 데미지를 확정했을 때 다음 스킬 연계를 즉시 허용할지 여부입니다.
        /// </summary>
        public bool AllowSkillChainOnConfirmedDamage => allowSkillChainOnConfirmedDamage;
    }
}
