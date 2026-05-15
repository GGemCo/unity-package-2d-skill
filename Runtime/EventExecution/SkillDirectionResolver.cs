using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 스킬 이벤트 실행 중 사용하는 2D 방향 벡터를 공통 규칙으로 보정합니다.
    /// </summary>
    internal static class SkillDirectionResolver
    {
        /// <summary>
        /// 2D 기준으로 사용할 전방 벡터를 보정합니다.
        /// 입력 전방이 비어 있거나 Z축 기준 기본값에 가까우면 캐스터의 좌우 방향을 사용합니다.
        /// </summary>
        /// <param name="caster">방향 보정 기준이 되는 캐스터 오브젝트입니다.</param>
        /// <param name="forward">원본 전방 벡터입니다.</param>
        /// <returns>Z가 제거되고 2D 기준으로 정규화된 전방 벡터입니다.</returns>
        public static Vector3 ResolveForward2D(GameObject caster, Vector3 forward)
        {
            var f2 = new Vector2(forward.x, forward.y);
            if (f2.sqrMagnitude < 1e-6f || Mathf.Abs(forward.z) > 0.5f)
            {
                float sign = 1f;
                if (caster != null)
                {
                    sign = Mathf.Sign(caster.transform.localScale.x);
                    if (Mathf.Approximately(sign, 0f))
                        sign = 1f;
                }

                return new Vector3(sign, 0f, 0f);
            }

            f2.Normalize();
            return new Vector3(f2.x, f2.y, 0f);
        }

        /// <summary>
        /// 캐릭터의 현재 바라보기 상태를 우선 사용하고, 없으면 Transform 스케일을 기준으로 좌우 방향을 계산합니다.
        /// </summary>
        /// <param name="caster">방향을 확인할 캐스터 오브젝트입니다.</param>
        /// <returns>현재 캐스터가 바라보는 2D 방향입니다.</returns>
        public static Vector2 ResolveCurrentFacing2D(GameObject caster)
        {
            if (caster == null)
                return Vector2.right;

            CharacterBase characterBase = caster.GetComponent<CharacterBase>();
            if (characterBase != null)
            {
                Vector2 facing = CharacterConstants.FacingToVector2(characterBase.CurrentFacing);
                if (facing.sqrMagnitude > 1e-6f)
                    return facing.normalized;
            }

            float sign = Mathf.Sign(caster.transform.localScale.x);
            if (Mathf.Approximately(sign, 0f))
                sign = 1f;

            return new Vector2(sign, 0f);
        }
    }
}
