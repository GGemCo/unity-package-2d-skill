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

        private ObjectField _overrideAssetField;
        private SkillTestOverrideAsset _overrideAsset;

        private ObjectField _areaRegistryField;

        private Button _spawnButton;
        private Button _executeButton;
        private Toggle _previewToggle;

        private Label _playModeHint;

        private MonsterTableProvider.MonsterRow? _selectedMonsterRow;
        private GameObject _selectedSpawnedMonster;

        private readonly List<string> _skillItems = new(); // 표시용(문자열/디버그)
        private SkillDefinition _selectedDevSkill;
        private string _selectedSkillId;
        private bool _isSpawning;

        public void CreateGUI()
        {
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            // Top toolbar
            var toolbar = new Toolbar();

            _searchField = new TextField("Search")
            {
                value = "",
                style =
                {
                    minWidth = 260
                }
            };
            _searchField.RegisterValueChangedCallback(_ => RefreshMonsterFilter());
            toolbar.Add(_searchField);

            _forceReloadToggle = new Toggle("Force Reload");
            toolbar.Add(_forceReloadToggle);

            var reloadBtn = new ToolbarButton(LoadMonsterTable) { text = "Reload Monsters" };
            toolbar.Add(reloadBtn);

            _overrideAssetField = new ObjectField("Override") { objectType = typeof(SkillTestOverrideAsset), allowSceneObjects = false };
            _overrideAssetField.RegisterValueChangedCallback(evt =>
            {
                _overrideAsset = evt.newValue as SkillTestOverrideAsset;
                RefreshSkillList();
            });
            toolbar.Add(_overrideAssetField);

            _areaRegistryField = new ObjectField("AreaRegistry") { objectType = typeof(AreaRegistry), allowSceneObjects = false };
            _areaRegistryField.RegisterValueChangedCallback(evt =>
            {
                SkillTestSelection.AreaRegistry = evt.newValue as AreaRegistry;
                Repaint();
                SceneView.RepaintAll();
            });
            toolbar.Add(_areaRegistryField);

            _previewToggle = new Toggle("Preview");
            _previewToggle.RegisterValueChangedCallback(evt =>
            {
                SkillTestSelection.PreviewEnabled = evt.newValue;
                SceneView.RepaintAll();
            });
            toolbar.Add(_previewToggle);

            rootVisualElement.Add(toolbar);

            _playModeHint = new Label();
            rootVisualElement.Add(_playModeHint);

            // Main 3 columns
            var main = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            rootVisualElement.Add(main);

            // Column: Monsters
            main.Add(BuildMonsterPanel());

            // Column: Spawned
            main.Add(BuildSpawnedPanel());

            // Column: Skills
            main.Add(BuildSkillPanel());

            UpdatePlayModeHint();
            LoadMonsterTable();
            EditorApplication.playModeStateChanged += _ => UpdatePlayModeHint();
        }

        private VisualElement BuildMonsterPanel()
        {
            var col = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    marginRight = 8
                }
            };

            col.Add(new Label("Monsters (from monster.txt via Core TableLoaderManager)") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            _monsterListView = new ListView
            {
                itemsSource = _filtered,
                makeItem = () => new Label(),
                bindItem = (e, i) =>
                {
                    (e as Label)!.text = _filtered[i].ToString();
                },
                selectionType = SelectionType.Single
            };
            _monsterListView.selectionChanged += objs =>
            {
                var first = objs.FirstOrDefault();
                if (first is MonsterTableProvider.MonsterRow row)
                {
                    _selectedMonsterRow = row;
                    RefreshSkillList();
                }
            };
            _monsterListView.style.flexGrow = 1;
            col.Add(_monsterListView);
            
            using (new EditorGUI.DisabledScope(_isSpawning || !EditorApplication.isPlaying))
            {
                _spawnButton = new Button(() =>
                    {
                        if (_selectedMonsterRow == null) return;
                        _ = SpawnSelectedMonster(_selectedMonsterRow.Value.Uid);
                    })
                    { text = _isSpawning ? "Spawning..." : "Spawn Selected Monster" };

                col.Add(_spawnButton);
            }

            return col;
        }

        private VisualElement BuildSpawnedPanel()
        {
            var col = new VisualElement
            {
                style =
                {
                    flexGrow = 1,
                    marginRight = 8
                }
            };

            col.Add(new Label("Spawned Monsters (Play Mode)") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            _spawnedListView = new ListView
            {
                itemsSource = new List<GameObject>(),
                makeItem = () => new Label()
            };
            _spawnedListView.bindItem = (e, i) =>
            {
                var list = (List<GameObject>)_spawnedListView.itemsSource;
                var go = list[i];
                (e as Label)!.text = go != null ? go.name : "<null>";
            };
            _spawnedListView.selectionType = SelectionType.Single;
            _spawnedListView.selectionChanged += objs =>
            {
                var go = objs.FirstOrDefault() as GameObject;
                if (go == null) return;

                _selectedSpawnedMonster = go;
                SkillTestSelection.SelectedMonster = go;
                Selection.activeObject = go;
                SceneView.FrameLastActiveSceneView();

                RefreshSkillList();
                SceneView.RepaintAll();
            };
            _spawnedListView.style.flexGrow = 1;
            col.Add(_spawnedListView);

            var row = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            var refresh = new Button(() => RefreshSpawnedList()) { text = "Refresh" };
            var despawn = new Button(() =>
            {
                if (!Application.isPlaying) return;
                if (_selectedSpawnedMonster == null) return;
                if (SkillTestRuntimeHub.Instance == null) return;
                SkillTestRuntimeHub.Instance.DespawnMonster(_selectedSpawnedMonster);
                _selectedSpawnedMonster = null;
                SkillTestSelection.SelectedMonster = null;
                RefreshSpawnedList();
                RefreshSkillList();
                SceneView.RepaintAll();
            })
            { text = "Despawn Selected" };
            row.Add(refresh);
            row.Add(despawn);
            col.Add(row);

            return col;
        }

        private VisualElement BuildSkillPanel()
        {
            var col = new VisualElement
            {
                style =
                {
                    flexGrow = 1
                }
            };

            col.Add(new Label("Skills") { style = { unityFontStyleAndWeight = FontStyle.Bold } });

            _skillListView = new ListView
            {
                itemsSource = _skillItems,
                makeItem = () => new Label(),
                bindItem = (e, i) => (e as Label)!.text = _skillItems[i],
                selectionType = SelectionType.Single
            };
            _skillListView.selectionChanged += objs =>
            {
                var s = objs.FirstOrDefault() as string;
                if (string.IsNullOrEmpty(s)) return;

                // 규칙:
                // - "[DEV] <skillId> ..." 형태면 devSkill
                // - "[ID ] <skillId>" 형태면 skillId
                _selectedDevSkill = null;
                _selectedSkillId = null;

                if (s.StartsWith("[DEV] "))
                {
                    var id = ExtractSkillId(s);
                    _selectedDevSkill = FindDevSkillById(id);
                    _selectedSkillId = id;

                    SkillTestSelection.SelectedDevSkill = _selectedDevSkill;
                    SkillTestSelection.SelectedSkillId = null;
                }
                else if (s.StartsWith("[ID ] "))
                {
                    var id = ExtractSkillId(s);
                    _selectedSkillId = id;

                    SkillTestSelection.SelectedDevSkill = null;
                    SkillTestSelection.SelectedSkillId = id;
                }

                SceneView.RepaintAll();
            };
            _skillListView.style.flexGrow = 1;
            col.Add(_skillListView);

            _executeButton = new Button(() =>
            {
                if (!Application.isPlaying) return;
                ExecuteSelectedSkill();
            })
            { text = "Execute" };
            col.Add(_executeButton);

            var hint = new Label("Tip: Assign AreaRegistry + select a DEV skill to preview its AreaDefinition in Scene View.")
                {
                    style =
                    {
                        unityFontStyleAndWeight = FontStyle.Italic
                    }
                };
            col.Add(hint);

            return col;
        }

        private void Update()
        {
            // Play Mode에서 spawned 리스트 동기화가 필요하면 간단히 갱신
            if (Application.isPlaying && SkillTestRuntimeHub.Instance != null)
            {
                // 선택 유지 + 목록 업데이트
                RefreshSpawnedList(false);
            }
        }

        private void UpdatePlayModeHint()
        {
            _playModeHint.text = !Application.isPlaying ? "Play Mode에서만 Spawn/Execute가 동작합니다. (SkillTestRuntimeHub를 씬에 하나 배치 권장)" : "Play Mode 활성. Spawn/Execute 가능.";
        }

        private void LoadMonsterTable()
        {
            _monsters = MonsterTableProvider.LoadMonsters(_forceReloadToggle.value);
            RefreshMonsterFilter();
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
            try
            {
                if (_isSpawning) return;
                if (!Application.isPlaying)
                {
                    Debug.LogWarning("[SkillTest] Play Mode에서만 스폰할 수 있습니다.");
                    return;
                }

                var hub = SkillTestRuntimeHub.Instance;
                if (hub == null)
                {
                    Debug.LogWarning("[SkillTest] SkillTestRuntimeHub가 씬에 없습니다. 빈 GameObject에 SkillTestRuntimeHub를 추가하세요.");
                    return;
                }

                _isSpawning = true;
                var monster = await hub.SpawnMonster(uid);
                if (monster == null)
                {
                    Debug.LogWarning($"[SkillTest] 몬스터 생성 실패. uid={uid}");
                    return;
                }
                _selectedSpawnedMonster = monster;
                SkillTestSelection.SelectedMonster = monster;
                Selection.activeObject = monster;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                _isSpawning = false;
                RefreshSpawnedList();
                RefreshSkillList();
                SceneView.RepaintAll();
            }
        }

        private void RefreshSkillList()
        {
            _skillItems.Clear();
            SkillTestSelection.SelectedDevSkill = null;
            SkillTestSelection.SelectedSkillId = null;

            // 몬스터 UID는 “테이블 선택”이든 “스폰 선택”이든 우선순위로 결정
            int uid = _selectedMonsterRow?.Uid ?? 0;

            // 스폰된 몬스터가 선택된 경우: 이름으로만은 UID 역추적이 어려우므로,
            // 툴의 기본 UX는 "좌측 테이블에서 몬스터 선택 -> 우측 스킬 확인"을 권장.
            // (추후 Monster 컴포넌트에 monsterUid를 보관하면 더 개선 가능)
            if (uid <= 0)
                return;

            // 1) 테이블 기반 skillId 목록(현재 프로젝트에 컬럼이 없다면 Override만 사용 가능)
            var baseSkillIds = new List<string>();
            

            // 2) Override 적용
            if (_overrideAsset != null && _overrideAsset.TryGetOverride(uid, out var ov))
            {
                if (ov.mode == SkillTestOverrideAsset.MergeMode.Replace)
                    baseSkillIds.Clear();

                // skillId 문자열
                foreach (var s in ov.skillIds)
                {
                    if (string.IsNullOrWhiteSpace(s)) continue;
                    baseSkillIds.Add(s.Trim());
                }

                // dev skills
                foreach (var dev in ov.devSkills)
                {
                    if (dev == null) continue;
                    _skillItems.Add($"[DEV] {dev.skillId}  (SO)  area={dev.defaultAreaId}");
                }
            }

            // 중복 제거/정렬
            foreach (var id in baseSkillIds.Where(x => !string.IsNullOrWhiteSpace(x))
                                           .Select(x => x.Trim())
                                           .Distinct(StringComparer.Ordinal)
                                           .OrderBy(x => x, StringComparer.Ordinal))
            {
                _skillItems.Add($"[ID ] {id}");
            }

            _skillListView.Rebuild();
        }

        private void ExecuteSelectedSkill()
        {
            var hub = SkillTestRuntimeHub.Instance;
            if (hub == null || hub.SelectedMonster == null)
            {
                Debug.LogWarning("[SkillTest] 선택된 몬스터가 없습니다. 먼저 Spawn 후 Spawned 목록에서 선택하세요.");
                return;
            }

            var monster = hub.SelectedMonster;

            // 타겟 컨텍스트 구성
            SkillTestSelection.GroundPoint = hub.GroundPoint;
            SkillTestSelection.LockedTarget = hub.LockedTarget;
            SkillTestSelection.Forward = hub.Forward;

            var ctx = new SkillTargetContext(
                caster: monster,
                lockedTarget: hub.LockedTarget != null ? hub.LockedTarget.gameObject : null,
                groundPoint: hub.GroundPoint,
                forward: new Vector3(hub.Forward.x, hub.Forward.y, 0f)
            );

            // 1) DEV SkillDefinition 직접 실행 (개발 중 스킬 보기/테스트)
            if (_selectedDevSkill != null)
            {
                var executor = monster.GetComponent<SkillExecutor>();
                if (executor == null)
                {
                    Debug.LogWarning("[SkillTest] 선택된 몬스터에 SkillExecutor가 없습니다.");
                    return;
                }

                bool ok = executor.TryUse(_selectedDevSkill, ctx);
                if (!ok) Debug.LogWarning("[SkillTest] 스킬 실행 실패(진행 중이거나 조건 불충족).");
                return;
            }

            // 2) skillId 기반 실행(운영 경로 테스트)
            if (!string.IsNullOrEmpty(_selectedSkillId))
            {
                // 권장: Core 인터페이스 경유(Ignore 시에도 Tool은 동작)
                var driver = monster.GetComponent<GGemCo2DCore.IMonsterSkillDriver>();
                if (driver == null)
                {
                    Debug.LogWarning("[SkillTest] IMonsterSkillDriver가 없습니다. (운영 경로 테스트는 어댑터 컴포넌트 필요)");
                    return;
                }

                var st = driver.TryUseSkill(_selectedSkillId,
                    new GGemCo2DCore.MonsterSkillTarget(
                        hub.LockedTarget,
                        hub.GroundPoint,
                        hub.Forward
                    ));

                if (st != GGemCo2DCore.SkillUseResult.Started)
                    Debug.LogWarning("[SkillTest] TryUseSkill 거부(쿨다운/조건/타겟 등).");
            }
        }

        private static string ExtractSkillId(string line)
        {
            // "[DEV] SK_0001 ..." or "[ID ] SK_0001"
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return string.Empty;
            return parts[1].Trim();
        }

        private SkillDefinition FindDevSkillById(string id)
        {
            if (_overrideAsset == null) return null;
            if (_selectedMonsterRow == null) return null;

            if (_overrideAsset.TryGetOverride(_selectedMonsterRow.Value.Uid, out var ov))
            {
                return ov.devSkills.FirstOrDefault(x => x != null && x.skillId == id);
            }
            return null;
        }
    }
}
