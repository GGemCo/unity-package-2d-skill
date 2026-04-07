using GGemCo2DCore;
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

        [Header("Target State Filter")]
        [Tooltip("체공 중인 대상은 제외하고, 지면에 붙어있는 대상에게만 데미지를 적용합니다.")]
        public bool isGroundOnly = false;
        [Tooltip("지면에 붙어있는 대상은 제외하고, 공중에 떠 있는 대상에게만 데미지를 적용합니다.")]
        public bool isAirOnly = false;

        [Header("OnHit Affect")]
        public OnHitAffectEntry[] onHitAffects;

        [Header("OnHit Crowd Control")]
        public OnHitCrowdControlEntry[] onHitCrowdControls;

        [Header("OnHit Element Gauge")]
        public OnHitElementGaugeEntry[] onHitElementGauges;

        [Header("Hit Stop (Self)")]
        public bool useHitStopSelf = false;
        public bool useDefaultSelfHitStop = true;
        public float selfHitStopSeconds = 0.03f;
        
        [Header("Hit Stop (Target)")]
        public bool useHitStopTarget = false;
        public bool useDefaultTargetHitStop = true;
        public float targetHitStopSeconds = 0.05f;

        [Header("Camera Shake")]
        public bool useCameraShakeOnHit = false;
        public CameraShakePreset cameraShakePreset;
        public DirectionalCameraShakeMode cameraShakeDirectionMode = DirectionalCameraShakeMode.PresetRaw;

        [Header("Chain Cancel")]
        [Tooltip("이 Damage 이벤트가 실제 데미지를 확정했을 때 다음 스킬 연계를 즉시 허용할지 여부입니다. GGemCoSkillSettings.enableSkillChainOnConfirmedDamage 가 함께 켜져 있어야 동작합니다.")]
        public bool allowSkillChainOnConfirmedDamage = false;

        [Header("Overrides")]
        public TargetingOverride targetingOverride;
        public AreaOverride areaOverride;
    }
}