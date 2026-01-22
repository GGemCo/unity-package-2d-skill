using Config;
using UnityEngine;
using UnityEngine.Timeline;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Timeline에서 SignalEmitter.asset에 넣어 사용하는 신호 자산.
    /// Bake 시점에 type/payload를 읽어서 SkillEventSequence로 변환한다.
    /// </summary>
    [CreateAssetMenu(menuName = "GGemCo/Skills/Authoring/Skill Signal", fileName = "SkillSignal")]
    public sealed class SkillSignalAsset : SignalAsset
    {
        public ConfigCommonSkill.SkillEventType eventType;
        public Object payload;
        public int order;
    }
}