using System;
using Config;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 실행 중 캐릭터 잔상 트레일 또는 단발 잔상을 생성하는 Timeline 이벤트 클립입니다.
    /// Bake 과정에서 <see cref="SkillAfterimageEventDefinition"/> Payload로 변환됩니다.
    /// </summary>
    [Serializable]
    public sealed class SkillAfterimageClip : SkillEventClipBase
    {
        [Header("Target")]
        [Tooltip("잔상 효과를 적용할 대상입니다.")]
        [SerializeField] private SkillAfterimageTargetType targetType = SkillAfterimageTargetType.Caster;

        [Tooltip("targetType이 DummyActor일 때 사용할 더미 캐릭터 식별 키입니다.")]
        [SerializeField] private string actorKey = "dummy_1";

        [Tooltip("대상을 찾지 못했을 때의 처리 정책입니다.")]
        [SerializeField] private DummyMissingActorPolicy missingActorPolicy = DummyMissingActorPolicy.Warn;

        [Header("Mode")]
        [Tooltip("잔상 이벤트 실행 방식입니다.")]
        [SerializeField] private SkillAfterimageMode mode = SkillAfterimageMode.Trail;

        [Header("Timing")]
        [Tooltip("켜면 Timeline Clip 길이를 트레일 지속 시간으로 사용합니다.")]
        [SerializeField] private bool useClipDuration = true;

        [Tooltip("useClipDuration이 꺼져 있을 때 사용할 트레일 지속 시간(초)입니다.")]
        [Min(0f)]
        [SerializeField] private float durationOverrideSeconds = 0f;

        [Tooltip("트레일 모드에서 잔상을 생성하는 주기(초)입니다.")]
        [Min(0.005f)]
        [SerializeField] private float spawnIntervalSeconds = 0.03f;

        [Tooltip("각 잔상 오브젝트가 유지되는 시간(초)입니다.")]
        [Min(0.01f)]
        [SerializeField] private float ghostLifetimeSeconds = 0.25f;

        [Header("Visual")]
        [Tooltip("생성되는 잔상 색상입니다. 알파값도 함께 사용됩니다.")]
        [SerializeField] private Color ghostColor = new(0.25f, 0.65f, 1f, 0.65f);

        [Tooltip("원본 SpriteRenderer sortingOrder 기준 보정값입니다.")]
        [SerializeField] private int sortingOrderOffset = -1;

        [Header("End Policy")]
        [Tooltip("스킬이 정상 종료될 때 진행 중인 잔상 생성을 중지할지 여부입니다.")]
        [SerializeField] private bool clearOnSkillEnd = true;

        [Tooltip("스킬이 취소될 때 진행 중인 잔상 생성을 중지할지 여부입니다.")]
        [SerializeField] private bool clearOnCancel = true;

        /// <summary>
        /// 이 클립이 표현하는 스킬 이벤트 타입입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Afterimage;

        /// <summary>
        /// 잔상 적용 대상을 반환합니다.
        /// </summary>
        public SkillAfterimageTargetType TargetType => targetType;

        /// <summary>
        /// 대상 더미 캐릭터 키를 반환합니다.
        /// </summary>
        public string ActorKey => actorKey;

        /// <summary>
        /// 대상 미존재 시 처리 정책을 반환합니다.
        /// </summary>
        public DummyMissingActorPolicy MissingActorPolicy => missingActorPolicy;

        /// <summary>
        /// 잔상 실행 방식을 반환합니다.
        /// </summary>
        public SkillAfterimageMode Mode => mode;

        /// <summary>
        /// Timeline Clip 길이를 지속 시간으로 사용할지 여부를 반환합니다.
        /// </summary>
        public bool UseClipDuration => useClipDuration;

        /// <summary>
        /// 지속 시간 오버라이드 값을 반환합니다.
        /// </summary>
        public float DurationOverrideSeconds => durationOverrideSeconds;

        /// <summary>
        /// 잔상 생성 주기를 반환합니다.
        /// </summary>
        public float SpawnIntervalSeconds => spawnIntervalSeconds;

        /// <summary>
        /// 잔상 수명을 반환합니다.
        /// </summary>
        public float GhostLifetimeSeconds => ghostLifetimeSeconds;

        /// <summary>
        /// 잔상 색상을 반환합니다.
        /// </summary>
        public Color GhostColor => ghostColor;

        /// <summary>
        /// SortingOrder 보정값을 반환합니다.
        /// </summary>
        public int SortingOrderOffset => sortingOrderOffset;

        /// <summary>
        /// 스킬 정상 종료 시 잔상 생성 중지 여부를 반환합니다.
        /// </summary>
        public bool ClearOnSkillEnd => clearOnSkillEnd;

        /// <summary>
        /// 스킬 취소 시 잔상 생성 중지 여부를 반환합니다.
        /// </summary>
        public bool ClearOnCancel => clearOnCancel;
    }
}
