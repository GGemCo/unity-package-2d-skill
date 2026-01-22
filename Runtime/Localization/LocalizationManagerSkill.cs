using System.Collections;
using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using UnityEngine.ResourceManagement.AsyncOperations;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace GGemCo2DSkill
{
    public class LocalizationManagerSkill : LocalizationManagerBase
    {
        /// <summary>
        /// 현재 활성화된 <see cref="LocalizationManagerSkill"/> 인스턴스입니다.
        /// </summary>
        public static LocalizationManagerSkill Instance { get; private set; }

        private readonly Dictionary<string, bool> _userTableExistsMap = new();

        /// <summary>
        /// 싱글톤 인스턴스를 설정하고, 씬 전환 시에도 유지되도록 합니다.
        /// </summary>
        /// <remarks>
        /// 이미 인스턴스가 존재하는 경우 중복 인스턴스를 제거합니다.
        /// </remarks>
        protected override void Awake()
        {
            base.Awake();

            if (!Instance)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                // 중복 인스턴스 방지
                Destroy(gameObject);
            }
        }

        protected override IEnumerator CheckUserTablesExist()
        {
            foreach (string baseTable in LocalizationConstantsSkill.Tables.All)
            {
                string userTableName = $"{baseTable}_User";

                // 선택된 로케일에 대해 사용자 테이블을 비동기로 조회합니다.
                AsyncOperationHandle<StringTable> handle = stringDatabase.GetTableAsync(userTableName, LocalizationSettings.SelectedLocale);
                yield return handle;

                bool exists = false;

                if (handle.IsValid())
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded && handle.Result != null)
                    {
                        exists = true;
                        // GcLogger.Log($"table: {userTableName} / exist: true");
                    }
                    else
                    {
                        // GcLogger.Log($"table: {userTableName} / exist: false");
                    }
                }
                else
                {
                    // 핸들이 유효하지 않은 경우(로딩 실패/잘못된 참조 등)
                    GcLogger.LogWarning($"Invalid handle for table: {userTableName}");
                }

                // baseTable을 키로 캐시합니다. (userTableName이 아니라 baseTable 기준으로 관리)
                _userTableExistsMap[baseTable] = exists;

                // Addressables 기반 핸들인 경우 리소스 참조를 해제합니다.
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
        }

        public string GetSkillNameByKey(string key) => GetString(LocalizationConstantsSkill.Tables.SkillName, key);

        public string GetSkillDescriptionByKey(string key) =>
            GetString(LocalizationConstantsSkill.Tables.SkillDescription, key);

        public string GetSkillDescriptionSmart(string key, params object[] args) =>
            GetSmartString(LocalizationConstantsSkill.Tables.SkillDescription, key, args);

        public bool HasSkillDescriptionLocalizationKey(string key)
        {
            return HasLocalizationKey(LocalizationConstantsSkill.Tables.SkillDescription, key);
        }
    }
}
