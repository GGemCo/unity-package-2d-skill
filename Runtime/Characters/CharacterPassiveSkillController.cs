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

        /// <summary>
        /// 현재 장착된 패시브 슬롯 단위 목록입니다.
        /// 같은 패시브 UID가 여러 번 들어오면 엔트리도 여러 개 유지됩니다.
        /// </summary>
        public IReadOnlyList<PassiveSkillLoadoutEntry> EquippedPassiveEntries => _equippedPassiveEntries;

        private readonly Dictionary<int, int> _equippedPassives = new();
        private readonly List<PassiveSkillLoadoutEntry> _equippedPassiveEntries = new();

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
            _equippedPassiveEntries.Clear();
            if (skillUidToLevel != null)
            {
                foreach (var kv in skillUidToLevel)
                {
                    if (kv.Key <= 0) continue;
                    int level = Mathf.Max(1, kv.Value);
                    _equippedPassives[kv.Key] = level;
                    _equippedPassiveEntries.Add(new PassiveSkillLoadoutEntry(kv.Key, level));
                }
            }

            Rebuild();
        }

        /// <summary>
        /// 슬롯 단위 패시브 장착 목록을 교체하고 즉시 적용합니다.
        /// 같은 패시브 UID가 여러 번 전달되면 장착 횟수만큼 Stat 옵션을 누적합니다.
        /// </summary>
        /// <param name="entries">슬롯 단위 패시브 장착 목록입니다.</param>
        public void ApplyEquippedPassiveStacks(IReadOnlyList<PassiveSkillLoadoutEntry> entries)
        {
            _equippedPassives.Clear();
            _equippedPassiveEntries.Clear();

            if (entries != null)
            {
                for (int i = 0; i < entries.Count; i++)
                {
                    PassiveSkillLoadoutEntry entry = entries[i];
                    if (entry.SkillUid <= 0)
                    {
                        continue;
                    }

                    int level = Mathf.Max(1, entry.Level);
                    _equippedPassiveEntries.Add(new PassiveSkillLoadoutEntry(entry.SkillUid, level));

                    // 기존 조회 API는 UID별 대표 레벨만 표현할 수 있으므로 가장 높은 레벨을 유지합니다.
                    if (!_equippedPassives.TryGetValue(entry.SkillUid, out int currentLevel) ||
                        currentLevel < level)
                    {
                        _equippedPassives[entry.SkillUid] = level;
                    }
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
            _equippedPassiveEntries.Clear();
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

        private void Rebuild()
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

            // 패시브 리빌드 중에는 TotalHpTemp/CurrentHpTemp 관련 Publish 타이밍을 하나로 묶는다.
            // segmented HUD는 CharacterStat 내부의 패시브/런타임 Temp 캐시를 직접 참조하므로,
            // RecalculateStats()가 먼저 발행되고 SyncPassiveBonusHpTempMaxFromProvider()가 나중에 호출되면
            // HUD가 이전 패시브 Temp 값을 읽어 임시 하트가 남아 보일 수 있다.
            // 따라서 리빌드 전체를 batch 구간으로 감싸고, 패시브 Temp 동기화/클램프까지 끝난 뒤 한 번만 publish 한다.
            using var batchUpdate = _character.BeginBatchUpdate();

            // 1) 기존 적용분 제거
            _character.ClearPassiveSkillModifiers(recalculate: false);
            SyncAffects(desired: null);

            // 2) 새로 계산
            var flat = new Dictionary<string, int>(32);
            var percent = new Dictionary<string, float>(32);
            var desiredAffects = new HashSet<int>();

            var tableSkillPassive = TableLoaderManagerSkill.Instance.TableSkillPassive;
            var tableOption = TableLoaderManagerSkill.Instance.TableSkillPassiveOption;

            for (int entryIndex = 0; entryIndex < _equippedPassiveEntries.Count; entryIndex++)
            {
                PassiveSkillLoadoutEntry entry = _equippedPassiveEntries[entryIndex];
                int skillUid = entry.SkillUid;
                int level = entry.Level;

                var skillRow = tableSkillPassive.GetDataByUid(skillUid);
                if (skillRow == null) continue;
                if (skillRow.SkillKind != ConfigCommonSkill.SkillKind.Passive) continue;

                var options = tableOption.GetOptions(skillRow.Uid, level);
                if (options == null || options.Count == 0) continue;

                for (int i = 0; i < options.Count; i++)
                {
                    var op = options[i];
                    if (op == null || !op.IsValid) continue;

                    switch (op.Kind)
                    {
                        case SkillOptionKind.Stat:
                            StatModifierHelper.AccumulateStat(flat, percent,
                                op.TargetId, op.Op, op.Value);
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
                _character.SetCurrentHpTempPassive(nextCurrent);
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
