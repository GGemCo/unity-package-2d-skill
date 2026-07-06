using GGemCo2DCore;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    public class SettingTableSkill : DefaultAddressable
    {
        private const string Title = "Skill 테이블 추가하기";

        /// <summary>
        /// UI 레이아웃(버튼 폭/높이 등) 정보를 제공하는 부모 에디터 윈도우 참조입니다.
        /// </summary>
        private readonly AddressableEditorSkill _addressableEditor;

        public SettingTableSkill(AddressableEditorSkill addressableEditorWindow)
        {
            _addressableEditor = addressableEditorWindow;

            // NOTE: 테이블 자산은 Table 그룹으로 등록합니다.
            targetGroupName = ConfigAddressableGroupName.Table;
        }

        /// <summary>
        /// 에디터 윈도우에 표시될 UI를 그립니다.
        /// </summary>
        /// <remarks>
        /// 버튼 클릭 시 Setup을 호출하여 테이블 Addressables 등록을 수행합니다.
        /// </remarks>
        public void OnGUI()
        {
            if (GUILayout.Button(Title, GUILayout.Width(_addressableEditor.buttonWidth), GUILayout.Height(_addressableEditor.buttonHeight)))
            {
                try
                {
                    Setup();
                }
                catch (System.Exception e)
                {
                    Debug.LogException(e);
                    EditorUtility.DisplayDialog(
                        Title,
                        "데이터 테이블 Addressable 설정 중 오류가 발생했습니다.\n자세한 내용은 콘솔 로그를 확인해주세요.",
                        "OK");
                }
            }
        }

        /// <summary>
        /// Skill 테이블 원본과 런타임 테이블 팩을 Addressables에 등록합니다.
        /// </summary>
        /// <param name="ctx">자동 설정 실행 컨텍스트입니다. null이면 완료 다이얼로그를 표시합니다.</param>
        public void Setup(EditorSetupContext ctx = null)
        {
            // AddressableSettings 가져오기 (없으면 생성)
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (!settings)
            {
                HelperLog.Warn("Addressable 설정을 찾을 수 없습니다. 새로 생성합니다.", ctx);
                settings = CreateAddressableSettings();
            }

            // Table 그룹 가져오기 또는 생성
            AddressableAssetGroup group = GetOrCreateGroup(settings, targetGroupName);
            if (!group)
            {
                HelperLog.Error($"'{targetGroupName}' 그룹을 설정할 수 없습니다.", ctx);
                return;
            }

            RegisterRuntimeTablePack(settings, group, ctx);

            foreach (var addressableAssetInfo in ConfigAddressableTableSkill.All)
            {
                Add(settings, group, addressableAssetInfo.Key, addressableAssetInfo.Path, ConfigAddressableLabel.Table);
                // Debug.Log($"Addressable 키 값 설정: {keyName}");
            }

            // 설정 저장
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);

            if (ctx != null)
            {
                HelperLog.Info("Addressable 설정 완료", ctx);
            }
            else
            {
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog(Title, "Addressable 설정 완료", "OK");
            }
        }

        /// <summary>
        /// Skill 개별 테이블 txt를 런타임 팩으로 생성하고 Addressables에 등록합니다.
        /// </summary>
        /// <param name="settings">Addressables 설정 객체입니다.</param>
        /// <param name="group">등록 대상 Table 그룹입니다.</param>
        /// <param name="ctx">자동 설정 실행 컨텍스트입니다.</param>
        private void RegisterRuntimeTablePack(AddressableAssetSettings settings, AddressableAssetGroup group, EditorSetupContext ctx)
        {
            AddressableAssetInfo pack = ConfigAddressableTableSkill.TablePackSkill;
            bool built = RuntimeTablePackBuilder.Build(
                ConfigAddressableTableSkill.PackageId,
                pack,
                ConfigAddressableTableSkill.All,
                ctx);

            if (!built)
            {
                HelperLog.Warn("Skill 런타임 테이블 팩 생성에 실패했습니다. 개별 테이블 등록은 계속 진행합니다.", ctx);
                return;
            }

            Add(settings, group, pack.Key, pack.Path, ConfigAddressableLabel.TablePack);
        }
    }
}
