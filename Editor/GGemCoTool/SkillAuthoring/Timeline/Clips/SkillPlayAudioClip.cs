using System;
using Config;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    [Serializable]
    public sealed class SkillPlayAudioClip : SkillEventClipBase
    {
        [SerializeField] private AudioClip clip;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        public override ConfigCommonSkill.SkillEventType EventType => ConfigCommonSkill.SkillEventType.PlayAudio;

        public AudioClip Clip => clip;
        public float Volume => volume;
    }
}
