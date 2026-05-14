using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 1회 실행의 런타임 상태를 관리합니다.
    /// </summary>
    public sealed class SkillRun
    {
        private readonly SkillExecutor _owner;
        private readonly RuntimeSkillDefinition _skill;
        private readonly SkillTargetContext _ctx;

        private readonly ICharacterAnimationController _animController;
        private readonly ICharacterActionController _actionController;
        private readonly ICharacterMotionController _motionController;
        private readonly Rigidbody2D _casterRigidbody2D;

        private SkillRuntimeSequence _sequence;
        private float _time;
        private int _nextEventIndex;

        // Casting
        private bool _didCastStart;
        private bool _didCastLoop;
        private bool _didCastEnd;
        private bool _didUse;

        // Charge
        private bool _didCharge;
        private bool _chargeCompleted;
        private bool _isChargeFailed;
        private bool _didChargeComplete;
        private int _chargeStageArrayIndex;
        private float _chargeStageElapsed;
        private float _chargeTotalElapsed;
        private float _chargeGaugeCurrent;
        private float _chargeGaugeMax;
        private float _chargeFailElapsed;
        private float _chargeFailDuration;
        private VfxBehaviourBase _activeChargeVfx;

        private float _castElapsed;
        private Vector3 _snapshotCasterPos;
        private Vector3 _snapshotTargetPos;
        private Vector3 _snapshotGroundPoint;
        private readonly Dictionary<string, SkillPositionAnchorSnapshot> _positionAnchors = new(StringComparer.Ordinal);

        private bool _isLoading;
        private bool _isEnded;
        private bool _isPositionHoldActive;
        private bool _keepPositionHoldUntilSkillEnd;

        public bool IsDone { get; private set; }
        public int SkillUid => _skill != null ? _skill.Uid : 0;
        public GameObject Caster => _ctx.caster;

        /// <summary>
        /// 현재 스킬이 차징 단계에 머무르고 있는지 여부입니다.
        /// </summary>
        public bool IsCharging => HasCharge && _didCharge && !_chargeCompleted && !_isChargeFailed && !IsDone;

        /// <summary>
        /// Use 애니메이션 기준으로 현재 스킬 이벤트 시퀀스가 진행된 시간입니다.
        /// </summary>
        public float CurrentTime => _time;

        public SkillRun(SkillExecutor owner, RuntimeSkillDefinition skill, SkillTargetContext ctx,
            ICharacterAnimationController animController,
            ICharacterActionController actionController)
        {
            _owner = owner;
            _skill = skill;
            _ctx = ctx;
            _animController = animController;
            _actionController = actionController;
            _motionController = _ctx.caster != null ? _ctx.caster.GetComponentInParent<ICharacterMotionController>() : null;
            _casterRigidbody2D = _ctx.caster != null ? _ctx.caster.GetComponentInParent<Rigidbody2D>() : null;
        }

        private bool HasCharge => _skill?.Charge != null && _skill.Charge.IsEnabled;

        /// <summary>
        /// 같은 스킬 실행 안에서 이후 이벤트가 참조할 수 있도록 위치 앵커를 저장합니다.
        /// </summary>
        /// <param name="key">위치 앵커를 식별할 키입니다.</param>
        /// <param name="snapshot">저장할 위치 스냅샷입니다.</param>
        public void SavePositionAnchor(string key, SkillPositionAnchorSnapshot snapshot)
        {
            if (string.IsNullOrWhiteSpace(key))
                return;

            _positionAnchors[key.Trim()] = snapshot;
        }

        /// <summary>
        /// 같은 스킬 실행 안에서 이전 이벤트가 저장한 위치 앵커를 조회합니다.
        /// </summary>
        /// <param name="key">조회할 위치 앵커 키입니다.</param>
        /// <param name="snapshot">조회된 위치 스냅샷입니다.</param>
        /// <returns>위치 앵커를 찾았으면 <see langword="true"/>입니다.</returns>
        public bool TryGetPositionAnchor(string key, out SkillPositionAnchorSnapshot snapshot)
        {
            snapshot = default;
            if (string.IsNullOrWhiteSpace(key))
                return false;

            return _positionAnchors.TryGetValue(key.Trim(), out snapshot);
        }

        /// <summary>
        /// 스킬 실행을 시작하고 런타임 시퀀스를 비동기로 로드합니다.
        /// </summary>
        public void Start()
        {
            SnapshotContext();

            ApplyInitialActionState();
            _isLoading = true;
            _ = LoadSequenceAsync();
        }

        private async Task LoadSequenceAsync()
        {
            try
            {
                // RuntimeSequenceKey는 Addressables Key 규칙(ConfigAddressableKeySkill)을 사용한다.
                // (에디터 테스트에서는 SkillRuntimeSequenceRepository.RegisterEditorOverride로 주입 가능)
                var runtimeSequenceKey = ConfigAddressableKeySkill.GetRuntimeSequenceKeyPlayer(_skill.Uid);
                if (_skill.OwnerType == ConfigCommonSkill.SkillOwnerType.Monster)
                {
                    runtimeSequenceKey = ConfigAddressableKeySkill.GetRuntimeSequenceKeyMonster(_skill.Uid);
                }
                
                if (string.IsNullOrEmpty(runtimeSequenceKey))
                {
                    IsDone = true;
                    EndRun();
                    return;
                }

                if (_skill.OwnerType == ConfigCommonSkill.SkillOwnerType.Monster)
                {
                    _sequence = await AddressableLoaderSkillRuntimeSequenceMonster.LoadAsyncMonster(runtimeSequenceKey);
                }
                else if (_skill.OwnerType == ConfigCommonSkill.SkillOwnerType.Player)
                {
                    // 플레이어 시퀀스는 시작 로딩에서 제외될 수 있으므로,
                    // 실제 스킬 사용 시점에 필요한 키만 지연 로드합니다.
                    var runtimeSequenceLoader = AddressableLoaderSkillRuntimeSequencePlayer.Instance;
                    if (runtimeSequenceLoader == null)
                    {
                        runtimeSequenceLoader = new GameObject(nameof(AddressableLoaderSkillRuntimeSequencePlayer))
                            .AddComponent<AddressableLoaderSkillRuntimeSequencePlayer>();
                    }

                    _sequence = await runtimeSequenceLoader.LoadByKeyAsync(runtimeSequenceKey);
                }

                _nextEventIndex = 0;
                _time = 0f;

            }
            catch (Exception e)
            {
                Debug.LogException(e);
                IsDone = true;
                EndRun();
            }
            finally
            {
                _isLoading = false;
            }
        }

        /// <summary>
        /// 스킬 실행 시간을 진행합니다.
        /// 차징이 있는 스킬은 차징이 완료된 뒤 기존 캐스팅/사용 단계로 진입합니다.
        /// </summary>
        /// <param name="dt">이번 프레임 경과 시간입니다.</param>
        public void Tick(float dt)
        {
            if (IsDone) return;
            if (_isLoading) return;

            if (_isChargeFailed)
            {
                TickChargeFailed(dt);
                return;
            }

            if (HasCharge && !_chargeCompleted)
            {
                TickCharge(dt);
                return;
            }

            TickCastAndUse(dt);
            TickRuntimeSequence(dt);
        }

        /// <summary>
        /// 피격으로 차징 게이지를 감소시킵니다.
        /// 게이지가 0이 되면 차징 실패 상태로 전환합니다.
        /// </summary>
        /// <param name="reason">피격 또는 취소 사유입니다.</param>
        /// <param name="damageAmount">감소시킬 게이지 값입니다. 0 이하이면 스킬 설정의 기본 감소량을 사용합니다.</param>
        /// <returns>현재 차징 게이지가 피격을 처리했으면 <see langword="true"/>입니다.</returns>
        public bool TryApplyChargeGaugeDamage(SkillCancelReason reason, float damageAmount = 0f)
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

        private void TickCharge(float dt)
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
            while (remaining > 0f && !_chargeCompleted && !_isChargeFailed)
            {
                var stage = GetCurrentChargeStage();
                if (stage == null || stage.DurationSeconds <= 0f)
                {
                    AdvanceChargeStage();
                    continue;
                }

                float stageRemaining = Mathf.Max(0f, stage.DurationSeconds - _chargeStageElapsed);
                float consume = Mathf.Min(remaining, stageRemaining);
                _chargeStageElapsed += consume;
                _chargeTotalElapsed += consume;
                remaining -= consume;

                if (_chargeStageElapsed + 1e-6f >= stage.DurationSeconds)
                {
                    AdvanceChargeStage();
                }
            }

            NotifyChargeSnapshot();
        }

        private void BeginCharge()
        {
            _didCharge = true;
            _chargeCompleted = false;
            _isChargeFailed = false;
            _chargeStageArrayIndex = 0;
            _chargeStageElapsed = 0f;
            _chargeTotalElapsed = 0f;
            _chargeGaugeMax = Mathf.Max(0f, _skill.Charge.GaugeMax);
            if (_chargeGaugeMax <= 0f)
                _chargeGaugeMax = 1f;
            _chargeGaugeCurrent = _chargeGaugeMax;

            EnterChargeStage(_chargeStageArrayIndex);
            NotifyChargeSnapshot();
        }

        private void EnterChargeStage(int stageArrayIndex)
        {
            CleanupActiveChargeVfx();

            var stage = GetChargeStage(stageArrayIndex);
            if (stage == null)
                return;

            PlayChargeLoop(stage);
            SpawnChargeVfx(stage);
        }

        private void AdvanceChargeStage()
        {
            _chargeStageArrayIndex++;
            _chargeStageElapsed = 0f;

            if (_skill.Charge.Stages == null || _chargeStageArrayIndex >= _skill.Charge.Stages.Length)
            {
                CompleteCharge();
                return;
            }

            EnterChargeStage(_chargeStageArrayIndex);
        }

        private void CompleteCharge()
        {
            if (_chargeCompleted)
                return;

            _chargeCompleted = true;
            CleanupActiveChargeVfx();
            PlayChargeComplete();
            NotifyChargeSnapshot();
        }

        private void BreakCharge()
        {
            if (_isChargeFailed || _chargeCompleted)
                return;

            _isChargeFailed = true;
            _chargeCompleted = false;
            _chargeFailElapsed = 0f;
            _chargeFailDuration = ResolveChargeFailDuration();

            CleanupActiveChargeVfx();
            PlayChargeFail();
            _owner?.NotifyChargeFailed(this);
            NotifyChargeSnapshot();

            if (_chargeFailDuration <= 0f)
            {
                IsDone = true;
                EndRun();
            }
        }

        private void TickChargeFailed(float dt)
        {
            _chargeFailElapsed += Mathf.Max(0f, dt);
            if (_chargeFailElapsed < _chargeFailDuration)
                return;

            IsDone = true;
            EndRun();
        }

        private RuntimeSkillChargeStageDefinition GetCurrentChargeStage()
        {
            return GetChargeStage(_chargeStageArrayIndex);
        }

        private RuntimeSkillChargeStageDefinition GetChargeStage(int stageArrayIndex)
        {
            var stages = _skill.Charge.Stages;
            if (stages == null || stageArrayIndex < 0 || stageArrayIndex >= stages.Length)
                return null;

            return stages[stageArrayIndex];
        }

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

        private float ResolveChargeFailDuration()
        {
            if (_skill?.Charge == null)
                return 0f;

            if (_skill.Charge.FailDurationSeconds > 0f)
                return _skill.Charge.FailDurationSeconds;

            if (_animController == null || string.IsNullOrWhiteSpace(_skill.Charge.FailClip))
                return 0.3f;

            float duration = _animController.GetCharacterAnimationDuration(_skill.Charge.FailClip, isMilliseconds: false);
            return duration > 0f ? duration : 0.3f;
        }

        private void SpawnChargeVfx(RuntimeSkillChargeStageDefinition stage)
        {
            if (stage == null || stage.VfxUid <= 0)
                return;

            var sceneGame = SceneGame.Instance;
            if (sceneGame == null || sceneGame.VfxManager == null)
                return;

            CharacterBase casterCharacter = _ctx.caster != null ? _ctx.caster.GetComponentInParent<CharacterBase>() : null;
            Vector3 spawnPosition = casterCharacter != null
                ? casterCharacter.transform.position
                : (_ctx.caster != null ? _ctx.caster.transform.position : _snapshotCasterPos);

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
            };

            _activeChargeVfx = sceneGame.VfxManager.CreateVfx(request);
        }

        private void CleanupActiveChargeVfx()
        {
            if (_activeChargeVfx == null)
                return;

            _activeChargeVfx.DestroyForce();
            _activeChargeVfx = null;
        }

        private void NotifyChargeSnapshot()
        {
            if (!HasCharge)
                return;

            _owner?.NotifyChargeStateChanged(BuildChargeSnapshot());
        }

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

        private void TickCastAndUse(float dt)
        {
            // 1) 캐스팅 처리
            float castTime = Mathf.Max(0f, _skill.CastTime);

            if (castTime > 0f && !_didUse)
            {
                _castElapsed += dt;

                if (!_didCastStart) PlayCastStart();
                if (!_didCastLoop) PlayCastLoop();

                if (_castElapsed >= castTime)
                {
                    PlayCastEnd();
                    ApplyUseActionState();
                    PlayUse();
                }
            }
            else
            {
                if (!_didUse)
                {
                    ApplyUseActionState();
                    PlayUse();
                }
            }
        }

        private void TickRuntimeSequence(float dt)
        {
            // 2) 이벤트 시퀀스 재생(Use 시작부터 재생)
            if (_didUse && _sequence != null && _sequence.Events != null)
            {
                while (_nextEventIndex < _sequence.Events.Length)
                {
                    var ev = _sequence.Events[_nextEventIndex];
                    if (_time + 1e-6f < ev.StartTime) break;

                    _owner.ExecuteEvent(this, _skill, _ctx, _sequence, ev, _snapshotCasterPos, _snapshotTargetPos,
                        _snapshotGroundPoint);
                    _nextEventIndex++;
                }
                _time += dt;

                if (_time >= _sequence.Duration && _nextEventIndex >= _sequence.Events.Length)
                {
                    IsDone = true;
                    EndRun();
                }
            }
            else if (_didUse)
            {
                // 시퀀스가 없으면 Use 클립 길이 정도로 종료(간단 정책)
                _time += dt;
                IsDone = _time >= 0.3f;
                if (IsDone) EndRun();
            }
        }

        private void SnapshotContext()
        {
            _snapshotCasterPos = _ctx.caster != null ? _ctx.caster.transform.position : Vector3.zero;
            _snapshotTargetPos = _ctx.lockedTarget != null ? _ctx.lockedTarget.transform.position : _snapshotCasterPos;
            _snapshotGroundPoint = _ctx.groundPoint;
        }

        private void PlayCastStart()
        {
            if (_didCastStart) return;
            _didCastStart = true;

            // Core 애니메이션 컨트롤러를 우선 사용
            if (GcLogger.IsNull(_animController, nameof(_animController))) return;

            _animController.PlaySkillAnimation(new SkillAnimationRequest(
                _skill.Uid,
                SkillAnimationPhase.CastingStart,
                loop: false,
                timeScale: 1f,
                overrideAnimationName: string.IsNullOrEmpty(_skill.CastStartClip) ? null : _skill.CastStartClip));
        }

        private void PlayCastLoop()
        {
            if (_didCastLoop) return;
            _didCastLoop = true;

            if (GcLogger.IsNull(_animController, nameof(_animController))) return;
            _animController.PlaySkillAnimation(new SkillAnimationRequest(
                _skill.Uid,
                SkillAnimationPhase.CastingLoop,
                loop: true,
                timeScale: 1f,
                overrideAnimationName: string.IsNullOrEmpty(_skill.CastLoopClip) ? null : _skill.CastLoopClip));
        }

        private void PlayCastEnd()
        {
            if (_didCastEnd) return;
            _didCastEnd = true;

            if (GcLogger.IsNull(_animController, nameof(_animController))) return;
            _animController.PlaySkillAnimation(new SkillAnimationRequest(
                _skill.Uid,
                SkillAnimationPhase.CastingEnd,
                loop: false,
                timeScale: 1f,
                overrideAnimationName: string.IsNullOrEmpty(_skill.CastEndClip) ? null : _skill.CastEndClip));
        }

        private void PlayUse()
        {
            if (_didUse) return;
            _didUse = true;

            // Use 시작 기준으로 이벤트 타임라인 리셋
            _time = 0f;
            _nextEventIndex = 0;
            
            if (_actionController != null)
            {
                _actionController.RequestAction(new CharacterActionRequest(
                    CharacterConstants.CharacterStatus.UseSkill));
            }
            
            if (GcLogger.IsNull(_animController, nameof(_animController))) return;
            _animController.PlaySkillAnimation(new SkillAnimationRequest(
                _skill.Uid,
                SkillAnimationPhase.Action,
                loop: false,
                timeScale: 1f,
                overrideAnimationName: string.IsNullOrEmpty(_skill.UseClip) ? null : _skill.UseClip));
        }

        /// <summary>
        /// 스킬 런 시작 시점에 적용할 초기 액션 상태를 설정합니다.
        /// 차징 또는 캐스팅이 있으면 준비 상태(CastingSkill)로 진입하고, 즉시 발동 스킬은 UseSkill로 진입합니다.
        /// </summary>
        private void ApplyInitialActionState()
        {
            if (_actionController == null || _ctx.caster == null)
                return;

            bool hasPreparePhase = HasCharge || _skill.CastTime > 0f;

            var request = new CharacterActionRequest(
                status: hasPreparePhase ? CharacterConstants.CharacterStatus.CastingSkill : CharacterConstants.CharacterStatus.UseSkill,
                skillUid: _skill.Uid,
                lockMove: true,
                lockFacing: false);

            _actionController.RequestAction(in request);
        }
        
        /// <summary>
        /// 실제 스킬 발동(Use) 시점에 UseSkill 상태를 적용합니다.
        /// 캐스팅/차징 → 사용 단계 전환에 해당합니다.
        /// </summary>
        private void ApplyUseActionState()
        {
            if (_actionController == null || _ctx.caster == null)
                return;

            // 준비 상태를 사용 상태로 전환(정책상 필요하면 Clear 후 Request)
            _actionController.ClearAction(CharacterConstants.CharacterStatus.CastingSkill);

            var request = new CharacterActionRequest(
                status: CharacterConstants.CharacterStatus.UseSkill,
                skillUid: _skill.Uid,
                lockMove: true,
                lockFacing: false);

            _actionController.RequestAction(in request);
        }

        public bool TryStartPositionHold(float durationSeconds, bool keepUntilSkillEnd, bool stopAtEnd, bool useMovePosition, bool allowReplace)
        {
            if (_motionController == null || _ctx.caster == null)
                return false;

            // 기존 Skill 채널 모션 잔여 영향 제거
            _motionController.CancelMotion(MotionChannel.Skill, 1001);

            if (_casterRigidbody2D != null)
            {
                _casterRigidbody2D.SetLinearVelocity(Vector2.zero);
            }

            Vector2 holdPos = _casterRigidbody2D != null
                ? _casterRigidbody2D.position
                : (Vector2)_ctx.caster.transform.position;

            var request = new MotionRequest(
                channel: MotionChannel.Skill,
                kind: MotionKind.PositionHold,
                direction: Vector2.right,
                durationSeconds: keepUntilSkillEnd ? 0f : Mathf.Max(0f, durationSeconds),
                distance: 0f,
                easeType: Easing.EaseType.Linear,
                stopAtEnd: stopAtEnd,
                useMovePosition: useMovePosition,
                allowReplace: true,
                startPosition: holdPos,
                targetPosition: holdPos);

            if (_motionController.TryStartMotion(request))
            {
                _isPositionHoldActive = true;
                _keepPositionHoldUntilSkillEnd = keepUntilSkillEnd;
                return true;
            }

            if (_casterRigidbody2D != null)
            {
                _casterRigidbody2D.SetLinearVelocity(Vector2.zero);
            }

            return false;
        }

        public void ReleasePositionHold()
        {
            if (!_isPositionHoldActive)
                return;

            _motionController?.CancelMotion(MotionChannel.Skill, 0);

            if (_casterRigidbody2D != null)
            {
                _casterRigidbody2D.SetLinearVelocity(Vector2.zero);
            }

            _isPositionHoldActive = false;
            _keepPositionHoldUntilSkillEnd = false;
        }

        /// <summary>
        /// 스킬 런 종료 처리(상태 해제 + 정리). 중복 호출 방지 포함.
        /// </summary>
        private void EndRun()
        {
            if (_isEnded)
                return;

            _isEnded = true;

            CleanupActiveChargeVfx();

            if (HasCharge)
            {
                _owner?.NotifyChargeStateChanged(SkillChargeSnapshot.Inactive(SkillUid));
            }

            if (_keepPositionHoldUntilSkillEnd || _isPositionHoldActive)
            {
                ReleasePositionHold();
            }

            if (_actionController != null)
            {
                _actionController.ClearAction(CharacterConstants.CharacterStatus.UseSkill);
                _actionController.ClearAction(CharacterConstants.CharacterStatus.CastingSkill);
            }

            _owner?.NotifyRunEnded(this);
        }
        
        public void Cancel(SkillCancelReason reason)
        {
            if (IsDone) return;

            // 이후 이벤트/캐스팅/차징 진행 차단
            IsDone = true;
            CleanupActiveChargeVfx();

            if (HasCharge)
            {
                _owner?.NotifyChargeStateChanged(SkillChargeSnapshot.Inactive(SkillUid));
            }

            // 체인 캔슬은 다음 스킬 애니메이션이 같은 프레임에 이어서 재생되므로
            // 대기 애니메이션으로 한 번 복귀시키지 않고 현재 스킬 재생만 끊습니다.
            if (reason != SkillCancelReason.ComboChain && reason != SkillCancelReason.ForcedBySystem)
            {
                _animController?.StopSkillAnimation();
            }

            // 상태 해제(UseSkill/CastingSkill 등)
            EndRun();
        }
    }
}
