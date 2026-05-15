using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 실행 중 캐릭터를 현재 위치에 머무르게 하는 이벤트 정의입니다.
    /// </summary>
    public enum PositionHoldDurationPolicy
    {
        /// <summary>클립 길이(또는 durationOverrideSeconds) 동안만 머뭅니다.</summary>
        ClipDuration = 0,

        /// <summary>스킬 런이 종료되거나 취소될 때까지 머뭅니다.</summary>
        UntilSkillEnd = 1,
    }

    /// <summary>
    /// PositionHold 이벤트 정의입니다.
    /// </summary>
    public sealed class PositionHoldEventDefinition : ScriptableObject
    {
        [Tooltip("0보다 크면 타임라인 클립 길이 대신 이 값을 사용합니다. 0 이하면 클립 길이를 사용합니다.")]
        public float durationOverrideSeconds = 0f;

        [Tooltip("머무르기 유지 정책입니다. ClipDuration 또는 UntilSkillEnd 를 선택합니다.")]
        public PositionHoldDurationPolicy durationPolicy = PositionHoldDurationPolicy.ClipDuration;

        [Tooltip("모션 종료 시 Rigidbody2D 속도를 정지 상태로 정리할지 여부입니다.")]
        public bool stopAtEnd = true;

        [Tooltip("Kinematic Rigidbody2D에서 MovePosition을 사용하여 위치를 유지합니다.")]
        public bool useMovePosition = true;

        [Tooltip("같은 채널의 기존 Skill 모션을 덮어쓸 수 있는지 여부입니다.")]
        public bool allowReplace = true;
    }
}
