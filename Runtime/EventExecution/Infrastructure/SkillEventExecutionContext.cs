using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 런타임 스킬 이벤트 하나를 실행하는 데 필요한 읽기 전용 컨텍스트입니다.
    /// </summary>
    internal readonly struct SkillEventExecutionContext
    {
        /// <summary>
        /// 현재 이벤트를 발생시킨 스킬 런타임입니다.
        /// </summary>
        public SkillRun Run { get; }

        /// <summary>
        /// 현재 실행 중인 스킬 정의입니다.
        /// </summary>
        public RuntimeSkillDefinition Skill { get; }

        /// <summary>
        /// 캐스터, 타겟, 지면 좌표, 전방 방향을 포함한 대상 컨텍스트입니다.
        /// </summary>
        public SkillTargetContext TargetContext { get; }

        /// <summary>
        /// 이벤트와 페이로드를 보관하는 런타임 시퀀스입니다.
        /// </summary>
        public SkillRuntimeSequence Sequence { get; }

        /// <summary>
        /// 실행할 런타임 이벤트 정보입니다.
        /// </summary>
        public SkillRuntimeEvent Event { get; }

        /// <summary>
        /// 이벤트 타입별 실행기가 사용할 Bake된 페이로드입니다.
        /// </summary>
        public Object Payload { get; }

        /// <summary>
        /// 스킬 시작 또는 이벤트 스냅샷 시점의 캐스터 위치입니다.
        /// </summary>
        public Vector3 SnapshotCasterPosition { get; }

        /// <summary>
        /// 스킬 시작 또는 이벤트 스냅샷 시점의 타겟 위치입니다.
        /// </summary>
        public Vector3 SnapshotTargetPosition { get; }

        /// <summary>
        /// 스킬 시작 또는 이벤트 스냅샷 시점의 지면 기준점입니다.
        /// </summary>
        public Vector3 SnapshotGroundPoint { get; }

        /// <summary>
        /// 이벤트 Timeline Clip 구간에서 계산한 지속 시간입니다.
        /// </summary>
        public float EventDurationSeconds { get; }

        /// <summary>
        /// 스킬 이벤트 실행 컨텍스트를 생성합니다.
        /// </summary>
        /// <param name="run">현재 이벤트를 발생시킨 스킬 런타임입니다.</param>
        /// <param name="skill">현재 실행 중인 스킬 정의입니다.</param>
        /// <param name="targetContext">대상 컨텍스트입니다.</param>
        /// <param name="sequence">런타임 시퀀스입니다.</param>
        /// <param name="runtimeEvent">실행할 런타임 이벤트입니다.</param>
        /// <param name="snapshotCasterPosition">스냅샷 캐스터 위치입니다.</param>
        /// <param name="snapshotTargetPosition">스냅샷 타겟 위치입니다.</param>
        /// <param name="snapshotGroundPoint">스냅샷 지면 기준점입니다.</param>
        public SkillEventExecutionContext(
            SkillRun run,
            RuntimeSkillDefinition skill,
            SkillTargetContext targetContext,
            SkillRuntimeSequence sequence,
            in SkillRuntimeEvent runtimeEvent,
            Vector3 snapshotCasterPosition,
            Vector3 snapshotTargetPosition,
            Vector3 snapshotGroundPoint)
        {
            Run = run;
            Skill = skill;
            TargetContext = targetContext;
            Sequence = sequence;
            Event = runtimeEvent;
            Payload = sequence != null ? sequence.GetPayload(runtimeEvent.PayloadIndex) : null;
            SnapshotCasterPosition = snapshotCasterPosition;
            SnapshotTargetPosition = snapshotTargetPosition;
            SnapshotGroundPoint = snapshotGroundPoint;
            EventDurationSeconds = Mathf.Max(0f, runtimeEvent.EndTime - runtimeEvent.StartTime);
        }
    }
}
