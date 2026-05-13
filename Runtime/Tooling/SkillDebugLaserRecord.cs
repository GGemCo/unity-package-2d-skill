#if UNITY_EDITOR
using System;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Skill 테스트/디버그용 레이저 범위 표시 데이터입니다.
    /// </summary>
    [Serializable]
    public sealed class SkillDebugLaserRecord
    {
        public int CasterInstanceId;
        public float ExpireTime;
        public Vector3 Start;
        public Vector3 End;
        public bool HasBlockHit;
        public Vector3 BlockPoint;

        /// <summary>
        /// 레이저 디버그 기록을 생성합니다.
        /// </summary>
        /// <param name="start">레이저 시작점입니다.</param>
        /// <param name="end">레이저 종료점입니다.</param>
        /// <param name="durationSeconds">기즈모 유지 시간입니다.</param>
        /// <param name="caster">레이저를 생성한 캐스터 오브젝트입니다.</param>
        /// <param name="hasBlockHit">차단 지점 존재 여부입니다.</param>
        /// <param name="blockPoint">차단 지점입니다.</param>
        /// <returns>생성된 레이저 디버그 기록입니다.</returns>
        public static SkillDebugLaserRecord Create(
            Vector3 start,
            Vector3 end,
            float durationSeconds,
            GameObject caster,
            bool hasBlockHit,
            Vector3 blockPoint)
        {
            return new SkillDebugLaserRecord
            {
                CasterInstanceId = caster != null ? caster.GetInstanceID() : 0,
                ExpireTime = Time.time + Mathf.Max(0.01f, durationSeconds),
                Start = start,
                End = end,
                HasBlockHit = hasBlockHit,
                BlockPoint = blockPoint,
            };
        }
    }
}
#endif
