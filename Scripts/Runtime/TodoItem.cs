using System;
using UnityEngine;
using UnityEditor;

[System.Serializable]
public class TodoItem
{
    public string id;
    public string title;
    public string description;
    public DateTime dueDate;
    public DateTime createdDate;
    public Priority priority;
    public Category category;
    public Status status;
    public int estimatedHours;
    public int actualHours;
    public SubTask[] subTasks;

    // Team Assignment
    public string[] assignedMembers; // Array of member IDs
    public string createdBy; // Creator member ID

    // Asset References
    public string[] referencedAssetGUIDs;
    public string[] referencedScenePaths;
    public string[] referencedScriptPaths;

    // Context
    public string gameObjectPath;
    public string componentType;
    public BuildTarget buildTarget;

    // Code Context
    public string methodName;
    public int lineNumber;
    public string gitBranch;

    [System.Serializable]
    public class SubTask
    {
        public string title;
        public bool isCompleted;
        public string[] assignedTo; // Members assigned to this subtask
        public string[] assetGUIDs;
    }
}

// Add TeamMember class
[System.Serializable]
public class TeamMember
{
    public string id;
    public string name;
    public string role;
    public Color color;
    public string initials;
    public bool isActive = true;

    public TeamMember(string memberName, string memberRole = "Developer")
    {
        id = Guid.NewGuid().ToString();
        name = memberName;
        role = memberRole;
        color = GenerateRandomColor();
        initials = GetInitials(memberName);
    }

    private Color GenerateRandomColor()
    {
        var colors = new Color[]
        {
            new Color(0.2f, 0.6f, 1f), // Blue
            new Color(0.8f, 0.2f, 0.2f), // Red
            new Color(0.2f, 0.8f, 0.2f), // Green
            new Color(0.8f, 0.6f, 0.2f), // Orange
            new Color(0.6f, 0.2f, 0.8f), // Purple
            new Color(0.2f, 0.8f, 0.8f), // Cyan
            new Color(0.8f, 0.2f, 0.8f), // Magenta
        };
        return colors[UnityEngine.Random.Range(0, colors.Length)];
    }

    private string GetInitials(string fullName)
    {
        if (string.IsNullOrEmpty(fullName)) return "??";
        var parts = fullName.Split(' ');
        if (parts.Length == 1) return parts[0].Length >= 2 ? parts[0].Substring(0, 2).ToUpper() : parts[0].ToUpper();
        return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
    }
}

public enum Priority { All, Low, Medium, High, Critical }
public enum Category { All, General, Programming, Art, Design, Testing, Documentation, Audio, Animation, UI }
public enum Status { All, NotStarted, InProgress, Completed, OnHold, Blocked }

