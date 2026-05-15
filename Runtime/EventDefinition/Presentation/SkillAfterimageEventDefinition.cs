using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 캐릭터 잔상 이벤트의 실행 방식을 정의합니다.
    /// </summary>
    public enum SkillAfterimageMode
    {
        /// <summary>
        /// 지정된 지속 시간 동안 일정 주기로 잔상을 생성합니다.
        /// </summary>
        Trail = 0,

        /// <summary>
        /// 이벤트 실행 시점의 현재 스프라이트를 단발 잔상으로 1회 생성합니다.
        /// </summary>
        Snapshot = 1,

        /// <summary>
        /// 선택한 대상의 진행 중인 잔상 생성을 중지합니다.
        /// </summary>
        Stop = 2,
    }

    /// <summary>
    /// 캐릭터 잔상 이벤트를 적용할 대상을 정의합니다.
    /// </summary>
    public enum SkillAfterimageTargetType
    {
        /// <summary>
        /// 현재 스킬을 사용한 캐스터에게 잔상을 적용합니다.
        /// </summary>
        Caster = 0,

        /// <summary>
        /// 현재 스킬 컨텍스트의 고정 타겟에게 잔상을 적용합니다.
        /// </summary>
        LockedTarget = 1,

        /// <summary>
        /// 같은 스킬 실행 중 actorKey로 등록된 더미 캐릭터에게 잔상을 적용합니다.
        /// </summary>
        DummyActor = 2,
    }

    /// <summary>
    /// 스킬 타임라인에서 캐릭터 잔상 트레일 또는 단발 잔상을 실행하기 위한 이벤트 정의입니다.
    /// Bake된 RuntimeSequence의 Payload로 저장되어 <see cref="SkillExecutor"/>에서 실행됩니다.
    /// </summary>
    public sealed class SkillAfterimageEventDefinition : ScriptableObject
    {
        [Header("Target")]
        [Tooltip("잔상 효과를 적용할 대상입니다.")]
        public SkillAfterimageTargetType targetType = SkillAfterimageTargetType.Caster;

        [Tooltip("targetType이 DummyActor일 때 사용할 더미 캐릭터 식별 키입니다.")]
        public string actorKey = "dummy_1";

        [Tooltip("대상을 찾지 못했을 때의 처리 정책입니다.")]
        public DummyMissingActorPolicy missingActorPolicy = DummyMissingActorPolicy.Warn;

        [Header("Mode")]
        [Tooltip("잔상 이벤트 실행 방식입니다.")]
        public SkillAfterimageMode mode = SkillAfterimageMode.Trail;

        [Header("Timing")]
        [Tooltip("켜면 Timeline Clip 길이를 트레일 지속 시간으로 사용합니다.")]
        public bool useClipDuration = true;

        [Tooltip("useClipDuration이 꺼져 있을 때 사용할 트레일 지속 시간(초)입니다.")]
        [Min(0f)] public float durationOverrideSeconds = 0f;

        [Tooltip("트레일 모드에서 잔상을 생성하는 주기(초)입니다.")]
        [Min(0.005f)] public float spawnIntervalSeconds = 0.03f;

        [Tooltip("각 잔상 오브젝트가 유지되는 시간(초)입니다.")]
        [Min(0.01f)] public float ghostLifetimeSeconds = 0.25f;

        [Header("Visual")]
        [Tooltip("생성되는 잔상 색상입니다. 알파값도 함께 사용됩니다.")]
        public Color ghostColor = new(0.25f, 0.65f, 1f, 0.65f);

        [Tooltip("원본 SpriteRenderer sortingOrder 기준 보정값입니다.")]
        public int sortingOrderOffset = -1;

        [Header("End Policy")]
        [Tooltip("스킬이 정상 종료될 때 진행 중인 잔상 생성을 중지할지 여부입니다.")]
        public bool clearOnSkillEnd = true;

        [Tooltip("스킬이 취소될 때 진행 중인 잔상 생성을 중지할지 여부입니다.")]
        public bool clearOnCancel = true;

        /// <summary>
        /// 이벤트 길이와 오버라이드 설정을 기준으로 실제 트레일 지속 시간을 계산합니다.
        /// </summary>
        /// <param name="clipDurationSeconds">Timeline Clip에서 계산된 이벤트 길이(초)입니다.</param>
        /// <returns>런타임에서 사용할 트레일 지속 시간(초)입니다.</returns>
        public float ResolveDuration(float clipDurationSeconds)
        {
            return useClipDuration
                ? Mathf.Max(0f, clipDurationSeconds)
                : Mathf.Max(0f, durationOverrideSeconds);
        }
    }
}
