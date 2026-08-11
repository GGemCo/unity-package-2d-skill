using System.Collections.Generic;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// SkillExecutor의 실행 시작, 정상 종료, 취소, 비활성화 시점에 필요한 리소스 정리 절차를 담당합니다.
    /// </summary>
    internal static class SkillExecutorCleanupUtility
    {
        /// <summary>
        /// 새 스킬 실행을 시작하기 전에 이전 실행에서 남은 추적 리소스와 상태 컨트롤러를 초기화합니다.
        /// </summary>
        /// <param name="runner">코루틴 중단과 카메라 이동·줌·화면 페이드 소유자 식별에 사용할 실행기입니다.</param>
        /// <param name="vfxTracker">실행기가 소유한 VFX 추적기입니다.</param>
        /// <param name="dummyActors">더미 액터 레지스트리입니다.</param>
        /// <param name="casterActorHandle">Caster 참조에 재사용하는 임시 더미 핸들입니다.</param>
        /// <param name="attackSequence">공격 식별자와 연계 해제 상태 관리자입니다.</param>
        /// <param name="cameraMoveController">카메라 이동 복귀 정책 관리자입니다.</param>
        /// <param name="cameraZoomController">카메라 줌 복귀 정책 관리자입니다.</param>
        /// <param name="screenFadeController">화면 페이드 정리 정책 관리자입니다.</param>
        /// <param name="casterFadeController">캐스터 페이드 정리 정책 관리자입니다.</param>
        /// <param name="afterimageController">캐릭터 잔상 정리 정책 관리자입니다.</param>
        /// <param name="groundSlamAnimationController">그라운드슬램 애니메이션 상태 관리자입니다.</param>
        /// <param name="arcLungeAnimationController">아크 런지 애니메이션 상태 관리자입니다.</param>
        public static void PrepareForNewRun(
            MonoBehaviour runner,
            SkillOwnedVfxTracker vfxTracker,
            Dictionary<string, SkillDummyActorHandle> dummyActors,
            SkillDummyActorHandle casterActorHandle,
            SkillAttackSequence attackSequence,
            SkillCameraMoveController cameraMoveController,
            SkillCameraZoomController cameraZoomController,
            SkillScreenFadeController screenFadeController,
            SkillCasterFadeController casterFadeController,
            SkillAfterimageController afterimageController,
            SkillGroundSlamAnimationController groundSlamAnimationController,
            SkillArcLungeAnimationController arcLungeAnimationController)
        {
            vfxTracker?.Cleanup();
            CleanupDummyActors(runner, dummyActors, forceAll: true, forCancel: false);
            SkillDummyActorReferenceUtility.ResetCasterTransientState(runner, casterActorHandle, clearCharacter: true);
            attackSequence?.Clear();
            cameraMoveController?.ResetCleanupFlags();
            cameraZoomController?.ResetCleanupFlags();
            screenFadeController?.ResetCleanupFlags();
            casterFadeController?.Cleanup(runner, forCancel: false, forceRestore: true);
            afterimageController?.ResetCleanupFlags();
            groundSlamAnimationController?.Clear();
            arcLungeAnimationController?.Clear();
        }

        /// <summary>
        /// 실행기가 비활성화될 때, 현재 실행 여부와 무관하게 남아 있으면 안 되는 더미와 잔상 상태를 정리합니다.
        /// </summary>
        /// <param name="runner">코루틴 중단에 사용할 실행기입니다.</param>
        /// <param name="dummyActors">더미 액터 레지스트리입니다.</param>
        /// <param name="casterActorHandle">Caster 참조에 재사용하는 임시 더미 핸들입니다.</param>
        /// <param name="cameraMoveController">카메라 이동 복귀 정책 관리자입니다.</param>
        /// <param name="cameraZoomController">카메라 줌 복귀 정책 관리자입니다.</param>
        /// <param name="casterFadeController">캐스터 페이드 정리 정책 관리자입니다.</param>
        /// <param name="afterimageController">캐릭터 잔상 정리 정책 관리자입니다.</param>
        public static void CleanupOnDisable(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> dummyActors,
            SkillDummyActorHandle casterActorHandle,
            SkillCameraMoveController cameraMoveController,
            SkillCameraZoomController cameraZoomController,
            SkillCasterFadeController casterFadeController,
            SkillAfterimageController afterimageController)
        {
            CleanupDummyActors(runner, dummyActors, forceAll: true, forCancel: false);
            SkillDummyActorReferenceUtility.ResetCasterTransientState(runner, casterActorHandle, clearCharacter: true);
            cameraMoveController?.Cleanup(runner, forCancel: false, forceRestore: true);
            cameraZoomController?.Cleanup(runner, forCancel: false, forceRestore: true);
            casterFadeController?.Cleanup(runner, forCancel: false, forceRestore: true);
            afterimageController?.Cleanup(forCancel: false);
        }

        /// <summary>
        /// 스킬이 정상 종료되었을 때 종료 정책에 따라 더미, 페이드, 잔상, 공격 시퀀스 상태를 정리합니다.
        /// </summary>
        /// <param name="runner">코루틴 중단과 카메라 이동·줌·화면 페이드 소유자 식별에 사용할 실행기입니다.</param>
        /// <param name="dummyActors">더미 액터 레지스트리입니다.</param>
        /// <param name="attackSequence">공격 식별자와 연계 해제 상태 관리자입니다.</param>
        /// <param name="cameraMoveController">카메라 이동 복귀 정책 관리자입니다.</param>
        /// <param name="cameraZoomController">카메라 줌 복귀 정책 관리자입니다.</param>
        /// <param name="screenFadeController">화면 페이드 정리 정책 관리자입니다.</param>
        /// <param name="casterFadeController">캐스터 페이드 정리 정책 관리자입니다.</param>
        /// <param name="afterimageController">캐릭터 잔상 정리 정책 관리자입니다.</param>
        public static void CleanupOnRunEnd(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> dummyActors,
            SkillAttackSequence attackSequence,
            SkillCameraMoveController cameraMoveController,
            SkillCameraZoomController cameraZoomController,
            SkillScreenFadeController screenFadeController,
            SkillCasterFadeController casterFadeController,
            SkillAfterimageController afterimageController)
        {
            attackSequence?.Clear();
            CleanupDummyActors(runner, dummyActors, forceAll: false, forCancel: false);
            cameraMoveController?.Cleanup(runner, forCancel: false);
            cameraZoomController?.Cleanup(runner, forCancel: false);
            screenFadeController?.Cleanup(runner, forCancel: false);
            casterFadeController?.Cleanup(runner, forCancel: false);
            afterimageController?.Cleanup(forCancel: false);
        }

        /// <summary>
        /// 대시, 회피, 프로젝트 전용 입력 전환처럼 스킬이 아닌 외부 액션이 즉시 이어질 때 남은 스킬 모션과 특수 애니메이션 상태를 정리합니다.
        /// </summary>
        /// <remarks>
        /// 정상 종료 후 착지 연출을 재생하는 ArcLunge/GroundSlam 컨트롤러가 외부 액션 애니메이션을 덮지 않도록,
        /// 외부 액션이 Animator를 사용하기 직전에 Skill 채널 모션과 후속 애니메이션 상태만 제거합니다.
        /// </remarks>
        /// <param name="caster">잔여 스킬 모션을 정리할 캐스터 오브젝트입니다.</param>
        /// <param name="groundSlamAnimationController">그라운드슬램 후속 애니메이션 상태 관리자입니다.</param>
        /// <param name="arcLungeAnimationController">아크 런지 후속 애니메이션 상태 관리자입니다.</param>
        public static void CleanupForExternalActionOverride(
            GameObject caster,
            SkillGroundSlamAnimationController groundSlamAnimationController,
            SkillArcLungeAnimationController arcLungeAnimationController)
        {
            CancelResidualSkillMotion(caster);
            groundSlamAnimationController?.Clear();
            arcLungeAnimationController?.Clear();
        }

        /// <summary>
        /// 스킬이 취소될 때 즉시 중단되어야 하는 VFX, 더미, 페이드, 잔상, 특수 애니메이션 상태를 정리합니다.
        /// </summary>
        /// <param name="runner">코루틴 중단과 카메라 이동·줌·화면 페이드 소유자 식별에 사용할 실행기입니다.</param>
        /// <param name="caster">현재 스킬을 실행하던 캐스터 오브젝트입니다.</param>
        /// <param name="vfxTracker">실행기가 소유한 VFX 추적기입니다.</param>
        /// <param name="dummyActors">더미 액터 레지스트리입니다.</param>
        /// <param name="cameraMoveController">카메라 이동 복귀 정책 관리자입니다.</param>
        /// <param name="cameraZoomController">카메라 줌 복귀 정책 관리자입니다.</param>
        /// <param name="screenFadeController">화면 페이드 정리 정책 관리자입니다.</param>
        /// <param name="casterFadeController">캐스터 페이드 정리 정책 관리자입니다.</param>
        /// <param name="afterimageController">캐릭터 잔상 정리 정책 관리자입니다.</param>
        /// <param name="groundSlamAnimationController">그라운드슬램 애니메이션 상태 관리자입니다.</param>
        /// <param name="arcLungeAnimationController">아크 런지 애니메이션 상태 관리자입니다.</param>
        public static void CleanupForCancel(
            MonoBehaviour runner,
            GameObject caster,
            SkillOwnedVfxTracker vfxTracker,
            Dictionary<string, SkillDummyActorHandle> dummyActors,
            SkillCameraMoveController cameraMoveController,
            SkillCameraZoomController cameraZoomController,
            SkillScreenFadeController screenFadeController,
            SkillCasterFadeController casterFadeController,
            SkillAfterimageController afterimageController,
            SkillGroundSlamAnimationController groundSlamAnimationController,
            SkillArcLungeAnimationController arcLungeAnimationController)
        {
            ClearDamageAreaGizmo(caster);
            vfxTracker?.Cleanup();
            CleanupDummyActors(runner, dummyActors, forceAll: false, forCancel: true);
            cameraMoveController?.Cleanup(runner, forCancel: true);
            cameraZoomController?.Cleanup(runner, forCancel: true);
            screenFadeController?.Cleanup(runner, forCancel: true);
            casterFadeController?.Cleanup(runner, forCancel: true);
            afterimageController?.Cleanup(forCancel: true);
            groundSlamAnimationController?.Clear();
            arcLungeAnimationController?.Clear();
        }

        /// <summary>
        /// 현재 등록된 더미 캐릭터를 종료 정책에 맞게 정리합니다.
        /// </summary>
        /// <param name="runner">더미 코루틴 중단에 사용할 실행기입니다.</param>
        /// <param name="dummyActors">더미 액터 레지스트리입니다.</param>
        /// <param name="forceAll">모든 더미를 강제 정리할지 여부입니다.</param>
        /// <param name="forCancel">취소 종료 기준(<see langword="true"/>) 또는 정상 종료 기준(<see langword="false"/>)을 선택합니다.</param>
        private static void CleanupDummyActors(
            MonoBehaviour runner,
            Dictionary<string, SkillDummyActorHandle> dummyActors,
            bool forceAll,
            bool forCancel)
        {
            SkillDummyActorLifecycleUtility.Cleanup(runner, dummyActors, forceAll, forCancel);
        }

        /// <summary>
        /// 외부 액션 전환 직전에 Skill 채널에 남아 있는 이동 모션을 중단합니다.
        /// </summary>
        /// <param name="caster">모션 컨트롤러를 찾을 기준 캐스터 오브젝트입니다.</param>
        private static void CancelResidualSkillMotion(GameObject caster)
        {
            ICharacterMotionController motion = SkillCharacterComponentResolver.ResolveMotionController(caster);
            if (motion == null || !motion.IsPlaying(MotionChannel.Skill))
                return;

            motion.CancelMotion(MotionChannel.Skill, 3101);
        }

        /// <summary>
        /// 에디터 테스트 허브에 남아 있는 데미지 영역과 레이저 표시를 제거합니다.
        /// </summary>
        /// <param name="caster">정리 기준이 되는 캐스터 오브젝트입니다.</param>
        private static void ClearDamageAreaGizmo(GameObject caster)
        {
#if UNITY_EDITOR
            SkillTestRuntimeHub.Instance?.ClearDamageAreas(caster);
            SkillTestRuntimeHub.Instance?.ClearLasers(caster);
#endif
        }
    }
}
