using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트로 생성된 더미 캐릭터에 특정 애니메이션을 재생할 때 사용하는 정의 데이터입니다.
    /// </summary>
    public sealed class PlayDummyCharacterAnimationEventDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("애니메이션을 재생할 더미 캐릭터 식별 키입니다.")]
        public string actorKey = "dummy_1";

        [Header("Animation")]
        [Tooltip("재생할 애니메이션 이름입니다.")]
        public string animationName = ICharacterAnimationController.WaitForwardAnim;

        [Tooltip("애니메이션 루프 여부입니다.")]
        public bool loop;

        [Tooltip("애니메이션 재생 속도 배율입니다.")]
        public float timeScale = 1f;

        [Header("Duration")]
        [Tooltip("애니메이션 유지 시간 해석 정책입니다.")]
        public DummyAnimationDurationPolicy durationPolicy = DummyAnimationDurationPolicy.UseClipWindow;

        [Header("End")]
        [Tooltip("애니메이션 유지 시간이 끝났을 때 적용할 후속 전환 정책입니다.")]
        public DummyAnimationEndPolicy endPolicy = DummyAnimationEndPolicy.PlayWait;

        [Tooltip("endPolicy가 PlayCustom일 때 재생할 애니메이션 이름입니다.")]
        public string endAnimationName = ICharacterAnimationController.WaitForwardAnim;

        [Tooltip("커스텀 종료 애니메이션 루프 여부입니다.")]
        public bool endAnimationLoop = true;

        [Tooltip("커스텀 종료 애니메이션 재생 속도 배율입니다.")]
        public float endAnimationTimeScale = 1f;

        [Header("Policy")]
        [Tooltip("actorKey에 해당하는 더미를 찾지 못했을 때 처리 정책입니다.")]
        public DummyMissingActorPolicy missingActorPolicy = DummyMissingActorPolicy.Warn;
    }
}
