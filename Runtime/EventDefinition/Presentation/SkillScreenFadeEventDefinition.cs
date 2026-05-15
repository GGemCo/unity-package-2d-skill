using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 타임라인에서 화면 전체 페이드 연출을 실행하기 위한 이벤트 정의입니다.
    /// Bake된 RuntimeSequence의 Payload로 저장되어 <see cref="SkillExecutor"/>에서 실행됩니다.
    /// </summary>
    public sealed class SkillScreenFadeEventDefinition : ScriptableObject
    {
        [Header("Fade")]
        [Tooltip("페이드 색상입니다.")]
        public Color color = Color.black;

        [Tooltip("시작 알파값입니다.")]
        [Range(0f, 1f)] public float fromAlpha = 0f;

        [Tooltip("종료 알파값입니다.")]
        [Range(0f, 1f)] public float toAlpha = 1f;

        [Tooltip("클립 종료 후 마지막 알파 상태를 유지할지 여부입니다.")]
        public bool holdFinalState = false;

        [Header("Timing")]
        [Tooltip("켜면 Timeline Clip 길이를 페이드 지속 시간으로 사용합니다.")]
        public bool useClipDuration = true;

        [Tooltip("useClipDuration이 꺼져 있을 때 사용할 페이드 지속 시간(초)입니다.")]
        [Min(0f)] public float durationOverrideSeconds = 0f;

        [Tooltip("알파 보간에 사용할 Easing 타입입니다.")]
        public Easing.EaseType easing = Easing.EaseType.Linear;

        [Tooltip("Time.timeScale과 무관하게 페이드를 진행할지 여부입니다.")]
        public bool useUnscaledTime = false;

        [Header("End Policy")]
        [Tooltip("스킬이 정상 종료될 때 이 페이드를 강제로 초기화할지 여부입니다.")]
        public bool clearOnSkillEnd = true;

        [Tooltip("스킬이 취소될 때 이 페이드를 강제로 초기화할지 여부입니다.")]
        public bool clearOnCancel = true;

        [Header("Render")]
        [Tooltip("Screen Fade를 어떤 Canvas 계층에 렌더링할지 결정합니다.")]
        public ScreenFadeRenderMode renderMode = ScreenFadeRenderMode.OverlayUi;

        [Tooltip("Canvas 정렬에 사용할 Sorting Layer 이름입니다. 비어 있으면 UI 레이어를 사용합니다.")]
        public string sortingLayerName = nameof(ConfigSortingLayer.Keys.UI);

        [Tooltip("Canvas 정렬 순서입니다.")]
        public int orderInLayer = 5000;

        [Tooltip("Screen Space - Camera Canvas의 Plane Distance 값입니다.")]
        [Min(0.01f)] public float planeDistance = 10f;

        [Header("Conflict")]
        [Tooltip("이미 재생 중인 화면 페이드가 있을 때의 처리 정책입니다.")]
        public ScreenFadeReplaceMode replaceMode = ScreenFadeReplaceMode.ReplaceCurrent;

        /// <summary>
        /// 이벤트 길이와 오버라이드 설정을 기준으로 실제 페이드 지속 시간을 계산합니다.
        /// </summary>
        /// <param name="clipDurationSeconds">Timeline Clip에서 계산된 이벤트 길이(초)입니다.</param>
        /// <returns>런타임에서 사용할 페이드 지속 시간(초)입니다.</returns>
        public float ResolveDuration(float clipDurationSeconds)
        {
            return useClipDuration
                ? Mathf.Max(0f, clipDurationSeconds)
                : Mathf.Max(0f, durationOverrideSeconds);
        }
    }
}
