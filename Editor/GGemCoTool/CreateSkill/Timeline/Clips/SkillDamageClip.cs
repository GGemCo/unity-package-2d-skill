using System;
using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬이 적중했을 때 적용되는 데미지 이벤트 클립입니다.
    /// 데미지 배율, 타입, 판정 영역 형태 및 크기, OnHit 부가 효과 정보를 정의합니다.
    /// </summary>
    [Serializable]
    public sealed class SkillDamageClip : SkillEventClipBase
    {
        /// <summary>
        /// 최종 계산된 데미지에 곱해지는 배율입니다.
        /// </summary>
        /// <remarks>
        /// 1은 기본값이며, 2는 2배, 0.5는 절반의 피해를 의미합니다.
        /// </remarks>
        [Tooltip("[피해 배율] 최종 데미지에 곱해지는 계수. 1=기본, 2=2배, 0.5=절반")]
        [SerializeField] private float multiplier = 1.0f;

        /// <summary>
        /// 데미지 타입(속성/분류)을 식별하는 UID입니다.
        /// </summary>
        /// <remarks>
        /// 저항, 약점, 면역 등 전투 계산 규칙에 사용됩니다.
        /// </remarks>
        [Tooltip("[피해 타입] 데미지 타입(속성/분류) UID. 저항/약점/면역 등 룰에 사용")]
        [SerializeField] private int damageTypeUid = 0;

        /// <summary>
        /// 히트 판정에 사용되는 영역의 형태입니다.
        /// </summary>
        [Tooltip("[영역 모양] 히트 판정에 사용할 영역 형태. Circle/Box/Cone/Capsule/Lin")]
        [SerializeField] private ConfigCommonSkill.SkillAreaShape areaShape = ConfigCommonSkill.SkillAreaShape.Circle;

        /// <summary>
        /// 캡슐 형태 사용 시, 세미원 끝 방향을 지정합니다.
        /// </summary>
        [Tooltip("[캡슐 방향] 캡슐의 세미원 끝 방향(Vertical/Horizontal).")]
        [SerializeField] private CapsuleDirection2D capsuleDirection = CapsuleDirection2D.Vertical;

        /// <summary>
        /// 영역의 반경 또는 두께를 정의합니다.
        /// </summary>
        /// <remarks>
        /// Circle = 반경,
        /// Capsule = 양 끝 반원의 반경(두께),
        /// Box/Cone/Line에서는 사용되지 않습니다.
        /// </remarks>
        [Tooltip("[반경/두께] Circle=반경. Capsule=양 끝 반원 반경(두께). (Box/Cone/Line에서는 미사용)")]
        [SerializeField] private float radius = 2f;

        /// <summary>
        /// 영역의 전방 길이 또는 사거리를 정의합니다.
        /// </summary>
        /// <remarks>
        /// Box/Line/Capsule = 전방 길이,
        /// Cone = 꼭지점에서 끝까지의 사거리,
        /// Circle에서는 사용되지 않습니다.
        /// </remarks>
        [Tooltip("[길이] Box/Line=전방 길이. Capsule=전방 길이. Cone=사거리(꼭지점→끝). (Circle에서는 미사용)")]
        [SerializeField] private float length = 3f;

        /// <summary>
        /// 영역의 가로 폭을 정의합니다.
        /// </summary>
        /// <remarks>
        /// Box/Line에서만 사용되며,
        /// Circle/Cone/Capsule에서는 사용되지 않습니다.
        /// </remarks>
        [Tooltip("[폭] Box/Line=가로 폭. (Circle/Cone/Capsule에서는 미사용)")]
        [SerializeField] private float width = 2f;

        /// <summary>
        /// 원뿔(Cone) 형태 사용 시 벌어짐 각도를 정의합니다.
        /// </summary>
        /// <remarks>
        /// 0~180도 범위에서 사용되며,
        /// Circle/Box/Line/Capsule에서는 사용되지 않습니다.
        /// </remarks>
        [Tooltip("[각도] Cone=원뿔(부채꼴) 벌어짐 각도(0~180). (Circle/Box/Line/Capsule에서는 미사용)")]
        [SerializeField] private float angle = 60f;

        /// <summary>
        /// 히트 영역의 중심을 로컬 좌표 기준으로 이동시키는 오프셋입니다.
        /// </summary>
        /// <remarks>
        /// 캐스터 또는 타겟 기준 중심점에 더해져 최종 판정 위치가 결정됩니다.
        /// </remarks>
        [Tooltip("[영역 오프셋] 영역 중심을 로컬 좌표로 이동. 캐스터/타겟 기준 중심점에 더해져 판정됨")]
        [SerializeField] private Vector2 offset = Vector2.zero;

        [Header("Position Reference")]
        [Tooltip("데미지 영역 중심을 기존 타겟팅, 스킬 시작 스냅샷, 또는 이름 있는 위치 앵커 중 어디에서 가져올지 지정합니다.")]
        [SerializeField] private SkillPositionReference damageCenterReference;

        /// <summary>
        /// 적중 시 대상(Target)에게 적용되는 추가 효과 목록입니다.
        /// </summary>
        [Header("OnHit Affect (Target)")]
        [SerializeField] private OnHitAffectEntry[] onHitAffects;

        /// <summary>
        /// 적중 시 대상(Target)에게 적용되는 Crowd Control 목록입니다.
        /// 실제 적용은 데미지 파이프라인을 통해 처리됩니다.
        /// </summary>
        [Header("OnHit Crowd Control (Target)")]
        [SerializeField] private OnHitCrowdControlEntry[] onHitCrowdControls;

        [Header("OnHit MP Gain")]
        [Tooltip("이 Damage 클립이 실제 타격에 성공했을 때 공격자에게 지급할 MP입니다. 0이면 지급하지 않습니다.")]
        [SerializeField] private int skillHitMpGain = 0;

        [Tooltip("같은 AttackId 안에서 이 Damage 클립의 MP 보상을 반복 지급할지 여부입니다.")]
        [SerializeField] private bool allowMultipleSkillHitMpGainPerAttack = false;

        /// <summary>
        /// 적중 후 재생할 사운드 목록입니다.
        /// </summary>
        [Header("OnHit Sound")]
        [SerializeField] private OnHitSoundEntry[] onHitSounds;

        /// <summary>
        /// 이 Damage 이벤트가 실제 데미지를 확정했을 때 다음 스킬 연계를 즉시 허용할지 여부입니다.
        /// 최종 런타임 동작은 <see cref="GGemCo2DSkill.GGemCoSkillSettings.enableSkillChainOnConfirmedDamage"/> 마스터 옵션이 함께 켜져 있어야 활성화됩니다.
        /// </summary>
        [Header("Target State Filter")]
        [Tooltip("[지상 전용] 대상이 지상에 있을 때만 데미지를 적용합니다. 공중 대상은 제외됩니다.")]
        [SerializeField] private bool isGroundOnly = false;

        [Tooltip("[공중 전용] 대상이 공중에 있을 때만 데미지를 적용합니다. 지상 대상은 제외됩니다.")]
        [SerializeField] private bool isAirOnly = false;

        [Header("Facing Policy")]
        [Tooltip("[바라보기 판정] 켜면 기존 바라보기 규칙을 따르고, 끄면 바라보기 상태를 무시하고 데미지를 적용합니다.")]
        [SerializeField] private DamageFacingPolicy facingDamagePolicy = DamageFacingPolicy.RespectFacing;

        [Header("Guard")]
        [Tooltip("[공격 방어 타입] GGemCoPlayerGuardSettings에서 가드 성공/브레이크/추가 CC를 결정할 때 사용하는 타입입니다.")]
        [SerializeField] private GuardAttackType guardAttackType = GuardAttackType.Normal;

        [Header("Camera Shake")]
        [Tooltip("[카메라 Shake 사용] 이 타격이 실제 데미지를 확정했을 때 카메라 Shake를 재생합니다.")]
        [SerializeField] private bool useCameraShakeOnHit = false;

        [Tooltip("[카메라 Shake Preset] 적중 시 사용할 카메라 Shake Preset 입니다.")]
        [SerializeField] private CameraShakePreset cameraShakePreset;

        [Tooltip("[카메라 방향 기준] 시전자/대상/고정 방향 중 어떤 기준으로 Shake 방향을 계산할지 지정합니다.")]
        [SerializeField] private CameraShakeDirectionSource cameraShakeDirectionSource = CameraShakeDirectionSource.Preset;

        [Tooltip("[카메라 고정 방향] 방향 기준이 FixedDirection 일 때 사용할 방향입니다.")]
        [SerializeField] private Vector2 cameraShakeFixedDirection = Vector2.right;

        [Tooltip("[카메라 좌우 방향만 사용] 켜면 계산된 방향에서 Y축을 제거하고 좌우 방향만 사용합니다.")]
        [SerializeField] private bool cameraShakeHorizontalOnly = true;

        [Header("Hit Stop")]
        [Tooltip("[경직 사용] 이 타격이 실제 데미지를 확정했을 때 캐스터에게 경직을 적용합니다.")]
        [SerializeField] private bool useHitStopSelf = false;

        [Tooltip("[자기 경직 기본값 사용] 켜면 캐스터의 ScriptableObject 기본 Self Hit Stop 시간을 사용합니다.")]
        [SerializeField] private bool useDefaultSelfHitStop = true;

        [Tooltip("[자기 경직 시간] 기본값을 사용하지 않을 때 캐스터에게 적용할 경직 시간(초)입니다.")]
        [SerializeField] private float selfHitStopSeconds = 0.03f;

        [Tooltip("[경직 사용] 이 타격이 실제 데미지를 확정했을 때 대상에게 경직을 적용합니다.")]
        [SerializeField] private bool useHitStopTarget = false;
        [Tooltip("[대상 경직 기본값 사용] 켜면 대상의 ScriptableObject 기본 Receive Hit Stop 시간을 사용합니다.")]
        [SerializeField] private bool useDefaultTargetHitStop = true;

        [Tooltip("[대상 경직 시간] 기본값을 사용하지 않을 때 대상에게 적용할 경직 시간(초)입니다.")]
        [SerializeField] private float targetHitStopSeconds = 0.05f;

        [Header("Chain Cancel")]
        [Tooltip("[체인 캔슬] 이 Damage 이벤트가 실제 데미지를 확정했을 때 다음 스킬 연계를 즉시 허용할지 여부. GGemCoSkillSettings.enableSkillChainOnConfirmedDamage 가 함께 켜져 있어야 동작")]
        [SerializeField] private bool allowSkillChainOnConfirmedDamage = false;

        /// <summary>
        /// 이 클립의 이벤트 타입을 반환합니다.
        /// </summary>
        /// <returns>데미지 이벤트 타입입니다.</returns>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Damage;

        /// <summary>
        /// 최종 데미지에 적용되는 배율을 반환합니다.
        /// </summary>
        public float Multiplier => multiplier;

        /// <summary>
        /// 데미지 타입 UID를 반환합니다.
        /// </summary>
        public int DamageTypeUid => damageTypeUid;

        /// <summary>
        /// 히트 판정 영역 형태를 반환합니다.
        /// </summary>
        public ConfigCommonSkill.SkillAreaShape AreaShape => areaShape;

        /// <summary>
        /// 캡슐 영역 사용 시 방향을 반환합니다.
        /// </summary>
        public CapsuleDirection2D CapsuleDirection => capsuleDirection;

        /// <summary>
        /// 영역 반경 또는 두께 값을 반환합니다.
        /// </summary>
        public float Radius => radius;

        /// <summary>
        /// 영역 길이 또는 사거리 값을 반환합니다.
        /// </summary>
        public float Length => length;

        /// <summary>
        /// 영역 가로 폭 값을 반환합니다.
        /// </summary>
        public float Width => width;

        /// <summary>
        /// Cone 영역 각도를 반환합니다.
        /// </summary>
        public float Angle => angle;

        /// <summary>
        /// 영역 중심 오프셋 값을 반환합니다.
        /// </summary>
        public Vector2 Offset => offset;

        /// <summary>
        /// 데미지 영역 중심을 계산할 때 사용할 위치 참조 설정을 반환합니다.
        /// </summary>
        public SkillPositionReference DamageCenterReference => damageCenterReference;

        /// <summary>
        /// 적중 시 적용될 추가 효과 목록을 반환합니다.
        /// </summary>
        public OnHitAffectEntry[] OnHitAffects => onHitAffects;

        /// <summary>
        /// 적중 시 적용될 Crowd Control 목록을 반환합니다.
        /// </summary>
        public OnHitCrowdControlEntry[] OnHitCrowdControls => onHitCrowdControls;

        /// <summary>
        /// 실제 타격 성공 시 공격자에게 지급할 MP를 반환합니다.
        /// </summary>
        public int SkillHitMpGain => Mathf.Max(0, skillHitMpGain);

        /// <summary>
        /// 같은 AttackId에서 스킬 타격 MP 보상을 반복 지급할지 여부를 반환합니다.
        /// </summary>
        public bool AllowMultipleSkillHitMpGainPerAttack => allowMultipleSkillHitMpGainPerAttack;

        /// <summary>
        /// 적중 후 재생할 사운드 목록을 반환합니다.
        /// </summary>
        public OnHitSoundEntry[] OnHitSounds => onHitSounds;


        /// <summary>
        /// 대상이 지상에 있을 때만 데미지를 적용할지 여부를 반환합니다.
        /// </summary>
        public bool IsGroundOnly => isGroundOnly;

        /// <summary>
        /// 대상이 공중에 있을 때만 데미지를 적용할지 여부를 반환합니다.
        /// </summary>
        public bool IsAirOnly => isAirOnly;

        /// <summary>
        /// 데미지 적용 시 바라보기 판정을 어떻게 처리할지 반환합니다.
        /// </summary>
        public DamageFacingPolicy FacingDamagePolicy => facingDamagePolicy;

        /// <summary>
        /// 이 공격이 가드 설정에서 어떤 공격 방어 타입으로 처리될지 반환합니다.
        /// </summary>
        public GuardAttackType GuardAttackType => guardAttackType;

        /// <summary>
        /// 이 타격에서 캐스터에게 경직을 사용할지 여부를 반환합니다.
        /// </summary>
        public bool UseHitStopSelf => useHitStopSelf;

        /// <summary>
        /// 캐스터의 기본 Self Hit Stop 시간을 사용할지 여부를 반환합니다.
        /// </summary>
        public bool UseDefaultSelfHitStop => useDefaultSelfHitStop;

        /// <summary>
        /// 캐스터에게 적용할 경직 시간을 반환합니다.
        /// </summary>
        public float SelfHitStopSeconds => selfHitStopSeconds;
        
        /// <summary>
        /// 이 타격에서 대상에게 경직을 사용할지 여부를 반환합니다.
        /// </summary>
        public bool UseHitStopTarget => useHitStopTarget;

        /// <summary>
        /// 대상의 기본 Receive Hit Stop 시간을 사용할지 여부를 반환합니다.
        /// </summary>
        public bool UseDefaultTargetHitStop => useDefaultTargetHitStop;

        /// <summary>
        /// 대상에게 적용할 경직 시간을 반환합니다.
        /// </summary>
        public float TargetHitStopSeconds => targetHitStopSeconds;

        /// <summary>
        /// 적중 시 카메라 Shake를 사용할지 여부를 반환합니다.
        /// </summary>
        public bool UseCameraShakeOnHit => useCameraShakeOnHit;

        /// <summary>
        /// 적중 시 사용할 카메라 Shake Preset 을 반환합니다.
        /// </summary>
        public CameraShakePreset CameraShakePreset => cameraShakePreset;

        /// <summary>
        /// 카메라 Shake 방향 계산 기준을 반환합니다.
        /// </summary>
        public CameraShakeDirectionSource CameraShakeDirectionSource => cameraShakeDirectionSource;

        /// <summary>
        /// 고정 방향 카메라 Shake에서 사용할 방향을 반환합니다.
        /// </summary>
        public Vector2 CameraShakeFixedDirection => cameraShakeFixedDirection;

        /// <summary>
        /// 카메라 Shake 방향 계산 시 좌우 방향만 사용할지 여부를 반환합니다.
        /// </summary>
        public bool CameraShakeHorizontalOnly => cameraShakeHorizontalOnly;

        /// <summary>
        /// 실제 데미지 확정 시 다음 스킬 연계를 즉시 허용할지 여부를 반환합니다.
        /// </summary>
        public bool AllowSkillChainOnConfirmedDamage => allowSkillChainOnConfirmedDamage;
    }
}
