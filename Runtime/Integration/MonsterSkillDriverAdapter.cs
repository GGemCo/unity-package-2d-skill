using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Core의 <c>IMonsterSkillDriver</c> 호출을 <see cref="SkillExecutor"/> 기반 스킬 실행 흐름으로 연결하는 어댑터입니다.
    /// 몬스터 스킬 UID를 기준으로 실행 가능 여부와 내부 쿨다운을 관리합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterSkillDriverAdapter : MonoBehaviour, IMonsterSkillDriverFeedback, ISkillCancelableDriver, IIncomingHitActionCanceler, IIncomingHitCombatFeedbackSink, IMonsterPoolLifecycle
    {
        /// <summary>
        /// 실제 스킬 실행과 취소를 담당하는 런타임 실행기입니다.
        /// </summary>
        private SkillExecutor _executor;

        /// <summary>
        /// 레거시 몬스터 공격 코루틴 정지에 사용하는 몬스터 컨트롤러입니다.
        /// </summary>
        private ControllerMonster _controllerMonster;

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
        /// 스킬 UID별 다음 사용 가능 시각을 저장합니다.
        /// </summary>
        private readonly Dictionary<int, float> _cooldownReadyAt = new();

        /// <summary>
        /// 현재 스킬 실행기가 다른 스킬을 처리 중인지 여부를 반환합니다.
        /// </summary>
        public bool IsSkillBusy => _executor != null && _executor.IsBusy;

        private MonsterSkillExecutionResult _lastSkillResult;
        private bool _hasLastSkillResult;
        private MonsterSkillCombatReport _lastCombatReport;
        private bool _hasLastCombatReport;
        private MonsterSkillCombatReport _pendingCombatReport;
        private bool _hasPendingCombatReport;
        private int _currentRunningSkillUid;

        /// <summary>
        /// 컴포넌트 초기화 시 동일한 게임 오브젝트에서 <see cref="SkillExecutor"/>를 찾아 연결합니다.
        /// </summary>
        private void Awake()
        {
            if (_executor == null)
                SetSkillExecutor(GetComponent<SkillExecutor>());
            else
                SetSkillExecutor(_executor);

            if (_controllerMonster == null) _controllerMonster = GetComponent<ControllerMonster>();
        }

        private void OnDestroy()
        {
            if (_executor != null)
                _executor.ExecutionFinished -= OnExecutionFinished;
        }

        /// <summary>
        /// 드라이버 요청 정보를 바탕으로 몬스터 스킬 사용을 시도합니다.
        /// </summary>
        /// <param name="skillUid">사용할 몬스터 스킬의 고유 식별자입니다.</param>
        /// <param name="request">타겟, 지면 위치, 방향, 테이블 출처를 포함한 스킬 요청 정보입니다.</param>
        /// <returns>스킬 실행이 시작되면 <see cref="SkillUseResult.Started"/>, 실패하면 <see cref="SkillUseResult.Rejected"/>를 반환합니다.</returns>
        public SkillUseResult TryUseSkill(int skillUid, in SkillDriverRequest request)
        {
            if (request.Source != ConfigCommon.SkillTableSource.Monster)
                return SkillUseResult.Fail(SkillUseFailReason.InvalidSource);

            var target = new MonsterSkillTarget(request.LockedTarget, request.GroundPoint, request.Forward);
            return TryUseSkillInternal(skillUid, target, request.ExecutionOptions);
        }

        /// <summary>
        /// 대상 지정 정보와 함께 몬스터 스킬 사용을 시도합니다.
        /// 실행기 상태, 입력값, 쿨다운, 스킬 정의, 타겟 조건을 검증한 뒤 실행을 요청합니다.
        /// </summary>
        /// <param name="skillUid">사용할 몬스터 스킬의 고유 식별자입니다.</param>
        /// <param name="target">고정 대상, 지면 위치, 방향을 포함한 타겟 정보입니다.</param>
        /// <returns>스킬 실행이 시작되면 <see cref="SkillUseResult.Started"/>, 실행할 수 없으면 <see cref="SkillUseResult.Rejected"/>를 반환합니다.</returns>
        public SkillUseResult TryUseSkill(int skillUid, in MonsterSkillTarget target)
        {
            return TryUseSkillInternal(skillUid, target, SkillExecutionOptions.None);
        }

        /// <summary>
        /// 몬스터 스킬 실행 요청을 검증하고 실행 옵션을 포함한 스킬 컨텍스트로 변환합니다.
        /// </summary>
        /// <param name="skillUid">사용할 몬스터 스킬 UID입니다.</param>
        /// <param name="target">고정 대상, 지면 위치, 방향을 포함한 타겟 정보입니다.</param>
        /// <param name="executionOptions">이번 실행에만 적용할 옵션 스냅샷입니다.</param>
        /// <returns>스킬 실행 시도 결과입니다.</returns>
        private SkillUseResult TryUseSkillInternal(
            int skillUid,
            in MonsterSkillTarget target,
            in SkillExecutionOptions executionOptions)
        {
            if (_executor == null) return SkillUseResult.Fail(SkillUseFailReason.InvalidInput);
            if (skillUid <= 0) return SkillUseResult.Fail(SkillUseFailReason.InvalidInput);

            // 동시 실행은 허용하지 않으므로 이미 실행 중이면 거부합니다.
            if (_executor.IsBusy) return SkillUseResult.Fail(SkillUseFailReason.Busy);

            // 스킬 UID 기준 내부 쿨다운이 남아 있으면 사용을 거부합니다.
            if (_cooldownReadyAt.TryGetValue(skillUid, out float readyAt) && Time.time < readyAt)
                return SkillUseResult.Fail(SkillUseFailReason.Cooldown);

            // 몬스터 스킬 정의를 조회할 수 없으면 실행하지 않습니다.
            if (!SkillDefinitionResolver.TryResolve(skillUid, ConfigCommon.SkillTableSource.Monster, out var skill) || skill == null)
                return SkillUseResult.Fail(SkillUseFailReason.InvalidDefinition);

            // LockOnGuaranteedHit 모드는 잠금 대상이 반드시 필요합니다.
            var mode = (ConfigCommonSkill.SkillTargetingMode)Mathf.Clamp((int)skill.TargetingMode, 0, int.MaxValue);
            if (mode == ConfigCommonSkill.SkillTargetingMode.LockOnGuaranteedHit && target.LockedTarget == null)
                return SkillUseResult.Fail(SkillUseFailReason.NoTarget);

            var ctx = new SkillTargetContext(
                caster: gameObject,
                lockedTarget: target.LockedTarget != null ? target.LockedTarget.gameObject : null,
                groundPoint: target.GroundPoint,
                forward: new Vector3(target.Forward.x, target.Forward.y, 0f),
                executionOptions: executionOptions
            );

            if (!SkillRangeResolver.IsWithinCastRange(skill, gameObject, ctx))
                return SkillUseResult.Fail(SkillUseFailReason.OutOfRange);

            ApplyPreSkillFacing(skill, in target);

            bool started = _executor.TryUse(skillUid, ctx, ConfigCommon.SkillTableSource.Monster);
            if (!started) return SkillUseResult.Fail(SkillUseFailReason.ExecutionRejected);

            float cd = Mathf.Max(0f, skill.CoolTime);
            if (cd > 0f) _cooldownReadyAt[skillUid] = Time.time + cd;

            _hasLastSkillResult = false;
            _hasLastCombatReport = false;
            _hasPendingCombatReport = false;
            _pendingCombatReport = default;
            _currentRunningSkillUid = skillUid;
            return SkillUseResult.Started;
        }

        /// <summary>
        /// 스킬 정의의 방향 정책에 따라 실행 직전 시전자 방향을 보정합니다.
        /// 기본 몬스터 스킬은 타겟을 바라보도록 동작하며, 예외 스킬은 데이터에서 정책을 덮어쓸 수 있습니다.
        /// </summary>
        /// <param name="skill">실행 예정인 런타임 스킬 정의입니다.</param>
        /// <param name="target">BT가 전달한 타겟/지면/전방 정보입니다.</param>
        private void ApplyPreSkillFacing(RuntimeSkillDefinition skill, in MonsterSkillTarget target)
        {
            if (skill == null)
                return;

            var characterBase = GetComponent<CharacterBase>();
            if (characterBase == null)
                return;

            Vector2 desired = ResolveDesiredFacing(skill.FacingMode, in target);
            if (desired.sqrMagnitude < 1e-6f)
                return;

            if (Mathf.Abs(desired.x) > 1e-4f)
            {
                characterBase.SetFacing(desired.x >= 0f
                    ? CharacterConstants.FacingDirection8.Right
                    : CharacterConstants.FacingDirection8.Left);
                return;
            }

            characterBase.SetFacing(desired);
        }

        /// <summary>
        /// 방향 정책과 요청 컨텍스트를 기반으로 실제로 바라볼 2D 방향을 계산합니다.
        /// </summary>
        /// <param name="facingMode">스킬 정의의 방향 정책입니다.</param>
        /// <param name="target">BT가 전달한 타겟/전방 정보입니다.</param>
        /// <returns>자동 보정에 사용할 정규화된 2D 방향입니다. 계산에 실패하면 <see cref="Vector2.zero"/>를 반환합니다.</returns>
        private Vector2 ResolveDesiredFacing(ConfigCommonSkill.SkillFacingMode facingMode, in MonsterSkillTarget target)
        {
            switch (facingMode)
            {
                case ConfigCommonSkill.SkillFacingMode.None:
                    return Vector2.zero;

                case ConfigCommonSkill.SkillFacingMode.FaceForwardInput:
                    return NormalizeFacing(target.Forward);

                case ConfigCommonSkill.SkillFacingMode.FaceLockedTarget:
                default:
                    if (target.LockedTarget != null)
                    {
                        var raw = target.LockedTarget.position - transform.position;
                        return NormalizeFacing(new Vector2(raw.x, raw.y));
                    }

                    return NormalizeFacing(target.Forward);
            }
        }

        /// <summary>
        /// 입력 벡터를 방향 보정용 정규화 2D 벡터로 변환합니다.
        /// </summary>
        /// <param name="raw">원본 방향 벡터입니다.</param>
        /// <returns>정규화된 방향입니다. 크기가 너무 작으면 <see cref="Vector2.zero"/>를 반환합니다.</returns>
        private static Vector2 NormalizeFacing(Vector2 raw)
        {
            if (raw.sqrMagnitude < 1e-6f)
                return Vector2.zero;

            return raw.normalized;
        }

        public bool IsRunningSkill(int skillUid)
        {
            if (skillUid <= 0 || _executor == null || !_executor.IsBusy)
                return false;

            return _executor.CurrentSkillUid == skillUid;
        }

        public bool TryGetLastSkillResult(int skillUid, out MonsterSkillExecutionResult result)
        {
            if (_hasLastSkillResult && _lastSkillResult.SkillUid == skillUid)
            {
                result = _lastSkillResult;
                return true;
            }

            result = default;
            return false;
        }

        public bool ConsumeLastSkillResult(int skillUid, out MonsterSkillExecutionResult result)
        {
            if (_hasLastSkillResult && _lastSkillResult.SkillUid == skillUid)
            {
                result = _lastSkillResult;
                _hasLastSkillResult = false;
                _lastSkillResult = default;
                return true;
            }

            result = default;
            return false;
        }

        private void OnExecutionFinished(SkillExecutionReport report)
        {
            _lastSkillResult = report.ToMonsterSkillExecutionResult();
            _hasLastSkillResult = _lastSkillResult.SkillUid > 0;

            if (_hasPendingCombatReport)
            {
                _lastCombatReport = new MonsterSkillCombatReport(
                    _pendingCombatReport.SkillUid,
                    _pendingCombatReport.Outcome,
                    _pendingCombatReport.AttackId,
                    report.Sequence,
                    _pendingCombatReport.Time > 0f ? _pendingCombatReport.Time : report.EndTime);
                _hasLastCombatReport = _lastCombatReport.SkillUid > 0;
            }
            else if (report.State == MonsterSkillExecutionState.Succeeded)
            {
                _lastCombatReport = new MonsterSkillCombatReport(
                    report.SkillUid,
                    MonsterSkillCombatOutcome.Missed,
                    0,
                    report.Sequence,
                    report.EndTime);
                _hasLastCombatReport = _lastCombatReport.SkillUid > 0;
            }
            else
            {
                _lastCombatReport = default;
                _hasLastCombatReport = false;
            }

            _pendingCombatReport = default;
            _hasPendingCombatReport = false;
            _currentRunningSkillUid = 0;
        }

        public bool TryGetLastSkillCombatReport(int skillUid, out MonsterSkillCombatReport report)
        {
            if (_hasLastCombatReport && _lastCombatReport.SkillUid == skillUid)
            {
                report = _lastCombatReport;
                return true;
            }

            report = default;
            return false;
        }

        public bool ConsumeLastSkillCombatReport(int skillUid, out MonsterSkillCombatReport report)
        {
            if (_hasLastCombatReport && _lastCombatReport.SkillUid == skillUid)
            {
                report = _lastCombatReport;
                _hasLastCombatReport = false;
                _lastCombatReport = default;
                return true;
            }

            report = default;
            return false;
        }

        public void NotifyIncomingHitResolved(in IncomingHitCombatFeedback feedback)
        {
            if (feedback.SkillUid <= 0)
                return;

            if (_currentRunningSkillUid > 0 && feedback.SkillUid != _currentRunningSkillUid)
                return;

            if (_hasPendingCombatReport)
                return;

            _pendingCombatReport = new MonsterSkillCombatReport(
                feedback.SkillUid,
                feedback.Outcome,
                feedback.AttackId,
                0,
                feedback.Time);
            _hasPendingCombatReport = true;
        }


        public void ResetForPoolReturn()
        {
            _controllerMonster ??= GetComponent<ControllerMonster>();
            _controllerMonster?.StopAttackCoroutine();

            if (_executor == null)
                SetSkillExecutor(GetComponent<SkillExecutor>());

            if (_executor != null && _executor.IsBusy)
            {
                _executor.TryCancel(SkillCancelReason.ForcedBySystem);
            }

            _cooldownReadyAt.Clear();
            _lastSkillResult = default;
            _hasLastSkillResult = false;
            _lastCombatReport = default;
            _hasLastCombatReport = false;
            _pendingCombatReport = default;
            _hasPendingCombatReport = false;
            _currentRunningSkillUid = 0;
        }

        public void OnPoolRent(Monster owner)
        {
            ResetForPoolReturn();
        }

        public void OnPoolReturn(Monster owner)
        {
            ResetForPoolReturn();
        }
        /// <summary>
        /// 현재 실행 중인 스킬에 취소를 요청합니다.
        /// </summary>
        /// <param name="reason">스킬 취소 사유입니다.</param>
        /// <returns>취소 요청이 정상적으로 전달되면 <see langword="true"/>, 실행기가 없거나 취소할 수 없으면 <see langword="false"/>를 반환합니다.</returns>
        public bool RequestCancelSkill(SkillCancelReason reason)
        {
            if (_executor == null) return false;
            return _executor.TryCancel(reason);
        }

        /// <summary>
        /// 피격/사망 인터럽트가 발생했을 때 진행 중인 몬스터 액션을 정리합니다.
        /// 스킬 실행기 취소와 레거시 공격 코루틴 정지를 함께 처리해 BT/기존 공격 흐름이 엇갈리지 않도록 맞춥니다.
        /// </summary>
        /// <param name="reason">외부 인터럽트 사유입니다.</param>
        public void CancelActionsOnIncomingHit(IncomingHitCancelReason reason)
        {
            _controllerMonster ??= GetComponent<ControllerMonster>();
            _controllerMonster?.StopAttackCoroutine();

            if (_executor == null)
                SetSkillExecutor(GetComponent<SkillExecutor>());

            if (_executor == null || !_executor.IsBusy)
                return;

            SkillCancelReason cancelReason = reason switch
            {
                IncomingHitCancelReason.Death => SkillCancelReason.Death,
                IncomingHitCancelReason.Damage => SkillCancelReason.HitStun,
                _ => SkillCancelReason.ForcedBySystem
            };

            if (_executor.TryApplyIncomingHitToChargeGauge(cancelReason))
            {
                return;
            }

            _executor.TryCancel(cancelReason);
        }
    }
}
