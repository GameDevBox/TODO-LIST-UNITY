using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class AddTaskModal : EditorWindow
{
    private TodoItem currentItem;
    private System.Action<TodoItem> onSave;

    private string title = "";
    private string description = "";
    private System.DateTime dueDate = System.DateTime.Now.AddDays(1);
    private Priority priority = Priority.Medium;
    private Category category = Category.General;
    private Status status = Status.NotStarted;
    private int estimatedHours = 0;
    private int actualHours = 0;

    private List<TodoItem.SubTask> modalSubTasks = new List<TodoItem.SubTask>();
    private string newModalSubTask = "";
    private List<string> modalAssignedMembers = new List<string>();
    private List<TeamMember> availableMembers = new List<TeamMember>();

    private List<UnityEngine.Object> assetReferences = new List<UnityEngine.Object>();

    public static void ShowWindow(TodoItem item, System.Action<TodoItem> saveCallback)
    {
        AddTaskModal window = CreateInstance<AddTaskModal>();
        window.currentItem = item;
        window.onSave = saveCallback;

        // Get available members from main window
        var mainWindow = GetWindow<TodoListEditor>();
        window.availableMembers = mainWindow.teamMembers;

        window.InitializeFromItem();
        window.ShowUtility();
        window.titleContent = new GUIContent(item == null ? "Add New Task" : "Edit Task");
        window.minSize = new Vector2(500, 650); // Increased height for members
    }

    private void InitializeFromItem()
    {
        if (currentItem != null)
        {
            title = currentItem.title;
            description = currentItem.description;
            dueDate = currentItem.dueDate;
            priority = currentItem.priority;
            category = currentItem.category;
            status = currentItem.status;
            estimatedHours = currentItem.estimatedHours;
            actualHours = currentItem.actualHours;

            // Load existing asset references
            if (currentItem.referencedAssetGUIDs != null)
            {
                assetReferences.Clear();
                foreach (var guid in currentItem.referencedAssetGUIDs)
                {
                    var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                        AssetDatabase.GUIDToAssetPath(guid));
                    if (asset != null)
                    {
                        assetReferences.Add(asset);
                    }
                }
            }

            if (currentItem.subTasks != null)
            {
                modalSubTasks = currentItem.subTasks.ToList();
            }

            if (currentItem.assignedMembers != null)
            {
                modalAssignedMembers = currentItem.assignedMembers.ToList();
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Space(10);

        // Basic task info
        title = EditorGUILayout.TextField("Title", title);
        description = EditorGUILayout.TextArea(description, GUILayout.Height(60));

        dueDate = System.DateTime.Parse(EditorGUILayout.TextField("Due Date", dueDate.ToString("yyyy-MM-dd")));

        GUILayout.BeginHorizontal();
        {
            priority = (Priority)EditorGUILayout.EnumPopup("Priority", priority);
            category = (Category)EditorGUILayout.EnumPopup("Category", category);
            status = (Status)EditorGUILayout.EnumPopup("Status", status);
        }
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        {
            estimatedHours = EditorGUILayout.IntField("Estimated Hours", estimatedHours);
            actualHours = EditorGUILayout.IntField("Actual Hours", actualHours);
        }
        GUILayout.EndHorizontal();

        DrawMemberAssignmentSection();

        DrawSubtasksSection();

        // Status description based on selection
        DrawStatusDescription();

        // Asset references section
        DrawAssetReferencesSection();

        GUILayout.Space(20);

        // Action buttons
        GUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("Save"))
            {
                SaveItem();
                Close();
            }

            if (GUILayout.Button("Cancel"))
            {
                Close();
            }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawSubtasksSection()
    {
        EditorGUILayout.Space();
        GUILayout.Label("Subtasks", EditorStyles.boldLabel);

        // Add new subtask
        GUILayout.BeginHorizontal();
        {
            newModalSubTask = EditorGUILayout.TextField("New Subtask", newModalSubTask);
            if (GUILayout.Button("Add", GUILayout.Width(40)) && !string.IsNullOrEmpty(newModalSubTask))
            {
                modalSubTasks.Add(new TodoItem.SubTask { title = newModalSubTask, isCompleted = false });
                newModalSubTask = "";
            }
        }
        GUILayout.EndHorizontal();

        // Existing subtasks
        if (modalSubTasks.Count > 0)
        {
            EditorGUILayout.Space();
            for (int i = 0; i < modalSubTasks.Count; i++)
            {
                DrawModalSubTask(i);
            }

            // Progress
            int completed = modalSubTasks.Count(st => st.isCompleted);
            float progress = (float)completed / modalSubTasks.Count;
            EditorGUILayout.LabelField($"Progress: {completed}/{modalSubTasks.Count} ({progress:P0})");
        }
    }

    private void DrawModalSubTask(int index)
    {
        if (index >= modalSubTasks.Count) return;

        var subTask = modalSubTasks[index];

        GUILayout.BeginHorizontal();
        {
            subTask.isCompleted = EditorGUILayout.Toggle(subTask.isCompleted, GUILayout.Width(20));
            subTask.title = EditorGUILayout.TextField(subTask.title);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Remove", GUILayout.Width(60)))
            {
                modalSubTasks.RemoveAt(index);
            }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawMemberAssignmentSection()
    {
        EditorGUILayout.Space();
        GUILayout.Label("Team Assignment", EditorStyles.boldLabel);

        if (availableMembers.Count <= 1)
        {
            EditorGUILayout.HelpBox("Add team members in the main window to assign tasks.", MessageType.Info);
            return;
        }

        // Assignment buttons
        GUILayout.BeginHorizontal();
        {
            if (GUILayout.Button("Assign All"))
            {
                modalAssignedMembers = availableMembers.Where(m => m.isActive).Select(m => m.id).ToList();
            }

            if (GUILayout.Button("Clear All"))
            {
                modalAssignedMembers.Clear();
            }
        }
        GUILayout.EndHorizontal();

        // Member list
        foreach (var member in availableMembers.Where(m => m.isActive))
        {
            bool isAssigned = modalAssignedMembers.Contains(member.id);
            bool newAssigned = EditorGUILayout.ToggleLeft($"{member.name} ({member.role})", isAssigned);

            if (newAssigned != isAssigned)
            {
                if (newAssigned)
                    modalAssignedMembers.Add(member.id);
                else
                    modalAssignedMembers.Remove(member.id);
            }
        }

        // Show current assignments
        if (modalAssignedMembers.Count > 0)
        {
            EditorGUILayout.Space();
            GUILayout.Label("Currently Assigned To:", EditorStyles.miniBoldLabel);
            GUILayout.BeginHorizontal();
            {
                foreach (var memberId in modalAssignedMembers)
                {
                    var member = availableMembers.FirstOrDefault(m => m.id == memberId);
                    if (member != null)
                    {
                        // Draw mini badge
                        GUIStyle badgeStyle = new GUIStyle(EditorStyles.miniButton);
                        badgeStyle.normal.textColor = Color.white;
                        var bgColor = member.color;
                        bgColor.a = 0.8f;
                        badgeStyle.normal.background = MakeTex(2, 2, bgColor);
                        GUILayout.Label(member.initials, badgeStyle, GUILayout.Width(25));
                    }
                }
            }
            GUILayout.EndHorizontal();
        }
    }

    private void DrawStatusDescription()
    {
        string statusDescription = GetStatusDescription(status);
        if (!string.IsNullOrEmpty(statusDescription))
        {
            EditorGUILayout.HelpBox(statusDescription, MessageType.Info);
        }
    }

    private string GetStatusDescription(Status status)
    {
        return status switch
        {
            Status.NotStarted => "Task is ready to be started",
            Status.InProgress => "Task is currently being worked on",
            Status.Completed => "Task is finished",
            Status.OnHold => "Task is temporarily paused",
            Status.Blocked => "Task cannot proceed due to dependencies or issues",
            _ => ""
        };
    }

    // === MISSING ASSET REFERENCE METHODS ===

    private void DrawAssetReferencesSection()
    {
        EditorGUILayout.Space();
        GUILayout.Label("Asset References", EditorStyles.boldLabel);

        // Drag and drop area
        Rect dropArea = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
        GUI.Box(dropArea, "Drag assets here to link them to this task\n\nSupported: Scripts, Scenes, Prefabs, Textures, Animations, etc.", EditorStyles.helpBox);

        HandleDragAndDrop(dropArea);

        // List current assets
        EditorGUILayout.Space();
        GUILayout.Label("Linked Assets:", EditorStyles.miniLabel);

        if (assetReferences.Count == 0)
        {
            EditorGUILayout.HelpBox("No assets linked to this task", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < assetReferences.Count; i++)
            {
                DrawAssetReference(i);
            }
        }

        // Add asset manually
        EditorGUILayout.Space();
        GUILayout.BeginHorizontal();
        {
            UnityEngine.Object newAsset = EditorGUILayout.ObjectField("Add Asset:", null, typeof(UnityEngine.Object), false);
            if (newAsset != null)
            {
                if (!assetReferences.Contains(newAsset))
                {
                    assetReferences.Add(newAsset);
                }
                // Clear the field
                Repaint();
            }

            if (GUILayout.Button("Add Selection", GUILayout.Width(100)))
            {
                AddCurrentSelection();
            }
        }
        GUILayout.EndHorizontal();
    }

    private void DrawAssetReference(int index)
    {
        if (index >= assetReferences.Count) return;

        var asset = assetReferences[index];
        if (asset == null)
        {
            // Remove null assets
            assetReferences.RemoveAt(index);
            return;
        }

        GUILayout.BeginVertical("box");
        {
            GUILayout.BeginHorizontal();
            {
                // Asset field (read-only display)
                EditorGUI.BeginDisabledGroup(true);
                EditorGUILayout.ObjectField(asset, typeof(UnityEngine.Object), false);
                EditorGUI.EndDisabledGroup();

                GUILayout.FlexibleSpace();

                // Action buttons
                if (GUILayout.Button("Ping", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    EditorGUIUtility.PingObject(asset);
                }

                if (GUILayout.Button("Open", EditorStyles.miniButton, GUILayout.Width(50)))
                {
                    AssetDatabase.OpenAsset(asset);
                }

                if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(60)))
                {
                    assetReferences.RemoveAt(index);
                    return; // Exit since we modified the list
                }
            }
            GUILayout.EndHorizontal();

            // Asset path
            string assetPath = AssetDatabase.GetAssetPath(asset);
            EditorGUILayout.LabelField(assetPath, EditorStyles.miniLabel);
        }
        GUILayout.EndVertical();
    }

    private void HandleDragAndDrop(Rect dropArea)
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
                    if (!assetReferences.Contains(draggedObject))
                    {
                        assetReferences.Add(draggedObject);
                    }
                }
                evt.Use();
                Repaint();
            }
        }
    }

    private void AddCurrentSelection()
    {
        var selectedAssets = Selection.objects;
        foreach (var asset in selectedAssets)
        {
            if (!assetReferences.Contains(asset))
            {
                assetReferences.Add(asset);
            }
        }
        Repaint();
    }

    private void SaveItem()
    {
        TodoItem item = currentItem ?? new TodoItem();

        if (currentItem == null)
        {
            item.id = System.Guid.NewGuid().ToString();
            item.createdDate = System.DateTime.Now;
        }

        item.title = title;
        item.description = description;
        item.dueDate = dueDate;
        item.priority = priority;
        item.category = category;
        item.status = status;
        item.estimatedHours = estimatedHours;
        item.actualHours = actualHours;

        item.assignedMembers = modalAssignedMembers.Count > 0 ? modalAssignedMembers.ToArray() : null;

        item.subTasks = modalSubTasks.Count > 0 ? modalSubTasks.ToArray() : null;

        // Save asset references
        item.referencedAssetGUIDs = assetReferences
            .Where(a => a != null)
            .Select(a => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(a)))
            .Where(guid => !string.IsNullOrEmpty(guid))
            .ToArray();

        onSave?.Invoke(item);
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
}