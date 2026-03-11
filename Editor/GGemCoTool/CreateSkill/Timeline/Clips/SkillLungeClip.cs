using System;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 전진(러시, 대시, 회피 등) 이동 이벤트를 정의하는 Authoring용 타임라인 클립입니다.
    /// Bake 과정에서 <see cref="GGemCo2DSkill.LungeEventDefinition"/> Payload로 변환되어
    /// 실제 스킬 실행 시스템에서 사용됩니다.
    /// 
    /// 이동은 속도 기반이 아니라 총 이동 거리(Distance) 기반으로 설계되며
    /// 필요 시 아크(Arc) 모션을 사용해 점프형 이동을 표현할 수 있습니다.
    /// </summary>
    [Serializable]
    public sealed class SkillLungeClip : SkillEventClipBase
    {
        [Header("Motion")]
        [Tooltip("총 이동 거리(월드 유닛 기준). Duration과 함께 실제 이동 속도가 결정됩니다.")]
        [SerializeField] private float distance = 2.5f;

        [Tooltip("0보다 크면 타임라인 클립 길이 대신 이 값을 사용합니다. (초 단위)")]
        [SerializeField] private float durationOverrideSeconds = 0f;

        [Tooltip("시간 진행에 따른 거리 보간 방식. Linear, EaseIn, EaseOut 등 이동 감속/가속 패턴을 제어합니다.")]
        [SerializeField] private Easing.EaseType easing = GGemCo2DCore.Easing.EaseType.Linear;

        [Header("Direction")]
        [Tooltip("체크 시 현재 Forward 방향의 반대로 이동합니다. (뒤로 회피/백스텝 구현용)")]
        [SerializeField] private bool invertForward = false;

        [Header("Arc")]
        [Tooltip("활성화 시 수직 아크 모션을 추가합니다. 점프형 회피/도약 이동에 사용됩니다.")]
        [SerializeField] private bool useArcMotion = false;

        [Tooltip("아크의 최고 높이(월드 유닛). useArcMotion이 활성화되어야 적용됩니다.")]
        [SerializeField] private float arcHeight = 0f;

        [Header("Rigidbody2D")]
        [Tooltip("모션 종료 시 Rigidbody2D의 속도를 0으로 초기화합니다.")]
        [SerializeField] private bool stopAtEnd = true;

        [Tooltip("Kinematic Rigidbody2D에서 MovePosition을 사용하여 이동합니다. 권장 옵션입니다.")]
        [SerializeField] private bool useMovePosition = true;

        [Tooltip("모션 시작 시 Forward 방향을 고정합니다. 해제 시 이동 중 Forward 변경을 반영합니다.")]
        [SerializeField] private bool useSnapshotForward = true;

        [Header("Policy")]
        [Tooltip("같은 채널의 기존 모션이 실행 중이어도 덮어쓰기를 허용합니다.")]
        [SerializeField] private bool allowReplace = false;

        /// <summary>
        /// 이 클립이 생성하는 스킬 이벤트 타입입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Lunge;

        /// <summary>
        /// 캐릭터가 이동할 총 거리(월드 유닛)입니다.
        /// </summary>
        public float Distance => distance;

        /// <summary>
        /// 타임라인 클립 길이를 대신하여 사용할 이동 지속 시간(초)입니다.
        /// 0 이하일 경우 타임라인 클립 길이를 사용합니다.
        /// </summary>
        public float DurationOverrideSeconds => durationOverrideSeconds;

        /// <summary>
        /// 이동 진행 시 적용되는 보간(Easing) 방식입니다.
        /// </summary>
        public Easing.EaseType Easing => easing;

        /// <summary>
        /// 이동 방향을 Forward의 반대로 뒤집을지 여부입니다.
        /// </summary>
        public bool InvertForward => invertForward;

        /// <summary>
        /// 이동 중 수직 아크 모션을 사용할지 여부입니다.
        /// </summary>
        public bool UseArcMotion => useArcMotion;

        /// <summary>
        /// 아크 모션 사용 시 적용되는 최대 높이입니다.
        /// </summary>
        public float ArcHeight => arcHeight;

        /// <summary>
        /// 이동 종료 시 Rigidbody2D의 속도를 0으로 초기화할지 여부입니다.
        /// </summary>
        public bool StopAtEnd => stopAtEnd;

        /// <summary>
        /// Rigidbody2D 이동 시 MovePosition을 사용할지 여부입니다.
        /// </summary>
        public bool UseMovePosition => useMovePosition;

        /// <summary>
        /// 이동 시작 시점의 Forward 방향을 고정하여 사용할지 여부입니다.
        /// </summary>
        public bool UseSnapshotForward => useSnapshotForward;

        /// <summary>
        /// 동일 채널에서 실행 중인 기존 모션을 덮어쓸 수 있는지 여부입니다.
        /// </summary>
        public bool AllowReplace => allowReplace;
    }
}