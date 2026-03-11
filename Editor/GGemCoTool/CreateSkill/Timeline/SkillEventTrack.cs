using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 이벤트 클립을 재생하기 위한 Timeline Track입니다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 이 Track은 <see cref="SkillEventClipBase"/>를 기반으로 하는 클립들을 포함하며,
    /// Timeline 재생 시 <see cref="SkillEventMixerBehaviour"/>를 통해 클립들의 실행을 조정합니다.
    /// </para>
    /// <para>
    /// 스킬 타임라인 Bake 과정에서 생성되는 이벤트 트랙으로,
    /// 스킬 실행 중 발생하는 이벤트(이펙트, 데미지, 상태이상 등)를 관리하는 역할을 합니다.
    /// </para>
    /// </remarks>
    [TrackColor(0.25f, 0.65f, 0.95f)]
    [TrackClipType(typeof(SkillEventClipBase))]
    public sealed class SkillEventTrack : TrackAsset
    {
        /// <summary>
        /// Timeline에서 이 Track이 사용할 Playable Mixer를 생성합니다.
        /// </summary>
        /// <param name="graph">PlayableGraph 인스턴스입니다.</param>
        /// <param name="go">이 Track이 바인딩된 GameObject입니다.</param>
        /// <param name="inputCount">Track에 연결된 클립 수입니다.</param>
        /// <returns>
        /// <see cref="SkillEventMixerBehaviour"/>를 사용하는 Playable을 반환합니다.
        /// </returns>
        /// <remarks>
        /// Mixer는 여러 이벤트 클립이 동시에 존재할 경우
        /// Timeline 재생 시점에 맞게 실행 흐름을 조정하는 역할을 합니다.
        /// </remarks>
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<SkillEventMixerBehaviour>.Create(graph, inputCount);
        }
    }
}