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
        [Header("Sound")]

        [Tooltip("재생할 sound 테이블의 대표 UID입니다.")]
        [SerializeField] private int soundUid;

        /// <summary>
        /// 이 클립이 생성하는 스킬 이벤트 유형입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.PlayAudio;

        /// <summary>
        /// 재생할 sound 테이블의 대표 UID를 반환합니다.
        /// </summary>
        public int SoundUid => soundUid;
    }
}
