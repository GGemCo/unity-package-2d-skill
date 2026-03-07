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
    /// 스킬 아이콘 이미지 로드
    /// </summary>
    public class AddressableLoaderSkill : MonoBehaviour
    {
        public static AddressableLoaderSkill Instance { get; private set; }
        private readonly Dictionary<string, SpriteAtlas> _dicImageIconSkill = new Dictionary<string, SpriteAtlas>();
        private readonly Dictionary<string, SpriteAtlas> _dicImageIconSkillPassive = new Dictionary<string, SpriteAtlas>();
        private readonly HashSet<AsyncOperationHandle> _activeHandles = new HashSet<AsyncOperationHandle>();
        private float _prefabLoadProgress;

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
        public async Task LoadAtlasesAsync()
        {
            try
            {
                // 엑티브 스킬 아이콘 이미지
                _dicImageIconSkill.Clear();
                var locationHandle = Addressables.LoadResourceLocationsAsync(ConfigAddressableLabelSkill.ImageSkillIcon);
                await locationHandle.Task;

                if (!locationHandle.IsValid() || locationHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    GcLogger.LogError($"{ConfigAddressableLabelSkill.ImageSkillIcon} 레이블을 가진 리소스를 찾을 수 없습니다.");
                    return;
                }

                int totalCount = locationHandle.Result.Count;
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
                    _dicImageIconSkill[address] = prefab;
                    loadedCount++;
                }
                _activeHandles.Add(locationHandle);

                #region 패시브 스킬
                    
                _dicImageIconSkillPassive.Clear();
                locationHandle = Addressables.LoadResourceLocationsAsync(ConfigAddressableLabelSkill.ImageSkillPassiveIcon);
                await locationHandle.Task;

                if (!locationHandle.IsValid() || locationHandle.Status != AsyncOperationStatus.Succeeded)
                {
                    GcLogger.LogError($"{ConfigAddressableLabelSkill.ImageSkillPassiveIcon} 레이블을 가진 리소스를 찾을 수 없습니다.");
                    return;
                }

                totalCount = locationHandle.Result.Count;
                loadedCount = 0;

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
                    _dicImageIconSkillPassive[address] = prefab;
                    loadedCount++;
                }
                _activeHandles.Add(locationHandle);
                #endregion

                _prefabLoadProgress = 1f; // 100%
                // GcLogger.Log($"총 {loadedCount}/{totalCount}개의 프리팹을 성공적으로 로드했습니다.");
            }
            catch (Exception ex)
            {
                GcLogger.LogError($"스킬 아이콘 이미지 로딩 중 오류 발생: {ex.Message}");
            }
        }

        public Sprite GetSkillIconImageByName(string fileName)
        {
            if (_dicImageIconSkill.TryGetValue(ConfigAddressableLabelSkill.ImageSkillIcon, out var prefab))
            {
                return prefab.GetSprite(fileName);
            }

            GcLogger.LogError($"아이콘 Atlas에서 엑티브 스킬 아이콘 이미지를 찾을 수 없습니다. fileName: {fileName} ");
            return null;
        }
        public Sprite GetSkillPassiveIconImageByName(string fileName)
        {
            if (_dicImageIconSkillPassive.TryGetValue(ConfigAddressableLabelSkill.ImageSkillPassiveIcon, out var prefab))
            {
                return prefab.GetSprite(fileName);
            }

            GcLogger.LogError($"아이콘 Atlas에서 패시브 스킬 아이콘 이미지를 찾을 수 없습니다. fileName: {fileName} ");
            return null;
        }
        public float GetPrefabLoadProgress() => _prefabLoadProgress;
    }
}
