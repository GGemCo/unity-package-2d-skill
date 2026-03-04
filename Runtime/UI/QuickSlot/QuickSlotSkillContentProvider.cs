using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    /// <summary>
    /// 퀵슬롯 스킬 아이콘 제공자(Skill 패키지).
    /// </summary>
    public sealed class QuickSlotSkillContentProvider : IQuickSlotContentProvider
    {
        public int Priority => 100;

        private TableSkill _tableSkill;

        public bool CanProvide(QuickSlotContentKind kind) => kind == QuickSlotContentKind.Skill;

        public Sprite GetIconSprite(QuickSlotContentKind kind, int uid, int level, long instanceId)
        {
            if (uid <= 0) return null;
            if (AddressableLoaderSkill.Instance == null) return null;
            if (TableLoaderManagerSkill.Instance == null) return null;

            _tableSkill ??= TableLoaderManagerSkill.Instance.TableSkill;
            var info = _tableSkill?.GetDataByUid(uid);
            if (info == null) return null;

            var path = info.IconFileName;
            if (string.IsNullOrEmpty(path)) return null;

            return AddressableLoaderSkill.Instance.GetImageIconByName(path);
        }
    }
}
