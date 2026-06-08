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

        private PlayerSkillComboController _comboController;
        private readonly Dictionary<GameObject, List<GameObject>> _mpIconPoolBySlot = new();

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
            ClearMainSlots();
            HideComboHud();
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
            if (mainSlot == null || mainSlot.Length == 0)
            {
                return;
            }

            List<RuntimeSkillComboNode> mainNodes = CollectMainNodes(definition);
            int activeCount = Mathf.Min(mainNodes.Count, mainSlot.Length);

            for (int i = 0; i < mainSlot.Length; i++)
            {
                GameObject slotObject = mainSlot[i];
                if (slotObject == null)
                {
                    continue;
                }

                bool isActive = i < activeCount;
                slotObject.SetActive(isActive);

                if (!isActive)
                {
                    DeactivateMpIcons(slotObject);
                    continue;
                }

                RuntimeSkillComboNode node = mainNodes[i];
                int needMp = ResolveNeedMp(node.SkillUid);
                CreateMpIcons(slotObject, needMp);
            }
        }

        /// <summary>
        /// 콤보 정의에서 메인 타입 노드만 인덱스 순서로 수집합니다.
        /// </summary>
        /// <param name="definition">노드를 수집할 콤보 정의입니다.</param>
        /// <returns>인덱스 기준으로 정렬된 메인 콤보 노드 목록입니다.</returns>
        private static List<RuntimeSkillComboNode> CollectMainNodes(RuntimeSkillComboDefinition definition)
        {
            List<RuntimeSkillComboNode> result = new();
            if (definition?.Nodes == null)
            {
                return result;
            }

            for (int i = 0; i < definition.Nodes.Count; i++)
            {
                RuntimeSkillComboNode node = definition.Nodes[i];
                if (node == null || !node.IsMain || node.SkillUid <= 0)
                {
                    continue;
                }

                result.Add(node);
            }

            result.Sort(CompareNodeIndex);
            return result;
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
        /// 모든 메인 슬롯의 MP 아이콘을 비활성화하고 슬롯을 숨깁니다.
        /// 아이콘 오브젝트는 재사용을 위해 파괴하지 않습니다.
        /// </summary>
        private void ClearMainSlots()
        {
            if (mainSlot == null)
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

                DeactivateMpIcons(slotObject);
                slotObject.SetActive(false);
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
                if (iconPool[i] != null)
                {
                    if (iconPool[i].GetComponent<Image>())
                        iconPool[i].GetComponent<Image>().sprite = imageBg;
                    iconPool[i].SetActive(false);
                }
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
            // TODO:
            // - 현재 실행 중인 스킬 슬롯 강조
            // - nodeType이 Last이면 마무리 스킬 UI 강조
            // - 다음 입력 가능 상태 표시
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
