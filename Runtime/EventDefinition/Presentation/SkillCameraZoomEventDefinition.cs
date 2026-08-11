using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 카메라 줌 이벤트의 실행 방식을 정의합니다.
    /// </summary>
    public enum SkillCameraZoomMode
    {
        /// <summary>
        /// 현재 카메라 크기에서 지정한 Orthographic Size로 보간합니다.
        /// </summary>
        ZoomTo = 0,

        /// <summary>
        /// 같은 스킬 실행기가 처음 줌을 요청하기 직전의 카메라 크기로 복귀합니다.
        /// </summary>
        Restore = 1,
    }

    /// <summary>
    /// 스킬 타임라인에서 카메라 줌 인·아웃 연출을 실행하기 위한 이벤트 정의입니다.
    /// Bake된 RuntimeSequence의 Payload로 저장되어 <see cref="SkillExecutor"/>에서 실행됩니다.
    /// </summary>
    public sealed class SkillCameraZoomEventDefinition : ScriptableObject
    {
        [Header("Zoom")]
        [Tooltip("카메라 줌 적용 또는 기존 크기 복귀 방식을 선택합니다.")]
        public SkillCameraZoomMode mode = SkillCameraZoomMode.ZoomTo;

        [Tooltip("ZoomTo 모드의 목표 Orthographic Size입니다. 값이 작을수록 더 확대됩니다.")]
        [Min(0.0001f)] public float targetOrthographicSize = 5f;

        [Header("Timing")]
        [Tooltip("켜면 Timeline Clip 길이를 줌 보간 시간으로 사용합니다.")]
        public bool useClipDuration = true;

        [Tooltip("useClipDuration이 꺼져 있을 때 사용할 줌 보간 시간(초)입니다.")]
        [Min(0f)] public float durationOverrideSeconds;

        [Tooltip("카메라 크기 보간에 사용할 Easing 타입입니다.")]
        public Easing.EaseType easing = Easing.EaseType.EaseOutQuad;

        [Tooltip("Time.timeScale과 무관하게 카메라 줌을 진행할지 여부입니다.")]
        public bool useUnscaledTime;

        [Header("End Policy")]
        [Tooltip("스킬이 정상 종료될 때 줌 요청 전 카메라 크기로 복귀할지 여부입니다.")]
        public bool restoreOnSkillEnd = true;

        [Tooltip("스킬이 취소될 때 줌 요청 전 카메라 크기로 복귀할지 여부입니다.")]
        public bool restoreOnCancel = true;

        [Header("Conflict")]
        [Tooltip("다른 시스템이 카메라 줌을 소유하고 있을 때의 처리 정책입니다.")]
        public CameraZoomReplaceMode replaceMode = CameraZoomReplaceMode.ReplaceCurrent;

        /// <summary>
        /// 이벤트 길이와 오버라이드 설정을 기준으로 실제 줌 보간 시간을 계산합니다.
        /// </summary>
        /// <param name="clipDurationSeconds">Timeline Clip에서 계산된 이벤트 길이(초)입니다.</param>
        /// <returns>런타임에서 사용할 줌 보간 시간(초)입니다.</returns>
        public float ResolveDuration(float clipDurationSeconds)
        {
            return useClipDuration
                ? Mathf.Max(0f, clipDurationSeconds)
                : Mathf.Max(0f, durationOverrideSeconds);
        }
    }
}
