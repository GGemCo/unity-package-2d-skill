using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트로 더미 캐릭터를 이동시킬 때 사용하는 정의 데이터입니다.
    /// </summary>
    public sealed class MoveDummyCharacterEventDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("이동 대상을 결정하는 참조 방식입니다. Caster를 선택하면 actorKey는 무시됩니다.")]
        public DummyActorReferenceType actorReferenceType = DummyActorReferenceType.Actor;

        [Tooltip("이동시킬 더미 캐릭터를 식별하는 키입니다.")]
        public string actorKey = "dummy_1";

        [Header("Move Target")]
        [Tooltip("이동 목표 위치 해석 방식입니다.")]
        public DummyMoveTargetMode moveTargetMode = DummyMoveTargetMode.GroundPoint;

        [Tooltip("moveTargetMode가 NamedPositionAnchor일 때 참조할 위치 앵커 키입니다.")]
        public string namedAnchorKey;

        [Tooltip("moveTargetMode가 AbsoluteWorld일 때 사용할 지면 기준 절대 월드 좌표입니다. 공중 높이는 별도 이벤트로 제어합니다.")]
        public Vector3 absoluteWorldPosition;

        [Tooltip("moveTargetMode가 LockedTargetFront일 때 타겟 중심에서 더미가 위치한 좌/우 방향으로 더할 거리입니다. 양수는 같은 방향, 음수는 반대 방향으로 계산됩니다.")]
        public float targetFrontDistance = 0.5f;

        [Tooltip("해석된 지면 목표 위치에 더할 오프셋입니다. 공중 높이 오프셋은 포함하지 않습니다.")]
        public Vector3 localOffset;

        [Tooltip("true이면 목표 위치 계산에 현재 시점 대신 스킬 시작 스냅샷(target/ground)을 사용합니다.")]
        public bool useSnapshotCenter;

        [Header("Motion")]
        [Tooltip("이동 지속 시간(초)입니다.")]
        public float durationSeconds = 0.35f;

        [Tooltip("이동 보간 Easing 타입입니다.")]
        public Easing.EaseType easing = Easing.EaseType.Linear;

        [Tooltip("Kinematic Rigidbody2D에서 MovePosition 기반 이동을 사용할지 여부입니다.")]
        public bool useMovePosition = true;

        [Tooltip("이동 종료 시 정지 처리를 수행할지 여부입니다.")]
        public bool stopAtEnd = true;

        [Tooltip("같은 채널 모션을 덮어쓸지 여부입니다.")]
        public bool allowReplace = true;

        [Tooltip("true이면 이동 중 매 프레임 타겟의 좌우 방향을 바라보도록 갱신합니다.")]
        public bool lookAtTargetDuringMove;

        [Header("Animation")]
        [Tooltip("이동 시작 시 지정한 애니메이션을 재생할지 여부입니다.")]
        public bool playMoveAnimation;

        [Tooltip("이동 시작 시 재생할 애니메이션 이름입니다.")]
        public string moveAnimationName = ICharacterAnimationController.WalkForwardAnim;

        [Tooltip("이동 시작 애니메이션 루프 여부입니다.")]
        public bool moveAnimationLoop = true;

        [Tooltip("이동 시작 애니메이션 재생 속도 배율입니다.")]
        public float moveAnimationTimeScale = 1f;

        [Header("Policy")]
        [Tooltip("actorKey에 해당하는 더미를 찾지 못했을 때 처리 정책입니다.")]
        public DummyMissingActorPolicy missingActorPolicy = DummyMissingActorPolicy.Warn;
    }
}
