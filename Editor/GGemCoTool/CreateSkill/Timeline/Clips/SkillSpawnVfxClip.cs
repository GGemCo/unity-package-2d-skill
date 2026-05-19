using System;
using Config;
using GGemCo2DCore;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 실행 중 VFX를 생성하는 Timeline 이벤트 클립입니다.
    /// </summary>
    /// <remarks>
    /// 이 클립은 Bake 단계에서 런타임 이벤트 정의(<c>VfxEventDefinition</c>)로 변환됩니다.
    /// </remarks>
    [Serializable]
    public sealed class SkillSpawnVfxClip : SkillEventClipBase
    {
        /// <summary>
        /// VFX를 생성할 기준 위치를 정의합니다.
        /// </summary>
        public enum AnchorType
        {
            /// <summary>시전자 위치를 기준으로 생성합니다.</summary>
            Caster = 0,

            /// <summary>타겟 위치를 기준으로 생성합니다.</summary>
            Target = 1,

            /// <summary>지면 기준점 위치를 기준으로 생성합니다.</summary>
            Ground = 2
        }

        /// <summary>
        /// 생성된 VFX를 어떤 Transform 하위에 둘지 결정합니다.
        /// </summary>
        public enum TargetBindingPolicy
        {
            /// <summary>어떤 Transform에도 하위 결합하지 않습니다.</summary>
            None = 0,

            /// <summary>생성된 VFX를 Caster Transform 하위에 둡니다.</summary>
            AttachToCaster = 1,

            /// <summary>생성된 VFX를 Target Transform 하위에 둡니다.</summary>
            AttachToTarget = 2
        }

        /// <summary>
        /// Offset을 어떤 좌표계로 적용할지 정의합니다.
        /// </summary>
        public enum OffsetSpacePolicy
        {
            /// <summary>Offset을 월드 좌표계로 적용합니다.</summary>
            World = 0,

            /// <summary>
            /// Offset을 부모 Transform 로컬 좌표계로 적용합니다.
            /// 부모가 없으면 월드 좌표계와 동일하게 처리됩니다.
            /// </summary>
            ParentLocal = 1
        }

        [Header("Vfx")]
        [Tooltip("생성할 VFX UID입니다. vfx 테이블 또는 Addressables 항목과 연결됩니다.")]
        [SerializeField] private int vfxUid;

        [Tooltip("VFX 생성의 기준 위치입니다. (Caster / Target / Ground)")]
        [SerializeField] private AnchorType anchor = AnchorType.Caster;

        [Tooltip("Anchor/Binding 계산 이후 적용할 오프셋 값입니다.")]
        [SerializeField] private Vector2 offset;

        [Header("Spawn Policy")]
        [Tooltip("생성된 VFX를 어떤 Transform 하위에 둘지 지정합니다.")]
        [SerializeField] private TargetBindingPolicy targetBindingPolicy = TargetBindingPolicy.None;

        [Tooltip("Offset을 월드 기준 또는 부모 로컬 기준으로 적용할지 지정합니다.")]
        [SerializeField] private OffsetSpacePolicy offsetSpacePolicy = OffsetSpacePolicy.World;

        [Tooltip("Caster가 좌우 반전된 상태일 때 Offset의 X 값을 반전할지 여부입니다.")]
        [SerializeField] private bool useCasterFlipOffsetX = false;

        [Header("Position Anchor")]
        [Tooltip("켜면 이 VFX 이벤트가 계산한 최종 생성 위치를 같은 스킬 실행 내에 저장합니다.")]
        [SerializeField] private SkillPositionAnchorWriteOptions positionAnchorWrite;

        [Header("Sorting")]
        [Tooltip("켜면 VFX Sorting Layer를 이벤트 값으로 덮어씁니다.")]
        [SerializeField] private bool overrideSortingLayer;

        [Tooltip("overrideSortingLayer가 켜졌을 때 적용할 Sorting Layer입니다.")]
        [SerializeField] private ConfigSortingLayer.Keys sortingLayerOverride = ConfigSortingLayer.Keys.CharacterTop;

        [Tooltip("켜면 VFX Sorting Order를 이벤트 값으로 고정합니다.")]
        [SerializeField] private bool overrideSortingOrder;

        [Tooltip("overrideSortingOrder가 켜졌을 때 적용할 Sorting Order입니다.")]
        [SerializeField] private int sortingOrderOverride;

        [Header("Lifetime")]
        [Tooltip("이펙트의 지속시간 정책입니다.")]
        [SerializeField] private VfxLifetimeMode lifetimeMode = VfxLifetimeMode.FixedDuration;

        [Tooltip("FixedDuration 모드에서 사용할 유지 시간(초)입니다.")]
        [Min(0f)]
        [SerializeField] private float lifetimeSeconds = 2f;

        /// <summary>클립이 생성하는 스킬 이벤트 타입입니다.</summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.SpawnVfx;

        /// <summary>생성할 VFX UID입니다.</summary>
        public int VFXUid => vfxUid;

        /// <summary>VFX 생성 기준 위치(enum)의 원시 값입니다.</summary>
        public int Anchor => (int)anchor;

        /// <summary>Anchor 기준점에 적용할 오프셋입니다.</summary>
        public Vector2 Offset => offset;

        /// <summary>부모 결합 정책(enum)의 원시 값입니다.</summary>
        public int TargetBindingPolicyRaw => (int)targetBindingPolicy;

        /// <summary>Offset 좌표계 정책(enum)의 원시 값입니다.</summary>
        public int OffsetSpacePolicyRaw => (int)offsetSpacePolicy;

        /// <summary>Caster 좌우 반전 상태에 따라 Offset X를 반전할지 여부입니다.</summary>
        public bool UseCasterFlipOffsetX => useCasterFlipOffsetX;

        /// <summary>최종 생성 위치 저장 옵션입니다.</summary>
        public SkillPositionAnchorWriteOptions PositionAnchorWrite => positionAnchorWrite;

        /// <summary>Sorting Layer 강제 덮어쓰기 여부입니다.</summary>
        public bool OverrideSortingLayer => overrideSortingLayer;

        /// <summary>덮어쓸 Sorting Layer 값입니다.</summary>
        public ConfigSortingLayer.Keys SortingLayerOverride => sortingLayerOverride;

        /// <summary>Sorting Order 강제 고정 여부입니다.</summary>
        public bool OverrideSortingOrder => overrideSortingOrder;

        /// <summary>고정할 Sorting Order 값입니다.</summary>
        public int SortingOrderOverride => sortingOrderOverride;

        /// <summary>이펙트 지속시간 정책입니다.</summary>
        public VfxLifetimeMode LifetimeMode => lifetimeMode;

        /// <summary>이펙트 유지 시간(초)입니다.</summary>
        public float LifetimeSeconds => lifetimeSeconds;
    }
}
