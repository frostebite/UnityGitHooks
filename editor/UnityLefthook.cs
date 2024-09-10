using System;
using System.Collections.Generic;
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
        // if headless mode ignore
        if (Application.isBatchMode)
        {
            return;
        }
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
    private static void InitalizeLefthook()
    {
        // run lefthook command
        System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo();
        start.FileName = "lefthook";
        start.Arguments = "version";
        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.CreateNoWindow = true;
        
        try {
            System.Diagnostics.Process process = System.Diagnostics.Process.Start(start);
            process.WaitForExit();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            Debug.Log(output);
            // if error is not empty or exit code is not 0, log install lefthook message
            if (string.IsNullOrEmpty(error) && process.ExitCode == 0)
            {
                return;
            }
        } catch {
            // ignored
        }

        Debug.Log("Lefthook is not installed. Please install lefthook by running \n'winget install evilmartians.lefthook -e; lefthook add' in terminal");
        // open lefthook window
        LeftHookWindow.ShowWindow();
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