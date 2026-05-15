using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트로 더미 캐릭터를 파괴(제거)할 때 사용하는 정의 데이터입니다.
    /// </summary>
    public sealed class DespawnDummyCharacterEventDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("제거할 더미 캐릭터를 식별하는 키입니다.")]
        public string actorKey = "dummy_1";

        [Header("Fade Out")]
        [Tooltip("true이면 제거 시 페이드 아웃을 재생합니다.")]
        public bool fadeOutEnabled = true;

        [Tooltip("페이드 아웃 시간(초)입니다.")]
        public float fadeOutDurationSeconds = 0.15f;

        [Header("Destroy")]
        [Tooltip("true이면 페이드 아웃 후 GameObject를 Destroy 합니다. false이면 비활성화만 수행합니다.")]
        public bool destroyAfterFade = true;

        [Header("Policy")]
        [Tooltip("actorKey에 해당하는 더미를 찾지 못했을 때 처리 정책입니다.")]
        public DummyMissingActorPolicy missingActorPolicy = DummyMissingActorPolicy.Warn;
    }
}
