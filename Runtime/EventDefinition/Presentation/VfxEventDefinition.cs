using UnityEngine;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트로 생성되는 이펙트의 지속시간 정책입니다.
    /// </summary>
    public enum VfxLifetimeMode
    {
        /// <summary>
        /// 이펙트 프리팹/애니메이션의 기본 재생 정책을 그대로 사용합니다.
        /// 런타임에서 별도의 duration override를 적용하지 않습니다.
        /// </summary>
        UseVfxDefault = 0,

        /// <summary>
        /// Start/Play/End를 1회 재생하는 기본 one-shot 방식입니다.
        /// </summary>
        OneShot = 1,

        /// <summary>
        /// 지정한 시간 동안 유지되도록 재생합니다.
        /// </summary>
        FixedDuration = 2,

        /// <summary>
        /// 종료 요청 전까지 무한 재생합니다.
        /// </summary>
        Infinite = 3
    }

    /// <summary>
    /// 스킬 이벤트로 생성되는 이펙트의 월드 위치 기준점을 정의합니다.
    /// </summary>
    public enum VfxSpawnAnchor
    {
        /// <summary>시전자 위치를 기준으로 이펙트를 생성합니다.</summary>
        Caster = 0,

        /// <summary>잠금 타겟 위치를 기준으로 이펙트를 생성합니다.</summary>
        Target = 1,

        /// <summary>지면 좌표 또는 스냅샷 지면 기준점을 기준으로 이펙트를 생성합니다.</summary>
        Ground = 2
    }

    /// <summary>
    /// 생성된 VFX를 어떤 Transform 하위에 둘지 정의합니다.
    /// </summary>
    public enum VfxTargetBindingPolicy
    {
        /// <summary>어떤 Transform에도 하위 결합하지 않습니다.</summary>
        None = 0,

        /// <summary>생성된 VFX를 Caster Transform 하위에 둡니다.</summary>
        AttachToCaster = 1,

        /// <summary>생성된 VFX를 Target Transform 하위에 둡니다.</summary>
        AttachToTarget = 2
    }

    /// <summary>
    /// Offset 적용 좌표계를 정의합니다.
    /// </summary>
    public enum VfxOffsetSpace
    {
        /// <summary>Offset을 월드 좌표계로 적용합니다.</summary>
        World = 0,

        /// <summary>
        /// Offset을 부모 Transform 로컬 좌표계로 적용합니다.
        /// 부모가 없으면 월드 좌표계와 동일하게 처리합니다.
        /// </summary>
        ParentLocal = 1
    }

    /// <summary>
    /// 스킬 런타임에서 VFX 이벤트 하나를 실행하기 위한 설정입니다.
    /// </summary>
    public sealed class VfxEventDefinition : ScriptableObject
    {
        [Header("Vfx")]
        public int vfxUid;

        [Header("Lifetime")]
        public VfxLifetimeMode lifetimeMode = VfxLifetimeMode.FixedDuration;
        public float lifetimeSeconds = 2f;

        [Header("Spawn Rule")]
        [Tooltip("이 VFX를 생성할 월드 위치 기준점입니다.")]
        public VfxSpawnAnchor spawnAnchor = VfxSpawnAnchor.Caster;

        [Tooltip("생성된 VFX를 어떤 Transform 하위에 둘지 결정합니다.")]
        public VfxTargetBindingPolicy targetBindingPolicy = VfxTargetBindingPolicy.None;

        [Tooltip("Anchor/Binding 계산 이후 적용할 오프셋 값입니다.")]
        public Vector3 localOffset;

        [Tooltip("Offset을 월드 기준 또는 부모 로컬 기준으로 적용할지 지정합니다.")]
        public VfxOffsetSpace offsetSpace = VfxOffsetSpace.World;

        [Header("Position Anchor")]
        [Tooltip("켜면 이 VFX 이벤트가 계산한 최종 생성 위치를 같은 스킬 실행 안에 저장합니다.")]
        public SkillPositionAnchorWriteOptions positionAnchorWrite;

        [Header("Sorting")]
        [Tooltip("스킬 이벤트가 VFX의 Sorting Layer를 덮어쓸지 여부입니다.")]
        public bool overrideSortingLayer;

        [Tooltip("overrideSortingLayer가 켜져 있을 때 적용할 Sorting Layer입니다.")]
        public ConfigSortingLayer.Keys sortingLayerOverride = ConfigSortingLayer.Keys.CharacterTop;

        [Tooltip("스킬 이벤트가 VFX의 Sorting Order를 고정할지 여부입니다.")]
        public bool overrideSortingOrder;

        [Tooltip("overrideSortingOrder가 켜져 있을 때 적용할 Sorting Order입니다.")]
        public int sortingOrderOverride;

        [Header("Overrides")]
        public TargetingOverride targetingOverride; // 타겟/지점 중심을 이벤트별로 바꿀 수 있음
    }
}
