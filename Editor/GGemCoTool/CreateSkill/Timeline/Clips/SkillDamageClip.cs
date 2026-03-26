using System;
using Config;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬이 적중했을 때 적용되는 데미지 이벤트 클립입니다.
    /// 데미지 배율, 타입, 판정 영역 형태 및 크기, OnHit 부가 효과 정보를 정의합니다.
    /// </summary>
    [Serializable]
    public sealed class SkillDamageClip : SkillEventClipBase
    {
        /// <summary>
        /// 최종 계산된 데미지에 곱해지는 배율입니다.
        /// </summary>
        /// <remarks>
        /// 1은 기본값이며, 2는 2배, 0.5는 절반의 피해를 의미합니다.
        /// </remarks>
        [Tooltip("[피해 배율] 최종 데미지에 곱해지는 계수. 1=기본, 2=2배, 0.5=절반")]
        [SerializeField] private float multiplier = 1.0f;

        /// <summary>
        /// 데미지 타입(속성/분류)을 식별하는 UID입니다.
        /// </summary>
        /// <remarks>
        /// 저항, 약점, 면역 등 전투 계산 규칙에 사용됩니다.
        /// </remarks>
        [Tooltip("[피해 타입] 데미지 타입(속성/분류) UID. 저항/약점/면역 등 룰에 사용")]
        [SerializeField] private int damageTypeUid = 0;

        /// <summary>
        /// 히트 판정에 사용되는 영역의 형태입니다.
        /// </summary>
        [Tooltip("[영역 모양] 히트 판정에 사용할 영역 형태. Circle/Box/Cone/Capsule/Lin")]
        [SerializeField] private ConfigCommonSkill.SkillAreaShape areaShape = ConfigCommonSkill.SkillAreaShape.Circle;

        /// <summary>
        /// 캡슐 형태 사용 시, 세미원 끝 방향을 지정합니다.
        /// </summary>
        [Tooltip("[캡슐 방향] 캡슐의 세미원 끝 방향(Vertical/Horizontal).")]
        [SerializeField] private CapsuleDirection2D capsuleDirection = CapsuleDirection2D.Vertical;

        /// <summary>
        /// 영역의 반경 또는 두께를 정의합니다.
        /// </summary>
        /// <remarks>
        /// Circle = 반경,
        /// Capsule = 양 끝 반원의 반경(두께),
        /// Box/Cone/Line에서는 사용되지 않습니다.
        /// </remarks>
        [Tooltip("[반경/두께] Circle=반경. Capsule=양 끝 반원 반경(두께). (Box/Cone/Line에서는 미사용)")]
        [SerializeField] private float radius = 2f;

        /// <summary>
        /// 영역의 전방 길이 또는 사거리를 정의합니다.
        /// </summary>
        /// <remarks>
        /// Box/Line/Capsule = 전방 길이,
        /// Cone = 꼭지점에서 끝까지의 사거리,
        /// Circle에서는 사용되지 않습니다.
        /// </remarks>
        [Tooltip("[길이] Box/Line=전방 길이. Capsule=전방 길이. Cone=사거리(꼭지점→끝). (Circle에서는 미사용)")]
        [SerializeField] private float length = 3f;

        /// <summary>
        /// 영역의 가로 폭을 정의합니다.
        /// </summary>
        /// <remarks>
        /// Box/Line에서만 사용되며,
        /// Circle/Cone/Capsule에서는 사용되지 않습니다.
        /// </remarks>
        [Tooltip("[폭] Box/Line=가로 폭. (Circle/Cone/Capsule에서는 미사용)")]
        [SerializeField] private float width = 2f;

        /// <summary>
        /// 원뿔(Cone) 형태 사용 시 벌어짐 각도를 정의합니다.
        /// </summary>
        /// <remarks>
        /// 0~180도 범위에서 사용되며,
        /// Circle/Box/Line/Capsule에서는 사용되지 않습니다.
        /// </remarks>
        [Tooltip("[각도] Cone=원뿔(부채꼴) 벌어짐 각도(0~180). (Circle/Box/Line/Capsule에서는 미사용)")]
        [SerializeField] private float angle = 60f;

        /// <summary>
        /// 히트 영역의 중심을 로컬 좌표 기준으로 이동시키는 오프셋입니다.
        /// </summary>
        /// <remarks>
        /// 캐스터 또는 타겟 기준 중심점에 더해져 최종 판정 위치가 결정됩니다.
        /// </remarks>
        [Tooltip("[영역 오프셋] 영역 중심을 로컬 좌표로 이동. 캐스터/타겟 기준 중심점에 더해져 판정됨")]
        [SerializeField] private Vector2 offset = Vector2.zero;

        /// <summary>
        /// 적중 시 대상(Target)에게 적용되는 추가 효과 목록입니다.
        /// </summary>
        [Header("OnHit Affect (Target)")]
        [SerializeField] private OnHitAffectEntry[] onHitAffects;

        /// <summary>
        /// 적중 시 대상(Target)에게 적용되는 Crowd Control 목록입니다.
        /// 실제 적용은 데미지 파이프라인을 통해 처리됩니다.
        /// </summary>
        [Header("OnHit Crowd Control (Target)")]
        [SerializeField] private OnHitCrowdControlEntry[] onHitCrowdControls;

        /// <summary>
        /// 이 Damage 이벤트가 실제 데미지를 확정했을 때 다음 스킬 연계를 즉시 허용할지 여부입니다.
        /// 최종 런타임 동작은 <see cref="GGemCo2DSkill.GGemCoSkillSettings.enableSkillChainOnConfirmedDamage"/> 마스터 옵션이 함께 켜져 있어야 활성화됩니다.
        /// </summary>
        [Header("Target State Filter")]
        [Tooltip("[지상 전용] 대상이 지상에 있을 때만 데미지를 적용합니다. 공중 대상은 제외됩니다.")]
        [SerializeField] private bool isGroundOnly = false;

        [Tooltip("[공중 전용] 대상이 공중에 있을 때만 데미지를 적용합니다. 지상 대상은 제외됩니다.")]
        [SerializeField] private bool isAirOnly = false;

        [Header("Chain Cancel")]
        [Tooltip("[체인 캔슬] 이 Damage 이벤트가 실제 데미지를 확정했을 때 다음 스킬 연계를 즉시 허용할지 여부. GGemCoSkillSettings.enableSkillChainOnConfirmedDamage 가 함께 켜져 있어야 동작")]
        [SerializeField] private bool allowSkillChainOnConfirmedDamage = false;

        /// <summary>
        /// 이 클립의 이벤트 타입을 반환합니다.
        /// </summary>
        /// <returns>데미지 이벤트 타입입니다.</returns>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Damage;

        /// <summary>
        /// 최종 데미지에 적용되는 배율을 반환합니다.
        /// </summary>
        public float Multiplier => multiplier;

        /// <summary>
        /// 데미지 타입 UID를 반환합니다.
        /// </summary>
        public int DamageTypeUid => damageTypeUid;

        /// <summary>
        /// 히트 판정 영역 형태를 반환합니다.
        /// </summary>
        public ConfigCommonSkill.SkillAreaShape AreaShape => areaShape;

        /// <summary>
        /// 캡슐 영역 사용 시 방향을 반환합니다.
        /// </summary>
        public CapsuleDirection2D CapsuleDirection => capsuleDirection;

        /// <summary>
        /// 영역 반경 또는 두께 값을 반환합니다.
        /// </summary>
        public float Radius => radius;

        /// <summary>
        /// 영역 길이 또는 사거리 값을 반환합니다.
        /// </summary>
        public float Length => length;

        /// <summary>
        /// 영역 가로 폭 값을 반환합니다.
        /// </summary>
        public float Width => width;

        /// <summary>
        /// Cone 영역 각도를 반환합니다.
        /// </summary>
        public float Angle => angle;

        /// <summary>
        /// 영역 중심 오프셋 값을 반환합니다.
        /// </summary>
        public Vector2 Offset => offset;

        /// <summary>
        /// 적중 시 적용될 추가 효과 목록을 반환합니다.
        /// </summary>
        public OnHitAffectEntry[] OnHitAffects => onHitAffects;

        /// <summary>
        /// 적중 시 적용될 Crowd Control 목록을 반환합니다.
        /// </summary>
        public OnHitCrowdControlEntry[] OnHitCrowdControls => onHitCrowdControls;


        /// <summary>
        /// 대상이 지상에 있을 때만 데미지를 적용할지 여부를 반환합니다.
        /// </summary>
        public bool IsGroundOnly => isGroundOnly;

        /// <summary>
        /// 대상이 공중에 있을 때만 데미지를 적용할지 여부를 반환합니다.
        /// </summary>
        public bool IsAirOnly => isAirOnly;

        /// <summary>
        /// 실제 데미지 확정 시 다음 스킬 연계를 즉시 허용할지 여부를 반환합니다.
        /// </summary>
        public bool AllowSkillChainOnConfirmedDamage => allowSkillChainOnConfirmedDamage;
    }
}