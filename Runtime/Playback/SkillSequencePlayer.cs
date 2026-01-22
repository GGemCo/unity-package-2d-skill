using System;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Bake된 SkillRuntimeSequence를 시간축으로 실행하는 런타임 플레이어.
    /// Timeline에 의존하지 않고, 정렬된 이벤트 배열을 순회한다.
    /// </summary>
    public sealed class SkillSequencePlayer
    {
        private readonly SkillRuntimeSequence _sequence;
        private int _cursor;

        public SkillSequencePlayer(SkillRuntimeSequence sequence)
        {
            _sequence = sequence != null ? sequence : throw new ArgumentNullException(nameof(sequence));
            _cursor = 0;
        }

        public void Reset() => _cursor = 0;

        public void Tick(float time, ISkillEventReceiver receiver)
        {
            if (receiver == null) throw new ArgumentNullException(nameof(receiver));
            if (_sequence.Events == null) return;

            // Events는 Bake 시점에 (StartTime, Order)로 정렬되어 있다고 가정(퍼포먼스).
            while (_cursor < _sequence.Events.Length)
            {
                ref readonly var e = ref _sequence.Events[_cursor];
                if (e.StartTime > time) break;

                receiver.OnSkillEvent(_sequence, e);
                _cursor++;
            }
        }
    }

    /// <summary>
    /// 런타임에서 이벤트를 실제 동작으로 해석/실행하는 수신자 인터페이스.
    /// </summary>
    public interface ISkillEventReceiver
    {
        void OnSkillEvent(SkillRuntimeSequence sequence, SkillRuntimeEvent e);
    }
}
