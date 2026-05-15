using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 아크 런지 이벤트의 상승, 정점, 하강, 착지 종료 애니메이션 상태를 관리합니다.
    /// </summary>
    internal sealed class SkillArcLungeAnimationController
    {
        private enum ArcLungeAnimationPhaseState
        {
            None = 0,
            Rise = 1,
            Apex = 2,
            Fall = 3,
            LandEnd = 4,
        }

        private struct ArcLungeAnimationState
        {
            public bool IsActive;
            public ICharacterAnimationController AnimationController;
            public ICharacterMotionController MotionController;
            public ArcLungeEventDefinition Definition;
            public ArcLungeAnimationPhaseState Phase;
            public float ElapsedSeconds;
            public float RiseDurationSeconds;
            public float ApexHoldDurationSeconds;
            public float LandEndRemainingSeconds;
        }

        private ArcLungeAnimationState _state;

        /// <summary>
        /// 아크 런지 애니메이션 상태를 프레임 단위로 갱신합니다.
        /// </summary>
        public void Tick()
        {
            if (!_state.IsActive)
                return;

            var anim = _state.AnimationController;
            var motion = _state.MotionController;
            var def = _state.Definition;
            if (anim == null || motion == null || def == null)
            {
                Clear();
                return;
            }

            if (_state.Phase == ArcLungeAnimationPhaseState.LandEnd)
            {
                _state.LandEndRemainingSeconds -= Time.deltaTime;
                if (_state.LandEndRemainingSeconds <= 0f)
                    Clear();
                return;
            }

            if (!motion.IsPlaying(MotionChannel.Skill))
            {
                PlayLandEndOrClear();
                return;
            }

            _state.ElapsedSeconds += Time.deltaTime;
            UpdatePhaseByElapsedTime(anim, def);
        }

        /// <summary>
        /// 아크 런지 모션 시작 후 단계별 애니메이션 상태를 초기화하고 첫 애니메이션을 재생합니다.
        /// </summary>
        /// <param name="caster">스킬을 실행하는 캐스터 오브젝트입니다.</param>
        /// <param name="motion">아크 런지 모션 진행 상태를 조회할 모션 컨트롤러입니다.</param>
        /// <param name="def">아크 런지 이벤트 정의입니다.</param>
        /// <param name="riseDurationSeconds">상승 단계 지속 시간(초)입니다.</param>
        /// <param name="apexHoldDurationSeconds">정점 대기 단계 지속 시간(초)입니다.</param>
        public void Begin(
            GameObject caster,
            ICharacterMotionController motion,
            ArcLungeEventDefinition def,
            float riseDurationSeconds,
            float apexHoldDurationSeconds)
        {
            Clear();

            if (caster == null || motion == null || def == null)
                return;

            var anim = SkillCharacterComponentResolver.ResolveAnimationController(caster);
            if (anim == null)
                return;

            _state = new ArcLungeAnimationState
            {
                IsActive = true,
                AnimationController = anim,
                MotionController = motion,
                Definition = def,
                Phase = ArcLungeAnimationPhaseState.None,
                ElapsedSeconds = 0f,
                RiseDurationSeconds = Mathf.Max(0f, riseDurationSeconds),
                ApexHoldDurationSeconds = Mathf.Max(0f, apexHoldDurationSeconds),
                LandEndRemainingSeconds = 0f,
            };

            PlayInitialPhase(anim, def);
        }

        /// <summary>
        /// 현재 아크 런지 애니메이션 상태를 초기화합니다.
        /// </summary>
        public void Clear()
        {
            _state = default;
        }

        /// <summary>
        /// 설정된 단계 지속 시간과 애니메이션 이름에 따라 첫 재생 단계를 선택합니다.
        /// </summary>
        /// <param name="anim">애니메이션을 재생할 컨트롤러입니다.</param>
        /// <param name="def">아크 런지 이벤트 정의입니다.</param>
        private void PlayInitialPhase(ICharacterAnimationController anim, ArcLungeEventDefinition def)
        {
            if (_state.RiseDurationSeconds > 0f && !string.IsNullOrWhiteSpace(def.riseAnimationName))
            {
                PlayAnimation(anim, def.riseAnimationName, loop: false);
                _state.Phase = ArcLungeAnimationPhaseState.Rise;
                return;
            }

            if (_state.ApexHoldDurationSeconds > 0f && !string.IsNullOrWhiteSpace(def.apexAnimationName))
            {
                PlayAnimation(anim, def.apexAnimationName, loop: true);
                _state.Phase = ArcLungeAnimationPhaseState.Apex;
                return;
            }

            if (!string.IsNullOrWhiteSpace(def.fallAnimationName))
            {
                PlayAnimation(anim, def.fallAnimationName, loop: true);
                _state.Phase = ArcLungeAnimationPhaseState.Fall;
            }
        }

        /// <summary>
        /// 누적 시간에 따라 상승에서 정점 또는 하강 단계로 애니메이션을 전환합니다.
        /// </summary>
        /// <param name="anim">애니메이션을 재생할 컨트롤러입니다.</param>
        /// <param name="def">아크 런지 이벤트 정의입니다.</param>
        private void UpdatePhaseByElapsedTime(ICharacterAnimationController anim, ArcLungeEventDefinition def)
        {
            float riseEnd = _state.RiseDurationSeconds;
            float apexEnd = riseEnd + _state.ApexHoldDurationSeconds;

            if (_state.Phase == ArcLungeAnimationPhaseState.Rise && _state.ElapsedSeconds >= riseEnd)
            {
                PlayApexOrFall(anim, def);
            }

            if (_state.Phase == ArcLungeAnimationPhaseState.Apex && _state.ElapsedSeconds >= apexEnd)
            {
                if (!string.IsNullOrWhiteSpace(def.fallAnimationName))
                    PlayAnimation(anim, def.fallAnimationName, loop: true);

                _state.Phase = ArcLungeAnimationPhaseState.Fall;
            }
        }

        /// <summary>
        /// 상승 단계가 끝났을 때 정점 대기 애니메이션 또는 하강 애니메이션으로 전환합니다.
        /// </summary>
        /// <param name="anim">애니메이션을 재생할 컨트롤러입니다.</param>
        /// <param name="def">아크 런지 이벤트 정의입니다.</param>
        private void PlayApexOrFall(ICharacterAnimationController anim, ArcLungeEventDefinition def)
        {
            if (_state.ApexHoldDurationSeconds > 0f && !string.IsNullOrWhiteSpace(def.apexAnimationName))
            {
                PlayAnimation(anim, def.apexAnimationName, loop: true);
                _state.Phase = ArcLungeAnimationPhaseState.Apex;
                return;
            }

            if (!string.IsNullOrWhiteSpace(def.fallAnimationName))
            {
                PlayAnimation(anim, def.fallAnimationName, loop: true);
                _state.Phase = ArcLungeAnimationPhaseState.Fall;
                return;
            }

            _state.Phase = ArcLungeAnimationPhaseState.Fall;
        }

        /// <summary>
        /// 착지 종료 애니메이션이 있으면 재생하고, 없으면 애니메이션 상태를 종료합니다.
        /// </summary>
        private void PlayLandEndOrClear()
        {
            if (!_state.IsActive)
                return;

            var anim = _state.AnimationController;
            var def = _state.Definition;
            if (anim == null || def == null)
            {
                Clear();
                return;
            }

            if (!string.IsNullOrWhiteSpace(def.landEndAnimationName))
            {
                PlayAnimation(anim, def.landEndAnimationName, loop: false);
                _state.Phase = ArcLungeAnimationPhaseState.LandEnd;
                _state.LandEndRemainingSeconds = GetAnimationDurationSafe(anim, def.landEndAnimationName);
                return;
            }

            Clear();
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
    }
}
