using System;
using System.Net;
using System.Threading;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using WebSocketSharp;

public class GitHookHttpListener : ICallbacks
{
    private static HttpListener listener;
    private Thread listenerThread;
    private TestRunnerApi testRunnerApi;
    private HttpListenerContext context;
    private CancellationTokenSource cancellationTokenSource;
    private readonly object streamLock = new object();

    public void Start()
    {
        listener = new HttpListener();
        var port = GitHookPreferences.Port;
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        cancellationTokenSource = new CancellationTokenSource();

        listenerThread = new Thread(() => ListenForCommands(cancellationTokenSource.Token));
        listenerThread.Start();
    }

    void ListenForCommands(CancellationToken token)
    {
        try
        {
            while (listener.IsListening && !token.IsCancellationRequested)
            context = listener.GetContext();
            Debug.Log("Command executed!");

            // check repoPath header log if exists
            if (context.Request.Headers.Contains("repoPath"))
            {
                Debug.Log(context.Request.Headers["repoPath"]);
            }
            if (context.Request.Headers.Contains("testCategory"))
            {
                context = listener.GetContext();
                Debug.Log("Command executed!");

                // check repoPath header log if exists
                if (context.Request.Headers.Contains("repoPath"))
                {
                    Debug.Log(context.Request.Headers["repoPath"]);
                }
                if (context.Request.Headers.Contains("testCategory"))
                {
                    Debug.Log(context.Request.Headers["testCategory"]);
                }

                // run unity test
                // run on main thread
                UnityLefthook.Enqueue(() => UnityLefthook.Listener.RunGitHook());
            }
        }
        catch (ObjectDisposedException)
        {
            // listener disposed

            // run unity test
            // run on main thread
            UnityLefthook.Enqueue(() => UnityLefthook.Listener.RunGitHook());
        }
    }

    public void RunGitHook() {
        Debug.Log("Running tests");
        testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();

        var mode = context.Request.Headers["testMode"];
        var categoryNames = context.Request.Headers.Contains("testCategory")
            ? context.Request.Headers["testCategory"].Split(",")
            : Array.Empty<string>();

        // log it
        Debug.Log($"Test mode: {mode}");
        Debug.Log($"Test category: {string.Join(",", categoryNames)}");

        var filter = new Filter
        {
            testMode = mode == "EditMode" ? TestMode.EditMode : TestMode.PlayMode,
            categoryNames = categoryNames
        };

        testRunnerApi.RegisterCallbacks(this);
        testRunnerApi.Execute(new ExecutionSettings(filter));

        Application.logMessageReceived += ApplicationOnlogMessageReceived;
    }

    private void ApplicationOnlogMessageReceived(string condition, string stacktrace, LogType type)
    {
        var buffer = System.Text.Encoding.UTF8.GetBytes($"{condition} {stacktrace} {type}\n");
        lock (streamLock)
        {
            WriteToStream(buffer);
        }
    }

    public void RunFinished(ITestResultAdaptor result)
    {
        Debug.Log("Tests finished");
        testRunnerApi.UnregisterCallbacks(this);
        Application.logMessageReceived -= ApplicationOnlogMessageReceived;

        var resultBuffer = System.Text.Encoding.UTF8.GetBytes($"Passed: {result.PassCount}, Failed: {result.FailCount}\n");
        Debug.Log($"Passed: {result.PassCount}, Failed: {result.FailCount}");
        lock (streamLock)
        {
            WriteToStream(resultBuffer);
            if (result.FailCount > 0)
            {
                WriteToStream(System.Text.Encoding.UTF8.GetBytes("Tests failed\n"));
            }

            // Optionally close the stream here if no more data will be sent
            context.Response.OutputStream.Close();
        }
    }

    private void WriteToStream(byte[] buffer)
    {
        if (!context.Response.OutputStream.CanWrite)
        {
            return;
        }

        context.Response.OutputStream.Write(buffer, 0, buffer.Length);
        context.Response.OutputStream.Flush(); // Ensure data is sent immediately
    }

    public void OnApplicationQuit()
    {
        cancellationTokenSource?.Cancel();
        listener?.Stop();
        listenerThread?.Join();
        listener?.Close();

        testRunnerApi?.UnregisterCallbacks(this);
        Application.logMessageReceived -= ApplicationOnlogMessageReceived;

        cancellationTokenSource?.Dispose();
    }

    public void RunStarted(ITestAdaptor testsToRun) {
    }

    public void TestStarted(ITestAdaptor test) {
    }

    public void TestFinished(ITestResultAdaptor result) {
    }
}

