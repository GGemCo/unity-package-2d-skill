using System;
using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 실행 중 생성된 더미 캐릭터를 이동시키는 타임라인 이벤트 클립입니다.
    /// Bake 과정에서 <see cref="MoveDummyCharacterEventDefinition"/> Payload로 변환됩니다.
    /// </summary>
    [Serializable]
    public sealed class SkillMoveDummyCharacterClip : SkillEventClipBase
    {
        [Header("Identity")]
        [Tooltip("이동 대상을 결정하는 참조 방식입니다. Caster를 선택하면 actorKey는 무시됩니다.")]
        [SerializeField] private DummyActorReferenceType actorReferenceType = DummyActorReferenceType.Actor;
        [SerializeField] private string actorKey = "dummy_1";

        [Header("Move Target")]
        [SerializeField] private DummyMoveTargetMode moveTargetMode = DummyMoveTargetMode.GroundPoint;
        [SerializeField] private string namedAnchorKey;
        [Tooltip("AbsoluteWorld 모드에서 사용할 지면 기준 좌표입니다. 공중 높이는 별도 공중 이벤트로 제어합니다.")]
        [SerializeField] private Vector3 absoluteWorldPosition;
        [Tooltip("LockedTargetFront 모드에서 타겟 중심으로부터 더미가 있던 좌/우 방향으로 더할 거리입니다. 양수는 현재 더미가 있는 방향, 음수는 반대 방향으로 계산됩니다.")]
        [SerializeField] private float targetFrontDistance = 0.5f;
        [Tooltip("LockedTargetFront 모드에서 targetFrontDistance가 0이 아닐 때 목표 Y 좌표를 계산하는 정책입니다.")]
        [SerializeField] private DummyLockedTargetFrontYPolicy lockedTargetFrontYPolicy = DummyLockedTargetFrontYPolicy.UseTargetY;
        [Tooltip("지면 기준 목표 위치에 더할 오프셋입니다.")]
        [SerializeField] private Vector3 localOffset;
        [SerializeField] private bool useSnapshotCenter;

        [Header("Screen Clamp")]
        [Tooltip("최종 표시 위치가 카메라 화면을 벗어나면 화면 가장자리 안쪽으로 목표 지면 좌표를 보정합니다.")]
        [SerializeField] private SkillLungeScreenClampPolicy screenClampPolicy = SkillLungeScreenClampPolicy.None;
        [Tooltip("화면 경계 안쪽으로 유지할 여유 거리(월드 단위)입니다.")]
        [Min(0f)]
        [SerializeField] private float screenEdgePadding = 0f;

        [Header("Motion")]
        [SerializeField] private float durationSeconds = 0.35f;
        [SerializeField] private Easing.EaseType easing = GGemCo2DCore.Easing.EaseType.Linear;
        [SerializeField] private bool useMovePosition = true;
        [SerializeField] private bool stopAtEnd = true;
        [SerializeField] private bool allowReplace = true;
        [Tooltip("true이면 이동 중 매 프레임 타겟의 좌우 방향을 바라보도록 갱신합니다.")]
        [SerializeField] private bool lookAtTargetDuringMove;

        [Header("Animation")]
        [SerializeField] private bool playMoveAnimation;
        [SerializeField] private string moveAnimationName = ICharacterAnimationController.WalkForwardAnim;
        [SerializeField] private bool moveAnimationLoop = true;
        [SerializeField] private float moveAnimationTimeScale = 1f;

        [Header("Policy")]
        [SerializeField] private DummyMissingActorPolicy missingActorPolicy = DummyMissingActorPolicy.Warn;

        /// <summary>
        /// 이 클립이 표현하는 스킬 이벤트 타입입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.MoveDummyCharacter;

        /// <summary>
        /// 이동 대상을 결정하는 참조 방식을 반환합니다.
        /// </summary>
        public DummyActorReferenceType ActorReferenceType => actorReferenceType;

        /// <summary>
        /// 이동 대상 더미 캐릭터 키를 반환합니다.
        /// </summary>
        public string ActorKey => actorKey;

        /// <summary>
        /// 이동 목표 해석 방식을 반환합니다.
        /// </summary>
        public DummyMoveTargetMode MoveTargetMode => moveTargetMode;

        /// <summary>
        /// 이름 있는 위치 앵커 키를 반환합니다.
        /// </summary>
        public string NamedAnchorKey => namedAnchorKey;

        /// <summary>
        /// 절대 월드 좌표 목표를 반환합니다.
        /// </summary>
        public Vector3 AbsoluteWorldPosition => absoluteWorldPosition;

        /// <summary>
        /// 타겟 중심 기준 좌/우 이동 거리(지면 기준)를 반환합니다. 양수는 더미가 있던 방향, 음수는 반대 방향입니다.
        /// </summary>
        public float TargetFrontDistance => targetFrontDistance;

        /// <summary>
        /// LockedTargetFront 이동 목표의 Y 좌표 계산 정책을 반환합니다.
        /// </summary>
        public DummyLockedTargetFrontYPolicy LockedTargetFrontYPolicy => lockedTargetFrontYPolicy;

        /// <summary>
        /// 목표 위치 오프셋을 반환합니다.
        /// </summary>
        public Vector3 LocalOffset => localOffset;

        /// <summary>
        /// 스냅샷 중심 사용 여부를 반환합니다.
        /// </summary>
        public bool UseSnapshotCenter => useSnapshotCenter;

        /// <summary>
        /// 화면 경계 기준 목표 위치 보정 정책을 반환합니다.
        /// </summary>
        public SkillLungeScreenClampPolicy ScreenClampPolicy => screenClampPolicy;

        /// <summary>
        /// 화면 경계 안쪽으로 유지할 여유 거리(월드 단위)를 반환합니다.
        /// </summary>
        public float ScreenEdgePadding => screenEdgePadding;

        /// <summary>
        /// 이동 지속 시간(초)을 반환합니다.
        /// </summary>
        public float DurationSeconds => durationSeconds;

        /// <summary>
        /// 이동 Easing 타입을 반환합니다.
        /// </summary>
        public Easing.EaseType Easing => easing;

        /// <summary>
        /// MovePosition 사용 여부를 반환합니다.
        /// </summary>
        public bool UseMovePosition => useMovePosition;

        /// <summary>
        /// 이동 종료 시 정지 처리 여부를 반환합니다.
        /// </summary>
        public bool StopAtEnd => stopAtEnd;

        /// <summary>
        /// 기존 모션 덮어쓰기 허용 여부를 반환합니다.
        /// </summary>
        public bool AllowReplace => allowReplace;

        /// <summary>
        /// 이동 중 타겟을 계속 바라볼지 여부를 반환합니다.
        /// </summary>
        public bool LookAtTargetDuringMove => lookAtTargetDuringMove;

        /// <summary>
        /// 이동 애니메이션 재생 여부를 반환합니다.
        /// </summary>
        public bool PlayMoveAnimation => playMoveAnimation;

        /// <summary>
        /// 이동 애니메이션 이름을 반환합니다.
        /// </summary>
        public string MoveAnimationName => moveAnimationName;

        /// <summary>
        /// 이동 애니메이션 루프 여부를 반환합니다.
        /// </summary>
        public bool MoveAnimationLoop => moveAnimationLoop;

        /// <summary>
        /// 이동 애니메이션 시간 배율을 반환합니다.
        /// </summary>
        public float MoveAnimationTimeScale => moveAnimationTimeScale;

        /// <summary>
        /// 더미 미존재 시 처리 정책을 반환합니다.
        /// </summary>
        public DummyMissingActorPolicy MissingActorPolicy => missingActorPolicy;
    }
}
