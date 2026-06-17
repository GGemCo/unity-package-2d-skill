using Config;
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
        [Header("Actor")]
        [Tooltip("레이저를 발사할 캐릭터 참조 방식입니다. Caster를 선택하면 actorKey는 무시됩니다.")]
        public DummyActorReferenceType actorReferenceType = DummyActorReferenceType.Caster;

        [Tooltip("actorReferenceType이 Actor일 때 사용할 더미 캐릭터 식별 키입니다.")]
        public string actorKey = "dummy_1";

        [Tooltip("actorKey에 해당하는 더미 캐릭터를 찾지 못했을 때 처리 정책입니다.")]
        public DummyMissingActorPolicy missingActorPolicy = DummyMissingActorPolicy.Warn;

        [Header("Laser (Core Laser Table)")]
        [Tooltip("Core laser 테이블 UID입니다.")]
        public int laserUid;

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
        /// 레이저 기본 피해량에 적용할 이벤트 단위 배율입니다.
        /// </summary>
        /// <remarks>
        /// 1은 기본값이며, 2는 2배, 0.5는 절반의 피해를 의미합니다.
        /// </remarks>
        public float multiplier = 1.0f;

        [Header("Timing")]
        [Tooltip("레이저 유지 시간(초)입니다. 0 이하이면 1프레임성 레이저처럼 동작합니다.")]
        public float durationSeconds = 0.25f;

        [Tooltip("레이저 발사 후 데미지 적용을 시작할 지연 시간(초)입니다.")]
        public float damageStartDelaySeconds = 0f;

        [Tooltip("데미지 판정을 유지할 시간(초)입니다. 0 이하이면 레이저 유지 시간 동안 계속 판정합니다.")]
        public float damageActiveDurationSeconds = -1f;

        [Tooltip("같은 대상에게 반복 데미지를 줄 간격(초)입니다. 0이면 진입 시 1회만 적용합니다.")]
        public float damageTickIntervalSeconds = 0f;

        [Tooltip("반복 데미지 간격이 있을 때 판정 시작 즉시 1회 데미지를 적용할지 여부입니다.")]
        public bool damageTickOnStart = true;

        [Header("Range / Aim")]
        [Tooltip("최대 사거리 오버라이드입니다. 0 이하이면 타겟/좌표 기반 거리 또는 기본값을 사용합니다.")]
        public float maxDistance = 0f;

        [Tooltip("레이저 유지 시간 동안 타겟/조준 방향을 계속 갱신할지 여부입니다.")]
        public bool updateAimContinuously = false;

        [Header("타겟 지점 정책")]
        [Tooltip("레이저 조준에 사용할 고정 타겟 지점을 계산하는 방식입니다. UseDefaultTargeting은 기존 동작을 유지합니다.")]
        public LaserTargetPointPolicy targetPointPolicy = LaserTargetPointPolicy.UseDefaultTargeting;

        [Tooltip("targetPointPolicy가 FixedOffsetFromTargetCenter일 때, 타겟 중심을 기준으로 적용할 오프셋입니다.")]
        public Vector2 fixedTargetOffset = Vector2.zero;

        [Tooltip("targetPointPolicy가 FixedNormalizedPointInTargetHitArea일 때, 타겟 HitArea 내부에서 사용할 정규화된 지점입니다. (0,0)=좌측 하단, (1,1)=우측 상단")]
        public Vector2 fixedTargetHitAreaNormalized = new(0.5f, 0.5f);

        [Header("Target Position Reference")]
        [Tooltip("레이저가 조준할 타겟 좌표를 기존 타겟팅, 스킬 시작 스냅샷, 또는 이름 있는 위치 앵커 중 어디에서 가져올지 지정합니다.")]
        public SkillPositionReference targetPositionReference;

        [Header("Angle Overrides")]
        [Tooltip("레이캐스트 방향 모드 오버라이드 사용 여부입니다. 켜지면 laser 테이블의 RaycastDirectionMode 대신 이 값을 사용합니다.")]
        public bool useRaycastDirectionModeOverride = false;

        [Tooltip("레이캐스트 방향 모드 오버라이드 값입니다.")]
        public LaserConstants.RaycastDirectionMode raycastDirectionModeOverride = LaserConstants.RaycastDirectionMode.TowardTarget;

        [Tooltip("레이캐스트 각도 오버라이드 사용 여부입니다. 켜지면 laser 테이블의 RaycastAngleDeg 대신 이 값을 사용합니다.")]
        public bool useRaycastAngleOverride = false;

        [Tooltip("레이캐스트 각도 오버라이드 값(도)입니다. RaycastDirectionMode가 ByAngle일 때 사용됩니다.")]
        public float raycastAngleOverrideDeg = 0f;

        [Tooltip("VFX 각도 동기화 모드 오버라이드 사용 여부입니다. 켜지면 laser 테이블의 VfxAngleSyncMode 대신 이 값을 사용합니다.")]
        public bool useVfxAngleSyncModeOverride = false;

        [Tooltip("VFX 각도 동기화 모드 오버라이드 값입니다.")]
        public LaserConstants.VfxAngleSyncMode vfxAngleSyncModeOverride = LaserConstants.VfxAngleSyncMode.FollowRaycast;

        [Header("Start Position Override")]
        [Tooltip("레이저 시작점 오버라이드 값을 어떤 기준점에서 해석할지 정의합니다.")]
        public LaserStartAnchor startAnchor = LaserStartAnchor.Caster;

        [Tooltip("startAnchor가 NamedPositionAnchor일 때 참조할 위치 앵커 키입니다.")]
        public string namedAnchorKey;

        [Tooltip("레이저 시작점 오버라이드 해석 방식입니다.")]
        public LaserConstants.StartPositionOverrideMode startPositionOverrideMode = LaserConstants.StartPositionOverrideMode.UseLaserTable;

        [Tooltip("레이저 시작점 오버라이드 값입니다. 기준점과 모드에 따라 월드 좌표 또는 오프셋으로 해석됩니다.")]
        public Vector2 startPositionOverride = Vector2.zero;

        [Tooltip("Caster가 좌우 반전된 상태일 때 레이저 시작점 오프셋의 X 값을 반전할지 여부입니다. WorldPosition 모드에는 적용되지 않습니다.")]
        public bool useCasterFlipStartOffsetX = false;

        [Tooltip("레이저 시작점을 발사 후에도 계속 갱신할지, 발사 시점에 고정할지 정의합니다. startAnchor가 Caster가 아니면 현재는 발사 시점 좌표로 고정됩니다.")]
        public LaserConstants.StartPointUpdateMode startPointUpdateMode = LaserConstants.StartPointUpdateMode.FollowOwner;

        [Header("Visual Overrides (optional)")]
        public float scaleMultiplier = 1f;
        public ProjectileConstants.ProjectileVisualType visualType = ProjectileConstants.ProjectileVisualType.Default;
        public Sprite visualSprite;
        public RuntimeAnimatorController visualAnimatorController;
        public int visualVfxUidOverride = 0;

        [Header("Chain Cancel")]
        [Tooltip("이 Laser 이벤트가 실제 데미지를 확정했을 때 다음 스킬 연계를 즉시 허용할지 여부입니다. GGemCoSkillSettings.enableSkillChainOnConfirmedDamage 가 함께 켜져 있어야 동작합니다.")]
        public bool allowSkillChainOnConfirmedDamage = false;

        [Header("OnHit Crowd Control (Target)")]
        [Tooltip("레이저 적중 시 대상에게 적용할 Crowd Control 후보 목록입니다.")]
        public OnHitCrowdControlEntry[] onHitCrowdControls;

        [Header("OnHit MP Gain")]
        [Min(0), Tooltip("이 Laser 이벤트가 실제 타격에 성공했을 때 공격자에게 지급할 MP입니다. 0이면 지급하지 않습니다.")]
        public int skillHitMpGain = 0;

        [Tooltip("같은 AttackId 안에서 이 Laser 이벤트의 MP 보상을 반복 지급할지 여부입니다.")]
        public bool allowMultipleSkillHitMpGainPerAttack = false;

        [Header("Guard")]
        [Tooltip("GGemCoPlayerGuardSettings에서 가드 성공/브레이크/추가 CC를 결정할 때 사용하는 공격 방어 타입입니다.")]
        public GuardAttackType guardAttackType = GuardAttackType.Normal;

        [Header("Targeting Overrides")]
        [Tooltip("스킬 기본 TargetingMode 대신, 이벤트 별 TargetingMode를 강제할 수 있습니다.")]
        public TargetingOverride targetingOverride;
    }
}
