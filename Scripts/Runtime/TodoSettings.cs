using System.Linq;
using UnityEngine;

[CreateAssetMenu(fileName = "TodoSettings", menuName = "TODO/Todo Settings")]
public class TodoSettings : ScriptableObject
{
    [Header("Priority Colors")]
    public Color highPriorityColor = Color.red;
    public Color mediumPriorityColor = new Color(1f, 0.5f, 0f); // Orange
    public Color lowPriorityColor = Color.green;
    public Color criticalPriorityColor = new Color(0.5f, 0f, 0.5f); // Purple

    [Header("Status Colors")]
    public Color notStartedColor = Color.gray;
    public Color inProgressColor = new Color(0.2f, 0.6f, 1f); // Blue
    public Color completedColor = new Color(0.2f, 0.8f, 0.2f); // Green
    public Color onHoldColor = new Color(1f, 0.8f, 0.2f); // Yellow
    public Color blockedColor = new Color(1f, 0.3f, 0.3f); // Red

    [Header("Category Colors")]
    public Color programmingColor = new Color(0.2f, 0.6f, 1f); // Blue
    public Color artColor = new Color(0.8f, 0.2f, 0.8f); // Magenta
    public Color designColor = new Color(0f, 0.8f, 0.8f); // Cyan
    public Color testingColor = new Color(0.2f, 0.8f, 0.2f); // Green
    public Color documentationColor = Color.white;
    public Color audioColor = new Color(0.8f, 0.4f, 1f); // Purple
    public Color animationColor = new Color(1f, 0.5f, 0f); // Orange
    public Color uiColor = new Color(0.9f, 0.9f, 0.2f); // Yellow

    [Header("Behavior Settings")]
    public bool autoSave = true;
    public float autoSaveInterval = 60f; // seconds
    public bool showConfirmationDialogs = true;
    public bool enableDueDateWarnings = true;
    public int dueDateWarningDays = 1;
    public bool enableAssetLinking = true;
    public bool enableContextMenuItems = true;
    public bool enableQuickStatusActions = true;

    [Header("Visual Settings")]
    public bool compactMode = false;
    public bool showProgressBars = true;
    public bool showAssetReferences = true;
    public bool showStatusIndicators = true;
    public bool showCategoryIcons = true;
    public bool colorCodeTasks = true;
    public bool showTaskStatistics = true;

    [Header("Window Settings")]
    public bool openAsUtilityWindow = true;
    public bool keepWindowOnTop = true;
    public Vector2 defaultWindowSize = new Vector2(500, 700);
    public bool rememberWindowPosition = true;

    [Header("Default Values")]
    public Priority defaultPriority = Priority.Medium;
    public Category defaultCategory = Category.General;
    public Status defaultStatus = Status.NotStarted;
    public int defaultEstimateHours = 2;
    public int defaultDueDays = 7;

    [Header("Notification Settings")]
    public bool enableNotifications = true;
    public bool notifyOnDueDate = true;
    public bool notifyOnOverdue = true;
    public bool playNotificationSound = false;
    public AudioClip notificationSound;

    [Header("Export Settings")]
    public bool includeAssetsInExport = true;
    public bool includeSubtasksInExport = true;
    public string defaultExportPath = "Exports/";

    [Header("Integration Settings")]
    public bool enableSceneIntegration = true;
    public bool enableAssetIntegration = true;
    public bool enableScriptIntegration = true;
    public string[] excludedAssetTypes;

    // Helper methods to get colors
    public Color GetPriorityColor(Priority priority)
    {
        return priority switch
        {
            Priority.Critical => criticalPriorityColor,
            Priority.High => highPriorityColor,
            Priority.Medium => mediumPriorityColor,
            Priority.Low => lowPriorityColor,
            _ => Color.gray
        };
    }

    public Color GetStatusColor(Status status)
    {
        return status switch
        {
            Status.NotStarted => notStartedColor,
            Status.InProgress => inProgressColor,
            Status.Completed => completedColor,
            Status.OnHold => onHoldColor,
            Status.Blocked => blockedColor,
            _ => Color.white
        };
    }

    public Color GetCategoryColor(Category category)
    {
        return category switch
        {
            Category.Programming => programmingColor,
            Category.Art => artColor,
            Category.Design => designColor,
            Category.Testing => testingColor,
            Category.Documentation => documentationColor,
            Category.Audio => audioColor,
            Category.Animation => animationColor,
            Category.UI => uiColor,
            _ => Color.gray
        };
    }

    public string GetStatusSymbol(Status status)
    {
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

    public string GetStatusDescription(Status status)
    {
        return status switch
        {
            Status.NotStarted => "Task is ready to be started",
            Status.InProgress => "Task is currently being worked on",
            Status.Completed => "Task is finished",
            Status.OnHold => "Task is temporarily paused",
            Status.Blocked => "Task cannot proceed due to dependencies or issues",
            _ => "Unknown status"
        };
    }

    public string GetPriorityDescription(Priority priority)
    {
        return priority switch
        {
            Priority.Critical => "Must be completed immediately",
            Priority.High => "Important and time-sensitive",
            Priority.Medium => "Normal priority task",
            Priority.Low => "Can be done when time permits",
            _ => "No priority set"
        };
    }

    // Validation methods
    public bool IsAssetTypeAllowed(UnityEngine.Object asset)
    {
        if (!enableAssetLinking) return false;
        if (asset == null) return false;

        string assetPath = UnityEditor.AssetDatabase.GetAssetPath(asset);
        string extension = System.IO.Path.GetExtension(assetPath).ToLower().TrimStart('.');

        return !excludedAssetTypes.Contains(extension);
    }

    public bool ShouldShowDueDateWarning(System.DateTime dueDate)
    {
        if (!enableDueDateWarnings) return false;

        System.TimeSpan timeUntilDue = dueDate - System.DateTime.Now;
        return timeUntilDue.TotalDays <= dueDateWarningDays && timeUntilDue.TotalDays >= 0;
    }

    public bool IsTaskOverdue(TodoItem task)
    {
        return task.dueDate < System.DateTime.Now && task.status != Status.Completed;
    }

    // Reset to default values
    [ContextMenu("Reset to Defaults")]
    public void ResetToDefaults()
    {
        // Priority Colors
        highPriorityColor = Color.red;
        mediumPriorityColor = new Color(1f, 0.5f, 0f);
        lowPriorityColor = Color.green;
        criticalPriorityColor = new Color(0.5f, 0f, 0.5f);

        // Status Colors
        notStartedColor = Color.gray;
        inProgressColor = new Color(0.2f, 0.6f, 1f);
        completedColor = new Color(0.2f, 0.8f, 0.2f);
        onHoldColor = new Color(1f, 0.8f, 0.2f);
        blockedColor = new Color(1f, 0.3f, 0.3f);

        // Behavior
        autoSave = true;
        showConfirmationDialogs = true;
        enableDueDateWarnings = true;
        enableAssetLinking = true;

        // Visual
        compactMode = false;
        showProgressBars = true;
        showAssetReferences = true;

        UnityEditor.EditorUtility.SetDirty(this);
    }

    [ContextMenu("Export Current Settings")]
    public void ExportSettings()
    {
        string json = JsonUtility.ToJson(this, true);
        string path = System.IO.Path.Combine(Application.dataPath, defaultExportPath, "TodoSettings_Backup.json");
        System.IO.File.WriteAllText(path, json);
        Debug.Log($"TODO Settings exported to: {path}");
    }
}