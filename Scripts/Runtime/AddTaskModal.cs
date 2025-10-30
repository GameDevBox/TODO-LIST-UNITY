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

    private List<UnityEngine.Object> assetReferences = new List<UnityEngine.Object>();

    public static void ShowWindow(TodoItem item, System.Action<TodoItem> saveCallback)
    {
        AddTaskModal window = CreateInstance<AddTaskModal>();
        window.currentItem = item;
        window.onSave = saveCallback;
        window.InitializeFromItem();
        window.ShowUtility();
        window.titleContent = new GUIContent(item == null ? "Add New Task" : "Edit Task");
        window.minSize = new Vector2(500, 600);
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

        // Save asset references
        item.referencedAssetGUIDs = assetReferences
            .Where(a => a != null)
            .Select(a => AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(a)))
            .Where(guid => !string.IsNullOrEmpty(guid))
            .ToArray();

        onSave?.Invoke(item);
    }
}