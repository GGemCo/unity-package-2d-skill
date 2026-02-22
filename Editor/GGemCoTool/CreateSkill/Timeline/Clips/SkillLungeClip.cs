using System;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 전진(러시/대시/회피) 이벤트 클립(Authoring).
    /// - Bake 시 <see cref="GGemCo2DSkill.LungeEventDefinition"/> Payload 로 변환된다.
    /// - Speed가 아니라 "거리(Distance)" 기반으로 설계한다.
    /// - Arc 옵션을 사용하면 점프형 회피처럼 수직 오프셋을 추가할 수 있다.
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

        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Lunge;

        public float Distance => distance;
        public float DurationOverrideSeconds => durationOverrideSeconds;
        public Easing.EaseType Easing => easing;

        public bool InvertForward => invertForward;

        public bool UseArcMotion => useArcMotion;
        public float ArcHeight => arcHeight;

        public bool StopAtEnd => stopAtEnd;
        public bool UseMovePosition => useMovePosition;
        public bool UseSnapshotForward => useSnapshotForward;

        public bool AllowReplace => allowReplace;
    }
}