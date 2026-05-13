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
        [SerializeField] private string actorKey = "dummy_1";

        [Header("Move Target")]
        [SerializeField] private DummyMoveTargetMode moveTargetMode = DummyMoveTargetMode.GroundPoint;
        [SerializeField] private string namedAnchorKey;
        [Tooltip("AbsoluteWorld 모드에서 사용할 지면 기준 좌표입니다. 공중 높이는 별도 공중 이벤트로 제어합니다.")]
        [SerializeField] private Vector3 absoluteWorldPosition;
        [Tooltip("지면 기준 목표 위치에 더할 오프셋입니다.")]
        [SerializeField] private Vector3 localOffset;
        [SerializeField] private bool useSnapshotCenter;

        [Header("Motion")]
        [SerializeField] private float durationSeconds = 0.35f;
        [SerializeField] private Easing.EaseType easing = GGemCo2DCore.Easing.EaseType.Linear;
        [SerializeField] private bool useMovePosition = true;
        [SerializeField] private bool stopAtEnd = true;
        [SerializeField] private bool allowReplace = true;

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
        /// 목표 위치 오프셋을 반환합니다.
        /// </summary>
        public Vector3 LocalOffset => localOffset;

        /// <summary>
        /// 스냅샷 중심 사용 여부를 반환합니다.
        /// </summary>
        public bool UseSnapshotCenter => useSnapshotCenter;

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
