using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 차징 진행도와 차징 내구도 게이지를 표시하는 UI View입니다.
    /// </summary>
    public sealed class UIElementSkillChargeGauge : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("차징 중에만 표시할 루트 오브젝트입니다. 비어 있으면 현재 GameObject를 사용합니다.")]
        [SerializeField] private GameObject root;

        [Header("Progress")]
        [Tooltip("시간 기반 차징 진행도를 표시할 Slider입니다.")]
        [SerializeField] private Slider progressSlider;

        [Tooltip("시간 기반 차징 진행도를 표시할 Image입니다. Slider 대신 사용할 수 있습니다.")]
        [SerializeField] private Image progressFillImage;

        [Header("Gauge")]
        [Tooltip("피격으로 감소하는 차징 내구도 게이지 Slider입니다.")]
        [SerializeField] private Slider gaugeSlider;

        [Tooltip("피격으로 감소하는 차징 내구도 게이지 Image입니다. Slider 대신 사용할 수 있습니다.")]
        [SerializeField] private Image gaugeFillImage;

        [Header("Options")]
        [Tooltip("차징이 비활성화되면 루트 오브젝트를 자동으로 숨깁니다.")]
        [SerializeField] private bool hideWhenInactive = true;

        [Header("Flip")]
        [Tooltip("캐릭터가 좌우 반전되었을 때 위치 오프셋의 X 값을 반전할지 여부입니다.")]
        [SerializeField] private bool useFlipOffset = true;

        [Tooltip("캐릭터가 좌우 반전되었을 때 Slider 시각 루트의 localScale.x 부호를 반전할지 여부입니다.")]
        [SerializeField] private bool useFlipSliderVisual = true;

        [Tooltip("좌우 반전을 적용할 Slider 시각 루트 목록입니다. 비어 있으면 Progress/Gauge Slider 또는 Fill Image를 자동으로 사용합니다.")]
        [SerializeField] private RectTransform[] flipSliderVisualTargets;

        private readonly List<RectTransform> _resolvedFlipTargets = new();
        private readonly Dictionary<RectTransform, Vector3> _defaultFlipTargetScales = new();
        private RectTransform _rectTransform;
        private bool _hasCachedDefaultVisual;
        private bool? _lastAppliedFlip;

        /// <summary>
        /// 차징 게이지 위치 오프셋에 캐릭터 좌우 반전을 적용할지 여부입니다.
        /// </summary>
        public bool UseFlipOffset => useFlipOffset;

        /// <summary>
        /// 차징 게이지 Slider 시각 루트의 localScale.x 부호에 캐릭터 좌우 반전을 적용할지 여부입니다.
        /// </summary>
        public bool UseFlipSliderVisual => useFlipSliderVisual;

        /// <summary>
        /// 차징 게이지 위치 계산에 사용할 RectTransform입니다.
        /// </summary>
        public RectTransform RectTransform
        {
            get
            {
                if (_rectTransform == null)
                    _rectTransform = transform as RectTransform;
                return _rectTransform;
            }
        }

        private void Reset()
        {
            root = gameObject;
            progressSlider = GetComponentInChildren<Slider>(true);
        }

        private void Awake()
        {
            if (root == null)
                root = gameObject;

            CacheDefaultVisualState();
        }

        /// <summary>
        /// 캐릭터 좌우 반전 상태에 맞춰 게이지 시각 루트의 Scale 반전을 적용합니다.
        /// </summary>
        /// <param name="isFlipped">캐릭터가 기본 방향 기준으로 좌우 반전된 상태인지 여부입니다.</param>
        public void ApplyFlipVisual(bool isFlipped)
        {
            CacheDefaultVisualState();

            bool shouldFlip = useFlipSliderVisual && isFlipped;
            if (_lastAppliedFlip.HasValue && _lastAppliedFlip.Value == shouldFlip)
                return;

            ApplyFlipTargetScale(shouldFlip);
            _lastAppliedFlip = shouldFlip;
        }

        /// <summary>
        /// 차징 상태 스냅샷을 기준으로 UI 표시를 갱신합니다.
        /// </summary>
        /// <param name="snapshot">스킬 실행기가 발행한 차징 상태 스냅샷입니다.</param>
        public void Render(in SkillChargeSnapshot snapshot)
        {
            if (hideWhenInactive && root != null)
                root.SetActive(snapshot.IsActive);

            SetProgress(snapshot.Progress01);
            SetGauge(snapshot.GaugeCurrent, snapshot.GaugeMax, snapshot.Gauge01);
        }

        /// <summary>
        /// 차징 UI를 즉시 숨기고 값을 초기화합니다.
        /// </summary>
        public void Hide()
        {
            if (root != null)
                root.SetActive(false);

            SetProgress(0f);
            SetGauge(0f, 1f, 0f);
        }

        /// <summary>
        /// Inspector에 설정된 반전 대상과 기본 Scale 값을 저장합니다.
        /// </summary>
        private void CacheDefaultVisualState()
        {
            if (_hasCachedDefaultVisual)
                return;

            _resolvedFlipTargets.Clear();
            _defaultFlipTargetScales.Clear();

            if (flipSliderVisualTargets != null && flipSliderVisualTargets.Length > 0)
            {
                foreach (RectTransform target in flipSliderVisualTargets)
                    AddFlipTarget(target);
            }
            else
            {
                AddFlipTarget(GetFallbackFlipTarget(progressSlider, progressFillImage));
                AddFlipTarget(GetFallbackFlipTarget(gaugeSlider, gaugeFillImage));
            }

            _hasCachedDefaultVisual = true;
            _lastAppliedFlip = null;
        }

        /// <summary>
        /// 좌우 반전 대상에 기본 Scale 또는 반전 Scale을 적용합니다.
        /// </summary>
        /// <param name="shouldFlip">기본 Scale의 X 부호를 반전할지 여부입니다.</param>
        private void ApplyFlipTargetScale(bool shouldFlip)
        {
            foreach (RectTransform target in _resolvedFlipTargets)
            {
                if (target == null)
                    continue;

                if (!_defaultFlipTargetScales.TryGetValue(target, out Vector3 defaultScale))
                    continue;

                float scaleX = shouldFlip ? -defaultScale.x : defaultScale.x;
                target.localScale = new Vector3(scaleX, defaultScale.y, defaultScale.z);
            }
        }

        /// <summary>
        /// 명시적 반전 대상이 없을 때 사용할 기본 반전 대상을 반환합니다.
        /// </summary>
        /// <param name="slider">우선 사용할 Slider입니다.</param>
        /// <param name="fillImage">Slider가 없을 때 사용할 Fill Image입니다.</param>
        /// <returns>반전 대상으로 사용할 RectTransform입니다.</returns>
        private static RectTransform GetFallbackFlipTarget(Slider slider, Image fillImage)
        {
            if (slider != null)
                return slider.transform as RectTransform;

            if (fillImage != null)
                return fillImage.rectTransform;

            return null;
        }

        /// <summary>
        /// 좌우 반전을 적용할 RectTransform과 기본 Scale 값을 등록합니다.
        /// </summary>
        /// <param name="target">좌우 반전을 적용할 RectTransform입니다.</param>
        private void AddFlipTarget(RectTransform target)
        {
            if (target == null)
                return;

            if (_defaultFlipTargetScales.ContainsKey(target))
                return;

            _resolvedFlipTargets.Add(target);
            _defaultFlipTargetScales[target] = target.localScale;
        }

        /// <summary>
        /// 차징 시간 진행도를 UI에 적용합니다.
        /// </summary>
        /// <param name="normalizedValue">0~1 범위의 차징 시간 진행도입니다.</param>
        private void SetProgress(float normalizedValue)
        {
            float value = Mathf.Clamp01(normalizedValue);

            if (progressSlider != null)
            {
                progressSlider.minValue = 0f;
                progressSlider.maxValue = 1f;
                progressSlider.value = value;
            }

            if (progressFillImage != null)
                progressFillImage.fillAmount = value;
        }

        /// <summary>
        /// 차징 내구도 게이지 값을 UI에 적용합니다.
        /// </summary>
        /// <param name="currentValue">현재 차징 내구도 값입니다.</param>
        /// <param name="maxValue">최대 차징 내구도 값입니다.</param>
        /// <param name="normalizedValue">0~1 범위의 차징 내구도 비율입니다.</param>
        private void SetGauge(float currentValue, float maxValue, float normalizedValue)
        {
            float safeMax = Mathf.Max(1f, maxValue);
            float current = Mathf.Clamp(currentValue, 0f, safeMax);
            float normalized = Mathf.Clamp01(normalizedValue);

            if (gaugeSlider != null)
            {
                gaugeSlider.minValue = 0f;
                gaugeSlider.maxValue = safeMax;
                gaugeSlider.value = current;
            }

            if (gaugeFillImage != null)
                gaugeFillImage.fillAmount = normalized;
        }
    }
}
