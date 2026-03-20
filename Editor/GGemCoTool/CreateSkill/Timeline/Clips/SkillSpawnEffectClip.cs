using System;
using Config;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 실행 중 이펙트(Effect)를 생성하는 Timeline 이벤트 클립입니다.
    /// </summary>
    /// <remarks>
    /// 이 클립은 Bake 과정에서 런타임 이벤트(<c>SkillRuntimeEvent</c>)로 변환되며,
    /// 지정된 Anchor 위치를 기준으로 이펙트를 생성합니다.
    /// </remarks>
    [Serializable]
    public sealed class SkillSpawnEffectClip : SkillEventClipBase
    {
        /// <summary>
        /// 이펙트를 생성할 기준 위치 유형입니다.
        /// </summary>
        public enum AnchorType
        {
            /// <summary>
            /// 스킬 시전자 위치를 기준으로 생성합니다.
            /// </summary>
            Caster = 0,

            /// <summary>
            /// 스킬 대상(Target) 위치를 기준으로 생성합니다.
            /// </summary>
            Target = 1,

            /// <summary>
            /// 월드 좌표 또는 지정된 지면 위치에 생성합니다.
            /// </summary>
            Ground = 2
        }

        [Header("Effect")]

        [Tooltip("생성할 이펙트 리소스의 UID입니다. effect 테이블 또는 Addressables Effect 식별자와 매칭됩니다.")]
        [SerializeField] private int effectUid;

        [Tooltip("이펙트를 생성할 기준 위치입니다. (Caster / Target / Ground)")]
        [SerializeField] private AnchorType anchor = AnchorType.Caster;

        [Tooltip("기준 위치로부터 적용할 로컬 오프셋입니다.")]
        [SerializeField] private Vector2 offset;

        [Header("Lifetime")]

        [Tooltip("이펙트 지속시간 해석 정책입니다.")]
        [SerializeField] private EffectLifetimeMode lifetimeMode = EffectLifetimeMode.FixedDuration;

        [Tooltip("FixedDuration일 때 사용할 유지 시간(초)입니다.")]
        [Min(0f)]
        [SerializeField] private float lifetimeSeconds = 2f;

        /// <summary>
        /// 이 클립이 생성하는 스킬 이벤트 유형입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.SpawnEffect;

        /// <summary>
        /// 생성할 이펙트의 UID를 반환합니다.
        /// </summary>
        public int EffectUid => effectUid;

        /// <summary>
        /// 이펙트 생성 기준 위치 타입을 정수 값으로 반환합니다.
        /// </summary>
        /// <remarks>
        /// Bake 또는 런타임 시스템에서 enum 대신 정수 기반 이벤트 데이터를 사용할 때 활용됩니다.
        /// </remarks>
        public int Anchor => (int)anchor;

        /// <summary>
        /// 기준 위치에 적용할 이펙트 오프셋을 반환합니다.
        /// </summary>
        public Vector2 Offset => offset;

        /// <summary>
        /// 이펙트 지속시간 정책을 반환합니다.
        /// </summary>
        public EffectLifetimeMode LifetimeMode => lifetimeMode;

        /// <summary>
        /// 이펙트 유지 시간을 반환합니다.
        /// </summary>
        public float LifetimeSeconds => lifetimeSeconds;
    }
}