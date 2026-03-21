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
    /// TimelineAsset을 <see cref="SkillRuntimeSequence"/>로 변환하는 Bake 유틸리티입니다.
    /// Authoring Timeline의 이벤트 클립을 런타임 이벤트와 Payload 에셋으로 변환하며, 필요 시 RuntimeSequence 에셋을 생성하거나 갱신합니다.
    /// </summary>
    public static class SkillTimelineBaker
    {
        /// <summary>
        /// 런타임 버전의 <see cref="ApplyStatusEventDefinition"/>에 존재할 수 있는 applyTo 필드에 대한 리플렉션 정보입니다.
        /// 구버전 런타임과의 호환성을 위해 직접 참조 대신 리플렉션으로 접근합니다.
        /// </summary>
        private static readonly System.Reflection.FieldInfo ApplyStatusApplyToField =
            typeof(ApplyStatusEventDefinition).GetField("applyTo");

        /// <summary>
        /// <see cref="ApplyStatusEventDefinition"/>의 applyTo 필드가 존재하는 경우 값을 설정합니다.
        /// 런타임 타입 확장 여부에 따라 선택적으로 적용되며, 필드가 없거나 형식이 맞지 않으면 무시합니다.
        /// </summary>
        /// <param name="def">설정할 상태 적용 이벤트 정의입니다.</param>
        /// <param name="applyToRaw">설정할 enum 원시 값입니다.</param>
        private static void TrySetApplyStatusApplyTo(ApplyStatusEventDefinition def, int applyToRaw)
        {
            // Runtime 쪽 ApplyStatusEventDefinition 이 확장되었을 때만 적용한다.
            // (구버전 런타임과의 컴파일/런타임 호환을 위해 Reflection 사용)
            if (ApplyStatusApplyToField == null) return;

            var fieldType = ApplyStatusApplyToField.FieldType;
            if (!fieldType.IsEnum) return;

            try
            {
                var value = Enum.ToObject(fieldType, applyToRaw);
                ApplyStatusApplyToField.SetValue(def, value);
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// 타임라인을 메모리 상의 런타임 데이터로 변환합니다.
        /// Play Mode 테스트에서 Addressables 로딩 없이 즉시 사용할 수 있는 이벤트 배열과 임시 Payload 인스턴스를 생성합니다.
        /// </summary>
        /// <param name="timeline">변환할 타임라인 에셋입니다.</param>
        /// <returns>런타임 이벤트 배열, Payload 배열, 타임라인 총 길이를 포함한 튜플입니다.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="timeline"/>이 <see langword="null"/>인 경우 발생합니다.</exception>
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

        /// <summary>
        /// 타임라인을 Bake하여 <see cref="SkillRuntimeSequence"/> 에셋을 생성하거나 기존 에셋을 갱신합니다.
        /// 기존 Payload Sub-Asset은 정리한 뒤 Bake 결과에 맞게 다시 생성합니다.
        /// </summary>
        /// <param name="skillUid">Bake 대상 스킬의 고유 ID입니다.</param>
        /// <param name="timeline">변환할 타임라인 에셋입니다.</param>
        /// <param name="assetPath">생성하거나 갱신할 RuntimeSequence 에셋 경로입니다.</param>
        /// <returns>생성되거나 갱신된 <see cref="SkillRuntimeSequence"/> 인스턴스입니다.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="timeline"/> 또는 <paramref name="assetPath"/>가 유효하지 않은 경우 발생합니다.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="skillUid"/>가 0 이하인 경우 발생합니다.</exception>
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

        /// <summary>
        /// 지정한 RuntimeSequence 에셋 경로에 포함된 기존 Payload Sub-Asset을 정리합니다.
        /// 메인 에셋을 제외한 <see cref="ScriptableObject"/> 타입의 서브 에셋을 제거하여 Bake 결과와의 불일치를 방지합니다.
        /// </summary>
        /// <param name="assetPath">정리할 RuntimeSequence 에셋 경로입니다.</param>
        /// <param name="seq">보존할 메인 RuntimeSequence 에셋입니다.</param>
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

        /// <summary>
        /// 타임라인에서 스킬 이벤트와 Payload 생성 팩터리를 추출합니다.
        /// SkillEventTrack의 클립을 순회하여 런타임 이벤트 배열과 Payload 생성 규칙을 구성합니다.
        /// </summary>
        /// <param name="timeline">추출할 타임라인 에셋입니다.</param>
        /// <returns>이벤트 배열, Payload 생성 팩터리 목록, 타임라인 총 길이를 포함한 튜플입니다.</returns>
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

        /// <summary>
        /// 타임라인의 모든 트랙을 재귀적으로 순회합니다.
        /// Root Track부터 시작하여 하위 트랙까지 포함한 전체 트랙 집합을 반환합니다.
        /// </summary>
        /// <param name="timeline">순회할 타임라인 에셋입니다.</param>
        /// <returns>타임라인에 포함된 모든 트랙의 열거 결과입니다.</returns>
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

        /// <summary>
        /// 지정한 트랙과 그 하위 트랙을 재귀적으로 순회합니다.
        /// </summary>
        /// <param name="track">순회를 시작할 기준 트랙입니다.</param>
        /// <returns>현재 트랙과 모든 하위 트랙의 열거 결과입니다.</returns>
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

        /// <summary>
        /// 이벤트 클립을 런타임 Payload 생성 팩터리로 변환합니다.
        /// 지원하지 않는 클립 타입은 Payload 없이 저장되도록 <see langword="null"/>을 반환합니다.
        /// </summary>
        /// <param name="clip">변환할 스킬 이벤트 클립입니다.</param>
        /// <returns>Payload를 생성하는 팩터리 델리게이트이며, 변환 대상이 아니면 <see langword="null"/>입니다.</returns>
        private static Func<UnityEngine.Object> CreatePayloadFactory(SkillEventClipBase clip)
        {
            // 현재 런타임 실행기(SkillExecutor)는 아래 이벤트만 처리한다.
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
                            capsuleDirection = dmg.CapsuleDirection,
                            radius = dmg.Radius,
                            length = dmg.Length,
                            width = dmg.Width,
                            angle = dmg.Angle,
                            localOffset = new Vector3(dmg.Offset.x, dmg.Offset.y, 0f)
                        };

                        // dmg.DamageTypeUid는 현재 DamageEventDefinition에 매핑 필드가 없으므로 보관하지 않는다.
                        // 필요 시 DamageEventDefinition에 damageTypeUid(또는 DamageTypeId)를 추가하고 여기서 매핑한다.

                        // OnHit Affect(피격 대상)
                        def.onHitAffects = dmg.OnHitAffects;
                        def.onHitCrowdControls = dmg.OnHitCrowdControls;
                        return def;
                    };

                case SkillSpawnVfxClip fx:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<VfxEventDefinition>();
                        def.vfxUid = fx.VFXUid;
                        def.attachToTarget = fx.Anchor == (int)SkillSpawnVfxClip.AnchorType.Target;
                        def.localOffset = new Vector3(fx.Offset.x, fx.Offset.y, 0f);
                        def.lifetimeMode = fx.LifetimeMode;
                        def.lifetimeSeconds = fx.LifetimeSeconds;
                        return def;
                    };

                case SkillApplyAffectClip aff:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<ApplyStatusEventDefinition>();
                        def.statusId = new StatusVfxId { id = aff.AffectUid.ToString() };
                        def.durationOverrideSeconds = aff.AffectDuration;
                        TrySetApplyStatusApplyTo(def, (int)aff.ApplyTo);
                        return def;
                    };

                case SkillLungeClip lunge:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<LungeEventDefinition>();
                        def.distance = Mathf.Max(0f, lunge.Distance);
                        def.durationOverrideSeconds = lunge.DurationOverrideSeconds;
                        def.easing = lunge.Easing;

                        def.resolveMode = lunge.ResolveMode;
                        def.targetResolveRange = lunge.TargetResolveRange;
                        def.stopOffset = Mathf.Max(0f, lunge.StopOffset);
                        def.horizontalOnly = lunge.HorizontalOnly;

                        def.invertForward = lunge.InvertForward;

                        def.useArcMotion = lunge.UseArcMotion;
                        def.arcHeight = Mathf.Max(0f, lunge.ArcHeight);
                        def.arcMode = lunge.ArcMode;
                        def.arcRiseEase = lunge.ArcRiseEase;
                        def.arcFallEase = lunge.ArcFallEase;
                        def.arcRiseRatioNormalized = Mathf.Max(0f, lunge.ArcRiseRatioNormalized);
                        def.arcApexHoldNormalized = Mathf.Max(0f, lunge.ArcApexHoldNormalized);
                        def.arcFallRatioNormalized = Mathf.Max(0f, lunge.ArcFallRatioNormalized);

                        def.stopAtEnd = lunge.StopAtEnd;
                        def.useMovePosition = lunge.UseMovePosition;
                        def.useSnapshotForward = lunge.UseSnapshotForward;

                        def.allowReplace = lunge.AllowReplace;
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
                        def.visualVfxUidOverride = proj.VisualVfxUidOverride;
                        def.targetingOverride = proj.TargetingOverride;
                        return def;
                    };

                default:
                    return null;
            }
        }
    }
}