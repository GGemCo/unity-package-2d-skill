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
        /// Timeline VFX 클립의 Anchor 값을 런타임 VFX 생성 앵커로 변환합니다.
        /// </summary>
        /// <param name="anchorRaw">Timeline 클립에 저장된 Anchor enum 원시 값입니다.</param>
        /// <returns>런타임에서 사용할 VFX 생성 앵커입니다.</returns>
        private static VfxSpawnAnchor ConvertVfxSpawnAnchor(int anchorRaw)
        {
            switch ((SkillSpawnVfxClip.AnchorType)anchorRaw)
            {
                case SkillSpawnVfxClip.AnchorType.Target:
                    return VfxSpawnAnchor.Target;
                case SkillSpawnVfxClip.AnchorType.Ground:
                    return VfxSpawnAnchor.Ground;
                case SkillSpawnVfxClip.AnchorType.Caster:
                default:
                    return VfxSpawnAnchor.Caster;
            }
        }

        /// <summary>
        /// 레이저 데미지 활성 지속 시간을 런타임 이벤트 정의에서 사용하는 값으로 보정합니다.
        /// </summary>
        /// <param name="value">타임라인 클립에 저장된 데미지 활성 지속 시간입니다.</param>
        /// <returns>0 이하이면 레이저 종료까지 유지하는 의미의 -1, 양수이면 해당 값을 반환합니다.</returns>
        private static float NormalizeLaserDamageActiveDuration(float value)
        {
            return value <= 0f ? -1f : value;
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
                        def.damageCenterReference = dmg.DamageCenterReference;

                        // dmg.DamageTypeUid는 현재 DamageEventDefinition에 매핑 필드가 없으므로 보관하지 않는다.
                        // 필요 시 DamageEventDefinition에 damageTypeUid(또는 DamageTypeId)를 추가하고 여기서 매핑한다.

                        // Target state filter
                        def.isGroundOnly = dmg.IsGroundOnly;
                        def.isAirOnly = dmg.IsAirOnly;
                        def.facingDamagePolicy = dmg.FacingDamagePolicy;

                        // OnHit Affect(피격 대상)
                        def.onHitAffects = dmg.OnHitAffects;
                        def.onHitCrowdControls = dmg.OnHitCrowdControls;
                        def.onHitElementGauges = dmg.OnHitElementGauges;
                        def.useHitStopSelf = dmg.UseHitStopSelf;
                        def.useDefaultSelfHitStop = dmg.UseDefaultSelfHitStop;
                        def.selfHitStopSeconds = dmg.SelfHitStopSeconds;
                        def.useHitStopTarget = dmg.UseHitStopTarget;
                        def.useDefaultTargetHitStop = dmg.UseDefaultTargetHitStop;
                        def.targetHitStopSeconds = dmg.TargetHitStopSeconds;
                        def.useCameraShakeOnHit = dmg.UseCameraShakeOnHit;
                        def.cameraShakePreset = dmg.CameraShakePreset;
                        def.cameraShakeDirectionMode = dmg.CameraShakeDirectionMode;
                        def.allowSkillChainOnConfirmedDamage = dmg.AllowSkillChainOnConfirmedDamage;
                        return def;
                    };

                case SkillSpawnVfxClip fx:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<VfxEventDefinition>();
                        var spawnAnchor = ConvertVfxSpawnAnchor(fx.Anchor);
                        def.vfxUid = fx.VFXUid;
                        def.spawnAnchor = spawnAnchor;
                        def.attachToTarget = spawnAnchor == VfxSpawnAnchor.Target;
                        def.localOffset = new Vector3(fx.Offset.x, fx.Offset.y, 0f);
                        def.positionAnchorWrite = fx.PositionAnchorWrite;
                        def.lifetimeMode = fx.LifetimeMode;
                        def.lifetimeSeconds = fx.LifetimeSeconds;
                        def.overrideSortingLayer = fx.OverrideSortingLayer;
                        def.sortingLayerOverride = fx.SortingLayerOverride;
                        def.overrideSortingOrder = fx.OverrideSortingOrder;
                        def.sortingOrderOverride = fx.SortingOrderOverride;
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

                case SkillApplyTempHpClip tempHp:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<ApplyTempHpEventDefinition>();
                        def.tempHpValue = tempHp.TempHpValue > 0 ? tempHp.TempHpValue : 0;
                        def.sourceKeyOverride = tempHp.SourceKeyOverride;
                        def.applyTo = tempHp.ApplyTo;
                        return def;
                    };

                case SkillScreenFadeClip screenFade:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<SkillScreenFadeEventDefinition>();
                        def.color = screenFade.Color;
                        def.fromAlpha = Mathf.Clamp01(screenFade.FromAlpha);
                        def.toAlpha = Mathf.Clamp01(screenFade.ToAlpha);
                        def.holdFinalState = screenFade.HoldFinalState;
                        def.useClipDuration = screenFade.UseClipDuration;
                        def.durationOverrideSeconds = Mathf.Max(0f, screenFade.DurationOverrideSeconds);
                        def.easing = screenFade.Easing;
                        def.useUnscaledTime = screenFade.UseUnscaledTime;
                        def.clearOnSkillEnd = screenFade.ClearOnSkillEnd;
                        def.clearOnCancel = screenFade.ClearOnCancel;
                        def.renderMode = screenFade.RenderMode;
                        def.sortingLayerName = screenFade.SortingLayerName;
                        def.orderInLayer = screenFade.OrderInLayer;
                        def.planeDistance = Mathf.Max(0.01f, screenFade.PlaneDistance);
                        def.replaceMode = screenFade.ReplaceMode;
                        return def;
                    };

                case SkillAfterimageClip afterimage:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<SkillAfterimageEventDefinition>();
                        def.targetType = afterimage.TargetType;
                        def.actorKey = afterimage.ActorKey;
                        def.missingActorPolicy = afterimage.MissingActorPolicy;
                        def.mode = afterimage.Mode;
                        def.useClipDuration = afterimage.UseClipDuration;
                        def.durationOverrideSeconds = Mathf.Max(0f, afterimage.DurationOverrideSeconds);
                        def.spawnIntervalSeconds = Mathf.Max(0.005f, afterimage.SpawnIntervalSeconds);
                        def.ghostLifetimeSeconds = Mathf.Max(0.01f, afterimage.GhostLifetimeSeconds);
                        def.ghostColor = afterimage.GhostColor;
                        def.sortingOrderOffset = afterimage.SortingOrderOffset;
                        def.clearOnSkillEnd = afterimage.ClearOnSkillEnd;
                        def.clearOnCancel = afterimage.ClearOnCancel;
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
                        def.targetRelationMode = lunge.TargetRelationMode;
                        def.stopOffset = Mathf.Max(0f, lunge.StopOffset);
                        def.passThroughExtraDistance = Mathf.Max(0f, lunge.PassThroughExtraDistance);
                        def.horizontalOnly = lunge.HorizontalOnly;
                        def.collisionPolicy = lunge.CollisionPolicy;

                        def.invertForward = lunge.InvertForward;


                        def.stopAtEnd = lunge.StopAtEnd;
                        def.useMovePosition = lunge.UseMovePosition;
                        def.useSnapshotForward = lunge.UseSnapshotForward;

                        def.allowReplace = lunge.AllowReplace;
                        return def;
                    };

                case SkillGroundSlamClip slam:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<GroundSlamEventDefinition>();
                        def.durationOverrideSeconds = slam.DurationOverrideSeconds;
                        def.airHoldDurationSeconds = Mathf.Max(0f, slam.AirHoldDurationSeconds);
                        def.holdPositionDuringAirHold = slam.HoldPositionDuringAirHold;
                        def.fallDurationSeconds = slam.FallDurationSeconds;
                        def.easing = slam.Easing;
                        def.landingMode = slam.LandingMode;
                        def.horizontalPolicy = slam.HorizontalPolicy;
                        def.forwardDistance = Mathf.Max(0f, slam.ForwardDistance);
                        def.groundProbeStartHeight = Mathf.Max(0f, slam.GroundProbeStartHeight);
                        def.groundProbeDistance = Mathf.Max(0.1f, slam.GroundProbeDistance);
                        def.fixedDropDistance = Mathf.Max(0f, slam.FixedDropDistance);
                        def.groundSnapDistance = Mathf.Max(0f, slam.GroundSnapDistance);
                        def.groundLayerMask = slam.GroundLayerMask;
                        def.startAnimationName = slam.StartAnimationName;
                        def.fallLoopAnimationName = slam.FallLoopAnimationName;
                        def.landEndAnimationName = slam.LandEndAnimationName;
                        def.startToLoopNormalizedTime = Mathf.Clamp01(slam.StartToLoopNormalizedTime);
                        def.useSnapshotForward = slam.UseSnapshotForward;
                        def.stopAtEnd = slam.StopAtEnd;
                        def.useMovePosition = slam.UseMovePosition;
                        def.allowReplace = slam.AllowReplace;
                        return def;
                    };

                case SkillPositionHoldClip positionHold:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<PositionHoldEventDefinition>();
                        def.durationOverrideSeconds = Mathf.Max(0f, positionHold.DurationOverrideSeconds);
                        def.durationPolicy = positionHold.DurationPolicy;
                        def.stopAtEnd = positionHold.StopAtEnd;
                        def.useMovePosition = positionHold.UseMovePosition;
                        def.allowReplace = positionHold.AllowReplace;
                        return def;
                    };

                case SkillArcLungeClip arcLunge:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<ArcLungeEventDefinition>();
                        def.distance = Mathf.Max(0f, arcLunge.Distance);
                        def.durationOverrideSeconds = arcLunge.DurationOverrideSeconds;
                        def.riseDurationSeconds = Mathf.Max(0f, arcLunge.RiseDurationSeconds);
                        def.apexHoldDurationSeconds = Mathf.Max(0f, arcLunge.ApexHoldDurationSeconds);
                        def.fallDurationSeconds = Mathf.Max(0f, arcLunge.FallDurationSeconds);
                        def.easing = arcLunge.Easing;
                        def.arcHeight = Mathf.Max(0f, arcLunge.ArcHeight);
                        def.arcMode = arcLunge.ArcMode;
                        def.arcRiseEase = arcLunge.ArcRiseEase;
                        def.arcFallEase = arcLunge.ArcFallEase;
                        def.resolveMode = arcLunge.ResolveMode;
                        def.targetResolveRange = arcLunge.TargetResolveRange;
                        def.targetRelationMode = arcLunge.TargetRelationMode;
                        def.stopOffset = Mathf.Max(0f, arcLunge.StopOffset);
                        def.passThroughExtraDistance = Mathf.Max(0f, arcLunge.PassThroughExtraDistance);
                        def.horizontalOnly = arcLunge.HorizontalOnly;
                        def.collisionPolicy = arcLunge.CollisionPolicy;
                        def.riseAnimationName = arcLunge.RiseAnimationName;
                        def.apexAnimationName = arcLunge.ApexAnimationName;
                        def.fallAnimationName = arcLunge.FallAnimationName;
                        def.landEndAnimationName = arcLunge.LandEndAnimationName;
                        def.invertForward = arcLunge.InvertForward;
                        def.useSnapshotForward = arcLunge.UseSnapshotForward;
                        def.stopAtEnd = arcLunge.StopAtEnd;
                        def.useMovePosition = arcLunge.UseMovePosition;
                        def.allowReplace = arcLunge.AllowReplace;
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
                        def.targetPointPolicy = proj.TargetPointPolicy;
                        def.fixedTargetOffset = proj.FixedTargetOffset;
                        def.fixedTargetHitAreaNormalized = proj.FixedTargetHitAreaNormalized;
                        def.useProjectileHitBehaviorOverride = proj.UseProjectileHitBehaviorOverride;
                        def.hitLifetimeMode = proj.HitLifetimeMode;
                        def.damageApplyMode = proj.DamageApplyMode;
                        def.tickDamageIntervalSeconds = Mathf.Max(0f, proj.TickDamageIntervalSeconds);
                        def.useEnvironmentHitPolicyOverride = proj.UseEnvironmentHitPolicyOverride;
                        def.environmentHitPolicy = proj.EnvironmentHitPolicy;
                        def.useDefaultGroundWallEnvironmentLayers = proj.UseDefaultGroundWallEnvironmentLayers;
                        def.customEnvironmentHitLayerMask = proj.CustomEnvironmentHitLayerMask;
                        def.allowSkillChainOnConfirmedDamage = proj.AllowSkillChainOnConfirmedDamage;
                        def.targetingOverride = proj.TargetingOverride;
                        return def;
                    };

                case SkillLaserClip laser:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<LaserEventDefinition>();
                        def.laserUid = laser.LaserUid;
                        def.damageType = laser.DamageType;
                        def.damage = laser.Damage;
                        def.durationSeconds = Mathf.Max(0f, laser.DurationSeconds);
                        def.damageStartDelaySeconds = Mathf.Max(0f, laser.DamageStartDelaySeconds);
                        def.damageActiveDurationSeconds = NormalizeLaserDamageActiveDuration(laser.DamageActiveDurationSeconds);
                        def.damageTickIntervalSeconds = Mathf.Max(0f, laser.DamageTickIntervalSeconds);
                        def.damageTickOnStart = laser.DamageTickOnStart;
                        def.maxDistance = Mathf.Max(0f, laser.MaxDistance);
                        def.updateAimContinuously = laser.UpdateAimContinuously;
                        def.targetPointPolicy = laser.TargetPointPolicy;
                        def.fixedTargetOffset = laser.FixedTargetOffset;
                        def.fixedTargetHitAreaNormalized = laser.FixedTargetHitAreaNormalized;
                        def.useRaycastDirectionModeOverride = laser.UseRaycastDirectionModeOverride;
                        def.raycastDirectionModeOverride = laser.RaycastDirectionModeOverride;
                        def.useRaycastAngleOverride = laser.UseRaycastAngleOverride;
                        def.raycastAngleOverrideDeg = laser.RaycastAngleOverrideDeg;
                        def.useVfxAngleSyncModeOverride = laser.UseVfxAngleSyncModeOverride;
                        def.vfxAngleSyncModeOverride = laser.VfxAngleSyncModeOverride;
                        def.startAnchor = laser.StartAnchor;
                        def.namedAnchorKey = laser.NamedAnchorKey;
                        def.startPositionOverrideMode = laser.StartPositionOverrideMode;
                        def.startPositionOverride = laser.StartPositionOverride;
                        def.useCasterFlipStartOffsetX = laser.UseCasterFlipStartOffsetX;
                        def.startPointUpdateMode = laser.StartPointUpdateMode;
                        def.scaleMultiplier = laser.ScaleMultiplier;
                        def.visualType = laser.VisualType;
                        def.visualSprite = laser.VisualSprite;
                        def.visualAnimatorController = laser.VisualAnimatorController;
                        def.visualVfxUidOverride = laser.VisualVfxUidOverride;
                        def.allowSkillChainOnConfirmedDamage = laser.AllowSkillChainOnConfirmedDamage;
                        def.onHitElementGauges = laser.OnHitElementGauges;
                        def.targetingOverride = laser.TargetingOverride;
                        return def;
                    };

                case SkillSpawnDummyCharacterClip spawnDummy:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<SpawnDummyCharacterEventDefinition>();
                        def.actorKey = spawnDummy.ActorKey;
                        def.sourceType = spawnDummy.SourceType;
                        def.characterUid = spawnDummy.CharacterUid;
                        def.spawnAnchor = spawnDummy.SpawnAnchor;
                        def.namedAnchorKey = spawnDummy.NamedAnchorKey;
                        def.localOffset = spawnDummy.LocalOffset;
                        def.useSnapshotCenter = spawnDummy.UseSnapshotCenter;
                        def.spawnFacing = spawnDummy.SpawnFacing;
                        def.fadeInEnabled = spawnDummy.FadeInEnabled;
                        def.fadeInDurationSeconds = Mathf.Max(0f, spawnDummy.FadeInDurationSeconds);
                        def.initialAnimationName = spawnDummy.InitialAnimationName;
                        def.initialAnimationLoop = spawnDummy.InitialAnimationLoop;
                        def.initialAnimationTimeScale = Mathf.Max(0f, spawnDummy.InitialAnimationTimeScale);
                        def.despawnOnSkillEnd = spawnDummy.DespawnOnSkillEnd;
                        def.despawnOnCancel = spawnDummy.DespawnOnCancel;
                        def.replaceIfExists = spawnDummy.ReplaceIfExists;
                        return def;
                    };

                case SkillMoveDummyCharacterClip moveDummy:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<MoveDummyCharacterEventDefinition>();
                        def.actorReferenceType = moveDummy.ActorReferenceType;
                        def.actorKey = moveDummy.ActorKey;
                        def.moveTargetMode = moveDummy.MoveTargetMode;
                        def.namedAnchorKey = moveDummy.NamedAnchorKey;
                        def.absoluteWorldPosition = moveDummy.AbsoluteWorldPosition;
                        def.targetFrontDistance = moveDummy.TargetFrontDistance;
                        def.localOffset = moveDummy.LocalOffset;
                        def.useSnapshotCenter = moveDummy.UseSnapshotCenter;
                        def.durationSeconds = Mathf.Max(0f, moveDummy.DurationSeconds);
                        def.easing = moveDummy.Easing;
                        def.useMovePosition = moveDummy.UseMovePosition;
                        def.stopAtEnd = moveDummy.StopAtEnd;
                        def.allowReplace = moveDummy.AllowReplace;
                        def.lookAtTargetDuringMove = moveDummy.LookAtTargetDuringMove;
                        def.playMoveAnimation = moveDummy.PlayMoveAnimation;
                        def.moveAnimationName = moveDummy.MoveAnimationName;
                        def.moveAnimationLoop = moveDummy.MoveAnimationLoop;
                        def.moveAnimationTimeScale = Mathf.Max(0f, moveDummy.MoveAnimationTimeScale);
                        def.missingActorPolicy = moveDummy.MissingActorPolicy;
                        return def;
                    };

                case SkillDespawnDummyCharacterClip despawnDummy:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<DespawnDummyCharacterEventDefinition>();
                        def.actorKey = despawnDummy.ActorKey;
                        def.fadeOutEnabled = despawnDummy.FadeOutEnabled;
                        def.fadeOutDurationSeconds = Mathf.Max(0f, despawnDummy.FadeOutDurationSeconds);
                        def.destroyAfterFade = despawnDummy.DestroyAfterFade;
                        def.missingActorPolicy = despawnDummy.MissingActorPolicy;
                        return def;
                    };

                case SkillSetDummyAirborneStateClip airborneDummy:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<SetDummyAirborneStateEventDefinition>();
                        def.actorReferenceType = airborneDummy.ActorReferenceType;
                        def.actorKey = airborneDummy.ActorKey;
                        def.airborneEnabled = airborneDummy.AirborneEnabled;
                        def.targetAirHeight = Mathf.Max(0f, airborneDummy.TargetAirHeight);
                        def.durationSeconds = Mathf.Max(0f, airborneDummy.DurationSeconds);
                        def.easing = airborneDummy.Easing;
                        def.allowReplace = airborneDummy.AllowReplace;
                        def.missingActorPolicy = airborneDummy.MissingActorPolicy;
                        return def;
                    };

                case SkillPlayDummyCharacterAnimationClip playDummyAnimation:
                    return () =>
                    {
                        var def = ScriptableObject.CreateInstance<PlayDummyCharacterAnimationEventDefinition>();
                        def.actorReferenceType = playDummyAnimation.ActorReferenceType;
                        def.actorKey = playDummyAnimation.ActorKey;
                        def.animationName = playDummyAnimation.AnimationName;
                        def.loop = playDummyAnimation.Loop;
                        def.timeScale = Mathf.Max(0f, playDummyAnimation.TimeScale);
                        def.durationPolicy = playDummyAnimation.DurationPolicy;
                        def.endPolicy = playDummyAnimation.EndPolicy;
                        def.endAnimationName = playDummyAnimation.EndAnimationName;
                        def.endAnimationLoop = playDummyAnimation.EndAnimationLoop;
                        def.endAnimationTimeScale = Mathf.Max(0f, playDummyAnimation.EndAnimationTimeScale);
                        def.missingActorPolicy = playDummyAnimation.MissingActorPolicy;
                        return def;
                    };

                default:
                    return null;
            }
        }
    }
}
