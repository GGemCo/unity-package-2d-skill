using System;
using Config;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 모든 스킬 이벤트 클립의 공통 베이스.
    /// Timeline Clip(구간) 1개 = SkillRuntimeEvent 1개로 Bake된다.
    /// </summary>
    [Serializable]
    public abstract class SkillEventClipBase : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private int order = 0;

        public int Order => order;

        // 제작 안정성을 위해 캡을 제한
        public ClipCaps clipCaps => ClipCaps.ClipIn | ClipCaps.SpeedMultiplier;

        public abstract ConfigCommonSkill.SkillEventType EventType { get; }

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            // 런타임은 Bake로 처리. 에디터 프리뷰 호환 목적의 최소 구현.
            return Playable.Null;
        }
    }
}
