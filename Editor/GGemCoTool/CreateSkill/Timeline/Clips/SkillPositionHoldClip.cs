using System;
using Config;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 캐릭터를 현재 위치에 머무르게 하는 Authoring용 타임라인 클립입니다.
    /// Bake 과정에서 <see cref="GGemCo2DSkill.PositionHoldEventDefinition"/> Payload로 변환됩니다.
    /// </summary>
    [Serializable]
    public sealed class SkillPositionHoldClip : SkillEventClipBase
    {
        [Tooltip("0보다 크면 타임라인 클립 길이 대신 이 값을 사용합니다. 0 이하면 클립 길이를 사용합니다.")]
        [SerializeField] private float durationOverrideSeconds = 0f;

        [Tooltip("머무르기 유지 정책입니다. ClipDuration 또는 UntilSkillEnd 를 선택합니다.")]
        [SerializeField] private PositionHoldDurationPolicy durationPolicy = PositionHoldDurationPolicy.ClipDuration;

        [Tooltip("모션 종료 시 Rigidbody2D 속도를 정지 상태로 정리할지 여부입니다.")]
        [SerializeField] private bool stopAtEnd = true;

        [Tooltip("Kinematic Rigidbody2D에서 MovePosition을 사용하여 위치를 유지합니다.")]
        [SerializeField] private bool useMovePosition = true;

        [Tooltip("같은 채널의 기존 Skill 모션을 덮어쓸 수 있는지 여부입니다.")]
        [SerializeField] private bool allowReplace = true;

        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.PositionHold;

        public float DurationOverrideSeconds => durationOverrideSeconds;
        public PositionHoldDurationPolicy DurationPolicy => durationPolicy;
        public bool StopAtEnd => stopAtEnd;
        public bool UseMovePosition => useMovePosition;
        public bool AllowReplace => allowReplace;
    }
}
