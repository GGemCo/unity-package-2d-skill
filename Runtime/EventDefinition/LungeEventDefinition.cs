using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 돌진 이동 거리 해석 방식입니다.
    /// </summary>
    public enum SkillLungeResolveMode
    {
        /// <summary>항상 고정 거리만큼 이동합니다.</summary>
        FixedDistance = 0,

        /// <summary>고정 타겟이 있으면 타겟 앞까지 이동합니다. 타겟이 없으면 이동하지 않습니다.</summary>
        ToLockedTarget = 1,

        /// <summary>고정 타겟이 해석 가능 거리 안에 있을 때만 타겟 앞까지 이동합니다.</summary>
        ToLockedTargetIfWithinResolveRange = 2,

        /// <summary>고정 타겟이 해석 가능 거리 안에 있으면 타겟 앞까지, 아니면 고정 거리만큼 이동합니다.</summary>
        ToLockedTargetElseFixedDistance = 3,
    }

    /// <summary>
    /// 타겟 추적 시 어떤 기준점을 도착 기준으로 사용할지 정의합니다.
    /// </summary>
    public enum SkillLungeTargetAnchorMode
    {
        TargetTransform = 0,
        HitAreaCenter = 1,
        Feet = 2,
    }

    /// <summary>
    /// 돌진 종료 시 Y 좌표 보정 방식입니다.
    /// </summary>
    public enum SkillLungeEndYMode
    {
        None = 0,
        KeepCasterY = 1,
        MatchTargetAnchorY = 2,
        GroundAtEndX = 3,
    }

    /// <summary>
    /// 전진(러시/대시) 이벤트 정의.
    /// - 스킬 타임라인(이벤트 구간)과 이동 구간을 정밀하게 동기화하기 위한 Payload 입니다.
    /// - Speed 기반이 아니라 "거리(Distance)" 기반으로 설계하여, 클립 시간에 따라 일관된 이동감을 제공합니다.
    /// - Arc 옵션을 사용하면 점프형 회피(뒤로 점프)뿐 아니라 공중 추적(Air Chase) 형태도 구성할 수 있습니다.
    /// </summary>
    public sealed class LungeEventDefinition : ScriptableObject
    {
        [Header("Motion")]
        [Tooltip("이 이벤트(클립) 구간 동안 이동할 총 거리(월드 단위)")]
        public float distance = 2.5f;

        [Tooltip("지속시간 오버라이드(<=0 이면 이벤트 구간(Start~End)을 사용)")]
        public float durationOverrideSeconds = -1f;

        [Tooltip("시간→진행률 Easing (Core의 Easing 클래스를 사용)")]
        public Easing.EaseType easing = Easing.EaseType.Linear;

        [Header("Resolve")]
        [Tooltip("고정 거리 / 타겟 추적 중 어떤 방식으로 실제 이동 거리를 계산할지 결정합니다.")]
        public SkillLungeResolveMode resolveMode = SkillLungeResolveMode.FixedDistance;

        [Tooltip("타겟 추적을 허용할 최대 거리(<=0 이면 CastRange, 그것도 없으면 Distance를 사용).")]
        public float targetResolveRange = -1f;

        [Tooltip("타겟 중심에 완전히 겹치지 않도록 남길 거리입니다.")]
        public float stopOffset = 0.2f;

        [Tooltip("true면 X축 기준으로만 타겟 접근 거리를 계산합니다.")]
        public bool horizontalOnly = true;

        [Header("Target Anchor")]
        [Tooltip("타겟 추적 시 어떤 기준점까지 접근할지 결정합니다.")]
        public SkillLungeTargetAnchorMode targetAnchorMode = SkillLungeTargetAnchorMode.TargetTransform;

        [Tooltip("타겟 기준점에 추가로 더할 로컬 오프셋입니다.")]
        public Vector2 targetAnchorOffset = Vector2.zero;

        [Header("Direction")]
        [Tooltip("true면 스킬 발동 시점의 전방(캐스터 스냅샷)을 사용합니다.")]
        public bool useSnapshotForward = true;

        [Tooltip("true면 최종 이동 방향을 반전합니다(예: 뒤로 회피).")]
        public bool invertForward = false;

        [Header("Arc")]
        [Tooltip("true면 Arc(수직 오프셋) 모션을 사용합니다.")]
        public bool useArcMotion = false;

        [Tooltip("Arc 높이(월드 단위). useArcMotion이 true일 때만 유효합니다.")]
        public float arcHeight = 0f;

        [Tooltip("Arc 구현 모드입니다. Air Chase 계열은 DistancePhased 권장입니다.")]
        public MotionArcMode arcMode = MotionArcMode.LegacyTimeSine;

        [Tooltip("DistancePhased Arc에서 상승 구간 easing입니다.")]
        public Easing.EaseType arcRiseEase = Easing.EaseType.EaseOutQuad;

        [Tooltip("DistancePhased Arc에서 하강 구간 easing입니다.")]
        public Easing.EaseType arcFallEase = Easing.EaseType.EaseInQuad;

        [Range(0f, 1f)]
        [Tooltip("정점 유지 구간 폭(정규화 0..1). 0이면 즉시 하강합니다.")]
        public float arcApexHoldNormalized = 0f;

        [Header("End Position")]
        [Tooltip("이동 종료 시 Y 좌표를 어떻게 보정할지 결정합니다.")]
        public SkillLungeEndYMode endYMode = SkillLungeEndYMode.None;

        [Tooltip("EndYMode 적용 후 추가 Y 오프셋입니다.")]
        public float endYOffset = 0f;

        [Tooltip("GroundAtEndX일 때 레이캐스트 시작 높이입니다.")]
        public float groundProbeHeight = 2f;

        [Tooltip("GroundAtEndX일 때 아래 방향 탐색 거리입니다.")]
        public float groundProbeDistance = 8f;

        [Header("Rigidbody2D")]
        [Tooltip("Kinematic이면 MovePosition 기반 이동을 사용합니다.")]
        public bool useMovePosition = true;

        [Tooltip("종료 시 정지(velocity 기반 구현에서 유효)")]
        public bool stopAtEnd = true;

        [Header("Policy")]
        [Tooltip("true면 동일 채널(Skill)에서 진행 중인 모션을 덮어쓸 수 있습니다.")]
        public bool allowReplace = false;
    }
}
