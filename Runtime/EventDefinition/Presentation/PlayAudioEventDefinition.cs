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
    }
}
