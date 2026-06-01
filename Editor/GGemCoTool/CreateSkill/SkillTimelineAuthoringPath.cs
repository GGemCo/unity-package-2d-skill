using System;
using System.Collections.Generic;
using System.IO;
using GGemCo2DCore;
using UnityEditor;
using UnityEngine.Timeline;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 스킬 제작용 Timeline 원본 에셋의 경로와 파일명 규칙을 관리하는 Editor 전용 유틸리티입니다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 런타임 Bake 결과인 <c>SkillRuntimeSequence</c>와 원본 Timeline 에셋의 책임을 분리하기 위해,
    /// 원본 Timeline은 Player/Monster 소스와 Skill UID를 기준으로 결정합니다.
    /// </para>
    /// <para>
    /// 자동 선택 로직은 이 유틸리티만 참조하도록 하여, 향후 폴더 구조나 파일명 규칙이 변경되어도
    /// <c>CreateSkillWindow</c>의 UI 로직 변경 범위를 최소화합니다.
    /// </para>
    /// </remarks>
    internal static class SkillTimelineAuthoringPath
    {
        /// <summary>
        /// 플레이어 스킬 Timeline 원본 에셋을 저장하는 기본 폴더입니다.
        /// </summary>
        public const string PlayerFolder = "Assets/Editor/Skill/Player";

        /// <summary>
        /// 몬스터 스킬 Timeline 원본 에셋을 저장하는 기본 폴더입니다.
        /// </summary>
        public const string MonsterFolder = "Assets/Editor/Skill/Monster";

        /// <summary>
        /// Timeline 에셋 파일 확장자입니다.
        /// </summary>
        private const string TimelineExtension = ".playable";

        /// <summary>
        /// 선택한 스킬 테이블 소스에 대응하는 Timeline 원본 에셋 폴더를 반환합니다.
        /// </summary>
        /// <param name="source">스킬 테이블 소스입니다.</param>
        /// <returns>Unity 프로젝트 기준 Timeline 원본 에셋 폴더 경로입니다.</returns>
        public static string GetFolder(ConfigCommon.SkillTableSource source)
        {
            return source == ConfigCommon.SkillTableSource.Monster
                ? MonsterFolder
                : PlayerFolder;
        }

        /// <summary>
        /// 선택한 스킬 테이블 소스에 대응하는 파일명 소유자 문자열을 반환합니다.
        /// </summary>
        /// <param name="source">스킬 테이블 소스입니다.</param>
        /// <returns>파일명에 사용할 <c>Player</c> 또는 <c>Monster</c> 문자열입니다.</returns>
        public static string GetOwnerName(ConfigCommon.SkillTableSource source)
        {
            return source == ConfigCommon.SkillTableSource.Monster ? "Monster" : "Player";
        }

        /// <summary>
        /// 스킬 UID와 테이블 소스를 기준으로 Timeline 원본 에셋 파일명을 생성합니다.
        /// </summary>
        /// <param name="source">스킬 테이블 소스입니다.</param>
        /// <param name="skillUid">스킬 UID입니다.</param>
        /// <returns>규칙에 맞게 생성된 Timeline 원본 에셋 파일명입니다.</returns>
        public static string GetFileName(ConfigCommon.SkillTableSource source, int skillUid)
        {
            return $"TimelineSkill_{GetOwnerName(source)}_{skillUid}{TimelineExtension}";
        }

        /// <summary>
        /// 스킬 UID와 테이블 소스를 기준으로 Timeline 원본 에셋의 권장 전체 경로를 생성합니다.
        /// </summary>
        /// <param name="source">스킬 테이블 소스입니다.</param>
        /// <param name="skillUid">스킬 UID입니다.</param>
        /// <returns>Unity 프로젝트 기준 Timeline 원본 에셋 경로입니다.</returns>
        public static string GetAssetPath(ConfigCommon.SkillTableSource source, int skillUid)
        {
            return NormalizeAssetPath($"{GetFolder(source)}/{GetFileName(source, skillUid)}");
        }

        /// <summary>
        /// 권장 경로 또는 같은 소스 폴더 하위의 단일 후보를 기준으로 Timeline 원본 에셋을 찾습니다.
        /// </summary>
        /// <param name="source">스킬 테이블 소스입니다.</param>
        /// <param name="skillUid">스킬 UID입니다.</param>
        /// <param name="timeline">찾은 Timeline 에셋입니다.</param>
        /// <param name="assetPath">찾은 Timeline 에셋의 Unity 프로젝트 경로입니다.</param>
        /// <param name="candidateCount">권장 경로 외 후보 개수입니다. 중복 후보 경고에 사용합니다.</param>
        /// <returns>Timeline 에셋을 하나로 확정했으면 <see langword="true"/>를 반환합니다.</returns>
        public static bool TryFindTimeline(
            ConfigCommon.SkillTableSource source,
            int skillUid,
            out TimelineAsset timeline,
            out string assetPath,
            out int candidateCount)
        {
            timeline = null;
            assetPath = null;
            candidateCount = 0;

            if (skillUid <= 0)
                return false;

            string expectedPath = GetAssetPath(source, skillUid);
            timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(expectedPath);
            if (timeline != null)
            {
                assetPath = expectedPath;
                candidateCount = 1;
                return true;
            }

            string folder = GetFolder(source);
            if (!AssetDatabase.IsValidFolder(folder))
                return false;

            string expectedFileNameWithoutExtension = Path.GetFileNameWithoutExtension(GetFileName(source, skillUid));
            string[] guids = AssetDatabase.FindAssets($"{expectedFileNameWithoutExtension} t:TimelineAsset", new[] { folder });
            var candidatePaths = new List<string>();

            foreach (string guid in guids)
            {
                string path = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                if (!IsMatchingTimelinePath(source, skillUid, path))
                    continue;

                candidatePaths.Add(path);
            }

            candidateCount = candidatePaths.Count;
            if (candidatePaths.Count != 1)
                return false;

            assetPath = candidatePaths[0];
            timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(assetPath);
            return timeline != null;
        }

        /// <summary>
        /// 지정한 에셋 경로가 현재 파일명 규칙과 소스별 폴더 규칙에 맞는지 확인합니다.
        /// </summary>
        /// <param name="source">스킬 테이블 소스입니다.</param>
        /// <param name="skillUid">스킬 UID입니다.</param>
        /// <param name="assetPath">검사할 Unity 프로젝트 기준 에셋 경로입니다.</param>
        /// <returns>규칙에 맞는 Timeline 경로이면 <see langword="true"/>를 반환합니다.</returns>
        private static bool IsMatchingTimelinePath(ConfigCommon.SkillTableSource source, int skillUid, string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return false;

            string normalizedPath = NormalizeAssetPath(assetPath);
            string normalizedFolder = NormalizeAssetPath(GetFolder(source));
            if (!normalizedPath.StartsWith($"{normalizedFolder}/", StringComparison.OrdinalIgnoreCase))
                return false;

            string expectedFileName = GetFileName(source, skillUid);
            return string.Equals(Path.GetFileName(normalizedPath), expectedFileName, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Unity 에셋 경로 비교가 안정적으로 동작하도록 경로 구분자를 정규화합니다.
        /// </summary>
        /// <param name="assetPath">정규화할 에셋 경로입니다.</param>
        /// <returns>슬래시 구분자로 정규화된 에셋 경로입니다.</returns>
        private static string NormalizeAssetPath(string assetPath)
        {
            return string.IsNullOrWhiteSpace(assetPath)
                ? string.Empty
                : assetPath.Replace('\\', '/').TrimEnd('/');
        }
    }
}
