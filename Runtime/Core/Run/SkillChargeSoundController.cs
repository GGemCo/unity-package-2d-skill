using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 차징 중 재생되는 전체 사운드와 단계별 사운드의 수명주기를 관리합니다.
    /// </summary>
    internal sealed class SkillChargeSoundController
    {
        private SoundPlaybackHandle _chargeLoopHandle;
        private SoundPlaybackHandle _stageLoopHandle;

        /// <summary>
        /// 차징 전체 진행 사운드를 시작합니다.
        /// 루프 사운드는 차징이 완료, 실패, 취소되기 전까지 핸들 기준으로 유지합니다.
        /// </summary>
        /// <param name="charge">실행 중인 차징 정의입니다.</param>
        public void BeginCharge(RuntimeSkillChargeDefinition charge)
        {
            StopAll();

            if (charge == null || charge.ChargeSound == null || !charge.ChargeSound.IsValid)
                return;

            _chargeLoopHandle = PlayManagedSound(charge.ChargeSound);
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
        /// 현재 차징 단계의 루프 사운드를 시작합니다.
        /// 루프 구간이 끝나거나 단계가 바뀌면 <see cref="StopStageLoop"/>로 정리합니다.
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
        /// 현재 단계 루프 사운드만 정지합니다.
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

            if (_chargeLoopHandle == null)
                return;

            _chargeLoopHandle.Stop();
            _chargeLoopHandle = null;
        }

        /// <summary>
        /// 루프 요청은 핸들 정지 전까지 유지하고, 비루프 요청은 1회 재생합니다.
        /// </summary>
        /// <param name="request">재생할 사운드 요청입니다.</param>
        /// <returns>루프 사운드 정지에 사용할 핸들입니다. 1회 재생이면 <see langword="null"/>입니다.</returns>
        private static SoundPlaybackHandle PlayManagedSound(SoundPlayRequest request)
        {
            if (request == null || !request.IsValid)
                return null;

            SoundManager soundManager = ResolveSoundManager();
            if (soundManager == null)
                return null;

            if (request.loop)
                return soundManager.Play(request.CloneLoopUntilHandleStopped());

            soundManager.Play(request);
            return null;
        }

        /// <summary>
        /// 사운드 매니저에 1회 재생 요청을 전달합니다.
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
