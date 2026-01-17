// Assets/GGemCo/Skills/Runtime/Combat/AreaRegistry.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DSkill
{
    [CreateAssetMenu(menuName = "GGemCo/Skills/Area/Area Registry", fileName = "AreaRegistry")]
    public sealed class AreaRegistry : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string areaId;
            public AreaDefinition def;
        }

        [SerializeField] private List<Entry> entries = new();

        private Dictionary<string, AreaDefinition> _cache;

        private void OnEnable() => BuildCache();

        public void BuildCache()
        {
            _cache = new Dictionary<string, AreaDefinition>(StringComparer.Ordinal);
            foreach (var e in entries)
            {
                if (string.IsNullOrWhiteSpace(e.areaId) || e.def == null) continue;
                _cache[e.areaId] = e.def;
            }
        }

        public bool TryGet(string areaId, out AreaDefinition def)
        {
            if (_cache == null) BuildCache();
            if (string.IsNullOrWhiteSpace(areaId))
            {
                def = null;
                return false;
            }
            return _cache.TryGetValue(areaId, out def) && def != null;
        }
    }
}