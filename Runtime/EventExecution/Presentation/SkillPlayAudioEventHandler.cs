using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 오디오 이벤트의 Payload 검증과 SoundManager 재생 요청을 담당합니다.
    /// </summary>
    internal static class SkillPlayAudioEventHandler
    {
        /// <summary>
        /// 오디오 이벤트 정의에 따라 sound 테이블 UID 기반 사운드를 재생합니다.
        /// </summary>
        /// <param name="ctx">스킬 실행 대상 컨텍스트입니다. 현재는 향후 위치 기반 사운드 확장을 위해 유지합니다.</param>
        /// <param name="payloadObj">Bake된 오디오 이벤트 정의입니다.</param>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        public static void Handle(SkillTargetContext ctx, Object payloadObj, float eventDurationSeconds)
        {
            if (payloadObj is not PlayAudioEventDefinition def)
                return;
            if (def.soundUid <= 0)
                return;

            SceneGame sceneGame = SceneGame.Instance;
            if (sceneGame == null || sceneGame.soundManager == null)
                return;

            // 사운드 해석, Addressables 로드, 풀 관리, 볼륨/피치 정책은 Core SoundManager가 일괄 처리합니다.
            sceneGame.soundManager.PlayByUid(def.soundUid, def.loop, eventDurationSeconds);
        }
    }
}
