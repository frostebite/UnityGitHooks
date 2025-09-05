using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using UnityEditor;
using NUnit.Framework.Interfaces;
using UnityEngine;

public class UnityLefthook
{
    public static GitHookHttpListener Listener => _listener;
    private static GitHookHttpListener _listener;
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    // when unity editor initalizes, initalize UnityLefthook
    [UnityEditor.InitializeOnLoadMethod]
    private static void Initialize()
    {
        // if headless mode, CI, or any automated environment ignore
        if (Application.isBatchMode || IsRunningInCI() || IsHeadlessEnvironment() || IsExplicitlyDisabled())
        {
            Debug.Log("[UnityLefthook] Skipping initialization in batch/CI/headless mode");
            return;
        }
        
        try
        {
            InitalizeLefthook();
            if (_listener != null)
            {
                _listener.OnApplicationQuit();
                _listener = null;
            }
            // start http listener
            _listener = new GitHookHttpListener();
            // handle cleanup
            EditorApplication.quitting += () => _listener.OnApplicationQuit();
            _listener.Start();
            EditorApplication.update += Update;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnityLefthook] Failed to initialize: {ex.Message}");
        }
    }
    private static bool IsExplicitlyDisabled()
    {
        // Honor explicit environment switch to disable Lefthook initialization
        var disable = Environment.GetEnvironmentVariable("UNITY_DISABLE_LEFTHOOK");
        return !string.IsNullOrEmpty(disable) && (disable == "1" || disable.Equals("true", StringComparison.OrdinalIgnoreCase));
    }
    private static bool IsRunningInCI()
    {
        // Check common CI environment variables
        var ciVariables = new[] { "CI", "GITHUB_ACTIONS", "JENKINS_URL", "BUILDKITE", "CIRCLECI", "TRAVIS", "APPVEYOR" };
        foreach (var variable in ciVariables)
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)))
            {
                return true;
            }
        }
        return false;
    }
    
    private static bool IsHeadlessEnvironment()
    {
        // Additional checks for headless/automated environments
        return SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null ||
               Environment.GetCommandLineArgs().Any(arg => arg.Contains("-batchmode") || arg.Contains("-nographics"));
    }
    
    private static void InitalizeLefthook()
    {
        // run lefthook command with timeout to prevent hanging
        System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo();
        start.FileName = "lefthook";
        start.Arguments = "version";
        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.CreateNoWindow = true;
        
        try {
            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(start))
            {
                // Add timeout to prevent hanging in CI
                if (process.WaitForExit(5000)) // 5 second timeout
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    Debug.Log($"[UnityLefthook] Version check: {output}");
                    // if error is not empty or exit code is not 0, log install lefthook message
                    if (string.IsNullOrEmpty(error) && process.ExitCode == 0)
                    {
                        return;
                    }
                }
                else
                {
                    Debug.LogWarning("[UnityLefthook] Lefthook version check timed out");
                    process.Kill();
                }
            }
        } catch (Exception ex) {
            Debug.LogWarning($"[UnityLefthook] Failed to check lefthook version: {ex.Message}");
        }

        Debug.Log("[UnityLefthook] Lefthook is not installed. You can install it by going to Window > GitHooks > Install Window");
    }
    

    public static void Enqueue(Action action)
    {
        lock (_executionQueue)
        {
            _executionQueue.Enqueue(action);
        }
    }

    private static void Update()
    {
        lock (_executionQueue)
        {
            while (_executionQueue.Count > 0)
            {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }
    
}