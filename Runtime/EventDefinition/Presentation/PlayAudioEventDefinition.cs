using UnityEngine;

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
    }
}
