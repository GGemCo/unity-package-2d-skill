using System;
using Config;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Bake된 스킬 런타임 이벤트 1개 단위.
    /// </summary>
    [Serializable]
    public struct SkillRuntimeEvent
    {
        public ConfigCommonSkill.SkillEventType Type;

        /// <summary>이벤트 시작 시간(초).</summary>
        public float StartTime;

        /// <summary>이벤트 종료 시간(초). Instant 이벤트는 StartTime==EndTime으로 저장 권장.</summary>
        public float EndTime;

        /// <summary>동일 시각에서의 실행 순서(오름차순).</summary>
        public int Order;

        /// <summary>타입별 Payload 배열 내 인덱스.</summary>
        public int PayloadIndex;
    }
}
