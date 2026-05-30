using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 콤보 진행 상태를 표시하는 HUD 윈도우입니다.
    /// </summary>
    public class UIWindowSkillComboHud : UIWindow
    {
        private PlayerSkillComboController _comboController;

        protected override void Awake()
        {
            base.Awake();
            MapManager.OnLoadCompleteMap += OnLoadCompleteMap;
        }

        private void OnLoadCompleteMap(MapTileCommon arg1, GameObject arg2)
        {
            TryBindComboController();
            SubscribeComboEvents();
        }

        protected override void Start()
        {
            base.Start();
        }

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
        /// 콤보 진입 UI를 갱신합니다.
        /// </summary>
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
        private void RefreshComboFinishedView(int skillUid, int nodeIndex)
        {
            // TODO:
            // - 마무리 스킬 완료 연출
            // - 콤보 성공 이펙트/텍스트 표시
        }
    }
}