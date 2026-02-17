using UnityEngine;

namespace GGemCo2DSkill
{
    public sealed class ApplyStatusEventDefinition : ScriptableObject
    {
        [Header("Status")]
        public StatusEffectId statusId;
        public int stacks = 1;
        public float durationOverrideSeconds = -1f;
        [Range(0f, 1f)] public float chance01 = 1f;

        [Header("Overrides")]
        public TargetingOverride targetingOverride;
        public AreaOverride areaOverride; // “범위 상태 이상”도 가능하게
    }
}