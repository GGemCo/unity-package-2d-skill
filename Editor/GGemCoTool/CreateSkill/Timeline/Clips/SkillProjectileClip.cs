using System;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 프로젝타일(투사체) 발사 이벤트를 정의하는 스킬 Timeline 클립입니다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이 클립은 스킬 제작 단계의 Authoring 데이터이며,
    /// Bake 시 <see cref="GGemCo2DSkill.ProjectileEventDefinition"/> Payload로 변환됩니다.
    /// </para>
    /// <para>
    /// 프로젝타일 기본 정보, 전투 수치, 시각적 오버라이드, 타게팅 오버라이드를 함께 정의하여
    /// 런타임 투사체 동작을 구성합니다.
    /// </para>
    /// </remarks>
    [Serializable]
    public sealed class SkillProjectileClip : SkillEventClipBase
    {
        [Header("Projectile (Core Table)")]
        [Tooltip("Core projectile 테이블에 정의된 프로젝타일 UID입니다.")]
        [SerializeField] private int projectileUid = 0;

        [Header("Combat")]
        [Tooltip("프로젝타일 적중 시 적용될 피해 유형입니다.")]
        [SerializeField] private ConfigCommon.DamageType damageType = ConfigCommon.DamageType.None;

        [Tooltip("프로젝타일 적중 시 적용될 기본 피해량입니다.")]
        [SerializeField] private long damage = 0;

        [Header("Dynamic Multipliers")]
        [Tooltip("프로젝타일 기본 속도에 곱해지는 배율입니다.")]
        [SerializeField] private float speedMultiplier = 1f;

        [Tooltip("프로젝타일 기본 스케일에 곱해지는 배율입니다.")]
        [SerializeField] private float scaleMultiplier = 1f;

        [Header("Visual Overrides (optional)")]
        [Tooltip("프로젝타일의 비주얼 표현 방식입니다.")]
        [SerializeField] private ProjectileConstants.ProjectileVisualType visualType = ProjectileConstants.ProjectileVisualType.Default;

        [Tooltip("프로젝타일 표시용 스프라이트 오버라이드입니다.")]
        [SerializeField] private Sprite visualSprite;

        [Tooltip("프로젝타일 표시용 Animator Controller 오버라이드입니다.")]
        [SerializeField] private RuntimeAnimatorController visualAnimatorController;

        [Tooltip("기본 이펙트 대신 사용할 Effect UID 오버라이드입니다.")]
        [SerializeField] private int visualVfxUidOverride = 0;

        [Header("Targeting Overrides")]
        [Tooltip("프로젝타일의 타게팅 규칙을 보정하기 위한 오버라이드 설정입니다.")]
        [SerializeField] private GGemCo2DSkill.TargetingOverride targetingOverride;

        [Header("Linear Then Segments Behavior Override")]
        [Tooltip("LinearThenSegments 타입에서 충돌 후 제거 정책과 데미지 방식을 이벤트 단위로 덮어쓸지 여부입니다.")]
        [SerializeField] private bool useLinearThenSegmentsBehaviorOverride = false;

        [Tooltip("LinearThenSegments 타입에서 타겟/지형 충돌 시 발사체를 언제 제거할지 결정합니다.")]
        [SerializeField] private ProjectileConstants.HitLifetimeMode hitLifetimeMode = ProjectileConstants.HitLifetimeMode.DestroyOnTargetHit;

        [Tooltip("LinearThenSegments 타입에서 데미지 적용 방식을 이벤트 단위로 지정합니다.")]
        [SerializeField] private ProjectileConstants.DamageApplyMode damageApplyMode = ProjectileConstants.DamageApplyMode.OnHitDestroy;

        [Tooltip("PeriodicOverlap일 때 몇 초 간격으로 데미지를 적용할지 설정합니다.")]
        [SerializeField] private float tickDamageIntervalSeconds = 0.25f;

        [Header("Chain Cancel")]
        [Tooltip("이 Projectile 이벤트가 실제 데미지를 확정했을 때 다음 스킬 연계를 즉시 허용할지 여부입니다. GGemCoSkillSettings.enableSkillChainOnConfirmedDamage 가 함께 켜져 있어야 동작합니다.")]
        [SerializeField] private bool allowSkillChainOnConfirmedDamage = false;

        /// <summary>
        /// 이 클립이 표현하는 스킬 이벤트 유형입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.Projectile;

        /// <summary>
        /// 사용할 프로젝타일 정의의 UID입니다.
        /// </summary>
        public int ProjectileUid => projectileUid;

        /// <summary>
        /// 프로젝타일이 적용할 피해 유형입니다.
        /// </summary>
        public ConfigCommon.DamageType DamageType => damageType;

        /// <summary>
        /// 프로젝타일이 적용할 기본 피해량입니다.
        /// </summary>
        public long Damage => damage;

        /// <summary>
        /// 프로젝타일 속도에 적용할 배율 값입니다.
        /// </summary>
        public float SpeedMultiplier => speedMultiplier;

        /// <summary>
        /// 프로젝타일 크기에 적용할 배율 값입니다.
        /// </summary>
        public float ScaleMultiplier => scaleMultiplier;

        /// <summary>
        /// 프로젝타일 비주얼 표현 방식입니다.
        /// </summary>
        public ProjectileConstants.ProjectileVisualType VisualType => visualType;

        /// <summary>
        /// 프로젝타일에 사용할 스프라이트 오버라이드입니다.
        /// </summary>
        public Sprite VisualSprite => visualSprite;

        /// <summary>
        /// 프로젝타일에 사용할 애니메이터 컨트롤러 오버라이드입니다.
        /// </summary>
        public RuntimeAnimatorController VisualAnimatorController => visualAnimatorController;

        /// <summary>
        /// 프로젝타일 비주얼 이펙트를 대체할 UID입니다.
        /// </summary>
        public int VisualVfxUidOverride => visualVfxUidOverride;

        /// <summary>
        /// 프로젝타일의 타게팅 규칙을 보정하는 오버라이드 설정입니다.
        /// </summary>
        public GGemCo2DSkill.TargetingOverride TargetingOverride => targetingOverride;

        /// <summary>
        /// LinearThenSegments 타입에서 충돌 후 제거 정책과 데미지 방식을 덮어쓸지 여부입니다.
        /// </summary>
        public bool UseLinearThenSegmentsBehaviorOverride => useLinearThenSegmentsBehaviorOverride;

        /// <summary>
        /// LinearThenSegments 타입에서 타겟/지형 충돌 시 발사체를 언제 제거할지 결정합니다.
        /// </summary>
        public ProjectileConstants.HitLifetimeMode HitLifetimeMode => hitLifetimeMode;

        /// <summary>
        /// LinearThenSegments 타입에서 사용할 데미지 적용 방식입니다.
        /// </summary>
        public ProjectileConstants.DamageApplyMode DamageApplyMode => damageApplyMode;

        /// <summary>
        /// PeriodicOverlap일 때 사용할 틱 데미지 간격(초)입니다.
        /// </summary>
        public float TickDamageIntervalSeconds => tickDamageIntervalSeconds;

        /// <summary>
        /// 이 Projectile 이벤트가 실제 데미지를 확정했을 때 다음 스킬 연계를 즉시 허용할지 여부입니다.
        /// </summary>
        public bool AllowSkillChainOnConfirmedDamage => allowSkillChainOnConfirmedDamage;
    }
}