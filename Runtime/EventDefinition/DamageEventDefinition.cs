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

        [Header("OnHit Affect")]
        public OnHitAffectEntry[] onHitAffects;

        [Header("OnHit Crowd Control")]
        public OnHitCrowdControlEntry[] onHitCrowdControls;

        [Header("Chain Cancel")]
        [Tooltip("이 Damage 이벤트가 실제 데미지를 확정했을 때 다음 스킬 연계를 즉시 허용할지 여부입니다. GGemCoSkillSettings.enableSkillChainOnConfirmedDamage 가 함께 켜져 있어야 동작합니다.")]
        public bool allowSkillChainOnConfirmedDamage = false;

        [Header("Overrides")]
        public TargetingOverride targetingOverride;
        public AreaOverride areaOverride;
    }
}