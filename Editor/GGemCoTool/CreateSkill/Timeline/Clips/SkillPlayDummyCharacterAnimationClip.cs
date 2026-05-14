using System;
using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 실행 중 생성된 더미 캐릭터에 특정 애니메이션을 재생하는 타임라인 이벤트 클립입니다.
    /// Bake 과정에서 <see cref="PlayDummyCharacterAnimationEventDefinition"/> Payload로 변환됩니다.
    /// </summary>
    [Serializable]
    public sealed class SkillPlayDummyCharacterAnimationClip : SkillEventClipBase
    {
        [Header("Identity")]
        [Tooltip("애니메이션 재생 대상을 결정하는 참조 방식입니다. Caster를 선택하면 actorKey는 무시됩니다.")]
        [SerializeField] private DummyActorReferenceType actorReferenceType = DummyActorReferenceType.Actor;
        [SerializeField] private string actorKey = "dummy_1";

        [Header("Animation")]
        [SerializeField] private string animationName = ICharacterAnimationController.WaitForwardAnim;
        [SerializeField] private bool loop;
        [SerializeField] private float timeScale = 1f;

        [Header("Duration")]
        [SerializeField] private DummyAnimationDurationPolicy durationPolicy = DummyAnimationDurationPolicy.UseClipWindow;

        [Header("End")]
        [SerializeField] private DummyAnimationEndPolicy endPolicy = DummyAnimationEndPolicy.PlayWait;
        [SerializeField] private string endAnimationName = ICharacterAnimationController.WaitForwardAnim;
        [SerializeField] private bool endAnimationLoop = true;
        [SerializeField] private float endAnimationTimeScale = 1f;

        [Header("Policy")]
        [SerializeField] private DummyMissingActorPolicy missingActorPolicy = DummyMissingActorPolicy.Warn;

        /// <summary>
        /// 이 클립이 표현하는 스킬 이벤트 타입입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.PlayDummyCharacterAnimation;

        /// <summary>
        /// 애니메이션 재생 대상을 결정하는 참조 방식을 반환합니다.
        /// </summary>
        public DummyActorReferenceType ActorReferenceType => actorReferenceType;

        /// <summary>
        /// 대상 더미 캐릭터 키를 반환합니다.
        /// </summary>
        public string ActorKey => actorKey;

        /// <summary>
        /// 재생할 애니메이션 이름을 반환합니다.
        /// </summary>
        public string AnimationName => animationName;

        /// <summary>
        /// 애니메이션 루프 여부를 반환합니다.
        /// </summary>
        public bool Loop => loop;

        /// <summary>
        /// 애니메이션 재생 속도 배율을 반환합니다.
        /// </summary>
        public float TimeScale => timeScale;

        /// <summary>
        /// 애니메이션 유지 시간 해석 정책을 반환합니다.
        /// </summary>
        public DummyAnimationDurationPolicy DurationPolicy => durationPolicy;

        /// <summary>
        /// 후속 전환 정책을 반환합니다.
        /// </summary>
        public DummyAnimationEndPolicy EndPolicy => endPolicy;

        /// <summary>
        /// 커스텀 종료 애니메이션 이름을 반환합니다.
        /// </summary>
        public string EndAnimationName => endAnimationName;

        /// <summary>
        /// 커스텀 종료 애니메이션 루프 여부를 반환합니다.
        /// </summary>
        public bool EndAnimationLoop => endAnimationLoop;

        /// <summary>
        /// 커스텀 종료 애니메이션 재생 속도 배율을 반환합니다.
        /// </summary>
        public float EndAnimationTimeScale => endAnimationTimeScale;

        /// <summary>
        /// 더미 미존재 시 처리 정책을 반환합니다.
        /// </summary>
        public DummyMissingActorPolicy MissingActorPolicy => missingActorPolicy;
    }
}
