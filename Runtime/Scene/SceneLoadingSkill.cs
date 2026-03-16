using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    public class SceneLoadingSkill : DefaultScene
    {
        private GameLoaderManager _gameLoaderManager;

        private void Awake()
        {
            if (!AddressableLoaderSettings.Instance)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(ConfigDefine.SceneNamePreIntro);
                return;
            }
        }

        /// <summary>
        /// 오브젝트 활성화 시 로딩 시작 직전 이벤트 훅을 구독합니다.
        /// </summary>
        private void OnEnable()
        {
            // PreIntro 씬/Loading 씬에서 로딩 시작 직전 훅
            GameLoaderManager.BeforeLoadStartInLoadingScene += OnBeforeLoadStartInLoadingScene;
        }

        /// <summary>
        /// 오브젝트 비활성화 시 이벤트 구독을 해제합니다.
        /// </summary>
        private void OnDisable()
        {
            GameLoaderManager.BeforeLoadStartInLoadingScene -= OnBeforeLoadStartInLoadingScene;
        }

        private void OnBeforeLoadStartInLoadingScene(
            GameLoaderManager sender,
            GameLoaderManager.EventArgsBeforeLoadStart e)
        {
            // 설정 스크립터블 오브젝트 
            var addrSettings = Object.FindFirstObjectByType<AddressableLoaderSettingsSkill>() ??
                               new GameObject("AddressableLoaderSettingsSkill")
                                   .AddComponent<AddressableLoaderSettingsSkill>();
            var step = new AddressableTaskStep(
                id: "skill.settings",
                order: 251,
                localizedKey: LocalizationConstants.Keys.Loading.TextTypeSettings(),
                startTask: () => addrSettings.LoadAllSettingsAsync(),
                getProgress: () => addrSettings.GetLoadProgress()
            );
            sender.Register(step);
            
            // 테이블 로더 준비 및 테이블 로딩 스텝 등록
            var tableLoader =
                FindFirstObjectByType<TableLoaderManagerSkill>() ??
                new GameObject("TableLoaderManagerSkill").AddComponent<TableLoaderManagerSkill>();

            var targetTables = ConfigAddressableTableSkill.All;
            var stepTable = new TableLoadStep(
                id: "core.table.skill",
                order: 247,
                localizedKey: LocalizationConstants.Keys.Loading.TextTypeTables(),
                tableLoader: tableLoader,
                tables: targetTables
            );
            sender.Register(stepTable);

            // 로컬라이징 매니저 준비 및 로컬라이징 로딩 스텝 등록
            var loc =
                Object.FindFirstObjectByType<LocalizationManagerSkill>() ??
                new GameObject("LocalizationManagerSkill").AddComponent<LocalizationManagerSkill>();

            var stepLocalization = new LocalizationLoadStep(
                "core.localization.skill",
                order: 222,
                localizedKey: LocalizationConstants.Keys.Loading.TextTypeLocalization(),
                localizationManager: loc,
                localeCode: PlayerPrefsManager.LoadLocalizationLocaleCode()
            );
            sender.Register(stepLocalization);

            // 어펙트 이미지(아틀라스 등) 로딩 스텝 등록
            var addrSkill = Object.FindFirstObjectByType<AddressableLoaderSkill>() ??
                             new GameObject("AddressableLoaderSkill").AddComponent<AddressableLoaderSkill>();

            var stepSkill = new AddressableTaskStep(
                id: "core.image.icon.skill",
                order: 340,
                localizedKey: LocalizationConstants.Keys.Loading.TextTypeSkill(),
                startTask: () => addrSkill.LoadAtlasesAsync(),
                getProgress: () => addrSkill.GetPrefabLoadProgress()
            );
            sender.Register(stepSkill);

            // 스킬 세이브 데이터
            var saveData = Object.FindFirstObjectByType<SaveDataLoaderSkill>() ?? new GameObject("SaveDataLoaderSkill").AddComponent<SaveDataLoaderSkill>();
            var stepSaveDta = new SaveDataLoadStep(
                "core.savedata.skill",
                order: 381,
                localizedKey: LocalizationConstants.Keys.Loading.TextTypeSaveData(),
                saveDataLoader: saveData
            );
            sender.Register(stepSaveDta);
        }
    }
}
