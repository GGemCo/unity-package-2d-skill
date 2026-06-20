using System;
using Config;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 Timeline의 특정 시점에서 타겟 또는 기준 위치를 이름 있는 위치 앵커로 저장하는 클립입니다.
    /// </summary>
    [Serializable]
    public sealed class SkillCaptureTargetPositionClip : SkillEventClipBase
    {
        [Header("Position Anchor")]
        [Tooltip("같은 스킬 실행 안에서 이후 이벤트가 참조할 위치 앵커 키입니다.")]
        [SerializeField] private string anchorKey = "target";

        [Tooltip("위치 앵커에 저장할 기준 위치입니다.")]
        [SerializeField] private SkillPositionCaptureSource source = SkillPositionCaptureSource.Target;

        [Tooltip("source가 DummyActor일 때 조회할 더미 Actor 키입니다.")]
        [SerializeField] private string actorKey = "dummy_1";

        [Tooltip("더미 Actor를 찾지 못했을 때의 처리 정책입니다.")]
        [SerializeField] private DummyMissingActorPolicy missingActorPolicy = DummyMissingActorPolicy.Warn;

        [Tooltip("타겟 기준 위치를 저장할 때 적용할 세부 목표점 보정 정책입니다.")]
        [SerializeField] private SkillPositionCaptureTargetPointPolicy targetPointPolicy =
            SkillPositionCaptureTargetPointPolicy.UseSourcePosition;

        [Tooltip("타겟 중심 또는 최종 기준 위치에 더할 월드 오프셋입니다.")]
        [SerializeField] private Vector2 offset = Vector2.zero;

        [Tooltip("타겟 HitArea 안에서 사용할 정규화 좌표입니다. (0,0)=좌하단, (1,1)=우상단입니다.")]
        [SerializeField] private Vector2 targetHitAreaNormalized = new(0.5f, 0.5f);

        [Header("Ground Projection")]
        [Tooltip("계산된 캡처 위치를 실제 2D 지면 표면으로 투영하기 위한 설정입니다.")]
        [SerializeField] private SkillGroundProjectionOptions groundProjection =
            SkillGroundProjectionOptions.CreateDefault();

        [Header("Targeting Overrides")]
        [Tooltip("스킬 기본 타겟팅 모드를 이벤트 단위로 덮어쓸 때 사용합니다.")]
        [SerializeField] private TargetingOverride targetingOverride;

        /// <summary>
        /// 이 클립이 생성하는 스킬 이벤트 타입입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType =>
            ConfigCommonSkill.SkillEventType.CaptureTargetPosition;

        /// <summary>
        /// 저장할 위치 앵커 키입니다.
        /// </summary>
        public string AnchorKey => anchorKey;

        /// <summary>
        /// 위치 앵커에 저장할 기준 위치입니다.
        /// </summary>
        public SkillPositionCaptureSource Source => source;

        /// <summary>
        /// 더미 Actor 위치를 저장할 때 조회할 actorKey입니다.
        /// </summary>
        public string ActorKey => actorKey;

        /// <summary>
        /// 더미 Actor를 찾지 못했을 때의 처리 정책입니다.
        /// </summary>
        public DummyMissingActorPolicy MissingActorPolicy => missingActorPolicy;

        /// <summary>
        /// 타겟 기준 위치 저장 시 사용할 세부 목표점 보정 정책입니다.
        /// </summary>
        public SkillPositionCaptureTargetPointPolicy TargetPointPolicy => targetPointPolicy;

        /// <summary>
        /// 저장 위치에 적용할 월드 오프셋입니다.
        /// </summary>
        public Vector2 Offset => offset;

        /// <summary>
        /// 타겟 HitArea 안에서 사용할 정규화 좌표입니다.
        /// </summary>
        public Vector2 TargetHitAreaNormalized => targetHitAreaNormalized;

        /// <summary>
        /// 계산된 캡처 위치에 적용할 지면 투영 설정입니다.
        /// </summary>
        public SkillGroundProjectionOptions GroundProjection => groundProjection;

        /// <summary>
        /// 이벤트 단위 타겟팅 오버라이드 설정입니다.
        /// </summary>
        public TargetingOverride TargetingOverride => targetingOverride;
    }
}
