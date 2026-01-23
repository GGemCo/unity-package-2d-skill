using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Timeline에서 Bake된 스킬 런타임 시퀀스.
    /// - Runtime은 Timeline에 의존하지 않고 이 에셋만 실행한다.
    /// </summary>
    [CreateAssetMenu(menuName = "GGemCo/Skills/RuntimeSequence", fileName = "SkillRuntimeSequence")]
    public sealed class SkillRuntimeSequence : ScriptableObject
    {
        [SerializeField] private int skillUid;
        [SerializeField] private float duration;
        [SerializeField] private SkillRuntimeEvent[] events;
        [SerializeField] private Object[] payloads;

        public int SkillUid => skillUid;
        public float Duration => duration;
        public SkillRuntimeEvent[] Events => events;

        public Object GetPayload(int index)
        {
            if (payloads == null) return null;
            if (index < 0 || index >= payloads.Length) return null;
            return payloads[index];
        }

#if UNITY_EDITOR
        public void EditorSetData(int uid, float dur, SkillRuntimeEvent[] ev, Object[] pl)
        {
            skillUid = uid;
            duration = dur;
            events = ev;
            payloads = pl;
        }
#endif
    }
}