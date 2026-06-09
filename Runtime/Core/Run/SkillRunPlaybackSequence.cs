using Config;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 대표 스킬 애니메이션과 동시에 재생되는 추가 스킬 RuntimeSequence 상태입니다.
    /// </summary>
    internal sealed class SkillRunPlaybackSequence
    {
        private int _nextEventIndex;
        private float _time;
        private float _realUseElapsed;
        private SkillRunTimingContext _timingContext = SkillRunTimingContext.Default;

        /// <summary>
        /// 추가 실행할 스킬 정의입니다.
        /// </summary>
        public RuntimeSkillDefinition Skill { get; }

        /// <summary>
        /// 추가 스킬에 적용할 실행 컨텍스트입니다.
        /// </summary>
        public SkillTargetContext Context { get; }

        /// <summary>
        /// 추가 스킬의 런타임 이벤트 시퀀스입니다.
        /// </summary>
        public SkillRuntimeSequence Sequence { get; }

        /// <summary>
        /// 현재 추가 시퀀스가 종료되었는지 여부입니다.
        /// </summary>
        public bool IsDone { get; private set; }

        /// <summary>
        /// 추가 스킬 시퀀스 상태를 생성합니다.
        /// </summary>
        /// <param name="skill">실행할 스킬 정의입니다.</param>
        /// <param name="context">해당 스킬용 실행 컨텍스트입니다.</param>
        /// <param name="sequence">실행할 런타임 시퀀스입니다.</param>
        public SkillRunPlaybackSequence(
            RuntimeSkillDefinition skill,
            in SkillTargetContext context,
            SkillRuntimeSequence sequence)
        {
            Skill = skill;
            Context = context;
            Sequence = sequence;
        }

        /// <summary>
        /// Use 시작 시점에 맞춰 시퀀스 재생 상태를 초기화합니다.
        /// </summary>
        public void Reset()
        {
            _nextEventIndex = 0;
            _time = 0f;
            _realUseElapsed = 0f;
            IsDone = false;
        }

        /// <summary>
        /// UseClip 기준 시간 보정 정보를 설정합니다.
        /// </summary>
        /// <param name="timingContext">해당 스킬 시퀀스에 적용할 시간 보정 정보입니다.</param>
        public void SetTimingContext(SkillRunTimingContext timingContext)
        {
            _timingContext = timingContext;
        }

        /// <summary>
        /// 추가 스킬 시퀀스를 한 프레임 진행하고 시작 시간이 지난 이벤트를 실행합니다.
        /// </summary>
        /// <param name="owner">이벤트 실행을 담당하는 스킬 실행기입니다.</param>
        /// <param name="run">대표 스킬 런타임입니다.</param>
        /// <param name="snapshotCasterPos">대표 스킬 시작 시점의 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPos">대표 스킬 시작 시점의 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">대표 스킬 시작 시점의 지면 좌표입니다.</param>
        /// <param name="dt">이번 프레임 경과 시간입니다.</param>
        public void Tick(
            SkillExecutor owner,
            SkillRun run,
            Vector3 snapshotCasterPos,
            Vector3 snapshotTargetPos,
            Vector3 snapshotGroundPoint,
            float dt)
        {
            if (IsDone)
                return;

            _realUseElapsed += Mathf.Max(0f, dt);

            if (Sequence != null && Sequence.Events != null)
            {
                while (_nextEventIndex < Sequence.Events.Length)
                {
                    SkillRuntimeEvent ev = Sequence.Events[_nextEventIndex];
                    if (_time + 1e-6f < ev.StartTime)
                        break;

                    owner.ExecuteEvent(
                        run,
                        Skill,
                        Context,
                        Sequence,
                        ev,
                        snapshotCasterPos,
                        snapshotTargetPos,
                        snapshotGroundPoint,
                        _timingContext);
                    _nextEventIndex++;
                }

                _time += dt * _timingContext.TimelineRate;
            }
            else
            {
                _time += dt;
            }

            if (ShouldEnd())
            {
                IsDone = true;
            }
        }

        /// <summary>
        /// 현재 시간 보정 정책에 따라 추가 시퀀스 종료 여부를 확인합니다.
        /// </summary>
        /// <returns>시퀀스가 종료되었으면 <see langword="true"/>입니다.</returns>
        private bool ShouldEnd()
        {
            bool sequenceEnded = Sequence == null ||
                                 (_time >= Sequence.Duration &&
                                  (Sequence.Events == null || _nextEventIndex >= Sequence.Events.Length));
            bool useClipEnded = _timingContext.RealUseDurationSeconds > 0f &&
                                _realUseElapsed >= _timingContext.RealUseDurationSeconds;
            bool fallbackEnded = Sequence == null &&
                                 _timingContext.RealUseDurationSeconds <= 0f &&
                                 _realUseElapsed >= 0.3f;

            return _timingContext.Policy switch
            {
                ConfigCommonSkill.SkillUseClipTimingPolicy.EndByUseClip => useClipEnded ||
                                                                           (_timingContext.RealUseDurationSeconds <= 0f && sequenceEnded) ||
                                                                           fallbackEnded,
                ConfigCommonSkill.SkillUseClipTimingPolicy.EndByLonger => sequenceEnded &&
                                                                          (_timingContext.RealUseDurationSeconds <= 0f || useClipEnded || fallbackEnded),
                _ => sequenceEnded || fallbackEnded,
            };
        }
    }
}
