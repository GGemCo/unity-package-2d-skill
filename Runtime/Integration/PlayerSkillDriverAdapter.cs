using System;
using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Core의 공용 스킬 드라이버 호출을 <see cref="SkillExecutor"/> 기반 플레이어 스킬 실행 흐름으로 연결하는 어댑터입니다.
    /// 플레이어 스킬 UID를 기준으로 실행 가능 여부와 내부 쿨다운을 관리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerSkillDriverAdapter : MonoBehaviour, ISkillCancelableDriver, ICharacterSkillBundleDriver, IIncomingHitCombatFeedbackSink, IIncomingHitActionCanceler, ISkillChainReadyNotifier, IPlayerSkillInputStateProvider
    {
        /// <summary>
        /// 실제 스킬 실행을 담당하는 런타임 실행기입니다.
        /// </summary>
        private SkillExecutor _executor;
        private CharacterBase _character;
        private ISkillStartActionCanceler _skillStartActionCanceler;
        private IForcedSkillStartActionCanceler _forcedSkillStartActionCanceler;
        private ISkillChainReadyFeedback _skillChainReadyFeedback;

        /// <summary>
        /// 스킬 UID별 다음 사용 가능 시각을 저장합니다.
        /// </summary>
        private readonly Dictionary<int, float> _cooldownReadyAt = new();

        /// <summary>
        /// 현재 스킬 실행기가 다른 스킬을 처리 중인지 여부를 반환합니다.
        /// </summary>
        public bool IsSkillBusy => _executor != null && _executor.IsBusy;

        private int _currentRunningSkillUid;
        private bool _chainUnlockedByConfirmedDamage;
        private bool _chainConsumed;

        /// <summary>
        /// 현재 실행 중인 스킬이 확정 타격으로 다음 스킬 체인을 받을 수 있을 때 발생합니다.
        /// </summary>
        public event Action<int> SkillChainReady;

        /// <summary>
        /// 현재 실행 중인 스킬을 다음 스킬로 체인 취소할 수 있는 상태인지 반환합니다.
        /// </summary>
        public bool IsSkillChainReady => CanStartNextSkillByConfirmedDamage();

        /// <summary>
        /// 컴포넌트 초기화 시 동일한 게임 오브젝트에서 <see cref="SkillExecutor"/>를 찾아 연결합니다.
        /// </summary>
        private void Awake()
        {
            _character = GetComponent<CharacterBase>();
            _skillStartActionCanceler = GetComponent<ISkillStartActionCanceler>();
            _forcedSkillStartActionCanceler = GetComponent<IForcedSkillStartActionCanceler>();
            _skillChainReadyFeedback = GetComponentInChildren<ISkillChainReadyFeedback>(true);

            if (_executor == null)
                SetSkillExecutor(GetComponent<SkillExecutor>());
            else
                SetSkillExecutor(_executor);
        }

        private void OnDestroy()
        {
            if (_executor != null)
                _executor.ExecutionFinished -= OnExecutionFinished;
        }

        /// <summary>
        /// 외부에서 사용할 <see cref="SkillExecutor"/> 인스턴스를 설정합니다.
        /// </summary>
        /// <param name="value">이 어댑터가 사용할 스킬 실행기입니다.</param>
        public void SetSkillExecutor(SkillExecutor value)
        {
            if (ReferenceEquals(_executor, value))
                return;

            if (_executor != null)
                _executor.ExecutionFinished -= OnExecutionFinished;

            _executor = value;

            if (_executor != null)
                _executor.ExecutionFinished += OnExecutionFinished;
        }

        /// <summary>
        /// 드라이버 요청 정보를 바탕으로 플레이어 스킬 사용을 시도합니다.
        /// 실행기 상태, 입력값, 쿨다운, 스킬 정의, 타겟 조건을 검증한 뒤 실행을 요청합니다.
        /// </summary>
        /// <param name="skillUid">사용할 플레이어 스킬의 고유 식별자입니다.</param>
        /// <param name="request">타겟, 지면 위치, 방향, 테이블 출처를 포함한 스킬 요청 정보입니다.</param>
        /// <returns>스킬 실행이 시작되면 <see cref="SkillUseResult.Started"/>, 실행할 수 없으면 <see cref="SkillUseResult.Rejected"/>를 반환합니다.</returns>
        public SkillUseResult TryUseSkill(int skillUid, in SkillDriverRequest request)
        {
            if (request.Source != ConfigCommon.SkillTableSource.Player)
                return SkillUseResult.Fail(SkillUseFailReason.InvalidSource);

            if (_executor == null || skillUid <= 0)
                return SkillUseResult.Fail(SkillUseFailReason.InvalidInput);

            SkillActivationOptions activationOptions = request.ActivationOptions;
            if (_character != null && _character.IsStatusDead())
                return SkillUseResult.Fail(SkillUseFailReason.ControlLocked);

            bool canStopCrowdControl =
                activationOptions.StopCrowdControlOnStart &&
                _character != null &&
                _character.HasActiveOrQueuedCrowdControl();
            bool canStopHitStop =
                activationOptions.CancelAllActionsOnStart &&
                _forcedSkillStartActionCanceler != null &&
                _character != null &&
                _character.HitStopController.IsActive;
            bool canBypassControlLock =
                activationOptions.AllowWhileControlLocked &&
                (canStopCrowdControl || canStopHitStop);
            if (_character != null &&
                _character.IsDontControl() &&
                !canBypassControlLock)
            {
                return SkillUseResult.Fail(SkillUseFailReason.ControlLocked);
            }

            // 스킬 UID 기준 내부 쿨다운이 남아 있으면 사용을 거부합니다.
            if (_cooldownReadyAt.TryGetValue(skillUid, out float readyAt) && Time.time < readyAt)
                return SkillUseResult.Fail(SkillUseFailReason.Cooldown);

            // 플레이어 스킬 정의를 조회할 수 없으면 실행하지 않습니다.
            if (!SkillDefinitionResolver.TryResolve(skillUid, ConfigCommon.SkillTableSource.Player, out var skill) || skill == null)
                return SkillUseResult.Fail(SkillUseFailReason.InvalidDefinition);

            // LockOnGuaranteedHit 모드는 잠금 대상이 반드시 필요합니다.
            if (RequiresLockedTarget(skill) && request.LockedTarget == null)
                return SkillUseResult.Fail(SkillUseFailReason.NoTarget);

            SkillTargetContext ctx = BuildSkillTargetContext(skill, request);

            if (!SkillRangeResolver.IsWithinCastRange(skill, gameObject, ctx))
                return SkillUseResult.Fail(SkillUseFailReason.OutOfRange);

            if (!HasEnoughMp(skill))
                return SkillUseResult.Fail(SkillUseFailReason.InsufficientMp);

            bool shouldInterruptRunningSkill =
                _executor.IsBusy && activationOptions.InterruptRunningSkill;
            bool shouldAttemptChainCancel =
                _executor.IsBusy &&
                !shouldInterruptRunningSkill &&
                CanStartNextSkillByConfirmedDamage();
            if (_executor.IsBusy && !shouldInterruptRunningSkill && !shouldAttemptChainCancel)
                return SkillUseResult.Fail(SkillUseFailReason.Busy);

            if (shouldInterruptRunningSkill)
            {
                if (!_executor.TryCancel(SkillCancelReason.ForcedBySystem))
                    return SkillUseResult.Fail(SkillUseFailReason.ExecutionRejected);

                _skillChainReadyFeedback?.StopSkillChainReady();
            }
            else if (shouldAttemptChainCancel)
            {
                if (!_executor.TryCancel(SkillCancelReason.ComboChain))
                    return SkillUseResult.Fail(SkillUseFailReason.ExecutionRejected);

                _chainConsumed = true;
                _skillChainReadyFeedback?.StopSkillChainReady();
            }

            // MP·쿨다운·타겟 조건 검증과 실행 중 스킬 정리가 모두 끝난 뒤에만 CC를 해제합니다.
            // 검증 실패만으로 긴급 탈출 효과가 적용되는 것을 방지하기 위한 순서입니다.
            if (activationOptions.StopCrowdControlOnStart)
            {
                _character?.TryStopCrowdControl(
                    CrowdControlStopReason.Manual,
                    isEndCharacterStop: true);
            }

            CancelActionsBeforeSkillStart(in activationOptions);

            bool started = _executor.TryUse(skillUid, ctx, ConfigCommon.SkillTableSource.Player);
            if (!started)
                return SkillUseResult.Fail(SkillUseFailReason.ExecutionRejected);

            SpendMp(skill);

            float cd = Mathf.Max(0f, skill.CoolTime);
            if (cd > 0f)
                _cooldownReadyAt[skillUid] = Time.time + cd;

            _currentRunningSkillUid = skillUid;
            _chainUnlockedByConfirmedDamage = false;
            _chainConsumed = false;

            return SkillUseResult.Started;
        }

        /// <summary>
        /// 스킬 발동 정책에 따라 일반 액션 취소 또는 모든 행동 강제 취소를 실행합니다.
        /// </summary>
        /// <param name="activationOptions">현재 스킬 요청에 적용된 발동 정책입니다.</param>
        private void CancelActionsBeforeSkillStart(in SkillActivationOptions activationOptions)
        {
            if (activationOptions.CancelAllActionsOnStart &&
                _forcedSkillStartActionCanceler != null)
            {
                _forcedSkillStartActionCanceler.CancelAllActionsOnForcedSkillStart();
                return;
            }

            // 강제 취소 구현이 없는 구성에서도 기존 스킬 시작 정리 정책은 유지합니다.
            _skillStartActionCanceler?.CancelActionsOnSkillStart();
        }


        /// <summary>
        /// 여러 플레이어 스킬을 하나의 대표 애니메이션으로 묶어 실행합니다.
        /// </summary>
        /// <param name="request">묶음 실행에 포함할 스킬과 공통 타겟팅 정보입니다.</param>
        /// <returns>묶음 실행이 시작되면 <see cref="SkillUseResult.Started"/>입니다.</returns>
        public SkillUseResult TryUseSkillBundle(in SkillBundleUseRequest request)
        {
            if (request.Request.Source != ConfigCommon.SkillTableSource.Player)
                return SkillUseResult.Fail(SkillUseFailReason.InvalidSource);

            if (_executor == null || request.Entries == null || request.Entries.Count == 0)
                return SkillUseResult.Fail(SkillUseFailReason.InvalidInput);

            if (_character != null && (_character.IsStatusDead() || _character.IsDontControl()))
                return SkillUseResult.Fail(SkillUseFailReason.ControlLocked);

            if (_executor.IsBusy)
                return SkillUseResult.Fail(SkillUseFailReason.Busy);

            var runtimeEntries = new List<SkillBundleRuntimeEntry>();
            int totalNeedMp = 0;
            for (int i = 0; i < request.Entries.Count; i++)
            {
                SkillBundleSkillEntry entry = request.Entries[i];
                if (entry.SkillUid <= 0)
                    return SkillUseResult.Fail(SkillUseFailReason.InvalidInput);

                if (_cooldownReadyAt.TryGetValue(entry.SkillUid, out float readyAt) && Time.time < readyAt)
                    return SkillUseResult.Fail(SkillUseFailReason.Cooldown);

                if (!SkillDefinitionResolver.TryResolve(entry.SkillUid, ConfigCommon.SkillTableSource.Player, out var skill) || skill == null)
                    return SkillUseResult.Fail(SkillUseFailReason.InvalidDefinition);

                SkillExecutionOptions executionOptions = request.Request.ExecutionOptions.Combine(entry.ExecutionOptions);
                SkillTargetContext context = BuildSkillTargetContext(skill, request.Request.WithExecutionOptions(executionOptions));

                if (RequiresLockedTarget(skill) && request.Request.LockedTarget == null)
                    return SkillUseResult.Fail(SkillUseFailReason.NoTarget);

                if (!SkillRangeResolver.IsWithinCastRange(skill, gameObject, context))
                    return SkillUseResult.Fail(SkillUseFailReason.OutOfRange);

                totalNeedMp += Mathf.Max(0, skill.NeedMp);
                runtimeEntries.Add(new SkillBundleRuntimeEntry(skill, context));
            }

            if (!HasEnoughMp(totalNeedMp))
                return SkillUseResult.Fail(SkillUseFailReason.InsufficientMp);

            int resolvedPrimarySkillUid = ResolvePrimarySkillUid(runtimeEntries, request.PrimarySkillUid);
            if (resolvedPrimarySkillUid <= 0)
                return SkillUseResult.Fail(SkillUseFailReason.InvalidInput);

            _skillStartActionCanceler?.CancelActionsOnSkillStart();

            bool started = _executor.TryUseBundle(runtimeEntries, resolvedPrimarySkillUid);
            if (!started)
                return SkillUseResult.Fail(SkillUseFailReason.ExecutionRejected);

            SpendMp(totalNeedMp);
            ApplyBundleCooldowns(runtimeEntries);

            _currentRunningSkillUid = resolvedPrimarySkillUid;
            _chainUnlockedByConfirmedDamage = false;
            _chainConsumed = false;

            return SkillUseResult.Started;
        }


        /// <summary>
        /// 요청된 대표 스킬 UID가 묶음에 포함되어 있는지 확인하고, 없으면 첫 번째 유효 스킬을 대표로 사용합니다.
        /// </summary>
        /// <param name="runtimeEntries">검증된 묶음 실행 항목입니다.</param>
        /// <param name="requestedPrimarySkillUid">요청된 대표 스킬 UID입니다.</param>
        /// <returns>실제로 사용할 대표 스킬 UID입니다.</returns>
        private static int ResolvePrimarySkillUid(
            IReadOnlyList<SkillBundleRuntimeEntry> runtimeEntries,
            int requestedPrimarySkillUid)
        {
            if (runtimeEntries == null || runtimeEntries.Count == 0)
                return 0;

            for (int i = 0; i < runtimeEntries.Count; i++)
            {
                RuntimeSkillDefinition skill = runtimeEntries[i].Skill;
                if (skill != null && skill.Uid == requestedPrimarySkillUid)
                    return skill.Uid;
            }

            for (int i = 0; i < runtimeEntries.Count; i++)
            {
                RuntimeSkillDefinition skill = runtimeEntries[i].Skill;
                if (skill != null)
                    return skill.Uid;
            }

            return 0;
        }

        /// <summary>
        /// 스킬 정의와 요청 정보를 실제 실행 컨텍스트로 변환합니다.
        /// </summary>
        /// <param name="skill">실행할 스킬 정의입니다.</param>
        /// <param name="request">타겟팅 요청입니다.</param>
        /// <returns>SkillExecutor에 전달할 실행 컨텍스트입니다.</returns>
        private SkillTargetContext BuildSkillTargetContext(RuntimeSkillDefinition skill, in SkillDriverRequest request)
        {
            return new SkillTargetContext(
                caster: gameObject,
                lockedTarget: request.LockedTarget != null ? request.LockedTarget.gameObject : null,
                groundPoint: request.GroundPoint,
                forward: new Vector3(request.Forward.x, request.Forward.y, 0f),
                executionOptions: ResolveExecutionOptions(skill, request.ExecutionOptions));
        }

        /// <summary>
        /// 스킬이 락온 대상을 반드시 필요로 하는지 확인합니다.
        /// </summary>
        /// <param name="skill">검사할 스킬 정의입니다.</param>
        /// <returns>락온 대상이 필요하면 <see langword="true"/>입니다.</returns>
        private static bool RequiresLockedTarget(RuntimeSkillDefinition skill)
        {
            if (skill == null)
                return false;

            var mode = (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);
            return mode == ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit;
        }

        /// <summary>
        /// 묶음 실행에 포함된 모든 스킬에 쿨다운을 적용합니다.
        /// </summary>
        /// <param name="runtimeEntries">실행을 시작한 스킬 목록입니다.</param>
        private void ApplyBundleCooldowns(IReadOnlyList<SkillBundleRuntimeEntry> runtimeEntries)
        {
            if (runtimeEntries == null)
                return;

            for (int i = 0; i < runtimeEntries.Count; i++)
            {
                RuntimeSkillDefinition skill = runtimeEntries[i].Skill;
                if (skill == null)
                    continue;

                float cd = Mathf.Max(0f, skill.CoolTime);
                if (cd > 0f)
                    _cooldownReadyAt[skill.Uid] = Time.time + cd;
            }
        }

        /// <summary>
        /// 요청으로 전달된 실행 옵션에 플레이어의 현재 상태 기반 보너스를 병합합니다.
        /// </summary>
        /// <param name="skill">이번에 실행할 플레이어 스킬 정의입니다.</param>
        /// <param name="requestOptions">입력/콤보 계층에서 전달한 실행 옵션입니다.</param>
        /// <returns>이번 스킬 실행에 적용할 최종 옵션 스냅샷입니다.</returns>
        private SkillExecutionOptions ResolveExecutionOptions(
            RuntimeSkillDefinition skill,
            in SkillExecutionOptions requestOptions)
        {
            SkillExecutionOptions resolved = SkillExecutionOptions.None.Combine(requestOptions);
            float airborneDamageMultiplier = ResolveAirborneDamageMultiplier(skill);
            if (airborneDamageMultiplier <= 1f)
                return resolved;

            if (!IsCasterAirborneAtSkillStart())
                return resolved;

            // 공중 사용 보너스는 스킬 시작 시점에 스냅샷으로 고정해 타격 시점 착지 여부에 흔들리지 않게 합니다.
            var airborneBonus = new SkillExecutionOptions(airborneDamageMultiplier, 0f, 0L);
            return resolved.Combine(airborneBonus);
        }

        /// <summary>
        /// 스킬 정의에 설정된 공중 사용 데미지 배율을 안전한 범위로 보정합니다.
        /// </summary>
        /// <remarks>
        /// 1 이하는 보너스를 적용하지 않는 값으로 다루어 기존 요청 옵션만 유지합니다.
        /// 테이블 값이 비어 있거나 잘못 들어와도 모든 스킬에 공중 보너스가 퍼지지 않도록 여기에서 한 번 더 방어합니다.
        /// </remarks>
        /// <param name="skill">확인할 플레이어 스킬 정의입니다.</param>
        /// <returns>공중 사용 시 추가로 적용할 데미지 배율입니다.</returns>
        private static float ResolveAirborneDamageMultiplier(RuntimeSkillDefinition skill)
        {
            if (skill == null || skill.AirborneDamageMultiplier <= 1f)
                return 1f;

            return skill.AirborneDamageMultiplier;
        }

        /// <summary>
        /// 스킬 시작 시점의 캐스터 공중 상태를 확인합니다.
        /// </summary>
        /// <returns>캐스터가 지면에 닿아 있지 않으면 true입니다.</returns>
        private bool IsCasterAirborneAtSkillStart()
        {
            return _character != null && !_character.IsCurrentlyGrounded();
        }

        public SkillUseResult TryUseSkill(int skillUid, in MonsterSkillTarget target)
        {
            var request = new SkillDriverRequest(target, ConfigCommon.SkillTableSource.Player);
            return TryUseSkill(skillUid, in request);
        }


        /// <summary>
        /// 스킬 정의 기준으로 현재 캐릭터가 필요한 MP를 지불할 수 있는지 확인합니다.
        /// </summary>
        /// <param name="skill">MP 비용을 확인할 플레이어 스킬 정의입니다.</param>
        /// <returns>스킬 정의가 유효하지 않거나 MP가 충분하면 <see langword="true"/>입니다.</returns>
        private bool HasEnoughMp(RuntimeSkillDefinition skill)
        {
            if (skill == null || skill.NeedMp <= 0)
                return true;

            return HasEnoughMp(skill.NeedMp);
        }

        /// <summary>
        /// 지정한 MP 합산 비용을 현재 캐릭터가 지불할 수 있는지 확인합니다.
        /// </summary>
        /// <param name="needMp">필요 MP 합계입니다.</param>
        /// <returns>MP가 충분하거나 개발용 MP 비용 무시 옵션이 켜져 있으면 <see langword="true"/>입니다.</returns>
        private bool HasEnoughMp(int needMp)
        {
            if (needMp <= 0)
                return true;

#if GGEMCO_ENABLE_CHEAT_TOOLS
            // MP 비용 무시 옵션은 개발 환경에서만 컴파일되어 릴리즈 빌드에는 포함되지 않습니다.
            if (ShouldIgnorePlayerSkillMpCost())
                return true;
#endif

            if (_character == null)
                return true;

            return _character.CheckNeedMp(needMp);
        }

        /// <summary>
        /// 스킬 정의에 설정된 MP 비용을 차감합니다.
        /// </summary>
        /// <param name="skill">MP 비용을 차감할 플레이어 스킬 정의입니다.</param>
        private void SpendMp(RuntimeSkillDefinition skill)
        {
            if (skill == null || skill.NeedMp <= 0)
                return;

            SpendMp(skill.NeedMp);
        }

        /// <summary>
        /// 지정한 MP 합산 비용을 한 번만 차감합니다.
        /// </summary>
        /// <param name="needMp">차감할 MP 합계입니다.</param>
        private void SpendMp(int needMp)
        {
            if (needMp <= 0)
                return;

#if GGEMCO_ENABLE_CHEAT_TOOLS
            // 테스트 중 MP 비용을 무시하는 경우, 비용 검사뿐 아니라 실제 차감도 건너뜁니다.
            if (ShouldIgnorePlayerSkillMpCost())
                return;
#endif

            _character?.MinusMp(needMp);
        }

#if GGEMCO_ENABLE_CHEAT_TOOLS
        /// <summary>
        /// 개발 환경에서 플레이어 스킬 MP 비용 무시 옵션이 활성화되어 있는지 확인합니다.
        /// </summary>
        /// <returns>플레이어 스킬 MP 비용 검사와 차감을 건너뛰어야 하면 <see langword="true"/>입니다.</returns>
        private static bool ShouldIgnorePlayerSkillMpCost()
        {
            AddressableLoaderSettingsSkill loader = AddressableLoaderSettingsSkill.Instance;
            GGemCoSkillSettings settings = loader != null ? loader.skillSettings : null;
            return settings != null && settings.IgnorePlayerSkillMpCost;
        }
#endif

        public bool RequestCancelSkill(SkillCancelReason reason)
        {
            if (_executor == null)
                return false;

            return _executor.TryCancel(reason);
        }

        public void NotifyIncomingHitResolved(in IncomingHitCombatFeedback feedback)
        {
            if (!IsSkillChainOnConfirmedDamageEnabled())
                return;

            if (_executor == null || feedback.SkillUid <= 0 || feedback.AttackId <= 0)
                return;

            if (feedback.Outcome != MonsterSkillCombatOutcome.Hit)
                return;

            if (_currentRunningSkillUid <= 0 || feedback.SkillUid != _currentRunningSkillUid)
                return;

            if (!_executor.IsChainUnlockAttack(feedback.AttackId))
                return;

            if (_chainUnlockedByConfirmedDamage)
                return;

            _chainUnlockedByConfirmedDamage = true;
            _skillChainReadyFeedback?.PlaySkillChainReady();
            NotifySkillChainReady(_currentRunningSkillUid);
        }

        private void OnExecutionFinished(SkillExecutionReport report)
        {
            if (_currentRunningSkillUid > 0 && report.SkillUid != _currentRunningSkillUid)
                return;

            ResetChainState();
        }

        /// <summary>
        /// 확정 타격으로 스킬 체인 입력 가능 상태가 열렸음을 외부 구독자에게 알립니다.
        /// </summary>
        /// <param name="skillUid">현재 실행 중인 스킬 UID입니다.</param>
        private void NotifySkillChainReady(int skillUid)
        {
            if (skillUid <= 0)
                return;

            SkillChainReady?.Invoke(skillUid);
        }

        private bool CanStartNextSkillByConfirmedDamage()
        {
            return IsSkillChainOnConfirmedDamageEnabled()
                   && _chainUnlockedByConfirmedDamage
                   && !_chainConsumed
                   && _currentRunningSkillUid > 0;
        }

        private static bool IsSkillChainOnConfirmedDamageEnabled()
        {
            var loader = AddressableLoaderSettingsSkill.Instance;
            var settings = loader != null ? loader.skillSettings : null;
            return settings != null && settings.enableSkillChainOnConfirmedDamage;
        }

        private void ResetChainState()
        {
            _skillChainReadyFeedback?.StopSkillChainReady();

            _currentRunningSkillUid = 0;
            _chainUnlockedByConfirmedDamage = false;
            _chainConsumed = false;
        }

        public void CancelActionsOnIncomingHit(IncomingHitCancelReason reason)
        {
            if (_executor == null)
                SetSkillExecutor(GetComponent<SkillExecutor>());

            if (_executor == null || !_executor.IsBusy)
                return;

            SkillCancelReason cancelReason = reason switch
            {
                IncomingHitCancelReason.Death => SkillCancelReason.Death,
                IncomingHitCancelReason.Damage => SkillCancelReason.Damage,
                _ => SkillCancelReason.ForcedBySystem
            };

            if (_executor.TryApplyIncomingHitToChargeGauge(cancelReason))
            {
                return;
            }

            if (_executor.TryCancel(cancelReason))
            {
                // 체인 상태/UI가 있다면 여기서 즉시 정리하거나,
                // 기존 ExecutionFinished 콜백에서 정리되도록 유지
            }
        }
    }
}
