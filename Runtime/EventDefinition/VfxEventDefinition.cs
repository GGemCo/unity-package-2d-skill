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

    public sealed class VfxEventDefinition : ScriptableObject
    {
        [Header("Vfx")]
        public int vfxUid;

        [Header("Lifetime")]
        public VfxLifetimeMode lifetimeMode = VfxLifetimeMode.FixedDuration;
        public float lifetimeSeconds = 2f;

        [Header("Spawn Rule")]
        public bool attachToTarget;
        public Vector3 localOffset;

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
