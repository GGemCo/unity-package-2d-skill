using System;
using System.Threading.Tasks;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
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

        private float _castElapsed;
        private Vector3 _snapshotCasterPos;
        private Vector3 _snapshotTargetPos;
        private Vector3 _snapshotGroundPoint;

        private bool _isLoading;
        private bool _isEnded;
        private bool _isGravityScaleOverridden;
        private float _savedGravityScale;
        private bool _isZeroGravityHoldStarted;

        public bool IsDone { get; private set; }
        public int SkillUid => _skill != null ? _skill.Uid : 0;
        public GameObject Caster => _ctx.caster;

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

        public void Start()
        {
            SnapshotContext();

            TryApplyGravityScaleOverride();
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
                    _sequence =
                        AddressableLoaderSkillRuntimeSequencePlayer.Instance.GetSkillRuntimeSequenceByKey(
                            runtimeSequenceKey);
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

        public void Tick(float dt)
        {
            if (IsDone) return;
            if (_isLoading) return;

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
        /// (캐스팅 유무에 따라 CastingSkill 또는 UseSkill)
        /// </summary>
        private void ApplyInitialActionState()
        {
            if (_actionController == null || _ctx.caster == null)
                return;

            // 캐스팅 시간이 존재하면 캐스팅 상태로 진입
            bool hasCasting = _skill.CastTime > 0f; // 프로젝트에서 사용하는 캐스팅 판정 기준에 맞추세요.

            var request = new CharacterActionRequest(
                status: hasCasting ? CharacterConstants.CharacterStatus.CastingSkill : CharacterConstants.CharacterStatus.UseSkill,
                skillUid: _skill.Uid,
                lockMove: true,
                lockFacing: false);

            _actionController.RequestAction(in request);
        }
        
        /// <summary>
        /// 실제 스킬 발동(Use) 시점에 UseSkill 상태를 적용합니다.
        /// 캐스팅 → 사용 단계 전환에 해당합니다.
        /// </summary>
        private void ApplyUseActionState()
        {
            if (_actionController == null || _ctx.caster == null)
                return;

            // 캐스팅 상태를 사용 상태로 전환(정책상 필요하면 Clear 후 Request)
            _actionController.ClearAction(CharacterConstants.CharacterStatus.CastingSkill);

            var request = new CharacterActionRequest(
                status: CharacterConstants.CharacterStatus.UseSkill,
                skillUid: _skill.Uid,
                lockMove: true,
                lockFacing: false);

            _actionController.RequestAction(in request);
        }
        
        private void TryApplyGravityScaleOverride()
        {
            if (_skill == null || !_skill.UseGravityScaleOverride)
                return;

            if (_casterRigidbody2D == null)
                return;

            if (_isGravityScaleOverridden)
                return;

            _savedGravityScale = _casterRigidbody2D.gravityScale;
            _casterRigidbody2D.gravityScale = _skill.GravityScaleOverride;
            _isGravityScaleOverridden = true;
            
            if (Mathf.Approximately(_skill.GravityScaleOverride, 0f))
            {
                TryStartZeroGravityHold();
            }
        }
        
        private void TryStartZeroGravityHold()
        {
            if (_isZeroGravityHoldStarted)
                return;

            if (_motionController != null && _ctx.caster != null)
            {
                Vector2 holdPos = _casterRigidbody2D != null
                    ? _casterRigidbody2D.position
                    : (Vector2)_ctx.caster.transform.position;

                var request = new MotionRequest(
                    channel: MotionChannel.Skill,
                    kind: MotionKind.PositionHold,
                    direction: Vector2.right,
                    durationSeconds: 0f, // cancel될 때까지 유지되도록 Core 쪽 의미 확장
                    distance: 0f,
                    easeType: Easing.EaseType.Linear,
                    stopAtEnd: true,
                    useMovePosition: true,
                    allowReplace: true,
                    startPosition: holdPos,
                    targetPosition: holdPos);

                if (_motionController.TryStartMotion(request))
                {
                    _isZeroGravityHoldStarted = true;
                    return;
                }
            }

            // fallback
            _casterRigidbody2D.SetLinearVelocity(Vector2.zero);
        }
        
        private void RestoreGravityScaleOverride()
        {
            if (!_isGravityScaleOverridden)
                return;

            if (_casterRigidbody2D != null)
            {
                _casterRigidbody2D.gravityScale = _savedGravityScale;
            }

            _savedGravityScale = 0f;
            _isGravityScaleOverridden = false;
        }

        /// <summary>
        /// 스킬 런 종료 처리(상태 해제 + 정리). 중복 호출 방지 포함.
        /// </summary>
        private void EndRun()
        {
            if (_isEnded)
                return;

            _isEnded = true;

            if (_isZeroGravityHoldStarted)
            {
                _motionController?.CancelMotion(MotionChannel.Skill, 0);
                _isZeroGravityHoldStarted = false;
            }

            RestoreGravityScaleOverride();

            if (_actionController != null)
            {
                _actionController.ClearAction(CharacterConstants.CharacterStatus.UseSkill);
                _actionController.ClearAction(CharacterConstants.CharacterStatus.CastingSkill);
            }

            // 이 아래는 프로젝트 구조에 맞춰 정리 호출을 넣으세요.
            // 예) 이펙트/타임라인 정리, 콜백 호출, SkillExecutor에게 완료 알림 등
            _owner?.NotifyRunEnded(this);
        }
        
        public void Cancel(SkillCancelReason reason)
        {
            if (IsDone) return;

            // 이후 이벤트/캐스팅 진행 차단
            IsDone = true;

            // 체인 캔슬은 다음 스킬 애니메이션이 같은 프레임에 이어서 재생되므로
            // 대기 애니메이션으로 한 번 복귀시키지 않고 현재 스킬 재생만 끊습니다.
            if (reason != SkillCancelReason.ComboChain)
            {
                _animController?.StopSkillAnimation();
            }

            // 모션 이동(러시/대시 등) 중단
            _motionController?.CancelMotion(MotionChannel.Skill, 999);

            // 상태 해제(UseSkill/CastingSkill 등)
            EndRun();
        }

    }
}