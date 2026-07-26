using System;
using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 레이저 발사 이벤트를 정의하는 스킬 Timeline 클립입니다.
    /// - Projectile 이벤트와 분리된 Laser 전용 런타임 경로를 사용합니다.
    /// - 정적 데이터는 Core laser 테이블을 참조합니다.
    /// </summary>
    [Serializable]
    public sealed class SkillLaserClip : SkillEventClipBase
    {
        [Header("Actor")]
        [Tooltip("레이저를 발사할 캐릭터 참조 방식입니다. Caster를 선택하면 actorKey는 무시됩니다.")]
        [SerializeField] private DummyActorReferenceType actorReferenceType = DummyActorReferenceType.Caster;

        [Tooltip("actorReferenceType이 Actor일 때 사용할 더미 캐릭터 식별 키입니다.")]
        [SerializeField] private string actorKey = "dummy_1";

        [Tooltip("actorKey에 해당하는 더미 캐릭터를 찾지 못했을 때 처리 정책입니다.")]
        [SerializeField] private DummyMissingActorPolicy missingActorPolicy = DummyMissingActorPolicy.Warn;

        [Header("Laser (Core Laser Table)")]
        [Tooltip("Core laser 테이블 UID입니다.")]
        [SerializeField] private int laserUid = 0;

        [Header("Combat")]
        [Tooltip("[피해 배율] 레이저 기본 피해량에 곱해지는 계수입니다. 1=기본, 2=2배, 0.5=절반")]
        [SerializeField] private float multiplier = 1.0f;

        [Header("Combat-Override")]
        [Tooltip("레이저 적중 시 적용할 피해 유형입니다.")]
        [SerializeField] private ConfigCommon.DamageType damageType = ConfigCommon.DamageType.None;

        [Tooltip("레이저 적중 시 적용할 기본 피해량입니다.")]
        [SerializeField] private long damage = 0;

        [Tooltip("Damage 오버라이드 값이 0보다 클 때, 해당 값을 어떤 기준으로 해석할지 지정합니다.")]
        [SerializeField] private ConfigCommonSkill.SkillDamageValueType damageValueType = ConfigCommonSkill.SkillDamageValueType.Fixed;

        [Header("OnHit Crowd Control (Target)")]
        [Tooltip("레이저 적중 시 대상에게 적용할 Crowd Control 후보 목록입니다.")]
        [SerializeField] private GGemCo2DSkill.OnHitCrowdControlEntry[] onHitCrowdControls;

        [Header("OnHit MP Gain")]
        [Tooltip("이 Laser 클립이 실제 타격에 성공했을 때 공격자에게 지급할 MP입니다. 0이면 지급하지 않습니다.")]
        [SerializeField] private int skillHitMpGain = 0;

        [Tooltip("같은 AttackId 안에서 이 Laser 클립의 MP 보상을 반복 지급할지 여부입니다.")]
        [SerializeField] private bool allowMultipleSkillHitMpGainPerAttack = false;

        [Header("Guard")]
        [Tooltip("[공격 방어 타입] GGemCoPlayerGuardSettings에서 가드 성공/브레이크/추가 CC를 결정할 때 사용하는 타입입니다.")]
        [SerializeField] private GuardAttackType guardAttackType = GuardAttackType.Normal;

        [Header("Timing")]
        [Tooltip("레이저 유지 시간(초)입니다.")]
        [SerializeField] private float durationSeconds = 0.25f;

        [Tooltip("레이저 발사 후 데미지 적용을 시작할 지연 시간(초)입니다.")]
        [SerializeField] private float damageStartDelaySeconds = 0f;

        [Tooltip("데미지 판정을 유지할 시간(초)입니다. 0 이하이면 레이저 유지 시간 동안 계속 판정합니다.")]
        [SerializeField] private float damageActiveDurationSeconds = -1f;

        [Tooltip("같은 대상에게 반복 데미지를 줄 간격(초)입니다. 0이면 진입 시 1회만 적용합니다.")]
        [SerializeField] private float damageTickIntervalSeconds = 0f;

        [Tooltip("반복 데미지 간격이 있을 때 판정 시작 즉시 1회 데미지를 적용할지 여부입니다.")]
        [SerializeField] private bool damageTickOnStart = true;

        [Header("Range")]
        [Tooltip("최대 사거리 오버라이드입니다. 0 이하이면 타겟/좌표 기반 거리 또는 기본값을 사용합니다.")]
        [SerializeField] private float maxDistance = 0f;

        [Header("Aim")]
        [Tooltip("타겟이 위치한 좌우 수평 방향을 기준으로 적용할 각도입니다. 0은 수평 직선이며, 양수는 위쪽, 음수는 아래쪽입니다.")]
        [SerializeField] private float targetHorizontalAngleDeg = 0f;

        [Header("Start Position Override")]
        [Tooltip("레이저 시작점 오버라이드 값을 어떤 기준점에서 해석할지 정의합니다.")]
        [SerializeField] private LaserStartAnchor startAnchor = LaserStartAnchor.Caster;

        [Tooltip("startAnchor가 NamedPositionAnchor일 때 참조할 위치 앵커 키입니다.")]
        [SerializeField] private string namedAnchorKey;

        [Tooltip("레이저 시작점 오버라이드 해석 방식입니다.")]
        [SerializeField] private LaserConstants.StartPositionOverrideMode startPositionOverrideMode = LaserConstants.StartPositionOverrideMode.UseLaserTable;

        [Tooltip("레이저 시작점 오버라이드 값입니다. 기준점과 모드에 따라 월드 좌표 또는 오프셋으로 해석됩니다.")]
        [SerializeField] private Vector2 startPositionOverride = Vector2.zero;

        [Tooltip("Caster가 좌우 반전된 상태일 때 레이저 시작점 오프셋의 X 값을 반전할지 여부입니다. WorldPosition 모드에는 적용되지 않습니다.")]
        [SerializeField] private bool useCasterFlipStartOffsetX = false;

        [Tooltip("레이저 시작점을 발사 후에도 계속 갱신할지, 발사 시점에 고정할지 정의합니다. startAnchor가 Caster가 아니면 현재는 발사 시점 좌표로 고정됩니다.")]
        [SerializeField] private LaserConstants.StartPointUpdateMode startPointUpdateMode = LaserConstants.StartPointUpdateMode.FollowOwner;

        [Header("Visual Overrides (optional)")]
        [Tooltip("레이저 비주얼 스케일 배율입니다.")]
        [SerializeField] private float scaleMultiplier = 1f;

        [Tooltip("레이저 비주얼 표현 방식입니다.")]
        [SerializeField] private ProjectileConstants.ProjectileVisualType visualType = ProjectileConstants.ProjectileVisualType.Default;

        [Tooltip("레이저 표시용 스프라이트 오버라이드입니다.")]
        [SerializeField] private Sprite visualSprite;

        [Tooltip("레이저 표시용 Animator Controller 오버라이드입니다.")]
        [SerializeField] private RuntimeAnimatorController visualAnimatorController;

        [Tooltip("기본 이펙트 대신 사용할 Effect UID 오버라이드입니다.")]
        [SerializeField] private int visualVfxUidOverride = 0;

        [Tooltip("레이저에 부착된 VFX의 재생 및 종료 수명 정책입니다.")]
        [SerializeField] private ProjectileConstants.AttachedVfxPlaybackPolicy attachedVfxPlaybackPolicy =
            ProjectileConstants.AttachedVfxPlaybackPolicy.OwnerLifetime;

        [Header("Targeting Overrides")]
        [Tooltip("레이저의 타게팅 규칙을 보정하기 위한 오버라이드 설정입니다.")]
        [SerializeField] private GGemCo2DSkill.TargetingOverride targetingOverride;

        [Header("Chain Cancel")]
        [Tooltip("이 Laser 이벤트가 실제 데미지를 확정했을 때 다음 스킬 연계를 즉시 허용할지 여부입니다. GGemCoSkillSettings.enableSkillChainOnConfirmedDamage 가 함께 켜져 있어야 동작합니다.")]
        [SerializeField] private bool allowSkillChainOnConfirmedDamage = false;

        /// <summary>
        /// 이 클립이 표현하는 스킬 이벤트 유형입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Laser;

        /// <summary>
        /// 레이저를 발사할 캐릭터 참조 방식을 반환합니다.
        /// </summary>
        public DummyActorReferenceType ActorReferenceType => actorReferenceType;

        /// <summary>
        /// 레이저를 발사할 더미 캐릭터의 actorKey를 반환합니다.
        /// </summary>
        public string ActorKey => actorKey;

        /// <summary>
        /// 레이저 발사 주체를 찾지 못했을 때 사용할 처리 정책을 반환합니다.
        /// </summary>
        public DummyMissingActorPolicy MissingActorPolicy => missingActorPolicy;

        /// <summary>
        /// 사용할 레이저 정의 UID입니다.
        /// </summary>
        public int LaserUid => laserUid;

        /// <summary>
        /// 적용할 데미지 타입입니다.
        /// </summary>
        public ConfigCommon.DamageType DamageType => damageType;

        /// <summary>
        /// 적용할 데미지 값입니다.
        /// </summary>
        public long Damage => damage;

        /// <summary>
        /// Damage 오버라이드 값을 해석할 방식입니다.
        /// </summary>
        /// <remarks>
        /// Damage가 0 이하이면 스킬 테이블의 DamageValueType을 사용하므로 이 값은 무시됩니다.
        /// </remarks>
        public ConfigCommonSkill.SkillDamageValueType DamageValueType => damageValueType;

        /// <summary>
        /// 레이저 기본 피해량에 적용할 이벤트 단위 배율입니다.
        /// </summary>
        public float Multiplier => multiplier;

        /// <summary>
        /// 레이저 유지 시간입니다.
        /// </summary>
        public float DurationSeconds => durationSeconds;

        /// <summary>
        /// 데미지 적용을 시작할 지연 시간입니다.
        /// </summary>
        public float DamageStartDelaySeconds => damageStartDelaySeconds;

        /// <summary>
        /// 데미지 판정을 유지할 시간입니다.
        /// </summary>
        public float DamageActiveDurationSeconds => damageActiveDurationSeconds;

        /// <summary>
        /// 같은 대상에게 반복 데미지를 줄 간격입니다.
        /// </summary>
        public float DamageTickIntervalSeconds => damageTickIntervalSeconds;

        /// <summary>
        /// 반복 데미지 간격이 있을 때 판정 시작 즉시 데미지를 적용할지 여부입니다.
        /// </summary>
        public bool DamageTickOnStart => damageTickOnStart;

        /// <summary>
        /// 최대 사거리 오버라이드입니다.
        /// </summary>
        public float MaxDistance => maxDistance;

        /// <summary>
        /// 스킬 타겟의 좌우 수평 방향을 기준으로 적용할 레이저 각도를 반환합니다.
        /// </summary>
        public float TargetHorizontalAngleDeg => targetHorizontalAngleDeg;

        /// <summary>
        /// 레이저 시작점 오버라이드 기준점입니다.
        /// </summary>
        public LaserStartAnchor StartAnchor => startAnchor;

        /// <summary>
        /// 이름 있는 위치 앵커 키입니다.
        /// </summary>
        public string NamedAnchorKey => namedAnchorKey;

        /// <summary>
        /// 레이저 시작점 오버라이드 해석 방식입니다.
        /// </summary>
        public LaserConstants.StartPositionOverrideMode StartPositionOverrideMode => startPositionOverrideMode;

        /// <summary>
        /// 레이저 시작점 오버라이드 값입니다.
        /// </summary>
        public Vector2 StartPositionOverride => startPositionOverride;

        /// <summary>
        /// Caster 좌우 반전 상태에 따라 레이저 시작점 오프셋 X 값을 반전할지 여부입니다.
        /// </summary>
        public bool UseCasterFlipStartOffsetX => useCasterFlipStartOffsetX;

        /// <summary>
        /// 레이저 시작점 갱신 방식입니다.
        /// </summary>
        public LaserConstants.StartPointUpdateMode StartPointUpdateMode => startPointUpdateMode;

        /// <summary>
        /// 비주얼 스케일 배율입니다.
        /// </summary>
        public float ScaleMultiplier => scaleMultiplier;

        /// <summary>
        /// 비주얼 표현 방식입니다.
        /// </summary>
        public ProjectileConstants.ProjectileVisualType VisualType => visualType;

        /// <summary>
        /// 스프라이트 비주얼 오버라이드입니다.
        /// </summary>
        public Sprite VisualSprite => visualSprite;

        /// <summary>
        /// 애니메이터 비주얼 오버라이드입니다.
        /// </summary>
        public RuntimeAnimatorController VisualAnimatorController => visualAnimatorController;

        /// <summary>
        /// VFX UID 오버라이드입니다.
        /// </summary>
        public int VisualVfxUidOverride => visualVfxUidOverride;

        /// <summary>
        /// 레이저에 부착된 VFX의 재생 및 종료 수명 정책을 반환합니다.
        /// </summary>
        public ProjectileConstants.AttachedVfxPlaybackPolicy AttachedVfxPlaybackPolicy => attachedVfxPlaybackPolicy;

        /// <summary>
        /// 타게팅 오버라이드 설정입니다.
        /// </summary>
        public GGemCo2DSkill.TargetingOverride TargetingOverride => targetingOverride;

        /// <summary>
        /// 실제 데미지 확정 시 스킬 연계를 즉시 허용할지 여부입니다.
        /// </summary>
        public bool AllowSkillChainOnConfirmedDamage => allowSkillChainOnConfirmedDamage;

        /// <summary>
        /// 레이저 적중 시 적용될 Crowd Control 후보 목록입니다.
        /// </summary>
        public GGemCo2DSkill.OnHitCrowdControlEntry[] OnHitCrowdControls => onHitCrowdControls;

        /// <summary>
        /// 실제 타격 성공 시 공격자에게 지급할 MP를 반환합니다.
        /// </summary>
        public int SkillHitMpGain => Mathf.Max(0, skillHitMpGain);

        /// <summary>
        /// 같은 AttackId에서 스킬 타격 MP 보상을 반복 지급할지 여부를 반환합니다.
        /// </summary>
        public bool AllowMultipleSkillHitMpGainPerAttack => allowMultipleSkillHitMpGainPerAttack;

        /// <summary>
        /// 이 레이저 공격이 가드 설정에서 어떤 공격 방어 타입으로 처리될지 반환합니다.
        /// </summary>
        public GuardAttackType GuardAttackType => guardAttackType;

        /// <summary>
        /// 적중 시 추가할 속성 게이지 목록입니다.
        /// </summary>
    }
}
