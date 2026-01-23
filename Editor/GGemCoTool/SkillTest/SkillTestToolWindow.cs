using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GGemCo2DSkill;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace GGemCo2DSkillEditor
{
    /// <summary>
    /// Play Mode에서 몬스터를 스폰하고, skill 테이블(Uid) 기반으로 스킬을 실행하는 테스트 툴.
    /// </summary>
    public sealed class SkillTestToolWindow : EditorWindow
    {
        private const string Title = "스킬 테스트 툴";

        [MenuItem(ConfigEditorSkill.NameToolTestSkill, false, (int)ConfigEditorSkill.ToolOrdering.SettingTestSkill)]
        public static void Open()
        {
            var w = GetWindow<SkillTestToolWindow>();
            w.titleContent = new GUIContent(Title);
            w.minSize = new Vector2(900, 180);
        }

        private IReadOnlyList<MonsterTableProvider.MonsterRow> _monsters = Array.Empty<MonsterTableProvider.MonsterRow>();
        private List<MonsterTableProvider.MonsterRow> _filtered = new();

        private TextField _searchField;
        private Toggle _forceReloadToggle;

        private ListView _monsterListView;
        private ListView _spawnedListView;
        private ListView _skillListView;

        private Button _spawnButton;
        private Button _executeButton;

        private Label _playModeHint;

        private MonsterTableProvider.MonsterRow? _selectedMonsterRow;
        private GameObject _selectedSpawnedMonster;

        private readonly List<SkillRow> _skills = new();
        private int _selectedSkillUid;
        private bool _isSpawning;

        private readonly struct SkillRow
        {
            public readonly int Uid;
            public readonly string Name;

            public SkillRow(int uid, string name)
            {
                Uid = uid;
                Name = name;
            }

            public override string ToString()
                => string.IsNullOrEmpty(Name) ? Uid.ToString() : $"{Uid} - {Name}";
        }

        public void CreateGUI()
        {
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            var toolbar = new Toolbar();

            _searchField = new TextField("Search") { style = { flexGrow = 1 } };
            _searchField.RegisterValueChangedCallback(_ => RefreshMonsterFilter());
            toolbar.Add(_searchField);

            _forceReloadToggle = new Toggle("ForceReload") { value = false };
            toolbar.Add(_forceReloadToggle);

            var reloadBtn = new Button(() =>
            {
                LoadMonsterTable();
                LoadSkillTable();
            }) { text = "Reload" };
            toolbar.Add(reloadBtn);

            rootVisualElement.Add(toolbar);

            var content = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };

            // Left: monsters
            content.Add(BuildMonsterColumn());

            // Mid: spawned
            content.Add(BuildSpawnedColumn());

            // Right: skills
            content.Add(BuildSkillColumn());

            rootVisualElement.Add(content);

            _playModeHint = new Label();
            rootVisualElement.Add(_playModeHint);
            UpdatePlayModeHint();

            LoadMonsterTable();
            LoadSkillTable();
        }

        private VisualElement BuildMonsterColumn()
        {
            var col = new VisualElement { style = { flexGrow = 1, paddingRight = 6 } };

            col.Add(new Label("Monsters (table)") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            _monsterListView = new ListView
            {
                selectionType = SelectionType.Single,
                style = { flexGrow = 1 }
            };

            _monsterListView.makeItem = () => new Label();
            _monsterListView.bindItem = (ve, i) =>
            {
                var l = (Label)ve;
                l.text = _filtered[i].ToString();
            };
            _monsterListView.onSelectionChange += items =>
            {
                var it = items.FirstOrDefault();
                _selectedMonsterRow = it as MonsterTableProvider.MonsterRow?;
            };

            col.Add(_monsterListView);

            _spawnButton = new Button(async () =>
            {
                if (!Application.isPlaying) return;
                if (_selectedMonsterRow == null) return;
                await SpawnSelectedMonster(_selectedMonsterRow.Value.Uid);
            }) { text = "Spawn" };
            col.Add(_spawnButton);

            return col;
        }

        private VisualElement BuildSpawnedColumn()
        {
            var col = new VisualElement { style = { flexGrow = 1, paddingRight = 6 } };

            col.Add(new Label("Spawned (runtime)") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            _spawnedListView = new ListView
            {
                selectionType = SelectionType.Single,
                style = { flexGrow = 1 }
            };

            _spawnedListView.makeItem = () => new Label();
            _spawnedListView.bindItem = (ve, i) =>
            {
                var l = (Label)ve;
                var list = (List<GameObject>)_spawnedListView.itemsSource;
                var go = list[i];
                l.text = go != null ? go.name : "(null)";
            };

            _spawnedListView.onSelectionChange += items =>
            {
                var go = items.FirstOrDefault() as GameObject;
                _selectedSpawnedMonster = go;
                if (Application.isPlaying && SkillTestRuntimeHub.Instance != null)
                {
                    SkillTestRuntimeHub.Instance.SelectMonster(go);
                }
            };

            col.Add(_spawnedListView);

            return col;
        }

        private VisualElement BuildSkillColumn()
        {
            var col = new VisualElement { style = { flexGrow = 1 } };

            col.Add(new Label("Skills (table uid)") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            _skillListView = new ListView
            {
                selectionType = SelectionType.Single,
                style = { flexGrow = 1 }
            };

            _skillListView.makeItem = () => new Label();
            _skillListView.bindItem = (ve, i) =>
            {
                ((Label)ve).text = _skills[i].ToString();
            };

            _skillListView.onSelectionChange += items =>
            {
                if (items.FirstOrDefault() is SkillRow row)
                {
                    _selectedSkillUid = row.Uid;
                }
            };

            col.Add(_skillListView);

            _executeButton = new Button(ExecuteSelectedSkill) { text = "Execute" };
            col.Add(_executeButton);

            return col;
        }

        private void Update()
        {
            if (Application.isPlaying && SkillTestRuntimeHub.Instance != null)
            {
                RefreshSpawnedList(false);
            }
            UpdatePlayModeHint();
        }

        private void UpdatePlayModeHint()
        {
            _playModeHint.text = !Application.isPlaying
                ? "Play Mode에서만 Spawn/Execute가 동작합니다. (SkillTestRuntimeHub를 씬에 하나 배치 권장)"
                : "Play Mode 활성. Spawn/Execute 가능.";
        }

        private void LoadMonsterTable()
        {
            _monsters = MonsterTableProvider.LoadMonsters(_forceReloadToggle.value);
            RefreshMonsterFilter();
        }

        private void LoadSkillTable()
        {
            _skills.Clear();

            var table = GGemCo2DSkillEditor.TableLoaderManagerSkill.LoadTableSkill(_forceReloadToggle.value);
            if (table != null)
            {
                foreach (var kv in table.GetDatas())
                {
                    var s = kv.Value;
                    if (s == null) continue;
                    if (s.Uid <= 0) continue;
                    _skills.Add(new SkillRow(s.Uid, s.Name));
                }
            }

            _skills.Sort((a, b) => a.Uid.CompareTo(b.Uid));
            _skillListView.itemsSource = _skills;
            _skillListView.Rebuild();
        }

        private void RefreshMonsterFilter()
        {
            var q = (_searchField.value ?? string.Empty).Trim();
            _filtered = string.IsNullOrEmpty(q)
                ? _monsters.ToList()
                : _monsters.Where(m => m.Uid.ToString().Contains(q, StringComparison.OrdinalIgnoreCase)
                                       || (m.Name ?? "").Contains(q, StringComparison.OrdinalIgnoreCase))
                          .ToList();

            _monsterListView.itemsSource = _filtered;
            _monsterListView.Rebuild();
        }

        private void RefreshSpawnedList(bool rebuild = true)
        {
            if (!Application.isPlaying || SkillTestRuntimeHub.Instance == null)
            {
                _spawnedListView.itemsSource = new List<GameObject>();
                if (rebuild) _spawnedListView.Rebuild();
                return;
            }

            var list = SkillTestRuntimeHub.Instance.Spawned.Where(x => x != null).ToList();
            _spawnedListView.itemsSource = list;
            if (rebuild) _spawnedListView.Rebuild();
        }

        private async Task SpawnSelectedMonster(int uid)
        {
            if (_isSpawning) return;

            try
            {
                _isSpawning = true;

                var hub = SkillTestRuntimeHub.Instance;
                if (hub == null)
                {
                    Debug.LogWarning("[SkillTest] SkillTestRuntimeHub가 없습니다. 씬에 배치하세요.");
                    return;
                }

                var go = await hub.SpawnMonster(uid);
                if (go == null)
                {
                    Debug.LogWarning("[SkillTest] 몬스터 스폰 실패.");
                    return;
                }

                RefreshSpawnedList();
            }
            finally
            {
                _isSpawning = false;
            }
        }

        private void ExecuteSelectedSkill()
        {
            if (!Application.isPlaying) return;

            var hub = SkillTestRuntimeHub.Instance;
            if (hub == null || hub.SelectedMonster == null)
            {
                Debug.LogWarning("[SkillTest] 선택된 몬스터가 없습니다. 먼저 Spawn 후 Spawned 목록에서 선택하세요.");
                return;
            }

            if (_selectedSkillUid <= 0)
            {
                Debug.LogWarning("[SkillTest] 선택된 스킬 UID가 없습니다.");
                return;
            }

            var monster = hub.SelectedMonster;

            var driver = monster.GetComponent<GGemCo2DCore.IMonsterSkillDriver>();
            if (driver == null)
            {
                Debug.LogWarning("[SkillTest] IMonsterSkillDriver가 없습니다. (MonsterSkillDriverAdapter 컴포넌트 필요)");
                return;
            }

            var st = driver.TryUseSkill(_selectedSkillUid,
                new GGemCo2DCore.MonsterSkillTarget(
                    hub.LockedTarget,
                    hub.GroundPoint,
                    hub.Forward
                ));

            if (st != GGemCo2DCore.SkillUseResult.Started)
                Debug.LogWarning("[SkillTest] TryUseSkill 거부(쿨다운/조건/타겟 등).");
        }
    }
}
