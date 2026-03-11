using GGemCo2DAffectEditor;
using GGemCo2DCore;
using GGemCo2DCoreEditor;

namespace GGemCo2DAffectEditor
{
    public class DefaultEditorWindowSkill : DefaultEditorWindow
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            packageType = ConfigPackageInfo.PackageType.Skill;
        }
    }
}