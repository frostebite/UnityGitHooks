using System;
using System.Net;
using System.Threading;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

public class GitHookHttpListener : ICallbacks
{
    private static HttpListener listener;
    private Thread listenerThread;
    private TestRunnerApi testRunnerApi;
    private HttpListenerContext context;
    private CancellationTokenSource cancellationTokenSource;
    private readonly object streamLock = new object();

    public bool IsRunning => listener != null && listener.IsListening;

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

    public void Stop()
    {
        cancellationTokenSource?.Cancel();
        listener?.Stop();
        listenerThread?.Join();
        listener?.Close();
        listener = null;
    }

    void ListenForCommands(CancellationToken token)
    {
        try
        {
            while (listener.IsListening && !token.IsCancellationRequested)
            {
                context = listener.GetContext();
                Debug.Log("[GitHookHttpListener] Command received!");

                // check repoPath header log if exists
                string repoPathHeader = context.Request.Headers["repoPath"];
                if (!string.IsNullOrEmpty(repoPathHeader))
                {
                    Debug.Log($"[GitHookHttpListener] Repo path: {repoPathHeader}");
                }
                string testCategoryHeader = context.Request.Headers["testCategory"];
                if (!string.IsNullOrEmpty(testCategoryHeader))
                {
                    Debug.Log($"[GitHookHttpListener] Test category: {testCategoryHeader}");
                }
                string testModeHeader = context.Request.Headers["testMode"];
                if (!string.IsNullOrEmpty(testModeHeader))
                {
                    Debug.Log($"[GitHookHttpListener] Test mode: {testModeHeader}");
                }

                // Initialize response
                context.Response.StatusCode = 200;
                context.Response.ContentType = "text/plain; charset=utf-8";

                // run unity test on main thread
                UnityLefthook.Enqueue(() => UnityLefthook.Listener.RunGitHook());
            }
        }
        catch (ObjectDisposedException)
        {
            // listener disposed, this is expected when stopping
            Debug.Log("[GitHookHttpListener] Listener disposed");
        }
        catch (ThreadAbortException)
        {
            // Thread is being aborted (e.g., during shutdown) - this is expected behavior
            // ThreadAbortException will automatically re-throw, so we just catch it to avoid logging as an error
        }
        catch (HttpListenerException ex)
        {
            // Handle HTTP listener exceptions (e.g., when stopping)
            if (listener.IsListening)
            {
                Debug.LogWarning($"[GitHookHttpListener] HTTP listener exception: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GitHookHttpListener] Unexpected error in listener: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public void RunGitHook() {
        if (context == null)
        {
            Debug.LogError("[GitHookHttpListener] Cannot run git hook: context is null");
            return;
        }

        Debug.Log("[GitHookHttpListener] Running tests");
        testRunnerApi = ScriptableObject.CreateInstance<TestRunnerApi>();

        var mode = context.Request.Headers["testMode"];
        if (string.IsNullOrEmpty(mode))
        {
            mode = "EditMode";
        }

        var categoryHeader = context.Request.Headers["testCategory"];
        var categoryNames = !string.IsNullOrEmpty(categoryHeader)
            ? categoryHeader.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            : Array.Empty<string>();

        // log it
        Debug.Log($"[GitHookHttpListener] Test mode: {mode}");
        Debug.Log($"[GitHookHttpListener] Test category: {string.Join(",", categoryNames)}");

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
        Debug.Log($"[GitHookHttpListener] Tests finished - Passed: {result.PassCount}, Failed: {result.FailCount}");
        testRunnerApi.UnregisterCallbacks(this);
        Application.logMessageReceived -= ApplicationOnlogMessageReceived;

        if (context == null || context.Response == null)
        {
            Debug.LogWarning("[GitHookHttpListener] Cannot write test results: context or response is null");
            return;
        }

        var resultBuffer = System.Text.Encoding.UTF8.GetBytes($"Passed: {result.PassCount}, Failed: {result.FailCount}\n");
        lock (streamLock)
        {
            try
            {
                WriteToStream(resultBuffer);
                if (result.FailCount > 0)
                {
                    WriteToStream(System.Text.Encoding.UTF8.GetBytes("Tests failed\n"));
                    context.Response.StatusCode = 500;
                }
                else
                {
                    context.Response.StatusCode = 200;
                }

                context.Response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GitHookHttpListener] Error writing test results: {ex.Message}");
            }
        }
    }

    private void WriteToStream(byte[] buffer)
    {
        if (context?.Response?.OutputStream == null || !context.Response.OutputStream.CanWrite)
        {
            Debug.LogWarning("[GitHookHttpListener] Cannot write to stream: context or response stream is null or not writable");
            return;
        }

        try
        {
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.OutputStream.Flush(); // Ensure data is sent immediately
        }
        catch (Exception ex)
        {
            Debug.LogError($"[GitHookHttpListener] Error writing to stream: {ex.Message}");
        }
    }

    public void OnApplicationQuit()
    {
        Stop();
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

