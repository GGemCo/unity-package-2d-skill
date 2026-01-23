using GGemCo2DCoreEditor;
using GGemCo2DSkill;

namespace GGemCo2DSkillEditor
{
    public class TableLoaderManagerSkill : TableLoaderManagerBase
    {

        public static TableSkill LoadTableSkill(bool forceReload = false)
        {
            return LoadTable<TableSkill>(ConfigAddressableTableSkill.TableSkill.Path, forceReload);
        }
    }
}