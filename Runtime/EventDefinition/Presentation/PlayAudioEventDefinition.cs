using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 Timeline에서 Bake된 오디오 재생 이벤트 정의입니다.
    /// </summary>
    public sealed class PlayAudioEventDefinition : ScriptableObject
    {
        /// <summary>
        /// 재생할 오디오 클립입니다.
        /// Timeline Authoring 단계에서 직접 참조한 AudioClip이 저장됩니다.
        /// </summary>
        public AudioClip clip;

        /// <summary>
        /// 오디오 재생 볼륨 배율입니다.
        /// 0은 음소거, 1은 원본 볼륨입니다.
        /// </summary>
        [Range(0f, 1f)]
        public float volume = 1f;
    }
}
