using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 차징 중 재생되는 전체 사운드와 단계별 사운드의 수명주기를 관리합니다.
    /// </summary>
    internal sealed class SkillChargeSoundController
    {
        private SoundPlaybackHandle _chargeSoundHandle;
        private SoundPlaybackHandle _stageLoopHandle;

        /// <summary>
        /// 차징 전체 진행 사운드를 시작합니다.
        /// 루프 여부와 관계없이 재생 핸들을 보관하여 차징 완료, 실패, 취소 시점에 정리할 수 있게 합니다.
        /// </summary>
        /// <param name="charge">실행 중인 차징 정의입니다.</param>
        public void BeginCharge(RuntimeSkillChargeDefinition charge)
        {
            StopAll();

            if (charge == null || charge.ChargeSound == null || !charge.ChargeSound.IsValid)
                return;

            _chargeSoundHandle = PlayManagedSound(charge.ChargeSound);
        }

        /// <summary>
        /// 새 차징 단계 진입 사운드를 재생하고 이전 단계 루프 사운드를 정리합니다.
        /// </summary>
        /// <param name="stage">진입한 차징 단계 정의입니다.</param>
        public void EnterStage(RuntimeSkillChargeStageDefinition stage)
        {
            StopStageLoop();

            if (stage == null)
                return;

            PlayOneShot(stage.EnterSound);
        }

        /// <summary>
        /// 현재 차징 단계의 루프 구간 사운드를 시작합니다.
        /// 단계 루프 사운드는 루프 여부와 관계없이 단계 종료 시점에 정리됩니다.
        /// </summary>
        /// <param name="stage">루프 구간에 진입한 차징 단계 정의입니다.</param>
        public void BeginStageLoop(RuntimeSkillChargeStageDefinition stage)
        {
            StopStageLoop();

            if (stage == null || stage.LoopSound == null || !stage.LoopSound.IsValid)
                return;

            _stageLoopHandle = PlayManagedSound(stage.LoopSound);
        }

        /// <summary>
        /// 현재 단계 루프 구간 사운드를 정지합니다.
        /// </summary>
        public void StopStageLoop()
        {
            if (_stageLoopHandle == null)
                return;

            _stageLoopHandle.Stop();
            _stageLoopHandle = null;
        }

        /// <summary>
        /// 차징 관련 사운드를 모두 정지합니다.
        /// </summary>
        public void StopAll()
        {
            StopStageLoop();

            if (_chargeSoundHandle == null)
                return;

            _chargeSoundHandle.Stop();
            _chargeSoundHandle = null;
        }

        /// <summary>
        /// 재생 요청을 차징 수명에 맞춰 관리 가능한 형태로 SoundManager에 전달합니다.
        /// 루프 요청은 핸들 정지 전까지 유지하고, 비루프 요청도 핸들을 보관해 차징 종료 시 중단할 수 있게 합니다.
        /// </summary>
        /// <param name="request">재생할 사운드 요청입니다.</param>
        /// <returns>정지에 사용할 재생 핸들입니다. 재생할 수 없으면 <see langword="null"/>입니다.</returns>
        private static SoundPlaybackHandle PlayManagedSound(SoundPlayRequest request)
        {
            if (request == null || !request.IsValid)
                return null;

            SoundManager soundManager = ResolveSoundManager();
            if (soundManager == null)
                return null;

            return request.loop
                ? soundManager.Play(request.CloneLoopUntilHandleStopped())
                : soundManager.Play(request);
        }

        /// <summary>
        /// 사운드 매니저에 1회 재생 요청을 전달합니다.
        /// 단계 진입 사운드는 차징 수명으로 강제 정지하지 않는 짧은 피드백으로 취급합니다.
        /// </summary>
        /// <param name="request">재생할 사운드 요청입니다.</param>
        private static void PlayOneShot(SoundPlayRequest request)
        {
            if (request == null || !request.IsValid)
                return;

            SoundManager soundManager = ResolveSoundManager();
            soundManager?.Play(request);
        }

        /// <summary>
        /// 현재 게임 씬의 사운드 매니저를 안전하게 조회합니다.
        /// </summary>
        /// <returns>사용 가능한 사운드 매니저입니다. 없으면 <see langword="null"/>입니다.</returns>
        private static SoundManager ResolveSoundManager()
        {
            SceneGame sceneGame = SceneGame.Instance;
            return sceneGame != null ? sceneGame.soundManager : null;
        }
    }
}
