using System;
using Config;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 모든 스킬 이벤트 Timeline 클립의 공통 기능을 정의하는 추상 베이스 클래스입니다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Timeline 상의 클립 1개는 Bake 과정에서 런타임용 스킬 이벤트 1개에 대응됩니다.
    /// </para>
    /// <para>
    /// 파생 클래스는 이벤트 종류를 나타내는 <see cref="EventType"/>을 구현하며,
    /// 필요 시 클립별 직렬화 데이터를 추가로 보관할 수 있습니다.
    /// </para>
    /// <para>
    /// 이 클래스는 주로 에디터 제작 파이프라인에서 사용되며,
    /// 실제 런타임 실행은 Timeline 재생이 아니라 Bake된 데이터에 의해 처리됩니다.
    /// </para>
    /// </remarks>
    [Serializable]
    public abstract class SkillEventClipBase : PlayableAsset, ITimelineClipAsset
    {
        /// <summary>
        /// 동일 시점 이벤트 간 정렬 또는 처리 우선순위를 나타내는 순서 값입니다.
        /// </summary>
        [SerializeField] private int order = 0;

        /// <summary>
        /// 동일 시점에 배치된 이벤트를 정렬할 때 사용하는 순서 값입니다.
        /// 값이 작을수록 먼저 처리되도록 사용할 수 있습니다.
        /// </summary>
        public int Order => order;

        /// <summary>
        /// 이 클립이 지원하는 Timeline 편집 기능을 반환합니다.
        /// </summary>
        /// <remarks>
        /// 제작 안정성을 위해 Clip In 및 Speed Multiplier만 허용하고,
        /// 루프나 블렌딩과 같은 기능은 제한합니다.
        /// </remarks>
        public ClipCaps clipCaps => ClipCaps.ClipIn | ClipCaps.SpeedMultiplier;

        /// <summary>
        /// 이 클립이 표현하는 스킬 이벤트 유형입니다.
        /// </summary>
        public abstract ConfigCommonSkill.SkillEventType EventType { get; }

        /// <summary>
        /// Timeline 미리보기 호환을 위한 Playable을 생성합니다.
        /// </summary>
        /// <param name="graph">현재 Timeline 재생에 사용되는 PlayableGraph입니다.</param>
        /// <param name="owner">이 Playable의 소유 GameObject입니다.</param>
        /// <returns>실제 런타임 로직을 수행하지 않는 빈 Playable을 반환합니다.</returns>
        /// <remarks>
        /// 이 시스템의 실제 런타임 동작은 Timeline 직접 재생이 아니라 Bake 결과를 사용하므로,
        /// 에디터 프리뷰 호환성을 위한 최소 구현만 제공합니다.
        /// </remarks>
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            // 런타임 실행은 Bake 결과를 사용하므로 Timeline 재생용 로직은 비워 둡니다.
            return Playable.Null;
        }
    }
}