using System.Collections.Generic;
using System.IO;
using GGemCo2DCore;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    public class SettingSkill : DefaultAddressable
    {
        private const string Title = "스킬 아이콘/스크립터블 오브젝트 추가하기";
        private readonly AddressableEditorSkill _addressableEditorSkill;
        private const string TargetGroupNameRuntimeSequence = ConfigAddressableGroupNameSkill.SkillRuntimeSequence;
        private const string TargetGroupNameSkillPassiveIcon = ConfigAddressableGroupNameSkill.SkillPassiveIcon;
        
        public SettingSkill(AddressableEditorSkill addressableEditorSkillWindow)
        {
            _addressableEditorSkill = addressableEditorSkillWindow;
            targetGroupName = ConfigAddressableGroupNameSkill.SkillIcon;
        }
        public void OnGUI()
        {
            if (!File.Exists($"{ConfigAddressableTableSkill.TableSkill.Path}"))
            {
                EditorGUILayout.HelpBox($"{ConfigAddressableTableSkill.Skill} 테이블이 없습니다.", MessageType.Info);
            }
            else
            {
                if (GUILayout.Button(Title, GUILayout.Width(_addressableEditorSkill.buttonWidth), GUILayout.Height(_addressableEditorSkill.buttonHeight)))
                {
                    try
                    {
                        Setup();
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogException(e);
                        EditorUtility.DisplayDialog(Title, "스킬 Addressable 설정 중 오류가 발생했습니다.\n자세한 내용은 콘솔 로그를 확인해주세요.", "OK");
                    }
                }
            }
        }
        /// <summary>
        /// Addressable 설정하기
        /// </summary>
        public void Setup(EditorSetupContext ctx = null)
        {
            if (ctx == null)
            {
                bool result = EditorUtility.DisplayDialog(TextDisplayDialogTitle, TextDisplayDialogMessage, "네", "아니요");
                if (!result) return;
            }
            Dictionary<int, StruckTableSkill> dictionary = TableLoaderManagerSkill.LoadTableSkill().GetDatas();
            
            // AddressableSettings 가져오기 (없으면 생성)
            AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
            if (!settings)
            {
                HelperLog.Warn("Addressable 설정을 찾을 수 없습니다. 새로 생성합니다.", ctx);
                settings = CreateAddressableSettings();
            }
            
            # region 아이콘
            // GGemCo_Tables 그룹 가져오기 또는 생성
            AddressableAssetGroup group = GetOrCreateGroup(settings, targetGroupName);
            if (!group)
            {
                HelperLog.Error($"'{targetGroupName}' 그룹을 설정할 수 없습니다.", ctx);
                return;
            }
            
            ClearGroupEntries(settings, group);
            
            string atlasFolderPath = ConfigAddressablePath.SpriteAtlas;
            Directory.CreateDirectory(atlasFolderPath);
    
            var atlas = GetOrCreateSpriteAtlas($"{atlasFolderPath}/SkillIconAtlas.spriteatlas");
            
            List<Object> assets = new();
            if (group)
            {
                // foreach 문을 사용하여 딕셔너리 내용을 출력
                foreach (var data in dictionary)
                {
                    var info = data.Value;
                    if (info.Uid <= 0) continue;
                    if (string.IsNullOrEmpty(info.IconFileName)) continue;
                
                    string key = $"{ConfigAddressableKeySkill.SkillIcon}_{info.Uid}";
                    string assetPath = $"{ConfigAddressablePathSkill.Images.Icon.Skill}/{info.IconFileName}.png";
                
                    Add(settings, group, key, assetPath);
                    AddToListIfExists(assets, assetPath);
                }
            }
            ClearAndAddToAtlas(atlas, assets);

            if (assets.Count > 0)
                Add(settings, group, ConfigAddressableKeySkill.SkillIcon, AssetDatabase.GetAssetPath(atlas),
                    ConfigAddressableLabelSkill.ImageSkillIcon);
            
            #endregion

            #region 패시브 스킬

            Dictionary<int, StruckTableSkillPassive> dictionaryPassive =
                TableLoaderManagerSkill.LoadTableSkillPassive().GetDatas();

            // GGemCo_Tables 그룹 가져오기 또는 생성
            group = GetOrCreateGroup(settings, TargetGroupNameSkillPassiveIcon);
            if (!group)
            {
                HelperLog.Error($"'{TargetGroupNameSkillPassiveIcon}' 그룹을 설정할 수 없습니다.", ctx);
                return;
            }
            
            ClearGroupEntries(settings, group);
            
            atlas = GetOrCreateSpriteAtlas($"{atlasFolderPath}/SkillPassiveIconAtlas.spriteatlas");
            
            assets.Clear();
            if (group)
            {
                // foreach 문을 사용하여 딕셔너리 내용을 출력
                foreach (var data in dictionaryPassive)
                {
                    var info = data.Value;
                    if (info.Uid <= 0) continue;
                    if (string.IsNullOrEmpty(info.IconFileName)) continue;
                
                    string key = $"{ConfigAddressableKeySkill.SkillPassiveIcon}_{info.Uid}";
                    string assetPath = $"{ConfigAddressablePathSkill.Images.Icon.SkillPassive}/{info.IconFileName}.png";
                
                    Add(settings, group, key, assetPath);
                    AddToListIfExists(assets, assetPath);
                }
            }
            ClearAndAddToAtlas(atlas, assets);

            if (assets.Count > 0)
                Add(settings, group, ConfigAddressableKeySkill.SkillPassiveIcon, AssetDatabase.GetAssetPath(atlas),
                    ConfigAddressableLabelSkill.ImageSkillPassiveIcon);

            #endregion

            #region 스크립터블 오브젝트

            // GGemCo_Tables 그룹 가져오기 또는 생성
            group = GetOrCreateGroup(settings, TargetGroupNameRuntimeSequence);
            if (!group)
            {
                HelperLog.Error($"'{TargetGroupNameRuntimeSequence}' 그룹을 설정할 수 없습니다.", ctx);
                return;
            }
            
            ClearGroupEntries(settings, group);
            
            if (group)
            {
                // foreach 문을 사용하여 딕셔너리 내용을 출력
                foreach (var data in dictionary)
                {
                    var info = data.Value;
                    if (info.Uid <= 0) continue;
                    if (string.IsNullOrEmpty(info.SoFileName)) continue;
                
                    string key = $"{ConfigAddressableKeySkill.GetRuntimeSequenceKey(info.Uid)}";
                    string assetPath = $"{ConfigAddressablePathSkill.Skill.RuntimeSequences}/{info.SoFileName}.asset";
                
                    Add(settings, group, key, assetPath);
                }
            }

            #endregion

            // 설정 저장
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryMoved, null, true);
            if (ctx != null)
            {
                HelperLog.Info("[Addressable] 스킬 설정 완료", ctx);
            }
            else
            {
                AssetDatabase.SaveAssets();
                EditorUtility.DisplayDialog(Title, "[Addressable] 스킬 설정 완료", "OK");
            }
        }
    }
}