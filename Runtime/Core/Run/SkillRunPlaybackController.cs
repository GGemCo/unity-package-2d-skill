using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 런의 캐스팅, Use 전환, RuntimeSequence 이벤트 재생 흐름을 담당합니다.
    /// </summary>
    internal sealed class SkillRunPlaybackController
    {
        private readonly SkillExecutor _owner;
        private readonly SkillRun _run;
        private readonly RuntimeSkillDefinition _skill;
        private readonly SkillTargetContext _ctx;
        private readonly ICharacterAnimationController _animController;
        private readonly ICharacterActionController _actionController;
        private readonly System.Func<Vector3> _snapshotCasterPositionProvider;
        private readonly System.Func<Vector3> _snapshotTargetPositionProvider;
        private readonly System.Func<Vector3> _snapshotGroundPointProvider;

        private SkillRuntimeSequence _sequence;
        private float _time;
        private int _nextEventIndex;
        private float _castElapsed;
        private bool _didCastStart;
        private bool _didCastLoop;
        private bool _didCastEnd;
        private bool _didUse;

        /// <summary>
        /// Use 애니메이션 기준으로 현재 스킬 이벤트 시퀀스가 진행된 시간입니다.
        /// </summary>
        public float CurrentTime => _time;

        /// <summary>
        /// 캐스팅/Use/시퀀스 재생 결과로 스킬 런 종료가 필요한지 여부입니다.
        /// </summary>
        public bool ShouldEndRun { get; private set; }

        /// <summary>
        /// 스킬 런 재생 컨트롤러를 생성하고 실행 이벤트에 필요한 의존성과 스냅샷 제공자를 저장합니다.
        /// </summary>
        /// <param name="owner">이벤트 실행을 위임할 스킬 실행기입니다.</param>
        /// <param name="run">현재 재생 중인 스킬 런입니다.</param>
        /// <param name="skill">실행할 런타임 스킬 정의입니다.</param>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="animController">캐스팅/Use 애니메이션을 재생할 컨트롤러입니다.</param>
        /// <param name="actionController">캐스팅/Use 액션 상태를 적용할 컨트롤러입니다.</param>
        /// <param name="snapshotCasterPositionProvider">스킬 시작 시점 캐스터 위치 제공자입니다.</param>
        /// <param name="snapshotTargetPositionProvider">스킬 시작 시점 타겟 위치 제공자입니다.</param>
        /// <param name="snapshotGroundPointProvider">스킬 시작 시점 지면 좌표 제공자입니다.</param>
        public SkillRunPlaybackController(
            SkillExecutor owner,
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext ctx,
            ICharacterAnimationController animController,
            ICharacterActionController actionController,
            System.Func<Vector3> snapshotCasterPositionProvider,
            System.Func<Vector3> snapshotTargetPositionProvider,
            System.Func<Vector3> snapshotGroundPointProvider)
        {
            _owner = owner;
            _run = run;
            _skill = skill;
            _ctx = ctx;
            _animController = animController;
            _actionController = actionController;
            _snapshotCasterPositionProvider = snapshotCasterPositionProvider ?? (() => Vector3.zero);
            _snapshotTargetPositionProvider = snapshotTargetPositionProvider ?? (() => Vector3.zero);
            _snapshotGroundPointProvider = snapshotGroundPointProvider ?? (() => Vector3.zero);
        }

        /// <summary>
        /// 로드된 런타임 시퀀스를 등록하고 Use 기준 재생 시간을 초기화합니다.
        /// </summary>
        /// <param name="sequence">Addressables 또는 에디터 오버라이드에서 로드한 런타임 시퀀스입니다.</param>
        public void SetSequence(SkillRuntimeSequence sequence)
        {
            _sequence = sequence;
            _nextEventIndex = 0;
            _time = 0f;
        }

        /// <summary>
        /// 스킬 런 시작 시점에 적용할 초기 액션 상태를 설정합니다.
        /// 차징 또는 캐스팅이 있으면 준비 상태(CastingSkill)로 진입하고, 즉시 발동 스킬은 UseSkill로 진입합니다.
        /// </summary>
        /// <param name="hasCharge">현재 스킬이 차징 단계를 사용하는지 여부입니다.</param>
        public void ApplyInitialActionState(bool hasCharge)
        {
            if (_actionController == null || _ctx.caster == null)
                return;

            bool hasPreparePhase = hasCharge || _skill.CastTime > 0f;

            var request = new CharacterActionRequest(
                status: hasPreparePhase ? CharacterConstants.CharacterStatus.CastingSkill : CharacterConstants.CharacterStatus.UseSkill,
                skillUid: _skill.Uid,
                lockMove: true,
                lockFacing: false);

            _actionController.RequestAction(in request);
        }

        /// <summary>
        /// 캐스팅/Use 전환과 런타임 이벤트 시퀀스를 한 프레임 진행합니다.
        /// </summary>
        /// <param name="dt">이번 프레임 경과 시간입니다.</param>
        public void Tick(float dt)
        {
            if (ShouldEndRun)
                return;

            TickCastAndUse(dt);
            TickRuntimeSequence(dt);
        }

        /// <summary>
        /// 스킬 종료 또는 취소 시 캐스팅/Use 액션 상태를 해제합니다.
        /// </summary>
        public void CleanupActionState()
        {
            if (_actionController == null)
                return;

            _actionController.ClearAction(CharacterConstants.CharacterStatus.UseSkill);
            _actionController.ClearAction(CharacterConstants.CharacterStatus.CastingSkill);
        }

        /// <summary>
        /// 외부 취소 흐름에서 현재 스킬 애니메이션을 중단합니다.
        /// </summary>
        public void StopSkillAnimation()
        {
            _animController?.StopSkillAnimation();
        }

        /// <summary>
        /// 차징 이후 캐스팅 시간을 누적하고, 사용 단계에 도달하면 Use 애니메이션과 액션 상태를 적용합니다.
        /// </summary>
        /// <param name="dt">이번 프레임 경과 시간입니다.</param>
        private void TickCastAndUse(float dt)
        {
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
            else if (!_didUse)
            {
                ApplyUseActionState();
                PlayUse();
            }
        }

        /// <summary>
        /// Use 시작 시간을 기준으로 런타임 이벤트 시퀀스를 재생하고 완료 시 스킬 런 종료를 요청합니다.
        /// </summary>
        /// <param name="dt">이번 프레임 경과 시간입니다.</param>
        private void TickRuntimeSequence(float dt)
        {
            if (_didUse && _sequence != null && _sequence.Events != null)
            {
                while (_nextEventIndex < _sequence.Events.Length)
                {
                    var ev = _sequence.Events[_nextEventIndex];
                    if (_time + 1e-6f < ev.StartTime) break;

                    _owner.ExecuteEvent(
                        _run,
                        _skill,
                        _ctx,
                        _sequence,
                        ev,
                        _snapshotCasterPositionProvider(),
                        _snapshotTargetPositionProvider(),
                        _snapshotGroundPointProvider());
                    _nextEventIndex++;
                }

                _time += dt;

                if (_time >= _sequence.Duration && _nextEventIndex >= _sequence.Events.Length)
                {
                    ShouldEndRun = true;
                }
            }
            else if (_didUse)
            {
                _time += dt;
                ShouldEndRun = _time >= 0.3f;
            }
        }

        /// <summary>
        /// 캐스팅 시작 애니메이션을 한 번만 재생합니다.
        /// </summary>
        private void PlayCastStart()
        {
            if (_didCastStart) return;
            _didCastStart = true;

            if (GcLogger.IsNull(_animController, nameof(_animController))) return;

            _animController.PlaySkillAnimation(new SkillAnimationRequest(
                _skill.Uid,
                SkillAnimationPhase.CastingStart,
                loop: false,
                timeScale: 1f,
                overrideAnimationName: string.IsNullOrEmpty(_skill.CastStartClip) ? null : _skill.CastStartClip));
        }

        /// <summary>
        /// 캐스팅 유지 루프 애니메이션을 한 번만 재생합니다.
        /// </summary>
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

        /// <summary>
        /// 캐스팅 종료 애니메이션을 한 번만 재생합니다.
        /// </summary>
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

        /// <summary>
        /// 실제 스킬 사용 단계로 진입하고 이벤트 타임라인 기준 시간을 초기화합니다.
        /// </summary>
        private void PlayUse()
        {
            if (_didUse) return;
            _didUse = true;

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
        /// 실제 스킬 발동(Use) 시점에 UseSkill 상태를 적용합니다.
        /// 캐스팅/차징 준비 상태를 사용 상태로 전환합니다.
        /// </summary>
        private void ApplyUseActionState()
        {
            if (_actionController == null || _ctx.caster == null)
                return;

            _actionController.ClearAction(CharacterConstants.CharacterStatus.CastingSkill);

            var request = new CharacterActionRequest(
                status: CharacterConstants.CharacterStatus.UseSkill,
                skillUid: _skill.Uid,
                lockMove: true,
                lockFacing: false);

            _actionController.RequestAction(in request);
        }
    }
}
