using UnityEngine;
using GGemCo2DCore;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 Timeline에서 Bake된 사운드 재생 이벤트 정의입니다.
    /// </summary>
    public sealed class PlayAudioEventDefinition : ScriptableObject
    {
        /// <summary>
        /// 재생할 sound 테이블의 대표 UID입니다.
        /// </summary>
        public int soundUid;

        /// <summary>
        /// Timeline Clip 구간 동안 오디오를 반복 재생할지 여부입니다.
        /// </summary>
        public bool loop;

        /// <summary>
        /// 공용 사운드 재생 요청입니다. 신규 Bake 데이터는 이 값을 우선 사용합니다.
        /// </summary>
        public SoundPlayRequest soundRequest = new SoundPlayRequest();

        /// <summary>
        /// 현재 정의에서 사용할 사운드 재생 요청을 계산합니다.
        /// </summary>
        /// <param name="eventDurationSeconds">Timeline Clip 길이에서 계산된 이벤트 지속 시간입니다.</param>
        /// <returns>런타임 사운드 매니저에 전달할 요청입니다.</returns>
        public SoundPlayRequest ResolveRequest(float eventDurationSeconds)
        {
            if (soundRequest != null && soundRequest.IsValid)
            {
                return soundRequest.loop
                    ? soundRequest.CloneWithDuration(eventDurationSeconds)
                    : soundRequest.Clone();
            }

            return SoundPlayRequest.Create(
                soundUid,
                loop,
                useLoopOverride: true,
                durationSeconds: eventDurationSeconds,
                useDurationOverride: loop);
        }
    }
}
