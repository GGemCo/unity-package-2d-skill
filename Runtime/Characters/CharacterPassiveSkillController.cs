using System;
using System.Collections.Generic;
using Config;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 패시브 스킬(장착형)을 CharacterStat(CharacterTotals)에 상시 반영하는 컨트롤러.
    /// - 매 프레임 유지가 아니라, 장착/해제/레벨 변경 시점에만 전체 리빌드하는 정책이다.
    /// - UI/세이브 시스템은 프로젝트 정책에 따라 외부에서 장착 목록을 전달한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class CharacterPassiveSkillController : MonoBehaviour
    {
        private CharacterBase _character;
        private readonly HashSet<int> _appliedAffects = new();

        /// <summary>
        /// 현재 장착된 패시브 목록(스킬 UID -> 레벨).
        /// - 외부 시스템(UI/세이브)이 관리하며, 변경 시 <see cref="ApplyEquippedPassives"/>를 호출한다.
        /// </summary>
        public IReadOnlyDictionary<int, int> EquippedPassives => _equippedPassives;

        private readonly Dictionary<int, int> _equippedPassives = new();

        private void Awake()
        {
            _character = GetComponent<CharacterBase>();
            if (_character == null)
            {
                Debug.LogError($"{nameof(CharacterPassiveSkillController)} requires {nameof(CharacterBase)}.");
            }
        }

        protected virtual void Start()
        {
        }

        /// <summary>
        /// 세이브 데이터에 저장된 패시브 장착 정보를 다시 읽어서 적용합니다.
        /// - 내부적으로 <see cref="SkillPackageManager"/>의 <see cref="SaveDataManagerSkill"/>을 참조합니다.
        /// </summary>
        public virtual void RefreshFromSaveData()
        {
        }

        /// <summary>
        /// 장착된 패시브 목록을 교체하고 즉시 적용한다.
        /// </summary>
        public void ApplyEquippedPassives(Dictionary<int, int> skillUidToLevel)
        {
            _equippedPassives.Clear();
            if (skillUidToLevel != null)
            {
                foreach (var kv in skillUidToLevel)
                {
                    if (kv.Key <= 0) continue;
                    _equippedPassives[kv.Key] = Mathf.Max(1, kv.Value);
                }
            }

            Rebuild();
        }

        /// <summary>
        /// 현재 장착된 패시브를 모두 제거한다.
        /// </summary>
        public void Clear()
        {
            _equippedPassives.Clear();
            Rebuild();
        }
        
        private PassiveTempHpApplyMode ResolveApplyMode(PassiveTempHpApplyMode requested)
        {
            if (requested != PassiveTempHpApplyMode.UsePolicy)
                return requested;

            var settings = AddressableLoaderSettings.Instance.playerSettings;
            if (settings == null)
                return PassiveTempHpApplyMode.KeepCurrent;

            return AddressableLoaderSettings.Instance.playerSettings.PassiveTempHpApplyPolicy switch
            {
                PassiveTempHpApplyPolicy.FillDelta => PassiveTempHpApplyMode.FillDelta,
                _ => PassiveTempHpApplyMode.KeepCurrent
            };
        }

        public void Rebuild()
        {
            Rebuild(PassiveTempHpApplyMode.UsePolicy);
        }
        /// <summary>
        /// 현재 장착된 패시브 목록을 기준으로 Stat/Affect 를 전체 재구성한다.
        /// </summary>
        private void Rebuild(PassiveTempHpApplyMode applyMode)
        {
            if (_character == null) return;
            if (TableLoaderManagerSkill.Instance == null) return;

            long beforePassiveTempMax = _character.GetPassiveBonusHpTempMax();
            long beforePassiveTempCurrent = _character.GetPassiveBonusHpTempCurrent();

            // 1) 기존 적용분 제거
            _character.ClearPassiveSkillModifiers(recalculate: false);
            SyncAffects(desired: null);

            // 2) 새로 계산
            var flat = new Dictionary<string, int>(32);
            var percent = new Dictionary<string, float>(32);
            var desiredAffects = new HashSet<int>();

            var tableSkillPassive = TableLoaderManagerSkill.Instance.TableSkillPassive;
            var tableOption = TableLoaderManagerSkill.Instance.TableSkillPassiveOption;

            foreach (var kv in _equippedPassives)
            {
                int skillUid = kv.Key;
                int level = kv.Value;

                var skillRow = tableSkillPassive.GetDataByUid(skillUid);
                if (skillRow == null) continue;
                if (skillRow.SkillKind != ConfigCommonSkill.SkillKind.Passive) continue;

                var groupUid = skillRow.OptionGroupUid;
                if (groupUid <= 0) continue;

                var options = tableOption.GetOptions(groupUid, level);
                if (options == null || options.Count == 0) continue;

                for (int i = 0; i < options.Count; i++)
                {
                    var op = options[i];
                    if (op == null || !op.IsValid) continue;

                    switch (op.Kind)
                    {
                        case SkillOptionKind.Stat:
                            ApplyStatOption(flat, percent, op);
                            break;

                        case SkillOptionKind.Affect:
                            if (TryParseIntId(op.TargetId, out var affectUid) && affectUid > 0)
                                desiredAffects.Add(affectUid);
                            break;
                    }
                }
            }

            // 3) modifier 적용
            _character.SetPassiveSkillModifiers(flat, percent, recalculate: false);
            SyncAffects(desiredAffects);
            _character.RecalculateStats();

            // 4) 패시브 임시 HP 최대치 동기화
            _character.SyncPassiveBonusHpTempMaxFromProvider();

            long afterPassiveTempMax = _character.GetPassiveBonusHpTempMax();
            long delta = afterPassiveTempMax - beforePassiveTempMax;

            var resolvedMode = ResolveApplyMode(applyMode);

            if (delta > 0)
            {
                switch (resolvedMode)
                {
                    case PassiveTempHpApplyMode.KeepCurrent:
                        break;

                    case PassiveTempHpApplyMode.FillDelta:
                        _character.AddPassiveBonusHpTempCurrent(delta);
                        break;

                    case PassiveTempHpApplyMode.FillToMax:
                        _character.FillPassiveBonusHpTempToMax();
                        break;
                }
            }
            else if (delta < 0)
            {
                // 감소했으면 현재치 클램프
                long nextCurrent = Math.Min(beforePassiveTempCurrent, afterPassiveTempMax);
                _character.SetPassiveBonusHpTempCurrent(nextCurrent);
            }
        }
        public void RebuildUsingPolicy()
        {
            Rebuild(PassiveTempHpApplyMode.UsePolicy);
        }

        public void RebuildKeepingCurrentPassiveTempHp()
        {
            Rebuild(PassiveTempHpApplyMode.KeepCurrent);
        }

        public void RebuildAndFillPassiveTempHpDelta()
        {
            Rebuild(PassiveTempHpApplyMode.FillDelta);
        }

        public void RebuildAndFillPassiveTempHpToMax()
        {
            Rebuild(PassiveTempHpApplyMode.FillToMax);
        }
        
        public void Rebuild_bak()
        {
            if (_character == null) return;
            if (TableLoaderManagerSkill.Instance == null) return;

            // 1) 기존 적용분 제거
            _character.ClearPassiveSkillModifiers(recalculate: false);
            SyncAffects(desired: null); // remove all

            // 2) 새로 계산
            var flat = new Dictionary<string, int>(32);
            var percent = new Dictionary<string, float>(32);
            var desiredAffects = new HashSet<int>();

            var tableSkillPassive = TableLoaderManagerSkill.Instance.TableSkillPassive;
            var tableOption = TableLoaderManagerSkill.Instance.TableSkillPassiveOption;

            foreach (var kv in _equippedPassives)
            {
                int skillUid = kv.Key;
                int level = kv.Value;

                var skillRow = tableSkillPassive.GetDataByUid(skillUid);
                if (skillRow == null) continue;
                if (skillRow.SkillKind != ConfigCommonSkill.SkillKind.Passive) continue;

                var groupUid = skillRow.OptionGroupUid;
                if (groupUid <= 0)
                {
                    // 데이터 누락 시 안전하게 스킵
                    continue;
                }

                var options = tableOption.GetOptions(groupUid, level);
                if (options == null || options.Count == 0) continue;

                for (int i = 0; i < options.Count; i++)
                {
                    var op = options[i];
                    if (op == null || !op.IsValid) continue;

                    switch (op.Kind)
                    {
                        case SkillOptionKind.Stat:
                            ApplyStatOption(flat, percent, op);
                            break;

                        case SkillOptionKind.Affect:
                            if (TryParseIntId(op.TargetId, out var affectUid) && affectUid > 0)
                                desiredAffects.Add(affectUid);
                            break;
                    }
                }
            }

            // 3) 적용(배치 재계산 1회)
            _character.SetPassiveSkillModifiers(flat, percent, recalculate: false);
            SyncAffects(desiredAffects);

            _character.RecalculateStats();
        }

        private static void ApplyStatOption(Dictionary<string, int> flat, Dictionary<string, float> percent,
            StruckTableSkillPassiveOption op)
        {
            if (string.IsNullOrEmpty(op.TargetId)) return;

            // 정책:
            // - Plus/Minus: flat(정수)
            // - Increase/Decrease: percent(퍼센트 포인트). 예) +5%면 Value=5
            // - None: Plus로 간주(레거시 호환)
            switch (op.Op)
            {
                case ConfigCommon.SuffixType.Plus:
                    flat[op.TargetId] = flat.GetValueOrDefault(op.TargetId, 0) + (int)op.Value;
                    break;

                case ConfigCommon.SuffixType.Minus:
                    flat[op.TargetId] = flat.GetValueOrDefault(op.TargetId, 0) - (int)op.Value;
                    break;

                case ConfigCommon.SuffixType.Increase:
                    percent[op.TargetId] = percent.GetValueOrDefault(op.TargetId, 0f) + op.Value;
                    break;

                case ConfigCommon.SuffixType.Decrease:
                    percent[op.TargetId] = percent.GetValueOrDefault(op.TargetId, 0f) - op.Value;
                    break;

                case ConfigCommon.SuffixType.None:
                default:
                    flat[op.TargetId] = flat.GetValueOrDefault(op.TargetId, 0) + (int)op.Value;
                    break;
            }
        }

        private void SyncAffects(HashSet<int> desired)
        {
            desired ??= new HashSet<int>();

            // remove
            var toRemove = new List<int>();
            foreach (var uid in _appliedAffects)
            {
                if (!desired.Contains(uid))
                    toRemove.Add(uid);
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                _character.RemoveAffect(toRemove[i]);
                _appliedAffects.Remove(toRemove[i]);
            }

            // apply
            foreach (var uid in desired)
            {
                if (_appliedAffects.Contains(uid)) continue;
                // duration 0: Affect 테이블 정책에 따름(상시/착용형은 Affect 쪽 규칙으로 처리)
                // 패시브 해제 시 RemoveAffect로 동기화한다.
                // CharacterStat.ApplyAffect는 protected라 RemoveAffect만 public이므로 브리지 사용.
                // AffectRuntimeBridge.ApplyAffect(gameObject, uid, 0);
                AffectApi.Apply(gameObject, uid);
                _appliedAffects.Add(uid);
            }
        }

        private static bool TryParseIntId(string v, out int id)
        {
            id = 0;
            if (string.IsNullOrEmpty(v)) return false;
            return int.TryParse(v, out id);
        }
    }
}