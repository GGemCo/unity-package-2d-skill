using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 런지 이동 거리 해석 방식입니다.
    /// </summary>
    public enum SkillLungeResolveMode
    {
        /// <summary>항상 고정 거리만큼 이동합니다.</summary>
        FixedDistance = 0,

        /// <summary>잠금 타겟이 있으면 타겟 기준으로 이동하고, 없으면 이동하지 않습니다.</summary>
        ToLockedTarget = 1,

        /// <summary>잠금 타겟이 해석 범위 안에 있을 때만 타겟 기준 이동합니다.</summary>
        ToLockedTargetIfWithinResolveRange = 2,

        /// <summary>잠금 타겟이 범위 안이면 타겟 기준, 아니면 고정 거리로 이동합니다.</summary>
        ToLockedTargetElseFixedDistance = 3,
    }

    /// <summary>
    /// 잠금 타겟 기준으로 런지 종료 지점을 해석하는 방식입니다.
    /// </summary>
    public enum SkillLungeTargetRelationMode
    {
        /// <summary>타겟과 겹치지 않도록 앞에서 멈춥니다.</summary>
        StopBeforeTarget = 0,

        /// <summary>타겟 중심까지 이동합니다.</summary>
        ReachTargetCenter = 1,

        /// <summary>타겟 중심을 지나 추가 거리만큼 더 이동합니다.</summary>
        PassThroughTarget = 2,
    }

    /// <summary>
    /// 런지 중 잠금 타겟과의 충돌 처리 정책입니다.
    /// </summary>
    public enum SkillLungeCollisionPolicy
    {
        /// <summary>기본 충돌 정책을 사용합니다.</summary>
        Default = 0,

        /// <summary>런지 중 잠금 타겟 캐릭터와의 충돌을 일시적으로 무시합니다.</summary>
        IgnoreLockedTargetCharacter = 1,
    }

    /// <summary>
    /// 런지 최종 도착 지점을 화면 경계 기준으로 보정하는 정책입니다.
    /// </summary>
    public enum SkillLungeScreenClampPolicy
    {
        /// <summary>화면 경계 보정을 적용하지 않습니다.</summary>
        None = 0,

        /// <summary>최종 도착 위치가 화면 밖이면 화면 가장자리까지만 이동합니다.</summary>
        ClampToViewportEdge = 1,
    }

    /// <summary>
    /// 직선 런지(대시/백스텝) 이벤트 정의입니다.
    /// 스킬 타임라인 이벤트 구간을 런타임 이동 요청으로 변환할 때 사용됩니다.
    /// </summary>
    public sealed class LungeEventDefinition : ScriptableObject
    {
        [Header("Actor")]
        [Tooltip("런지를 수행할 캐릭터 참조 방식입니다. Caster를 선택하면 actorKey는 무시됩니다.")]
        public DummyActorReferenceType actorReferenceType = DummyActorReferenceType.Caster;

        [Tooltip("actorReferenceType이 Actor일 때 사용할 더미 캐릭터 식별 키입니다.")]
        public string actorKey = "dummy_1";

        [Tooltip("actorKey에 해당하는 더미 캐릭터를 찾지 못했을 때 처리 정책입니다.")]
        public DummyMissingActorPolicy missingActorPolicy = DummyMissingActorPolicy.Warn;

        [Header("Motion")]
        [Tooltip("이벤트 구간 동안 이동할 총 거리(월드 단위)입니다.")]
        public float distance = 2.5f;

        [Tooltip("지속 시간 오버라이드(초)입니다. 0 이하면 이벤트 구간 길이를 사용합니다.")]
        public float durationOverrideSeconds = -1f;

        [Tooltip("시간 대비 거리 진행 곡선(Easing)입니다.")]
        public Easing.EaseType easing = Easing.EaseType.Linear;

        [Header("Resolve")]
        [Tooltip("고정 거리 또는 잠금 타겟 기준 이동 해석 방식을 지정합니다.")]
        public SkillLungeResolveMode resolveMode = SkillLungeResolveMode.FixedDistance;

        [Tooltip("타겟 해석 최대 거리입니다. 0 이하면 CastRange, 없으면 distance를 사용합니다.")]
        public float targetResolveRange = -1f;

        [Tooltip("타겟과의 종단 관계(앞에서 멈춤/중심 도달/관통)를 설정합니다.")]
        public SkillLungeTargetRelationMode targetRelationMode = SkillLungeTargetRelationMode.StopBeforeTarget;

        [Tooltip("StopBeforeTarget 모드에서 타겟과 겹치지 않기 위한 여유 거리입니다.")]
        public float stopOffset = 0.2f;

        [Tooltip("PassThroughTarget 모드에서 타겟 중심 이후 추가 이동 거리입니다.")]
        public float passThroughExtraDistance = 0.5f;

        [Tooltip("true면 X축 기준으로만 타겟 거리를 계산합니다.")]
        public bool horizontalOnly = true;

        [Tooltip("런지 중 잠금 타겟 충돌 처리 정책입니다.")]
        public SkillLungeCollisionPolicy collisionPolicy = SkillLungeCollisionPolicy.Default;

        [Header("Screen Clamp")]
        [Tooltip("최종 도착 위치가 카메라 화면을 벗어나면 화면 가장자리까지만 이동하도록 보정합니다.")]
        public SkillLungeScreenClampPolicy screenClampPolicy = SkillLungeScreenClampPolicy.None;

        [Tooltip("화면 경계 안쪽으로 유지할 여유 거리(월드 단위)입니다.")]
        [Min(0f)]
        public float screenEdgePadding = 0f;

        [Header("Direction")]
        [Tooltip("true면 스킬 발동 시점 Forward(캐스트 방향)를 우선 사용합니다.")]
        public bool useSnapshotForward = true;

        [Tooltip("true면 최종 이동 방향을 반전합니다.")]
        public bool invertForward = false;

        [Header("Rigidbody2D")]
        [Tooltip("Kinematic이면 MovePosition 기반 이동을 사용합니다.")]
        public bool useMovePosition = true;

        [Tooltip("종료 시 정지 처리 여부입니다.")]
        public bool stopAtEnd = true;

        [Header("Airborne State")]
        [Tooltip("true면 이 런지 이벤트가 진행되는 동안 캐릭터를 공통 공중 상태로 등록합니다. 지상 직선 돌진은 false를 권장합니다.")]
        public bool acquireAirborneDuringEvent = false;

        [Header("Policy")]
        [Tooltip("true면 동일 채널(Skill)에서 진행 중인 모션을 덮어쓸 수 있습니다.")]
        public bool allowReplace = false;
    }
}
