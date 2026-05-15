using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// <see cref="SkillExecutor"/>의 차징 상태 이벤트를 UI 게이지 View에 전달하고,
    /// 필요 시 차징 게이지 Prefab을 캐릭터 하단 Canvas에 생성하는 Presenter입니다.
    /// </summary>
    public sealed class SkillChargeGaugePresenter : MonoBehaviour
    {
        [Header("Binding")]
        [Tooltip("차징 상태를 발행하는 SkillExecutor입니다. 비어 있으면 부모/자식에서 자동 탐색합니다.")]
        [SerializeField] private SkillExecutor skillExecutor;

        [Tooltip("차징 게이지를 표시할 UI View입니다. 비어 있으면 자식에서 자동 탐색하거나 설정 Prefab에서 생성합니다.")]
        [SerializeField] private UIElementSkillChargeGauge gaugeView;

        [Header("Prefab Override")]
        [Tooltip("설정 파일보다 우선 사용할 차징 게이지 Prefab입니다. 비어 있으면 GGemCoSkillSettings.skillChargeGaugePrefab을 사용합니다.")]
        [SerializeField] private UIElementSkillChargeGauge gaugePrefabOverride;

        [Tooltip("설정 파일보다 우선 사용할 월드 오프셋입니다.")]
        [SerializeField] private Vector3 worldOffsetOverride = new(0f, -0.35f, 0f);

        [Tooltip("설정 파일보다 우선 사용할 Canvas 픽셀 오프셋입니다.")]
        [SerializeField] private Vector2 screenOffsetOverride = Vector2.zero;

        [Tooltip("Prefab Override 위치 값을 사용할지 여부입니다. 꺼져 있으면 GGemCoSkillSettings 값을 사용합니다.")]
        [SerializeField] private bool useLocalOffsetOverride;

        private CharacterBase _character;
        private SkillChargeGaugeWorldFollower _worldFollower;
        private bool _ownsGaugeView;
        private bool _isGaugeActive;
        private SkillExecutor _subscribedExecutor;

        private void Reset()
        {
            skillExecutor = GetComponentInParent<SkillExecutor>();
            gaugeView = GetComponentInChildren<UIElementSkillChargeGauge>(true);
            _character = GetComponentInParent<CharacterBase>();
        }

        private void Awake()
        {
            EnsureReferences();
        }

        private void OnEnable()
        {
            EnsureReferences();
            Subscribe();
            gaugeView?.Hide();
            _isGaugeActive = false;
        }

        private void OnDisable()
        {
            Unsubscribe();
            HideGauge();
        }

        private void OnDestroy()
        {
            ReleaseGaugeView();
        }

        private void LateUpdate()
        {
            if (!_isGaugeActive || _worldFollower == null)
                return;

            _worldFollower.RefreshPosition();
        }

        /// <summary>
        /// 외부 초기화 코드에서 사용할 캐릭터와 실행기를 명시적으로 연결합니다.
        /// </summary>
        /// <param name="character">게이지 위치 추적 기준이 되는 캐릭터입니다.</param>
        /// <param name="executor">차징 상태를 발행할 스킬 실행기입니다.</param>
        public void Initialize(CharacterBase character, SkillExecutor executor)
        {
            _character = character;
            Bind(executor, gaugeView);
        }

        /// <summary>
        /// 외부 초기화 코드에서 사용할 실행기와 View를 명시적으로 연결합니다.
        /// </summary>
        /// <param name="executor">차징 상태를 발행할 스킬 실행기입니다.</param>
        /// <param name="view">차징 상태를 표시할 UI View입니다.</param>
        public void Bind(SkillExecutor executor, UIElementSkillChargeGauge view)
        {
            Unsubscribe();

            skillExecutor = executor;
            if (view != null)
            {
                gaugeView = view;
                _ownsGaugeView = false;
                ConfigureFollower();
            }

            if (isActiveAndEnabled)
                Subscribe();

            gaugeView?.Hide();
            _isGaugeActive = false;
        }

        /// <summary>
        /// Presenter가 생성한 차징 게이지 Prefab 인스턴스를 정리합니다.
        /// </summary>
        public void ReleaseGaugeView()
        {
            _isGaugeActive = false;
            _worldFollower = null;

            if (_ownsGaugeView && gaugeView != null)
            {
                Destroy(gaugeView.gameObject);
            }

            if (_ownsGaugeView)
                gaugeView = null;

            _ownsGaugeView = false;
        }

        /// <summary>
        /// 실행기, View, 캐릭터 참조를 자동으로 탐색합니다.
        /// </summary>
        private void EnsureReferences()
        {
            if (_character == null)
                _character = GetComponentInParent<CharacterBase>();
            if (_character == null)
                _character = GetComponentInChildren<CharacterBase>(true);

            if (skillExecutor == null)
                skillExecutor = GetComponentInParent<SkillExecutor>();
            if (skillExecutor == null)
                skillExecutor = GetComponentInChildren<SkillExecutor>(true);

            if (gaugeView == null)
                gaugeView = GetComponentInChildren<UIElementSkillChargeGauge>(true);

            ConfigureFollower();
        }

        /// <summary>
        /// SkillExecutor의 차징 상태 변경 이벤트를 구독합니다.
        /// </summary>
        private void Subscribe()
        {
            if (_subscribedExecutor == skillExecutor)
                return;

            Unsubscribe();

            if (skillExecutor == null)
                return;

            skillExecutor.ChargeStateChanged += HandleChargeStateChanged;
            _subscribedExecutor = skillExecutor;
        }

        /// <summary>
        /// SkillExecutor의 차징 상태 변경 이벤트 구독을 해제합니다.
        /// </summary>
        private void Unsubscribe()
        {
            if (_subscribedExecutor == null)
                return;

            _subscribedExecutor.ChargeStateChanged -= HandleChargeStateChanged;
            _subscribedExecutor = null;
        }

        /// <summary>
        /// 차징 상태 스냅샷을 View에 반영하고, 필요한 경우 Prefab 인스턴스를 생성합니다.
        /// </summary>
        /// <param name="snapshot">스킬 실행기가 발행한 차징 상태 스냅샷입니다.</param>
        private void HandleChargeStateChanged(SkillChargeSnapshot snapshot)
        {
            if (!ShouldShow(snapshot))
            {
                HideGauge();
                return;
            }

            if (!EnsureGaugeView())
                return;

            gaugeView.Render(in snapshot);
            _isGaugeActive = true;
            _worldFollower?.RefreshPosition();
        }

        /// <summary>
        /// 현재 설정과 캐릭터 타입을 기준으로 차징 게이지를 표시할지 판단합니다.
        /// </summary>
        /// <param name="snapshot">스킬 실행기가 발행한 차징 상태 스냅샷입니다.</param>
        /// <returns>표시 가능하면 <see langword="true"/>를 반환합니다.</returns>
        private bool ShouldShow(SkillChargeSnapshot snapshot)
        {
            if (!snapshot.IsActive)
                return false;

            GGemCoSkillSettings settings = GetSkillSettings();
            if (settings != null)
            {
                if (_character != null && _character.IsPlayer() && !settings.showChargeGaugeForPlayer)
                    return false;

                if (_character != null && _character.IsMonster() && !settings.showChargeGaugeForMonster)
                    return false;

                if (!settings.useSkillChargeGaugePrefab && gaugeView == null)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 차징 게이지 View를 보장합니다. 기존 자식 View가 없으면 설정된 Prefab을 Canvas 하위에 생성합니다.
        /// </summary>
        /// <returns>사용 가능한 View가 있으면 <see langword="true"/>를 반환합니다.</returns>
        private bool EnsureGaugeView()
        {
            if (gaugeView != null)
            {
                ConfigureFollower();
                return true;
            }

            UIElementSkillChargeGauge prefab = ResolveGaugePrefab();
            if (prefab == null)
                return false;

            Transform parent = ResolveGaugeParent();
            if (parent == null)
                return false;

            gaugeView = Instantiate(prefab, parent, false);
            _ownsGaugeView = true;
            gaugeView.Hide();
            ConfigureFollower();
            return gaugeView != null;
        }

        /// <summary>
        /// 차징 게이지 Prefab이 배치될 부모 Transform을 반환합니다.
        /// </summary>
        /// <returns>SceneGame.canvasFromWorldCharacterBottom의 Transform입니다.</returns>
        private static Transform ResolveGaugeParent()
        {
            if (SceneGame.Instance == null || SceneGame.Instance.canvasFromWorldCharacterBottom == null)
                return null;

            return SceneGame.Instance.canvasFromWorldCharacterBottom.transform;
        }

        /// <summary>
        /// Local Override와 설정 파일을 기준으로 사용할 차징 게이지 Prefab을 반환합니다.
        /// </summary>
        /// <returns>생성할 차징 게이지 Prefab입니다.</returns>
        private UIElementSkillChargeGauge ResolveGaugePrefab()
        {
            if (gaugePrefabOverride != null)
                return gaugePrefabOverride;

            GGemCoSkillSettings settings = GetSkillSettings();
            if (settings == null || !settings.useSkillChargeGaugePrefab)
                return null;

            return settings.skillChargeGaugePrefab;
        }

        /// <summary>
        /// View의 월드 위치 추적 컴포넌트를 구성합니다.
        /// </summary>
        private void ConfigureFollower()
        {
            if (gaugeView == null || _character == null)
                return;

            RectTransform rectTransform = gaugeView.RectTransform;
            if (rectTransform == null)
                return;

            Transform parent = gaugeView.transform.parent;
            RectTransform parentRect = parent as RectTransform;
            Canvas canvas = parent != null ? parent.GetComponentInParent<Canvas>() : null;

            if (_worldFollower == null)
                _worldFollower = gaugeView.GetComponent<SkillChargeGaugeWorldFollower>();
            if (_worldFollower == null)
                _worldFollower = gaugeView.gameObject.AddComponent<SkillChargeGaugeWorldFollower>();

            _worldFollower.Initialize(
                rectTransform,
                _character.transform,
                canvas,
                parentRect,
                ResolveWorldCamera(),
                ResolveWorldOffset(),
                ResolveScreenOffset());
        }

        /// <summary>
        /// 차징 게이지를 숨기고 설정에 따라 인스턴스를 재사용하거나 정리합니다.
        /// </summary>
        private void HideGauge()
        {
            _isGaugeActive = false;
            gaugeView?.Hide();

            GGemCoSkillSettings settings = GetSkillSettings();
            bool reuseInstance = settings == null || settings.reuseSkillChargeGaugeInstance;
            if (!reuseInstance)
                ReleaseGaugeView();
        }

        /// <summary>
        /// 월드 좌표 변환에 사용할 카메라를 반환합니다.
        /// </summary>
        /// <returns>SceneGame.mainCamera 또는 Camera.main입니다.</returns>
        private static Camera ResolveWorldCamera()
        {
            if (SceneGame.Instance != null && SceneGame.Instance.mainCamera != null)
                return SceneGame.Instance.mainCamera;

            return Camera.main;
        }

        /// <summary>
        /// 현재 설정에 맞는 월드 위치 오프셋을 반환합니다.
        /// </summary>
        /// <returns>캐릭터 월드 위치 기준 오프셋입니다.</returns>
        private Vector3 ResolveWorldOffset()
        {
            if (useLocalOffsetOverride)
                return worldOffsetOverride;

            GGemCoSkillSettings settings = GetSkillSettings();
            return settings != null ? settings.skillChargeGaugeWorldOffset : worldOffsetOverride;
        }

        /// <summary>
        /// 현재 설정에 맞는 Canvas 픽셀 오프셋을 반환합니다.
        /// </summary>
        /// <returns>Canvas 로컬 좌표에 더할 픽셀 오프셋입니다.</returns>
        private Vector2 ResolveScreenOffset()
        {
            if (useLocalOffsetOverride)
                return screenOffsetOverride;

            GGemCoSkillSettings settings = GetSkillSettings();
            return settings != null ? settings.skillChargeGaugeScreenOffset : screenOffsetOverride;
        }

        /// <summary>
        /// Addressables로 로드된 Skill 설정을 반환합니다.
        /// </summary>
        /// <returns>현재 로드된 Skill 설정입니다.</returns>
        private static GGemCoSkillSettings GetSkillSettings()
        {
            return AddressableLoaderSettingsSkill.Instance != null
                ? AddressableLoaderSettingsSkill.Instance.skillSettings
                : null;
        }
    }
}
