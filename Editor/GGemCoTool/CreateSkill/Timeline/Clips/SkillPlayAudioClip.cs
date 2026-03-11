using System;
using Config;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 실행 중 오디오를 재생하는 Timeline 이벤트 클립입니다.
    /// </summary>
    /// <remarks>
    /// Timeline 제작 단계에서 오디오 재생 타이밍을 정의하며,
    /// Bake 시 런타임 스킬 이벤트(<c>SkillRuntimeEvent</c>)의 PlayAudio 타입으로 변환됩니다.
    /// </remarks>
    [Serializable]
    public sealed class SkillPlayAudioClip : SkillEventClipBase
    {
        [Header("Audio")]

        [Tooltip("재생할 AudioClip 리소스입니다.")]
        [SerializeField] private AudioClip clip;

        [Tooltip("오디오 재생 볼륨입니다. 0은 음소거, 1은 원본 볼륨입니다.")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        /// <summary>
        /// 이 클립이 생성하는 스킬 이벤트 유형입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.PlayAudio;

        /// <summary>
        /// 재생할 오디오 클립을 반환합니다.
        /// </summary>
        public AudioClip Clip => clip;

        /// <summary>
        /// 오디오 재생 볼륨 값을 반환합니다.
        /// </summary>
        public float Volume => volume;
    }
}