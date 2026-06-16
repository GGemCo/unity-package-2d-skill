using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.UI;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 콤보 진행 상태를 표시하는 HUD 윈도우입니다.
    /// </summary>
    public class UIWindowSkillComboHud : UIWindow
    {
        [Header(UIWindowConstants.TitleHeaderIndividual)]
        [Tooltip("콤보 슬롯 오브젝트")]
        [SerializeField] private GameObject[] mainSlot;
        [Tooltip("콤보 연결 라인 오브젝트")]
        [SerializeField] private GameObject[] mainLine;

        [Tooltip("MP 아이콘 프리팹")]
        [SerializeField] private GameObject prefabIconMp;
        [Tooltip("BG 사용 후")]
        [SerializeField] private Sprite imageBg;
        [Tooltip("BG 사용 전")]
        [SerializeField] private Sprite imageBgNext;

        private const float CompletedSlotMpIconAlpha = 0.3f;

        private enum ComboHudMainSlotState
        {
            /// <summary>
            /// 아직 사용하지 않은 기본 슬롯 상태입니다.
            /// </summary>
            Normal,

            /// <summary>
            /// 다음 입력으로 사용할 슬롯 상태입니다.
            /// </summary>
            Next,

            /// <summary>
            /// 이미 사용을 완료한 슬롯 상태입니다.
            /// </summary>
            Completed,
        }

        private PlayerSkillComboController _comboController;
        private readonly Dictionary<GameObject, List<GameObject>> _mpIconPoolBySlot = new();
        private readonly List<RuntimeSkillComboNode> _cachedMainNodes = new();
        private readonly Dictionary<int, int> _slotIndexByNodeIndex = new();
        private int _activeMainSlotCount;
        private int _nextMainSlotIndex;
        private int _lastPreviewSlotIndex = -1;

        /// <summary>
        /// 윈도우 기본 초기화와 맵 로드 완료 이벤트 구독을 처리합니다.
        /// </summary>
        protected override void Awake()
        {
            base.Awake();
            MapManager.OnLoadCompleteMap += OnLoadCompleteMap;
            ClearMainSlots();
        }

        /// <summary>
        /// 맵 로드 완료 후 현재 플레이어의 콤보 컨트롤러를 찾아 UI 이벤트를 연결합니다.
        /// </summary>
        /// <param name="arg1">로드가 완료된 맵 타일 정보입니다.</param>
        /// <param name="arg2">로드된 맵 루트 오브젝트입니다.</param>
        private void OnLoadCompleteMap(MapTileCommon arg1, GameObject arg2)
        {
            TryBindComboController();
            SubscribeComboEvents();
        }

        /// <summary>
        /// 윈도우 시작 시 기본 처리를 수행합니다.
        /// </summary>
        protected override void Start()
        {
            base.Start();
        }

        /// <summary>
        /// 콤보 UI 이벤트와 맵 로드 이벤트 구독을 해제합니다.
        /// </summary>
        private void OnDestroy()
        {
            UnsubscribeComboEvents();
            MapManager.OnLoadCompleteMap -= OnLoadCompleteMap;
        }

        /// <summary>
        /// 현재 플레이어에서 콤보 컨트롤러를 찾아 캐시합니다.
        /// </summary>
        private void TryBindComboController()
        {
            if (_comboController != null)
            {
                return;
            }

            if (SceneGame.Instance == null || SceneGame.Instance.player == null)
            {
                return;
            }

            _comboController = SceneGame.Instance.player.GetComponent<PlayerSkillComboController>();
        }

        /// <summary>
        /// 콤보 UI 갱신에 필요한 이벤트를 구독합니다.
        /// </summary>
        private void SubscribeComboEvents()
        {
            if (_comboController == null)
            {
                return;
            }

            UnsubscribeComboEvents();
            _comboController.ComboOpenedForUi += OnComboOpenedForUi;
            _comboController.ComboSkillStartedForUi += OnComboSkillStartedForUi;
            _comboController.ComboCanceledForUi += OnComboCanceledForUi;
            _comboController.ComboFinishedByLastSkillForUi += OnComboFinishedByLastSkillForUi;
            _comboController.ComboLastPreviewChangedForUi += OnComboLastPreviewChangedForUi;
        }

        /// <summary>
        /// 콤보 UI 갱신 이벤트 구독을 해제합니다.
        /// </summary>
        private void UnsubscribeComboEvents()
        {
            if (_comboController == null)
            {
                return;
            }

            _comboController.ComboOpenedForUi -= OnComboOpenedForUi;
            _comboController.ComboSkillStartedForUi -= OnComboSkillStartedForUi;
            _comboController.ComboCanceledForUi -= OnComboCanceledForUi;
            _comboController.ComboFinishedByLastSkillForUi -= OnComboFinishedByLastSkillForUi;
            _comboController.ComboLastPreviewChangedForUi -= OnComboLastPreviewChangedForUi;
        }

        /// <summary>
        /// 콤보가 열렸을 때 호출됩니다.
        /// 첫 입력 안내, 메인 스킬/마무리 스킬 후보 표시를 처리합니다.
        /// </summary>
        /// <param name="result">콤보 열기 결과입니다.</param>
        private void OnComboOpenedForUi(SkillComboOpenResult result)
        {
            if (!result.IsOpened)
            {
                return;
            }

            ShowComboHud();
            RefreshMainSlots();
            ApplyMainSlotProgress(0);

            // result.SkillUid: 첫 번째 Main 입력으로 실행될 스킬 UID
            // result.LastSkillUid: 첫 번째 Last 입력으로 실행될 마무리 스킬 UID
            // result.EntryTrigger: 콤보가 열린 조건
            RefreshComboOpenView(result.SkillUid, result.LastSkillUid, result.EntryTrigger);
        }

        /// <summary>
        /// 콤보 스킬이 실제로 시작되었을 때 호출됩니다.
        /// 현재 실행 중인 스킬 강조, 다음 입력 대기 UI 갱신을 처리합니다.
        /// </summary>
        /// <param name="result">콤보 스킬 실행 결과입니다.</param>
        private void OnComboSkillStartedForUi(SkillComboUseResult result)
        {
            if (!result.IsStarted)
            {
                return;
            }

            ShowComboHud();

            // result.SkillUid: 실행된 스킬 UID
            // result.NodeIndex: 실행된 콤보 노드 인덱스
            // result.NodeType: Main 또는 Last 타입
            RefreshComboSkillStartedView(result.SkillUid, result.NodeIndex, result.NodeType);
        }

        /// <summary>
        /// 콤보가 취소되었을 때 호출됩니다.
        /// 입력 시간 만료, 스킬 실패 등으로 HUD를 닫거나 취소 연출을 처리합니다.
        /// </summary>
        /// <param name="cancelEvent">콤보 취소 정보입니다.</param>
        private void OnComboCanceledForUi(SkillComboCancelEvent cancelEvent)
        {
            // cancelEvent.Reason 값으로 만료/실패/수동 취소를 구분할 수 있습니다.
            RefreshComboCanceledView(cancelEvent.Reason);
            ClearLastPreview();
            ClearMainSlots();
            HideComboHud();
        }

        /// <summary>
        /// 마무리 스킬 사용으로 콤보가 정상 종료되었을 때 호출됩니다.
        /// 완료 연출 후 HUD를 닫습니다.
        /// </summary>
        /// <param name="result">마무리 스킬 실행 결과입니다.</param>
        private void OnComboFinishedByLastSkillForUi(SkillComboUseResult result)
        {
            if (!result.IsStarted)
            {
                return;
            }

            RefreshComboFinishedView(result.SkillUid, result.NodeIndex);
            ClearLastPreview();
            ClearMainSlots();
            HideComboHud();
        }

        /// <summary>
        /// 마무리 스킬 프리뷰 상태가 바뀌었을 때 다음 입력 슬롯의 MP 아이콘을 갱신합니다.
        /// </summary>
        /// <param name="previewEvent">마무리 스킬 프리뷰 상태 데이터입니다.</param>
        private void OnComboLastPreviewChangedForUi(SkillComboLastPreviewEvent previewEvent)
        {
            if (!previewEvent.IsPreviewActive || previewEvent.LastSkillUid <= 0)
            {
                ClearLastPreview();
                return;
            }

            ApplyLastPreview(previewEvent.LastSkillUid);
        }

        /// <summary>
        /// 콤보 HUD를 표시합니다.
        /// </summary>
        private void ShowComboHud()
        {
            Show(true);
        }

        /// <summary>
        /// 콤보 HUD를 숨깁니다.
        /// </summary>
        private void HideComboHud()
        {
            Show(false);
        }

        /// <summary>
        /// 현재 콤보 정의를 기준으로 메인 슬롯과 MP 아이콘 표시를 갱신합니다.
        /// </summary>
        private void RefreshMainSlots()
        {
            if (_comboController == null ||
                !_comboController.TryGetComboDefinitionForUi(out RuntimeSkillComboDefinition definition))
            {
                ClearMainSlots();
                return;
            }

            RefreshMainSlots(definition);
        }

        /// <summary>
        /// 지정한 콤보 정의에 포함된 메인 노드 수만큼 슬롯을 활성화하고, 각 스킬의 필요 MP 아이콘을 표시합니다.
        /// </summary>
        /// <param name="definition">HUD에 표시할 콤보 정의입니다.</param>
        private void RefreshMainSlots(RuntimeSkillComboDefinition definition)
        {
            CacheMainNodes(definition);
            RefreshMainSlotObjects();
            RefreshMainLines();
        }

        /// <summary>
        /// 콤보 정의에서 Main 노드를 수집하고, 콤보 노드 인덱스와 HUD 슬롯 인덱스 매핑을 갱신합니다.
        /// </summary>
        /// <param name="definition">노드를 수집할 콤보 정의입니다.</param>
        private void CacheMainNodes(RuntimeSkillComboDefinition definition)
        {
            _cachedMainNodes.Clear();
            _slotIndexByNodeIndex.Clear();

            if (definition?.Nodes == null)
            {
                _activeMainSlotCount = 0;
                _nextMainSlotIndex = 0;
                return;
            }

            for (int i = 0; i < definition.Nodes.Count; i++)
            {
                RuntimeSkillComboNode node = definition.Nodes[i];
                if (node == null || !node.IsMain || node.SkillUid <= 0)
                {
                    continue;
                }

                _cachedMainNodes.Add(node);
            }

            _cachedMainNodes.Sort(CompareNodeIndex);
            _activeMainSlotCount = Mathf.Min(_cachedMainNodes.Count, mainSlot != null ? mainSlot.Length : 0);
            _nextMainSlotIndex = Mathf.Clamp(_nextMainSlotIndex, 0, _activeMainSlotCount);

            for (int i = 0; i < _activeMainSlotCount; i++)
            {
                RuntimeSkillComboNode node = _cachedMainNodes[i];
                if (node == null)
                {
                    continue;
                }

                _slotIndexByNodeIndex[node.Index] = i;
            }
        }

        /// <summary>
        /// 캐시된 Main 노드 수에 맞춰 슬롯 오브젝트와 MP 아이콘 표시를 갱신합니다.
        /// </summary>
        private void RefreshMainSlotObjects()
        {
            if (mainSlot == null || mainSlot.Length == 0)
            {
                return;
            }

            for (int i = 0; i < mainSlot.Length; i++)
            {
                GameObject slotObject = mainSlot[i];
                if (slotObject == null)
                {
                    continue;
                }

                bool isActive = i < _activeMainSlotCount;
                slotObject.SetActive(isActive);

                if (!isActive)
                {
                    ApplyMainSlotState(slotObject, ComboHudMainSlotState.Normal);
                    DeactivateMpIcons(slotObject);
                    continue;
                }

                RuntimeSkillComboNode node = _cachedMainNodes[i];
                int needMp = ResolveNeedMp(node.SkillUid);
                CreateMpIcons(slotObject, needMp);
                ApplyMainSlotState(slotObject, ComboHudMainSlotState.Normal);
            }

            _lastPreviewSlotIndex = -1;
        }

        /// <summary>
        /// 현재 다음 입력 슬롯에 마무리 스킬 필요 MP를 임시로 표시합니다.
        /// </summary>
        /// <param name="lastSkillUid">프리뷰로 표시할 마무리 스킬 UID입니다.</param>
        private void ApplyLastPreview(int lastSkillUid)
        {
            int previewSlotIndex = ResolvePreviewSlotIndex();
            if (!TryGetActiveMainSlot(previewSlotIndex, out GameObject slotObject))
            {
                ClearLastPreview();
                return;
            }

            int needMp = ResolveNeedMp(lastSkillUid);
            CreateMpIcons(slotObject, needMp);
            ApplyMainSlotState(slotObject, ComboHudMainSlotState.Next);
            _lastPreviewSlotIndex = previewSlotIndex;
        }

        /// <summary>
        /// 마무리 스킬 프리뷰를 해제하고 해당 슬롯의 메인 콤보 필요 MP 표시를 복원합니다.
        /// </summary>
        private void ClearLastPreview()
        {
            if (_lastPreviewSlotIndex < 0)
            {
                return;
            }

            RestoreMainSlotMpIcons(_lastPreviewSlotIndex);
            _lastPreviewSlotIndex = -1;
        }

        /// <summary>
        /// 지정한 슬롯의 MP 아이콘을 원래 메인 콤보 스킬 기준으로 복원합니다.
        /// </summary>
        /// <param name="slotIndex">복원할 HUD 메인 슬롯 인덱스입니다.</param>
        private void RestoreMainSlotMpIcons(int slotIndex)
        {
            if (!TryGetActiveMainSlot(slotIndex, out GameObject slotObject))
            {
                return;
            }

            RuntimeSkillComboNode node = _cachedMainNodes[slotIndex];
            int needMp = node != null ? ResolveNeedMp(node.SkillUid) : 0;
            CreateMpIcons(slotObject, needMp);

            ComboHudMainSlotState state = ComboHudMainSlotState.Normal;
            if (slotIndex < _nextMainSlotIndex)
            {
                state = ComboHudMainSlotState.Completed;
            }
            else if (slotIndex == _nextMainSlotIndex)
            {
                state = ComboHudMainSlotState.Next;
            }

            ApplyMainSlotState(slotObject, state);
        }

        /// <summary>
        /// 마무리 스킬 프리뷰를 표시할 HUD 슬롯 인덱스를 계산합니다.
        /// </summary>
        /// <returns>프리뷰 대상 슬롯 인덱스입니다.</returns>
        private int ResolvePreviewSlotIndex()
        {
            return Mathf.Clamp(_nextMainSlotIndex, 0, Mathf.Max(0, _activeMainSlotCount - 1));
        }

        /// <summary>
        /// 지정한 HUD 메인 슬롯이 사용 가능한지 확인하고 슬롯 오브젝트를 반환합니다.
        /// </summary>
        /// <param name="slotIndex">조회할 HUD 메인 슬롯 인덱스입니다.</param>
        /// <param name="slotObject">조회된 슬롯 오브젝트입니다.</param>
        /// <returns>활성 메인 슬롯을 찾으면 <see langword="true"/>입니다.</returns>
        private bool TryGetActiveMainSlot(int slotIndex, out GameObject slotObject)
        {
            slotObject = null;
            if (mainSlot == null ||
                slotIndex < 0 ||
                slotIndex >= _activeMainSlotCount ||
                slotIndex >= mainSlot.Length ||
                slotIndex >= _cachedMainNodes.Count)
            {
                return false;
            }

            slotObject = mainSlot[slotIndex];
            return slotObject != null && slotObject.activeSelf;
        }

        /// <summary>
        /// 콤보 노드 인덱스를 기준으로 오름차순 정렬합니다.
        /// </summary>
        /// <param name="left">비교할 왼쪽 노드입니다.</param>
        /// <param name="right">비교할 오른쪽 노드입니다.</param>
        /// <returns>정렬 순서를 나타내는 비교 값입니다.</returns>
        private static int CompareNodeIndex(RuntimeSkillComboNode left, RuntimeSkillComboNode right)
        {
            int leftIndex = left != null ? left.Index : int.MaxValue;
            int rightIndex = right != null ? right.Index : int.MaxValue;
            return leftIndex.CompareTo(rightIndex);
        }

        /// <summary>
        /// 스킬 정의에서 필요 MP 값을 조회합니다.
        /// </summary>
        /// <param name="skillUid">조회할 플레이어 스킬 UID입니다.</param>
        /// <returns>스킬에 설정된 필요 MP입니다. 조회 실패 시 0입니다.</returns>
        private static int ResolveNeedMp(int skillUid)
        {
            if (skillUid <= 0)
            {
                return 0;
            }

            return SkillDefinitionResolver.TryResolve(skillUid, out RuntimeSkillDefinition definition) && definition != null
                ? Mathf.Max(0, definition.NeedMp)
                : 0;
        }

        /// <summary>
        /// 지정한 슬롯에 필요한 MP 개수만큼 아이콘을 표시합니다.
        /// 이미 생성된 아이콘은 재사용하고, 부족한 수량만 새로 생성합니다.
        /// </summary>
        /// <param name="slotObject">MP 아이콘을 배치할 콤보 슬롯 오브젝트입니다.</param>
        /// <param name="needMp">표시할 필요 MP 개수입니다.</param>
        private void CreateMpIcons(GameObject slotObject, int needMp)
        {
            if (slotObject == null)
            {
                return;
            }

            List<GameObject> iconPool = GetOrCreateMpIconPool(slotObject);
            int requiredCount = Mathf.Max(0, needMp);

            for (int i = 0; i < requiredCount; i++)
            {
                GameObject iconObject = GetOrCreateMpIcon(slotObject.transform, iconPool, i);
                if (iconObject != null)
                {
                    iconObject.SetActive(true);
                }
            }

            for (int i = requiredCount; i < iconPool.Count; i++)
            {
                if (iconPool[i] != null)
                {
                    iconPool[i].SetActive(false);
                }
            }
        }

        /// <summary>
        /// 지정한 슬롯의 MP 아이콘 풀을 반환합니다.
        /// </summary>
        /// <param name="slotObject">MP 아이콘 풀을 찾을 슬롯 오브젝트입니다.</param>
        /// <returns>슬롯별로 캐시된 MP 아이콘 목록입니다.</returns>
        private List<GameObject> GetOrCreateMpIconPool(GameObject slotObject)
        {
            if (_mpIconPoolBySlot.TryGetValue(slotObject, out List<GameObject> iconPool) && iconPool != null)
            {
                return iconPool;
            }

            iconPool = new List<GameObject>();
            _mpIconPoolBySlot[slotObject] = iconPool;
            return iconPool;
        }

        /// <summary>
        /// 지정한 인덱스의 MP 아이콘을 반환합니다.
        /// 기존 아이콘이 없거나 Unity null 상태이면 새 아이콘을 생성해 풀에 저장합니다.
        /// </summary>
        /// <param name="parent">아이콘을 배치할 부모 Transform입니다.</param>
        /// <param name="iconPool">슬롯에 연결된 MP 아이콘 풀입니다.</param>
        /// <param name="index">가져올 아이콘 인덱스입니다.</param>
        /// <returns>사용 가능한 MP 아이콘 오브젝트입니다.</returns>
        private GameObject GetOrCreateMpIcon(Transform parent, List<GameObject> iconPool, int index)
        {
            while (iconPool.Count <= index)
            {
                iconPool.Add(null);
            }

            GameObject iconObject = iconPool[index];
            if (iconObject != null)
            {
                iconObject.transform.SetParent(parent, false);
                return iconObject;
            }

            if (prefabIconMp == null)
            {
                return null;
            }

            iconObject = Instantiate(prefabIconMp, parent, false);
            iconPool[index] = iconObject;
            return iconObject;
        }

        /// <summary>
        /// 모든 메인 슬롯과 연결 라인을 기본 상태로 되돌리고 슬롯을 숨깁니다.
        /// MP 아이콘 오브젝트는 재사용을 위해 파괴하지 않습니다.
        /// </summary>
        private void ClearMainSlots()
        {
            _cachedMainNodes.Clear();
            _slotIndexByNodeIndex.Clear();
            _activeMainSlotCount = 0;
            _nextMainSlotIndex = 0;
            _lastPreviewSlotIndex = -1;

            if (mainSlot != null)
            {
                for (int i = 0; i < mainSlot.Length; i++)
                {
                    GameObject slotObject = mainSlot[i];
                    if (slotObject == null)
                    {
                        continue;
                    }

                    ApplyMainSlotState(slotObject, ComboHudMainSlotState.Normal);
                    DeactivateMpIcons(slotObject);
                    slotObject.SetActive(false);
                }
            }

            ClearMainLines();
        }

        /// <summary>
        /// 모든 콤보 연결 라인을 비활성화합니다.
        /// </summary>
        private void ClearMainLines()
        {
            if (mainLine == null)
            {
                return;
            }

            for (int i = 0; i < mainLine.Length; i++)
            {
                if (mainLine[i] != null)
                {
                    mainLine[i].SetActive(false);
                }
            }
        }

        /// <summary>
        /// Main 슬롯의 활성 상태에 맞춰 슬롯 사이 연결 라인을 갱신합니다.
        /// mainLine[i]는 mainSlot[i - 1]과 mainSlot[i] 사이의 라인으로 사용합니다.
        /// </summary>
        private void RefreshMainLines()
        {
            if (mainLine == null)
            {
                return;
            }

            for (int i = 0; i < mainLine.Length; i++)
            {
                GameObject lineObject = mainLine[i];
                if (lineObject == null)
                {
                    continue;
                }

                bool isActive =
                    i > 0 &&
                    mainSlot != null &&
                    i < mainSlot.Length &&
                    mainSlot[i - 1] != null &&
                    mainSlot[i] != null &&
                    mainSlot[i - 1].activeSelf &&
                    mainSlot[i].activeSelf;

                lineObject.SetActive(isActive);
            }
        }

        /// <summary>
        /// 현재 콤보 진행 상태에 맞춰 완료 슬롯, 다음 슬롯, 기본 슬롯 표시를 갱신합니다.
        /// </summary>
        /// <param name="nextSlotIndex">다음 입력으로 사용할 Main 슬롯 인덱스입니다.</param>
        private void ApplyMainSlotProgress(int nextSlotIndex)
        {
            ClearLastPreview();

            if (mainSlot == null || _activeMainSlotCount <= 0)
            {
                RefreshMainLines();
                return;
            }

            _nextMainSlotIndex = Mathf.Clamp(nextSlotIndex, 0, _activeMainSlotCount);

            for (int i = 0; i < _activeMainSlotCount; i++)
            {
                GameObject slotObject = mainSlot[i];
                if (slotObject == null)
                {
                    continue;
                }

                ComboHudMainSlotState state = ComboHudMainSlotState.Normal;
                if (i < _nextMainSlotIndex)
                {
                    state = ComboHudMainSlotState.Completed;
                }
                else if (i == _nextMainSlotIndex)
                {
                    state = ComboHudMainSlotState.Next;
                }

                ApplyMainSlotState(slotObject, state);
            }

            RefreshMainLines();
        }

        /// <summary>
        /// 콤보 노드 인덱스를 HUD Main 슬롯 인덱스로 변환합니다.
        /// </summary>
        /// <param name="nodeIndex">변환할 콤보 노드 인덱스입니다.</param>
        /// <returns>HUD 슬롯 인덱스입니다. 찾지 못하면 -1입니다.</returns>
        private int ResolveMainSlotIndexByNodeIndex(int nodeIndex)
        {
            return _slotIndexByNodeIndex.TryGetValue(nodeIndex, out int slotIndex)
                ? slotIndex
                : -1;
        }

        /// <summary>
        /// 지정한 Main 슬롯 오브젝트에 배경 이미지와 MP 아이콘 알파 상태를 적용합니다.
        /// </summary>
        /// <param name="slotObject">상태를 적용할 슬롯 오브젝트입니다.</param>
        /// <param name="state">적용할 슬롯 상태입니다.</param>
        private void ApplyMainSlotState(GameObject slotObject, ComboHudMainSlotState state)
        {
            if (slotObject == null)
            {
                return;
            }

            Sprite backgroundSprite = state == ComboHudMainSlotState.Next ? imageBgNext : imageBg;
            SetSlotBackground(slotObject, backgroundSprite);

            float iconAlpha = state == ComboHudMainSlotState.Completed ? CompletedSlotMpIconAlpha : 1f;
            SetMpIconAlpha(slotObject, iconAlpha);
        }

        /// <summary>
        /// 슬롯 오브젝트에 연결된 Image 컴포넌트의 배경 스프라이트를 변경합니다.
        /// </summary>
        /// <param name="slotObject">배경 이미지를 변경할 슬롯 오브젝트입니다.</param>
        /// <param name="sprite">적용할 배경 스프라이트입니다.</param>
        private static void SetSlotBackground(GameObject slotObject, Sprite sprite)
        {
            if (slotObject == null || sprite == null)
            {
                return;
            }

            if (slotObject.TryGetComponent(out Image image))
            {
                image.sprite = sprite;
            }
        }

        /// <summary>
        /// 지정한 슬롯에 생성된 MP 아이콘들의 알파 값을 변경합니다.
        /// </summary>
        /// <param name="slotObject">MP 아이콘을 포함한 슬롯 오브젝트입니다.</param>
        /// <param name="alpha">적용할 알파 값입니다.</param>
        private void SetMpIconAlpha(GameObject slotObject, float alpha)
        {
            if (slotObject == null ||
                !_mpIconPoolBySlot.TryGetValue(slotObject, out List<GameObject> iconPool))
            {
                return;
            }

            alpha = Mathf.Clamp01(alpha);
            for (int i = 0; i < iconPool.Count; i++)
            {
                GameObject iconObject = iconPool[i];
                if (iconObject == null)
                {
                    continue;
                }

                SetGraphicAlpha(iconObject, alpha);
            }
        }

        /// <summary>
        /// 지정한 UI 오브젝트와 하위 Graphic 컴포넌트의 알파 값을 변경합니다.
        /// CanvasGroup이 있으면 CanvasGroup을 우선 사용합니다.
        /// </summary>
        /// <param name="target">알파 값을 변경할 대상 오브젝트입니다.</param>
        /// <param name="alpha">적용할 알파 값입니다.</param>
        private static void SetGraphicAlpha(GameObject target, float alpha)
        {
            if (target == null)
            {
                return;
            }

            alpha = Mathf.Clamp01(alpha);
            if (target.TryGetComponent(out CanvasGroup canvasGroup))
            {
                canvasGroup.alpha = alpha;
                return;
            }

            Graphic[] graphics = target.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null)
                {
                    continue;
                }

                Color color = graphic.color;
                color.a = alpha;
                graphic.color = color;
            }
        }

        /// <summary>
        /// 지정한 슬롯에 연결된 모든 MP 아이콘을 비활성화합니다.
        /// </summary>
        /// <param name="slotObject">MP 아이콘을 숨길 콤보 슬롯 오브젝트입니다.</param>
        private void DeactivateMpIcons(GameObject slotObject)
        {
            if (slotObject == null || !_mpIconPoolBySlot.TryGetValue(slotObject, out List<GameObject> iconPool))
            {
                return;
            }

            for (int i = 0; i < iconPool.Count; i++)
            {
                GameObject iconObject = iconPool[i];
                if (iconObject == null)
                {
                    continue;
                }

                SetGraphicAlpha(iconObject, 1f);
                iconObject.SetActive(false);
            }
        }

        /// <summary>
        /// 콤보 진입 UI를 갱신합니다.
        /// </summary>
        /// <param name="mainSkillUid">첫 번째 메인 콤보 스킬 UID입니다.</param>
        /// <param name="lastSkillUid">첫 번째 마무리 콤보 스킬 UID입니다.</param>
        /// <param name="entryTrigger">콤보가 열린 진입 조건입니다.</param>
        private void RefreshComboOpenView(
            int mainSkillUid,
            int lastSkillUid,
            SkillComboEntryTrigger entryTrigger)
        {
            // TODO:
            // - Main 스킬 아이콘 표시
            // - Last 스킬 아이콘 표시
            // - entryTrigger 기준 안내 문구 표시
        }

        /// <summary>
        /// 콤보 스킬 시작 UI를 갱신합니다.
        /// </summary>
        /// <param name="skillUid">실행이 시작된 스킬 UID입니다.</param>
        /// <param name="nodeIndex">실행이 시작된 콤보 노드 인덱스입니다.</param>
        /// <param name="nodeType">실행이 시작된 콤보 노드 타입입니다.</param>
        private void RefreshComboSkillStartedView(
            int skillUid,
            int nodeIndex,
            SkillComboNodeType nodeType)
        {
            if (nodeType != SkillComboNodeType.Main)
            {
                return;
            }

            int usedSlotIndex = ResolveMainSlotIndexByNodeIndex(nodeIndex);
            if (usedSlotIndex < 0)
            {
                return;
            }

            ApplyMainSlotProgress(usedSlotIndex + 1);
        }

        /// <summary>
        /// 콤보 취소 UI를 갱신합니다.
        /// </summary>
        /// <param name="reason">콤보가 취소된 사유입니다.</param>
        private void RefreshComboCanceledView(SkillComboCancelReason reason)
        {
            // TODO:
            // - Expired: 시간 만료 연출
            // - SkillExecutionFailed: 실패 연출
            // - Manual: 즉시 닫기
            // - ChainInputWindowDisabled: 자연 종료 또는 닫기
            // - NoNextSkill: 다음 스킬이 없는 자연 종료 또는 닫기
            // - ChainInputNotUnlockedByConfirmedDamage: 확정 타격 없이 종료되어 닫기
        }

        /// <summary>
        /// 콤보 완료 UI를 갱신합니다.
        /// </summary>
        /// <param name="skillUid">완료를 발생시킨 마무리 스킬 UID입니다.</param>
        /// <param name="nodeIndex">완료를 발생시킨 콤보 노드 인덱스입니다.</param>
        private void RefreshComboFinishedView(int skillUid, int nodeIndex)
        {
            // TODO:
            // - 마무리 스킬 완료 연출
            // - 콤보 성공 이펙트/텍스트 표시
        }
    }
}
