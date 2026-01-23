using System;
using Config;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 이벤트 클립 기반 Authoring(V2).
    /// - Marker를 사용하지 않고, Timeline Clip의 start 시간으로 이벤트를 Bake한다.
    /// </summary>
    [Serializable]
    public abstract class SkillEventClipV2 : PlayableAsset, ITimelineClipAsset
    {
        [SerializeField] private ConfigCommonSkill.SkillEventType eventType;

        /// <summary>런타임에서 사용할 Payload(ScriptableObject)</summary>
        [SerializeField] private UnityEngine.Object payload;

        public ConfigCommonSkill.SkillEventType EventType => eventType;
        public UnityEngine.Object Payload => payload;

        public ClipCaps clipCaps => ClipCaps.None;

        protected void SetType(ConfigCommonSkill.SkillEventType type) => eventType = type;

        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
            => Playable.Null;
    }
}
