using Config;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 런타임 이벤트 타입을 해석하여 해당 실행 로직으로 전달합니다.
    /// </summary>
    internal sealed class SkillEventDispatcher
    {
        /// <summary>
        /// 이벤트 타입에 맞는 실행 메서드를 호출합니다.
        /// </summary>
        /// <param name="executor">이벤트를 실제로 처리할 스킬 실행기입니다.</param>
        /// <param name="context">이번 이벤트 실행에 필요한 컨텍스트입니다.</param>
        public void Execute(SkillExecutor executor, in SkillEventExecutionContext context)
        {
            if (executor == null)
                return;

            Object payload = context.Payload;
            SkillTargetContext targetContext = context.TargetContext;

            switch (context.Event.Type)
            {
                case ConfigCommonSkill.SkillEventType.Damage:
                    executor.HandleDamage(
                        context.Run,
                        context.Skill,
                        targetContext,
                        payload,
                        context.SnapshotCasterPosition,
                        context.SnapshotTargetPosition,
                        context.SnapshotGroundPoint,
                        Mathf.Max(0.05f, context.EventDurationSeconds));
                    break;

                case ConfigCommonSkill.SkillEventType.SpawnVfx:
                    executor.HandleVfx(
                        context.Run,
                        context.Skill,
                        targetContext,
                        payload,
                        context.SnapshotCasterPosition,
                        context.SnapshotTargetPosition,
                        context.SnapshotGroundPoint);
                    break;

                case ConfigCommonSkill.SkillEventType.ApplyAffect:
                    executor.HandleApplyStatus(
                        context.Skill,
                        targetContext,
                        payload,
                        context.SnapshotCasterPosition,
                        context.SnapshotTargetPosition,
                        context.SnapshotGroundPoint);
                    break;

                case ConfigCommonSkill.SkillEventType.PlayAudio:
                    executor.HandlePlayAudio(targetContext, payload, context.EventDurationSeconds);
                    break;

                case ConfigCommonSkill.SkillEventType.Camera:
                    executor.HandleCameraZoom(payload, context.EventDurationSeconds);
                    break;

                case ConfigCommonSkill.SkillEventType.Lunge:
                    executor.HandleLunge(context.Skill, targetContext, payload, context.EventDurationSeconds);
                    break;

                case ConfigCommonSkill.SkillEventType.Projectile:
                    executor.HandleProjectile(
                        context.Skill,
                        targetContext,
                        payload,
                        context.SnapshotCasterPosition,
                        context.SnapshotTargetPosition,
                        context.SnapshotGroundPoint);
                    break;

                case ConfigCommonSkill.SkillEventType.Laser:
                    executor.HandleLaser(
                        context.Run,
                        context.Skill,
                        targetContext,
                        payload,
                        context.SnapshotCasterPosition,
                        context.SnapshotTargetPosition,
                        context.SnapshotGroundPoint);
                    break;

                case ConfigCommonSkill.SkillEventType.PositionHold:
                    executor.HandlePositionHold(context.Run, targetContext, payload, context.EventDurationSeconds);
                    break;

                case ConfigCommonSkill.SkillEventType.MovementControlLock:
                    executor.HandleMovementControlLock(context.Run, targetContext, payload, context.EventDurationSeconds);
                    break;

                case ConfigCommonSkill.SkillEventType.CaptureTargetPosition:
                    executor.HandleCaptureTargetPosition(
                        context.Run,
                        context.Skill,
                        targetContext,
                        payload,
                        context.SnapshotCasterPosition,
                        context.SnapshotTargetPosition,
                        context.SnapshotGroundPoint);
                    break;

                case ConfigCommonSkill.SkillEventType.GroundSlam:
                    executor.HandleGroundSlam(targetContext, payload, context.EventDurationSeconds);
                    break;

                case ConfigCommonSkill.SkillEventType.ApplyTempHp:
                    executor.HandleApplyTempHp(context.Skill, targetContext, payload);
                    break;

                case ConfigCommonSkill.SkillEventType.ScreenFade:
                    executor.HandleScreenFade(payload, context.EventDurationSeconds);
                    break;

                case ConfigCommonSkill.SkillEventType.CasterFade:
                    executor.HandleCasterFade(targetContext, payload, context.EventDurationSeconds);
                    break;

                case ConfigCommonSkill.SkillEventType.Afterimage:
                    executor.HandleAfterimage(targetContext, payload, context.EventDurationSeconds);
                    break;

                case ConfigCommonSkill.SkillEventType.SpawnDummyCharacter:
                    executor.HandleSpawnDummyCharacter(
                        context.Run,
                        context.Skill,
                        targetContext,
                        payload,
                        context.SnapshotCasterPosition,
                        context.SnapshotTargetPosition,
                        context.SnapshotGroundPoint);
                    break;

                case ConfigCommonSkill.SkillEventType.MoveDummyCharacter:
                    executor.HandleMoveDummyCharacter(
                        context.Run,
                        targetContext,
                        payload,
                        context.SnapshotTargetPosition,
                        context.SnapshotGroundPoint);
                    break;

                case ConfigCommonSkill.SkillEventType.DespawnDummyCharacter:
                    executor.HandleDespawnDummyCharacter(payload);
                    break;

                case ConfigCommonSkill.SkillEventType.SetDummyAirborneState:
                    executor.HandleSetDummyAirborneState(targetContext, payload);
                    break;

                case ConfigCommonSkill.SkillEventType.PlayDummyCharacterAnimation:
                    executor.HandlePlayDummyCharacterAnimation(targetContext, payload, context.EventDurationSeconds);
                    break;
            }
        }
    }
}
