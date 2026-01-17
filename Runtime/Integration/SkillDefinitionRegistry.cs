using System;
using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 런타임에서 SkillDefinition.skillId -> SkillDefinition 매핑을 제공하는 레지스트리.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillDefinitionRegistry : MonoBehaviour
    {
        [SerializeField] private List<SkillDefinition> skills = new();

        private readonly Dictionary<string, SkillDefinition> _byId = new(StringComparer.Ordinal);

        private void Awake() => Rebuild();

        private void OnValidate()
        {
            if (!Application.isPlaying)
                Rebuild();
        }

        public void Rebuild()
        {
            _byId.Clear();
            if (skills == null) return;

            for (int i = 0; i < skills.Count; i++)
            {
                var def = skills[i];
                if (def == null) continue;
                if (string.IsNullOrEmpty(def.skillId)) continue;
                _byId[def.skillId] = def;
            }
        }

        public bool TryGet(string skillId, out SkillDefinition definition)
        {
            if (string.IsNullOrEmpty(skillId))
            {
                definition = null;
                return false;
            }
            return _byId.TryGetValue(skillId, out definition) && definition != null;
        }
    }
}