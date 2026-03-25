using System;
using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Arc 전용 점프 이동 이벤트를 정의하는 Authoring용 타임라인 클립입니다.
    /// Bake 과정에서 <see cref="GGemCo2DSkill.ArcLungeEventDefinition"/> Payload로 변환되어
    /// 실제 스킬 실행 시스템에서 사용됩니다.
    /// </summary>
    [Serializable]
    public sealed class SkillArcLungeClip : SkillEventClipBase
    {
        [Header("Motion")]
        [Tooltip("총 수평 이동 거리(월드 유닛 기준).")]
        [SerializeField] private float distance = 2.5f;

        [Tooltip("0보다 크면 rise/apexHold/fall 합계 대신 이 값을 총 이동 시간으로 사용합니다.")]
        [SerializeField] private float durationOverrideSeconds = 0f;

        [Tooltip("상승 구간 시간(초)입니다.")]
        [SerializeField] private float riseDurationSeconds = 0.15f;

        [Tooltip("정점에서 머무르는 시간(초)입니다.")]
        [SerializeField] private float apexHoldDurationSeconds = 0f;

        [Tooltip("하강 구간 시간(초)입니다.")]
        [SerializeField] private float fallDurationSeconds = 0.15f;

        [Tooltip("수평 이동의 진행 곡선을 결정합니다.")]
        [SerializeField] private Easing.EaseType easing = GGemCo2DCore.Easing.EaseType.Linear;

        [Tooltip("아크 최고 높이(월드 유닛)입니다.")]
        [SerializeField] private float arcHeight = 1.5f;

        [Tooltip("Arc 구현 모드입니다. Arc 전용 이벤트는 DistancePhased를 권장합니다.")]
        [SerializeField] private MotionArcMode arcMode = MotionArcMode.DistancePhased;

        [Tooltip("상승 구간 수직 easing 입니다.")]
        [SerializeField] private Easing.EaseType arcRiseEase = GGemCo2DCore.Easing.EaseType.Linear;

        [Tooltip("하강 구간 수직 easing 입니다.")]
        [SerializeField] private Easing.EaseType arcFallEase = GGemCo2DCore.Easing.EaseType.Linear;

        [Header("Resolve")]
        [Tooltip("고정 거리 / 타겟 추적 중 어떤 방식으로 실제 이동 거리를 계산할지 결정합니다.")]
        [SerializeField] private SkillLungeResolveMode resolveMode = SkillLungeResolveMode.FixedDistance;

        [Tooltip("타겟 추적 허용 최대 거리입니다. 0 이하이면 스킬 CastRange를 사용합니다.")]
        [SerializeField] private float targetResolveRange = -1f;

        [Tooltip("타겟 종착 관계를 해석하는 방식입니다.")]
        [SerializeField] private SkillLungeTargetRelationMode targetRelationMode = SkillLungeTargetRelationMode.StopBeforeTarget;

        [Tooltip("타겟과 완전히 겹치지 않도록 남길 거리입니다.")]
        [SerializeField] private float stopOffset = 0.2f;

        [Tooltip("PassThroughTarget일 때 타겟 중심을 지난 뒤 추가로 이동할 거리입니다.")]
        [SerializeField] private float passThroughExtraDistance = 0.5f;

        [Tooltip("체크 시 X축 기준으로만 타겟 접근 거리를 계산합니다.")]
        [SerializeField] private bool horizontalOnly = true;

        [Tooltip("이동 중 타겟과의 충돌 처리 정책입니다.")]
        [SerializeField] private SkillLungeCollisionPolicy collisionPolicy = SkillLungeCollisionPolicy.Default;

        [Header("Animation")]
        [Tooltip("상승 시작 시 1회 재생할 애니메이션 이름입니다.")]
        [SerializeField] private string riseAnimationName = string.Empty;

        [Tooltip("정점 대기 구간에서 재생할 애니메이션 이름입니다.")]
        [SerializeField] private string apexAnimationName = string.Empty;

        [Tooltip("하강 구간에서 재생할 애니메이션 이름입니다.")]
        [SerializeField] private string fallAnimationName = string.Empty;

        [Tooltip("착지 시 1회 재생할 애니메이션 이름입니다.")]
        [SerializeField] private string landEndAnimationName = string.Empty;

        [Header("Direction")]
        [Tooltip("체크 시 현재 Forward 방향의 반대로 이동합니다.")]
        [SerializeField] private bool invertForward = false;

        [Tooltip("모션 시작 시 Forward 방향을 고정합니다. 해제 시 이동 중 Forward 변경을 반영합니다.")]
        [SerializeField] private bool useSnapshotForward = true;

        [Header("Rigidbody2D")]
        [Tooltip("모션 종료 시 Rigidbody2D의 속도를 0으로 초기화합니다.")]
        [SerializeField] private bool stopAtEnd = true;

        [Tooltip("Kinematic Rigidbody2D에서 MovePosition을 사용하여 이동합니다. 권장 옵션입니다.")]
        [SerializeField] private bool useMovePosition = true;

        [Header("Policy")]
        [Tooltip("같은 채널의 기존 모션이 실행 중이어도 덮어쓰기를 허용합니다.")]
        [SerializeField] private bool allowReplace = false;

        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Lunge;

        public float Distance => distance;
        public float DurationOverrideSeconds => durationOverrideSeconds;
        public float RiseDurationSeconds => riseDurationSeconds;
        public float ApexHoldDurationSeconds => apexHoldDurationSeconds;
        public float FallDurationSeconds => fallDurationSeconds;
        public Easing.EaseType Easing => easing;
        public float ArcHeight => arcHeight;
        public MotionArcMode ArcMode => arcMode;
        public Easing.EaseType ArcRiseEase => arcRiseEase;
        public Easing.EaseType ArcFallEase => arcFallEase;
        public SkillLungeResolveMode ResolveMode => resolveMode;
        public float TargetResolveRange => targetResolveRange;
        public SkillLungeTargetRelationMode TargetRelationMode => targetRelationMode;
        public float StopOffset => stopOffset;
        public float PassThroughExtraDistance => passThroughExtraDistance;
        public bool HorizontalOnly => horizontalOnly;
        public SkillLungeCollisionPolicy CollisionPolicy => collisionPolicy;
        public string RiseAnimationName => riseAnimationName;
        public string ApexAnimationName => apexAnimationName;
        public string FallAnimationName => fallAnimationName;
        public string LandEndAnimationName => landEndAnimationName;
        public bool InvertForward => invertForward;
        public bool UseSnapshotForward => useSnapshotForward;
        public bool StopAtEnd => stopAtEnd;
        public bool UseMovePosition => useMovePosition;
        public bool AllowReplace => allowReplace;
    }
}
