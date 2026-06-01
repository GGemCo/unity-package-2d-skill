using System;
using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 공중에서 지면으로 내려치는 강하 이동 이벤트를 정의하는 Authoring용 타임라인 클립입니다.
    /// </summary>
    [Serializable]
    public sealed class SkillGroundSlamClip : SkillEventClipBase
    {
        [Header("Motion")]
        [Tooltip("0보다 크면 Definition의 fallback 하강 지속시간 대신 이 값을 사용합니다. fallDurationSeconds가 0 이하일 때만 사용됩니다.")]
        [SerializeField] private float durationOverrideSeconds = 0f;
        [Tooltip("공중에서 잠시 머무르는 시간입니다. 0 이하이면 즉시 하강을 시작합니다.")]
        [SerializeField] private float airHoldDurationSeconds = 0f;
        [Tooltip("공중 대기 시간 동안 시작 위치를 유지할지 여부입니다. true면 대기 시작 순간의 위치에 고정됩니다.")]
        public bool holdPositionDuringAirHold = true;
        [Tooltip("실제 내려가는 시간입니다. 0 이하이면 durationOverrideSeconds, 그것도 0 이하이면 이벤트 구간 길이를 사용합니다.")]
        [SerializeField] private float fallDurationSeconds = 0f;
        [Tooltip("하강 이동의 진행 곡선을 결정합니다.")]
        [SerializeField] private Easing.EaseType easing = GGemCo2DCore.Easing.EaseType.Linear;

        [Header("Landing")]
        [Tooltip("착지 지점을 어떤 기준으로 계산할지 결정합니다.")]
        [SerializeField] private GroundSlamLandingMode landingMode = GroundSlamLandingMode.CurrentGround;
        [Tooltip("하강 중 X축 이동 방식을 결정합니다.")]
        [SerializeField] private GroundSlamHorizontalPolicy horizontalPolicy = GroundSlamHorizontalPolicy.KeepCurrentX;
        [Tooltip("HorizontalPolicy가 전방 이동 계열일 때 사용할 전진 거리입니다.")]
        [SerializeField] private float forwardDistance = 0f;
        [Tooltip("지면 탐색 Ray 시작점을 현재 위치에서 위로 얼마나 올릴지 설정합니다.")]
        [SerializeField] private float groundProbeStartHeight = 0.5f;
        [Tooltip("지면 탐색 Ray의 최대 길이입니다.")]
        [SerializeField] private float groundProbeDistance = 12f;
        [Tooltip("LandingMode가 FixedDistanceDown일 때 아래로 내려갈 거리입니다.")]
        [SerializeField] private float fixedDropDistance = 6f;
        [Tooltip("착지 판정 후 지면에 스냅할 때 허용할 최대 보정 거리입니다.")]
        [SerializeField] private float groundSnapDistance = 0.15f;
        [Tooltip("착지 지면을 탐색할 때 사용할 레이어 마스크입니다.")]
        [SerializeField] private LayerMask groundLayerMask = Physics2D.DefaultRaycastLayers;

        [Header("Animation")]
        [Tooltip("내려치기 시작 시 1회 재생할 애니메이션 이름입니다. 비워두면 즉시 Loop 단계로 진입합니다.")]
        [SerializeField] private string startAnimationName = string.Empty;
        [Tooltip("내려치기 시작 애니메이션 재생 속도입니다. 1이면 기본 속도입니다.")]
        [SerializeField, Min(0.001f)] private float startAnimationTimeScale = 1f;
        [Tooltip("내려오는 동안 반복 재생할 애니메이션 이름입니다.")]
        [SerializeField] private string fallLoopAnimationName = string.Empty;
        [Tooltip("하강 루프 애니메이션 재생 속도입니다. 1이면 기본 속도입니다.")]
        [SerializeField, Min(0.001f)] private float fallLoopAnimationTimeScale = 1f;
        [Tooltip("지면에 닿았을 때 1회 재생할 애니메이션 이름입니다.")]
        [SerializeField] private string landEndAnimationName = string.Empty;
        [Tooltip("착지 종료 애니메이션 재생 속도입니다. 1이면 기본 속도입니다.")]
        [SerializeField, Min(0.001f)] private float landEndAnimationTimeScale = 1f;
        [Tooltip("Ground Slam 진행률이 이 값 이상이 되면 Start 애니메이션에서 Fall Loop 애니메이션으로 전환합니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float startToLoopNormalizedTime = 0.15f;

        [Header("Direction")]
        [Tooltip("클립 시작 시점의 바라보는 방향을 고정해서 사용할지 여부입니다.")]
        [SerializeField] private bool useSnapshotForward = true;

        [Header("Rigidbody2D")]
        [Tooltip("강하 종료 시 Rigidbody2D의 속도를 정리해서 즉시 멈출지 여부입니다.")]
        [SerializeField] private bool stopAtEnd = true;
        [Tooltip("이동 적용 시 Rigidbody2D.MovePosition 기반으로 처리할지 여부입니다.")]
        [SerializeField] private bool useMovePosition = true;

        [Header("Policy")]
        [Tooltip("같은 채널에서 이미 재생 중인 모션을 이 Ground Slam으로 교체할 수 있는지 여부입니다.")]
        [SerializeField] private bool allowReplace = false;

        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.GroundSlam;
        public float DurationOverrideSeconds => durationOverrideSeconds;
        public float AirHoldDurationSeconds => airHoldDurationSeconds;
        public bool HoldPositionDuringAirHold => holdPositionDuringAirHold;
        public float FallDurationSeconds => fallDurationSeconds;
        public Easing.EaseType Easing => easing;
        public GroundSlamLandingMode LandingMode => landingMode;
        public GroundSlamHorizontalPolicy HorizontalPolicy => horizontalPolicy;
        public float ForwardDistance => forwardDistance;
        public float GroundProbeStartHeight => groundProbeStartHeight;
        public float GroundProbeDistance => groundProbeDistance;
        public float FixedDropDistance => fixedDropDistance;
        public float GroundSnapDistance => groundSnapDistance;
        public LayerMask GroundLayerMask => groundLayerMask;
        public string StartAnimationName => startAnimationName;
        public float StartAnimationTimeScale => startAnimationTimeScale;
        public string FallLoopAnimationName => fallLoopAnimationName;
        public float FallLoopAnimationTimeScale => fallLoopAnimationTimeScale;
        public string LandEndAnimationName => landEndAnimationName;
        public float LandEndAnimationTimeScale => landEndAnimationTimeScale;
        public float StartToLoopNormalizedTime => startToLoopNormalizedTime;
        public bool UseSnapshotForward => useSnapshotForward;
        public bool StopAtEnd => stopAtEnd;
        public bool UseMovePosition => useMovePosition;
        public bool AllowReplace => allowReplace;
    }
}
