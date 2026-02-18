using System;
using System.Collections.Generic;
using System.IO;
using GGemCo2DSkill;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// TimelineAsset -> SkillRuntimeSequence Bake 유틸리티.
    /// - Authoring Timeline은 제작/검증용이며, 런타임은 Bake된 <see cref="SkillRuntimeSequence"/>만 사용한다.
    /// - SkillEventTrack의 이벤트 클립을 수집하여 <see cref="SkillRuntimeEvent"/>로 변환한다.
    /// - 이벤트 클립의 Payload(Definition ScriptableObject)는 RuntimeSequence 에셋의 Sub-Asset으로 생성/갱신한다.
    /// </summary>
    public static class SkillTimelineBaker
    {
        /// <summary>
        /// Timeline을 메모리 상의 런타임 데이터로 변환한다.
        /// </summary>
        /// <remarks>
        /// <para>
        /// - PlayMode 테스트에서 Addressables 로딩을 우회하기 위한 용도이다.
        /// - Sub-Asset 생성/저장은 하지 않으며, Payload는 임시 ScriptableObject 인스턴스로 생성된다.
        /// </para>
        /// </remarks>
        /// <param name="timeline">변환할 타임라인 에셋</param>
        /// <returns>이벤트/페이로드/총 길이</returns>
        public static (SkillRuntimeEvent[] events, UnityEngine.Object[] payloads, float duration) BakeToMemory(TimelineAsset timeline)
        {
            if (timeline == null) throw new ArgumentNullException(nameof(timeline));

            var (events, payloadFactories, duration) = Extract(timeline);

            var payloads = new UnityEngine.Object[payloadFactories.Count];
            for (int i = 0; i < payloadFactories.Count; i++)
            {
                var f = payloadFactories[i];
                payloads[i] = f != null ? f.Invoke() : null;
            }

            return (events, payloads, duration);
        }

        public static SkillRuntimeSequence BakeOrUpdate(int skillUid, TimelineAsset timeline, string assetPath)
        {
            if (timeline == null) throw new ArgumentNullException(nameof(timeline));
            if (skillUid <= 0) throw new ArgumentOutOfRangeException(nameof(skillUid));
            if (string.IsNullOrEmpty(assetPath)) throw new ArgumentNullException(nameof(assetPath));

            var (events, payloadFactories, duration) = Extract(timeline);

            Directory.CreateDirectory(Path.GetDirectoryName(assetPath) ?? "Assets");

            var seq = AssetDatabase.LoadAssetAtPath<SkillRuntimeSequence>(assetPath);
            if (seq == null)
            {
                seq = ScriptableObject.CreateInstance<SkillRuntimeSequence>();
                AssetDatabase.CreateAsset(seq, assetPath);
            }

            // 기존 Payload(Sub-Asset) 정리 후 재생성 (Bake 결과와의 불일치 방지)
            CleanupPayloadSubAssets(assetPath, seq);

            var bakedPayloads = new List<UnityEngine.Object>(payloadFactories.Count);
            for (int i = 0; i < payloadFactories.Count; i++)
            {
                var factory = payloadFactories[i];
                if (factory == null)
                {
                    bakedPayloads.Add(null);
                    continue;
                }

                var payload = factory.Invoke();
                if (payload == null)
                {
                    bakedPayloads.Add(null);
                    continue;
                }

                payload.name = $"Skill_{skillUid}_Payload_{i}";
                AssetDatabase.AddObjectToAsset(payload, seq);
                bakedPayloads.Add(payload);
            }

            seq.EditorSetData(skillUid, duration, events, bakedPayloads.ToArray());

            EditorUtility.SetDirty(seq);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return seq;
        }

        private static void CleanupPayloadSubAssets(string assetPath, SkillRuntimeSequence seq)
        {
            // 같은 파일 경로의 모든 에셋을 읽어, 메인(seq) 외 ScriptableObject payload를 제거한다.
            // (사용자가 실수로 수동 생성해 넣은 에셋도 같이 정리될 수 있으므로, RuntimeSequence 경로는 전용 폴더를 권장)
            var all = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int i = 0; i < all.Length; i++)
            {
                var o = all[i];
                if (o == null) continue;
                if (o == seq) continue;

                // Payload는 Definition ScriptableObject 계열만 제거한다.
                // (텍스처/머테리얼 등 다른 타입이 섞일 가능성은 낮지만, 방어 차원에서 ScriptableObject만 처리)
                if (o is ScriptableObject)
                {
                    UnityEngine.Object.DestroyImmediate(o, true);
                }
            }
        }

        private static (SkillRuntimeEvent[] events, List<Func<UnityEngine.Object>> payloadFactories, float duration) Extract(TimelineAsset timeline)
        {
            var tmpEvents = new List<SkillRuntimeEvent>(64);
            var payloadFactories = new List<Func<UnityEngine.Object>>(64);

            float duration = (float)timeline.duration;

            foreach (var track in EnumerateAllTracks(timeline))
            {
                if (track == null) continue;
                if (track is not SkillEventTrack) continue;

                foreach (var clip in track.GetClips())
                {
                    if (clip.asset is not SkillEventClipBase evClip) continue;

                    int payloadIndex = -1;
                    var factory = CreatePayloadFactory(evClip);
                    if (factory != null)
                    {
                        payloadIndex = payloadFactories.Count;
                        payloadFactories.Add(factory);
                    }

                    tmpEvents.Add(new SkillRuntimeEvent
                    {
                        Type = evClip.EventType,
                        StartTime = (float)clip.start,
                        EndTime = (float)clip.end,
                        Order = evClip.Order,
                        PayloadIndex = payloadIndex
                    });
                }
            }

            // 정렬 안정성: time -> order
            tmpEvents.Sort((a, b) =>
            {
                int t = a.StartTime.CompareTo(b.StartTime);
                if (t != 0) return t;
                return a.Order.CompareTo(b.Order);
            });

            return (tmpEvents.ToArray(), payloadFactories, duration);
        }

        private static IEnumerable<TrackAsset> EnumerateAllTracks(TimelineAsset timeline)
        {
            // GetOutputTracks()는 GroupTrack/하위 트랙 구성이 섞였을 때 누락될 수 있어,
            // RootTracks부터 재귀 순회하여 모든 트랙을 수집한다.
            foreach (var root in timeline.GetRootTracks())
            {
                foreach (var t in EnumerateTrackRecursive(root))
                    yield return t;
            }
        }

        private static IEnumerable<TrackAsset> EnumerateTrackRecursive(TrackAsset track)
        {
            if (track == null) yield break;

            yield return track;

            foreach (var child in track.GetChildTracks())
            {
                foreach (var t in EnumerateTrackRecursive(child))
                    yield return t;
            }
        }

        private static Func<UnityEngine.Object> CreatePayloadFactory(SkillEventClipBase clip)
        {
            // 현재 런타임 실행기(SkillExecutor)는 아래 3종 이벤트만 처리한다.
            // 그 외 이벤트는 PayloadIndex = -1 로 저장되며, 런타임에서 무시된다.
            switch (clip)
            {
                case SkillDamageClip dmg:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<DamageEventDefinition>();
                        def.multiplier = dmg.Multiplier;

                        def.area = new SkillAreaSpec
                        {
                            shape = dmg.AreaShape,
                            radius = dmg.Radius,
                            length = dmg.Length,
                            width = dmg.Width,
                            angle = dmg.Angle,
                            localOffset = new Vector3(dmg.Offset.x, dmg.Offset.y, 0f)
                        };

                        // dmg.DamageTypeUid는 현재 DamageEventDefinition에 매핑 필드가 없으므로 보관하지 않는다.
                        // 필요 시 DamageEventDefinition에 damageTypeUid(또는 DamageTypeId)를 추가하고 여기서 매핑한다.
                        return def;
                    };

                case SkillSpawnEffectClip fx:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<EffectEventDefinition>();
                        def.effectUid = fx.EffectUid;
                        def.attachToTarget = fx.Anchor == (int)SkillSpawnEffectClip.AnchorType.Target;
                        def.localOffset = new Vector3(fx.Offset.x, fx.Offset.y, 0f);
                        return def;
                    };

                case SkillApplyAffectClip aff:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<ApplyStatusEventDefinition>();
                        def.statusId = new StatusEffectId { id = aff.AffectUid.ToString() };
                        def.durationOverrideSeconds = aff.Duration;
                        return def;
                    };


                case SkillLungeClip lunge:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<LungeEventDefinition>();
                        def.distance = Mathf.Max(0f, lunge.Distance);
                        def.durationOverrideSeconds = lunge.DurationOverrideSeconds;
                        def.easing = lunge.Easing;
                        def.stopAtEnd = lunge.StopAtEnd;
                        def.useMovePosition = lunge.UseMovePosition;
                        def.useSnapshotForward = lunge.UseSnapshotForward;
                        return def;
                    };

                case SkillProjectileClip proj:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<ProjectileEventDefinition>();
                        def.projectileUid = proj.ProjectileUid;
                        def.damageType = proj.DamageType;
                        def.damage = proj.Damage;
                        def.speedMultiplier = proj.SpeedMultiplier;
                        def.scaleMultiplier = proj.ScaleMultiplier;
                        def.visualType = proj.VisualType;
                        def.visualSprite = proj.VisualSprite;
                        def.visualAnimatorController = proj.VisualAnimatorController;
                        def.visualEffectUidOverride = proj.VisualEffectUidOverride;
                        def.targetingOverride = proj.TargetingOverride;
                        return def;
                    };

                default:
                    return null;
            }
        }
    }
}
