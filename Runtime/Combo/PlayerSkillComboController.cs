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
        private SkillExecutor _skillExecutor;
        private bool _isInputWindowArmed;
        private float _inputWindowExpireTime = InputWindowDisabledTime;

        /// <summary>
        /// 현재 플레이어 콤보 진행 상태입니다.
        /// </summary>
        public SkillComboState State => _state;

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
        /// 현재 공격 입력으로 다음 콤보 스킬을 받을 수 있는 상태인지 반환합니다.
        /// </summary>
        /// <remarks>
        /// 스킬끼리 이어지는 B안은 이전 스킬이 끝난 뒤에만 다음 입력을 받기 때문에,
        /// 스킬 실행 중에는 콤보 상태가 살아 있어도 입력 대기 상태로 보지 않습니다.
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
        }

        /// <summary>
        /// 스킬 실행 종료 이벤트 구독을 해제합니다.
        /// </summary>
        private void OnDisable()
        {
            UnsubscribeSkillExecutor();
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
        /// 현재 콤보 진행 상태를 초기화합니다.
        /// </summary>
        public void ResetCombo()
        {
            _state.Reset();
            ClearInputWindow();
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

            if (!SkillComboGraphResolver.TryResolveEntryNode(
                    definition,
                    SkillComboCommand.Main,
                    out RuntimeSkillComboNode entryMainNode,
                    out failReason))
            {
                return SkillComboOpenResult.Fail(
                    ConvertOpenFailReason(failReason),
                    entryTrigger);
            }

            RuntimeSkillComboNode entryLastNode = null;
            if (!SkillComboGraphResolver.TryResolveEntryNode(
                    definition,
                    SkillComboCommand.Last,
                    out entryLastNode,
                    out SkillComboUseFailReason lastFailReason) &&
                lastFailReason != SkillComboUseFailReason.MissingLastNode)
            {
                return SkillComboOpenResult.Fail(
                    ConvertOpenFailReason(lastFailReason),
                    entryTrigger);
            }

            _state.OpenEntryGate(entryTrigger, confirmedSkillUid);
            ArmInputWindow(entryInputWindowSeconds);
            return SkillComboOpenResult.Opened(entryMainNode, entryLastNode, entryTrigger);
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
            CacheSkillDriver();
            if (_skillDriver == null)
                return SkillComboUseResult.Fail(SkillComboUseFailReason.MissingSkillDriver);

            SkillUseResult skillUseResult = _skillDriver.TryUseSkill(node.SkillUid, in request);
            if (!skillUseResult.IsStarted)
            {
                return SkillComboUseResult.Fail(
                    SkillComboUseFailReason.SkillUseRejected,
                    skillUseResult.FailReason);
            }

            ClearInputWindow();

            bool comboEnded = node.IsLast;
            if (comboEnded)
                ResetCombo();
            else
                _state.Activate(node);

            return SkillComboUseResult.Started(node, comboEnded);
        }

        /// <summary>
        /// 지정한 시간 동안 다음 콤보 입력을 기다리도록 만료 시간을 설정합니다.
        /// </summary>
        /// <param name="windowSeconds">입력을 기다릴 시간입니다. 0 이하이면 만료를 사용하지 않습니다.</param>
        private void ArmInputWindow(float windowSeconds)
        {
            _isInputWindowArmed = true;
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

            ResetCombo();
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

            if (report.State != MonsterSkillExecutionState.Succeeded)
            {
                ResetCombo();
                return;
            }

            if (chainInputWindowSeconds <= 0f)
            {
                ResetCombo();
                return;
            }

            ArmInputWindow(chainInputWindowSeconds);
        }
    }
}
