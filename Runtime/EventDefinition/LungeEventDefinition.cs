using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 전진(러시/대시) 이벤트 정의.
    /// - 스킬 타임라인(이벤트 구간)과 이동 구간을 정밀하게 동기화하기 위한 Payload 입니다.
    /// - Speed 기반이 아니라 "거리(Distance)" 기반으로 설계하여, 클립 시간에 따라 일관된 이동감을 제공합니다.
    /// - Arc 옵션을 사용하면 점프형 회피(뒤로 점프)처럼 수직 오프셋을 추가할 수 있습니다.
    /// </summary>
    public sealed class LungeEventDefinition : ScriptableObject
    {
        [Header("Motion")]
        [Tooltip("이 이벤트(클립) 구간 동안 이동할 총 거리(월드 단위)")]
        public float distance = 2.5f;

        [Tooltip("지속시간 오버라이드(<=0 이면 이벤트 구간(Start~End)을 사용)")]
        public float durationOverrideSeconds = -1f;

        [Tooltip("시간→진행률 Easing (Core의 Easing 클래스를 사용)")]
        public Easing.EaseType easing = Easing.EaseType.Linear;

        [Header("Direction")]
        [Tooltip("true면 스킬 발동 시점의 전방(캐스터 스냅샷)을 사용합니다.")]
        public bool useSnapshotForward = true;

        [Tooltip("true면 최종 이동 방향을 반전합니다(예: 뒤로 회피).")]
        public bool invertForward = false;

        [Header("Arc")]
        [Tooltip("true면 Arc(수직 오프셋) 모션을 사용합니다.")]
        public bool useArcMotion = false;

        [Tooltip("Arc 높이(월드 단위). useArcMotion이 true일 때만 유효합니다.")]
        public float arcHeight = 0f;

        [Header("Rigidbody2D")]
        [Tooltip("Kinematic이면 MovePosition 기반 이동을 사용합니다.")]
        public bool useMovePosition = true;

        [Tooltip("종료 시 정지(velocity 기반 구현에서 유효)")]
        public bool stopAtEnd = true;

        [Header("Policy")]
        [Tooltip("true면 동일 채널(Skill)에서 진행 중인 모션을 덮어쓸 수 있습니다.")]
        public bool allowReplace = false;
    }
}
