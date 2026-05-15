using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트로 생성된 더미 캐릭터를 식별하기 위한 마커 컴포넌트입니다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SkillDummyCharacterMarker : MonoBehaviour
    {
        [SerializeField] private string actorKey;
        [SerializeField] private int ownerSkillUid;

        /// <summary>
        /// 더미 캐릭터 식별 키를 반환합니다.
        /// </summary>
        public string ActorKey => actorKey;

        /// <summary>
        /// 더미 캐릭터를 생성한 스킬 UID를 반환합니다.
        /// </summary>
        public int OwnerSkillUid => ownerSkillUid;

        /// <summary>
        /// 더미 캐릭터 마커에 런타임 식별 정보를 기록합니다.
        /// </summary>
        /// <param name="key">더미 캐릭터를 식별할 actorKey입니다.</param>
        /// <param name="skillUid">더미 캐릭터를 생성한 스킬 UID입니다.</param>
        public void Bind(string key, int skillUid)
        {
            actorKey = key ?? string.Empty;
            ownerSkillUid = skillUid;
        }
    }
}
