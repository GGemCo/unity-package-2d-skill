using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 Authoring용 테이블 로드/저장/런타임 동기화를 담당합니다.
    /// </summary>
    public static class SkillAuthoringRepository
    {
        private static readonly string[] PlayerHeaders =
        {
            "Uid", "Name", "Memo", "DefaultLearn", "NeedPlayerLevel", "IconFileName", "SoFileName",
            "CastTime", "CoolTime", "TargetingMode", "Range", "MaxTargets",
            "CastStartClip", "CastLoopClip", "CastEndClip", "UseClip"
        };

        private static readonly string[] MonsterHeaders =
        {
            "Uid", "Memo", "SoFileName", "CastTime", "CoolTime", "TargetingMode", "Range", "MaxTargets",
            "CastStartClip", "CastLoopClip", "CastEndClip", "UseClip"
        };

        public static List<SkillAuthoringModel> LoadSkills(SkillAuthoringTableKind tableKind, bool forceReload)
        {
            var list = new List<SkillAuthoringModel>(128);

            if (tableKind == SkillAuthoringTableKind.Player)
            {
                var table = TableLoaderManagerSkill.LoadTableSkill(forceReload);
                if (table != null)
                {
                    foreach (var kv in table.GetDatas())
                    {
                        if (kv.Value == null) continue;
                        list.Add(SkillAuthoringModel.FromPlayer(kv.Value));
                    }
                }
            }
            else
            {
                var table = TableLoaderManagerSkill.LoadTableSkillMonster(forceReload);
                if (table != null)
                {
                    foreach (var kv in table.GetDatas())
                    {
                        if (kv.Value == null) continue;
                        list.Add(SkillAuthoringModel.FromMonster(kv.Value));
                    }
                }
            }

            list.Sort((a, b) => a.Uid.CompareTo(b.Uid));
            return list;
        }

        public static bool Save(SkillAuthoringTableKind tableKind, List<SkillAuthoringModel> models, out string error)
        {
            error = null;
            try
            {
                var assetInfo = tableKind == SkillAuthoringTableKind.Player
                    ? ConfigAddressableTableSkill.TableSkill
                    : ConfigAddressableTableSkill.TableSkillMonster;

                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                var fullPath = Path.Combine(projectRoot ?? string.Empty, assetInfo.Path);
                var sb = new StringBuilder(1024 * 32);
                sb.AppendLine(string.Join("\t", tableKind == SkillAuthoringTableKind.Player ? PlayerHeaders : MonsterHeaders));

                foreach (var model in models.OrderBy(x => x.Uid))
                {
                    if (model == null) continue;
                    if (tableKind == SkillAuthoringTableKind.Player)
                    {
                        sb.Append(model.Uid).Append('\t');
                        sb.Append(model.Name ?? string.Empty).Append('\t');
                        sb.Append(model.Memo ?? string.Empty).Append('\t');
                        sb.Append(model.DefaultLearn ? "TRUE" : "FALSE").Append('\t');
                        sb.Append(model.NeedPlayerLevel).Append('\t');
                        sb.Append(model.IconFileName ?? string.Empty).Append('\t');
                        sb.Append(model.SoFileName ?? string.Empty).Append('\t');
                        sb.Append(FormatFloat(model.CastTime)).Append('\t');
                        sb.Append(FormatFloat(model.CoolTime)).Append('\t');
                        sb.Append(model.TargetingMode).Append('\t');
                        sb.Append(FormatFloat(model.Range)).Append('\t');
                        sb.Append(model.MaxTargets).Append('\t');
                        sb.Append(model.CastStartClip ?? string.Empty).Append('\t');
                        sb.Append(model.CastLoopClip ?? string.Empty).Append('\t');
                        sb.Append(model.CastEndClip ?? string.Empty).Append('\t');
                        sb.AppendLine(model.UseClip ?? string.Empty);
                    }
                    else
                    {
                        sb.Append(model.Uid).Append('\t');
                        sb.Append(model.Memo ?? string.Empty).Append('\t');
                        sb.Append(model.SoFileName ?? string.Empty).Append('\t');
                        sb.Append(FormatFloat(model.CastTime)).Append('\t');
                        sb.Append(FormatFloat(model.CoolTime)).Append('\t');
                        sb.Append(model.TargetingMode).Append('\t');
                        sb.Append(FormatFloat(model.Range)).Append('\t');
                        sb.Append(model.MaxTargets).Append('\t');
                        sb.Append(model.CastStartClip ?? string.Empty).Append('\t');
                        sb.Append(model.CastLoopClip ?? string.Empty).Append('\t');
                        sb.Append(model.CastEndClip ?? string.Empty).Append('\t');
                        sb.AppendLine(model.UseClip ?? string.Empty);
                    }
                }

                File.WriteAllText(fullPath, sb.ToString(), new UTF8Encoding(false));
                AssetDatabase.ImportAsset(assetInfo.Path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                AssetDatabase.Refresh();
                TableLoaderManagerBase.Unload(assetInfo.Path);
                return true;
            }
            catch (System.Exception e)
            {
                error = $"Skill 테이블 저장 중 오류: {e.Message}";
                return false;
            }
        }

        public static void UpdateInGameSkillTableInfo(SkillAuthoringModel row)
        {
            if (row == null || !Application.isPlaying || !GGemCo2DSkill.TableLoaderManagerSkill.Instance)
                return;

            if (row.TableKind == SkillAuthoringTableKind.Player)
            {
                var table = GGemCo2DSkill.TableLoaderManagerSkill.Instance.TableSkill;
                if (table == null) return;
                var datas = table.GetDatas();
                if (datas == null || !datas.TryGetValue(row.Uid, out var info) || info == null) return;

                info.Name = row.Name;
                info.Memo = row.Memo;
                info.DefaultLearn = row.DefaultLearn;
                info.NeedPlayerLevel = row.NeedPlayerLevel;
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
                info.UseClip = row.UseClip;
            }
            else
            {
                var table = GGemCo2DSkill.TableLoaderManagerSkill.Instance.TableSkillMonster;
                if (table == null) return;
                var datas = table.GetDatas();
                if (datas == null || !datas.TryGetValue(row.Uid, out var info) || info == null) return;

                info.Name = row.Name;
                info.Memo = row.Memo;
                info.SoFileName = row.SoFileName;
                info.CastTime = row.CastTime;
                info.CoolTime = row.CoolTime;
                info.TargetingMode = row.TargetingMode;
                info.Range = row.Range;
                info.MaxTargets = row.MaxTargets;
                info.CastStartClip = row.CastStartClip;
                info.CastLoopClip = row.CastLoopClip;
                info.CastEndClip = row.CastEndClip;
                info.UseClip = row.UseClip;
            }
        }

        private static string FormatFloat(float v) => v.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}
