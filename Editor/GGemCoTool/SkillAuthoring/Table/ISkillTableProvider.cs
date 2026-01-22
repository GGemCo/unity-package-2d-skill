namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// skill 테이블 접근 추상화.
    /// Core의 TableLoaderManager 또는 프로젝트별 로더에 의존하도록 어댑터를 제공한다.
    /// </summary>
    public interface ISkillTableProvider
    {
        bool TryGetTimelineAssetPath(int skillUid, out string timelineAssetPath);
    }
}
