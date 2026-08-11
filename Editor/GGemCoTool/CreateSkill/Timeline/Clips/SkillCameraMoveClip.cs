using System;
using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 실행 중 Caster 또는 Target 기준으로 카메라를 이동시키는 Timeline 이벤트 클립입니다.
    /// </summary>
    [Serializable]
    public sealed class SkillCameraMoveClip : SkillEventClipBase
    {
        [Header("Move")]
        [Tooltip("카메라 포커스를 이동하거나 기본 게임플레이 Follow로 복귀합니다.")]
        [SerializeField] private SkillCameraMoveMode mode = SkillCameraMoveMode.MoveTo;

        [Tooltip("카메라 이동 기준으로 사용할 대상을 선택합니다.")]
        [SerializeField] private SkillCameraMoveTargetSource targetSource = SkillCameraMoveTargetSource.Caster;

        [Tooltip("대상의 현재 위치를 계속 추적하거나 이벤트 시작 시점 위치를 사용합니다.")]
        [SerializeField] private CameraFocusTrackingMode trackingMode = CameraFocusTrackingMode.FollowTarget;

        [Tooltip("대상과 맵 기본 Follow Offset에 추가할 월드 좌표 보정값입니다.")]
        [SerializeField] private Vector2 offset = Vector2.zero;

        [Tooltip("선택한 대상을 찾지 못했을 때의 처리 정책입니다.")]
        [SerializeField] private SkillCameraMoveMissingTargetPolicy missingTargetPolicy =
            SkillCameraMoveMissingTargetPolicy.Warn;

        [Header("Timing")]
        [Tooltip("켜면 Timeline Clip 길이를 카메라 이동 시간으로 사용합니다.")]
        [SerializeField] private bool useClipDuration = true;

        [Tooltip("useClipDuration이 꺼져 있을 때 사용할 이동 시간(초)입니다.")]
        [Min(0f)] [SerializeField] private float durationOverrideSeconds;

        [Tooltip("카메라 위치 보간에 사용할 Easing 타입입니다.")]
        [SerializeField] private Easing.EaseType easing = GGemCo2DCore.Easing.EaseType.EaseOutQuad;

        [Tooltip("Time.timeScale과 무관하게 카메라 이동을 진행할지 여부입니다.")]
        [SerializeField] private bool useUnscaledTime;

        [Tooltip("임시 카메라 포커스에 맵 경계를 적용할지 여부입니다.")]
        [SerializeField] private bool respectMapBounds = true;

        [Header("End Policy")]
        [Tooltip("스킬 정상 종료 시 기본 게임플레이 Follow로 복귀할지 여부입니다.")]
        [SerializeField] private bool restoreOnSkillEnd = true;

        [Tooltip("스킬 취소 시 기본 게임플레이 Follow로 복귀할지 여부입니다.")]
        [SerializeField] private bool restoreOnCancel = true;

        [Header("Conflict")]
        [Tooltip("다른 시스템이 카메라 포커스를 소유하고 있을 때의 처리 정책입니다.")]
        [SerializeField] private CameraFocusReplaceMode replaceMode = CameraFocusReplaceMode.ReplaceCurrent;

        /// <summary>이 클립이 표현하는 스킬 이벤트 타입입니다.</summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Camera;

        public SkillCameraMoveMode Mode => mode;
        public SkillCameraMoveTargetSource TargetSource => targetSource;
        public CameraFocusTrackingMode TrackingMode => trackingMode;
        public Vector2 Offset => offset;
        public SkillCameraMoveMissingTargetPolicy MissingTargetPolicy => missingTargetPolicy;
        public bool UseClipDuration => useClipDuration;
        public float DurationOverrideSeconds => durationOverrideSeconds;
        public Easing.EaseType Easing => easing;
        public bool UseUnscaledTime => useUnscaledTime;
        public bool RespectMapBounds => respectMapBounds;
        public bool RestoreOnSkillEnd => restoreOnSkillEnd;
        public bool RestoreOnCancel => restoreOnCancel;
        public CameraFocusReplaceMode ReplaceMode => replaceMode;
    }
}
