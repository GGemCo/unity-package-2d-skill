using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Arc 전용 점프 이동 이벤트 정의입니다.
    /// 상승/정점 대기/하강 시간을 개별적으로 제어하고,
    /// 각 단계에 대응하는 애니메이션을 분리하여 설정할 수 있습니다.
    /// </summary>
    public sealed class ArcLungeEventDefinition : ScriptableObject
    {
        [Header("Motion")]
        [Tooltip("이 이벤트(클립) 구간 동안 이동할 총 수평 거리(월드 단위)")]
        public float distance = 2.5f;

        [Tooltip("지속시간 오버라이드(<=0 이면 rise/apexHold/fall 합계를 사용하고, 그것도 <=0 이면 이벤트 구간 Start~End를 사용)")]
        public float durationOverrideSeconds = -1f;

        [Tooltip("상승 구간 시간(초).")]
        public float riseDurationSeconds = 0.15f;

        [Tooltip("정점에서 머무르는 시간(초).")]
        public float apexHoldDurationSeconds = 0f;

        [Tooltip("하강 구간 시간(초).")]
        public float fallDurationSeconds = 0.15f;

        [Tooltip("수평 이동 진행에 적용할 easing")]
        public Easing.EaseType easing = Easing.EaseType.Linear;

        [Tooltip("아크 높이(월드 단위)")]
        public float arcHeight = 1.5f;

        [Tooltip("Arc 구현 모드입니다. 기본은 DistancePhased를 권장합니다.")]
        public MotionArcMode arcMode = MotionArcMode.DistancePhased;

        [Tooltip("DistancePhased Arc의 상승 구간 easing 입니다.")]
        public Easing.EaseType arcRiseEase = Easing.EaseType.Linear;

        [Tooltip("DistancePhased Arc의 하강 구간 easing 입니다.")]
        public Easing.EaseType arcFallEase = Easing.EaseType.Linear;

        [Header("Resolve")]
        [Tooltip("고정 거리 / 타겟 추적 중 어떤 방식으로 실제 이동 거리를 계산할지 결정합니다.")]
        public SkillLungeResolveMode resolveMode = SkillLungeResolveMode.FixedDistance;

        [Tooltip("타겟 추적을 허용할 최대 거리(<=0 이면 CastRange, 그것도 없으면 Distance를 사용).")]
        public float targetResolveRange = -1f;

        [Tooltip("타겟 종착 관계를 해석하는 방식입니다.")]
        public SkillLungeTargetRelationMode targetRelationMode = SkillLungeTargetRelationMode.StopBeforeTarget;

        [Tooltip("StopBeforeTarget일 때 타겟과 겹치지 않도록 남길 거리입니다.")]
        public float stopOffset = 0.2f;

        [Tooltip("PassThroughTarget일 때 타겟 중심을 지난 뒤 추가로 이동할 거리입니다.")]
        public float passThroughExtraDistance = 0.5f;

        [Tooltip("true면 X축 기준으로만 타겟 접근 거리를 계산합니다.")]
        public bool horizontalOnly = true;

        [Tooltip("이동 중 타겟과의 충돌 처리 정책입니다. 타겟을 지나가는 연출이 필요할 때 사용할 수 있습니다.")]
        public SkillLungeCollisionPolicy collisionPolicy = SkillLungeCollisionPolicy.Default;

        [Header("Animation")]
        [Tooltip("상승 시작 시 1회 재생할 애니메이션 이름입니다.")]
        public string riseAnimationName;

        [Tooltip("정점 대기 구간에서 재생할 애니메이션 이름입니다.")]
        public string apexAnimationName;

        [Tooltip("하강 구간에서 재생할 애니메이션 이름입니다.")]
        public string fallAnimationName;

        [Tooltip("착지 시 1회 재생할 애니메이션 이름입니다.")]
        public string landEndAnimationName;

        [Header("Direction")]
        [Tooltip("true면 스킬 발동 시점의 전방(캐스터 스냅샷)을 사용합니다.")]
        public bool useSnapshotForward = true;

        [Tooltip("true면 최종 이동 방향을 반전합니다(예: 뒤로 점프).")]
        public bool invertForward = false;

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
