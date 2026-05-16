using GGemCo2DSkill;
using UnityEngine;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Damage Timeline 클립을 DamageEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillDamagePayloadFactory : SkillPayloadFactoryBase<SkillDamageClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillDamageClip dmg)
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
            def.isGroundOnly = dmg.IsGroundOnly;
            def.isAirOnly = dmg.IsAirOnly;
            def.facingDamagePolicy = dmg.FacingDamagePolicy;
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
        }
    }

    /// <summary>
    /// SpawnVfx Timeline 클립을 VfxEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillSpawnVfxPayloadFactory : SkillPayloadFactoryBase<SkillSpawnVfxClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillSpawnVfxClip fx)
        {
            var def = ScriptableObject.CreateInstance<VfxEventDefinition>();
            var spawnAnchor = SkillPayloadBakeUtility.ConvertVfxSpawnAnchor(fx.Anchor);
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
        }
    }

    /// <summary>
    /// ApplyAffect Timeline 클립을 ApplyStatusEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillApplyAffectPayloadFactory : SkillPayloadFactoryBase<SkillApplyAffectClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillApplyAffectClip aff)
        {
            var def = ScriptableObject.CreateInstance<ApplyStatusEventDefinition>();
            def.statusId = new StatusVfxId { id = aff.AffectUid.ToString() };
            def.durationOverrideSeconds = aff.AffectDuration;
            SkillPayloadBakeUtility.TrySetApplyStatusApplyTo(def, (int)aff.ApplyTo);
            return def;
        }
    }

    /// <summary>
    /// ApplyTempHp Timeline 클립을 ApplyTempHpEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillApplyTempHpPayloadFactory : SkillPayloadFactoryBase<SkillApplyTempHpClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillApplyTempHpClip tempHp)
        {
            var def = ScriptableObject.CreateInstance<ApplyTempHpEventDefinition>();
            def.tempHpValue = tempHp.TempHpValue > 0 ? tempHp.TempHpValue : 0;
            def.sourceKeyOverride = tempHp.SourceKeyOverride;
            def.applyTo = tempHp.ApplyTo;
            return def;
        }
    }

    /// <summary>
    /// PlayAudio Timeline 클립을 PlayAudioEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillPlayAudioPayloadFactory : SkillPayloadFactoryBase<SkillPlayAudioClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillPlayAudioClip audio)
        {
            var def = ScriptableObject.CreateInstance<PlayAudioEventDefinition>();
            def.clip = audio.Clip;
            def.volume = Mathf.Clamp01(audio.Volume);
            return def;
        }
    }

    /// <summary>
    /// ScreenFade Timeline 클립을 SkillScreenFadeEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillScreenFadePayloadFactory : SkillPayloadFactoryBase<SkillScreenFadeClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillScreenFadeClip screenFade)
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
        }
    }

    /// <summary>
    /// Afterimage Timeline 클립을 SkillAfterimageEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillAfterimagePayloadFactory : SkillPayloadFactoryBase<SkillAfterimageClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillAfterimageClip afterimage)
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
        }
    }

    /// <summary>
    /// Lunge Timeline 클립을 LungeEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillLungePayloadFactory : SkillPayloadFactoryBase<SkillLungeClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillLungeClip lunge)
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
        }
    }

    /// <summary>
    /// GroundSlam Timeline 클립을 GroundSlamEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillGroundSlamPayloadFactory : SkillPayloadFactoryBase<SkillGroundSlamClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillGroundSlamClip slam)
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
        }
    }

    /// <summary>
    /// PositionHold Timeline 클립을 PositionHoldEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillPositionHoldPayloadFactory : SkillPayloadFactoryBase<SkillPositionHoldClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillPositionHoldClip positionHold)
        {
            var def = ScriptableObject.CreateInstance<PositionHoldEventDefinition>();
            def.durationOverrideSeconds = Mathf.Max(0f, positionHold.DurationOverrideSeconds);
            def.durationPolicy = positionHold.DurationPolicy;
            def.stopAtEnd = positionHold.StopAtEnd;
            def.useMovePosition = positionHold.UseMovePosition;
            def.allowReplace = positionHold.AllowReplace;
            return def;
        }
    }

    /// <summary>
    /// ArcLunge Timeline 클립을 ArcLungeEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillArcLungePayloadFactory : SkillPayloadFactoryBase<SkillArcLungeClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillArcLungeClip arcLunge)
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
        }
    }

    /// <summary>
    /// Projectile Timeline 클립을 ProjectileEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillProjectilePayloadFactory : SkillPayloadFactoryBase<SkillProjectileClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillProjectileClip proj)
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
        }
    }

    /// <summary>
    /// Laser Timeline 클립을 LaserEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillLaserPayloadFactory : SkillPayloadFactoryBase<SkillLaserClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillLaserClip laser)
        {
            var def = ScriptableObject.CreateInstance<LaserEventDefinition>();
            def.laserUid = laser.LaserUid;
            def.damageType = laser.DamageType;
            def.damage = laser.Damage;
            def.durationSeconds = Mathf.Max(0f, laser.DurationSeconds);
            def.damageStartDelaySeconds = Mathf.Max(0f, laser.DamageStartDelaySeconds);
            def.damageActiveDurationSeconds = SkillPayloadBakeUtility.NormalizeLaserDamageActiveDuration(laser.DamageActiveDurationSeconds);
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
        }
    }

    /// <summary>
    /// SpawnDummyCharacter Timeline 클립을 SpawnDummyCharacterEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillSpawnDummyCharacterPayloadFactory : SkillPayloadFactoryBase<SkillSpawnDummyCharacterClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillSpawnDummyCharacterClip spawnDummy)
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
        }
    }

    /// <summary>
    /// MoveDummyCharacter Timeline 클립을 MoveDummyCharacterEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillMoveDummyCharacterPayloadFactory : SkillPayloadFactoryBase<SkillMoveDummyCharacterClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillMoveDummyCharacterClip moveDummy)
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
        }
    }

    /// <summary>
    /// DespawnDummyCharacter Timeline 클립을 DespawnDummyCharacterEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillDespawnDummyCharacterPayloadFactory : SkillPayloadFactoryBase<SkillDespawnDummyCharacterClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillDespawnDummyCharacterClip despawnDummy)
        {
            var def = ScriptableObject.CreateInstance<DespawnDummyCharacterEventDefinition>();
            def.actorKey = despawnDummy.ActorKey;
            def.fadeOutEnabled = despawnDummy.FadeOutEnabled;
            def.fadeOutDurationSeconds = Mathf.Max(0f, despawnDummy.FadeOutDurationSeconds);
            def.destroyAfterFade = despawnDummy.DestroyAfterFade;
            def.missingActorPolicy = despawnDummy.MissingActorPolicy;
            return def;
        }
    }

    /// <summary>
    /// SetDummyAirborneState Timeline 클립을 SetDummyAirborneStateEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillSetDummyAirborneStatePayloadFactory : SkillPayloadFactoryBase<SkillSetDummyAirborneStateClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillSetDummyAirborneStateClip airborneDummy)
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
        }
    }

    /// <summary>
    /// PlayDummyCharacterAnimation Timeline 클립을 PlayDummyCharacterAnimationEventDefinition Payload로 변환합니다.
    /// </summary>
    internal sealed class SkillPlayDummyCharacterAnimationPayloadFactory : SkillPayloadFactoryBase<SkillPlayDummyCharacterAnimationClip>
    {
        /// <inheritdoc />
        protected override UnityEngine.Object CreatePayload(SkillPlayDummyCharacterAnimationClip playDummyAnimation)
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
        }
    }
}
