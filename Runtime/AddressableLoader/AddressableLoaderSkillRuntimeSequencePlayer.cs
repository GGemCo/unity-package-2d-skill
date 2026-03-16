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
    /// 이펙트 프리팹 로드
    /// </summary>
    public class AddressableLoaderSkillRuntimeSequencePlayer : MonoBehaviour
    {
        public static AddressableLoaderSkillRuntimeSequencePlayer Instance { get; private set; }
        private readonly Dictionary<string, SkillRuntimeSequence> _handles = new Dictionary<string, SkillRuntimeSequence>();
        private readonly HashSet<AsyncOperationHandle> _activeHandles = new HashSet<AsyncOperationHandle>();
        private float _prefabLoadProgress;

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

                int totalCount = locationHandle.Result.Count;
                int loadedCount = 0;

                foreach (var location in locationHandle.Result)
                {
                    string address = location.PrimaryKey;
                    var loadHandle = Addressables.LoadAssetAsync<SkillRuntimeSequence>(address);

                    while (!loadHandle.IsDone)
                    {
                        _prefabLoadProgress = (loadedCount + loadHandle.PercentComplete) / totalCount;
                        await Task.Yield();
                    }
                    _activeHandles.Add(loadHandle);

                    SkillRuntimeSequence prefab = await loadHandle.Task;
                    if (!prefab) continue;
                    _handles[address] = prefab;
                    loadedCount++;
                }
                _activeHandles.Add(locationHandle);

                _prefabLoadProgress = 1f; // 100%
                // GcLogger.Log($"총 {loadedCount}/{totalCount}개의 프리팹을 성공적으로 로드했습니다.");
            }
            catch (Exception ex)
            {
                GcLogger.LogError($"프리팹 로딩 중 오류 발생: {ex.Message}");
            }
        }
        
        public SkillRuntimeSequence GetSkillRuntimeSequenceByKey(string key)
        {
            if (_handles.TryGetValue(key, out var prefab))
            {
                return prefab;
            }

            GcLogger.LogError($"Addressables에서 {key} 프리팹을 찾을 수 없습니다.");
            return null;
        }

        public float GetLoadProgress() => _prefabLoadProgress;
    }
}
