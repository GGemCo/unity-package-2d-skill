using Config;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// UseClip 재생 속도 정책과 RuntimeSequence 논리 시간을 연결하는 런타임 시간 보정 정보입니다.
    /// </summary>
    public readonly struct SkillRunTimingContext
    {
        /// <summary>
        /// 보정이 없는 기본 시간 컨텍스트입니다.
        /// </summary>
        public static SkillRunTimingContext Default => new(
            ConfigCommonSkill.SkillUseClipTimingPolicy.RuntimeSequence,
            timelineRate: 1f,
            realUseDurationSeconds: 0f,
            logicalSequenceDurationSeconds: 0f);

        /// <summary>
        /// UseClip과 RuntimeSequence를 동기화하는 정책입니다.
        /// </summary>
        public ConfigCommonSkill.SkillUseClipTimingPolicy Policy { get; }

        /// <summary>
        /// 실제 시간 1초가 RuntimeSequence 논리 시간에서 얼마나 진행되는지 나타내는 배율입니다.
        /// </summary>
        public float TimelineRate { get; }

        /// <summary>
        /// RuntimeSequence 논리 지속 시간을 실제 지속 시간으로 환산할 때 사용하는 배율입니다.
        /// </summary>
        public float DurationScale => 1f / TimelineRate;

        /// <summary>
        /// UseClip 길이와 재생 속도를 반영한 실제 재생 시간입니다.
        /// ScaleSequenceToUseClip 정책에서는 종료 기준으로 사용하지 않고, RuntimeSequence 시간축 배율이 종료를 결정합니다.
        /// </summary>
        public float RealUseDurationSeconds { get; }

        /// <summary>
        /// RuntimeSequence에 Bake된 원본 전체 길이입니다.
        /// </summary>
        public float LogicalSequenceDurationSeconds { get; }

        /// <summary>
        /// 시간 보정 컨텍스트를 생성합니다.
        /// </summary>
        /// <param name="policy">UseClip 동기화 정책입니다.</param>
        /// <param name="timelineRate">실제 시간에서 RuntimeSequence 논리 시간으로 진행되는 배율입니다.</param>
        /// <param name="realUseDurationSeconds">UseClip 실제 재생 시간입니다. 정책에 따라 종료 기준으로 사용하지 않을 수 있습니다.</param>
        /// <param name="logicalSequenceDurationSeconds">RuntimeSequence 원본 길이입니다.</param>
        public SkillRunTimingContext(
            ConfigCommonSkill.SkillUseClipTimingPolicy policy,
            float timelineRate,
            float realUseDurationSeconds,
            float logicalSequenceDurationSeconds)
        {
            Policy = policy;
            TimelineRate = Mathf.Max(0.001f, timelineRate);
            RealUseDurationSeconds = Mathf.Max(0f, realUseDurationSeconds);
            LogicalSequenceDurationSeconds = Mathf.Max(0f, logicalSequenceDurationSeconds);
        }

        /// <summary>
        /// RuntimeSequence 논리 시간 구간을 실제 런타임 지속 시간으로 변환합니다.
        /// </summary>
        /// <param name="logicalDurationSeconds">RuntimeSequence에 저장된 논리 지속 시간입니다.</param>
        /// <returns>현재 시간 보정 정책을 반영한 실제 지속 시간입니다.</returns>
        public float ScaleDuration(float logicalDurationSeconds)
        {
            return Mathf.Max(0f, logicalDurationSeconds) * DurationScale;
        }
    }
}
