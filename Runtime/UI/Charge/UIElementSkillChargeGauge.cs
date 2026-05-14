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

        private void Reset()
        {
            root = gameObject;
            progressSlider = GetComponentInChildren<Slider>(true);
        }

        private void Awake()
        {
            if (root == null)
                root = gameObject;
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
