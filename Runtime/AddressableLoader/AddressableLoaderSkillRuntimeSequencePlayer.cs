using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 플레이어 스킬 런타임 시퀀스를 Addressables에서 로드하고 캐싱합니다.
    /// </summary>
    public class AddressableLoaderSkillRuntimeSequencePlayer : MonoBehaviour
    {
        public static AddressableLoaderSkillRuntimeSequencePlayer Instance { get; private set; }

        private readonly Dictionary<string, SkillRuntimeSequence> _handles = new Dictionary<string, SkillRuntimeSequence>();
        private readonly HashSet<AsyncOperationHandle> _activeHandles = new HashSet<AsyncOperationHandle>();
        private float _prefabLoadProgress;

#if UNITY_EDITOR
        /// <summary>
        /// Play Mode 스킬 테스트에서 Addressables 로딩을 우회하기 위한 오버라이드 캐시입니다.
        /// </summary>
        private static readonly Dictionary<string, SkillRuntimeSequence> EditorOverrides = new(StringComparer.Ordinal);

        /// <summary>
        /// 에디터 테스트용 시퀀스를 등록합니다.
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

        private void Awake()
        {
            _prefabLoadProgress = 0f;
            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDestroy()
        {
            ReleaseAll();
        }

        /// <summary>
        /// 모든 로드된 리소스를 해제합니다.
        /// </summary>
        private void ReleaseAll()
        {
            AddressableLoaderController.ReleaseByHandles(_activeHandles);
#if UNITY_EDITOR
            EditorOverrides.Clear();
#endif
        }

        /// <summary>
        /// 플레이어 시퀀스 전체를 선로드합니다.
        /// </summary>
        public async Task LoadAsync()
        {
            try
            {
                _handles.Clear();
                var locationHandle = Addressables.LoadResourceLocationsAsync(ConfigAddressableLabelSkill.SkillRuntimeSequencePlayer);
                await locationHandle.Task;

                if (!locationHandle.IsValid() || locationHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    GcLogger.LogError($"{ConfigAddressableLabelSkill.SkillRuntimeSequencePlayer} 레이블을 가진 리소스를 찾을 수 없습니다.");
                    return;
                }

                int totalCount = Mathf.Max(1, locationHandle.Result.Count);
                int loadedCount = 0;

                foreach (var location in locationHandle.Result)
                {
                    await LoadByKeyAsync(location.PrimaryKey);
                    loadedCount++;
                    _prefabLoadProgress = loadedCount / (float)totalCount;
                }

                Addressables.Release(locationHandle);
                _prefabLoadProgress = 1f;
            }
            catch (Exception ex)
            {
                GcLogger.LogError($"프리팹 로딩 중 오류 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// 지정 키의 플레이어 시퀀스를 반환합니다.
        /// </summary>
        public SkillRuntimeSequence GetSkillRuntimeSequenceByKey(string key)
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(key) && EditorOverrides.TryGetValue(key, out var editorOverride) && editorOverride != null)
            {
                return editorOverride;
            }
#endif
            if (_handles.TryGetValue(key, out var prefab))
            {
                return prefab;
            }

            GcLogger.LogError($"Addressables에서 {key} 프리팹을 찾을 수 없습니다.");
            return null;
        }

        /// <summary>
        /// 지정 키의 플레이어 시퀀스를 필요할 때만 비동기로 지연 로드합니다.
        /// </summary>
        /// <param name="key">시퀀스 Addressables 키입니다.</param>
        /// <returns>로드된 시퀀스입니다. 실패 시 null입니다.</returns>
        public async Task<SkillRuntimeSequence> LoadByKeyAsync(string key)
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(key) && EditorOverrides.TryGetValue(key, out var editorOverride) && editorOverride != null)
            {
                return editorOverride;
            }
#endif
            if (string.IsNullOrWhiteSpace(key))
            {
                return null;
            }

            if (_handles.TryGetValue(key, out var cached) && cached != null)
            {
                return cached;
            }

            var loadHandle = Addressables.LoadAssetAsync<SkillRuntimeSequence>(key);
            _activeHandles.Add(loadHandle);
            SkillRuntimeSequence sequence = await loadHandle.Task;

            if (loadHandle.Status == AsyncOperationStatus.Succeeded && sequence != null)
            {
                _handles[key] = sequence;
                return sequence;
            }

            GcLogger.LogError($"Addressables에서 {key} 프리팹을 찾을 수 없습니다.");
            return null;
        }

        public float GetLoadProgress() => _prefabLoadProgress;
    }
}
