using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 공중에서 지면으로 내려치는 이동의 착지 지점 해석 방식입니다.
    /// </summary>
    public enum GroundSlamLandingMode
    {
        /// <summary>현재 캐릭터 아래의 지면으로 내려칩니다.</summary>
        CurrentGround = 0,

        /// <summary>고정 타겟의 X 위치 아래 지면으로 내려칩니다.</summary>
        LockedTargetGround = 1,

        /// <summary>스킬 타겟 컨텍스트의 groundPoint 기준 지면으로 내려칩니다.</summary>
        GroundPoint = 2,

        /// <summary>현재 위치에서 지정 거리만큼 아래로 내려칩니다.</summary>
        FixedDistanceDown = 3,
    }

    /// <summary>
    /// Ground Slam의 수평 이동 정책입니다.
    /// </summary>
    public enum GroundSlamHorizontalPolicy
    {
        /// <summary>현재 X를 유지하고 수직에 가깝게 내려칩니다.</summary>
        KeepCurrentX = 0,

        /// <summary>고정 타겟 또는 groundPoint의 X로 이동하면서 내려칩니다.</summary>
        MoveToTargetX = 1,

        /// <summary>전방으로 일정 거리 이동하면서 내려칩니다.</summary>
        MoveByForward = 2,
    }

    /// <summary>
    /// 공중에서 지면으로 내려치는 전용 이벤트 정의입니다.
    /// </summary>
    public sealed class GroundSlamEventDefinition : ScriptableObject
    {
        [Header("Motion")]
        [Tooltip("전체 하강 지속시간 오버라이드(하강 시간 <= 0일 때 fallback 용도로 사용, <=0 이면 이벤트 구간 Start~End 사용)")]
        public float durationOverrideSeconds = -1f;

        [Tooltip("공중에서 잠시 머무르는 시간(<=0 이면 체공 없이 즉시 하강 시작)")]
        public float airHoldDurationSeconds = 0f;

        [Tooltip("공중 대기 시간 동안 시작 위치를 유지할지 여부입니다. true면 대기 시작 순간의 위치에 고정됩니다.")]
        public bool holdPositionDuringAirHold = true;

        [Tooltip("실제 내려가는 시간(<=0 이면 durationOverrideSeconds, 그것도 <=0 이면 이벤트 구간 Start~End 사용)")]
        public float fallDurationSeconds = -1f;

        [Tooltip("시간 진행에 적용할 easing")]
        public Easing.EaseType easing = Easing.EaseType.Linear;

        [Header("Landing")]
        [Tooltip("착지 지점 해석 방식")]
        public GroundSlamLandingMode landingMode = GroundSlamLandingMode.CurrentGround;

        [Tooltip("수평 이동 정책")]
        public GroundSlamHorizontalPolicy horizontalPolicy = GroundSlamHorizontalPolicy.KeepCurrentX;

        [Tooltip("전방 이동 정책에서 사용할 수평 이동 거리")]
        public float forwardDistance = 0f;

        [Tooltip("지면 탐색 시작 높이 오프셋")]
        public float groundProbeStartHeight = 0.5f;

        [Tooltip("지면 탐색 최대 거리")]
        public float groundProbeDistance = 12f;

        [Tooltip("지면 탐색 실패 시 아래로 내려칠 기본 거리")]
        public float fixedDropDistance = 6f;

        [Tooltip("착지 직전 목표 Y에 스냅하는 허용 거리")]
        public float groundSnapDistance = 0.15f;

        [Tooltip("Ground 레이어 마스크")]
        public LayerMask groundLayerMask = Physics2D.DefaultRaycastLayers;

        [Header("Animation")]
        [Tooltip("내려치기 시작 시 1회 재생할 애니메이션 이름입니다. 비어 있으면 즉시 Loop 단계로 진입합니다.")]
        public string startAnimationName;

        [Tooltip("내려치기 시작 애니메이션 재생 속도입니다. 1이면 기본 속도입니다.")]
        [Min(0.001f)]
        public float startAnimationTimeScale = 1f;

        [Tooltip("내려오는 동안 반복 재생할 애니메이션 이름입니다.")]
        public string fallLoopAnimationName;

        [Tooltip("하강 루프 애니메이션 재생 속도입니다. 1이면 기본 속도입니다.")]
        [Min(0.001f)]
        public float fallLoopAnimationTimeScale = 1f;

        [Tooltip("지면에 닿았을 때 1회 재생할 애니메이션 이름입니다.")]
        public string landEndAnimationName;

        [Tooltip("착지 종료 애니메이션 재생 속도입니다. 1이면 기본 속도입니다.")]
        [Min(0.001f)]
        public float landEndAnimationTimeScale = 1f;

        [Tooltip("Ground Slam 진행률이 이 값 이상이 되면 Start 애니메이션에서 Fall Loop 애니메이션으로 전환합니다.")]
        [Range(0f, 1f)]
        public float startToLoopNormalizedTime = 0.15f;

        [Header("Direction")]
        [Tooltip("true면 이벤트 스냅샷 시점의 forward를 사용합니다.")]
        public bool useSnapshotForward = true;

        [Header("Rigidbody2D")]
        [Tooltip("Kinematic이면 MovePosition 기반 이동을 사용합니다.")]
        public bool useMovePosition = true;

        [Tooltip("종료 시 정지(velocity 기반 구현에서 유효)")]
        public bool stopAtEnd = true;

        [Header("Policy")]
        [Tooltip("true면 동일 채널(Skill)의 진행 중 모션을 덮어쓸 수 있습니다.")]
        public bool allowReplace = false;
    }
}
