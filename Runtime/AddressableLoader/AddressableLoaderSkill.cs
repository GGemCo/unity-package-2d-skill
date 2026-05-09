using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GGemCo2DCore;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.U2D;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 아이콘 Atlas를 Addressables에서 로드하고 캐싱합니다.
    /// </summary>
    public class AddressableLoaderSkill : MonoBehaviour
    {
        public static AddressableLoaderSkill Instance { get; private set; }

        private readonly Dictionary<string, SpriteAtlas> _dicImageIconSkill = new Dictionary<string, SpriteAtlas>();
        private readonly Dictionary<string, SpriteAtlas> _dicImageIconSkillPassive = new Dictionary<string, SpriteAtlas>();
        private readonly HashSet<AsyncOperationHandle> _activeHandles = new HashSet<AsyncOperationHandle>();
        private float _prefabLoadProgress;
        private bool _isAtlasLoaded;

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
        }

        /// <summary>
        /// 스킬 아이콘 Atlas 전체를 선로드합니다.
        /// </summary>
        public async Task LoadAtlasesAsync()
        {
            try
            {
                _dicImageIconSkill.Clear();
                _dicImageIconSkillPassive.Clear();

                await LoadAtlasGroupAsync(ConfigAddressableLabelSkill.ImageSkillIcon, _dicImageIconSkill);
                await LoadAtlasGroupAsync(ConfigAddressableLabelSkill.ImageSkillPassiveIcon, _dicImageIconSkillPassive);

                _isAtlasLoaded = true;
                _prefabLoadProgress = 1f;
            }
            catch (Exception ex)
            {
                GcLogger.LogError($"스킬 아이콘 이미지 로딩 중 오류 발생: {ex.Message}");
            }
        }

        /// <summary>
        /// 액티브 스킬 아이콘을 반환합니다.
        /// 선로드되지 않은 경우 최초 1회 지연 로드를 수행합니다.
        /// </summary>
        public Sprite GetSkillIconImageByName(string fileName)
        {
            EnsureAtlasLoadedSync();
            Sprite sprite = FindSpriteInAtlases(_dicImageIconSkill, fileName);
            if (sprite != null)
            {
                return sprite;
            }

            GcLogger.LogError($"아이콘 Atlas에서 엑티브 스킬 아이콘 이미지를 찾을 수 없습니다. fileName: {fileName} ");
            return null;
        }

        /// <summary>
        /// 패시브 스킬 아이콘을 반환합니다.
        /// 선로드되지 않은 경우 최초 1회 지연 로드를 수행합니다.
        /// </summary>
        public Sprite GetSkillPassiveIconImageByName(string fileName)
        {
            EnsureAtlasLoadedSync();
            Sprite sprite = FindSpriteInAtlases(_dicImageIconSkillPassive, fileName);
            if (sprite != null)
            {
                return sprite;
            }

            GcLogger.LogError($"아이콘 Atlas에서 패시브 스킬 아이콘 이미지를 찾을 수 없습니다. fileName: {fileName} ");
            return null;
        }


        /// <summary>
        /// 로드된 Atlas 집합에서 지정한 스프라이트를 검색합니다.
        /// </summary>
        private static Sprite FindSpriteInAtlases(Dictionary<string, SpriteAtlas> atlases, string fileName)
        {
            foreach (var pair in atlases)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                Sprite sprite = pair.Value.GetSprite(fileName);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return null;
        }

        /// <summary>
        /// 시작 로딩에서 제외된 Atlas를 최초 접근 시 동기적으로 로드합니다.
        /// </summary>
        private void EnsureAtlasLoadedSync()
        {
            if (_isAtlasLoaded)
            {
                return;
            }

            LoadAtlasGroupSync(ConfigAddressableLabelSkill.ImageSkillIcon, _dicImageIconSkill);
            LoadAtlasGroupSync(ConfigAddressableLabelSkill.ImageSkillPassiveIcon, _dicImageIconSkillPassive);
            _isAtlasLoaded = true;
        }

        /// <summary>
        /// 지정 라벨의 Atlas 그룹을 비동기로 로드합니다.
        /// </summary>
        private async Task LoadAtlasGroupAsync(string label, Dictionary<string, SpriteAtlas> target)
        {
            var locationHandle = Addressables.LoadResourceLocationsAsync(label);
            await locationHandle.Task;

            if (!locationHandle.IsValid() || locationHandle.Status != AsyncOperationStatus.Succeeded)
            {
                GcLogger.LogError($"{label} 레이블을 가진 리소스를 찾을 수 없습니다.");
                return;
            }

            int totalCount = Mathf.Max(1, locationHandle.Result.Count);
            int loadedCount = 0;

            foreach (var location in locationHandle.Result)
            {
                string address = location.PrimaryKey;
                var loadHandle = Addressables.LoadAssetAsync<SpriteAtlas>(address);

                while (!loadHandle.IsDone)
                {
                    _prefabLoadProgress = (loadedCount + loadHandle.PercentComplete) / totalCount;
                    await Task.Yield();
                }
                _activeHandles.Add(loadHandle);

                SpriteAtlas prefab = await loadHandle.Task;
                if (!prefab) continue;
                target[address] = prefab;
                loadedCount++;
            }

            Addressables.Release(locationHandle);
        }

        /// <summary>
        /// 지정 라벨의 Atlas 그룹을 동기적으로 로드합니다.
        /// </summary>
        private void LoadAtlasGroupSync(string label, Dictionary<string, SpriteAtlas> target)
        {
            if (target.Count > 0)
            {
                return;
            }

            var locationHandle = Addressables.LoadResourceLocationsAsync(label);
            var locations = locationHandle.WaitForCompletion();

            if (!locationHandle.IsValid() || locationHandle.Status != AsyncOperationStatus.Succeeded || locations == null)
            {
                GcLogger.LogError($"{label} 레이블을 가진 리소스를 찾을 수 없습니다.");
                Addressables.Release(locationHandle);
                return;
            }

            foreach (var location in locations)
            {
                string address = location.PrimaryKey;
                var loadHandle = Addressables.LoadAssetAsync<SpriteAtlas>(address);
                _activeHandles.Add(loadHandle);
                SpriteAtlas atlas = loadHandle.WaitForCompletion();
                if (loadHandle.Status == AsyncOperationStatus.Succeeded && atlas != null)
                {
                    target[address] = atlas;
                }
            }

            Addressables.Release(locationHandle);
        }

        public float GetPrefabLoadProgress() => _prefabLoadProgress;
    }
}
