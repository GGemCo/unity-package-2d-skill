using System;
using Config;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 실행 중 생성된 더미 캐릭터를 제거하는 타임라인 이벤트 클립입니다.
    /// Bake 과정에서 <see cref="DespawnDummyCharacterEventDefinition"/> Payload로 변환됩니다.
    /// </summary>
    [Serializable]
    public sealed class SkillDespawnDummyCharacterClip : SkillEventClipBase
    {
        [Header("Identity")]
        [SerializeField] private string actorKey = "dummy_1";

        [Header("Fade Out")]
        [SerializeField] private bool fadeOutEnabled = true;
        [SerializeField] private float fadeOutDurationSeconds = 0.15f;

        [Header("Destroy")]
        [SerializeField] private bool destroyAfterFade = true;

        [Header("Policy")]
        [SerializeField] private DummyMissingActorPolicy missingActorPolicy = DummyMissingActorPolicy.Warn;

        /// <summary>
        /// 이 클립이 표현하는 스킬 이벤트 타입입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.DespawnDummyCharacter;

        /// <summary>
        /// 제거 대상 더미 캐릭터 키를 반환합니다.
        /// </summary>
        public string ActorKey => actorKey;

        /// <summary>
        /// 페이드 아웃 사용 여부를 반환합니다.
        /// </summary>
        public bool FadeOutEnabled => fadeOutEnabled;

        /// <summary>
        /// 페이드 아웃 시간(초)을 반환합니다.
        /// </summary>
        public float FadeOutDurationSeconds => fadeOutDurationSeconds;

        /// <summary>
        /// 페이드 아웃 후 Destroy 여부를 반환합니다.
        /// </summary>
        public bool DestroyAfterFade => destroyAfterFade;

        /// <summary>
        /// 더미 미존재 시 처리 정책을 반환합니다.
        /// </summary>
        public DummyMissingActorPolicy MissingActorPolicy => missingActorPolicy;
    }
}
