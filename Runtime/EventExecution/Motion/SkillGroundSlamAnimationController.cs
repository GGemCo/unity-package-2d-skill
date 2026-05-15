using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 그라운드슬램 이벤트의 대기 상태와 애니메이션 전환 상태를 관리합니다.
    /// </summary>
    internal sealed class SkillGroundSlamAnimationController
    {
        private enum GroundSlamAnimationPhaseState
        {
            None = 0,
            Start = 1,
            FallLoop = 2,
            LandEnd = 3,
        }

        private struct GroundSlamAnimationState
        {
            public bool IsActive;
            public ICharacterAnimationController AnimationController;
            public ICharacterMotionController MotionController;
            public GroundSlamEventDefinition Definition;
            public GroundSlamAnimationPhaseState Phase;
            public bool UsePhaseBasedLoopTransition;
            public bool IsInstantLandSequence;
            public float PhaseRemainingSeconds;
        }

        private struct PendingGroundSlamState
        {
            public bool IsActive;
            public GameObject Caster;
            public ICharacterMotionController MotionController;
            public GroundSlamEventDefinition Definition;
            public Vector2 StartPosition;
            public Vector2 TargetPosition;
            public float FallDurationSeconds;
            public float HoldRemainingSeconds;
            public bool UsePhaseBasedLoopTransition;
        }

        private GroundSlamAnimationState _animationState;
        private PendingGroundSlamState _pendingState;

        /// <summary>
        /// 그라운드슬램 대기 상태와 애니메이션 상태를 프레임 단위로 갱신합니다.
        /// </summary>
        public void Tick()
        {
            UpdatePendingSlam();
            UpdateAnimation();
        }

        /// <summary>
        /// 공중 대기 이후 착지 모션으로 전환될 그라운드슬램 상태를 예약합니다.
        /// </summary>
        /// <param name="caster">스킬을 실행하는 캐스터 오브젝트입니다.</param>
        /// <param name="motion">착지 모션을 실행할 캐릭터 모션 컨트롤러입니다.</param>
        /// <param name="def">그라운드슬램 이벤트 정의입니다.</param>
        /// <param name="startPosition">낙하 시작 월드 좌표입니다.</param>
        /// <param name="targetPosition">낙하 도착 월드 좌표입니다.</param>
        /// <param name="fallDurationSeconds">낙하 지속 시간(초)입니다.</param>
        /// <param name="holdDurationSeconds">공중 대기 지속 시간(초)입니다.</param>
        public void BeginPendingSlam(
            GameObject caster,
            ICharacterMotionController motion,
            GroundSlamEventDefinition def,
            Vector2 startPosition,
            Vector2 targetPosition,
            float fallDurationSeconds,
            float holdDurationSeconds)
        {
            ClearPendingState();
            BeginMotionAnimation(caster, motion, def, usePhaseBasedLoopTransition: true);
            _pendingState = new PendingGroundSlamState
            {
                IsActive = true,
                Caster = caster,
                MotionController = motion,
                Definition = def,
                StartPosition = startPosition,
                TargetPosition = targetPosition,
                FallDurationSeconds = fallDurationSeconds,
                HoldRemainingSeconds = holdDurationSeconds,
                UsePhaseBasedLoopTransition = true,
            };
        }

        /// <summary>
        /// 실제 낙하 모션과 함께 재생되는 그라운드슬램 애니메이션 상태를 시작합니다.
        /// </summary>
        /// <param name="caster">스킬을 실행하는 캐스터 오브젝트입니다.</param>
        /// <param name="motion">스킬 모션 진행률을 조회할 캐릭터 모션 컨트롤러입니다.</param>
        /// <param name="def">그라운드슬램 이벤트 정의입니다.</param>
        /// <param name="usePhaseBasedLoopTransition">대기 구간 종료 시 루프 애니메이션으로 전환할지 여부입니다.</param>
        public void BeginMotionAnimation(
            GameObject caster,
            ICharacterMotionController motion,
            GroundSlamEventDefinition def,
            bool usePhaseBasedLoopTransition)
        {
            ClearAnimationState();

            if (caster == null || motion == null || def == null)
                return;

            var anim = SkillCharacterComponentResolver.ResolveAnimationController(caster);
            if (anim == null)
                return;

            _animationState = new GroundSlamAnimationState
            {
                IsActive = true,
                AnimationController = anim,
                MotionController = motion,
                Definition = def,
                Phase = GroundSlamAnimationPhaseState.None,
                UsePhaseBasedLoopTransition = usePhaseBasedLoopTransition,
                IsInstantLandSequence = false,
                PhaseRemainingSeconds = 0f,
            };

            if (!string.IsNullOrWhiteSpace(def.startAnimationName))
            {
                PlayAnimation(anim, def.startAnimationName, loop: false);
                _animationState.Phase = GroundSlamAnimationPhaseState.Start;
                return;
            }

            if (!string.IsNullOrWhiteSpace(def.fallLoopAnimationName))
            {
                PlayAnimation(anim, def.fallLoopAnimationName, loop: true);
                _animationState.Phase = GroundSlamAnimationPhaseState.FallLoop;
                return;
            }

            _animationState.Phase = GroundSlamAnimationPhaseState.None;
        }

        /// <summary>
        /// 이동 거리가 없는 그라운드슬램의 시작 후 착지 종료 애니메이션 시퀀스를 시작합니다.
        /// </summary>
        /// <param name="caster">스킬을 실행하는 캐스터 오브젝트입니다.</param>
        /// <param name="motion">현재 캐릭터 모션 컨트롤러입니다.</param>
        /// <param name="def">그라운드슬램 이벤트 정의입니다.</param>
        public void BeginInstantLandSequence(
            GameObject caster,
            ICharacterMotionController motion,
            GroundSlamEventDefinition def)
        {
            Clear();

            if (caster == null || motion == null || def == null)
                return;

            var anim = SkillCharacterComponentResolver.ResolveAnimationController(caster);
            if (anim == null)
                return;

            _animationState = new GroundSlamAnimationState
            {
                IsActive = true,
                AnimationController = anim,
                MotionController = motion,
                Definition = def,
                Phase = GroundSlamAnimationPhaseState.None,
                UsePhaseBasedLoopTransition = false,
                IsInstantLandSequence = true,
                PhaseRemainingSeconds = 0f,
            };

            if (!string.IsNullOrWhiteSpace(def.startAnimationName))
            {
                PlayAnimation(anim, def.startAnimationName, loop: false);
                _animationState.Phase = GroundSlamAnimationPhaseState.Start;
                _animationState.PhaseRemainingSeconds = GetAnimationDurationSafe(anim, def.startAnimationName);
                return;
            }

            PlayLandEndOrClear();
        }

        /// <summary>
        /// 예약된 그라운드슬램 대기와 애니메이션 상태를 모두 초기화합니다.
        /// </summary>
        public void Clear()
        {
            ClearPendingState();
            ClearAnimationState();
        }

        /// <summary>
        /// 공중 대기 시간이 끝난 그라운드슬램을 실제 낙하 모션으로 전환합니다.
        /// </summary>
        private void UpdatePendingSlam()
        {
            if (!_pendingState.IsActive)
                return;

            if (_pendingState.Caster == null ||
                _pendingState.MotionController == null ||
                _pendingState.Definition == null)
            {
                Clear();
                return;
            }

            _pendingState.HoldRemainingSeconds -= Time.deltaTime;
            if (_pendingState.HoldRemainingSeconds > 0f)
                return;

            var pending = _pendingState;
            ClearPendingState();

            if (!SkillGroundSlamMotionResolver.TryStartSlamMotion(
                    pending.MotionController,
                    pending.Definition,
                    pending.StartPosition,
                    pending.TargetPosition,
                    pending.FallDurationSeconds))
            {
                ClearAnimationState();
                return;
            }

            EnterFallLoopAfterPending(pending);
        }

        /// <summary>
        /// 공중 대기에서 낙하로 전환된 뒤 현재 애니메이션 상태를 낙하 루프 단계로 맞춥니다.
        /// </summary>
        /// <param name="pending">낙하로 전환된 예약 상태입니다.</param>
        private void EnterFallLoopAfterPending(PendingGroundSlamState pending)
        {
            if (_animationState.IsActive)
            {
                _animationState.MotionController = pending.MotionController;
                _animationState.Definition = pending.Definition;
                if (!string.IsNullOrWhiteSpace(pending.Definition.fallLoopAnimationName))
                    PlayAnimation(_animationState.AnimationController, pending.Definition.fallLoopAnimationName, loop: true);

                _animationState.Phase = GroundSlamAnimationPhaseState.FallLoop;
                return;
            }

            BeginMotionAnimation(pending.Caster, pending.MotionController, pending.Definition, pending.UsePhaseBasedLoopTransition);
        }

        /// <summary>
        /// 즉시 착지 시퀀스의 시작/착지 종료 애니메이션 지속 시간을 갱신합니다.
        /// </summary>
        private void UpdateInstantLandSequence()
        {
            if (!_animationState.IsActive)
                return;

            _animationState.PhaseRemainingSeconds -= Time.deltaTime;
            if (_animationState.PhaseRemainingSeconds > 0f)
                return;

            switch (_animationState.Phase)
            {
                case GroundSlamAnimationPhaseState.Start:
                    PlayLandEndOrClear();
                    return;
                case GroundSlamAnimationPhaseState.LandEnd:
                    ClearAnimationState();
                    return;
                default:
                    ClearAnimationState();
                    return;
            }
        }

        /// <summary>
        /// 착지 종료 애니메이션이 있으면 재생하고, 없으면 애니메이션 상태를 종료합니다.
        /// </summary>
        private void PlayLandEndOrClear()
        {
            if (!_animationState.IsActive)
                return;

            var anim = _animationState.AnimationController;
            var def = _animationState.Definition;
            if (anim == null || def == null)
            {
                ClearAnimationState();
                return;
            }

            if (!string.IsNullOrWhiteSpace(def.landEndAnimationName))
            {
                PlayAnimation(anim, def.landEndAnimationName, loop: false);
                _animationState.Phase = GroundSlamAnimationPhaseState.LandEnd;
                _animationState.PhaseRemainingSeconds = GetAnimationDurationSafe(anim, def.landEndAnimationName);
                return;
            }

            ClearAnimationState();
        }

        /// <summary>
        /// 진행 중인 그라운드슬램 모션 상태에 맞춰 시작, 루프, 착지 종료 애니메이션을 전환합니다.
        /// </summary>
        private void UpdateAnimation()
        {
            if (!_animationState.IsActive)
                return;

            var anim = _animationState.AnimationController;
            var motion = _animationState.MotionController;
            var def = _animationState.Definition;

            if (anim == null || motion == null || def == null)
            {
                ClearAnimationState();
                return;
            }

            if (_animationState.IsInstantLandSequence)
            {
                UpdateInstantLandSequence();
                return;
            }

            if (!motion.IsPlaying(MotionChannel.Skill))
            {
                if (IsWaitingForPendingSlam(motion, def))
                    return;

                if (_animationState.Phase != GroundSlamAnimationPhaseState.LandEnd &&
                    !string.IsNullOrWhiteSpace(def.landEndAnimationName))
                {
                    PlayAnimation(anim, def.landEndAnimationName, loop: false);
                    _animationState.Phase = GroundSlamAnimationPhaseState.LandEnd;
                }

                ClearAnimationState();
                return;
            }

            if (_animationState.Phase != GroundSlamAnimationPhaseState.Start)
                return;

            if (_animationState.UsePhaseBasedLoopTransition)
                return;

            if (!motion.TryGetMotionProgress(MotionChannel.Skill, out float progress01))
                return;

            if (progress01 < Mathf.Clamp01(def.startToLoopNormalizedTime))
                return;

            if (string.IsNullOrWhiteSpace(def.fallLoopAnimationName))
            {
                _animationState.Phase = GroundSlamAnimationPhaseState.FallLoop;
                return;
            }

            PlayAnimation(anim, def.fallLoopAnimationName, loop: true);
            _animationState.Phase = GroundSlamAnimationPhaseState.FallLoop;
        }

        /// <summary>
        /// 현재 모션 정지가 공중 대기 후 낙하 전환을 기다리는 상태인지 확인합니다.
        /// </summary>
        /// <param name="motion">현재 애니메이션 상태가 참조하는 모션 컨트롤러입니다.</param>
        /// <param name="def">현재 애니메이션 상태가 참조하는 그라운드슬램 정의입니다.</param>
        /// <returns>예약된 대기 상태와 동일한 모션/정의이면 <see langword="true"/>입니다.</returns>
        private bool IsWaitingForPendingSlam(ICharacterMotionController motion, GroundSlamEventDefinition def)
        {
            return _pendingState.IsActive &&
                   ReferenceEquals(_pendingState.MotionController, motion) &&
                   ReferenceEquals(_pendingState.Definition, def);
        }

        /// <summary>
        /// 캐릭터 애니메이션 길이를 안전하게 조회하고 최소 재생 시간을 보장합니다.
        /// </summary>
        /// <param name="anim">길이를 조회할 애니메이션 컨트롤러입니다.</param>
        /// <param name="animationName">조회할 애니메이션 이름입니다.</param>
        /// <returns>최소값이 보정된 애니메이션 길이(초)입니다.</returns>
        private static float GetAnimationDurationSafe(ICharacterAnimationController anim, string animationName)
        {
            if (anim == null || string.IsNullOrWhiteSpace(animationName))
                return 0.05f;

            float duration = anim.GetCharacterAnimationDuration(animationName, isMilliseconds: false);
            return Mathf.Max(0.05f, duration);
        }

        /// <summary>
        /// 지정한 캐릭터 애니메이션을 스킬 액션 단계로 재생합니다.
        /// </summary>
        /// <param name="anim">애니메이션을 재생할 컨트롤러입니다.</param>
        /// <param name="animationName">재생할 애니메이션 이름입니다.</param>
        /// <param name="loop">반복 재생 여부입니다.</param>
        private static void PlayAnimation(ICharacterAnimationController anim, string animationName, bool loop)
        {
            if (anim == null || string.IsNullOrWhiteSpace(animationName))
                return;

            anim.PlaySkillAnimation(new SkillAnimationRequest(
                skillUid: 0,
                phase: SkillAnimationPhase.Action,
                loop: loop,
                timeScale: 1f,
                overrideAnimationName: animationName));
        }

        /// <summary>
        /// 현재 그라운드슬램 애니메이션 상태를 초기화합니다.
        /// </summary>
        private void ClearAnimationState()
        {
            _animationState = default;
        }

        /// <summary>
        /// 공중 대기 후 낙하로 전환될 예약 상태를 초기화합니다.
        /// </summary>
        private void ClearPendingState()
        {
            _pendingState = default;
        }
    }
}
