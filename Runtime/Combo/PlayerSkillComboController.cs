using System;
using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 플레이어의 메인/마무리 스킬 콤보 입력을 실제 스킬 실행 요청으로 변환합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSkillComboController : MonoBehaviour
    {
        [Header("Combo Definition")]
        [SerializeField, Tooltip("별도 제공자가 없을 때 사용할 기본 콤보 정의입니다.")]
        private RuntimeSkillComboDefinition defaultComboDefinition = new();

        [SerializeField, Tooltip("현재 장착된 콤보 정의를 제공하는 선택적 컴포넌트입니다.")]
        private MonoBehaviour loadoutProviderBehaviour;

        [Header("Combo Input Window")]
        [SerializeField, Min(0f), Tooltip("외부 진입 조건으로 콤보가 열린 뒤 첫 입력을 기다릴 시간입니다. 0이면 만료 시간을 사용하지 않습니다.")]
        private float entryInputWindowSeconds = 1f;

        [SerializeField, Min(0f), Tooltip("스킬 종료 후 다음 콤보 입력을 기다릴 시간입니다. 0이면 기존처럼 스킬 종료 시 콤보를 초기화합니다.")]
        private float chainInputWindowSeconds;

        private const float InputWindowDisabledTime = -1f;

        private readonly SkillComboState _state = new();
        private IPlayerSkillComboLoadoutProvider _loadoutProvider;
        private ICharacterSkillDriver _skillDriver;
        private IPlayerSkillTargetingProvider _targetingProvider;
        private ISkillChainReadyNotifier _chainReadyNotifier;
        private SkillExecutor _skillExecutor;
        private bool _isInputWindowArmed;
        private bool _hasBufferedMainInput;
        private float _inputWindowExpireTime = InputWindowDisabledTime;
        private readonly List<object> _inputWindowHoldOwners = new();
        private float _heldInputWindowRemainingSeconds = InputWindowDisabledTime;
        private bool _chainGateOpenedByConfirmedDamage;

        /// <summary>
        /// 현재 플레이어 콤보 진행 상태입니다.
        /// </summary>
        public SkillComboState State => _state;

        /// <summary>
        /// 콤보 진입 조건이 충족되어 UI가 첫 입력 안내를 표시할 수 있을 때 호출됩니다.
        /// </summary>
        public event Action<SkillComboOpenResult> ComboOpenedForUi;

        /// <summary>
        /// 콤보 입력으로 스킬 실행이 실제 시작되어 UI가 진행 상태를 갱신할 수 있을 때 호출됩니다.
        /// </summary>
        public event Action<SkillComboUseResult> ComboSkillStartedForUi;

        /// <summary>
        /// 콤보가 마무리 스킬이 아닌 사유로 취소되어 UI를 닫거나 초기화해야 할 때 호출됩니다.
        /// </summary>
        public event Action<SkillComboCancelEvent> ComboCanceledForUi;

        /// <summary>
        /// 마무리 스킬 사용으로 콤보가 정상 종료되어 UI가 완료 연출을 표시할 수 있을 때 호출됩니다.
        /// </summary>
        public event Action<SkillComboUseResult> ComboFinishedByLastSkillForUi;

        /// <summary>
        /// 현재 이어갈 수 있는 콤보가 열려 있는지 반환합니다.
        /// </summary>
        public bool IsComboActive
        {
            get
            {
                ResetExpiredComboIfNeeded();
                return _state.IsActive;
            }
        }

        /// <summary>
        /// 외부 성공 이벤트로 콤보가 열리고, 아직 첫 Main 또는 Last 입력을 기다리는 진입 게이트 상태인지 반환합니다.
        /// </summary>
        public bool IsEntryGateActive
        {
            get
            {
                ResetExpiredComboIfNeeded();
                return _state.IsActive && _state.IsEntryGateActive;
            }
        }

        /// <summary>
        /// 현재 공격 입력으로 다음 콤보 스킬을 받을 수 있는 상태인지 반환합니다.
        /// </summary>
        /// <remarks>
        /// 기본적으로 이전 스킬 종료 후 열리는 체인 입력 창에서 true가 됩니다.
        /// 단, Skill 패키지의 확정 타격 체인 기능이 열리면 스킬 종료 전에도 true가 될 수 있습니다.
        /// </remarks>
        public bool CanAcceptComboInput
        {
            get
            {
                ResetExpiredComboIfNeeded();
                return _state.IsActive && _isInputWindowArmed;
            }
        }

        /// <summary>
        /// 현재 마무리 콤보 입력으로 다음 Last 노드를 실행할 수 있는 상태인지 반환합니다.
        /// </summary>
        /// <remarks>
        /// 입력 라우팅 계층이 공격 입력을 Main 또는 Last 명령 중 어디로 보낼지 판단할 때 사용합니다.
        /// 실제 스킬 실행이나 콤보 상태 변경은 <see cref="TryUseLast"/>에서만 수행합니다.
        /// </remarks>
        public bool CanAcceptLastComboInput
        {
            get
            {
                ResetExpiredComboIfNeeded();
                return _state.IsActive &&
                       _isInputWindowArmed &&
                       TryResolveNextNode(SkillComboCommand.Last, out _, out _);
            }
        }

        /// <summary>
        /// 현재 진입 게이트 위치에서 첫 번째 마무리 콤보 입력을 받을 수 있는지 반환합니다.
        /// </summary>
        /// <remarks>
        /// 일반 체인 중 Last 입력과 달리, 외부 성공 이벤트로 열린 직후의 EntryLast 노드만 대상으로 합니다.
        /// </remarks>
        public bool CanAcceptEntryLastComboInput
        {
            get
            {
                ResetExpiredComboIfNeeded();
                return _state.IsActive &&
                       _state.IsEntryGateActive &&
                       _isInputWindowArmed &&
                       TryResolveNextNode(SkillComboCommand.Last, out _, out _);
            }
        }

        /// <summary>
        /// 확정 피해 기반 체인 타이밍에서 메인 콤보 공격 입력을 받을 수 있는지 반환합니다.
        /// </summary>
        /// <remarks>
        /// 스킬 실행 중 일반 공격 입력을 차단하는 입력 규칙이, 실제 체인 공격으로 이어질 수 있는 경우만
        /// 기존 콤보 처리 흐름으로 통과시키기 위해 사용합니다.
        /// </remarks>
        public bool CanAcceptSkillChainMainInput
        {
            get
            {
                ResetExpiredComboIfNeeded();
                return _state.IsActive &&
                       !_state.IsEntryGateActive &&
                       _isInputWindowArmed &&
                       TryResolveNextNode(SkillComboCommand.Main, out _, out _);
            }
        }

        /// <summary>
        /// 확정 피해 기반 체인 타이밍에서 마무리 콤보 공격 입력을 받을 수 있는지 반환합니다.
        /// </summary>
        /// <remarks>
        /// 가드 유지 기반 마무리 입력을 사용하는 프로젝트 입력 규칙이 스킬 실행 중에도 유효한 Last 체인을
        /// 기존 로직으로 처리할 수 있도록 읽기 전용 상태만 제공합니다.
        /// </remarks>
        public bool CanAcceptSkillChainLastInput
        {
            get
            {
                ResetExpiredComboIfNeeded();
                return _state.IsActive &&
                       !_state.IsEntryGateActive &&
                       _isInputWindowArmed &&
                       TryResolveNextNode(SkillComboCommand.Last, out _, out _);
            }
        }

        /// <summary>
        /// 현재 공격 입력을 선입력 버퍼로 저장할 수 있는 콤보 진행 상태인지 반환합니다.
        /// </summary>
        public bool CanBufferComboInput
        {
            get
            {
                ResetExpiredComboIfNeeded();
                return _state.IsActive && !_isInputWindowArmed && CanResolveBufferedMainNode();
            }
        }

        /// <summary>
        /// 필요한 컴포넌트 참조를 초기화합니다.
        /// </summary>
        private void Awake()
        {
            CacheComponents();
        }

        /// <summary>
        /// 스킬 실행 종료 이벤트를 구독합니다.
        /// </summary>
        private void OnEnable()
        {
            CacheComponents();
            SubscribeSkillExecutor();
            SubscribeSkillChainReadyNotifier();
        }

        /// <summary>
        /// 스킬 실행 종료 이벤트 구독을 해제합니다.
        /// </summary>
        private void OnDisable()
        {
            UnsubscribeSkillExecutor();
            UnsubscribeSkillChainReadyNotifier();
        }

        /// <summary>
        /// 콤보 입력 가능 시간이 만료되었는지 매 프레임 확인합니다.
        /// </summary>
        private void Update()
        {
            ResetExpiredComboIfNeeded();
        }

        /// <summary>
        /// 외부에서 콤보 입력 대기 시간을 설정합니다.
        /// </summary>
        /// <param name="entryWindowSeconds">외부 진입 후 첫 입력을 기다릴 시간입니다. 0이면 만료 시간을 사용하지 않습니다.</param>
        /// <param name="chainWindowSeconds">스킬 종료 후 다음 입력을 기다릴 시간입니다. 0이면 스킬 종료 시 초기화합니다.</param>
        public void SetInputWindows(float entryWindowSeconds, float chainWindowSeconds)
        {
            entryInputWindowSeconds = Mathf.Max(0f, entryWindowSeconds);
            chainInputWindowSeconds = Mathf.Max(0f, chainWindowSeconds);
        }

        /// <summary>
        /// 현재 콤보 입력창의 만료 시간을 지정한 소유자가 해제할 때까지 보류합니다.
        /// </summary>
        /// <param name="owner">입력창 보류를 요청하는 소유자 객체입니다.</param>
        /// <returns>입력창 보류가 적용되었거나 이미 같은 소유자로 적용 중이면 <see langword="true"/>입니다.</returns>
        public bool HoldInputWindowExpiration(object owner)
        {
            if (owner == null)
            {
                return false;
            }

            ResetExpiredComboIfNeeded();
            if (!_state.IsActive || !_isInputWindowArmed)
            {
                return false;
            }

            if (_inputWindowHoldOwners.Contains(owner))
            {
                return true;
            }

            if (_inputWindowHoldOwners.Count == 0)
            {
                _heldInputWindowRemainingSeconds = _inputWindowExpireTime > 0f
                    ? Mathf.Max(0f, _inputWindowExpireTime - Time.time)
                    : InputWindowDisabledTime;
                _inputWindowExpireTime = InputWindowDisabledTime;
            }

            _inputWindowHoldOwners.Add(owner);
            return true;
        }

        /// <summary>
        /// 지정한 소유자가 보류 중이던 콤보 입력창 만료 시간을 다시 진행합니다.
        /// </summary>
        /// <param name="owner">입력창 보류를 해제할 소유자 객체입니다.</param>
        /// <param name="minRemainingSeconds">보류 해제 후 최소로 보장할 남은 입력 시간입니다.</param>
        public void ReleaseInputWindowExpirationHold(object owner, float minRemainingSeconds = 0f)
        {
            if (owner == null || !_inputWindowHoldOwners.Remove(owner))
            {
                return;
            }

            if (_inputWindowHoldOwners.Count > 0)
            {
                return;
            }

            float safeMinRemainingSeconds = Mathf.Max(0f, minRemainingSeconds);
            if (!_state.IsActive || !_isInputWindowArmed)
            {
                _heldInputWindowRemainingSeconds = InputWindowDisabledTime;
                return;
            }

            if (_heldInputWindowRemainingSeconds <= InputWindowDisabledTime)
            {
                _inputWindowExpireTime = safeMinRemainingSeconds > 0f
                    ? Time.time + safeMinRemainingSeconds
                    : InputWindowDisabledTime;
            }
            else
            {
                float remainingSeconds = Mathf.Max(_heldInputWindowRemainingSeconds, safeMinRemainingSeconds);
                _inputWindowExpireTime = remainingSeconds > 0f
                    ? Time.time + remainingSeconds
                    : Time.time;
            }

            _heldInputWindowRemainingSeconds = InputWindowDisabledTime;
        }

        /// <summary>
        /// 외부에서 콤보 장착 정보 제공자를 직접 지정합니다.
        /// </summary>
        /// <param name="provider">사용할 콤보 장착 정보 제공자입니다.</param>
        public void SetLoadoutProvider(IPlayerSkillComboLoadoutProvider provider)
        {
            _loadoutProvider = provider;
        }

        /// <summary>
        /// 기본 콤보 정의를 교체하고 현재 진행 중인 콤보 상태를 초기화합니다.
        /// </summary>
        /// <param name="definition">새로 사용할 기본 콤보 정의입니다.</param>
        public void SetDefaultComboDefinition(RuntimeSkillComboDefinition definition)
        {
            defaultComboDefinition = definition;
            ResetCombo();
        }

        /// <summary>
        /// 뒤늦게 부착되거나 교체된 런타임 의존성을 다시 확인하고 이벤트 구독을 갱신합니다.
        /// </summary>
        /// <remarks>
        /// 캐릭터 스폰 시 여러 패키지의 부트스트랩이 독립적으로 컴포넌트를 추가하므로,
        /// 이 컨트롤러가 <see cref="SkillExecutor"/> 또는 스킬 드라이버보다 먼저 활성화될 수 있습니다.
        /// 그 경우 첫 스킬 종료 이벤트를 받지 못해 다음 체인 입력 대기 시간이 열리지 않으므로,
        /// 외부 부트스트랩 또는 입력 처리 직전에 이 메서드로 참조와 구독 상태를 보정합니다.
        /// </remarks>
        public void RefreshRuntimeReferences()
        {
            CacheComponents();
            SubscribeSkillExecutor();
            SubscribeSkillChainReadyNotifier();
        }

        /// <summary>
        /// 현재 장착 상태 기준으로 HUD가 표시할 콤보 정의를 조회합니다.
        /// </summary>
        /// <param name="definition">HUD에 표시할 런타임 콤보 정의입니다.</param>
        /// <returns>사용 가능한 콤보 정의를 찾으면 <see langword="true"/>입니다.</returns>
        public bool TryGetComboDefinitionForUi(out RuntimeSkillComboDefinition definition)
        {
            return TryResolveComboDefinition(out definition, out _);
        }

        /// <summary>
        /// 현재 콤보 진행 상태를 초기화합니다.
        /// </summary>
        public void ResetCombo()
        {
            ResetComboInternal();
        }

        /// <summary>
        /// 진행 중인 콤보를 지정한 사유로 취소하고 UI 구독자에게 취소 상태를 알립니다.
        /// </summary>
        /// <param name="reason">콤보가 취소된 사유입니다.</param>
        public void CancelCombo(SkillComboCancelReason reason)
        {
            if (!_state.IsActive && !_isInputWindowArmed)
            {
                return;
            }

            SkillComboCancelEvent cancelEvent = CreateCancelEvent(reason);
            ResetComboInternal();
            NotifyComboCanceledForUi(cancelEvent);
        }

        /// <summary>
        /// 콤보 상태와 입력 대기 시간을 이벤트 없이 초기화합니다.
        /// </summary>
        private void ResetComboInternal()
        {
            _state.Reset();
            ResetCurrentSkillChainGate();
            ClearInputWindow();
            ClearBufferedMainInput();
        }

        /// <summary>
        /// 외부 전투 성공 이벤트를 트리거 위치로 보고 콤보 트리를 엽니다.
        /// 이 메서드는 스킬을 실행하지 않고, 다음 Main 또는 Last 입력이 진입 노드로 이어지도록 상태만 갱신합니다.
        /// </summary>
        /// <param name="entryTrigger">콤보를 열게 만든 외부 진입 조건입니다.</param>
        /// <param name="confirmedSkillUid">외부에서 성공이 확인된 스킬 UID입니다. 트리거 노드가 실제 콤보 노드가 아니므로 현재는 기록용 인자입니다.</param>
        /// <returns>콤보 열기 결과입니다.</returns>
        public SkillComboOpenResult TryOpenComboAtStart(
            SkillComboEntryTrigger entryTrigger,
            int confirmedSkillUid = 0)
        {
            if (entryTrigger == SkillComboEntryTrigger.None ||
                entryTrigger == SkillComboEntryTrigger.ManualSkillUse)
            {
                return SkillComboOpenResult.Fail(
                    SkillComboOpenFailReason.InvalidEntryTrigger,
                    entryTrigger);
            }

            if (!TryResolveComboDefinition(
                    out RuntimeSkillComboDefinition definition,
                    out SkillComboUseFailReason failReason))
            {
                return SkillComboOpenResult.Fail(
                    ConvertOpenFailReason(failReason),
                    entryTrigger);
            }

            if (!TryResolveOptionalEntryNode(
                    definition,
                    SkillComboCommand.Main,
                    out RuntimeSkillComboNode entryMainNode,
                    out failReason))
            {
                return SkillComboOpenResult.Fail(
                    ConvertOpenFailReason(failReason),
                    entryTrigger);
            }

            if (!TryResolveOptionalEntryNode(
                    definition,
                    SkillComboCommand.Last,
                    out RuntimeSkillComboNode entryLastNode,
                    out SkillComboUseFailReason lastFailReason))
            {
                return SkillComboOpenResult.Fail(
                    ConvertOpenFailReason(lastFailReason),
                    entryTrigger);
            }

            if (entryMainNode == null && entryLastNode == null)
            {
                return SkillComboOpenResult.Fail(
                    SkillComboOpenFailReason.MissingStartNode,
                    entryTrigger);
            }

            _state.OpenEntryGate(entryTrigger, confirmedSkillUid);
            ArmInputWindow(entryInputWindowSeconds);

            SkillComboOpenResult openResult = SkillComboOpenResult.Opened(entryMainNode, entryLastNode, entryTrigger);
            NotifyComboOpenedForUi(openResult);
            return openResult;
        }

        /// <summary>
        /// 콤보 진입 게이트에서 사용할 수 있는 선택형 진입 노드를 확인합니다.
        /// </summary>
        /// <remarks>
        /// 기본 콤보 마지막 타격 이후에는 Main 슬롯 없이 마무리 슬롯만 장착한 구성도 허용해야 합니다.
        /// 따라서 노드가 없는 상태는 실패가 아닌 선택지 없음으로 처리하고, 노드가 존재하지만 타입이 잘못된 경우만 실패로 반환합니다.
        /// </remarks>
        /// <param name="definition">확인할 콤보 정의입니다.</param>
        /// <param name="command">확인할 진입 명령입니다.</param>
        /// <param name="node">확인된 진입 노드입니다. 해당 명령의 진입 노드가 없으면 null입니다.</param>
        /// <param name="failReason">선택형 진입 노드 확인 중 발생한 실패 사유입니다.</param>
        /// <returns>진입 노드가 없거나 유효한 진입 노드가 있으면 true, 정의가 손상되었거나 노드 타입이 잘못되었으면 false입니다.</returns>
        private static bool TryResolveOptionalEntryNode(
            RuntimeSkillComboDefinition definition,
            SkillComboCommand command,
            out RuntimeSkillComboNode node,
            out SkillComboUseFailReason failReason)
        {
            if (SkillComboGraphResolver.TryResolveEntryNode(
                    definition,
                    command,
                    out node,
                    out failReason))
            {
                return true;
            }

            SkillComboUseFailReason missingReason = command == SkillComboCommand.Main
                ? SkillComboUseFailReason.MissingStartNode
                : SkillComboUseFailReason.MissingLastNode;

            if (failReason == missingReason)
            {
                node = null;
                failReason = SkillComboUseFailReason.None;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 현재 공격 입력을 다음 메인 콤보 스킬 선입력으로 저장합니다.
        /// </summary>
        /// <remarks>
        /// 콤보는 진행 중이지만 아직 체인 입력 창이 열리지 않은 상태에서 호출됩니다.
        /// 이후 확정 타격으로 체인 게이트가 열리거나, 확정 타격으로 열린 현재 스킬이 정상 종료되면 저장된 입력을 즉시 실행합니다.
        /// </remarks>
        /// <returns>공격 입력을 선입력으로 저장했으면 <see langword="true"/>입니다.</returns>
        public bool TryBufferMainInput()
        {
            if (ResetExpiredComboIfNeeded())
                return false;

            if (!_state.IsActive || _isInputWindowArmed || !CanResolveBufferedMainNode())
                return false;

            _hasBufferedMainInput = true;
            return true;
        }

        /// <summary>
        /// 메인 콤보 명령으로 스킬 사용을 시도합니다.
        /// </summary>
        /// <returns>콤보 명령 처리 결과입니다.</returns>
        public SkillComboUseResult TryUseMain()
        {
            return TryUseComboCommand(SkillComboCommand.Main);
        }

        /// <summary>
        /// 마무리 콤보 명령으로 스킬 사용을 시도합니다.
        /// </summary>
        /// <returns>콤보 명령 처리 결과입니다.</returns>
        public SkillComboUseResult TryUseLast()
        {
            return TryUseComboCommand(SkillComboCommand.Last);
        }

        /// <summary>
        /// 콤보 명령을 해석하고 타겟팅 컨텍스트를 자동으로 구성해 스킬 사용을 시도합니다.
        /// </summary>
        /// <param name="command">처리할 콤보 명령입니다.</param>
        /// <returns>콤보 명령 처리 결과입니다.</returns>
        public SkillComboUseResult TryUseComboCommand(SkillComboCommand command)
        {
            if (ResetExpiredComboIfNeeded())
                return SkillComboUseResult.Fail(SkillComboUseFailReason.Expired);

            if (!TryResolveNextNode(command, out RuntimeSkillComboNode node, out SkillComboUseFailReason failReason))
                return SkillComboUseResult.Fail(failReason);

            if (!TryBuildSkillRequest(node.SkillUid, out SkillDriverRequest request, out SkillUseFailReason skillFailReason))
                return SkillComboUseResult.Fail(SkillComboUseFailReason.MissingSkillRequest, skillFailReason);

            return TryUseResolvedNode(node, in request);
        }

        /// <summary>
        /// 콤보 명령을 해석하고 외부에서 전달한 스킬 요청 컨텍스트로 스킬 사용을 시도합니다.
        /// </summary>
        /// <param name="command">처리할 콤보 명령입니다.</param>
        /// <param name="request">스킬 실행에 사용할 요청 컨텍스트입니다.</param>
        /// <returns>콤보 명령 처리 결과입니다.</returns>
        public SkillComboUseResult TryUseComboCommand(SkillComboCommand command, in SkillDriverRequest request)
        {
            if (ResetExpiredComboIfNeeded())
                return SkillComboUseResult.Fail(SkillComboUseFailReason.Expired);

            if (!TryResolveNextNode(command, out RuntimeSkillComboNode node, out SkillComboUseFailReason failReason))
                return SkillComboUseResult.Fail(failReason);

            return TryUseResolvedNode(node, in request);
        }

        /// <summary>
        /// 현재 콤보 상태와 명령을 기준으로 실행할 다음 콤보 노드를 결정합니다.
        /// </summary>
        /// <param name="command">처리할 콤보 명령입니다.</param>
        /// <param name="node">실행할 콤보 노드입니다.</param>
        /// <param name="failReason">실패 시 콤보 해석 실패 이유입니다.</param>
        /// <returns>실행할 노드를 찾으면 true입니다.</returns>
        private bool TryResolveNextNode(
            SkillComboCommand command,
            out RuntimeSkillComboNode node,
            out SkillComboUseFailReason failReason)
        {
            node = null;

            if (!TryResolveComboDefinition(out RuntimeSkillComboDefinition definition, out failReason))
                return false;

            if (!_state.IsActive)
            {
                if (command != SkillComboCommand.Main)
                {
                    failReason = SkillComboUseFailReason.InvalidCommand;
                    return false;
                }

                return SkillComboGraphResolver.TryGetStartNode(definition, out node, out failReason);
            }

            if (_state.IsEntryGateActive)
            {
                return SkillComboGraphResolver.TryResolveEntryNode(
                    definition,
                    command,
                    out node,
                    out failReason);
            }

            return SkillComboGraphResolver.TryResolveNextNode(
                definition,
                _state.CurrentNodeIndex,
                command,
                out node,
                out failReason);
        }

        /// <summary>
        /// 현재 사용할 콤보 정의를 장착 정보 제공자 또는 기본 정의에서 가져옵니다.
        /// </summary>
        /// <param name="definition">사용할 콤보 정의입니다.</param>
        /// <param name="failReason">실패 시 콤보 해석 실패 이유입니다.</param>
        /// <returns>사용 가능한 콤보 정의를 찾으면 true입니다.</returns>
        private bool TryResolveComboDefinition(
            out RuntimeSkillComboDefinition definition,
            out SkillComboUseFailReason failReason)
        {
            ResolveLoadoutProvider();

            if (_loadoutProvider != null &&
                _loadoutProvider.TryBuildComboDefinition(out definition) &&
                definition != null)
            {
                failReason = SkillComboUseFailReason.None;
                return true;
            }

            if (defaultComboDefinition != null)
            {
                definition = defaultComboDefinition;
                failReason = SkillComboUseFailReason.None;
                return true;
            }

            definition = null;
            failReason = SkillComboUseFailReason.MissingDefinition;
            return false;
        }

        /// <summary>
        /// 결정된 콤보 노드의 스킬 UID로 실제 스킬 사용을 시도하고 콤보 상태를 갱신합니다.
        /// </summary>
        /// <param name="node">실행할 콤보 노드입니다.</param>
        /// <param name="request">스킬 실행에 사용할 요청 컨텍스트입니다.</param>
        /// <returns>콤보 명령 처리 결과입니다.</returns>
        private SkillComboUseResult TryUseResolvedNode(RuntimeSkillComboNode node, in SkillDriverRequest request)
        {
            RefreshRuntimeReferences();
            if (_skillDriver == null)
                return SkillComboUseResult.Fail(SkillComboUseFailReason.MissingSkillDriver);

            SkillExecutionOptions executionOptions = request.ExecutionOptions.Combine(node.ExecutionOptions);
            SkillDriverRequest resolvedRequest = request.WithExecutionOptions(executionOptions);
            SkillUseResult skillUseResult = _skillDriver.TryUseSkill(node.SkillUid, in resolvedRequest);
            if (!skillUseResult.IsStarted)
            {
                return SkillComboUseResult.Fail(
                    SkillComboUseFailReason.SkillUseRejected,
                    skillUseResult.FailReason);
            }

            ClearBufferedMainInput();
            ClearInputWindow();

            if (node.IsLast)
            {
                return FinishComboByLastSkill(node);
            }

            _state.Activate(node);
            ResetCurrentSkillChainGate();
            if (!CanContinueComboFromNode(node))
            {
                return FinishComboBecauseNoNextSkill(node);
            }

            SkillComboUseResult useResult = SkillComboUseResult.Started(node, false);
            NotifyComboSkillStartedForUi(useResult);
            return useResult;
        }

        /// <summary>
        /// 현재 메인 콤보 노드에서 이어갈 다음 스킬이 있는지 확인합니다.
        /// </summary>
        /// <remarks>
        /// Main 또는 Last 명령 중 하나라도 유효한 다음 노드로 해석되면 콤보 가능 상태를 유지합니다.
        /// 두 명령 모두 다음 노드가 없거나 손상된 연결이라면 더 받을 입력이 없으므로 즉시 종료 대상으로 판단합니다.
        /// </remarks>
        /// <param name="currentNode">현재 실행을 시작한 메인 콤보 노드입니다.</param>
        /// <returns>이어갈 수 있는 다음 콤보 스킬이 있으면 <see langword="true"/>입니다.</returns>
        private bool CanContinueComboFromNode(RuntimeSkillComboNode currentNode)
        {
            if (currentNode == null || currentNode.IsLast)
            {
                return false;
            }

            return CanContinueComboFromNodeIndex(currentNode.Index);
        }

        /// <summary>
        /// 현재 활성 콤보 상태에서 이어갈 다음 스킬이 있는지 확인합니다.
        /// </summary>
        /// <returns>현재 노드에서 이어갈 다음 콤보 스킬이 있으면 <see langword="true"/>입니다.</returns>
        private bool CanContinueComboFromCurrentState()
        {
            if (!_state.IsActive ||
                _state.IsEntryGateActive ||
                _state.CurrentNodeIndex == RuntimeSkillComboDefinition.InvalidNodeIndex)
            {
                return false;
            }

            return CanContinueComboFromNodeIndex(_state.CurrentNodeIndex);
        }

        /// <summary>
        /// 지정한 메인 콤보 노드 인덱스에서 실제로 이어갈 수 있는 다음 스킬이 있는지 확인합니다.
        /// </summary>
        /// <param name="currentNodeIndex">현재 메인 콤보 노드 인덱스입니다.</param>
        /// <returns>Main 또는 Last 명령으로 이동할 수 있는 다음 스킬이 있으면 <see langword="true"/>입니다.</returns>
        private bool CanContinueComboFromNodeIndex(int currentNodeIndex)
        {
            if (!TryResolveComboDefinition(out RuntimeSkillComboDefinition definition, out _))
            {
                return false;
            }

            return SkillComboGraphResolver.TryResolveNextNode(
                       definition,
                       currentNodeIndex,
                       SkillComboCommand.Main,
                       out _,
                       out _) ||
                   SkillComboGraphResolver.TryResolveNextNode(
                       definition,
                       currentNodeIndex,
                       SkillComboCommand.Last,
                       out _,
                       out _);
        }

        /// <summary>
        /// 다음 스킬이 없는 메인 콤보 노드를 시작한 직후 콤보 가능 상태를 종료합니다.
        /// </summary>
        /// <param name="node">실행을 시작했지만 이어갈 다음 스킬이 없는 메인 콤보 노드입니다.</param>
        /// <returns>콤보 종료가 반영된 스킬 사용 결과입니다.</returns>
        private SkillComboUseResult FinishComboBecauseNoNextSkill(RuntimeSkillComboNode node)
        {
            SkillComboUseResult useResult = SkillComboUseResult.Started(node, true);
            NotifyComboSkillStartedForUi(useResult);

            SkillComboCancelEvent cancelEvent = CreateCancelEvent(SkillComboCancelReason.NoNextSkill);
            ResetComboInternal();
            NotifyComboCanceledForUi(cancelEvent);
            return useResult;
        }

        /// <summary>
        /// 마무리 스킬 사용으로 콤보를 정상 종료하고 UI 구독자에게 완료 상태를 알립니다.
        /// </summary>
        /// <param name="lastNode">실행을 시작한 마무리 콤보 노드입니다.</param>
        /// <returns>마무리 스킬 사용 결과입니다.</returns>
        private SkillComboUseResult FinishComboByLastSkill(RuntimeSkillComboNode lastNode)
        {
            SkillComboUseResult useResult = SkillComboUseResult.Started(lastNode, true);
            NotifyComboSkillStartedForUi(useResult);
            ResetComboInternal();
            NotifyComboFinishedByLastSkillForUi(useResult);
            return useResult;
        }

        /// <summary>
        /// 현재 콤보 상태를 기준으로 취소 이벤트 데이터를 생성합니다.
        /// </summary>
        /// <param name="reason">콤보 취소 사유입니다.</param>
        /// <returns>UI에 전달할 콤보 취소 이벤트 데이터입니다.</returns>
        private SkillComboCancelEvent CreateCancelEvent(SkillComboCancelReason reason)
        {
            return new SkillComboCancelEvent(
                reason,
                _state.CurrentSkillUid,
                _state.CurrentNodeIndex,
                _state.EntryTrigger);
        }

        /// <summary>
        /// 콤보 진입 UI 이벤트를 발행합니다.
        /// </summary>
        /// <param name="result">콤보 열기 결과입니다.</param>
        private void NotifyComboOpenedForUi(SkillComboOpenResult result)
        {
            ComboOpenedForUi?.Invoke(result);
        }

        /// <summary>
        /// 콤보 스킬 시작 UI 이벤트를 발행합니다.
        /// </summary>
        /// <param name="result">콤보 스킬 사용 결과입니다.</param>
        private void NotifyComboSkillStartedForUi(SkillComboUseResult result)
        {
            ComboSkillStartedForUi?.Invoke(result);
        }

        /// <summary>
        /// 콤보 취소 UI 이벤트를 발행합니다.
        /// </summary>
        /// <param name="cancelEvent">콤보 취소 이벤트 데이터입니다.</param>
        private void NotifyComboCanceledForUi(SkillComboCancelEvent cancelEvent)
        {
            ComboCanceledForUi?.Invoke(cancelEvent);
        }

        /// <summary>
        /// 마무리 스킬 종료 UI 이벤트를 발행합니다.
        /// </summary>
        /// <param name="result">마무리 스킬 사용 결과입니다.</param>
        private void NotifyComboFinishedByLastSkillForUi(SkillComboUseResult result)
        {
            ComboFinishedByLastSkillForUi?.Invoke(result);
        }

        /// <summary>
        /// 지정한 시간 동안 다음 콤보 입력을 기다리도록 만료 시간을 설정합니다.
        /// </summary>
        /// <remarks>
        /// 공중 콤보 대시처럼 입력창 만료를 보류한 소유자가 있으면 실제 만료 시간은 진행하지 않고,
        /// 보류 해제 후 다시 적용할 남은 시간만 저장합니다.
        /// </remarks>
        /// <param name="windowSeconds">입력을 기다릴 시간입니다. 0 이하이면 만료를 사용하지 않습니다.</param>
        private void ArmInputWindow(float windowSeconds)
        {
            _isInputWindowArmed = true;

            if (_inputWindowHoldOwners.Count > 0)
            {
                _heldInputWindowRemainingSeconds = windowSeconds > 0f
                    ? windowSeconds
                    : InputWindowDisabledTime;
                _inputWindowExpireTime = InputWindowDisabledTime;
                return;
            }

            _inputWindowExpireTime = windowSeconds > 0f
                ? Time.time + windowSeconds
                : InputWindowDisabledTime;
        }

        /// <summary>
        /// 현재 설정된 콤보 입력 만료 시간을 해제합니다.
        /// </summary>
        private void ClearInputWindow()
        {
            _isInputWindowArmed = false;
            _inputWindowExpireTime = InputWindowDisabledTime;
            _inputWindowHoldOwners.Clear();
            _heldInputWindowRemainingSeconds = InputWindowDisabledTime;
        }

        /// <summary>
        /// 현재 실행 중인 메인 콤보 스킬에서 확정 타격으로 체인 게이트가 열렸는지 기록한 상태를 초기화합니다.
        /// </summary>
        private void ResetCurrentSkillChainGate()
        {
            _chainGateOpenedByConfirmedDamage = false;
        }

        /// <summary>
        /// 저장된 메인 콤보 선입력을 제거합니다.
        /// </summary>
        private void ClearBufferedMainInput()
        {
            _hasBufferedMainInput = false;
        }

        /// <summary>
        /// 체인 입력 창이 열린 상태라면 저장된 메인 콤보 선입력을 즉시 실행합니다.
        /// </summary>
        /// <returns>저장된 입력으로 다음 스킬 실행이 시작되면 <see langword="true"/>입니다.</returns>
        private bool TryConsumeBufferedMainInputIfReady()
        {
            if (!_hasBufferedMainInput || !CanAcceptComboInput)
                return false;

            _hasBufferedMainInput = false;
            SkillComboUseResult result = TryUseMain();
            return result.IsStarted;
        }

        /// <summary>
        /// 현재 콤보 상태에서 메인 선입력이 실제 다음 노드로 해석될 수 있는지 확인합니다.
        /// </summary>
        /// <returns>다음 메인 콤보 노드가 있으면 <see langword="true"/>입니다.</returns>
        private bool CanResolveBufferedMainNode()
        {
            return TryResolveNextNode(
                SkillComboCommand.Main,
                out _,
                out _);
        }

        /// <summary>
        /// 콤보 입력 대기 시간이 만료되었으면 콤보 상태를 초기화합니다.
        /// </summary>
        /// <returns>이번 호출에서 만료로 초기화되었으면 <see langword="true"/>입니다.</returns>
        private bool ResetExpiredComboIfNeeded()
        {
            if (!_state.IsActive || !_isInputWindowArmed || _inputWindowExpireTime <= 0f)
                return false;

            if (Time.time <= _inputWindowExpireTime)
                return false;

            CancelCombo(SkillComboCancelReason.Expired);
            return true;
        }

        /// <summary>
        /// 기존 콤보 해석 실패 이유를 외부 진입 열기 실패 이유로 변환합니다.
        /// </summary>
        /// <param name="failReason">콤보 해석 단계의 실패 이유입니다.</param>
        /// <returns>외부 진입 열기 결과에서 사용할 실패 이유입니다.</returns>
        private static SkillComboOpenFailReason ConvertOpenFailReason(SkillComboUseFailReason failReason)
        {
            return failReason switch
            {
                SkillComboUseFailReason.EmptyDefinition => SkillComboOpenFailReason.EmptyDefinition,
                SkillComboUseFailReason.MissingStartNode => SkillComboOpenFailReason.MissingStartNode,
                SkillComboUseFailReason.InvalidStartNode => SkillComboOpenFailReason.InvalidStartNode,
                SkillComboUseFailReason.InvalidTransition => SkillComboOpenFailReason.InvalidEntryLastNode,
                _ => SkillComboOpenFailReason.MissingDefinition,
            };
        }

        /// <summary>
        /// 스킬 UID에 맞는 타겟팅 요청을 구성합니다.
        /// </summary>
        /// <param name="skillUid">실행할 스킬 UID입니다.</param>
        /// <param name="request">구성된 스킬 요청 컨텍스트입니다.</param>
        /// <param name="skillFailReason">타겟팅 요청 구성 실패 이유입니다.</param>
        /// <returns>요청 컨텍스트를 구성하면 true입니다.</returns>
        private bool TryBuildSkillRequest(
            int skillUid,
            out SkillDriverRequest request,
            out SkillUseFailReason skillFailReason)
        {
            CacheTargetingProvider();

            if (_targetingProvider != null)
            {
                return _targetingProvider.TryBuildSkillRequest(
                    gameObject,
                    skillUid,
                    ConfigCommon.SkillTableSource.Player,
                    out request,
                    out skillFailReason);
            }

            request = BuildFallbackSkillRequest();
            skillFailReason = SkillUseFailReason.None;
            return true;
        }

        /// <summary>
        /// 별도 타겟팅 제공자가 없을 때 사용할 기본 스킬 요청 컨텍스트를 만듭니다.
        /// </summary>
        /// <returns>캐스터 위치와 현재 바라보는 방향을 기준으로 만든 요청 컨텍스트입니다.</returns>
        private SkillDriverRequest BuildFallbackSkillRequest()
        {
            return new SkillDriverRequest(
                lockedTarget: null,
                groundPoint: transform.position,
                forward: ResolveForward2D(),
                source: ConfigCommon.SkillTableSource.Player);
        }

        /// <summary>
        /// 캐릭터 상태 또는 Transform 기준으로 2D 전방 방향을 계산합니다.
        /// </summary>
        /// <returns>정규화된 2D 전방 방향입니다.</returns>
        private Vector2 ResolveForward2D()
        {
            var characterBase = GetComponent<CharacterBase>();
            if (characterBase != null)
            {
                Vector2 facing = CharacterConstants.FacingToVector2(characterBase.CurrentFacing);
                if (facing.sqrMagnitude > 1e-6f)
                    return facing.normalized;
            }

            Vector3 right = transform.right;
            Vector2 fallback = new Vector2(right.x, right.y);
            return fallback.sqrMagnitude < 1e-6f ? Vector2.right : fallback.normalized;
        }

        /// <summary>
        /// 필요한 런타임 컴포넌트 참조를 캐싱합니다.
        /// </summary>
        private void CacheComponents()
        {
            CacheSkillDriver();
            CacheTargetingProvider();
            ResolveLoadoutProvider();
        }

        /// <summary>
        /// 현재 오브젝트에서 스킬 실행 드라이버를 찾습니다.
        /// </summary>
        private void CacheSkillDriver()
        {
            _skillDriver ??= GetComponent<ICharacterSkillDriver>();
        }

        /// <summary>
        /// 현재 오브젝트에서 플레이어 타겟팅 제공자를 찾습니다.
        /// </summary>
        private void CacheTargetingProvider()
        {
            _targetingProvider ??= GetComponent<IPlayerSkillTargetingProvider>();
        }

        /// <summary>
        /// Inspector 또는 현재 오브젝트에서 콤보 장착 정보 제공자를 찾습니다.
        /// </summary>
        private void ResolveLoadoutProvider()
        {
            if (_loadoutProvider != null)
                return;

            if (loadoutProviderBehaviour is IPlayerSkillComboLoadoutProvider provider)
            {
                _loadoutProvider = provider;
                return;
            }

            _loadoutProvider = GetComponent<IPlayerSkillComboLoadoutProvider>();
        }

        /// <summary>
        /// 확정 타격으로 스킬 체인 가능 상태가 열렸을 때 받을 알림 포트를 갱신합니다.
        /// </summary>
        private void SubscribeSkillChainReadyNotifier()
        {
            ISkillChainReadyNotifier notifier = GetComponent<ISkillChainReadyNotifier>();
            if (ReferenceEquals(_chainReadyNotifier, notifier))
                return;

            UnsubscribeSkillChainReadyNotifier();
            _chainReadyNotifier = notifier;

            if (_chainReadyNotifier == null)
                return;

            _chainReadyNotifier.SkillChainReady += OnSkillChainReady;
            if (_chainReadyNotifier.IsSkillChainReady && _state.IsActive)
                OnSkillChainReady(_state.CurrentSkillUid);
        }

        /// <summary>
        /// 확정 타격 체인 가능 상태 알림 구독을 해제합니다.
        /// </summary>
        private void UnsubscribeSkillChainReadyNotifier()
        {
            if (_chainReadyNotifier == null)
                return;

            _chainReadyNotifier.SkillChainReady -= OnSkillChainReady;
            _chainReadyNotifier = null;
        }

        /// <summary>
        /// 확정 타격으로 현재 스킬의 체인 게이트가 열리면 현재 스킬이 종료될 때까지 콤보 입력 창을 엽니다.
        /// </summary>
        /// <remarks>
        /// 확정 타격 시점은 <c>UseClipTimingPolicy</c>와 무관한 체인 시작 기준입니다.
        /// <see cref="chainInputWindowSeconds"/>는 여기서 사용하지 않고, 현재 스킬이 정상 종료된 뒤 추가 입력 유예 시간으로만 사용합니다.
        /// </remarks>
        /// <param name="skillUid">확정 타격으로 체인을 연 현재 실행 스킬 UID입니다.</param>
        private void OnSkillChainReady(int skillUid)
        {
            if (!_state.IsActive || _state.IsEntryGateActive || skillUid != _state.CurrentSkillUid)
                return;

            if (!CanContinueComboFromCurrentState())
            {
                CancelCombo(SkillComboCancelReason.NoNextSkill);
                return;
            }

            _chainGateOpenedByConfirmedDamage = true;
            ArmInputWindow(0f);
            TryConsumeBufferedMainInputIfReady();
        }

        /// <summary>
        /// 스킬 실행 종료 이벤트를 구독할 실행기를 갱신합니다.
        /// </summary>
        private void SubscribeSkillExecutor()
        {
            var executor = GetComponent<SkillExecutor>();
            if (ReferenceEquals(_skillExecutor, executor))
                return;

            UnsubscribeSkillExecutor();
            _skillExecutor = executor;

            if (_skillExecutor != null)
                _skillExecutor.ExecutionFinished += OnSkillExecutionFinished;
        }

        /// <summary>
        /// 스킬 실행 종료 이벤트 구독을 해제합니다.
        /// </summary>
        private void UnsubscribeSkillExecutor()
        {
            if (_skillExecutor == null)
                return;

            _skillExecutor.ExecutionFinished -= OnSkillExecutionFinished;
            _skillExecutor = null;
        }

        /// <summary>
        /// 현재 메인 콤보 스킬이 종료되면 다음 입력 대기 시간을 열거나 콤보를 초기화합니다.
        /// </summary>
        /// <param name="report">스킬 실행 종료 리포트입니다.</param>
        private void OnSkillExecutionFinished(SkillExecutionReport report)
        {
            if (!_state.IsActive || report.SkillUid != _state.CurrentSkillUid)
                return;

            if (report.State == MonsterSkillExecutionState.Canceled &&
                report.CancelReason == SkillCancelReason.ComboChain)
            {
                return;
            }

            if (report.State != MonsterSkillExecutionState.Succeeded)
            {
                CancelCombo(SkillComboCancelReason.SkillExecutionFailed);
                return;
            }

            if (!CanContinueComboFromCurrentState())
            {
                CancelCombo(SkillComboCancelReason.NoNextSkill);
                return;
            }

            if (!_chainGateOpenedByConfirmedDamage)
            {
                CancelCombo(SkillComboCancelReason.ChainInputNotUnlockedByConfirmedDamage);
                return;
            }

            if (chainInputWindowSeconds <= 0f)
            {
                CancelCombo(SkillComboCancelReason.ChainInputWindowDisabled);
                return;
            }

            ResetCurrentSkillChainGate();
            ArmInputWindow(chainInputWindowSeconds);
            TryConsumeBufferedMainInputIfReady();
        }
    }
}
