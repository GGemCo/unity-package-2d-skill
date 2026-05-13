using System;
using Config;
using GGemCo2DCore;
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
        [Header("Laser (Core Laser Table)")]
        [Tooltip("Core laser 테이블 UID입니다.")]
        [SerializeField] private int laserUid = 0;

        [Header("Combat")]
        [Tooltip("레이저 적중 시 적용할 피해 유형입니다.")]
        [SerializeField] private ConfigCommon.DamageType damageType = ConfigCommon.DamageType.None;

        [Tooltip("레이저 적중 시 적용할 기본 피해량입니다.")]
        [SerializeField] private long damage = 0;

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

        [Header("Range / Aim")]
        [Tooltip("최대 사거리 오버라이드입니다. 0 이하이면 타겟/좌표 기반 거리 또는 기본값을 사용합니다.")]
        [SerializeField] private float maxDistance = 0f;

        [Tooltip("레이저 유지 시간 동안 타겟/방향을 계속 갱신할지 여부입니다.")]
        [SerializeField] private bool updateAimContinuously = false;

        [Header("Angle Overrides")]
        [Tooltip("레이캐스트 방향 모드 오버라이드 사용 여부입니다. 켜지면 laser 테이블의 RaycastDirectionMode 대신 이 값을 사용합니다.")]
        [SerializeField] private bool useRaycastDirectionModeOverride = false;

        [Tooltip("레이캐스트 방향 모드 오버라이드 값입니다.")]
        [SerializeField] private LaserConstants.RaycastDirectionMode raycastDirectionModeOverride = LaserConstants.RaycastDirectionMode.TowardTarget;

        [Tooltip("레이캐스트 각도 오버라이드 사용 여부입니다. 켜지면 laser 테이블의 RaycastAngleDeg 대신 이 값을 사용합니다.")]
        [SerializeField] private bool useRaycastAngleOverride = false;

        [Tooltip("레이캐스트 각도 오버라이드 값(도)입니다. RaycastDirectionMode가 ByAngle일 때 사용됩니다.")]
        [SerializeField] private float raycastAngleOverrideDeg = 0f;

        [Tooltip("VFX 각도 동기화 모드 오버라이드 사용 여부입니다. 켜지면 laser 테이블의 VfxAngleSyncMode 대신 이 값을 사용합니다.")]
        [SerializeField] private bool useVfxAngleSyncModeOverride = false;

        [Tooltip("VFX 각도 동기화 모드 오버라이드 값입니다.")]
        [SerializeField] private LaserConstants.VfxAngleSyncMode vfxAngleSyncModeOverride = LaserConstants.VfxAngleSyncMode.FollowRaycast;

        [Header("Start Position Override")]
        [Tooltip("레이저 시작점 오버라이드 해석 방식입니다.")]
        [SerializeField] private LaserConstants.StartPositionOverrideMode startPositionOverrideMode = LaserConstants.StartPositionOverrideMode.UseLaserTable;

        [Tooltip("레이저 시작점 오버라이드 값입니다. 모드에 따라 월드 좌표 또는 오프셋으로 해석됩니다.")]
        [SerializeField] private Vector2 startPositionOverride = Vector2.zero;

        [Tooltip("레이저 시작점을 발사 후에도 계속 갱신할지, 발사 시점에 고정할지 정의합니다.")]
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

        [Header("Targeting Overrides")]
        [Tooltip("레이저의 타게팅 규칙을 보정하기 위한 오버라이드 설정입니다.")]
        [SerializeField] private GGemCo2DSkill.TargetingOverride targetingOverride;

        [Header("Chain Cancel")]
        [Tooltip("이 Laser 이벤트가 실제 데미지를 확정했을 때 다음 스킬 연계를 즉시 허용할지 여부입니다. GGemCoSkillSettings.enableSkillChainOnConfirmedDamage 가 함께 켜져 있어야 동작합니다.")]
        [SerializeField] private bool allowSkillChainOnConfirmedDamage = false;

        [Header("OnHit Element Gauge")]
        [SerializeField] private GGemCo2DSkill.OnHitElementGaugeEntry[] onHitElementGauges;

        /// <summary>
        /// 이 클립이 표현하는 스킬 이벤트 유형입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Laser;

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
        /// 지속 시간 동안 에임을 계속 갱신할지 여부입니다.
        /// </summary>
        public bool UpdateAimContinuously => updateAimContinuously;

        /// <summary>
        /// 레이캐스트 방향 모드 오버라이드 사용 여부입니다.
        /// </summary>
        public bool UseRaycastDirectionModeOverride => useRaycastDirectionModeOverride;

        /// <summary>
        /// 레이캐스트 방향 모드 오버라이드 값입니다.
        /// </summary>
        public LaserConstants.RaycastDirectionMode RaycastDirectionModeOverride => raycastDirectionModeOverride;

        /// <summary>
        /// 레이캐스트 각도 오버라이드 사용 여부입니다.
        /// </summary>
        public bool UseRaycastAngleOverride => useRaycastAngleOverride;

        /// <summary>
        /// 레이캐스트 각도 오버라이드 값입니다.
        /// </summary>
        public float RaycastAngleOverrideDeg => raycastAngleOverrideDeg;

        /// <summary>
        /// VFX 각도 동기화 모드 오버라이드 사용 여부입니다.
        /// </summary>
        public bool UseVfxAngleSyncModeOverride => useVfxAngleSyncModeOverride;

        /// <summary>
        /// VFX 각도 동기화 모드 오버라이드 값입니다.
        /// </summary>
        public LaserConstants.VfxAngleSyncMode VfxAngleSyncModeOverride => vfxAngleSyncModeOverride;

        /// <summary>
        /// 레이저 시작점 오버라이드 해석 방식입니다.
        /// </summary>
        public LaserConstants.StartPositionOverrideMode StartPositionOverrideMode => startPositionOverrideMode;

        /// <summary>
        /// 레이저 시작점 오버라이드 값입니다.
        /// </summary>
        public Vector2 StartPositionOverride => startPositionOverride;

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
        /// 타게팅 오버라이드 설정입니다.
        /// </summary>
        public GGemCo2DSkill.TargetingOverride TargetingOverride => targetingOverride;

        /// <summary>
        /// 실제 데미지 확정 시 스킬 연계를 즉시 허용할지 여부입니다.
        /// </summary>
        public bool AllowSkillChainOnConfirmedDamage => allowSkillChainOnConfirmedDamage;

        /// <summary>
        /// 적중 시 추가할 속성 게이지 목록입니다.
        /// </summary>
        public GGemCo2DSkill.OnHitElementGaugeEntry[] OnHitElementGauges => onHitElementGauges;
    }
}
