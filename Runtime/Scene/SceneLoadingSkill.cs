using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    public class SceneLoadingSkill : DefaultScene
    {
        private GameLoaderManager _gameLoaderManager;
        private AddressableLoaderSettingsSkill _addressableLoaderSettingsSkill;

        private void Awake()
        {
            if (!AddressableLoaderSettings.Instance)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(ConfigDefine.SceneNamePreIntro);
                return;
            }

            _addressableLoaderSettingsSkill = CompatObjectFind.FindFirst<AddressableLoaderSettingsSkill>() ??
                                              new GameObject("AddressableLoaderSettingsSkill")
                                                  .AddComponent<AddressableLoaderSettingsSkill>();
        }

        /// <summary>
        /// 오브젝트 활성화 시 로딩 시작 직전 이벤트 훅을 구독합니다.
        /// </summary>
        private void OnEnable()
        {
            // PreIntro 씬/Loading 씬에서 로딩 시작 직전 훅
            GameLoaderManager.BeforeLoadStartInLoadingScene += OnBeforeLoadStartInLoadingScene;

            if (_addressableLoaderSettingsSkill == null)
            {
                _addressableLoaderSettingsSkill = CompatObjectFind.FindFirst<AddressableLoaderSettingsSkill>() ??
                                                 new GameObject("AddressableLoaderSettingsSkill")
                                                     .AddComponent<AddressableLoaderSettingsSkill>();
            }

            _addressableLoaderSettingsSkill.OnLoadSettings -= HandleLoadSettings;
            _addressableLoaderSettingsSkill.OnLoadSettings += HandleLoadSettings;
        }

        /// <summary>
        /// 오브젝트 비활성화 시 이벤트 구독을 해제합니다.
        /// </summary>
        private void OnDisable()
        {
            GameLoaderManager.BeforeLoadStartInLoadingScene -= OnBeforeLoadStartInLoadingScene;

            if (_addressableLoaderSettingsSkill != null)
                _addressableLoaderSettingsSkill.OnLoadSettings -= HandleLoadSettings;
        }


        private void OnDestroy()
        {
            if (_addressableLoaderSettingsSkill != null)
                _addressableLoaderSettingsSkill.OnLoadSettings -= HandleLoadSettings;
        }

        private void HandleLoadSettings(GGemCoSkillSettings settings)
        {
#if UNITY_EDITOR
            SkillTestRuntimeHub.TryInitializeFromLoadedSettings(settings);
#endif
        }

        private void OnBeforeLoadStartInLoadingScene(
            GameLoaderManager sender,
            GameLoaderManager.EventArgsBeforeLoadStart e)
        {
            // 설정 스크립터블 오브젝트 
#if UNITY_EDITOR
            SkillTestRuntimeHub.ResetLoadedSettings();
#endif
            var addrSettings = _addressableLoaderSettingsSkill ??
                               CompatObjectFind.FindFirst<AddressableLoaderSettingsSkill>() ??
                               new GameObject("AddressableLoaderSettingsSkill")
                                   .AddComponent<AddressableLoaderSettingsSkill>();
            _addressableLoaderSettingsSkill = addrSettings;
            var step = new AddressableTaskStep(
                id: "skill.settings",
                order: 251,
                localizedKey: LocalizationConstants.Keys.Loading.TextTypeSettings(),
                startTask: () => addrSettings.LoadAllSettingsAsync(),
                getProgress: () => addrSettings.GetLoadProgress()
            );
            sender.Register(step);
            
            // 테이블 로더 준비 및 테이블 로딩 스텝 등록
            var tableLoader = CompatObjectFind.FindFirst<TableLoaderManagerSkill>() ??
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
                CompatObjectFind.FindFirst<LocalizationManagerSkill>() ??
                new GameObject("LocalizationManagerSkill").AddComponent<LocalizationManagerSkill>();

            var stepLocalization = new LocalizationLoadStep(
                "core.localization.skill",
                order: 222,
                localizedKey: LocalizationConstants.Keys.Loading.TextTypeLocalization(),
                localizationManager: loc,
                localeCode: PlayerPrefsManager.LoadLocalizationLocaleCode()
            );
            sender.Register(stepLocalization);

            // 스킬 아이콘 로딩 스텝 등록
            var addrSkill = CompatObjectFind.FindFirst<AddressableLoaderSkill>() ??
                             new GameObject("AddressableLoaderSkill").AddComponent<AddressableLoaderSkill>();

            var stepSkill = new AddressableTaskStep(
                id: "core.image.icon.skill",
                order: 340,
                localizedKey: LocalizationConstants.Keys.Loading.TextTypeSkill(),
                startTask: () => addrSkill.LoadAtlasesAsync(),
                getProgress: () => addrSkill.GetPrefabLoadProgress()
            );
            sender.Register(stepSkill);
            
            // 플레이어 스킬 런타임 시퀀스 파일 로드
            var addrSkillRuntimeSequencePlayer = CompatObjectFind.FindFirst<AddressableLoaderSkillRuntimeSequencePlayer>() ??
                             new GameObject("AddressableLoaderSkillRuntimeSequencePlayer").AddComponent<AddressableLoaderSkillRuntimeSequencePlayer>();

            var stepSkillRuntimeSequencePlayer = new AddressableTaskStep(
                id: "skill.runtimesequence.player",
                order: 345,
                localizedKey: LocalizationConstants.Keys.Loading.TextTypeSkill(),
                startTask: () => addrSkillRuntimeSequencePlayer.LoadAsync(),
                getProgress: () => addrSkillRuntimeSequencePlayer.GetLoadProgress()
            );
            sender.Register(stepSkillRuntimeSequencePlayer);

            // 스킬 세이브 데이터
            var saveData = CompatObjectFind.FindFirst<SaveDataLoaderSkill>() ?? new GameObject("SaveDataLoaderSkill").AddComponent<SaveDataLoaderSkill>();
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
