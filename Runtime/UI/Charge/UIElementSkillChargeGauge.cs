using GGemCo2DCore;
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

        [Tooltip("캐릭터가 좌우 반전되었을 때 Slider 또는 Filled Image의 진행 방향을 반전할지 여부입니다.")]
        [SerializeField] private bool useFlipSliderVisual = true;

        private RectTransform _rectTransform;
        private Slider.Direction _defaultProgressSliderDirection;
        private Slider.Direction _defaultGaugeSliderDirection;
        private int _defaultProgressFillOrigin;
        private int _defaultGaugeFillOrigin;
        private bool _hasCachedDefaultVisual;
        private bool _lastAppliedFlip;

        /// <summary>
        /// 차징 게이지 위치 오프셋에 캐릭터 좌우 반전을 적용할지 여부입니다.
        /// </summary>
        public bool UseFlipOffset => useFlipOffset;

        /// <summary>
        /// 차징 게이지 Slider 또는 Filled Image의 진행 방향에 캐릭터 좌우 반전을 적용할지 여부입니다.
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
        /// 캐릭터 좌우 반전 상태에 맞춰 게이지 시각 방향을 적용합니다.
        /// </summary>
        /// <param name="isFlipped">캐릭터가 기본 방향 기준으로 좌우 반전된 상태인지 여부입니다.</param>
        public void ApplyFlipVisual(bool isFlipped)
        {
            CacheDefaultVisualState();

            bool shouldFlip = useFlipSliderVisual && isFlipped;
            if (_lastAppliedFlip == shouldFlip)
                return;

            ApplySliderDirection(progressSlider, _defaultProgressSliderDirection, shouldFlip);
            ApplySliderDirection(gaugeSlider, _defaultGaugeSliderDirection, shouldFlip);
            ApplyImageFillOrigin(progressFillImage, _defaultProgressFillOrigin, shouldFlip);
            ApplyImageFillOrigin(gaugeFillImage, _defaultGaugeFillOrigin, shouldFlip);

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
        /// Inspector에 설정된 기본 Slider 방향과 Filled Image 방향을 저장합니다.
        /// </summary>
        private void CacheDefaultVisualState()
        {
            if (_hasCachedDefaultVisual)
                return;

            _defaultProgressSliderDirection = progressSlider != null ? progressSlider.direction : Slider.Direction.LeftToRight;
            _defaultGaugeSliderDirection = gaugeSlider != null ? gaugeSlider.direction : Slider.Direction.LeftToRight;
            _defaultProgressFillOrigin = progressFillImage != null ? progressFillImage.fillOrigin : 0;
            _defaultGaugeFillOrigin = gaugeFillImage != null ? gaugeFillImage.fillOrigin : 0;
            _hasCachedDefaultVisual = true;
            _lastAppliedFlip = useFlipSliderVisual && false;
        }

        /// <summary>
        /// 기준 방향과 반전 여부에 맞춰 Slider 진행 방향을 적용합니다.
        /// </summary>
        /// <param name="slider">방향을 적용할 Slider입니다.</param>
        /// <param name="defaultDirection">Prefab에 설정된 기본 Slider 방향입니다.</param>
        /// <param name="shouldFlip">좌우 진행 방향을 반전할지 여부입니다.</param>
        private static void ApplySliderDirection(Slider slider, Slider.Direction defaultDirection, bool shouldFlip)
        {
            if (slider == null)
                return;

            slider.direction = shouldFlip ? GetFlippedDirection(defaultDirection) : defaultDirection;
        }

        /// <summary>
        /// 기준 방향과 반전 여부에 맞춰 Filled Image의 수평 Fill 기준점을 적용합니다.
        /// </summary>
        /// <param name="image">Fill 기준점을 적용할 Image입니다.</param>
        /// <param name="defaultFillOrigin">Prefab에 설정된 기본 Fill Origin 값입니다.</param>
        /// <param name="shouldFlip">수평 Fill 기준점을 반전할지 여부입니다.</param>
        private static void ApplyImageFillOrigin(Image image, int defaultFillOrigin, bool shouldFlip)
        {
            if (image == null)
                return;

            if (image.type != Image.Type.Filled || image.fillMethod != Image.FillMethod.Horizontal)
                return;

            image.fillOrigin = shouldFlip ? GetFlippedHorizontalFillOrigin(defaultFillOrigin) : defaultFillOrigin;
        }

        /// <summary>
        /// 좌우 Slider 진행 방향을 반전한 값을 반환합니다.
        /// </summary>
        /// <param name="direction">Prefab에 설정된 기본 Slider 방향입니다.</param>
        /// <returns>좌우 방향만 반전한 Slider 방향입니다.</returns>
        private static Slider.Direction GetFlippedDirection(Slider.Direction direction)
        {
            return direction switch
            {
                Slider.Direction.LeftToRight => Slider.Direction.RightToLeft,
                Slider.Direction.RightToLeft => Slider.Direction.LeftToRight,
                _ => direction
            };
        }

        /// <summary>
        /// Filled Image의 수평 Fill 기준점을 반전한 값을 반환합니다.
        /// </summary>
        /// <param name="origin">기본 수평 Fill Origin 값입니다. 0은 Left, 1은 Right입니다.</param>
        /// <returns>반전된 수평 Fill Origin 값입니다.</returns>
        private static int GetFlippedHorizontalFillOrigin(int origin)
        {
            return origin == 0 ? 1 : 0;
        }

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
