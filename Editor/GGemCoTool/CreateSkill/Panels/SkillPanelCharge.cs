using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Config;
using GGemCo2DCore;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// CreateSkillWindow의 차징 단계 편집 패널입니다.
    /// 선택된 스킬에 연결된 skill_charge_stage Row를 추가/편집/저장하고, 플레이 모드에서 피격 시뮬레이션을 수행합니다.
    /// </summary>
    public partial class CreateSkillWindow
    {
        private bool _foldCharge = true;
        private int _selectedChargeStageUid;
        private StruckTableSkillChargeStage _editingChargeStage;
        private bool _chargeStageDirty;

        private static readonly TableRowEditorUtility.TableRowEditorField[] ChargeStageEditorFields =
            TableRowEditorUtility.BuildFields<StruckTableSkillChargeStage>(BuildChargeStageEditorOptions());

        private static TableRowEditorUtility.TableRowEditorBuildOptions BuildChargeStageEditorOptions()
        {
            var options = new TableRowEditorUtility.TableRowEditorBuildOptions();
            options.ReadOnlyMembers.Add(nameof(StruckTableSkillChargeStage.Uid));
            options.ReadOnlyMembers.Add(nameof(StruckTableSkillChargeStage.SkillUid));
            options.ReadOnlyMembers.Add(nameof(StruckTableSkillChargeStage.OwnerType));
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.StageIndex)] = "Stage Index";
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.StartClip)] = "Start Animation Clip";
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.StartDurationSeconds)] = "Start Duration Seconds";
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.DurationSeconds)] = "Loop Duration Seconds";
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.LoopClip)] = "Loop Animation Clip";
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.EndClip)] = "End Animation Clip";
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.EndDurationSeconds)] = "End Duration Seconds";
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.EnterSoundUid)] = "Enter Sound Uid";
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.LoopSoundUid)] = "Loop Sound Uid";
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.LoopSoundLoop)] = "Loop Sound Loop";
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.VfxUid)] = "VFX Uid";
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.VfxFollowMode)] = "VFX Follow Mode";
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.VfxFollowAnchorMode)] = "VFX Follow Anchor Mode";
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.VfxPositionYType)] = "VFX Position Y Type";
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.VfxPositionY)] = "VFX Position Y";
            options.LabelByMemberName[nameof(StruckTableSkillChargeStage.VfxScale)] = "VFX Scale";
            return options;
        }

        /// <summary>
        /// 차징 단계 편집 패널을 그립니다.
        /// </summary>
        private void OnGUICharge()
        {
            _foldCharge = EditorGUILayout.Foldout(_foldCharge, "차징 설정 / 단계", true);
            if (!_foldCharge)
                return;

            using (new EditorGUILayout.VerticalScope("box"))
            {
                int skillUid = GetSelectedUid();
                if (skillUid <= 0)
                {
                    EditorGUILayout.HelpBox("스킬을 먼저 선택하면 차징 단계를 편집할 수 있습니다.", MessageType.Info);
                    return;
                }

                if (_tableSkillChargeStage == null)
                {
                    EditorGUILayout.HelpBox("skill_charge_stage 테이블을 로드하지 못했습니다. 테이블 파일과 Addressables 등록을 확인해주세요.", MessageType.Warning);
                    if (GUILayout.Button("차징 단계 테이블 다시 로드", EditorConstants.GUILayoutButtonHeight22))
                    {
                        ReloadChargeStageTable();
                    }
                    return;
                }

                DrawChargeSummary(skillUid);
                EditorGUILayout.Space(4);
                DrawChargeStageList(skillUid);
                EditorGUILayout.Space(6);
                DrawChargeStageEditor(skillUid);
                EditorGUILayout.Space(6);
                DrawChargeRuntimeTestButtons();
            }
        }

        private void DrawChargeSummary(int skillUid)
        {
            bool useCharge = GetCurrentUseChargeValue();
            float gaugeMax = GetCurrentChargeGaugeMaxValue();
            float gaugeDamage = GetCurrentChargeGaugeDamagePerHitValue();
            SkillChargeIncomingHitPolicy incomingHitPolicy =
                GetCurrentChargeIncomingHitPolicyValue();
            int stageCount = GetChargeStagesForCurrentSkill(skillUid).Count;

            EditorGUILayout.LabelField("선택 스킬", $"{skillUid} / {GetSelectedDisplayName()}");
            EditorGUILayout.LabelField("UseCharge", useCharge ? "Y" : "N");
            EditorGUILayout.LabelField("Gauge", $"Max={gaugeMax:0.###}, Damage Per Hit={gaugeDamage:0.###}");
            EditorGUILayout.LabelField("Incoming Hit Policy", incomingHitPolicy.ToString());
            EditorGUILayout.LabelField("Stage Count", stageCount.ToString());

            if (!useCharge)
                EditorGUILayout.HelpBox("스킬 Row의 UseCharge가 꺼져 있습니다. 차징 단계를 만들어도 런타임에서는 기존 방식으로 즉시 실행됩니다.", MessageType.Info);
            if (useCharge && stageCount <= 0)
                EditorGUILayout.HelpBox("UseCharge는 켜져 있지만 연결된 차징 단계가 없습니다. skill_charge_stage Row를 추가해주세요.", MessageType.Warning);
        }

        private void DrawChargeStageList(int skillUid)
        {
            var stages = GetChargeStagesForCurrentSkill(skillUid);
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("단계 목록", EditorStyles.boldLabel);
                if (GUILayout.Button("테이블 다시 로드", GUILayout.Width(110)))
                    ReloadChargeStageTable();
                if (GUILayout.Button("단계 추가", GUILayout.Width(90)))
                    CreateNewChargeStage(skillUid, stages);
            }

            if (stages.Count <= 0)
            {
                EditorGUILayout.HelpBox("현재 스킬에 연결된 차징 단계가 없습니다.", MessageType.Info);
                return;
            }

            if (_selectedChargeStageUid <= 0 || stages.All(row => row.Uid != _selectedChargeStageUid))
            {
                SelectChargeStage(stages[0]);
            }

            for (int i = 0; i < stages.Count; i++)
            {
                var row = stages[i];
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool selected = row.Uid == _selectedChargeStageUid;
                    string label = $"#{row.StageIndex}  Uid:{row.Uid}  Start:{row.StartClip}  Loop:{row.LoopClip}({row.DurationSeconds:0.###}s)  End:{row.EndClip}";
                    if (GUILayout.Toggle(selected, label, "Button"))
                    {
                        if (!selected)
                            SelectChargeStage(row);
                    }
                }
            }
        }

        private void DrawChargeStageEditor(int skillUid)
        {
            if (_editingChargeStage == null)
            {
                EditorGUILayout.HelpBox("편집할 차징 단계를 선택하거나 새 단계를 추가해주세요.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("단계 편집", EditorStyles.boldLabel);
            var result = TableRowEditorUtility.DrawObjectEditor(_editingChargeStage, ChargeStageEditorFields, NormalizeChargeStageFieldValue);
            if (result.Changed)
                _chargeStageDirty = true;

            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUI.DisabledScope(!_chargeStageDirty))
                {
                    if (GUILayout.Button("되돌리기"))
                    {
                        if (TryFindChargeStageByUid(_selectedChargeStageUid, out var source))
                            SelectChargeStage(source);
                    }

                    if (GUILayout.Button("테스트 적용"))
                    {
                        ApplyChargeStageToRuntime(_editingChargeStage);
                        ShowNotification(new GUIContent("차징 단계 테스트 적용"));
                    }

                    if (GUILayout.Button("저장(테이블 파일)"))
                    {
                        if (!TrySaveChargeStageTableFile(out string error))
                        {
                            EditorUtility.DisplayDialog(Title, error, "OK");
                            return;
                        }

                        ReloadChargeStageTable();
                        if (TryFindChargeStageByUid(_selectedChargeStageUid, out var reloaded))
                            SelectChargeStage(reloaded);

                        ApplyChargeStageToRuntime(_editingChargeStage);
                        ShowNotification(new GUIContent("차징 단계 저장 완료"));
                    }
                }
            }
        }

        private void DrawChargeRuntimeTestButtons()
        {
            using (new EditorGUI.DisabledScope(!Application.isPlaying || selectedCharacter == null))
            {
                if (GUILayout.Button("차징 피격 시뮬레이션", EditorConstants.GUILayoutButtonHeight22))
                {
                    var executor = selectedCharacter.GetComponent<SkillExecutor>() ??
                                   selectedCharacter.GetComponentInChildren<SkillExecutor>();
                    if (executor == null)
                    {
                        ShowNotification(new GUIContent("SkillExecutor 없음"));
                        return;
                    }

                    bool consumed = executor.TryApplyIncomingHitToChargeGauge(SkillCancelReason.Damage);
                    ShowNotification(new GUIContent(consumed ? "차징 게이지 감소" : "차징 중이 아님"));
                }
            }
        }

        private List<StruckTableSkillChargeStage> GetChargeStagesForCurrentSkill(int skillUid)
        {
            if (_tableSkillChargeStage == null || skillUid <= 0)
                return new List<StruckTableSkillChargeStage>();

            return _tableSkillChargeStage.GetStageList(skillUid, GetCurrentOwnerType());
        }

        private ConfigCommonSkill.SkillOwnerType GetCurrentOwnerType()
        {
            return _selectedSource == ConfigCommon.SkillTableSource.Monster
                ? ConfigCommonSkill.SkillOwnerType.Monster
                : ConfigCommonSkill.SkillOwnerType.Player;
        }

        private bool GetCurrentUseChargeValue()
        {
            if (_editingRow is StruckTableSkill editingPlayer)
                return editingPlayer.UseCharge;
            if (_editingRow is StruckTableSkillMonster editingMonster)
                return editingMonster.UseCharge;
            if (_selectedData is StruckTableSkill selectedPlayer)
                return selectedPlayer.UseCharge;
            if (_selectedData is StruckTableSkillMonster selectedMonster)
                return selectedMonster.UseCharge;
            return false;
        }

        private float GetCurrentChargeGaugeMaxValue()
        {
            if (_editingRow is StruckTableSkill editingPlayer)
                return editingPlayer.ChargeGaugeMax;
            if (_editingRow is StruckTableSkillMonster editingMonster)
                return editingMonster.ChargeGaugeMax;
            if (_selectedData is StruckTableSkill selectedPlayer)
                return selectedPlayer.ChargeGaugeMax;
            if (_selectedData is StruckTableSkillMonster selectedMonster)
                return selectedMonster.ChargeGaugeMax;
            return 0f;
        }

        private float GetCurrentChargeGaugeDamagePerHitValue()
        {
            if (_editingRow is StruckTableSkill editingPlayer)
                return editingPlayer.ChargeGaugeDamagePerHit;
            if (_editingRow is StruckTableSkillMonster editingMonster)
                return editingMonster.ChargeGaugeDamagePerHit;
            if (_selectedData is StruckTableSkill selectedPlayer)
                return selectedPlayer.ChargeGaugeDamagePerHit;
            if (_selectedData is StruckTableSkillMonster selectedMonster)
                return selectedMonster.ChargeGaugeDamagePerHit;
            return 0f;
        }

        /// <summary>
        /// 현재 선택하거나 편집 중인 스킬의 차징 피격 피해 처리 정책을 반환합니다.
        /// </summary>
        /// <returns>현재 차징 피격 피해 처리 정책입니다.</returns>
        private SkillChargeIncomingHitPolicy GetCurrentChargeIncomingHitPolicyValue()
        {
            if (_editingRow is StruckTableSkill editingPlayer)
                return editingPlayer.ChargeIncomingHitPolicy;
            if (_editingRow is StruckTableSkillMonster editingMonster)
                return editingMonster.ChargeIncomingHitPolicy;
            if (_selectedData is StruckTableSkill selectedPlayer)
                return selectedPlayer.ChargeIncomingHitPolicy;
            if (_selectedData is StruckTableSkillMonster selectedMonster)
                return selectedMonster.ChargeIncomingHitPolicy;
            return SkillChargeIncomingHitPolicy.DamageAndGauge;
        }

        private void SelectChargeStage(StruckTableSkillChargeStage row)
        {
            if (row == null)
                return;

            _selectedChargeStageUid = row.Uid;
            _editingChargeStage = TableRowEditorUtility.CloneShallow(row);
            _chargeStageDirty = false;
        }

        private void CreateNewChargeStage(int skillUid, IReadOnlyList<StruckTableSkillChargeStage> currentStages)
        {
            int nextUid = 1;
            if (_tableSkillChargeStage != null && _tableSkillChargeStage.GetDatas().Count > 0)
                nextUid = _tableSkillChargeStage.GetDatas().Keys.Max() + 1;

            int nextStageIndex = currentStages != null && currentStages.Count > 0
                ? currentStages.Max(row => row.StageIndex) + 1
                : 1;

            var row = new StruckTableSkillChargeStage
            {
                Uid = nextUid,
                SkillUid = skillUid,
                OwnerType = GetCurrentOwnerType(),
                StageIndex = nextStageIndex,
                StartClip = string.Empty,
                StartDurationSeconds = 0f,
                DurationSeconds = 0.5f,
                LoopClip = string.Empty,
                EndClip = string.Empty,
                EndDurationSeconds = 0f,
                EnterSoundUid = 0,
                LoopSoundUid = 0,
                LoopSoundLoop = true,
                VfxUid = 0,
                VfxFollowMode = VfxConstants.FollowMode.Position,
                VfxFollowAnchorMode = VfxConstants.FollowAnchorMode.FollowTargetOrigin,
                VfxPositionYType = ConfigCommon.PositionYType.None,
                VfxPositionY = 0f,
                VfxScale = 0f,
                Memo = string.Empty,
            };

            _selectedChargeStageUid = row.Uid;
            _editingChargeStage = row;
            _chargeStageDirty = true;
        }

        private bool TryFindChargeStageByUid(int uid, out StruckTableSkillChargeStage row)
        {
            row = null;
            if (_tableSkillChargeStage == null || uid <= 0)
                return false;

            return _tableSkillChargeStage.GetDatas().TryGetValue(uid, out row) && row != null;
        }

        private void NormalizeChargeStageFieldValue(object target, string memberName)
        {
            if (target is not StruckTableSkillChargeStage row || string.IsNullOrWhiteSpace(memberName))
                return;

            switch (memberName)
            {
                case nameof(StruckTableSkillChargeStage.StageIndex):
                    row.StageIndex = Mathf.Max(0, row.StageIndex);
                    break;
                case nameof(StruckTableSkillChargeStage.StartDurationSeconds):
                    row.StartDurationSeconds = Mathf.Max(0f, row.StartDurationSeconds);
                    break;
                case nameof(StruckTableSkillChargeStage.DurationSeconds):
                    row.DurationSeconds = Mathf.Max(0f, row.DurationSeconds);
                    break;
                case nameof(StruckTableSkillChargeStage.EndDurationSeconds):
                    row.EndDurationSeconds = Mathf.Max(0f, row.EndDurationSeconds);
                    break;
                case nameof(StruckTableSkillChargeStage.EnterSoundUid):
                    row.EnterSoundUid = Mathf.Max(0, row.EnterSoundUid);
                    break;
                case nameof(StruckTableSkillChargeStage.LoopSoundUid):
                    row.LoopSoundUid = Mathf.Max(0, row.LoopSoundUid);
                    break;
                case nameof(StruckTableSkillChargeStage.VfxUid):
                    row.VfxUid = Mathf.Max(0, row.VfxUid);
                    break;
                case nameof(StruckTableSkillChargeStage.VfxScale):
                    row.VfxScale = Mathf.Max(0f, row.VfxScale);
                    break;
            }
        }

        private void ReloadChargeStageTable()
        {
            TableLoaderManagerBase.Unload(ConfigAddressableTableSkill.TableSkillChargeStage.Path);
            _tableSkillChargeStage = TableLoaderManagerSkill.LoadTableSkillChargeStage();
            _editingChargeStage = null;
            _chargeStageDirty = false;
            Repaint();
        }

        private bool TrySaveChargeStageTableFile(out string error)
        {
            error = null;
            if (_editingChargeStage == null)
            {
                error = "저장할 차징 단계 Row가 없습니다.";
                return false;
            }

            NormalizeChargeStageRow(_editingChargeStage);

            if (!EnsureChargeStageTableFileExists(out error))
                return false;

            if (!TableTextRowPatchUtility.TryPatchRowByUid(
                    ConfigAddressableTableSkill.TableSkillChargeStage.Path,
                    _editingChargeStage.Uid,
                    _editingChargeStage,
                    SerializeRow,
                    out error))
            {
                error = $"차징 단계 테이블 저장 중 오류: {error}";
                return false;
            }

            _chargeStageDirty = false;
            return true;
        }

        private static bool EnsureChargeStageTableFileExists(out string error)
        {
            error = null;
            string assetPath = ConfigAddressableTableSkill.TableSkillChargeStage.Path;
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.Combine(projectRoot ?? string.Empty, assetPath);

            if (File.Exists(fullPath))
                return true;

            try
            {
                string directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory))
                    Directory.CreateDirectory(directory);

                const string header = "Uid	SkillUid	OwnerType	StageIndex	StartClip	StartDurationSeconds	DurationSeconds	LoopClip	EndClip	EndDurationSeconds	EnterSoundUid	LoopSoundUid	LoopSoundLoop	VfxUid	VfxFollowMode	VfxFollowAnchorMode	VfxPositionYType	VfxPositionY	VfxScale	Memo";
                File.WriteAllText(fullPath, header + "\n", new UTF8Encoding(false));
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh();
                return true;
            }
            catch (Exception e)
            {
                error = $"차징 단계 테이블 파일 생성 중 오류: {e.Message}";
                return false;
            }
        }

        private void NormalizeChargeStageRow(StruckTableSkillChargeStage row)
        {
            if (row == null)
                return;

            row.SkillUid = GetSelectedUid();
            row.OwnerType = GetCurrentOwnerType();
            row.StageIndex = Mathf.Max(0, row.StageIndex);
            row.StartDurationSeconds = Mathf.Max(0f, row.StartDurationSeconds);
            row.DurationSeconds = Mathf.Max(0f, row.DurationSeconds);
            row.EndDurationSeconds = Mathf.Max(0f, row.EndDurationSeconds);
            row.EnterSoundUid = Mathf.Max(0, row.EnterSoundUid);
            row.LoopSoundUid = Mathf.Max(0, row.LoopSoundUid);
            row.VfxUid = Mathf.Max(0, row.VfxUid);
            row.VfxScale = Mathf.Max(0f, row.VfxScale);
        }

        private static void ApplyChargeStageToRuntime(StruckTableSkillChargeStage row)
        {
            if (row == null)
                return;
            if (!Application.isPlaying)
                return;
            if (!GGemCo2DSkill.TableLoaderManagerSkill.Instance)
                return;

            var liveTable = GGemCo2DSkill.TableLoaderManagerSkill.Instance.TableSkillChargeStage;
            if (liveTable == null)
                return;

            var liveRow = TableRowEditorUtility.CloneShallow(row);
            liveTable.UpsertRuntimeRow(liveRow);
        }
    }
}
