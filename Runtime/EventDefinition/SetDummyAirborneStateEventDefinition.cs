using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트로 더미 캐릭터의 공중 상태(높이/중력)를 제어할 때 사용하는 정의 데이터입니다.
    /// </summary>
    public sealed class SetDummyAirborneStateEventDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("공중 상태를 변경할 더미 캐릭터 식별 키입니다.")]
        public string actorKey = "dummy_1";

        [Header("Airborne State")]
        [Tooltip("true이면 공중 상태를 활성화하고, false이면 지면 상태로 복귀합니다.")]
        public bool airborneEnabled = true;

        [Tooltip("airborneEnabled가 true일 때 목표 공중 높이(지면 기준 +Y)입니다.")]
        public float targetAirHeight = 1f;

        [Tooltip("공중 높이 보간 시간(초)입니다.")]
        public float durationSeconds = 0.2f;

        [Tooltip("공중 높이 보간 Easing 타입입니다.")]
        public Easing.EaseType easing = Easing.EaseType.Linear;

        [Tooltip("기존 공중 높이 변경 보간을 새 요청으로 덮어쓸지 여부입니다.")]
        public bool allowReplace = true;

        [Header("Policy")]
        [Tooltip("actorKey에 해당하는 더미를 찾지 못했을 때 처리 정책입니다.")]
        public DummyMissingActorPolicy missingActorPolicy = DummyMissingActorPolicy.Warn;
    }
}
