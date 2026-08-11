using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>스킬 카메라 이동 이벤트의 실행 방식을 정의합니다.</summary>
    public enum SkillCameraMoveMode
    {
        /// <summary>선택한 대상 위치로 임시 카메라 포커스를 이동합니다.</summary>
        MoveTo = 0,

        /// <summary>이 스킬 실행기가 소유한 포커스를 기본 게임플레이 Follow로 복귀시킵니다.</summary>
        Restore = 1,
    }

    /// <summary>카메라 이동 기준으로 사용할 스킬 대상을 정의합니다.</summary>
    public enum SkillCameraMoveTargetSource
    {
        /// <summary>현재 스킬을 실행한 캐스터를 사용합니다.</summary>
        Caster = 0,

        /// <summary>현재 스킬 컨텍스트의 고정 타겟을 사용합니다.</summary>
        Target = 1,
    }

    /// <summary>카메라 이동 대상을 찾지 못했을 때의 처리 방식을 정의합니다.</summary>
    public enum SkillCameraMoveMissingTargetPolicy
    {
        /// <summary>로그 없이 이벤트를 무시합니다.</summary>
        Ignore = 0,

        /// <summary>경고 로그를 출력하고 이벤트를 무시합니다.</summary>
        Warn = 1,

        /// <summary>Target이 없으면 Caster를 대신 사용합니다.</summary>
        FallbackToCaster = 2,
    }

    /// <summary>
    /// 스킬 타임라인에서 Caster 또는 Target 기준 카메라 이동을 실행하기 위한 이벤트 정의입니다.
    /// </summary>
    public sealed class SkillCameraMoveEventDefinition : ScriptableObject
    {
        [Header("Move")]
        [Tooltip("카메라 포커스를 이동하거나 기본 게임플레이 Follow로 복귀합니다.")]
        public SkillCameraMoveMode mode = SkillCameraMoveMode.MoveTo;

        [Tooltip("카메라 이동 기준으로 사용할 대상을 선택합니다.")]
        public SkillCameraMoveTargetSource targetSource = SkillCameraMoveTargetSource.Caster;

        [Tooltip("대상의 현재 위치를 계속 추적하거나 이벤트 시작 시점 위치를 사용합니다.")]
        public CameraFocusTrackingMode trackingMode = CameraFocusTrackingMode.FollowTarget;

        [Tooltip("대상과 맵 기본 Follow Offset에 추가할 월드 좌표 보정값입니다.")]
        public Vector2 offset = Vector2.zero;

        [Tooltip("선택한 대상을 찾지 못했을 때의 처리 정책입니다.")]
        public SkillCameraMoveMissingTargetPolicy missingTargetPolicy = SkillCameraMoveMissingTargetPolicy.Warn;

        [Header("Timing")]
        [Tooltip("켜면 Timeline Clip 길이를 카메라 이동 시간으로 사용합니다.")]
        public bool useClipDuration = true;

        [Tooltip("useClipDuration이 꺼져 있을 때 사용할 이동 시간(초)입니다.")]
        [Min(0f)] public float durationOverrideSeconds;

        [Tooltip("카메라 위치 보간에 사용할 Easing 타입입니다.")]
        public Easing.EaseType easing = Easing.EaseType.EaseOutQuad;

        [Tooltip("Time.timeScale과 무관하게 카메라 이동을 진행할지 여부입니다.")]
        public bool useUnscaledTime;

        [Tooltip("임시 카메라 포커스에 맵 경계를 적용할지 여부입니다.")]
        public bool respectMapBounds = true;

        [Header("End Policy")]
        [Tooltip("스킬 정상 종료 시 기본 게임플레이 Follow로 복귀할지 여부입니다.")]
        public bool restoreOnSkillEnd = true;

        [Tooltip("스킬 취소 시 기본 게임플레이 Follow로 복귀할지 여부입니다.")]
        public bool restoreOnCancel = true;

        [Header("Conflict")]
        [Tooltip("다른 시스템이 카메라 포커스를 소유하고 있을 때의 처리 정책입니다.")]
        public CameraFocusReplaceMode replaceMode = CameraFocusReplaceMode.ReplaceCurrent;

        /// <summary>
        /// 이벤트 길이와 오버라이드 설정을 기준으로 실제 카메라 이동 시간을 계산합니다.
        /// </summary>
        /// <param name="clipDurationSeconds">Timeline Clip에서 계산된 이벤트 길이(초)입니다.</param>
        /// <returns>런타임에서 사용할 카메라 이동 시간(초)입니다.</returns>
        public float ResolveDuration(float clipDurationSeconds)
        {
            return useClipDuration
                ? Mathf.Max(0f, clipDurationSeconds)
                : Mathf.Max(0f, durationOverrideSeconds);
        }
    }
}
