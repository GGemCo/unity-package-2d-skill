using System;
using System.Collections.Generic;
using System.Linq;
using Config;
using GGemCo2DCore;
using GGemCo2DCoreEditor;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 맵에 배치된 몬스터가 사용하는 스킬을 분석하여 Core 사운드 사용 매니페스트에 추가합니다.
    /// </summary>
    public sealed class MonsterSkillSoundUsageContributor : ISoundUsageManifestContributor
    {
        private readonly Dictionary<int, IReadOnlyList<MonsterSkillSoundUsage>> _skillUsageCache =
            new Dictionary<int, IReadOnlyList<MonsterSkillSoundUsage>>();

        /// <inheritdoc />
        public int Order => 100;

        /// <inheritdoc />
        public string DisplayName => "Skill 패키지 몬스터 스킬 사운드 분석";

        /// <inheritdoc />
        public void Collect(SoundUsageManifestBuildContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));

            TableSkillMonster tableSkillMonster =
                TableLoaderManagerSkill.LoadTableSkillMonster(forceReload: true);
            TableSkillChargeStage tableChargeStage =
                TableLoaderManagerSkill.LoadTableSkillChargeStage(forceReload: true);

            if (tableSkillMonster == null)
            {
                context.AddWarning("Skill 패키지의 skill_monster 테이블을 로드하지 못해 몬스터 스킬 사운드 분석을 건너뜁니다.");
                return;
            }

            if (tableChargeStage == null)
            {
                context.AddWarning(
                    "Skill 패키지의 skill_charge_stage 테이블을 로드하지 못했습니다. 스킬 기본 차징 사운드와 RuntimeSequence는 분석하지만 단계별 사운드는 제외합니다.");
            }

            Dictionary<int, Dictionary<int, MonsterSkillMapOrigin>> skillsByMap =
                CollectSkillOriginsByMap(context);
            int mapSkillLinkCount = 0;
            int addedUsageCount = 0;

            foreach (KeyValuePair<int, Dictionary<int, MonsterSkillMapOrigin>> mapPair in
                     skillsByMap.OrderBy(pair => pair.Key))
            {
                int mapUid = mapPair.Key;
                string mapName = context.TryGetMap(mapUid, out StruckTableMap map)
                    ? map.Name
                    : string.Empty;

                foreach (KeyValuePair<int, MonsterSkillMapOrigin> skillPair in
                         mapPair.Value.OrderBy(pair => pair.Key))
                {
                    int skillUid = skillPair.Key;
                    MonsterSkillMapOrigin origin = skillPair.Value;
                    if (!tableSkillMonster.TryGetDataByUid(skillUid, out StruckTableSkillMonster skill) ||
                        skill == null)
                    {
                        context.AddWarning(
                            $"monster 테이블에 연결된 스킬 UID가 skill_monster 테이블에 없습니다. mapUid={mapUid}, skillUid={skillUid}, monsters={origin.BuildMonsterUidText()}");
                        continue;
                    }

                    IReadOnlyList<MonsterSkillSoundUsage> usages = GetOrAnalyzeSkill(
                        skill,
                        tableChargeStage,
                        context);
                    mapSkillLinkCount++;

                    for (int i = 0; i < usages.Count; i++)
                    {
                        MonsterSkillSoundUsage usage = usages[i];
                        if (usage == null || usage.SoundUid <= 0)
                            continue;

                        context.AddMapSoundUsage(
                            mapUid,
                            usage.SoundUid,
                            usage.SourceType,
                            skillUid,
                            usage.SourcePath,
                            BuildUsageMemo(mapUid, mapName, skill, origin, usage));
                        addedUsageCount++;
                    }
                }
            }

            context.AddMessage(
                $"Skill 몬스터 스킬 사운드 분석 완료: maps={skillsByMap.Count}, uniqueSkills={_skillUsageCache.Count}, mapSkillLinks={mapSkillLinkCount}, usages={addedUsageCount}");
        }

        /// <summary>
        /// Core 기본 분석기가 수집한 맵별 몬스터 목록에서 일반 스킬과 사망 스킬 사용 관계를 구성합니다.
        /// </summary>
        /// <param name="context">맵 몬스터 배치와 Core 몬스터 테이블 조회 API를 제공하는 컨텍스트입니다.</param>
        /// <returns>맵 UID, 스킬 UID 순으로 묶은 스킬 사용 원본 정보입니다.</returns>
        private static Dictionary<int, Dictionary<int, MonsterSkillMapOrigin>> CollectSkillOriginsByMap(
            SoundUsageManifestBuildContext context)
        {
            Dictionary<int, Dictionary<int, MonsterSkillMapOrigin>> result =
                new Dictionary<int, Dictionary<int, MonsterSkillMapOrigin>>();
            IReadOnlyList<SoundUsageManifestMapMonsterPlacement> placements =
                context.MapMonsterPlacements;

            for (int i = 0; i < placements.Count; i++)
            {
                SoundUsageManifestMapMonsterPlacement placement = placements[i];
                if (!context.TryGetMonster(placement.MonsterUid, out StruckTableMonster monster))
                    continue;

                if (!result.TryGetValue(
                        placement.MapUid,
                        out Dictionary<int, MonsterSkillMapOrigin> skills))
                {
                    skills = new Dictionary<int, MonsterSkillMapOrigin>();
                    result.Add(placement.MapUid, skills);
                }

                int[] normalSkillUids = monster.SkillMonsterUid;
                if (normalSkillUids != null)
                {
                    for (int skillIndex = 0; skillIndex < normalSkillUids.Length; skillIndex++)
                    {
                        AddSkillOrigin(
                            skills,
                            normalSkillUids[skillIndex],
                            placement.MonsterUid,
                            isDeathSkill: false);
                    }
                }

                AddSkillOrigin(
                    skills,
                    monster.DeathSkillMonsterUid,
                    placement.MonsterUid,
                    isDeathSkill: true);
            }

            return result;
        }

        /// <summary>
        /// 맵 안에서 같은 스킬을 사용하는 몬스터 UID와 사망 스킬 여부를 중복 없이 누적합니다.
        /// </summary>
        /// <param name="skills">현재 맵의 스킬 사용 원본 사전입니다.</param>
        /// <param name="skillUid">등록할 몬스터 스킬 UID입니다.</param>
        /// <param name="monsterUid">해당 스킬을 사용하는 몬스터 UID입니다.</param>
        /// <param name="isDeathSkill">사망 시 실행되는 스킬이면 true입니다.</param>
        private static void AddSkillOrigin(
            Dictionary<int, MonsterSkillMapOrigin> skills,
            int skillUid,
            int monsterUid,
            bool isDeathSkill)
        {
            if (skills == null || skillUid <= 0 || monsterUid <= 0)
                return;

            if (!skills.TryGetValue(skillUid, out MonsterSkillMapOrigin origin))
            {
                origin = new MonsterSkillMapOrigin();
                skills.Add(skillUid, origin);
            }

            origin.MonsterUids.Add(monsterUid);
            origin.ContainsDeathSkill |= isDeathSkill;
        }

        /// <summary>
        /// 같은 몬스터 스킬이 여러 맵에서 사용될 때 RuntimeSequence와 테이블 분석 결과를 재사용합니다.
        /// </summary>
        /// <param name="skill">분석할 monster skill 테이블 행입니다.</param>
        /// <param name="tableChargeStage">몬스터 차징 단계 사운드를 조회할 테이블입니다.</param>
        /// <param name="context">누락 에셋 경고를 기록할 생성 컨텍스트입니다.</param>
        /// <returns>스킬에서 발견한 고유 사운드 사용처 목록입니다.</returns>
        private IReadOnlyList<MonsterSkillSoundUsage> GetOrAnalyzeSkill(
            StruckTableSkillMonster skill,
            TableSkillChargeStage tableChargeStage,
            SoundUsageManifestBuildContext context)
        {
            if (_skillUsageCache.TryGetValue(
                    skill.Uid,
                    out IReadOnlyList<MonsterSkillSoundUsage> cached))
            {
                return cached;
            }

            List<MonsterSkillSoundUsage> usages = new List<MonsterSkillSoundUsage>();
            CollectChargeSounds(skill, tableChargeStage, usages);
            CollectRuntimeSequenceSounds(skill, usages, context);

            IReadOnlyList<MonsterSkillSoundUsage> normalized = usages
                .Where(usage => usage != null && usage.SoundUid > 0)
                .GroupBy(usage => usage.BuildDeduplicationKey(), StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(usage => usage.SourceType)
                .ThenBy(usage => usage.SoundUid)
                .ThenBy(usage => usage.SourcePath, StringComparer.Ordinal)
                .ToArray();
            _skillUsageCache.Add(skill.Uid, normalized);
            return normalized;
        }

        /// <summary>
        /// skill_monster의 차징 전체 사운드와 skill_charge_stage의 진입·루프 사운드를 수집합니다.
        /// </summary>
        /// <param name="skill">분석할 몬스터 스킬 테이블 행입니다.</param>
        /// <param name="tableChargeStage">차징 단계 테이블입니다.</param>
        /// <param name="target">발견한 사운드 사용처를 추가할 목록입니다.</param>
        private static void CollectChargeSounds(
            StruckTableSkillMonster skill,
            TableSkillChargeStage tableChargeStage,
            List<MonsterSkillSoundUsage> target)
        {
            if (skill == null || target == null || !skill.UseCharge)
                return;

            if (skill.ChargeSoundUid > 0)
            {
                target.Add(new MonsterSkillSoundUsage
                {
                    SoundUid = skill.ChargeSoundUid,
                    SourceType = SoundUsageManifestSourceType.MonsterSkillCharge,
                    SourcePath = $"{ConfigAddressableTableSkill.TableSkillMonster.Path}#Uid={skill.Uid}/ChargeSoundUid",
                    Memo = "skill_monster ChargeSoundUid",
                });
            }

            if (tableChargeStage == null)
                return;

            IReadOnlyList<StruckTableSkillChargeStage> stages = tableChargeStage.GetStages(
                skill.Uid,
                ConfigCommonSkill.SkillOwnerType.Monster);
            for (int i = 0; i < stages.Count; i++)
            {
                StruckTableSkillChargeStage stage = stages[i];
                if (stage == null)
                    continue;

                AddChargeStageSound(
                    target,
                    skill.Uid,
                    stage,
                    stage.EnterSoundUid,
                    nameof(stage.EnterSoundUid));
                AddChargeStageSound(
                    target,
                    skill.Uid,
                    stage,
                    stage.LoopSoundUid,
                    nameof(stage.LoopSoundUid));
            }
        }

        /// <summary>
        /// 차징 단계의 단일 사운드 필드를 매니페스트 분석 결과에 추가합니다.
        /// </summary>
        /// <param name="target">발견한 사운드 사용처를 추가할 목록입니다.</param>
        /// <param name="skillUid">차징 단계가 연결된 스킬 UID입니다.</param>
        /// <param name="stage">원본 차징 단계 행입니다.</param>
        /// <param name="soundUid">필드에 설정된 sound UID입니다.</param>
        /// <param name="fieldName">진단 경로에 사용할 필드 이름입니다.</param>
        private static void AddChargeStageSound(
            List<MonsterSkillSoundUsage> target,
            int skillUid,
            StruckTableSkillChargeStage stage,
            int soundUid,
            string fieldName)
        {
            if (target == null || stage == null || soundUid <= 0)
                return;

            target.Add(new MonsterSkillSoundUsage
            {
                SoundUid = soundUid,
                SourceType = SoundUsageManifestSourceType.MonsterSkillChargeStage,
                SourcePath =
                    $"{ConfigAddressableTableSkill.TableSkillChargeStage.Path}#Uid={stage.Uid}/{fieldName}",
                Memo = $"skillUid={skillUid}, stageIndex={stage.StageIndex}, field={fieldName}",
            });
        }

        /// <summary>
        /// 베이크된 몬스터 SkillRuntimeSequence를 열어 사운드 제공 Payload의 UID를 수집합니다.
        /// </summary>
        /// <param name="skill">분석할 몬스터 스킬 테이블 행입니다.</param>
        /// <param name="target">발견한 사운드 사용처를 추가할 목록입니다.</param>
        /// <param name="context">누락된 RuntimeSequence와 잘못된 Payload를 경고로 기록할 컨텍스트입니다.</param>
        private static void CollectRuntimeSequenceSounds(
            StruckTableSkillMonster skill,
            List<MonsterSkillSoundUsage> target,
            SoundUsageManifestBuildContext context)
        {
            if (skill == null || target == null)
                return;

            if (string.IsNullOrWhiteSpace(skill.SoFileName))
            {
                context.AddWarning(
                    $"몬스터 스킬의 RuntimeSequence 파일명이 비어 있어 사운드 이벤트를 분석하지 못했습니다. skillUid={skill.Uid}");
                return;
            }

            string assetPath =
                $"{ConfigAddressablePathSkill.Skill.RuntimeSequence.Monster}/{skill.SoFileName}.asset";
            SkillRuntimeSequence sequence =
                AssetDatabase.LoadAssetAtPath<SkillRuntimeSequence>(assetPath);
            if (sequence == null)
            {
                context.AddWarning(
                    $"몬스터 스킬 RuntimeSequence 에셋을 찾지 못했습니다. skillUid={skill.Uid}, path={assetPath}");
                return;
            }

            if (sequence.SkillUid > 0 && sequence.SkillUid != skill.Uid)
            {
                context.AddWarning(
                    $"몬스터 스킬 RuntimeSequence의 SkillUid가 테이블과 다릅니다. tableSkillUid={skill.Uid}, sequenceSkillUid={sequence.SkillUid}, path={assetPath}");
            }

            SkillRuntimeEvent[] events = sequence.Events;
            if (events == null || events.Length == 0)
                return;

            for (int eventIndex = 0; eventIndex < events.Length; eventIndex++)
            {
                SkillRuntimeEvent runtimeEvent = events[eventIndex];
                UnityEngine.Object payload = sequence.GetPayload(runtimeEvent.PayloadIndex);
                if (!(payload is ISkillSoundUsageProvider soundUsageProvider))
                    continue;

                HashSet<int> soundUids = new HashSet<int>();
                soundUsageProvider.CollectSoundUids(soundUids);
                if (soundUids.Count == 0)
                    continue;

                SoundUsageManifestSourceType sourceType = ResolveSourceType(payload);
                foreach (int soundUid in soundUids.OrderBy(uid => uid))
                {
                    if (soundUid <= 0)
                        continue;

                    target.Add(new MonsterSkillSoundUsage
                    {
                        SoundUid = soundUid,
                        SourceType = sourceType,
                        SourcePath =
                            $"{assetPath}#event[{eventIndex}]/{payload.GetType().Name}",
                        Memo =
                            $"eventType={runtimeEvent.Type}, start={runtimeEvent.StartTime:0.###}, payloadIndex={runtimeEvent.PayloadIndex}",
                    });
                }
            }
        }

        /// <summary>
        /// RuntimeSequence Payload 종류를 매니페스트 보고서용 원본 종류로 변환합니다.
        /// </summary>
        /// <param name="payload">사운드 UID를 제공한 RuntimeSequence Payload입니다.</param>
        /// <returns>프로젝타일 비행음 또는 일반 스킬 오디오 원본 종류입니다.</returns>
        private static SoundUsageManifestSourceType ResolveSourceType(UnityEngine.Object payload)
        {
            return payload is ProjectileEventDefinition
                ? SoundUsageManifestSourceType.MonsterSkillProjectileFlight
                : SoundUsageManifestSourceType.MonsterSkillTimelineAudio;
        }

        /// <summary>
        /// 맵·몬스터·스킬 출처와 세부 사운드 위치를 결합한 추적 메모를 생성합니다.
        /// </summary>
        /// <param name="mapUid">스킬 사용 몬스터가 배치된 맵 UID입니다.</param>
        /// <param name="mapName">맵 테이블의 표시 이름입니다.</param>
        /// <param name="skill">분석된 몬스터 스킬 행입니다.</param>
        /// <param name="origin">이 스킬을 사용하는 몬스터 목록과 사망 스킬 여부입니다.</param>
        /// <param name="usage">스킬 내부에서 발견한 사운드 사용처입니다.</param>
        /// <returns>sound_usage_manifest의 Memo에 기록할 문자열입니다.</returns>
        private static string BuildUsageMemo(
            int mapUid,
            string mapName,
            StruckTableSkillMonster skill,
            MonsterSkillMapOrigin origin,
            MonsterSkillSoundUsage usage)
        {
            string deathSkillText = origin.ContainsDeathSkill ? ", includesDeathSkill=Y" : string.Empty;
            return
                $"map={mapName}({mapUid}), skill={skill.Name}({skill.Uid}), monsters={origin.BuildMonsterUidText()}{deathSkillText}, {usage.Memo}";
        }

        /// <summary>
        /// 한 맵에서 특정 스킬을 사용하는 몬스터 정보를 보관합니다.
        /// </summary>
        private sealed class MonsterSkillMapOrigin
        {
            public readonly HashSet<int> MonsterUids = new HashSet<int>();
            public bool ContainsDeathSkill;

            /// <summary>
            /// 진단 메모에 사용할 정렬된 몬스터 UID 문자열을 생성합니다.
            /// </summary>
            /// <returns>쉼표로 구분된 몬스터 UID 목록입니다.</returns>
            public string BuildMonsterUidText()
            {
                return string.Join(",", MonsterUids.OrderBy(uid => uid));
            }
        }

        /// <summary>
        /// 한 몬스터 스킬에서 발견한 사운드 사용처 한 건입니다.
        /// </summary>
        private sealed class MonsterSkillSoundUsage
        {
            public int SoundUid;
            public SoundUsageManifestSourceType SourceType;
            public string SourcePath;
            public string Memo;

            /// <summary>
            /// 같은 스킬 필드 또는 RuntimeSequence 이벤트가 중복 분석되지 않도록 비교 키를 생성합니다.
            /// </summary>
            /// <returns>원본 종류, sound UID 및 에셋 경로를 결합한 키입니다.</returns>
            public string BuildDeduplicationKey()
            {
                return $"{(int)SourceType}|{SoundUid}|{SourcePath ?? string.Empty}";
            }
        }
    }
}
