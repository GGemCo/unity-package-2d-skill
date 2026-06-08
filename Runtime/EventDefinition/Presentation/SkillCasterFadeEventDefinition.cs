using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 캐스터 캐릭터 페이드 이벤트의 실행 방식을 정의합니다.
    /// </summary>
    public enum SkillCasterFadeMode
    {
        /// <summary>
        /// 캐스터를 보이도록 페이드 인합니다.
        /// </summary>
        FadeIn = 0,

        /// <summary>
        /// 캐스터를 숨기도록 페이드 아웃합니다.
        /// </summary>
        FadeOut = 1,
    }

    /// <summary>
    /// 스킬 타임라인에서 캐스터 캐릭터의 표시 알파를 제어하기 위한 이벤트 정의입니다.
    /// Bake된 RuntimeSequence의 Payload로 저장되어 <see cref="SkillExecutor"/>에서 실행됩니다.
    /// </summary>
    public sealed class SkillCasterFadeEventDefinition : ScriptableObject
    {
        [Header("Mode")]
        [Tooltip("캐스터 페이드 실행 방식입니다.")]
        public SkillCasterFadeMode mode = SkillCasterFadeMode.FadeOut;

        [Header("Timing")]
        [Tooltip("켜면 Timeline Clip 길이를 페이드 지속 시간으로 사용합니다.")]
        public bool useClipDuration = true;

        [Tooltip("useClipDuration이 꺼져 있을 때 사용할 페이드 지속 시간(초)입니다.")]
        [Min(0f)] public float durationOverrideSeconds = 0.15f;

        [Header("End Policy")]
        [Tooltip("스킬이 정상 종료될 때 캐스터 알파를 1로 복구할지 여부입니다.")]
        public bool restoreOnSkillEnd = false;

        [Tooltip("스킬이 취소될 때 캐스터 알파를 1로 복구할지 여부입니다.")]
        public bool restoreOnCancel = true;

        /// <summary>
        /// 이벤트 길이와 오버라이드 설정을 기준으로 실제 페이드 지속 시간을 계산합니다.
        /// </summary>
        /// <param name="clipDurationSeconds">Timeline Clip에서 계산된 이벤트 길이(초)입니다.</param>
        /// <returns>런타임에서 사용할 페이드 지속 시간(초)입니다.</returns>
        public float ResolveDuration(float clipDurationSeconds)
        {
            return useClipDuration
                ? Mathf.Max(0f, clipDurationSeconds)
                : Mathf.Max(0f, durationOverrideSeconds);
        }
    }
}
