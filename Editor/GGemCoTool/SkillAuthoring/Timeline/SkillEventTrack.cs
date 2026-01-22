using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace GGemCo2DSkillEditor
{
    [TrackColor(0.25f, 0.65f, 0.95f)]
    [TrackClipType(typeof(SkillEventClipBase))]
    public sealed class SkillEventTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<SkillEventMixerBehaviour>.Create(graph, inputCount);
        }
    }
}
