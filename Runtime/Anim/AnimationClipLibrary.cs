// Assets/GGemCo/Skills/Runtime/Anim/AnimationClipLibrary.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GGemCo2DSkill
{
    [CreateAssetMenu(menuName = "GGemCo/Skills/Animation Clip Library", fileName = "AnimationClipLibrary")]
    public sealed class AnimationClipLibrary : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key; // 고정 규칙 이름(예: "SK_0001_Cast_Start")
            public AnimationClip clip;
        }

        [SerializeField] private List<Entry> entries = new();

        private Dictionary<string, AnimationClip> _cache;

        private void OnEnable()
        {
            BuildCache();
        }

        public void BuildCache()
        {
            _cache = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
            foreach (var e in entries)
            {
                if (string.IsNullOrWhiteSpace(e.key) || e.clip == null) continue;
                _cache[e.key] = e.clip;
            }
        }

        public bool TryGetClip(string key, out AnimationClip clip)
        {
            if (_cache == null) BuildCache();
            if (string.IsNullOrWhiteSpace(key))
            {
                clip = null;
                return false;
            }
            return _cache.TryGetValue(key, out clip) && clip != null;
        }
    }
}