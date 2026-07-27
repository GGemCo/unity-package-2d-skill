using System;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 1회 실행 중 차징 단계의 상태 전이, 애니메이션, VFX, UI 스냅샷 알림을 담당합니다.
    /// </summary>
    internal sealed class SkillRunChargeController
    {
        /// <summary>
        /// 차징 단계 내부 재생 구간입니다.
        /// 시작/루프/종료 애니메이션이 서로 덮어쓰지 않도록 상태를 분리합니다.
        /// </summary>
        private enum ChargeStagePhase
        {
            /// <summary>아직 단계 구간을 결정하지 않은 상태입니다.</summary>
            None,

            /// <summary>단계 진입 시 시작 애니메이션을 재생하는 구간입니다.</summary>
            Start,

            /// <summary>단계 유지 시간 동안 루프 애니메이션을 재생하는 구간입니다.</summary>
            Loop,

            /// <summary>단계 종료 시 종료 애니메이션을 재생하는 구간입니다.</summary>
            End
        }

        private readonly SkillExecutor _owner;
        private readonly SkillRun _run;
        private readonly RuntimeSkillDefinition _skill;
        private readonly SkillTargetContext _ctx;
        private readonly ICharacterAnimationController _animController;
        private readonly Func<Vector3> _snapshotCasterPositionProvider;
        private readonly SkillChargeSoundController _chargeSoundController = new();

        private bool _didCharge;
        private bool _chargeCompleted;
        private bool _isChargeFailed;
        private bool _didChargeComplete;
        private bool _isChargeCompleteWaiting;
        private bool _didNotifyInactive;
        private int _chargeStageArrayIndex;
        private ChargeStagePhase _chargeStagePhase;
        private float _chargeStagePhaseElapsed;
        private float _chargeStagePhaseDuration;
        private float _chargeStageElapsed;
        private float _chargeTotalElapsed;
        private float _chargeGaugeCurrent;
        private float _chargeGaugeMax;
        private float _chargeCompleteElapsed;
        private float _chargeCompleteDuration;
        private float _chargeFailElapsed;
        private float _chargeFailDuration;
        private VfxBehaviourBase _activeChargeVfx;

        /// <summary>
        /// 스킬 정의에 차징 설정이 있고 활성화되어 있는지 여부입니다.
        /// </summary>
        public bool HasCharge => _skill?.Charge != null && _skill.Charge.IsEnabled;

        /// <summary>
        /// 현재 스킬이 차징 단계에 머무르고 있는지 여부입니다.
        /// </summary>
        public bool IsCharging => HasCharge && _didCharge && !_chargeCompleted && !_isChargeFailed;

        /// <summary>
        /// 차징 전체 단계가 완료되었는지 여부입니다.
        /// </summary>
        public bool IsCompleted => _chargeCompleted;

        /// <summary>
        /// 차징 실패 애니메이션 또는 종료 대기 상태인지 여부입니다.
        /// </summary>
        public bool IsFailed => _isChargeFailed;

        /// <summary>
        /// 차징 완료 애니메이션 대기 중인지 여부입니다.
        /// </summary>
        public bool IsCompleteWaiting => _isChargeCompleteWaiting;

        /// <summary>
        /// 차징 처리 때문에 캐스팅/사용 단계 진입을 이번 프레임에 보류해야 하는지 여부입니다.
        /// </summary>
        public bool ShouldBlockSkillFlow => IsFailed || IsCompleteWaiting || (HasCharge && !IsCompleted);

        /// <summary>
        /// 차징 실패 처리 결과로 스킬 런 종료가 필요한지 여부입니다.
        /// </summary>
        public bool ShouldEndRun { get; private set; }

        /// <summary>
        /// 차징 컨트롤러를 생성하고 스킬 런에서 필요한 의존성을 보관합니다.
        /// </summary>
        /// <param name="owner">차징 상태 알림을 전달할 스킬 실행기입니다.</param>
        /// <param name="run">차징 실패를 보고할 현재 스킬 런입니다.</param>
        /// <param name="skill">실행 중인 런타임 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="animController">차징 애니메이션을 재생할 캐릭터 애니메이션 컨트롤러입니다.</param>
        /// <param name="snapshotCasterPositionProvider">캐스터가 없을 때 사용할 시작 시점 캐스터 위치 제공자입니다.</param>
        public SkillRunChargeController(
            SkillExecutor owner,
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            ICharacterAnimationController animController,
            Func<Vector3> snapshotCasterPositionProvider)
        {
            _owner = owner;
            _run = run;
            _skill = skill;
            _ctx = ctx;
            _animController = animController;
            _snapshotCasterPositionProvider = snapshotCasterPositionProvider ?? (() => Vector3.zero);
        }

        /// <summary>
        /// 차징 상태를 한 프레임 진행합니다.
        /// 실패/완료 대기/활성 차징 순서로 처리하여 SkillRun의 캐스팅 단계 진입 여부를 결정합니다.
        /// </summary>
        /// <param name="dt">이번 프레임 경과 시간입니다.</param>
        public void Tick(float dt)
        {
            if (!HasCharge)
                return;

            if (_isChargeFailed)
            {
                TickChargeFailed(dt);
                return;
            }

            if (_isChargeCompleteWaiting)
            {
                TickChargeComplete(dt);
                return;
            }

            if (_chargeCompleted)
                return;

            TickActiveCharge(dt);
        }

        /// <summary>
        /// 피격으로 차징 게이지를 감소시킵니다.
        /// 게이지가 0이 되면 차징 실패 상태로 전환합니다.
        /// </summary>
        /// <param name="reason">피격 또는 취소 사유입니다.</param>
        /// <param name="damageAmount">감소시킬 게이지 값입니다. 0 이하이면 스킬 설정의 기본 감소량을 사용합니다.</param>
        /// <returns>현재 차징 게이지가 피격을 처리했으면 <see langword="true"/>입니다.</returns>
        public bool TryApplyGaugeDamage(SkillCancelReason reason, float damageAmount = 0f)
        {
            if (!IsCharging)
                return false;

            if (reason == SkillCancelReason.Death)
                return false;

            float resolvedDamage = damageAmount > 0f
                ? damageAmount
                : Mathf.Max(0f, _skill.Charge.GaugeDamagePerHit);

            if (resolvedDamage <= 0f)
                resolvedDamage = 1f;

            _chargeGaugeCurrent = Mathf.Max(0f, _chargeGaugeCurrent - resolvedDamage);
            NotifyChargeSnapshot();

            if (_chargeGaugeCurrent <= 0f)
            {
                BreakCharge();
            }

            return true;
        }

        /// <summary>
        /// 치명적인 즉시 피격을 활성 차징 게이지로 대신 소비하고 차징 실패 상태로 전환합니다.
        /// </summary>
        /// <returns>활성 차징 게이지를 완전히 소진했으면 <see langword="true"/>입니다.</returns>
        public bool TryBreakChargeByLethalIncomingHit()
        {
            if (!IsCharging)
                return false;

            // 일반 피격의 고정 게이지 감소량과 구분하여, 치명타 보호에서는
            // 남은 게이지 양과 관계없이 완전 소진 후 기존 실패 연출 흐름을 재사용합니다.
            _chargeGaugeCurrent = 0f;
            NotifyChargeSnapshot();
            BreakCharge();
            return true;
        }

        /// <summary>
        /// 차징 VFX를 정리하고 외부 UI에 비활성 스냅샷을 한 번만 전달합니다.
        /// </summary>
        public void CleanupForRunEnd()
        {
            _chargeSoundController.StopAll();
            CleanupActiveChargeVfx();
            NotifyInactive();
        }

        /// <summary>
        /// 차징 단계 시간을 진행합니다.
        /// 각 단계는 시작 애니메이션, 루프 유지 시간, 종료 애니메이션 순서로 처리됩니다.
        /// </summary>
        /// <param name="dt">이번 프레임 경과 시간입니다.</param>
        private void TickActiveCharge(float dt)
        {
            if (!_didCharge)
                BeginCharge();

            if (_chargeCompleted || _isChargeFailed)
                return;

            var stages = _skill.Charge.Stages;
            if (stages == null || stages.Length == 0)
            {
                CompleteCharge();
                return;
            }

            float remaining = Mathf.Max(0f, dt);
            int safetyCounter = 0;
            while (remaining > 0f && !_chargeCompleted && !_isChargeFailed && safetyCounter++ < 128)
            {
                var stage = GetCurrentChargeStage();
                if (stage == null)
                {
                    CompleteCharge();
                    break;
                }

                switch (_chargeStagePhase)
                {
                    case ChargeStagePhase.Start:
                        TickChargeStageStart(stage, ref remaining);
                        break;
                    case ChargeStagePhase.Loop:
                        TickChargeStageLoop(stage, ref remaining);
                        break;
                    case ChargeStagePhase.End:
                        TickChargeStageEnd(stage, ref remaining);
                        break;
                    default:
                        EnterChargeStage(_chargeStageArrayIndex);
                        break;
                }
            }

            NotifyChargeSnapshot();
        }

        /// <summary>
        /// 차징을 시작하고 첫 번째 차징 단계로 진입합니다.
        /// </summary>
        private void BeginCharge()
        {
            _didCharge = true;
            _chargeCompleted = false;
            _isChargeFailed = false;
            _isChargeCompleteWaiting = false;
            _chargeStageArrayIndex = 0;
            _chargeStagePhase = ChargeStagePhase.None;
            _chargeStagePhaseElapsed = 0f;
            _chargeStagePhaseDuration = 0f;
            _chargeStageElapsed = 0f;
            _chargeTotalElapsed = 0f;
            _chargeCompleteElapsed = 0f;
            _chargeCompleteDuration = 0f;
            _chargeGaugeMax = Mathf.Max(0f, _skill.Charge.GaugeMax);
            if (_chargeGaugeMax <= 0f)
                _chargeGaugeMax = 1f;
            _chargeGaugeCurrent = _chargeGaugeMax;

            _chargeSoundController.BeginCharge(_skill.Charge);
            EnterChargeStage(_chargeStageArrayIndex);
            NotifyChargeSnapshot();
        }

        /// <summary>
        /// 지정한 차징 단계로 진입하고, 시작 애니메이션 또는 루프 구간을 준비합니다.
        /// </summary>
        /// <param name="stageArrayIndex">진입할 차징 단계 배열 인덱스입니다.</param>
        private void EnterChargeStage(int stageArrayIndex)
        {
            CleanupActiveChargeVfx();

            var stage = GetChargeStage(stageArrayIndex);
            if (stage == null)
            {
                CompleteCharge();
                return;
            }

            _chargeStageElapsed = 0f;
            _chargeStagePhaseElapsed = 0f;
            _chargeStagePhaseDuration = 0f;
            SpawnChargeVfx(stage);
            _chargeSoundController.EnterStage(stage);

            float startDuration = ResolveChargeStageStartDuration(stage);
            if (startDuration > 0f)
            {
                _chargeStagePhase = ChargeStagePhase.Start;
                _chargeStagePhaseDuration = startDuration;
                PlayChargeStart(stage);
                return;
            }

            BeginChargeStageLoop(stage);
        }

        /// <summary>
        /// 차징 단계의 시작 애니메이션 구간을 진행합니다.
        /// </summary>
        /// <param name="stage">현재 차징 단계 정의입니다.</param>
        /// <param name="remaining">이번 프레임에서 아직 소비하지 않은 시간입니다.</param>
        private void TickChargeStageStart(RuntimeSkillChargeStageDefinition stage, ref float remaining)
        {
            if (!ConsumeChargePhaseTime(_chargeStagePhaseDuration, ref remaining))
                return;

            BeginChargeStageLoop(stage);
        }

        /// <summary>
        /// 차징 단계의 루프 유지 구간을 시작합니다.
        /// </summary>
        /// <param name="stage">현재 차징 단계 정의입니다.</param>
        private void BeginChargeStageLoop(RuntimeSkillChargeStageDefinition stage)
        {
            _chargeStagePhase = ChargeStagePhase.Loop;
            _chargeStagePhaseElapsed = 0f;
            _chargeStagePhaseDuration = stage != null ? Mathf.Max(0f, stage.DurationSeconds) : 0f;
            _chargeSoundController.BeginStageLoop(stage);
            PlayChargeLoop(stage);
        }

        /// <summary>
        /// 차징 단계의 실제 차징 유지 시간을 진행합니다.
        /// 진행도 게이지는 이 루프 구간의 누적 시간만 사용합니다.
        /// </summary>
        /// <param name="stage">현재 차징 단계 정의입니다.</param>
        /// <param name="remaining">이번 프레임에서 아직 소비하지 않은 시간입니다.</param>
        private void TickChargeStageLoop(RuntimeSkillChargeStageDefinition stage, ref float remaining)
        {
            if (stage == null || stage.DurationSeconds <= 0f)
            {
                BeginChargeStageEnd(stage);
                return;
            }

            float stageRemaining = Mathf.Max(0f, stage.DurationSeconds - _chargeStageElapsed);
            if (stageRemaining <= 0f)
            {
                BeginChargeStageEnd(stage);
                return;
            }

            float consume = Mathf.Min(remaining, stageRemaining);
            _chargeStageElapsed += consume;
            _chargeTotalElapsed += consume;
            remaining -= consume;

            if (_chargeStageElapsed + 1e-6f >= stage.DurationSeconds)
            {
                BeginChargeStageEnd(stage);
            }
        }

        /// <summary>
        /// 차징 단계의 종료 애니메이션 구간을 시작하거나, 종료 연출이 없으면 다음 단계로 이동합니다.
        /// </summary>
        /// <param name="stage">현재 차징 단계 정의입니다.</param>
        private void BeginChargeStageEnd(RuntimeSkillChargeStageDefinition stage)
        {
            _chargeSoundController.StopStageLoop();
            _chargeStagePhaseElapsed = 0f;
            _chargeStagePhaseDuration = 0f;

            float endDuration = ResolveChargeStageEndDuration(stage);
            if (endDuration > 0f)
            {
                _chargeStagePhase = ChargeStagePhase.End;
                _chargeStagePhaseDuration = endDuration;
                PlayChargeEnd(stage);
                return;
            }

            AdvanceChargeStage();
        }

        /// <summary>
        /// 차징 단계의 종료 애니메이션 구간을 진행합니다.
        /// </summary>
        /// <param name="stage">현재 차징 단계 정의입니다.</param>
        /// <param name="remaining">이번 프레임에서 아직 소비하지 않은 시간입니다.</param>
        private void TickChargeStageEnd(RuntimeSkillChargeStageDefinition stage, ref float remaining)
        {
            if (!ConsumeChargePhaseTime(_chargeStagePhaseDuration, ref remaining))
                return;

            AdvanceChargeStage();
        }

        /// <summary>
        /// 시작/종료처럼 고정 길이를 가진 차징 하위 구간 시간을 소비합니다.
        /// </summary>
        /// <param name="durationSeconds">소비해야 할 구간 길이입니다.</param>
        /// <param name="remaining">이번 프레임에서 아직 소비하지 않은 시간입니다.</param>
        /// <returns>구간이 끝났으면 <see langword="true"/>입니다.</returns>
        private bool ConsumeChargePhaseTime(float durationSeconds, ref float remaining)
        {
            if (durationSeconds <= 0f)
                return true;

            float phaseRemaining = Mathf.Max(0f, durationSeconds - _chargeStagePhaseElapsed);
            if (phaseRemaining <= 0f)
                return true;

            if (remaining <= 0f)
                return false;

            float consume = Mathf.Min(remaining, phaseRemaining);
            _chargeStagePhaseElapsed += consume;
            remaining -= consume;

            return _chargeStagePhaseElapsed + 1e-6f >= durationSeconds;
        }

        /// <summary>
        /// 다음 차징 단계로 이동하거나 모든 단계가 끝났으면 차징 완료 상태로 전환합니다.
        /// </summary>
        private void AdvanceChargeStage()
        {
            _chargeStageArrayIndex++;
            _chargeStagePhase = ChargeStagePhase.None;
            _chargeStagePhaseElapsed = 0f;
            _chargeStagePhaseDuration = 0f;
            _chargeStageElapsed = 0f;

            if (_skill.Charge.Stages == null || _chargeStageArrayIndex >= _skill.Charge.Stages.Length)
            {
                CompleteCharge();
                return;
            }

            EnterChargeStage(_chargeStageArrayIndex);
        }

        /// <summary>
        /// 전체 차징 완료를 처리하고, 완료 애니메이션 대기 시간이 있으면 사용 단계 진입을 지연합니다.
        /// </summary>
        private void CompleteCharge()
        {
            if (_chargeCompleted)
                return;

            _chargeCompleted = true;
            _chargeStagePhase = ChargeStagePhase.None;
            _chargeStagePhaseElapsed = 0f;
            _chargeStagePhaseDuration = 0f;
            _chargeSoundController.StopAll();
            CleanupActiveChargeVfx();
            PlayChargeComplete();

            _chargeCompleteElapsed = 0f;
            _chargeCompleteDuration = ResolveChargeCompleteDuration();
            _isChargeCompleteWaiting = _chargeCompleteDuration > 0f;

            NotifyChargeSnapshot();
        }

        /// <summary>
        /// 차징 완료 애니메이션 대기 시간을 진행합니다.
        /// 대기가 끝나면 SkillRun이 다음 프레임부터 캐스팅/사용 단계로 진입할 수 있습니다.
        /// </summary>
        /// <param name="dt">이번 프레임 경과 시간입니다.</param>
        private void TickChargeComplete(float dt)
        {
            _chargeCompleteElapsed += Mathf.Max(0f, dt);
            if (_chargeCompleteElapsed < _chargeCompleteDuration)
                return;

            _isChargeCompleteWaiting = false;
            _chargeCompleteElapsed = 0f;
            _chargeCompleteDuration = 0f;
        }

        /// <summary>
        /// 피격 등으로 차징을 중단하고 실패 애니메이션 상태로 전환합니다.
        /// </summary>
        private void BreakCharge()
        {
            if (_isChargeFailed || _chargeCompleted)
                return;

            _isChargeFailed = true;
            _chargeCompleted = false;
            _isChargeCompleteWaiting = false;
            _chargeFailElapsed = 0f;
            _chargeFailDuration = ResolveChargeFailDuration();

            _chargeSoundController.StopAll();
            CleanupActiveChargeVfx();
            PlayChargeFail();
            _owner?.NotifyChargeFailed(_run);
            NotifyChargeSnapshot();

            if (_chargeFailDuration <= 0f)
            {
                ShouldEndRun = true;
            }
        }

        /// <summary>
        /// 차징 실패 애니메이션 대기 시간을 진행하고, 완료되면 스킬 런 종료를 요청합니다.
        /// </summary>
        /// <param name="dt">이번 프레임 경과 시간입니다.</param>
        private void TickChargeFailed(float dt)
        {
            _chargeFailElapsed += Mathf.Max(0f, dt);
            if (_chargeFailElapsed < _chargeFailDuration)
                return;

            ShouldEndRun = true;
        }

        /// <summary>
        /// 현재 차징 단계 정의를 반환합니다.
        /// </summary>
        /// <returns>현재 단계 정의입니다.</returns>
        private RuntimeSkillChargeStageDefinition GetCurrentChargeStage()
        {
            return GetChargeStage(_chargeStageArrayIndex);
        }

        /// <summary>
        /// 지정한 배열 인덱스의 차징 단계 정의를 반환합니다.
        /// </summary>
        /// <param name="stageArrayIndex">조회할 차징 단계 배열 인덱스입니다.</param>
        /// <returns>조회된 단계 정의입니다. 범위를 벗어나면 <see langword="null"/>입니다.</returns>
        private RuntimeSkillChargeStageDefinition GetChargeStage(int stageArrayIndex)
        {
            var stages = _skill.Charge.Stages;
            if (stages == null || stageArrayIndex < 0 || stageArrayIndex >= stages.Length)
                return null;

            return stages[stageArrayIndex];
        }

        /// <summary>
        /// 차징 단계 시작 애니메이션을 재생합니다.
        /// </summary>
        /// <param name="stage">현재 차징 단계 정의입니다.</param>
        private void PlayChargeStart(RuntimeSkillChargeStageDefinition stage)
        {
            if (stage == null)
                return;
            if (GcLogger.IsNull(_animController, nameof(_animController)))
                return;

            _animController.PlaySkillAnimation(new SkillAnimationRequest(
                _skill.Uid,
                SkillAnimationPhase.ChargeStart,
                loop: false,
                timeScale: 1f,
                overrideAnimationName: string.IsNullOrEmpty(stage.StartClip) ? null : stage.StartClip));
        }

        /// <summary>
        /// 차징 단계 루프 애니메이션을 재생합니다.
        /// </summary>
        /// <param name="stage">현재 차징 단계 정의입니다.</param>
        private void PlayChargeLoop(RuntimeSkillChargeStageDefinition stage)
        {
            if (stage == null)
                return;
            if (GcLogger.IsNull(_animController, nameof(_animController)))
                return;

            _animController.PlaySkillAnimation(new SkillAnimationRequest(
                _skill.Uid,
                SkillAnimationPhase.ChargeLoop,
                loop: true,
                timeScale: 1f,
                overrideAnimationName: string.IsNullOrEmpty(stage.LoopClip) ? null : stage.LoopClip));
        }

        /// <summary>
        /// 차징 단계 종료 애니메이션을 재생합니다.
        /// </summary>
        /// <param name="stage">현재 차징 단계 정의입니다.</param>
        private void PlayChargeEnd(RuntimeSkillChargeStageDefinition stage)
        {
            if (stage == null)
                return;
            if (GcLogger.IsNull(_animController, nameof(_animController)))
                return;

            _animController.PlaySkillAnimation(new SkillAnimationRequest(
                _skill.Uid,
                SkillAnimationPhase.ChargeEnd,
                loop: false,
                timeScale: 1f,
                overrideAnimationName: string.IsNullOrEmpty(stage.EndClip) ? null : stage.EndClip));
        }

        /// <summary>
        /// 전체 차징 완료 애니메이션을 재생합니다.
        /// </summary>
        private void PlayChargeComplete()
        {
            if (_didChargeComplete)
                return;

            _didChargeComplete = true;
            if (string.IsNullOrWhiteSpace(_skill.Charge.CompleteClip))
                return;
            if (GcLogger.IsNull(_animController, nameof(_animController)))
                return;

            _animController.PlaySkillAnimation(new SkillAnimationRequest(
                _skill.Uid,
                SkillAnimationPhase.ChargeComplete,
                loop: false,
                timeScale: 1f,
                overrideAnimationName: _skill.Charge.CompleteClip));
        }

        /// <summary>
        /// 차징 실패 애니메이션을 재생합니다.
        /// </summary>
        private void PlayChargeFail()
        {
            if (GcLogger.IsNull(_animController, nameof(_animController)))
                return;

            string failClip = _skill.Charge.FailClip;
            if (string.IsNullOrWhiteSpace(failClip))
            {
                _animController.StopSkillAnimation();
                return;
            }

            _animController.PlaySkillAnimation(new SkillAnimationRequest(
                _skill.Uid,
                SkillAnimationPhase.ChargeFail,
                loop: false,
                timeScale: 1f,
                overrideAnimationName: failClip));
        }

        /// <summary>
        /// 차징 단계 시작 애니메이션 유지 시간을 계산합니다.
        /// </summary>
        /// <param name="stage">현재 차징 단계 정의입니다.</param>
        /// <returns>시작 애니메이션 유지 시간(초)입니다.</returns>
        private float ResolveChargeStageStartDuration(RuntimeSkillChargeStageDefinition stage)
        {
            if (stage == null)
                return 0f;

            return ResolveChargeAnimationDuration(stage.StartClip, stage.StartDurationSeconds);
        }

        /// <summary>
        /// 차징 단계 종료 애니메이션 유지 시간을 계산합니다.
        /// </summary>
        /// <param name="stage">현재 차징 단계 정의입니다.</param>
        /// <returns>종료 애니메이션 유지 시간(초)입니다.</returns>
        private float ResolveChargeStageEndDuration(RuntimeSkillChargeStageDefinition stage)
        {
            if (stage == null)
                return 0f;

            return ResolveChargeAnimationDuration(stage.EndClip, stage.EndDurationSeconds);
        }

        /// <summary>
        /// 차징 완료 애니메이션 유지 시간을 계산합니다.
        /// </summary>
        /// <returns>차징 완료 애니메이션 유지 시간(초)입니다.</returns>
        private float ResolveChargeCompleteDuration()
        {
            if (_skill?.Charge == null)
                return 0f;

            return ResolveChargeAnimationDuration(_skill.Charge.CompleteClip, _skill.Charge.CompleteDurationSeconds);
        }

        /// <summary>
        /// 차징 실패 애니메이션 유지 시간을 계산합니다.
        /// </summary>
        /// <returns>차징 실패 애니메이션 유지 시간(초)입니다.</returns>
        private float ResolveChargeFailDuration()
        {
            if (_skill?.Charge == null)
                return 0f;

            if (_skill.Charge.FailDurationSeconds > 0f)
                return _skill.Charge.FailDurationSeconds;

            if (string.IsNullOrWhiteSpace(_skill.Charge.FailClip))
                return 0.3f;

            return ResolveChargeAnimationDuration(_skill.Charge.FailClip, 0f);
        }

        /// <summary>
        /// 차징 계열 애니메이션의 유지 시간을 명시값 또는 클립 길이로 계산합니다.
        /// 명시값이 있으면 우선 사용하고, 클립 길이를 확인할 수 없으면 짧은 기본값을 사용합니다.
        /// </summary>
        /// <param name="clipName">재생할 애니메이션 클립 이름입니다.</param>
        /// <param name="explicitDurationSeconds">테이블에서 지정한 명시 유지 시간입니다.</param>
        /// <returns>해당 애니메이션 유지 시간(초)입니다.</returns>
        private float ResolveChargeAnimationDuration(string clipName, float explicitDurationSeconds)
        {
            if (explicitDurationSeconds > 0f)
                return explicitDurationSeconds;

            if (string.IsNullOrWhiteSpace(clipName))
                return 0f;

            if (_animController == null)
                return 0.3f;

            float duration = _animController.GetCharacterAnimationDuration(clipName, isMilliseconds: false);
            return duration > 0f ? duration : 0.3f;
        }

        /// <summary>
        /// 현재 차징 단계에 설정된 VFX를 생성합니다.
        /// 단계가 바뀌거나 차징이 끝나면 생성된 VFX를 정리합니다.
        /// </summary>
        /// <param name="stage">현재 차징 단계 정의입니다.</param>
        private void SpawnChargeVfx(RuntimeSkillChargeStageDefinition stage)
        {
            if (stage == null || stage.VfxUid <= 0)
                return;

            var sceneGame = SceneGame.Instance;
            if (sceneGame == null || sceneGame.VfxManager == null)
                return;

            CharacterBase casterCharacter = _ctx.caster != null ? _ctx.caster.GetComponentInParent<CharacterBase>() : null;
            Vector3 fallbackCasterPosition = _snapshotCasterPositionProvider();
            Vector3 spawnPosition = casterCharacter != null
                ? casterCharacter.transform.position
                : (_ctx.caster != null ? _ctx.caster.transform.position : fallbackCasterPosition);

            var request = new VfxSpawnRequest
            {
                VfxUid = stage.VfxUid,
                Owner = casterCharacter,
                OwnerGameObject = _ctx.caster,
                FollowTarget = stage.VfxFollowMode != VfxConstants.FollowMode.None ? casterCharacter : null,
                WorldPosition = spawnPosition,
                DurationOverride = 0f,
                ScaleOverride = stage.VfxScale,
                PositionY = stage.VfxPositionY,
                PositionYType = stage.VfxPositionYType,
                FollowModeOverride = stage.VfxFollowMode,
                FollowAnchorModeOverride = stage.VfxFollowAnchorMode,
            };

            _activeChargeVfx = sceneGame.VfxManager.CreateVfx(request);
        }

        /// <summary>
        /// 현재 차징 단계에서 생성한 VFX를 즉시 제거합니다.
        /// </summary>
        private void CleanupActiveChargeVfx()
        {
            if (_activeChargeVfx == null)
                return;

            _activeChargeVfx.DestroyForce();
            _activeChargeVfx = null;
        }

        /// <summary>
        /// 현재 차징 상태 스냅샷을 SkillExecutor에 전달합니다.
        /// UI 게이지와 테스트 툴은 이 스냅샷을 기준으로 갱신됩니다.
        /// </summary>
        private void NotifyChargeSnapshot()
        {
            if (!HasCharge)
                return;

            _owner?.NotifyChargeStateChanged(BuildChargeSnapshot());
        }

        /// <summary>
        /// 스킬 종료 또는 취소 시 UI가 차징 게이지를 숨기도록 비활성 스냅샷을 전달합니다.
        /// </summary>
        private void NotifyInactive()
        {
            if (!HasCharge || _didNotifyInactive)
                return;

            _didNotifyInactive = true;
            _owner?.NotifyChargeStateChanged(SkillChargeSnapshot.Inactive(SkillUid));
        }

        /// <summary>
        /// 현재 차징 상태를 UI와 외부 구독자가 사용할 수 있는 스냅샷으로 변환합니다.
        /// </summary>
        /// <returns>현재 차징 상태 스냅샷입니다.</returns>
        private SkillChargeSnapshot BuildChargeSnapshot()
        {
            if (!HasCharge)
                return SkillChargeSnapshot.Inactive(SkillUid);

            var stage = GetCurrentChargeStage();
            float totalDuration = Mathf.Max(0f, _skill.Charge.TotalDurationSeconds);
            float progress01 = totalDuration > 0f ? Mathf.Clamp01(_chargeTotalElapsed / totalDuration) : 1f;
            float gauge01 = _chargeGaugeMax > 0f ? Mathf.Clamp01(_chargeGaugeCurrent / _chargeGaugeMax) : 0f;

            return new SkillChargeSnapshot(
                SkillUid,
                isActive: IsCharging || _isChargeFailed,
                isCharging: IsCharging,
                isFailed: _isChargeFailed,
                stageIndex: stage != null ? stage.StageIndex : 0,
                stageCount: _skill.Charge.Stages?.Length ?? 0,
                stageElapsedSeconds: _chargeStageElapsed,
                stageDurationSeconds: stage != null ? stage.DurationSeconds : 0f,
                totalElapsedSeconds: _chargeTotalElapsed,
                totalDurationSeconds: totalDuration,
                progress01: progress01,
                gaugeCurrent: _chargeGaugeCurrent,
                gaugeMax: _chargeGaugeMax,
                gauge01: gauge01);
        }

        /// <summary>
        /// 현재 스킬 UID를 안전하게 반환합니다.
        /// </summary>
        private int SkillUid => _skill != null ? _skill.Uid : 0;
    }
}
