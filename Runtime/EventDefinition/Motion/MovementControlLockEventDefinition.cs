using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이동 조작 잠금 이벤트의 유지 시간을 결정하는 정책입니다.
    /// </summary>
    public enum MovementControlLockDurationPolicy
    {
        /// <summary>Timeline 클립 길이 또는 durationOverrideSeconds 동안만 잠금을 유지합니다.</summary>
        ClipDuration = 0,

        /// <summary>현재 스킬 실행이 종료되거나 취소될 때까지 잠금을 유지합니다.</summary>
        UntilSkillEnd = 1,
    }

    /// <summary>
    /// 스킬 이동 조작 잠금 이벤트가 자동 이동을 처리하는 방식입니다.
    /// </summary>
    public enum SkillAutoMoveControlPolicy
    {
        /// <summary>자동 이동에는 직접 관여하지 않습니다.</summary>
        None = 0,

        /// <summary>자동 이동을 일시정지하고 이벤트 종료 시 다시 이어갈 수 있게 합니다.</summary>
        Suspend = 1,

        /// <summary>현재 자동 이동 요청을 취소합니다.</summary>
        Cancel = 2,
    }

    /// <summary>
    /// 스킬 실행 중 캐릭터 이동과 조작 입력을 잠그는 이벤트 정의입니다.
    /// </summary>
    public sealed class MovementControlLockEventDefinition : ScriptableObject
    {
        [Tooltip("0보다 크면 타임라인 클립 길이 대신 이 값을 사용합니다. 0 이하면 클립 길이를 사용합니다.")]
        public float durationOverrideSeconds = 0f;

        [Tooltip("잠금 유지 정책입니다. ClipDuration 또는 UntilSkillEnd를 선택합니다.")]
        public MovementControlLockDurationPolicy durationPolicy = MovementControlLockDurationPolicy.ClipDuration;

        [Tooltip("이벤트 시작 시 캐릭터 이동 입력, 이동 애니메이션, Rigidbody2D 속도를 즉시 정지합니다.")]
        public bool stopImmediately = true;

        [Tooltip("이벤트 시작 시 Skill 채널의 기존 모션을 취소합니다.")]
        public bool cancelSkillMotion = true;

        [Tooltip("CharacterBase 제어 잠금을 획득하여 플레이어 입력과 캐릭터 제어 로직을 차단합니다.")]
        public bool lockControl = true;

        [Tooltip("자동 이동이 활성화되어 있을 때 처리할 정책입니다.")]
        public SkillAutoMoveControlPolicy autoMovePolicy = SkillAutoMoveControlPolicy.Suspend;
    }
}
