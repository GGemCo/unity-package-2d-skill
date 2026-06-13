using System.Collections.Generic;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Skill RuntimeSequence Payload가 직접 사용하는 sound UID를 외부 분석기에 제공하는 계약입니다.
    /// </summary>
    /// <remarks>
    /// 신규 스킬 이벤트에 사운드 필드가 추가되면 이 인터페이스를 구현하여
    /// Core의 사운드 사용 매니페스트 생성 과정이 별도 리플렉션 없이 사용처를 수집할 수 있습니다.
    /// </remarks>
    public interface ISkillSoundUsageProvider
    {
        /// <summary>
        /// 현재 Payload가 실제 런타임에서 사용할 수 있는 sound UID를 결과 컬렉션에 추가합니다.
        /// </summary>
        /// <param name="target">발견한 sound UID를 추가할 결과 컬렉션입니다.</param>
        void CollectSoundUids(ICollection<int> target);
    }
}
