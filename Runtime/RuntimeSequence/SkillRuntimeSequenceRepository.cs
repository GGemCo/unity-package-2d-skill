using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GGemCo2DSkill
{
    /// <summary>
    /// SkillRuntimeSequence Addressables 로더/캐시.
    /// </summary>
    public static class SkillRuntimeSequenceRepository
    {
        private static readonly Dictionary<string, AsyncOperationHandle<SkillRuntimeSequence>> _handles = new(StringComparer.Ordinal);

        public static async Task<SkillRuntimeSequence> LoadAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

            if (_handles.TryGetValue(key, out var h) && h.IsValid())
            {
                return await h.Task;
            }

            var handle = Addressables.LoadAssetAsync<SkillRuntimeSequence>(key);
            _handles[key] = handle;

            var asset = await handle.Task;
            return asset;
        }

        public static void Release(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (_handles.TryGetValue(key, out var h) && h.IsValid())
            {
                Addressables.Release(h);
            }
            _handles.Remove(key);
        }

        public static void ReleaseAll()
        {
            foreach (var kv in _handles)
            {
                var h = kv.Value;
                if (h.IsValid()) Addressables.Release(h);
            }
            _handles.Clear();
        }
    }
}
