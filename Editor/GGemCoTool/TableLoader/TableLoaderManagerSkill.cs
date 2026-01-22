using GGemCo2DCoreEditor;
using GGemCo2DSkill;

namespace GGemCo2DSkillEditor
{
    public class TableLoaderManagerSkill : TableLoaderManagerBase
    {

        public static TableSkill LoadTableSkill()
        {
            return LoadTable<TableSkill>(ConfigAddressableTableSkill.TableSkill.Path);
        }
    }
}