using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 오디오 이벤트의 Payload 검증과 일회성 AudioSource 생성을 담당합니다.
    /// </summary>
    internal static class SkillPlayAudioEventHandler
    {
        /// <summary>
        /// 오디오 이벤트 정의에 따라 캐스터 위치 기준의 일회성 사운드를 재생합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다.</param>
        /// <param name="payloadObj">Bake된 오디오 이벤트 정의입니다.</param>
        public static void Handle(SkillTargetContext ctx, Object payloadObj)
        {
            if (payloadObj is not PlayAudioEventDefinition def)
                return;
            if (def.clip == null)
                return;

            Vector3 playPosition = ctx.caster != null ? ctx.caster.transform.position : Vector3.zero;
            GameObject audioObject = new GameObject($"SkillAudio_{def.clip.name}");
            audioObject.transform.position = playPosition;

            AudioSource source = audioObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 1f;
            source.clip = def.clip;
            source.PlayOneShot(def.clip, Mathf.Clamp01(def.volume));

            Object.Destroy(audioObject, def.clip.length + 0.1f);
        }
    }
}
