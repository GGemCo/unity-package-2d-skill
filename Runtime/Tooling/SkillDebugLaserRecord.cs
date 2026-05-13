#if UNITY_EDITOR
using System;
using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// Skill 테스트/디버그용 레이저 범위 표시 데이터입니다.
    /// </summary>
    [Serializable]
    public sealed class SkillDebugLaserRecord
    {
        public int casterInstanceId;
        public float expireTime;
        public Vector3 start;
        public Vector3 end;
        public bool hasBlockHit;
        public Vector3 blockPoint;
        public Vector3 raycastDirection;
        public Vector3 visualDirection;
        public LaserConstants.VfxAngleSyncMode vfxAngleSyncMode;

        /// <summary>
        /// 레이저 디버그 기록을 생성합니다.
        /// </summary>
        /// <param name="start">레이저 시작점입니다.</param>
        /// <param name="end">레이저 종료점입니다.</param>
        /// <param name="durationSeconds">기즈모 유지 시간입니다.</param>
        /// <param name="caster">레이저를 생성한 캐스터 오브젝트입니다.</param>
        /// <param name="hasBlockHit">차단 지점 존재 여부입니다.</param>
        /// <param name="blockPoint">차단 지점입니다.</param>
        /// <param name="raycastDirection">레이캐스트 기준 방향입니다.</param>
        /// <param name="visualDirection">시각 회전 가이드 방향입니다.</param>
        /// <param name="vfxAngleSyncMode">적용된 VFX 각도 동기화 모드입니다.</param>
        /// <returns>생성된 레이저 디버그 기록입니다.</returns>
        public static SkillDebugLaserRecord Create(
            Vector3 start,
            Vector3 end,
            float durationSeconds,
            GameObject caster,
            bool hasBlockHit,
            Vector3 blockPoint,
            Vector3 raycastDirection,
            Vector3 visualDirection,
            LaserConstants.VfxAngleSyncMode vfxAngleSyncMode)
        {
            return new SkillDebugLaserRecord
            {
                casterInstanceId = caster != null ? caster.GetInstanceID() : 0,
                expireTime = Time.time + Mathf.Max(0.01f, durationSeconds),
                start = start,
                end = end,
                hasBlockHit = hasBlockHit,
                blockPoint = blockPoint,
                raycastDirection = raycastDirection.sqrMagnitude > 1e-6f ? raycastDirection.normalized : Vector3.right,
                visualDirection = visualDirection.sqrMagnitude > 1e-6f ? visualDirection.normalized : Vector3.right,
                vfxAngleSyncMode = vfxAngleSyncMode,
            };
        }
    }
}
#endif
