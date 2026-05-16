using System;
using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 실행 중 더미 캐릭터의 공중 상태(높이/중력)를 제어하는 타임라인 이벤트 클립입니다.
    /// Bake 과정에서 <see cref="SetDummyAirborneStateEventDefinition"/> Payload로 변환됩니다.
    /// </summary>
    [Serializable]
    public sealed class SkillSetDummyAirborneStateClip : SkillEventClipBase
    {
        [Header("Identity")]
        [Tooltip("공중 상태를 적용할 대상을 결정하는 참조 방식입니다. Caster를 선택하면 actorKey는 무시됩니다.")]
        [SerializeField] private DummyActorReferenceType actorReferenceType = DummyActorReferenceType.Actor;
        [SerializeField] private string actorKey = "dummy_1";

        [Header("Airborne State")]
        [SerializeField] private bool airborneEnabled = true;
        [SerializeField] private float targetAirHeight = 1f;
        [SerializeField] private float durationSeconds = 0.2f;
        [SerializeField] private Easing.EaseType easing = GGemCo2DCore.Easing.EaseType.Linear;
        [SerializeField] private bool allowReplace = true;

        [Header("Policy")]
        [SerializeField] private DummyMissingActorPolicy missingActorPolicy = DummyMissingActorPolicy.Warn;

        /// <summary>
        /// 이 클립이 표현하는 스킬 이벤트 타입입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.SetDummyAirborneState;

        /// <summary>
        /// 공중 상태를 적용할 대상 참조 방식을 반환합니다.
        /// </summary>
        public DummyActorReferenceType ActorReferenceType => actorReferenceType;

        /// <summary>
        /// 대상 더미 캐릭터의 actorKey를 반환합니다.
        /// </summary>
        public string ActorKey => actorKey;

        /// <summary>
        /// 공중 상태 활성 여부를 반환합니다.
        /// </summary>
        public bool AirborneEnabled => airborneEnabled;

        /// <summary>
        /// 목표 공중 높이(지면 기준 +Y)를 반환합니다.
        /// </summary>
        public float TargetAirHeight => targetAirHeight;

        /// <summary>
        /// 공중 높이 보간 시간(초)을 반환합니다.
        /// </summary>
        public float DurationSeconds => durationSeconds;

        /// <summary>
        /// 공중 높이 보간 easing을 반환합니다.
        /// </summary>
        public Easing.EaseType Easing => easing;

        /// <summary>
        /// 기존 공중 보간 덮어쓰기 허용 여부를 반환합니다.
        /// </summary>
        public bool AllowReplace => allowReplace;

        /// <summary>
        /// 대상 미존재 시 처리 정책을 반환합니다.
        /// </summary>
        public DummyMissingActorPolicy MissingActorPolicy => missingActorPolicy;
    }
}
