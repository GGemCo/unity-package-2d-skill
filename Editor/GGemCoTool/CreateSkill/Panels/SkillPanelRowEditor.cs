using System;
using System.Collections.Generic;
using System.Reflection;
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
        private object _cachedRow;

        /// <summary>
        /// 사용자가 편집 중인 Row 복사본입니다.
        /// 원본 데이터와 분리하여 임시 편집 상태를 유지합니다.
        /// </summary>
        private object _editingRow;

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
            int keepUid = GetUid(_cachedRow);
            if (keepUid <= 0)
                keepUid = GetSelectedUid();

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

            if (keepUid > 0 && TryGetCurrentRowByUid(keepUid, out var selected))
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
                    GetUid(_cachedRow),
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
        private static string SerializeRow(object row, IReadOnlyList<string> headers)
        {
            var values = new string[headers.Count];

            for (int i = 0; i < headers.Count; i++)
            {
                values[i] = SerializeMemberValue(GetMemberValue(row, headers[i]));
            }

            return string.Join("\t", values);
        }

        /// <summary>
        /// 플레이 중인 게임의 스킬 테이블 데이터에 편집 내용을 즉시 반영합니다.
        /// 에디터에서 테스트 목적으로 런타임 테이블 값을 갱신할 때 사용됩니다.
        /// </summary>
        /// <param name="row">런타임 테이블에 반영할 스킬 Row입니다.</param>
        private static void UpdateInGameTableInfo(object row)
        {
            if (row == null) return;
            if (!Application.isPlaying) return;
            if (!GGemCo2DSkill.TableLoaderManagerSkill.Instance) return;

            switch (row)
            {
                case StruckTableSkillMonster monsterRow:
                {
                    var liveRow = GGemCo2DSkill.TableLoaderManagerSkill.Instance.TableSkillMonster.GetDataByUid(monsterRow.Uid);
                    if (liveRow == null) return;
                    TableRowEditorUtility.CopyMembers(monsterRow, liveRow, RowEditorFieldsMonster);
                    break;
                }
                case StruckTableSkill playerRow:
                {
                    var liveRow = GGemCo2DSkill.TableLoaderManagerSkill.Instance.TableSkill.GetDataByUid(playerRow.Uid);
                    if (liveRow == null) return;
                    TableRowEditorUtility.CopyMembers(playerRow, liveRow, RowEditorFieldsPlayer);
                    break;
                }
            }
        }

        /// <summary>
        /// 현재 선택된 데이터를 기준으로 원본 Row와 편집용 Row를 다시 캐시합니다.
        /// </summary>
        private void CacheRow()
        {
            _cachedRow = null;
            _editingRow = null;
            _editingDirty = false;

            int selectedUid = GetSelectedUid();
            if (selectedUid <= 0)
                return;
            if (!TryGetCurrentRowByUid(selectedUid, out var row) || row == null)
                return;

            _cachedRow = row;
            _editingRow = CloneRow(row);
        }

        /// <summary>
        /// 지정한 Row의 얕은 복사본을 생성합니다.
        /// </summary>
        /// <param name="row">복사할 원본 Row입니다.</param>
        /// <returns>편집에 사용할 수 있는 Row 복사본입니다.</returns>
        private static object CloneRow(object row)
        {
            return row switch
            {
                StruckTableSkill player => TableRowEditorUtility.CloneShallow<StruckTableSkill>(player),
                StruckTableSkillMonster monster => TableRowEditorUtility.CloneShallow<StruckTableSkillMonster>(monster),
                _ => null,
            };
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

            switch (_editingRow)
            {
                case StruckTableSkill editingPlayerRow when _cachedRow is StruckTableSkill cachedPlayerRow:
                    TableRowEditorUtility.CopyMembers(editingPlayerRow, cachedPlayerRow, RowEditorFieldsPlayer);
                    return true;

                case StruckTableSkillMonster editingMonsterRow when _cachedRow is StruckTableSkillMonster cachedMonsterRow:
                    TableRowEditorUtility.CopyMembers(editingMonsterRow, cachedMonsterRow, RowEditorFieldsMonster);
                    return true;
            }

            return false;
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
                case nameof(StruckTableSkill.CastTime):
                case nameof(StruckTableSkill.CoolTime):
                case nameof(StruckTableSkill.CastRange):
                case nameof(StruckTableSkill.PlacementRange):
                case nameof(StruckTableSkill.ChargeGaugeMax):
                case nameof(StruckTableSkill.ChargeGaugeDamagePerHit):
                case nameof(StruckTableSkill.ChargeCompleteDurationSeconds):
                case nameof(StruckTableSkill.ChargeFailDurationSeconds):
                case nameof(StruckTableSkill.UseClipReferenceDurationSeconds):
                    ClampFloatMember(target, memberName);
                    break;
                case nameof(StruckTableSkill.UseClipTimeScale):
                    ClampPositiveFloatMember(target, memberName, 0.001f);
                    break;
                case nameof(StruckTableSkill.MaxTargets):
                case nameof(StruckTableSkill.NeedPlayerLevel):
                case nameof(StruckTableSkill.NeedMp):
                    ClampIntMember(target, memberName);
                    break;
            }
        }

        /// <summary>
        /// 편집 중인 Row 전체에 대해 유효성 보정을 수행합니다.
        /// 현재는 음수가 허용되지 않는 주요 수치 필드를 정규화합니다.
        /// </summary>
        private void NormalizeEditingRow()
        {
            NormalizeEditingFieldValue(_editingRow, nameof(StruckTableSkill.CastTime));
            NormalizeEditingFieldValue(_editingRow, nameof(StruckTableSkill.CoolTime));
            NormalizeEditingFieldValue(_editingRow, nameof(StruckTableSkill.CastRange));
            NormalizeEditingFieldValue(_editingRow, nameof(StruckTableSkill.PlacementRange));
            NormalizeEditingFieldValue(_editingRow, nameof(StruckTableSkill.ChargeGaugeMax));
            NormalizeEditingFieldValue(_editingRow, nameof(StruckTableSkill.ChargeGaugeDamagePerHit));
            NormalizeEditingFieldValue(_editingRow, nameof(StruckTableSkill.ChargeCompleteDurationSeconds));
            NormalizeEditingFieldValue(_editingRow, nameof(StruckTableSkill.ChargeFailDurationSeconds));
            NormalizeEditingFieldValue(_editingRow, nameof(StruckTableSkill.UseClipTimeScale));
            NormalizeEditingFieldValue(_editingRow, nameof(StruckTableSkill.UseClipReferenceDurationSeconds));
            NormalizeEditingFieldValue(_editingRow, nameof(StruckTableSkill.MaxTargets));
            NormalizeEditingFieldValue(_editingRow, nameof(StruckTableSkill.NeedPlayerLevel));
            NormalizeEditingFieldValue(_editingRow, nameof(StruckTableSkill.NeedMp));
        }

        /// <summary>
        /// 멤버 값을 문자열로 직렬화합니다.
        /// </summary>
        private static string SerializeMemberValue(object value)
        {
            return value switch
            {
                null => string.Empty,
                bool boolValue => MathHelper.FormatBool(boolValue),
                float floatValue => MathHelper.FormatFloat(floatValue),
                double doubleValue => MathHelper.FormatFloat((float)doubleValue),
                Enum enumValue => enumValue.ToString(),
                _ => value.ToString() ?? string.Empty,
            };
        }

        /// <summary>
        /// Row의 멤버 값을 읽습니다.
        /// </summary>
        private static object GetMemberValue(object row, string memberName)
        {
            if (row == null || string.IsNullOrWhiteSpace(memberName))
                return null;

            var type = row.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanRead)
                return property.GetValue(row);

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
                return field.GetValue(row);

            return null;
        }

        /// <summary>
        /// float 멤버를 0 이상으로 보정합니다.
        /// </summary>
        private static void ClampFloatMember(object target, string memberName)
        {
            var rawValue = GetMemberValue(target, memberName);
            if (rawValue == null)
                return;

            float value = Convert.ToSingle(rawValue);
            if (value >= 0f)
                return;

            SetMemberValue(target, memberName, 0f);
        }

        /// <summary>
        /// float 멤버를 지정한 최소 양수 이상으로 보정합니다.
        /// </summary>
        /// <param name="target">보정 대상 객체입니다.</param>
        /// <param name="memberName">보정할 멤버 이름입니다.</param>
        /// <param name="minValue">허용할 최소값입니다.</param>
        private static void ClampPositiveFloatMember(object target, string memberName, float minValue)
        {
            var rawValue = GetMemberValue(target, memberName);
            if (rawValue == null)
                return;

            float value = Convert.ToSingle(rawValue);
            if (value >= minValue)
                return;

            SetMemberValue(target, memberName, minValue);
        }

        /// <summary>
        /// int 멤버를 0 이상으로 보정합니다.
        /// </summary>
        private static void ClampIntMember(object target, string memberName)
        {
            var rawValue = GetMemberValue(target, memberName);
            if (rawValue == null)
                return;

            int value = Convert.ToInt32(rawValue);
            if (value >= 0)
                return;

            SetMemberValue(target, memberName, 0);
        }

        /// <summary>
        /// Row의 멤버 값을 설정합니다.
        /// </summary>
        private static void SetMemberValue(object row, string memberName, object value)
        {
            if (row == null || string.IsNullOrWhiteSpace(memberName))
                return;

            var type = row.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (property != null && property.CanWrite)
            {
                property.SetValue(row, value);
                return;
            }

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(row, value);
            }
        }
    }
}
