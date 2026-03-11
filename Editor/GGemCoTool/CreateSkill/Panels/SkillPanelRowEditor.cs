using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    public partial class CreateSkillWindow
    {
        private bool _foldRowEdit = true;
        private SkillEditorRow _cachedRow;
        private SkillEditorRow _editingRow;
        private bool _editingDirty;
        /// <summary>
        /// 현재 선택된 테이블 종류에 대응하는 Addressable 테이블 경로를 반환합니다.
        /// </summary>
        private string CurrentTablePath =>
            _selectedSource == ConfigCommonSkill.SkillTableSource.Monster
                ? ConfigAddressableTableSkill.TableSkillMonster.Path
                : ConfigAddressableTableSkill.TableSkill.Path;
        
        private static readonly TableRowEditorUtility.TableRowEditorField[] RowEditorFieldsPlayer =
        {
            new("Uid", readOnly: true),
            new("Memo"),
            new("DefaultLearn"),
            new("NeedPlayerLevel"),
            new("UseClip"),
            new("IconFileName"),
            new("SoFileName"),
            new("CastTime"),
            new("CoolTime"),
            new("TargetingMode"),
            new("Range"),
            new("MaxTargets"),
            new("CastStartClip"),
            new("CastLoopClip"),
            new("CastEndClip"),
        };

        private static readonly TableRowEditorUtility.TableRowEditorField[] RowEditorFieldsMonster =
        {
            new("Uid", readOnly: true),
            new("Memo"),
            new("UseClip"),
            new("SoFileName"),
            new("CastTime"),
            new("CoolTime"),
            new("TargetingMode"),
            new("Range"),
            new("MaxTargets"),
            new("CastStartClip"),
            new("CastLoopClip"),
            new("CastEndClip"),
        };

        private IReadOnlyList<TableRowEditorUtility.TableRowEditorField> CurrentRowEditorFields =>
            _selectedSource == ConfigCommonSkill.SkillTableSource.Monster ? RowEditorFieldsMonster : RowEditorFieldsPlayer;

        private void OnGUIRowEditor()
        {
            if (_cachedRow == null || _editingRow == null)
            {
                EditorGUILayout.HelpBox("선택된 데이터가 없습니다.", MessageType.Info);
                return;
            }

            _foldRowEdit = EditorGUILayout.Foldout(_foldRowEdit, "테이블 편집(선택 Row)", true);
            if (!_foldRowEdit) return;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                var drawResult = TableRowEditorUtility.DrawObjectEditor(_editingRow, CurrentRowEditorFields, NormalizeEditingFieldValue);
                if (drawResult.Changed)
                {
                    _editingDirty = true;
                }
                
                EditorGUILayout.Space(6);

                using (new EditorGUILayout.HorizontalScope())
                {
                    using (new EditorGUI.DisabledScope(!_editingDirty))
                    {
                        if (GUILayout.Button("되돌리기"))
                        {
                            _editingRow = CloneRow(_cachedRow);
                            _editingDirty = false;
                        }
                
                        if (GUILayout.Button("테스트 적용"))
                        {
                            UpdateInGameTableInfo(_editingRow);
                        }

                        if (GUILayout.Button("저장(테이블 파일)"))
                        {
                            if (!ApplyEditingToCachedRow())
                                return;

                            if (!TrySaveTableFile(out var err))
                            {
                                EditorUtility.DisplayDialog(Title, err, "OK");
                                return;
                            }

                            ReloadCurrentTable();

                            UpdateInGameTableInfo(_cachedRow);

                            _editingDirty = false;
                            ShowNotification(new GUIContent("테이블 저장 완료"));
                        }
                    }
                }
            }
        }

        private void ReloadCurrentTable()
        {
            int keepUid = _cachedRow != null ? _cachedRow.Uid : (_selectedData != null ? _selectedData.Uid : 0);

            TableLoaderManagerBase.Unload(CurrentTablePath);
            if (_selectedSource == ConfigCommonSkill.SkillTableSource.Monster)
            {
                _tableSkillMonster = TableLoaderManagerSkill.LoadTableSkillMonster();
                _monsterDictionary = BuildMonsterDictionary(_tableSkillMonster);
            }
            else
            {
                _tableSkill = TableLoaderManagerSkill.LoadTableSkill();
                _playerDictionary = BuildPlayerDictionary(_tableSkill);
            }

            RebuildDropdown();

            if (keepUid > 0 && CurrentDictionary != null && CurrentDictionary.TryGetValue(keepUid, out var selected))
            {
                _selectedData = selected;
                CacheRow();
            }
        }

        private bool TrySaveTableFile(out string error)
        {
            error = null;

            if (_cachedRow == null)
            {
                error = "저장할 Row가 없습니다.";
                return false;
            }

            if (!HasCurrentTableLoaded)
            {
                error = "테이블이 로드되지 않았습니다.";
                return false;
            }

            if (!TableTextRowPatchUtility.TryPatchRowByUid(
                    CurrentTablePath,
                    _cachedRow.Uid,
                    _cachedRow,
                    SerializeRow,
                    out error))
            {
                error = $"테이블 저장 중 오류: {error}";
                return false;
            }

            return true;
        }

        private static string SerializeRow(SkillEditorRow row, IReadOnlyList<string> headers)
        {
            var values = new string[headers.Count];

            for (int i = 0; i < headers.Count; i++)
            {
                values[i] = headers[i] switch
                {
                    "Uid" => row.Uid.ToString(),
                    "Name" => row.Name ?? string.Empty,
                    "Memo" => row.Memo ?? string.Empty,
                    "DefaultLearn" => MathHelper.FormatBool(row.DefaultLearn),
                    "NeedPlayerLevel" => row.NeedPlayerLevel.ToString(),
                    "IconFileName" => row.IconFileName ?? string.Empty,
                    "SoFileName" => row.SoFileName ?? string.Empty,
                    "SkillKind" => row.SkillKind.ToString(),
                    "CastTime" => MathHelper.FormatFloat(row.CastTime),
                    "CoolTime" => MathHelper.FormatFloat(row.CoolTime),
                    "TargetingMode" => row.TargetingMode.ToString(),
                    "Range" => MathHelper.FormatFloat(row.Range),
                    "MaxTargets" => row.MaxTargets.ToString(),
                    "CastStartClip" => row.CastStartClip ?? string.Empty,
                    "CastLoopClip" => row.CastLoopClip ?? string.Empty,
                    "CastEndClip" => row.CastEndClip ?? string.Empty,
                    "UseClip" => row.UseClip ?? string.Empty,
                    _ => string.Empty,
                };
            }

            return string.Join("\t", values);
        }
        
        private static void UpdateInGameTableInfo(SkillEditorRow row)
        {
            if (row == null) return;
            if (!Application.isPlaying) return;
            if (!GGemCo2DSkill.TableLoaderManagerSkill.Instance) return;

            if (row.Source == ConfigCommonSkill.SkillTableSource.Monster)
            {
                var info = GGemCo2DSkill.TableLoaderManagerSkill.Instance.TableSkillMonster.GetDataByUid(row.Uid);
                if (info == null) return;

                info.Uid = row.Uid;
                info.Memo = row.Memo;
                info.SoFileName = row.SoFileName;
                info.SkillKind = row.SkillKind;
                info.CastTime = row.CastTime;
                info.CoolTime = row.CoolTime;
                info.TargetingMode = row.TargetingMode;
                info.Range = row.Range;
                info.MaxTargets = row.MaxTargets;
                info.CastStartClip = row.CastStartClip;
                info.CastLoopClip = row.CastLoopClip;
                info.CastEndClip = row.CastEndClip;
                info.UseClip = row.UseClip;
                return;
            }

            var playerInfo = GGemCo2DSkill.TableLoaderManagerSkill.Instance.TableSkill.GetDataByUid(row.Uid);
            if (playerInfo == null) return;
            
            playerInfo.Uid = row.Uid;
            playerInfo.Memo = row.Memo;
            playerInfo.DefaultLearn = row.DefaultLearn;
            playerInfo.NeedPlayerLevel = row.NeedPlayerLevel;
            playerInfo.IconFileName = row.IconFileName;
            playerInfo.SoFileName = row.SoFileName;
            playerInfo.SkillKind = row.SkillKind;
            playerInfo.CastTime = row.CastTime;
            playerInfo.CoolTime = row.CoolTime;
            playerInfo.TargetingMode = row.TargetingMode;
            playerInfo.Range = row.Range;
            playerInfo.MaxTargets = row.MaxTargets;
            playerInfo.CastStartClip = row.CastStartClip;
            playerInfo.CastLoopClip = row.CastLoopClip;
            playerInfo.CastEndClip = row.CastEndClip;
            playerInfo.UseClip = row.UseClip;
        }
        
        private void CacheRow()
        {
            _cachedRow = null;
            _editingRow = null;
            _editingDirty = false;

            if (_selectedData == null || CurrentDictionary == null) return;
            if (!CurrentDictionary.TryGetValue(_selectedData.Uid, out var row) || row == null)
                return;

            _cachedRow = row;
            _editingRow = CloneRow(row);
        }

        private static SkillEditorRow CloneRow(SkillEditorRow row)
        {
            return TableRowEditorUtility.CloneShallow<SkillEditorRow>(row);
        }

        private bool ApplyEditingToCachedRow()
        {
            if (_cachedRow == null || _editingRow == null)
                return false;

            TableRowEditorUtility.CopyMembers(_editingRow, _cachedRow, CurrentRowEditorFields);
            NormalizeEditingRow();

            return true;
        }

        
        private void NormalizeEditingFieldValue(object target, string memberName)
        {
            if (!ReferenceEquals(target, _editingRow) || string.IsNullOrWhiteSpace(memberName))
                return;

            switch (memberName)
            {
                case nameof(SkillEditorRow.CastTime):
                    if (_editingRow.CastTime < 0f) _editingRow.CastTime = 0f;
                    break;
                case nameof(SkillEditorRow.CoolTime):
                    if (_editingRow.CoolTime < 0f) _editingRow.CoolTime = 0f;
                    break;
                case nameof(SkillEditorRow.Range):
                    if (_editingRow.Range < 0f) _editingRow.Range = 0f;
                    break;
                case nameof(SkillEditorRow.MaxTargets):
                    if (_editingRow.MaxTargets < 0) _editingRow.MaxTargets = 0;
                    break;
                case nameof(SkillEditorRow.NeedPlayerLevel):
                    if (_editingRow.NeedPlayerLevel < 0) _editingRow.NeedPlayerLevel = 0;
                    break;
            }
        }
        private void NormalizeEditingRow()
        {
            NormalizeEditingFieldValue(_editingRow, nameof(SkillEditorRow.CastTime));
            NormalizeEditingFieldValue(_editingRow, nameof(SkillEditorRow.CoolTime));
            NormalizeEditingFieldValue(_editingRow, nameof(SkillEditorRow.Range));
            NormalizeEditingFieldValue(_editingRow, nameof(SkillEditorRow.MaxTargets));
            NormalizeEditingFieldValue(_editingRow, nameof(SkillEditorRow.NeedPlayerLevel));
        }
    }
}
