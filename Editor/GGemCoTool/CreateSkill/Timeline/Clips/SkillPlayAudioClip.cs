using System;
using Config;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 실행 중 오디오를 재생하는 Timeline 이벤트 클립입니다.
    /// </summary>
    /// <remarks>
    /// Timeline 작성 단계에서 사운드 UID와 반복 재생 여부를 정의하며,
    /// Bake 시 런타임 스킬 이벤트(<c>SkillRuntimeEvent</c>)의 PlayAudio 타입으로 변환됩니다.
    /// </remarks>
    [Serializable]
    public sealed class SkillPlayAudioClip : SkillEventClipBase
    {
        [Header("Sound")]
        [Tooltip("재생할 sound 테이블의 대표 UID입니다.")]
        [SerializeField] private int soundUid;

        [Tooltip("켜면 Timeline Clip 길이 동안 오디오를 반복 재생합니다.")]
        [SerializeField] private bool loop;

        /// <summary>
        /// 이 클립이 생성하는 스킬 이벤트 타입입니다.
        /// </summary>
        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.PlayAudio;

        /// <summary>
        /// 재생할 sound 테이블의 대표 UID를 반환합니다.
        /// </summary>
        public int SoundUid => soundUid;

        /// <summary>
        /// Timeline Clip 구간 동안 오디오를 반복 재생할지 여부입니다.
        /// </summary>
        public bool Loop => loop;
    }
}
