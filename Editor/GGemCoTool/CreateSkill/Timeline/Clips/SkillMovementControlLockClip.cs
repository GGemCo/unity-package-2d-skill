using System;
using Config;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 현재 Player의 이동과 조작 입력을 잠그는 Authoring용 타임라인 클립입니다.
    /// Bake 과정에서 <see cref="MovementControlLockEventDefinition"/> Payload로 변환됩니다.
    /// </summary>
    [Serializable]
    public sealed class SkillMovementControlLockClip : SkillEventClipBase
    {
        [Tooltip("0보다 크면 타임라인 클립 길이 대신 이 값을 사용합니다. 0 이하면 클립 길이를 사용합니다.")]
        [SerializeField] private float durationOverrideSeconds = 0f;

        [Tooltip("잠금 유지 정책입니다. ClipDuration 또는 UntilSkillEnd를 선택합니다.")]
        [SerializeField] private MovementControlLockDurationPolicy durationPolicy = MovementControlLockDurationPolicy.ClipDuration;

        [Tooltip("이벤트 시작 시 Player 이동 입력, 이동 애니메이션, Rigidbody2D 속도를 즉시 정지합니다.")]
        [SerializeField] private bool stopImmediately = true;

        [Tooltip("이벤트 시작 시 Skill 채널의 기존 모션을 취소합니다.")]
        [SerializeField] private bool cancelSkillMotion = true;

        [Tooltip("Player 조작을 어느 범위까지 잠글지 결정합니다.")]
        [SerializeField] private SkillPlayerControlLockMode controlLockMode = SkillPlayerControlLockMode.AllControl;

        [Tooltip("자동 이동이 활성화되어 있을 때 처리할 정책입니다.")]
        [SerializeField] private SkillAutoMoveControlPolicy autoMovePolicy = SkillAutoMoveControlPolicy.Suspend;

        /// <summary>
        /// 이 클립이 생성하는 스킬 이벤트 타입입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.MovementControlLock;

        /// <summary>
        /// 타임라인 클립 길이 대신 사용할 잠금 시간입니다.
        /// </summary>
        public float DurationOverrideSeconds => durationOverrideSeconds;

        /// <summary>
        /// 이동 조작 잠금 유지 정책입니다.
        /// </summary>
        public MovementControlLockDurationPolicy DurationPolicy => durationPolicy;

        /// <summary>
        /// 이벤트 시작 시 Player 이동을 즉시 멈출지 여부입니다.
        /// </summary>
        public bool StopImmediately => stopImmediately;

        /// <summary>
        /// 이벤트 시작 시 Skill 채널 모션을 취소할지 여부입니다.
        /// </summary>
        public bool CancelSkillMotion => cancelSkillMotion;

        /// <summary>
        /// Player 조작을 차단할 범위입니다.
        /// </summary>
        public SkillPlayerControlLockMode ControlLockMode => controlLockMode;

        /// <summary>
        /// 자동 이동 처리 정책입니다.
        /// </summary>
        public SkillAutoMoveControlPolicy AutoMovePolicy => autoMovePolicy;
    }
}
