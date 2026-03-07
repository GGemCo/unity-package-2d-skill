using GGemCo2DCore;
using UnityEngine;

namespace GGemCo2DSkill
{
    public sealed class QuickSlotSkillPassiveContentProvider : IQuickSlotContentProvider
    {
        public int Priority => 100;

        private TableSkillPassive _tableSkillPassive;

        public bool CanProvide(QuickSlotContentKind kind)
            => kind == QuickSlotContentKind.SkillPassive;

        public Sprite GetIconSprite(QuickSlotContentKind kind, int uid, int level, long instanceId)
        {
            if (uid <= 0) return null;
            if (AddressableLoaderSkill.Instance == null) return null;
            if (TableLoaderManagerSkill.Instance == null) return null;

            _tableSkillPassive ??= TableLoaderManagerSkill.Instance.TableSkillPassive;
            var info = _tableSkillPassive?.GetDataByUid(uid);
            if (info == null) return null;

            var path = info.IconFileName;
            if (string.IsNullOrEmpty(path)) return null;

            return AddressableLoaderSkill.Instance.GetSkillPassiveIconImageByName(path);
        }
    }
}