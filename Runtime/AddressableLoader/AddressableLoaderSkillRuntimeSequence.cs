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
    public static class AddressableLoaderSkillRuntimeSequence
    {
        private static readonly Dictionary<string, AsyncOperationHandle<SkillRuntimeSequence>> Handles = new(StringComparer.Ordinal);

#if UNITY_EDITOR
        /// <summary>
        /// Play Mode 스킬 테스트(에디터)에서 Addressables 로딩을 우회하기 위한 오버라이드 캐시.
        /// </summary>
        private static readonly Dictionary<string, SkillRuntimeSequence> EditorOverrides = new(StringComparer.Ordinal);

        /// <summary>
        /// 에디터 테스트용 시퀀스를 등록합니다.
        /// - 동일 key가 이미 Addressables handle로 로드되어 있다면, 오버라이드가 우선됩니다.
        /// </summary>
        public static void RegisterEditorOverride(string key, SkillRuntimeSequence sequence)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (sequence == null)
            {
                EditorOverrides.Remove(key);
                return;
            }
            EditorOverrides[key] = sequence;
        }

        public static void ClearEditorOverrides() => EditorOverrides.Clear();
#endif

        public static async Task<SkillRuntimeSequence> LoadAsync(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;

#if UNITY_EDITOR
            // 에디터 테스트 오버라이드 우선
            if (EditorOverrides.TryGetValue(key, out var overrideSeq) && overrideSeq != null)
                return overrideSeq;
#endif

            if (Handles.TryGetValue(key, out var h) && h.IsValid())
            {
                return await h.Task;
            }

            var handle = Addressables.LoadAssetAsync<SkillRuntimeSequence>(key);
            Handles[key] = handle;

            var asset = await handle.Task;
            return asset;
        }

        public static void Release(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (Handles.TryGetValue(key, out var h) && h.IsValid())
            {
                Addressables.Release(h);
            }
            Handles.Remove(key);

#if UNITY_EDITOR
            EditorOverrides.Remove(key);
#endif
        }

        public static void ReleaseAll()
        {
            foreach (var kv in Handles)
            {
                var h = kv.Value;
                if (h.IsValid()) Addressables.Release(h);
            }
            Handles.Clear();

#if UNITY_EDITOR
            EditorOverrides.Clear();
#endif
        }
    }
}
