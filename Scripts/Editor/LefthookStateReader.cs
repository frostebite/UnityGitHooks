using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Reads lefthook hook execution state from .git/lefthook-state/hook-state.json.
/// This provides a reliable file-based fallback for monitoring hook progress
/// when the HTTP listener is unavailable (e.g., editor is busy compiling).
/// </summary>
public static class LefthookStateReader
{
    private const string StateDirName = "lefthook-state";
    private const string StateFileName = "hook-state.json";

    private static string _cachedStatePath;
    private static DateTime _lastFileWriteTime;
    private static LefthookHookState _cachedState;

    /// <summary>
    /// Get the path to hook-state.json under .git/lefthook-state/.
    /// Returns null if .git cannot be found.
    /// </summary>
    public static string GetStatePath()
    {
        if (_cachedStatePath != null && File.Exists(_cachedStatePath))
            return _cachedStatePath;

        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        var gitDir = Path.Combine(projectRoot, ".git");

        if (!Directory.Exists(gitDir))
        {
            // Could be a .git file (worktree)
            if (File.Exists(gitDir))
            {
                try
                {
                    var content = File.ReadAllText(gitDir).Trim();
                    if (content.StartsWith("gitdir:"))
                    {
                        var relative = content.Substring("gitdir:".Length).Trim();
                        gitDir = Path.GetFullPath(Path.Combine(projectRoot, relative));
                    }
                }
                catch
                {
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        _cachedStatePath = Path.Combine(gitDir, StateDirName, StateFileName);
        return _cachedStatePath;
    }

    /// <summary>
    /// Read the current hook state. Returns null if missing, corrupt, or unreadable.
    /// Uses file modification time to avoid re-parsing unchanged files.
    /// </summary>
    public static LefthookHookState ReadState()
    {
        var statePath = GetStatePath();
        if (statePath == null || !File.Exists(statePath))
            return null;

        try
        {
            var writeTime = File.GetLastWriteTimeUtc(statePath);
            if (_cachedState != null && writeTime == _lastFileWriteTime)
                return _cachedState;

            var json = File.ReadAllText(statePath);
            _cachedState = JsonUtility.FromJson<LefthookHookState>(json);
            _lastFileWriteTime = writeTime;
            return _cachedState;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LefthookState] Failed to read state: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Write state to the file (for editor-side updates like test results).
    /// </summary>
    public static bool WriteState(LefthookHookState state)
    {
        var statePath = GetStatePath();
        if (statePath == null) return false;

        try
        {
            var dir = Path.GetDirectoryName(statePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var json = JsonUtility.ToJson(state, true);
            var tmpPath = statePath + ".tmp";
            File.WriteAllText(tmpPath, json);
            if (File.Exists(statePath))
                File.Delete(statePath);
            File.Move(tmpPath, statePath);

            _cachedState = state;
            _lastFileWriteTime = File.GetLastWriteTimeUtc(statePath);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[LefthookState] Failed to write state: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Update the test summary on the current state and write it back.
    /// Safe to call even if no state file exists (no-op).
    /// </summary>
    public static void UpdateTestSummary(int passCount, int failCount, string testMode, string category, string logFile)
    {
        var state = ReadState();
        if (state == null) return;

        state.testSummary = new LefthookTestSummary
        {
            passCount = passCount,
            failCount = failCount,
            testMode = testMode,
            category = category,
            logFile = logFile
        };

        WriteState(state);
    }

    /// <summary>
    /// Invalidate the cached state, forcing a re-read on next call.
    /// </summary>
    public static void InvalidateCache()
    {
        _cachedState = null;
        _lastFileWriteTime = default;
    }

    /// <summary>
    /// Check if the current state indicates a hook is actively running.
    /// Includes stale detection based on PID existence.
    /// </summary>
    public static bool IsHookRunning()
    {
        var state = ReadState();
        if (state == null) return false;
        if (state.status != "running") return false;

        // Check if PID is still alive (stale detection)
        if (state.pid > 0)
        {
            try
            {
                var process = System.Diagnostics.Process.GetProcessById(state.pid);
                return !process.HasExited;
            }
            catch
            {
                // Process doesn't exist - state is stale
                return false;
            }
        }

        return true;
    }
}

[Serializable]
public class LefthookHookState
{
    public string hookName;
    public string status;
    public string startTime;
    public string endTime;
    public int pid;
    public string machineName;
    public List<LefthookStepState> steps;
    public string result;
    public string error;
    public LefthookTestSummary testSummary;
}

[Serializable]
public class LefthookStepState
{
    public string name;
    public string status;
    public string startTime;
    public string endTime;
    public string detail;
}

[Serializable]
public class LefthookTestSummary
{
    public int passCount;
    public int failCount;
    public string testMode;
    public string category;
    public string logFile;
}
