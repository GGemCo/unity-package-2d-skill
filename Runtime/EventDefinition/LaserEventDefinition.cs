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
        [Tooltip("레이저 시작점 오버라이드 해석 방식입니다.")]
        public LaserConstants.StartPositionOverrideMode startPositionOverrideMode = LaserConstants.StartPositionOverrideMode.UseLaserTable;

        [Tooltip("레이저 시작점 오버라이드 값입니다. 모드에 따라 월드 좌표 또는 오프셋으로 해석됩니다.")]
        public Vector2 startPositionOverride = Vector2.zero;

        [Tooltip("레이저 시작점을 발사 후에도 계속 갱신할지, 발사 시점에 고정할지 정의합니다.")]
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

        [Header("OnHit Element Gauge")]
        public OnHitElementGaugeEntry[] onHitElementGauges;

        [Header("Targeting Overrides")]
        [Tooltip("스킬 기본 TargetingMode 대신, 이벤트 별 TargetingMode를 강제할 수 있습니다.")]
        public TargetingOverride targetingOverride;
    }
}
