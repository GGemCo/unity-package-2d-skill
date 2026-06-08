using System;
using Config;
using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 실행 중 캐스터 캐릭터의 표시 알파를 페이드 인 또는 페이드 아웃하는 Timeline 이벤트 클립입니다.
    /// </summary>
    /// <remarks>
    /// 이 클립은 Bake 과정에서 <c>SkillCasterFadeEventDefinition</c> Payload로 변환되며,
    /// 런타임에서는 캐스터의 애니메이션 컨트롤러를 통해 페이드 연출을 실행합니다.
    /// </remarks>
    [Serializable]
    public sealed class SkillCasterFadeClip : SkillEventClipBase
    {
        [Header("Mode")]
        [Tooltip("캐스터 페이드 실행 방식입니다.")]
        [SerializeField] private SkillCasterFadeMode mode = SkillCasterFadeMode.FadeOut;

        [Header("Timing")]
        [Tooltip("켜면 Timeline Clip 길이를 페이드 지속 시간으로 사용합니다.")]
        [SerializeField] private bool useClipDuration = true;

        [Tooltip("useClipDuration이 꺼져 있을 때 사용할 페이드 지속 시간(초)입니다.")]
        [Min(0f)]
        [SerializeField] private float durationOverrideSeconds = 0.15f;

        [Header("End Policy")]
        [Tooltip("스킬이 정상 종료될 때 캐스터 알파를 1로 복구할지 여부입니다.")]
        [SerializeField] private bool restoreOnSkillEnd = false;

        [Tooltip("스킬이 취소될 때 캐스터 알파를 1로 복구할지 여부입니다.")]
        [SerializeField] private bool restoreOnCancel = true;

        /// <summary>
        /// 이 클립이 생성하는 스킬 이벤트 유형입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.CasterFade;

        /// <summary>
        /// 캐스터 페이드 실행 방식을 반환합니다.
        /// </summary>
        public SkillCasterFadeMode Mode => mode;

        /// <summary>
        /// Timeline Clip 길이를 지속 시간으로 사용할지 여부를 반환합니다.
        /// </summary>
        public bool UseClipDuration => useClipDuration;

        /// <summary>
        /// 지속 시간 오버라이드 값을 반환합니다.
        /// </summary>
        public float DurationOverrideSeconds => durationOverrideSeconds;

        /// <summary>
        /// 스킬 정상 종료 시 알파 복구 여부를 반환합니다.
        /// </summary>
        public bool RestoreOnSkillEnd => restoreOnSkillEnd;

        /// <summary>
        /// 스킬 취소 시 알파 복구 여부를 반환합니다.
        /// </summary>
        public bool RestoreOnCancel => restoreOnCancel;
    }
}
