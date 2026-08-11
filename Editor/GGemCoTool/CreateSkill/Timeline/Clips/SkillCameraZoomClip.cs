using System;
using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 실행 중 카메라 Orthographic Size를 변경하거나 기존 크기로 복귀시키는 Timeline 이벤트 클립입니다.
    /// </summary>
    [Serializable]
    public sealed class SkillCameraZoomClip : SkillEventClipBase
    {
        [Header("Zoom")]
        [Tooltip("카메라 줌 적용 또는 기존 크기 복귀 방식을 선택합니다.")]
        [SerializeField] private SkillCameraZoomMode mode = SkillCameraZoomMode.ZoomTo;

        [Tooltip("ZoomTo 모드의 목표 Orthographic Size입니다. 값이 작을수록 더 확대됩니다.")]
        [Min(0.0001f)]
        [SerializeField] private float targetOrthographicSize = 5f;

        [Header("Timing")]
        [Tooltip("켜면 Timeline Clip 길이를 줌 보간 시간으로 사용합니다.")]
        [SerializeField] private bool useClipDuration = true;

        [Tooltip("useClipDuration이 꺼져 있을 때 사용할 줌 보간 시간(초)입니다.")]
        [Min(0f)]
        [SerializeField] private float durationOverrideSeconds;

        [Tooltip("카메라 크기 보간에 사용할 Easing 타입입니다.")]
        [SerializeField] private Easing.EaseType easing = GGemCo2DCore.Easing.EaseType.EaseOutQuad;

        [Tooltip("Time.timeScale과 무관하게 카메라 줌을 진행할지 여부입니다.")]
        [SerializeField] private bool useUnscaledTime;

        [Header("End Policy")]
        [Tooltip("스킬이 정상 종료될 때 줌 요청 전 카메라 크기로 복귀할지 여부입니다.")]
        [SerializeField] private bool restoreOnSkillEnd = true;

        [Tooltip("스킬이 취소될 때 줌 요청 전 카메라 크기로 복귀할지 여부입니다.")]
        [SerializeField] private bool restoreOnCancel = true;

        [Header("Conflict")]
        [Tooltip("다른 시스템이 카메라 줌을 소유하고 있을 때의 처리 정책입니다.")]
        [SerializeField] private CameraZoomReplaceMode replaceMode = CameraZoomReplaceMode.ReplaceCurrent;

        /// <summary>
        /// 이 클립이 표현하는 스킬 이벤트 타입입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Camera;

        public SkillCameraZoomMode Mode => mode;
        public float TargetOrthographicSize => targetOrthographicSize;
        public bool UseClipDuration => useClipDuration;
        public float DurationOverrideSeconds => durationOverrideSeconds;
        public Easing.EaseType Easing => easing;
        public bool UseUnscaledTime => useUnscaledTime;
        public bool RestoreOnSkillEnd => restoreOnSkillEnd;
        public bool RestoreOnCancel => restoreOnCancel;
        public CameraZoomReplaceMode ReplaceMode => replaceMode;
    }
}
