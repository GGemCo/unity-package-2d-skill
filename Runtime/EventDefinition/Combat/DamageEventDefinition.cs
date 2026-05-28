using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 데미지 적용 시 캐스터/타겟의 바라보기 조건을 어떻게 처리할지 정의합니다.
    /// </summary>
    public enum DamageFacingPolicy
    {
        /// <summary>
        /// 기존 바라보기 판정 규칙을 따릅니다.
        /// </summary>
        RespectFacing = 0,

        /// <summary>
        /// 바라보기 상태를 무시하고 데미지를 적용합니다.
        /// </summary>
        IgnoreFacing = 1,
    }

    public sealed class DamageEventDefinition : ScriptableObject
    {
        [Header("Damage")]
        public string damageModelId = "Default";
        public float multiplier = 1f;

        [Header("Area")]
        public SkillAreaSpec area = SkillAreaSpec.Default;

        [Header("Position Reference")]
        [Tooltip("데미지 영역 중심을 기존 타겟팅, 스킬 시작 스냅샷, 또는 이름 있는 위치 앵커 중 어디에서 가져올지 지정합니다.")]
        public SkillPositionReference damageCenterReference;

        [Header("Hit Policy")]
        public string hitGroupId = "HitGroup_0";
        public bool allowMultiHit = false;
        public float multiHitIntervalSeconds = 0.1f;

        [Header("Target State Filter")]
        [Tooltip("체공 중인 대상은 제외하고, 지면에 붙어있는 대상에게만 데미지를 적용합니다.")]
        public bool isGroundOnly = false;
        [Tooltip("지면에 붙어있는 대상은 제외하고, 공중에 떠 있는 대상에게만 데미지를 적용합니다.")]
        public bool isAirOnly = false;

        [Header("Facing Policy")]
        [Tooltip("데미지 적용 시 캐스터/타겟 바라보기 판정을 따를지 무시할지 결정합니다.")]
        public DamageFacingPolicy facingDamagePolicy = DamageFacingPolicy.RespectFacing;

        [Header("Guard Break")]
        [Tooltip("이 공격이 대상의 가드와 상호작용하는 방식입니다.")]
        public GuardInteractionMode guardInteractionMode = GuardInteractionMode.Normal;

        [Tooltip("가드 브레이크 공격이 저스트 가드 타이밍에 들어왔을 때의 처리 정책입니다.")]
        public GuardBreakJustGuardPolicy guardBreakJustGuardPolicy = GuardBreakJustGuardPolicy.JustGuardCanBlock;

        [Tooltip("가드 브레이크 시 실제 HP에 적용할 데미지 배율입니다. 0=HP 피해 없음, 1=원래 데미지 모두 적용")]
        [Range(0f, 1f)]
        public float guardBreakDamageMultiplier = 0f;

        [Tooltip("가드 브레이크 시 추가로 차감할 스태미나입니다. 0이면 방어 설정 기본값을 사용합니다.")]
        public long guardBreakStaminaCost = 0;

        [Tooltip("가드 브레이크 시 우선 재생할 VFX UID입니다. 0이면 방어 설정 기본 VFX를 사용합니다.")]
        public int guardBreakVfxUid = 0;

        [Tooltip("가드 브레이크 시 표시할 피드백 텍스트입니다. 비어 있으면 방어 설정 기본 텍스트를 사용합니다.")]
        public string guardBreakFeedbackText = string.Empty;

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
