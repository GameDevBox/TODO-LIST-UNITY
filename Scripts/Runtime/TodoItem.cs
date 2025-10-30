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
    
    // Asset References
    public string[] referencedAssetGUIDs;
    public string[] referencedScenePaths;
    public string[] referencedScriptPaths;
    
    // Context
    public string assignedTo; // Team member name
    public string gameObjectPath; // Specific GameObject in scene
    public string componentType; // Specific component to work on
    
    // Code Context
    public string methodName;
    public int lineNumber;
    public string gitBranch;
    
    [System.Serializable]
    public class SubTask
    {
        public string title;
        public bool isCompleted;
        public string[] assetGUIDs; // Assets specific to this subtask
    }
}

public enum Priority { All, Low, Medium, High, Critical }
public enum Category { All, General, Programming, Art, Design, Testing, Documentation, Audio, Animation, UI }
public enum Status { All, NotStarted, InProgress, Completed, OnHold, Blocked }

