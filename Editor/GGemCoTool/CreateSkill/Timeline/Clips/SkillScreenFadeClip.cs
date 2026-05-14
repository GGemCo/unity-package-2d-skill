using System;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 실행 중 화면 전체 페이드 연출을 수행하는 Timeline 이벤트 클립입니다.
    /// </summary>
    /// <remarks>
    /// 이 클립은 Bake 과정에서 <c>SkillScreenFadeEventDefinition</c> Payload로 변환되며,
    /// 런타임에서는 Core의 <c>ScreenFadeRuntimeService</c>를 통해 화면 전체 페이드를 실행합니다.
    /// </remarks>
    [Serializable]
    public sealed class SkillScreenFadeClip : SkillEventClipBase
    {
        [Header("Fade")]
        [Tooltip("페이드 색상입니다.")]
        [SerializeField] private Color color = Color.black;

        [Tooltip("시작 알파값입니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float fromAlpha = 0f;

        [Tooltip("종료 알파값입니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float toAlpha = 1f;

        [Tooltip("클립 완료 후 마지막 알파 상태를 유지할지 여부입니다.")]
        [SerializeField] private bool holdFinalState = false;

        [Header("Timing")]
        [Tooltip("켜면 Timeline Clip 길이를 페이드 지속 시간으로 사용합니다.")]
        [SerializeField] private bool useClipDuration = true;

        [Tooltip("useClipDuration이 꺼져 있을 때 사용할 페이드 지속 시간(초)입니다.")]
        [Min(0f)]
        [SerializeField] private float durationOverrideSeconds = 0f;

        [Tooltip("알파 보간에 사용할 Easing 타입입니다.")]
        [SerializeField] private Easing.EaseType easing = GGemCo2DCore.Easing.EaseType.Linear;

        [Tooltip("Time.timeScale과 무관하게 페이드를 진행할지 여부입니다.")]
        [SerializeField] private bool useUnscaledTime = false;

        [Header("End Policy")]
        [Tooltip("스킬이 정상 종료될 때 이 페이드를 강제로 초기화할지 여부입니다.")]
        [SerializeField] private bool clearOnSkillEnd = true;

        [Tooltip("스킬이 취소될 때 이 페이드를 강제로 초기화할지 여부입니다.")]
        [SerializeField] private bool clearOnCancel = true;

        [Header("Render")]
        [Tooltip("Screen Fade를 어떤 Canvas 계층에 렌더링할지 결정합니다.")]
        [SerializeField] private ScreenFadeRenderMode renderMode = ScreenFadeRenderMode.OverlayUi;

        [Tooltip("Canvas 정렬에 사용할 Sorting Layer 이름입니다. 비어 있으면 UI 레이어를 사용합니다.")]
        [SerializeField] private string sortingLayerName = nameof(ConfigSortingLayer.Keys.UI);

        [Tooltip("Canvas 정렬 순서입니다.")]
        [SerializeField] private int orderInLayer = 5000;

        [Tooltip("Screen Space - Camera Canvas의 Plane Distance 값입니다.")]
        [Min(0.01f)]
        [SerializeField] private float planeDistance = 10f;

        [Header("Conflict")]
        [Tooltip("이미 재생 중인 화면 페이드가 있을 때의 처리 정책입니다.")]
        [SerializeField] private ScreenFadeReplaceMode replaceMode = ScreenFadeReplaceMode.ReplaceCurrent;

        /// <summary>
        /// 이 클립이 생성하는 스킬 이벤트 유형입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.ScreenFade;

        /// <summary>
        /// 페이드 색상을 반환합니다.
        /// </summary>
        public Color Color => color;

        /// <summary>
        /// 시작 알파값을 반환합니다.
        /// </summary>
        public float FromAlpha => fromAlpha;

        /// <summary>
        /// 종료 알파값을 반환합니다.
        /// </summary>
        public float ToAlpha => toAlpha;

        /// <summary>
        /// 완료 후 마지막 상태 유지 여부를 반환합니다.
        /// </summary>
        public bool HoldFinalState => holdFinalState;

        /// <summary>
        /// Timeline Clip 길이를 지속 시간으로 사용할지 여부를 반환합니다.
        /// </summary>
        public bool UseClipDuration => useClipDuration;

        /// <summary>
        /// 지속 시간 오버라이드 값을 반환합니다.
        /// </summary>
        public float DurationOverrideSeconds => durationOverrideSeconds;

        /// <summary>
        /// 알파 보간 Easing 타입을 반환합니다.
        /// </summary>
        public Easing.EaseType Easing => easing;

        /// <summary>
        /// Unscaled Time 사용 여부를 반환합니다.
        /// </summary>
        public bool UseUnscaledTime => useUnscaledTime;

        /// <summary>
        /// 스킬 정상 종료 시 초기화 여부를 반환합니다.
        /// </summary>
        public bool ClearOnSkillEnd => clearOnSkillEnd;

        /// <summary>
        /// 스킬 취소 시 초기화 여부를 반환합니다.
        /// </summary>
        public bool ClearOnCancel => clearOnCancel;

        /// <summary>
        /// 화면 페이드 렌더 모드를 반환합니다.
        /// </summary>
        public ScreenFadeRenderMode RenderMode => renderMode;

        /// <summary>
        /// Sorting Layer 이름을 반환합니다.
        /// </summary>
        public string SortingLayerName => sortingLayerName;

        /// <summary>
        /// Canvas 정렬 순서를 반환합니다.
        /// </summary>
        public int OrderInLayer => orderInLayer;

        /// <summary>
        /// Screen Space - Camera Plane Distance 값을 반환합니다.
        /// </summary>
        public float PlaneDistance => planeDistance;

        /// <summary>
        /// 화면 페이드 교체 정책을 반환합니다.
        /// </summary>
        public ScreenFadeReplaceMode ReplaceMode => replaceMode;
    }
}
