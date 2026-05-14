using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// <see cref="SkillExecutor"/>의 차징 상태 이벤트를 UI 게이지 View에 전달하는 Presenter입니다.
    /// </summary>
    public sealed class SkillChargeGaugePresenter : MonoBehaviour
    {
        [Tooltip("차징 상태를 발행하는 SkillExecutor입니다. 비어 있으면 부모/자식에서 자동 탐색합니다.")]
        [SerializeField] private SkillExecutor skillExecutor;

        [Tooltip("차징 게이지를 표시할 UI View입니다. 비어 있으면 자식에서 자동 탐색합니다.")]
        [SerializeField] private UIElementSkillChargeGauge gaugeView;

        private void Reset()
        {
            skillExecutor = GetComponentInParent<SkillExecutor>();
            gaugeView = GetComponentInChildren<UIElementSkillChargeGauge>(true);
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();

            if (skillExecutor != null)
                skillExecutor.ChargeStateChanged += HandleChargeStateChanged;

            gaugeView?.Hide();
        }

        private void OnDisable()
        {
            if (skillExecutor != null)
                skillExecutor.ChargeStateChanged -= HandleChargeStateChanged;
        }

        /// <summary>
        /// 외부 초기화 코드에서 사용할 실행기와 View를 명시적으로 연결합니다.
        /// </summary>
        /// <param name="executor">차징 상태를 발행할 스킬 실행기입니다.</param>
        /// <param name="view">차징 상태를 표시할 UI View입니다.</param>
        public void Bind(SkillExecutor executor, UIElementSkillChargeGauge view)
        {
            if (skillExecutor != null)
                skillExecutor.ChargeStateChanged -= HandleChargeStateChanged;

            skillExecutor = executor;
            gaugeView = view;

            if (isActiveAndEnabled && skillExecutor != null)
                skillExecutor.ChargeStateChanged += HandleChargeStateChanged;

            gaugeView?.Hide();
        }

        private void EnsureReferences()
        {
            if (skillExecutor == null)
                skillExecutor = GetComponentInParent<SkillExecutor>();
            if (skillExecutor == null)
                skillExecutor = GetComponentInChildren<SkillExecutor>(true);

            if (gaugeView == null)
                gaugeView = GetComponentInChildren<UIElementSkillChargeGauge>(true);
        }

        private void HandleChargeStateChanged(SkillChargeSnapshot snapshot)
        {
            if (gaugeView == null)
                return;

            if (snapshot.IsActive)
                gaugeView.Render(in snapshot);
            else
                gaugeView.Hide();
        }
    }
}
