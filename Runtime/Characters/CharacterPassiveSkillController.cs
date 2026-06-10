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
        private PassiveFormulaVariableProvider _formulaVariableProvider;
        private PassiveMpGainBonusProvider _mpGainBonusProvider;
        private readonly HashSet<int> _appliedAffects = new();
        private readonly HashSet<ConfigCommon.DamageType> _suppressedOnHitElementGaugeTypes = new();
        private bool _suppressAllOnHitElementGauge;

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
            _formulaVariableProvider = GetComponent<PassiveFormulaVariableProvider>();
            _mpGainBonusProvider = GetComponent<PassiveMpGainBonusProvider>();
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
        /// 현재 장착된 패시브가 Damage 이벤트의 OnHitElementGauge 적용을 차단하는지 확인합니다.
        /// </summary>
        /// <param name="damageType">확인할 원소 게이지 데미지 타입입니다.</param>
        /// <returns>전체 차단 또는 해당 데미지 타입 차단 패시브가 있으면 true를 반환합니다.</returns>
        public bool SuppressesOnHitElementGauge(ConfigCommon.DamageType damageType)
        {
            if (_suppressAllOnHitElementGauge)
            {
                return true;
            }

            return damageType != ConfigCommon.DamageType.None &&
                   _suppressedOnHitElementGaugeTypes.Contains(damageType);
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
            ResetPassiveCombatPolicies();

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
            var formulaVariables = new List<StruckTableSkillPassiveOption>(8);
            var mpGainBonuses = new List<StruckTableSkillPassiveOption>(4);

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
                            if (TryResolvePassiveStatId(op, out string statId))
                            {
                                StatModifierHelper.AccumulateStat(flat, percent, statId, op.Op, op.Value);
                            }
                            break;

                        case SkillOptionKind.Affect:
                            if (TryParseIntId(op.TargetId, out var affectUid) && affectUid > 0)
                                desiredAffects.Add(affectUid);
                            break;

                        case SkillOptionKind.SuppressOnHitElementGauge:
                            AccumulateOnHitElementGaugeSuppression(op);
                            break;

                        case SkillOptionKind.FormulaVariable:
                            AccumulateFormulaVariable(formulaVariables, op);
                            break;

                        case SkillOptionKind.MpGainBonus:
                            AccumulateMpGainBonus(mpGainBonuses, op);
                            break;
                    }
                }
            }

            // 3) modifier 적용
            _character.SetPassiveSkillModifiers(flat, percent, recalculate: false);
            SyncAffects(desiredAffects);
            SyncFormulaVariables(formulaVariables);
            SyncMpGainBonuses(mpGainBonuses);
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

        /// <summary>
        /// 패시브에서 제공하는 전투 정책 캐시를 초기화합니다.
        /// Stat/Affect와 달리 CharacterStat에 저장되지 않는 실행 정책은 리빌드마다 다시 계산합니다.
        /// </summary>
        private void ResetPassiveCombatPolicies()
        {
            _suppressAllOnHitElementGauge = false;
            _suppressedOnHitElementGaugeTypes.Clear();
            _formulaVariableProvider?.ClearVariables();
            _mpGainBonusProvider?.ClearBonuses();
        }

        /// <summary>
        /// 패시브 Stat 옵션의 TargetId를 stat 테이블 ID로 해석합니다.
        /// </summary>
        /// <param name="option">패시브 옵션 테이블 행입니다.</param>
        /// <param name="statId">정규화된 stat 테이블 ID입니다.</param>
        /// <returns>BASE_* 또는 STAT_* 계열 stat ID로 사용할 수 있으면 true를 반환합니다.</returns>
        /// <remarks>
        /// skill_passive_option.TargetId 컬럼은 여러 옵션 종류가 공유합니다.
        /// Stat 옵션에서는 TargetId를 stat 테이블의 ID로 사용하므로, BASE_*와 STAT_*만 modifier 버킷에 누적합니다.
        /// STAT_ATK는 Core 계산 정책에 따라 TotalStatAtk에 반영되고, BASE_ATK는 TotalBaseAtk에 반영됩니다.
        /// STAT_HP_TEMP는 마이그레이션 호환을 위해 BASE_HP_TEMP로 정규화합니다.
        /// </remarks>
        private static bool TryResolvePassiveStatId(StruckTableSkillPassiveOption option, out string statId)
        {
            statId = null;
            if (option == null || option.Kind != SkillOptionKind.Stat)
            {
                return false;
            }

            string rawStatId = option.StatId;
            if (string.IsNullOrWhiteSpace(rawStatId))
            {
                return false;
            }

            string normalizedStatId = ConfigCommon.NormalizeStatId(rawStatId);
            ConfigCommon.StatGroup statGroup = ConfigCommon.ResolveStatGroupById(normalizedStatId);
            if (statGroup == ConfigCommon.StatGroup.None)
            {
                return false;
            }

            statId = normalizedStatId;
            return true;
        }


        /// <summary>
        /// 패시브 옵션 한 줄을 공식 변수 적용 목록에 추가합니다.
        /// </summary>
        /// <param name="formulaVariables">공식 변수 옵션을 임시로 누적할 목록입니다.</param>
        /// <param name="option">패시브 옵션 테이블 행입니다.</param>
        /// <remarks>
        /// FormulaVariable 옵션은 CharacterStat에 반영하지 않고, 데미지 공식 계산 직전 Provider를 통해서만 주입합니다.
        /// </remarks>
        private static void AccumulateFormulaVariable(
            List<StruckTableSkillPassiveOption> formulaVariables,
            StruckTableSkillPassiveOption option)
        {
            if (formulaVariables == null || option == null || option.Kind != SkillOptionKind.FormulaVariable)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(option.FormulaVariableId))
            {
                return;
            }

            formulaVariables.Add(option);
        }

        /// <summary>
        /// 패시브 공식 변수 Provider에 현재 장착 패시브의 공식 변수를 전체 교체 방식으로 반영합니다.
        /// </summary>
        /// <param name="formulaVariables">현재 장착 패시브에서 계산된 공식 변수 옵션 목록입니다.</param>
        /// <remarks>
        /// 패시브는 장착 목록을 기준으로 전체 리빌드되므로, 기존 값을 모두 제거한 뒤 현재 값만 다시 등록합니다.
        /// </remarks>
        private void SyncFormulaVariables(IReadOnlyList<StruckTableSkillPassiveOption> formulaVariables)
        {
            if (formulaVariables == null || formulaVariables.Count == 0)
            {
                _formulaVariableProvider?.ClearVariables();
                return;
            }

            PassiveFormulaVariableProvider provider = EnsureFormulaVariableProvider();
            if (provider == null)
            {
                return;
            }

            provider.ClearVariables();
            for (int i = 0; i < formulaVariables.Count; i++)
            {
                StruckTableSkillPassiveOption option = formulaVariables[i];
                if (option == null || string.IsNullOrWhiteSpace(option.FormulaVariableId))
                {
                    continue;
                }

                provider.AddVariable(
                    option.FormulaVariableId,
                    option.FormulaVariableValue,
                    option.FormulaVariableValueType,
                    option.FormulaVariableOperation);
            }
        }

        /// <summary>
        /// 패시브 옵션 한 줄을 MP 획득 보너스 적용 목록에 추가합니다.
        /// </summary>
        /// <param name="mpGainBonuses">MP 획득 보너스 옵션을 임시로 누적할 목록입니다.</param>
        /// <param name="option">패시브 옵션 테이블 행입니다.</param>
        /// <remarks>
        /// MP 획득 보너스는 CharacterStat에 직접 반영하지 않고, 실제 MP 지급 시점에 Provider를 통해 계산합니다.
        /// </remarks>
        private static void AccumulateMpGainBonus(
            List<StruckTableSkillPassiveOption> mpGainBonuses,
            StruckTableSkillPassiveOption option)
        {
            if (mpGainBonuses == null || option == null || option.Kind != SkillOptionKind.MpGainBonus)
            {
                return;
            }

            mpGainBonuses.Add(option);
        }

        /// <summary>
        /// MP 획득 보너스 Provider에 현재 장착 패시브의 MP 획득 보너스를 전체 교체 방식으로 반영합니다.
        /// </summary>
        /// <param name="mpGainBonuses">현재 장착 패시브에서 계산된 MP 획득 보너스 옵션 목록입니다.</param>
        /// <remarks>
        /// 패시브는 장착 목록을 기준으로 전체 리빌드되므로, 기존 값을 모두 제거한 뒤 현재 값만 다시 등록합니다.
        /// </remarks>
        private void SyncMpGainBonuses(IReadOnlyList<StruckTableSkillPassiveOption> mpGainBonuses)
        {
            if (mpGainBonuses == null || mpGainBonuses.Count == 0)
            {
                _mpGainBonusProvider?.ClearBonuses();
                return;
            }

            PassiveMpGainBonusProvider provider = EnsureMpGainBonusProvider();
            if (provider == null)
            {
                return;
            }

            provider.ClearBonuses();
            for (int i = 0; i < mpGainBonuses.Count; i++)
            {
                provider.AddBonus(mpGainBonuses[i]);
            }
        }

        /// <summary>
        /// 공식 변수 Provider를 반환하고, 필요하면 현재 캐릭터 오브젝트에 자동 부착합니다.
        /// </summary>
        /// <returns>공식 변수 Provider 컴포넌트입니다.</returns>
        private PassiveFormulaVariableProvider EnsureFormulaVariableProvider()
        {
            if (_formulaVariableProvider != null)
            {
                return _formulaVariableProvider;
            }

            _formulaVariableProvider = GetComponent<PassiveFormulaVariableProvider>();
            if (_formulaVariableProvider == null)
            {
                _formulaVariableProvider = gameObject.AddComponent<PassiveFormulaVariableProvider>();
            }

            return _formulaVariableProvider;
        }


        /// <summary>
        /// MP 획득 보너스 Provider를 반환하고, 필요하면 현재 캐릭터 오브젝트에 자동 부착합니다.
        /// </summary>
        /// <returns>MP 획득 보너스 Provider 컴포넌트입니다.</returns>
        private PassiveMpGainBonusProvider EnsureMpGainBonusProvider()
        {
            if (_mpGainBonusProvider != null)
            {
                return _mpGainBonusProvider;
            }

            _mpGainBonusProvider = GetComponent<PassiveMpGainBonusProvider>();
            if (_mpGainBonusProvider == null)
            {
                _mpGainBonusProvider = gameObject.AddComponent<PassiveMpGainBonusProvider>();
            }

            return _mpGainBonusProvider;
        }

        /// <summary>
        /// 패시브 옵션 한 줄을 OnHitElementGauge 차단 정책으로 누적합니다.
        /// TargetId가 비어 있거나 All이면 모든 원소 게이지를 차단하고, 특정 DamageType이면 해당 타입만 차단합니다.
        /// </summary>
        /// <param name="option">패시브 옵션 테이블 행입니다.</param>
        private void AccumulateOnHitElementGaugeSuppression(StruckTableSkillPassiveOption option)
        {
            if (option == null)
            {
                return;
            }

            if (IsAllElementGaugeSuppressionTarget(option.TargetId))
            {
                _suppressAllOnHitElementGauge = true;
                _suppressedOnHitElementGaugeTypes.Clear();
                return;
            }

            if (TryParseDamageType(option.TargetId, out ConfigCommon.DamageType damageType) &&
                damageType != ConfigCommon.DamageType.None)
            {
                _suppressedOnHitElementGaugeTypes.Add(damageType);
            }
        }

        /// <summary>
        /// OnHitElementGauge 차단 대상 문자열이 전체 차단을 의미하는지 확인합니다.
        /// </summary>
        /// <param name="targetId">패시브 옵션 TargetId 값입니다.</param>
        /// <returns>비어 있거나 All, Any, *이면 true를 반환합니다.</returns>
        private static bool IsAllElementGaugeSuppressionTarget(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return true;
            }

            string normalized = targetId.Trim();
            return string.Equals(normalized, "All", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(normalized, "Any", StringComparison.OrdinalIgnoreCase) ||
                   normalized == "*";
        }

        /// <summary>
        /// 패시브 옵션 TargetId를 원소 게이지 데미지 타입으로 해석합니다.
        /// 문자열 enum 이름과 정수 enum 값을 모두 지원합니다.
        /// </summary>
        /// <param name="targetId">패시브 옵션 TargetId 값입니다.</param>
        /// <param name="damageType">해석된 데미지 타입입니다.</param>
        /// <returns>DamageType으로 해석할 수 있으면 true를 반환합니다.</returns>
        private static bool TryParseDamageType(string targetId, out ConfigCommon.DamageType damageType)
        {
            damageType = ConfigCommon.DamageType.None;
            if (string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            string normalized = targetId.Trim();
            if (Enum.TryParse(normalized, ignoreCase: true, out ConfigCommon.DamageType parsed))
            {
                damageType = parsed;
                return true;
            }

            if (int.TryParse(normalized, out int rawValue) &&
                Enum.IsDefined(typeof(ConfigCommon.DamageType), rawValue))
            {
                damageType = (ConfigCommon.DamageType)rawValue;
                return true;
            }

            return false;
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
