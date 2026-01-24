using UnityEngine;

namespace GGemCo2DSkill
{
    public sealed class DamageEventDefinition : ScriptableObject
    {
        [Header("Damage")]
        public string damageModelId = "Default";
        public float multiplier = 1f;

        [Header("Area")]
        public SkillAreaSpec area = SkillAreaSpec.Default;

        [Header("Hit Policy")]
        public string hitGroupId = "HitGroup_0";
        public bool allowMultiHit = false;
        public float multiHitIntervalSeconds = 0.1f;

        [Header("Overrides")]
        public TargetingOverride targetingOverride;
        public AreaOverride areaOverride;
    }
}