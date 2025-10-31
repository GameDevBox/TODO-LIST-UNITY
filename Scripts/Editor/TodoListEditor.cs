using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class TodoListEditor : EditorWindow
{
    private List<TodoItem> todoItems = new List<TodoItem>();
    private Vector2 scrollPosition;
    private Vector2 settingsScrollPosition;
    private string searchFilter = "";
    private Priority priorityFilter = Priority.All;
    private Category categoryFilter = Category.All;
    private Status statusFilter = Status.All;
    private TodoSettings settings;
    private bool showCompleted = true;
    private bool showSettings = false;
    private string newTaskTitle = "";
    private string newTaskDescription = "";
    private DateTime newTaskDueDate = DateTime.Now.AddDays(1);
    private Priority newTaskPriority = Priority.Medium;
    private Category newTaskCategory = Category.General;
    private string newSubTask = "";
    private TodoItem selectedItemForSubtasks;
    private TodoItem selectedItemForAssets;

    public List<TeamMember> teamMembers = new List<TeamMember>();
    private bool showTeamManagement = false;
    private string newMemberName = "";
    private string newMemberRole = "Developer";
    private Vector2 teamScrollPosition;
    private string selectedMemberFilter = "All";
    private List<string> quickAddAssignedMembers = new List<string>();
    private bool showQuickAddAssignment = false;

    private const string TEAM_DATA_KEY = "TodoList_TeamData";
    private const string DATA_KEY = "TodoList_Data";
    private const string SETTINGS_PATH = "Assets/TodoList/TodoSettings.asset";

    [MenuItem("Tools/TODO List %#t")]
    public static void ShowWindow()
    {
        var window = GetWindow<TodoListEditor>("TODO List");

        // Apply settings to window if available
        if (window.settings != null)
        {
            window.minSize = window.settings.defaultWindowSize;
            if (window.settings.openAsUtilityWindow)
            {
                window.ShowUtility();
            }
        }
    }

    // Quick filtering by asset type
    private void DrawAssetTypeFilters()
    {
        GUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("Scripts", EditorStyles.miniButton))
            {
                FilterByAssetType(typeof(MonoScript));
            }
            if (GUILayout.Button("Scenes", EditorStyles.miniButton))
            {
                FilterByAssetType(typeof(SceneAsset));
            }
            if (GUILayout.Button("Prefabs", EditorStyles.miniButton))
            {
                FilterByAssetType("prefab");
            }
            if (GUILayout.Button("Textures", EditorStyles.miniButton))
            {
                FilterByAssetType(typeof(Texture2D));
            }
        }
        GUILayout.EndHorizontal();
    }

    private void FilterByAssetType(Type assetType)
    {
        searchFilter = "";
        todoItems = todoItems.Where(item =>
            item.referencedAssetGUIDs?.Any(guid =>
                AssetDatabase.GetMainAssetTypeAtPath(AssetDatabase.GUIDToAssetPath(guid)) == assetType
            ) == true
        ).ToList();
        Repaint();
    }

    private void FilterByAssetType(string fileExtension)
    {
        searchFilter = "";
        todoItems = todoItems.Where(item =>
            item.referencedAssetGUIDs?.Any(guid =>
                AssetDatabase.GUIDToAssetPath(guid).EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase)
            ) == true
        ).ToList();
        Repaint();
    }

    // Context menu integration
    [MenuItem("CONTEXT/MonoBehaviour/Add TODO for Script")]
    public static void AddTodoForScript(MenuCommand command)
    {
        var script = command.context as MonoBehaviour;
        if (script != null)
        {
            ShowWindow();
            var window = GetWindow<TodoListEditor>();

            var newItem = new TodoItem
            {
                id = Guid.NewGuid().ToString(),
                title = $"Implement: {script.GetType().Name}",
                description = $"Work on script: {script.GetType().Name}",
                dueDate = DateTime.Now.AddDays(2),
                priority = Priority.Medium,
                category = Category.Programming,
                status = Status.NotStarted,
                createdDate = DateTime.Now,
                componentType = script.GetType().FullName,
                referencedAssetGUIDs = new string[] { AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(script)) }
            };

            window.AddTodoItem(newItem);
        }
    }

    [MenuItem("Assets/Create/TODO from Selection", false, 100)]
    public static void CreateTodoFromSelection()
    {
        var selectedAssets = Selection.objects;
        if (selectedAssets.Length == 0) return;

        ShowWindow();

        // Create new task with selected assets
        var newItem = new TodoItem
        {
            id = Guid.NewGuid().ToString(),
            title = $"Work on {selectedAssets[0].name}",
            description = $"Task created from selection: {string.Join(", ", selectedAssets.Select(a => a.name))}",
            dueDate = DateTime.Now.AddDays(7),
            priority = Priority.Medium,
            category = GetCategoryFromAssetType(selectedAssets[0]),
            status = Status.NotStarted,
            createdDate = DateTime.Now,
            referencedAssetGUIDs = selectedAssets.Select(a => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(a))).ToArray()
        };

        // Add to todo list
        var window = GetWindow<TodoListEditor>();
        window.AddTodoItem(newItem);
    }

    [MenuItem("Assets/Create/TODO from Selection", true)]
    public static bool ValidateCreateTodoFromSelection()
    {
        return Selection.activeObject != null;
    }

    private static Category GetCategoryFromAssetType(UnityEngine.Object asset)
    {
        return asset switch
        {
            MonoScript _ => Category.Programming,
            Texture2D _ => Category.Art,
            Material _ => Category.Art,
            GameObject _ => Category.Design,
            SceneAsset _ => Category.Design,
            AudioClip _ => Category.Audio,
            Animator _ => Category.Animation,
            _ => Category.General
        };
    }

    private void OnEnable()
    {
        LoadData();
        LoadTeamData();
        LoadSettings();
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawQuickAddSection();
        DrawFilters();
        DrawTodoList();
        DrawStats();

        if (showSettings)
        {
            DrawSettings();
        }
    }

    private void DrawToolbar()
    {
        GUILayout.BeginHorizontal(EditorStyles.toolbar);
        {
            if (GUILayout.Button("New Task", EditorStyles.toolbarButton))
            {
                ShowAddTaskModal();
            }

            if (GUILayout.Button(showCompleted ? "Hide Completed" : "Show Completed", EditorStyles.toolbarButton))
            {
                showCompleted = !showCompleted;
            }

            if (GUILayout.Button("Team", EditorStyles.toolbarButton))
            {
                showSettings = true; // Show settings
                showTeamManagement = true; // Auto-expand team section
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Settings", EditorStyles.toolbarButton))
            {
                showSettings = !showSettings;
            }

            if (GUILayout.Button("Export", EditorStyles.toolbarButton))
            {
                ExportToCSV();
            }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawQuickAddSection()
    {
        EditorGUILayout.Space();
        GUILayout.Label("Quick Add Task", EditorStyles.boldLabel);

        GUILayout.BeginVertical("box");
        {
            newTaskTitle = EditorGUILayout.TextField("Title", newTaskTitle);
            newTaskDescription = EditorGUILayout.TextField("Description", newTaskDescription);

            GUILayout.BeginHorizontal();
            {
                newTaskDueDate = DateTime.Parse(EditorGUILayout.TextField("Due Date", newTaskDueDate.ToString("yyyy-MM-dd")));

                // Use settings defaults if available
                if (settings != null)
                {
                    newTaskPriority = (Priority)EditorGUILayout.EnumPopup("Priority", newTaskPriority);
                    newTaskCategory = (Category)EditorGUILayout.EnumPopup("Category", newTaskCategory);
                }
                else
                {
                    newTaskPriority = (Priority)EditorGUILayout.EnumPopup("Priority", newTaskPriority);
                    newTaskCategory = (Category)EditorGUILayout.EnumPopup("Category", newTaskCategory);
                }


                // Add member assignment to quick add
                if (teamMembers.Count > 1) // Only show if there are other members
                {
                    string assignButtonText = "Assign";
                    if (quickAddAssignedMembers.Count > 0)
                    {
                        assignButtonText = $"Assign ({quickAddAssignedMembers.Count})";
                    }

                    if (GUILayout.Button(assignButtonText, GUILayout.Width(70)))
                    {
                        ShowQuickAddMemberAssignment();
                    }
                }
            }
            GUILayout.EndHorizontal();

            // Show assigned members in quick add
            if (quickAddAssignedMembers.Count > 0)
            {
                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label("Assigned to:", GUILayout.Width(70));
                    foreach (var memberId in quickAddAssignedMembers)
                    {
                        var member = teamMembers.FirstOrDefault(m => m.id == memberId);
                        if (member != null)
                        {
                            DrawMemberBadge(member);
                        }
                    }
                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("Clear", GUILayout.Width(50)))
                    {
                        quickAddAssignedMembers.Clear();
                    }
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Add Task") && !string.IsNullOrEmpty(newTaskTitle))
                {
                    AddNewTask();
                    newTaskTitle = "";
                    newTaskDescription = "";
                }

                if (GUILayout.Button("Clear"))
                {
                    newTaskTitle = "";
                    newTaskDescription = "";
                }
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
    }

    private void ShowQuickAddMemberAssignment()
    {
        GenericMenu menu = new GenericMenu();

        // Clear all assignments
        menu.AddItem(new GUIContent("Clear All"), false, () =>
        {
            quickAddAssignedMembers.Clear();
        });

        menu.AddSeparator("");

        // Assign to each team member
        foreach (var member in teamMembers.Where(m => m.isActive))
        {
            bool isCurrentlyAssigned = quickAddAssignedMembers.Contains(member.id);
            menu.AddItem(new GUIContent($"Assign to {member.name}"), isCurrentlyAssigned, () =>
            {
                ToggleQuickAddMemberAssignment(member.id);
            });
        }

        menu.ShowAsContext();
    }

    private void ToggleQuickAddMemberAssignment(string memberId)
    {
        if (quickAddAssignedMembers.Contains(memberId))
        {
            quickAddAssignedMembers.Remove(memberId);
        }
        else
        {
            quickAddAssignedMembers.Add(memberId);
        }
    }

    private void InitializeDefaultTeam()
    {
        if (teamMembers.Count == 0)
        {
            SaveTeamData();
        }
    }

    private void DrawTeamManagementSection()
    {
        showTeamManagement = EditorGUILayout.Foldout(showTeamManagement, "Team Management", true);
        if (!showTeamManagement) return;

        GUILayout.BeginVertical("box");
        {
            // Add new member
            GUILayout.Label("Add Team Member", EditorStyles.miniBoldLabel);

            // === BETTER VERTICAL LAYOUT ===
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            {
                newMemberName = EditorGUILayout.TextField("Name", newMemberName);
                EditorGUILayout.Space(2); // Small space between fields
                newMemberRole = EditorGUILayout.TextField("Role", newMemberRole);
                EditorGUILayout.Space(5); // Space before button

                GUILayout.BeginHorizontal();
                {
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Add Team Member", GUILayout.Width(120)) && !string.IsNullOrEmpty(newMemberName))
                    {
                        teamMembers.Add(new TeamMember(newMemberName, newMemberRole));
                        newMemberName = "";
                        newMemberRole = "Developer";
                        SaveTeamData();
                    }
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.EndVertical();

            // Team member list
            EditorGUILayout.Space();
            GUILayout.Label("Current Team Members", EditorStyles.miniBoldLabel);

            if (teamMembers.Count == 0)
            {
                EditorGUILayout.HelpBox("No team members added yet. Add your first team member above!", MessageType.Info);
            }
            else
            {
                teamScrollPosition = EditorGUILayout.BeginScrollView(teamScrollPosition, GUILayout.Height(150));
                for (int i = 0; i < teamMembers.Count; i++)
                {
                    DrawTeamMember(teamMembers[i], i);
                }
                EditorGUILayout.EndScrollView();
            }
        }
        GUILayout.EndVertical();
    }

    private void DrawTeamMember(TeamMember member, int index)
    {
        GUILayout.BeginHorizontal();
        {
            // Color indicator
            Rect colorRect = GUILayoutUtility.GetRect(20, 20, GUILayout.Width(20));
            EditorGUI.DrawRect(colorRect, member.color);

            // Member info
            GUILayout.BeginVertical();
            {
                GUILayout.Label($"{member.name} ({member.initials})", EditorStyles.boldLabel);
                GUILayout.Label(member.role, EditorStyles.miniLabel);
            }
            GUILayout.EndVertical();

            GUILayout.FlexibleSpace();

            // Active toggle
            bool newActive = EditorGUILayout.Toggle(member.isActive, GUILayout.Width(20));
            if (newActive != member.isActive)
            {
                member.isActive = newActive;
                SaveTeamData();
            }

            // Remove button - FIXED: Don't modify the list during iteration
            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                // Schedule removal for after the layout is complete
                EditorApplication.delayCall += () => RemoveTeamMember(member.id);
                return; // Exit early since we're removing this item
            }
        }
        GUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    private void RemoveTeamMember(string memberId)
    {
        // Remove from team members
        teamMembers.RemoveAll(m => m.id == memberId);

        // Also remove this member from any assigned tasks
        foreach (var task in todoItems)
        {
            if (task.assignedMembers != null && task.assignedMembers.Contains(memberId))
            {
                var list = task.assignedMembers.ToList();
                list.Remove(memberId);
                task.assignedMembers = list.Count > 0 ? list.ToArray() : null;
            }
        }

        SaveTeamData();
        SaveData(); // Save task changes too
        Repaint(); // Refresh the UI
    }

    private void DrawFilters()
    {
        GUILayout.BeginHorizontal();
        {
            searchFilter = EditorGUILayout.TextField("Search", searchFilter);
            priorityFilter = (Priority)EditorGUILayout.EnumPopup(priorityFilter, GUILayout.Width(100));
            categoryFilter = (Category)EditorGUILayout.EnumPopup(categoryFilter, GUILayout.Width(100));
            statusFilter = (Status)EditorGUILayout.EnumPopup(statusFilter, GUILayout.Width(100));

            if (teamMembers.Count > 1)
            {
                DrawMemberFilter();
            }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawMemberFilter()
    {
        GenericMenu menu = new GenericMenu();
        menu.AddItem(new GUIContent("All"), selectedMemberFilter == "All", () => selectedMemberFilter = "All");
        foreach (var member in teamMembers.Where(m => m.isActive))
        {
            menu.AddItem(new GUIContent(member.name), selectedMemberFilter == member.id, () => selectedMemberFilter = member.id);
        }

        string buttonText = selectedMemberFilter == "All" ? "All Members" : teamMembers.FirstOrDefault(m => m.id == selectedMemberFilter)?.name ?? "All";
        if (GUILayout.Button(buttonText, GUILayout.Width(120)))
        {
            menu.ShowAsContext();
        }
    }

    private void DrawTodoList()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        var filteredItems = GetFilteredItems();

        foreach (var item in filteredItems)
        {
            DrawTodoItem(item);
        }

        if (filteredItems.Count == 0)
        {
            EditorGUILayout.HelpBox("No tasks found. Create a new task to get started!", MessageType.Info);
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawTodoItem(TodoItem item)
    {
        GUILayout.BeginVertical("box");
        {
            // Header with status and title
            GUILayout.BeginHorizontal();
            {
                // Status dropdown instead of just completion toggle
                Status newStatus = (Status)EditorGUILayout.EnumPopup(item.status, GUILayout.Width(100));
                if (newStatus != item.status)
                {
                    item.status = newStatus;
                    SaveData();
                }

                // Title with styling based on status
                GUIStyle titleStyle = new GUIStyle(EditorStyles.label);

                EditorGUILayout.LabelField(item.title, titleStyle);

                GUILayout.FlexibleSpace();
                switch (item.status)
                {
                    case Status.Completed:
                        titleStyle.normal.textColor = Color.gray;
                        titleStyle.fontStyle = FontStyle.Italic;
                        break;
                    case Status.Blocked:
                        titleStyle.normal.textColor = Color.red;
                        titleStyle.fontStyle = FontStyle.Bold;
                        break;
                    case Status.OnHold:
                        titleStyle.normal.textColor = Color.yellow;
                        break;
                    case Status.InProgress:
                        titleStyle.normal.textColor = Color.blue;
                        break;
                    case Status.NotStarted:
                        titleStyle.normal.textColor = Color.white;
                        break;
                }

                if (item.dueDate < DateTime.Now && item.status != Status.Completed)
                {
                    titleStyle.normal.textColor = Color.red;
                    titleStyle.fontStyle = FontStyle.Bold;
                }

                EditorGUILayout.LabelField(item.title, titleStyle);

                GUILayout.FlexibleSpace();

                // === ADD MEMBER ASSIGNMENT HERE ===
                DrawMemberAssignment(item);

                // Priority and category badges
                DrawPriorityBadge(item.priority);
                DrawCategoryBadge(item.category);
            }
            GUILayout.EndHorizontal();

            // Description
            if (!string.IsNullOrEmpty(item.description))
            {
                EditorGUILayout.LabelField(item.description, EditorStyles.wordWrappedLabel);
            }

            // === ADD SUBTASKS SECTION HERE ===
            DrawSubtasksSection(item);

            // Asset References Section
            DrawAssetReferencesSection(item);

            // Task details with status-specific info
            GUILayout.BeginHorizontal();
            {
                // Due date with color coding
                string dueDateText = $"Due: {item.dueDate:MMM dd, yyyy}";
                if (item.dueDate < DateTime.Now && item.status != Status.Completed)
                {
                    GUI.contentColor = Color.red;
                    EditorGUILayout.LabelField(dueDateText, EditorStyles.boldLabel, GUILayout.Width(100));
                    GUI.contentColor = Color.white;
                }
                else
                {
                    EditorGUILayout.LabelField(dueDateText, GUILayout.Width(100));
                }

                EditorGUILayout.LabelField($"Created: {item.createdDate:MMM dd}", GUILayout.Width(100));

                if (item.estimatedHours > 0)
                {
                    EditorGUILayout.LabelField($"Est: {item.estimatedHours}h", GUILayout.Width(60));
                }

                if (item.actualHours > 0)
                {
                    EditorGUILayout.LabelField($"Actual: {item.actualHours}h", GUILayout.Width(70));
                }

                GUILayout.FlexibleSpace();

                // Status icon/indicator
                DrawStatusIndicator(item.status);

                // Progress for subtasks
                if (item.subTasks != null && item.subTasks.Length > 0)
                {
                    int completed = item.subTasks.Count(st => st.isCompleted);
                    float progress = (float)completed / item.subTasks.Length;
                    EditorGUILayout.LabelField($"{completed}/{item.subTasks.Length}", GUILayout.Width(40));
                    Rect progressRect = GUILayoutUtility.GetRect(60, 16);

                    // Color progress bar based on status
                    Color progressColor = GetStatusColor(item.status);
                    DrawColoredProgressBar(progressRect, progress, progressColor);
                }
            }
            GUILayout.EndHorizontal();

            // Action buttons
            GUILayout.BeginHorizontal();
            {
                if (GUILayout.Button("Edit", GUILayout.Width(40)))
                {
                    ShowEditTaskModal(item);
                }

                if (GUILayout.Button("Subtasks", GUILayout.Width(60)))
                {
                    ToggleSubtasks(item);
                }

                if (GUILayout.Button("Assets", GUILayout.Width(50)))
                {
                    ToggleAssetReferences(item);
                }

                // Quick status buttons
                if (item.status != Status.InProgress && item.status != Status.Completed)
                {
                    if (GUILayout.Button("Start", GUILayout.Width(50)))
                    {
                        item.status = Status.InProgress;
                        SaveData();
                    }
                }

                if (item.status == Status.InProgress)
                {
                    if (GUILayout.Button("Complete", GUILayout.Width(70)))
                    {
                        item.status = Status.Completed;
                        SaveData();
                    }
                }

                if (GUILayout.Button("Duplicate", GUILayout.Width(70)))
                {
                    DuplicateTask(item);
                }

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Delete", GUILayout.Width(50)))
                {
                    if (EditorUtility.DisplayDialog("Delete Task",
                        $"Are you sure you want to delete '{item.title}'?", "Delete", "Cancel"))
                    {
                        todoItems.Remove(item);
                        SaveData();
                    }
                }
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();

        EditorGUILayout.Space();
    }

    private void ToggleSubtasks(TodoItem item)
    {
        selectedItemForSubtasks = selectedItemForSubtasks == item ? null : item;
        Repaint();
    }

    private void DrawSubtasksSection(TodoItem item)
    {
        bool showSubtasks = selectedItemForSubtasks == item ||
                           (item.subTasks != null && item.subTasks.Length > 0);

        if (!showSubtasks) return;

        GUILayout.BeginVertical("box");
        {
            GUILayout.Label("Subtasks:", EditorStyles.miniBoldLabel);

            // Show progress if there are subtasks
            if (item.subTasks != null && item.subTasks.Length > 0)
            {
                int completed = item.subTasks.Count(st => st.isCompleted);
                float progress = (float)completed / item.subTasks.Length;

                GUILayout.BeginHorizontal();
                {
                    GUILayout.Label($"Progress: {completed}/{item.subTasks.Length} ({progress:P0})");
                    GUILayout.FlexibleSpace();

                    // Progress bar
                    Rect progressRect = GUILayoutUtility.GetRect(100, 16);
                    Color progressColor = GetStatusColor(item.status);
                    DrawColoredProgressBar(progressRect, progress, progressColor);
                }
                GUILayout.EndHorizontal();
            }

            // Add new subtask
            GUILayout.BeginHorizontal();
            {
                newSubTask = EditorGUILayout.TextField("New Subtask", newSubTask);
                if (GUILayout.Button("Add", GUILayout.Width(40)) && !string.IsNullOrEmpty(newSubTask))
                {
                    AddSubTask(item, newSubTask);
                    newSubTask = "";
                }
            }
            GUILayout.EndHorizontal();

            // Existing subtasks
            if (item.subTasks != null && item.subTasks.Length > 0)
            {
                EditorGUILayout.Space();
                for (int i = 0; i < item.subTasks.Length; i++)
                {
                    DrawSubTask(item, i);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No subtasks yet. Add one above!", MessageType.Info);
            }
        }
        GUILayout.EndVertical();
    }

    private void DrawSubTask(TodoItem item, int index)
    {
        if (index >= item.subTasks.Length) return;

        var subTask = item.subTasks[index];

        GUILayout.BeginHorizontal();
        {
            // Completion toggle
            bool newCompleted = EditorGUILayout.Toggle(subTask.isCompleted, GUILayout.Width(20));
            if (newCompleted != subTask.isCompleted)
            {
                subTask.isCompleted = newCompleted;
                SaveData();
            }

            // Subtask title with strikethrough if completed
            GUIStyle titleStyle = new GUIStyle(EditorStyles.label);
            if (subTask.isCompleted)
            {
                titleStyle.normal.textColor = Color.gray;
                titleStyle.fontStyle = FontStyle.Italic;
            }

            EditorGUILayout.LabelField(subTask.title, titleStyle);

            GUILayout.FlexibleSpace();

            // Remove button
            if (GUILayout.Button("×", GUILayout.Width(20)))
            {
                RemoveSubTask(item, index);
            }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawMemberAssignment(TodoItem item)
    {
        if (teamMembers.Count == 0) return;

        // Show assigned members as badges
        if (item.assignedMembers != null && item.assignedMembers.Length > 0)
        {
            foreach (var memberId in item.assignedMembers)
            {
                var member = teamMembers.FirstOrDefault(m => m.id == memberId);
                if (member != null && member.isActive)
                {
                    DrawMemberBadge(member);
                }
            }
        }

        // Assignment button
        if (GUILayout.Button("Assign", GUILayout.Width(60)))
        {
            ShowMemberAssignmentPopup(item);
        }
    }

    private void DrawMemberBadge(TeamMember member)
    {
        GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniButton);
        badgeStyle.normal.textColor = Color.white;
        badgeStyle.normal.background = MakeTex(2, 2, member.color);

        if (GUILayout.Button(member.initials, badgeStyle, GUILayout.Width(30)))
        {
            // Clicking badge could show member info or remove assignment
        }
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;
        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    private void ShowMemberAssignmentPopup(TodoItem item)
    {
        GenericMenu menu = new GenericMenu();

        // Unassign option
        menu.AddItem(new GUIContent("Unassign All"), false, () =>
        {
            item.assignedMembers = null;
            SaveData();
        });

        menu.AddSeparator("");

        // Assign to each team member
        foreach (var member in teamMembers.Where(m => m.isActive))
        {
            bool isCurrentlyAssigned = item.assignedMembers?.Contains(member.id) == true;
            menu.AddItem(new GUIContent($"Assign to {member.name}"), isCurrentlyAssigned, () =>
            {
                ToggleMemberAssignment(item, member.id);
            });
        }

        menu.ShowAsContext();
    }

    private void ToggleMemberAssignment(TodoItem item, string memberId)
    {
        if (item.assignedMembers == null)
        {
            item.assignedMembers = new string[] { memberId };
        }
        else if (item.assignedMembers.Contains(memberId))
        {
            // Remove assignment
            var list = item.assignedMembers.ToList();
            list.Remove(memberId);
            item.assignedMembers = list.ToArray();
        }
        else
        {
            // Add assignment
            var list = item.assignedMembers.ToList();
            list.Add(memberId);
            item.assignedMembers = list.ToArray();
        }
        SaveData();
    }

    private void DrawAssetReferencesSection(TodoItem item)
    {
        if (settings != null && !settings.enableAssetLinking) return;

        bool showAssets = selectedItemForAssets == item ||
                         (item.referencedAssetGUIDs != null && item.referencedAssetGUIDs.Length > 0);

        if (!showAssets) return;

        if (settings != null && !settings.showAssetReferences) return;

        GUILayout.BeginVertical("box");
        {
            GUILayout.Label("Linked Assets:", EditorStyles.miniLabel);

            // Display existing assets
            if (item.referencedAssetGUIDs != null && item.referencedAssetGUIDs.Length > 0)
            {
                foreach (var guid in item.referencedAssetGUIDs)
                {
                    DrawAssetReference(guid, item);
                }
            }
            else
            {
                EditorGUILayout.HelpBox("No assets linked to this task", MessageType.Info);
            }

            // Add new asset section
            DrawAddAssetReference(item);
        }
        GUILayout.EndVertical();
    }

    private void DrawAssetReference(string guid, TodoItem item)
    {
        string assetPath = AssetDatabase.GUIDToAssetPath(guid);
        UnityEngine.Object asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

        if (asset != null)
        {
            GUILayout.BeginHorizontal();
            {
                // Asset icon and name
                GUIContent assetContent = new GUIContent(
                    asset.name,
                    AssetDatabase.GetCachedIcon(assetPath)
                );

                GUILayout.Label(assetContent, GUILayout.Height(20), GUILayout.ExpandWidth(true));

                // Quick action buttons
                if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    EditorGUIUtility.PingObject(asset);
                }

                if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(40)))
                {
                    AssetDatabase.OpenAsset(asset);
                }

                if (GUILayout.Button("Select", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    Selection.activeObject = asset;
                }

                // Remove button
                if (GUILayout.Button("×", EditorStyles.miniButton, GUILayout.Width(20)))
                {
                    RemoveAssetReference(item, guid);
                }
            }
            GUILayout.EndHorizontal();

            // Asset path (small info)
            EditorGUILayout.LabelField(assetPath, EditorStyles.miniLabel);
        }
        else
        {
            // Asset might be deleted
            GUILayout.BeginHorizontal();
            {
                EditorGUILayout.LabelField("Missing asset: " + guid, EditorStyles.miniLabel);
                if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    RemoveAssetReference(item, guid);
                }
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawAddAssetReference(TodoItem item)
    {
        GUILayout.Space(5);
        GUILayout.BeginHorizontal();
        {
            // Drag and drop area
            Rect dropArea = GUILayoutUtility.GetRect(0, 30, GUILayout.ExpandWidth(true));
            GUI.Box(dropArea, "Drag assets here to link them to this task", EditorStyles.helpBox);

            HandleAssetDragAndDrop(dropArea, item);

            // Or use object field
            UnityEngine.Object newAsset = EditorGUILayout.ObjectField("", null, typeof(UnityEngine.Object), false, GUILayout.Width(100));
            if (newAsset != null)
            {
                AddAssetReference(item, newAsset);
                // Clear the field by forcing a repaint
                Repaint();
            }
        }
        GUILayout.EndHorizontal();
    }

    private void HandleAssetDragAndDrop(Rect dropArea, TodoItem item)
    {
        Event evt = Event.current;

        if (dropArea.Contains(evt.mousePosition))
        {
            if (evt.type == EventType.DragUpdated)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.Use();
            }
            else if (evt.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();

                foreach (UnityEngine.Object draggedObject in DragAndDrop.objectReferences)
                {
                    AddAssetReference(item, draggedObject);
                }

                evt.Use();
                Repaint();
            }
        }
    }

    private void AddAssetReference(TodoItem item, UnityEngine.Object asset)
    {
        if (asset == null) return;

        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset));

        if (string.IsNullOrEmpty(guid)) return;

        if (item.referencedAssetGUIDs == null)
        {
            item.referencedAssetGUIDs = new string[] { guid };
        }
        else if (!item.referencedAssetGUIDs.Contains(guid))
        {
            var list = item.referencedAssetGUIDs.ToList();
            list.Add(guid);
            item.referencedAssetGUIDs = list.ToArray();
        }

        SaveData();
        Repaint();
    }

    private void RemoveAssetReference(TodoItem item, string guid)
    {
        if (item.referencedAssetGUIDs != null)
        {
            var list = item.referencedAssetGUIDs.ToList();
            list.Remove(guid);
            item.referencedAssetGUIDs = list.ToArray();
            SaveData();
            Repaint();
        }
    }

    private void ToggleAssetReferences(TodoItem item)
    {
        selectedItemForAssets = selectedItemForAssets == item ? null : item;
        Repaint();
    }

    private void DrawSubtasks(TodoItem item)
    {
        GUILayout.BeginVertical("box");
        GUILayout.Label("Subtasks:", EditorStyles.miniLabel);

        // Add new subtask
        GUILayout.BeginHorizontal();
        {
            newSubTask = EditorGUILayout.TextField(newSubTask);
            if (GUILayout.Button("Add", GUILayout.Width(40)) && !string.IsNullOrEmpty(newSubTask))
            {
                AddSubTask(item, newSubTask);
                newSubTask = "";
            }
        }
        GUILayout.EndHorizontal();

        // Existing subtasks
        if (item.subTasks != null)
        {
            for (int i = 0; i < item.subTasks.Length; i++)
            {
                GUILayout.BeginHorizontal();
                {
                    bool newCompleted = EditorGUILayout.Toggle(item.subTasks[i].isCompleted, GUILayout.Width(20));
                    if (newCompleted != item.subTasks[i].isCompleted)
                    {
                        item.subTasks[i].isCompleted = newCompleted;
                        SaveData();
                    }

                    EditorGUILayout.LabelField(item.subTasks[i].title);

                    GUILayout.FlexibleSpace();

                    if (GUILayout.Button("×", GUILayout.Width(20)))
                    {
                        RemoveSubTask(item, i);
                    }
                }
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.EndVertical();
    }

    private void DrawAssetReferences(TodoItem item)
    {
        if (item.referencedAssetGUIDs != null && item.referencedAssetGUIDs.Length > 0)
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("Linked Assets:", EditorStyles.miniLabel);

            foreach (var guid in item.referencedAssetGUIDs)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);

                if (asset != null)
                {
                    GUILayout.BeginHorizontal();
                    {
                        EditorGUILayout.ObjectField(asset, typeof(UnityEngine.Object), false);

                        if (GUILayout.Button("Ping", GUILayout.Width(40)))
                        {
                            EditorGUIUtility.PingObject(asset);
                        }

                        if (GUILayout.Button("Open", GUILayout.Width(40)))
                        {
                            AssetDatabase.OpenAsset(asset);
                        }

                        if (GUILayout.Button("×", GUILayout.Width(20)))
                        {
                            RemoveAssetReference(item, guid);
                        }
                    }
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();
        }
    }

    private void DrawPriorityBadge(Priority priority)
    {
        Color color = GetPriorityColor(priority);
        string label = priority.ToString();

        GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
        style.normal.textColor = color;
        style.fontStyle = FontStyle.Bold;

        EditorGUILayout.LabelField(label, style, GUILayout.Width(60));
    }

    private void DrawCategoryBadge(Category category)
    {
        GUIStyle style = new GUIStyle(EditorStyles.miniLabel);
        style.normal.textColor = GetCategoryColor(category);

        EditorGUILayout.LabelField(category.ToString(), style, GUILayout.Width(80));
    }

    private void DrawStatusIndicator(Status status)
    {
        string statusSymbol = GetStatusSymbol(status);
        string statusTooltip = GetStatusTooltip(status);

        GUIContent statusContent = new GUIContent(statusSymbol, statusTooltip);
        GUIStyle statusStyle = new GUIStyle(EditorStyles.miniLabel);
        statusStyle.normal.textColor = GetStatusColor(status);

        EditorGUILayout.LabelField(statusContent, statusStyle, GUILayout.Width(20));
    }

    private string GetStatusSymbol(Status status)
    {
        if (settings != null)
        {
            return settings.GetStatusSymbol(status);
        }

        return status switch
        {
            Status.NotStarted => "○",
            Status.InProgress => "▶",
            Status.Completed => "✓",
            Status.OnHold => "⏸",
            Status.Blocked => "⛔",
            _ => "○"
        };
    }

    private string GetStatusTooltip(Status status)
    {
        if (settings != null)
        {
            return settings.GetStatusDescription(status);
        }

        return status.ToString();
    }

    private Color GetStatusColor(Status status)
    {
        if (settings != null)
        {
            return settings.GetStatusColor(status);
        }

        return status switch
        {
            Status.NotStarted => Color.gray,
            Status.InProgress => new Color(0.2f, 0.6f, 1f), // Blue
            Status.Completed => new Color(0.2f, 0.8f, 0.2f), // Green
            Status.OnHold => new Color(1f, 0.8f, 0.2f), // Yellow
            Status.Blocked => new Color(1f, 0.3f, 0.3f), // Red
            _ => Color.white
        };
    }

    private void DrawColoredProgressBar(Rect rect, float progress, Color color)
    {
        // Draw background
        EditorGUI.DrawRect(rect, new Color(0.3f, 0.3f, 0.3f));

        // Draw progress with color
        Rect progressRect = new Rect(rect.x, rect.y, rect.width * progress, rect.height);
        EditorGUI.DrawRect(progressRect, color);

        // Draw border
        Handles.color = new Color(0.5f, 0.5f, 0.5f);
        Handles.DrawPolyLine(
            new Vector3(rect.x, rect.y, 0),
            new Vector3(rect.x + rect.width, rect.y, 0),
            new Vector3(rect.x + rect.width, rect.y + rect.height, 0),
            new Vector3(rect.x, rect.y + rect.height, 0),
            new Vector3(rect.x, rect.y, 0)
        );
    }

    private void DrawStats()
    {
        var filteredItems = GetFilteredItems();
        int total = filteredItems.Count;
        int completed = filteredItems.Count(i => i.status == Status.Completed);
        int overdue = filteredItems.Count(i => i.dueDate < DateTime.Now && i.status != Status.Completed);
        int inProgress = filteredItems.Count(i => i.status == Status.InProgress);

        GUILayout.BeginHorizontal("box");
        {
            EditorGUILayout.LabelField($"Total: {total}", GUILayout.Width(60));
            EditorGUILayout.LabelField($"Completed: {completed}", GUILayout.Width(80));
            EditorGUILayout.LabelField($"In Progress: {inProgress}", GUILayout.Width(90));
            EditorGUILayout.LabelField($"Overdue: {overdue}", GUILayout.Width(70));
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSettings()
    {
        if (settings == null)
        {
            GUILayout.BeginVertical("box");
            EditorGUILayout.HelpBox("No settings asset found. Create one to customize the TODO list.", MessageType.Warning);
            if (GUILayout.Button("Create Settings Asset"))
            {
                CreateSettingsAsset();
            }
            GUILayout.EndVertical();
            return;
        }

        // Settings categories with foldouts
        bool showBehaviorSettings = true;
        bool showVisualSettings = true;
        bool showColorSettings = true;
        bool showDefaultSettings = true;
        bool showDataManagement = true;

        settingsScrollPosition = EditorGUILayout.BeginScrollView(settingsScrollPosition, GUILayout.Height(300));

        GUILayout.BeginVertical("box");
        GUILayout.Label("TODO List Settings", EditorStyles.boldLabel);

        DrawTeamManagementSection();

        // Behavior Settings (Collapsible)
        showBehaviorSettings = EditorGUILayout.Foldout(showBehaviorSettings, "Behavior Settings", true);
        if (showBehaviorSettings)
        {
            GUILayout.BeginVertical("box");
            settings.autoSave = EditorGUILayout.Toggle("Auto Save", settings.autoSave);
            settings.showConfirmationDialogs = EditorGUILayout.Toggle("Show Confirmation Dialogs", settings.showConfirmationDialogs);
            settings.enableDueDateWarnings = EditorGUILayout.Toggle("Due Date Warnings", settings.enableDueDateWarnings);
            settings.dueDateWarningDays = EditorGUILayout.IntField("Warning Days Before Due", settings.dueDateWarningDays);
            settings.enableAssetLinking = EditorGUILayout.Toggle("Asset Linking", settings.enableAssetLinking);
            settings.enableContextMenuItems = EditorGUILayout.Toggle("Context Menu Items", settings.enableContextMenuItems);
            settings.enableQuickStatusActions = EditorGUILayout.Toggle("Quick Status Actions", settings.enableQuickStatusActions);
            GUILayout.EndVertical();
        }

        // Visual Settings (Collapsible)
        showVisualSettings = EditorGUILayout.Foldout(showVisualSettings, "Visual Settings", true);
        if (showVisualSettings)
        {
            GUILayout.BeginVertical("box");
            settings.compactMode = EditorGUILayout.Toggle("Compact Mode", settings.compactMode);
            settings.showProgressBars = EditorGUILayout.Toggle("Show Progress Bars", settings.showProgressBars);
            settings.showAssetReferences = EditorGUILayout.Toggle("Show Asset References", settings.showAssetReferences);
            settings.showStatusIndicators = EditorGUILayout.Toggle("Show Status Indicators", settings.showStatusIndicators);
            settings.showCategoryIcons = EditorGUILayout.Toggle("Show Category Icons", settings.showCategoryIcons);
            settings.colorCodeTasks = EditorGUILayout.Toggle("Color Code Tasks", settings.colorCodeTasks);
            settings.showTaskStatistics = EditorGUILayout.Toggle("Show Task Statistics", settings.showTaskStatistics);
            GUILayout.EndVertical();
        }

        // Color Settings (Collapsible)
        showColorSettings = EditorGUILayout.Foldout(showColorSettings, "Color Settings", true);
        if (showColorSettings)
        {
            GUILayout.BeginVertical("box");

            GUILayout.Label("Priority Colors", EditorStyles.miniBoldLabel);
            settings.criticalPriorityColor = EditorGUILayout.ColorField("Critical", settings.criticalPriorityColor);
            settings.highPriorityColor = EditorGUILayout.ColorField("High", settings.highPriorityColor);
            settings.mediumPriorityColor = EditorGUILayout.ColorField("Medium", settings.mediumPriorityColor);
            settings.lowPriorityColor = EditorGUILayout.ColorField("Low", settings.lowPriorityColor);

            GUILayout.Space(5);
            GUILayout.Label("Status Colors", EditorStyles.miniBoldLabel);
            settings.notStartedColor = EditorGUILayout.ColorField("Not Started", settings.notStartedColor);
            settings.inProgressColor = EditorGUILayout.ColorField("In Progress", settings.inProgressColor);
            settings.completedColor = EditorGUILayout.ColorField("Completed", settings.completedColor);
            settings.onHoldColor = EditorGUILayout.ColorField("On Hold", settings.onHoldColor);
            settings.blockedColor = EditorGUILayout.ColorField("Blocked", settings.blockedColor);

            GUILayout.Space(5);
            GUILayout.Label("Category Colors", EditorStyles.miniBoldLabel);
            settings.programmingColor = EditorGUILayout.ColorField("Programming", settings.programmingColor);
            settings.artColor = EditorGUILayout.ColorField("Art", settings.artColor);
            settings.designColor = EditorGUILayout.ColorField("Design", settings.designColor);
            settings.testingColor = EditorGUILayout.ColorField("Testing", settings.testingColor);
            settings.documentationColor = EditorGUILayout.ColorField("Documentation", settings.documentationColor);
            settings.audioColor = EditorGUILayout.ColorField("Audio", settings.audioColor);
            settings.animationColor = EditorGUILayout.ColorField("Animation", settings.animationColor);
            settings.uiColor = EditorGUILayout.ColorField("UI", settings.uiColor);

            GUILayout.EndVertical();
        }

        // Default Values (Collapsible)
        showDefaultSettings = EditorGUILayout.Foldout(showDefaultSettings, "Default Values", true);
        if (showDefaultSettings)
        {
            GUILayout.BeginVertical("box");
            settings.defaultPriority = (Priority)EditorGUILayout.EnumPopup("Default Priority", settings.defaultPriority);
            settings.defaultCategory = (Category)EditorGUILayout.EnumPopup("Default Category", settings.defaultCategory);
            settings.defaultStatus = (Status)EditorGUILayout.EnumPopup("Default Status", settings.defaultStatus);
            settings.defaultEstimateHours = EditorGUILayout.IntField("Default Estimate Hours", settings.defaultEstimateHours);
            settings.defaultDueDays = EditorGUILayout.IntField("Default Due Days", settings.defaultDueDays);
            GUILayout.EndVertical();
        }

        // Settings Actions (Always visible)
        GUILayout.Space(10);
        GUILayout.BeginVertical("box");
        GUILayout.Label("Settings Actions", EditorStyles.miniBoldLabel);
        GUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("Reset to Defaults"))
            {
                if (EditorUtility.DisplayDialog("Reset Settings",
                    "Reset all settings to default values?", "Reset", "Cancel"))
                {
                    settings.ResetToDefaults();
                    EditorUtility.DisplayDialog("Settings Reset", "All settings have been reset to defaults.", "OK");
                }
            }

            if (GUILayout.Button("Export Settings"))
            {
                settings.ExportSettings();
            }
        }
        GUILayout.EndHorizontal();
        GUILayout.EndVertical();

        // Data Management (Always visible at bottom)
        GUILayout.Space(10);
        showDataManagement = EditorGUILayout.Foldout(showDataManagement, "Data Management", true);
        if (showDataManagement)
        {
            GUILayout.BeginVertical("box");
            EditorGUILayout.HelpBox("These actions are irreversible. Make sure to export your data first if needed.", MessageType.Warning);

            if (GUILayout.Button("Clear All Completed Tasks", GUILayout.Height(25)))
            {
                int completedCount = todoItems.Count(i => i.status == Status.Completed);
                if (completedCount > 0)
                {
                    if (EditorUtility.DisplayDialog("Clear Completed Tasks",
                        $"Are you sure you want to delete {completedCount} completed task(s)?", "Delete", "Cancel"))
                    {
                        todoItems.RemoveAll(i => i.status == Status.Completed);
                        SaveData();
                        Repaint();
                    }
                }
                else
                {
                    EditorUtility.DisplayDialog("No Completed Tasks", "There are no completed tasks to clear.", "OK");
                }
            }

            if (GUILayout.Button("Reset All Data", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("Reset All Data",
                    "This will permanently delete ALL tasks and cannot be undone. Are you sure?", "Delete Everything", "Cancel"))
                {
                    todoItems.Clear();
                    SaveData();
                    Repaint();
                    EditorUtility.DisplayDialog("Data Reset", "All task data has been cleared.", "OK");
                }
            }

            if (GUILayout.Button("Export to CSV", GUILayout.Height(25)))
            {
                ExportToCSV();
            }

            GUILayout.EndVertical();
        }

        GUILayout.EndVertical(); // End settings box
        EditorGUILayout.EndScrollView();
    }

    private List<TodoItem> GetFilteredItems()
    {
        IEnumerable<TodoItem> filtered = todoItems;

        if (!showCompleted)
            filtered = filtered.Where(i => i.status != Status.Completed);

        if (!string.IsNullOrEmpty(searchFilter))
            filtered = filtered.Where(i =>
                i.title.Contains(searchFilter, StringComparison.OrdinalIgnoreCase) ||
                i.description.Contains(searchFilter, StringComparison.OrdinalIgnoreCase));

        if (priorityFilter != Priority.All)
            filtered = filtered.Where(i => i.priority == priorityFilter);

        if (categoryFilter != Category.All)
            filtered = filtered.Where(i => i.category == categoryFilter);

        if (statusFilter != Status.All)
            filtered = filtered.Where(i => i.status == statusFilter);

        if (selectedMemberFilter != "All" && teamMembers.Count > 1)
        {
            filtered = filtered.Where(i =>
                i.assignedMembers != null && i.assignedMembers.Contains(selectedMemberFilter));
        }

        return filtered.OrderByDescending(i => (int)i.priority)
                      .ThenBy(i => i.dueDate)
                      .ToList();
    }

    private void AddNewTask()
    {
        TodoItem newItem = new TodoItem
        {
            id = Guid.NewGuid().ToString(),
            title = newTaskTitle,
            description = newTaskDescription,
            dueDate = newTaskDueDate,
            priority = settings?.defaultPriority ?? newTaskPriority,
            category = settings?.defaultCategory ?? newTaskCategory,
            status = settings?.defaultStatus ?? Status.NotStarted,
            createdDate = DateTime.Now,
            estimatedHours = settings?.defaultEstimateHours ?? 0,

            // Add assigned members from quick add
        assignedMembers = quickAddAssignedMembers.Count > 0 ? quickAddAssignedMembers.ToArray() : null
        };

        todoItems.Add(newItem);
        SaveData();
    }

    public void AddTodoItem(TodoItem newItem)
    {
        todoItems.Add(newItem);
        SaveData();
        Repaint();
    }

    private void AddSubTask(TodoItem item, string subTaskTitle)
    {
        var subTask = new TodoItem.SubTask { title = subTaskTitle, isCompleted = false };

        if (item.subTasks == null)
        {
            item.subTasks = new TodoItem.SubTask[] { subTask };
        }
        else
        {
            var list = item.subTasks.ToList();
            list.Add(subTask);
            item.subTasks = list.ToArray();
        }

        SaveData();
    }

    private void RemoveSubTask(TodoItem item, int index)
    {
        if (item.subTasks != null && index < item.subTasks.Length)
        {
            var list = item.subTasks.ToList();
            list.RemoveAt(index);
            item.subTasks = list.ToArray();
            SaveData();
        }
    }

    private void ShowAddTaskModal()
    {
        // Implementation for detailed task modal
        // You can create a custom EditorWindow for this
        AddTaskModal.ShowWindow(null, (newItem) =>
        {
            todoItems.Add(newItem);
            SaveData();
        });
    }

    private void ShowEditTaskModal(TodoItem item)
    {
        AddTaskModal.ShowWindow(item, (updatedItem) =>
        {
            var index = todoItems.FindIndex(i => i.id == updatedItem.id);
            if (index >= 0)
            {
                todoItems[index] = updatedItem;
                SaveData();
            }
        });
    }

    private void DuplicateTask(TodoItem original)
    {
        var duplicate = new TodoItem
        {
            id = Guid.NewGuid().ToString(),
            title = $"{original.title} (Copy)",
            description = original.description,
            dueDate = original.dueDate.AddDays(7),
            priority = original.priority,
            category = original.category,
            status = Status.NotStarted,
            createdDate = DateTime.Now,
            estimatedHours = original.estimatedHours,
            subTasks = original.subTasks?.Select(st => new TodoItem.SubTask
            {
                title = st.title,
                isCompleted = false
            }).ToArray()
        };

        todoItems.Add(duplicate);
        SaveData();
    }

    private void ExportToCSV()
    {
        // Implementation for CSV export
        string path = EditorUtility.SaveFilePanel("Export TODO List", "", "todo_export.csv", "csv");
        if (!string.IsNullOrEmpty(path))
        {
            // Create CSV content
            System.Text.StringBuilder csv = new System.Text.StringBuilder();
            csv.AppendLine("Title,Description,Priority,Category,Status,Due Date,Created Date,Estimated Hours,Actual Hours");

            foreach (var item in todoItems)
            {
                csv.AppendLine($"\"{item.title}\",\"{item.description}\",{item.priority},{item.category},{item.status},{item.dueDate:yyyy-MM-dd},{item.createdDate:yyyy-MM-dd},{item.estimatedHours},{item.actualHours}");
            }

            System.IO.File.WriteAllText(path, csv.ToString());
            EditorUtility.DisplayDialog("Export Successful", $"Tasks exported to {path}", "OK");
        }
    }

    private Color GetPriorityColor(Priority priority)
    {
        if (settings != null)
        {
            return settings.GetPriorityColor(priority);
        }

        // Fallback to hardcoded colors if settings not loaded
        return priority switch
        {
            Priority.Critical => new Color(0.5f, 0f, 0.5f), // Purple
            Priority.High => Color.red,
            Priority.Medium => Color.yellow,
            Priority.Low => Color.green,
            _ => Color.gray
        };
    }

    private Color GetCategoryColor(Category category)
    {
        if (settings != null)
        {
            return settings.GetCategoryColor(category);
        }

        return category switch
        {
            Category.Programming => Color.blue,
            Category.Art => Color.magenta,
            Category.Design => Color.cyan,
            Category.Testing => Color.green,
            Category.Documentation => Color.white,
            Category.Audio => new Color(0.8f, 0.4f, 1f), // Purple
            Category.Animation => new Color(1f, 0.5f, 0f), // Orange
            Category.UI => new Color(0.9f, 0.9f, 0.2f), // Yellow
            _ => Color.gray
        };
    }

    private void PingAllAssets(TodoItem item)
    {
        foreach (var guid in item.referencedAssetGUIDs)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset != null)
            {
                EditorGUIUtility.PingObject(asset);
            }
        }
    }

    private void OpenAllAssets(TodoItem item)
    {
        foreach (var guid in item.referencedAssetGUIDs)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset);
            }
        }
    }

    private void SelectAssetsInProject(TodoItem item)
    {
        List<UnityEngine.Object> assets = new List<UnityEngine.Object>();
        foreach (var guid in item.referencedAssetGUIDs)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset != null)
            {
                assets.Add(asset);
            }
        }
        Selection.objects = assets.ToArray();
    }

    private void LoadData()
    {
        string data = EditorPrefs.GetString(DATA_KEY, "");
        if (!string.IsNullOrEmpty(data))
        {
            try
            {
                todoItems = JsonUtility.FromJson<TodoListWrapper>(data)?.items ?? new List<TodoItem>();
            }
            catch
            {
                todoItems = new List<TodoItem>();
            }
        }
    }

    private void SaveData()
    {
        var wrapper = new TodoListWrapper { items = todoItems };
        string data = JsonUtility.ToJson(wrapper);
        EditorPrefs.SetString(DATA_KEY, data);
    }

    private void LoadSettings()
    {
        settings = AssetDatabase.LoadAssetAtPath<TodoSettings>(SETTINGS_PATH);
    }

    private void LoadTeamData()
    {
        string data = EditorPrefs.GetString(TEAM_DATA_KEY, "");
        if (!string.IsNullOrEmpty(data))
        {
            try
            {
                teamMembers = JsonUtility.FromJson<TeamListWrapper>(data)?.members ?? new List<TeamMember>();
            }
            catch
            {
                teamMembers = new List<TeamMember>();
            }
        }
        InitializeDefaultTeam();
    }

    private void SaveTeamData()
    {
        var wrapper = new TeamListWrapper { members = teamMembers };
        string data = JsonUtility.ToJson(wrapper);
        EditorPrefs.SetString(TEAM_DATA_KEY, data);
    }

    [System.Serializable]
    private class TeamListWrapper { public List<TeamMember> members = new List<TeamMember>(); }

    private void CreateSettingsAsset()
    {
        if (!AssetDatabase.IsValidFolder("Assets/TodoList"))
        {
            AssetDatabase.CreateFolder("Assets", "TodoList");
        }

        settings = CreateInstance<TodoSettings>();
        AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [System.Serializable]
    private class TodoListWrapper { public List<TodoItem> items = new List<TodoItem>(); }
}