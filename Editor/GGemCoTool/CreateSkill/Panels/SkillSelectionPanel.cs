using System;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// 상단 툴바와 스킬 선택 영역 UI를 조립합니다.
    /// </summary>
    public sealed class SkillSelectionPanel
    {
        public Toolbar BuildToolbar(
            SkillAuthoringState state,
            Action<bool> onForceReloadChanged,
            Action<SkillAuthoringTableKind> onTableKindChanged,
            Action onReload)
        {
            var toolbar = new Toolbar();

            var forceReload = new Toggle("ForceReload") { value = false };
            forceReload.RegisterValueChangedCallback(evt => onForceReloadChanged?.Invoke(evt.newValue));
            toolbar.Add(forceReload);

            var tableKindField = new EnumField("Table", state.TableKind);
            tableKindField.RegisterValueChangedCallback(evt =>
            {
                onTableKindChanged?.Invoke((SkillAuthoringTableKind)evt.newValue);
            });
            toolbar.Add(tableKindField);

            var reloadButton = new Button(() => onReload?.Invoke()) { text = "Reload" };
            toolbar.Add(reloadButton);
            return toolbar;
        }

        public VisualElement BuildSkillSelectionBar(Action onSelectSkill)
        {
            var root = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Column,
                    marginTop = 6,
                    marginBottom = 6,
                    flexGrow = 1,
                }
            };

            var button = new Button(() => onSelectSkill?.Invoke())
            {
                text = "스킬 선택",
                style = { marginRight = 8 }
            };
            root.Add(button);

            var label = new Label("선택된 스킬: (없음)")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    flexGrow = 1,
                }
            };
            label.name = "selected-skill-label";
            root.Add(label);

            return root;
        }
    }
}
