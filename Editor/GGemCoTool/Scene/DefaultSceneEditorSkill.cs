using GGemCo2DCore;
using GGemCo2DCoreEditor;

namespace GGemCo2DSkillEditor
{
    public class DefaultSceneEditorSkill : DefaultSceneEditor
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            packageType = ConfigPackageInfo.PackageType.Skill;
        }
    }
}