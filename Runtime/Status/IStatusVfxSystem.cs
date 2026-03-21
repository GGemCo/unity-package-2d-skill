using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 상태 이상 시스템은 별도로 존재한다고 가정한다.
    /// 스킬은 적용 요청만 수행한다.
    /// </summary>
    public interface IStatusVfxSystem
    {
        void Apply(GameObject applier, GameObject target, StatusVfxId statusId, int stacks, float durationOverride, float chance01);
    }

    [System.Serializable]
    public struct StatusVfxId
    {
        public string id;
    }
}