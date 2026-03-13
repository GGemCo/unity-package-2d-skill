using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// CreateSkillWindow의 테이블 Row 편집 패널을 구성하는 partial 구현입니다.
    /// 선택된 스킬 Row를 편집하고, 변경 내용을 테스트 적용하거나 테이블 파일에 저장하는 기능을 제공합니다.
    /// </summary>
    public partial class CreateSkillWindow
    {
        /// <summary>
        /// Row 편집 패널의 Foldout 펼침 상태를 나타냅니다.
        /// </summary>
        private bool _foldRowEdit = true;

        /// <summary>
        /// 현재 선택된 원본 Row의 캐시입니다.
        /// 저장 시 기준이 되는 데이터로 사용됩니다.
        /// </summary>
        private SkillEditorRow _cachedRow;

        /// <summary>
        /// 사용자가 편집 중인 Row 복사본입니다.
        /// 원본 데이터와 분리하여 임시 편집 상태를 유지합니다.
        /// </summary>
        private SkillEditorRow _editingRow;

        /// <summary>
        /// 편집 중인 Row에 저장되지 않은 변경 사항이 있는지 여부를 나타냅니다.
        /// </summary>
        private bool _editingDirty;

        /// <summary>
        /// 현재 선택된 테이블 종류에 대응하는 Addressable 테이블 경로를 반환합니다.
        /// </summary>
        private string CurrentTablePath =>
            _selectedSource == ConfigCommon.SkillTableSource.Monster
                ? ConfigAddressableTableSkill.TableSkillMonster.Path
                : ConfigAddressableTableSkill.TableSkill.Path;
        
        /// <summary>
        /// 플레이어 스킬 테이블 Row 편집 시 표시할 필드 정의 목록입니다.
        /// </summary>
        private static readonly TableRowEditorUtility.TableRowEditorField[] RowEditorFieldsPlayer =
            TableRowEditorUtility.BuildFields<StruckTableSkill>(BuildRowEditorOptionsPlayer());
        private static TableRowEditorUtility.TableRowEditorBuildOptions BuildRowEditorOptionsPlayer()
        {
            var options = new TableRowEditorUtility.TableRowEditorBuildOptions();
            options.ReadOnlyMembers.Add(nameof(StruckTableSkill.Uid));
            return options;
        }

        /// <summary>
        /// 몬스터 스킬 테이블 Row 편집 시 표시할 필드 정의 목록입니다.
        /// </summary>
        private static readonly TableRowEditorUtility.TableRowEditorField[] RowEditorFieldsMonster =
            TableRowEditorUtility.BuildFields<StruckTableSkillMonster>(BuildRowEditorOptionsMonster());
        private static TableRowEditorUtility.TableRowEditorBuildOptions BuildRowEditorOptionsMonster()
        {
            var options = new TableRowEditorUtility.TableRowEditorBuildOptions();
            options.ReadOnlyMembers.Add(nameof(StruckTableSkillMonster.Uid));
            return options;
        }

        /// <summary>
        /// 현재 선택된 테이블 종류에 맞는 Row 편집 필드 목록을 반환합니다.
        /// </summary>
        private IReadOnlyList<TableRowEditorUtility.TableRowEditorField> CurrentRowEditorFields =>
            _selectedSource == ConfigCommon.SkillTableSource.Monster
                ? RowEditorFieldsMonster
                : RowEditorFieldsPlayer;

        /// <summary>
        /// 선택된 테이블 Row를 편집하는 UI를 그립니다.
        /// 편집값 변경, 되돌리기, 런타임 테스트 적용 및 테이블 파일 저장을 처리합니다.
        /// </summary>
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
                var drawResult = TableRowEditorUtility.DrawObjectEditor(_editingRow, CurrentRowEditorFields,
                    NormalizeEditingFieldValue);
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

        /// <summary>
        /// 현재 선택된 스킬 테이블을 다시 로드하고 선택 상태를 복원합니다.
        /// 가능한 경우 기존에 선택되어 있던 Uid를 기준으로 Row를 다시 선택합니다.
        /// </summary>
        private void ReloadCurrentTable()
        {
            int keepUid = _cachedRow != null ? _cachedRow.Uid : (_selectedData != null ? _selectedData.Uid : 0);

            TableLoaderManagerBase.Unload(CurrentTablePath);
            if (_selectedSource == ConfigCommon.SkillTableSource.Monster)
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

        /// <summary>
        /// 현재 캐시된 Row를 테이블 파일에 저장합니다.
        /// </summary>
        /// <param name="error">저장 실패 시 오류 메시지를 반환합니다.</param>
        /// <returns>저장에 성공하면 <see langword="true"/>, 실패하면 <see langword="false"/>입니다.</returns>
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

        /// <summary>
        /// 스킬 Row를 테이블 헤더 순서에 맞는 탭 구분 문자열로 직렬화합니다.
        /// </summary>
        /// <param name="row">직렬화할 스킬 Row입니다.</param>
        /// <param name="headers">출력 순서를 결정하는 테이블 헤더 목록입니다.</param>
        /// <returns>테이블 한 줄 형식으로 직렬화된 문자열입니다.</returns>
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

        /// <summary>
        /// 플레이 중인 게임의 스킬 테이블 데이터에 편집 내용을 즉시 반영합니다.
        /// 에디터에서 테스트 목적으로 런타임 테이블 값을 갱신할 때 사용됩니다.
        /// </summary>
        /// <param name="row">런타임 테이블에 반영할 스킬 Row입니다.</param>
        private static void UpdateInGameTableInfo(SkillEditorRow row)
        {
            if (row == null) return;
            if (!Application.isPlaying) return;
            if (!GGemCo2DSkill.TableLoaderManagerSkill.Instance) return;

            if (row.Source == ConfigCommon.SkillTableSource.Monster)
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

        /// <summary>
        /// 현재 선택된 데이터를 기준으로 원본 Row와 편집용 Row를 다시 캐시합니다.
        /// </summary>
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

        /// <summary>
        /// 지정한 Row의 얕은 복사본을 생성합니다.
        /// </summary>
        /// <param name="row">복사할 원본 Row입니다.</param>
        /// <returns>편집에 사용할 수 있는 Row 복사본입니다.</returns>
        private static SkillEditorRow CloneRow(SkillEditorRow row)
        {
            return TableRowEditorUtility.CloneShallow<SkillEditorRow>(row);
        }

        /// <summary>
        /// 편집 중인 Row의 값을 원본 캐시 Row에 반영하고 정규화합니다.
        /// </summary>
        /// <returns>반영에 성공하면 <see langword="true"/>, 반영할 대상이 없으면 <see langword="false"/>입니다.</returns>
        private bool ApplyEditingToCachedRow()
        {
            if (_cachedRow == null || _editingRow == null)
                return false;

            NormalizeEditingRow();
            TableRowEditorUtility.CopyMembers(_editingRow, _cachedRow, CurrentRowEditorFields);

            return true;
        }

        /// <summary>
        /// 편집 중인 Row의 특정 필드 값을 유효 범위로 보정합니다.
        /// 음수가 허용되지 않는 수치 항목은 0 이상으로 정규화합니다.
        /// </summary>
        /// <param name="target">보정 대상 객체입니다.</param>
        /// <param name="memberName">보정할 멤버 이름입니다.</param>
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

        /// <summary>
        /// 편집 중인 Row 전체에 대해 유효성 보정을 수행합니다.
        /// 현재는 음수가 허용되지 않는 주요 수치 필드를 정규화합니다.
        /// </summary>
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