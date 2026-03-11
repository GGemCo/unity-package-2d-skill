using System.Collections.Generic;
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
        private StruckTableSkill _cachedRow;
        private StruckTableSkill _editingRow;
        private bool _editingDirty;
        
        private static readonly TableRowEditorUtility.TableRowEditorField[] RowEditorFields =
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

        private void OnGUIRowEditor()
        {
            if (_cachedRow == null || _editingRow == null)
            {
                EditorGUILayout.HelpBox($"선택된 데이터가 없습니다.", MessageType.Info);
                return;
            }

            _foldRowEdit = EditorGUILayout.Foldout(_foldRowEdit, "테이블 편집(선택 Row)", true);
            if (!_foldRowEdit) return;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                var drawResult = TableRowEditorUtility.DrawObjectEditor(_editingRow, RowEditorFields);
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
                            var info = GGemCo2DCore.TableLoaderManager.Instance.GetProjectileData(_selectedData.Uid);
                            if (info != null)
                            {
                                UpdateInGameTableInfo(_editingRow);
                            }
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

                            // 저장 후 재로드(툴 테이블)
                            // int keepUid = crowdControlUid;
                            TableLoaderManagerBase.Unload(ConfigAddressableTableSkill.TableSkill.Path);
                            _tableSkill = TableLoaderManagerSkill.LoadTableSkill();
                            _dictionary = _tableSkill?.GetDatas();

                            // LoadDropdown();
                            // crowdControlUid = keepUid;
                            // SyncSelectedIndexByUid();
                            CacheRow();

                            // 플레이 중이면 인게임에도 반영
                            UpdateInGameTableInfo(_cachedRow);

                            _editingDirty = false;
                            ShowNotification(new GUIContent("테이블 저장 완료"));
                        }
                    }
                }
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

            if (_tableSkill == null)
            {
                error = "테이블이 로드되지 않았습니다.";
                return false;
            }

            if (!TableTextRowPatchUtility.TryPatchRowByUid(
                    ConfigAddressableTableSkill.TableSkill.Path,
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

        private static string SerializeRow(StruckTableSkill row, IReadOnlyList<string> headers)
        {
            var values = new string[headers.Count];

            for (int i = 0; i < headers.Count; i++)
            {
                values[i] = headers[i] switch
                {
                    "Uid" => row.Uid.ToString(),
                    "Memo" => row.Memo ?? string.Empty,
                    "DefaultLearn" => MathHelper.FormatBool(row.DefaultLearn),
                    "NeedPlayerLevel" => row.NeedPlayerLevel.ToString(),
                    "UseClip" => row.UseClip ?? string.Empty,
                    "IconFileName" => row.IconFileName ?? string.Empty,
                    "SoFileName" => row.SoFileName ?? string.Empty,
                    "CastTime" => MathHelper.FormatFloat(row.CastTime),
                    "CoolTime" => MathHelper.FormatFloat(row.CoolTime),
                    "TargetingMode" => row.TargetingMode.ToString(),
                    "Range" => MathHelper.FormatFloat(row.Range),
                    "MaxTargets" => row.MaxTargets.ToString(),
                    "CastStartClip" => row.CastStartClip ?? string.Empty,
                    "CastLoopClip" => row.CastLoopClip ?? string.Empty,
                    "CastEndClip" => row.CastEndClip ?? string.Empty,
                    _ => string.Empty,
                };
            }

            return string.Join("\t", values);
        }
        
        private static void UpdateInGameTableInfo(StruckTableSkill row)
        {
            if (row == null) return;
            if (!Application.isPlaying) return;
            if (!GGemCo2DSkill.TableLoaderManagerSkill.Instance) return;

            var info = GGemCo2DSkill.TableLoaderManagerSkill.Instance.TableSkill.GetDataByUid(row.Uid);
            if (info == null) return;
            
            info.Uid = row.Uid;
            info.Memo = row.Memo;
            info.DefaultLearn = row.DefaultLearn;
            info.NeedPlayerLevel = row.NeedPlayerLevel;
            info.UseClip = row.UseClip;
            info.IconFileName = row.IconFileName;
            info.SoFileName = row.SoFileName;
            info.CastTime = row.CastTime;
            info.CoolTime = row.CoolTime;
            info.TargetingMode = row.TargetingMode;
            info.Range = row.Range;
            info.MaxTargets = row.MaxTargets;
            info.CastStartClip = row.CastStartClip;
            info.CastLoopClip = row.CastLoopClip;
            info.CastEndClip = row.CastEndClip;
        }
        
        private void CacheRow()
        {
            _cachedRow = null;
            _editingRow = null;
            _editingDirty = false;

            if (_dictionary == null) return;
            if (!_dictionary.TryGetValue(_selectedData.Uid, out var row) || row == null)
                return;

            _cachedRow = row;
            _editingRow = CloneRow(row);
        }

        private static StruckTableSkill CloneRow(StruckTableSkill row)
        {
            return TableRowEditorUtility.CloneShallow<StruckTableSkill>(row);
        }

        private bool ApplyEditingToCachedRow()
        {
            if (_cachedRow == null || _editingRow == null)
                return false;

            TableRowEditorUtility.CopyMembers(_editingRow, _cachedRow, RowEditorFields);
            NormalizeEditingRow();

            return true;
        }

        
        private void NormalizeEditingFieldValue(object target, string memberName)
        {
            if (!ReferenceEquals(target, _editingRow) || string.IsNullOrWhiteSpace(memberName))
                return;

            switch (memberName)
            {
                // case nameof(StruckTableCrowdControl.Distance):
                //     if (_editingRow.Distance < 0f) _editingRow.Distance = 0f;
                //     break;
                //
                // case nameof(StruckTableCrowdControl.Duration):
                //     if (_editingRow.Duration < 0f) _editingRow.Duration = 0f;
                //     break;
            }
        }
        private void NormalizeEditingRow()
        {
            // NormalizeEditingFieldValue(_editingRow, nameof(StruckTableCrowdControl.Distance));
            // NormalizeEditingFieldValue(_editingRow, nameof(StruckTableCrowdControl.Duration));
        }
    }
}
