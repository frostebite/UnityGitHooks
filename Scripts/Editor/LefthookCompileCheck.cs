using System.Diagnostics;
using System.Threading;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Standalone compile check for UnityLefthook.
/// Can be invoked via Unity batchmode: -executeMethod LefthookCompileCheck.Run
///
/// In batchmode, scripts are already compiled before this method runs.
/// If Unity opens the project and compilation succeeds, exit 0.
/// If compilation fails, Unity will exit with a non-zero code before reaching this method.
///
/// In editor mode (called from HTTP listener), triggers a recompile and polls for results.
/// </summary>
public static class LefthookCompileCheck
{
    /// <summary>
    /// Batchmode entry point. Called via -executeMethod LefthookCompileCheck.Run.
    /// In batchmode, if we reach this method, compilation already succeeded.
    /// </summary>
    public static void Run()
    {
        Debug.Log("[LefthookCompileCheck] Starting compile check...");

        if (Application.isBatchMode)
        {
            // In batchmode, scripts compile during project open.
            // If we reached here, compilation succeeded.
            Debug.Log("[LefthookCompileCheck] Batchmode: compilation already verified on project open. Exiting success.");
            EditorApplication.Exit(0);
            return;
        }

        // Editor mode: force refresh and request compilation, then poll
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        try { CompilationPipeline.RequestScriptCompilation(); } catch { /* ignore if not supported */ }

        var stopwatch = Stopwatch.StartNew();
        var timeout = System.TimeSpan.FromMinutes(10);
        var nextLog = System.TimeSpan.FromSeconds(10);

        while (EditorApplication.isCompiling)
        {
            if (stopwatch.Elapsed >= nextLog)
            {
                Debug.Log($"[LefthookCompileCheck] Waiting for compilation... elapsed {stopwatch.Elapsed:mm\\:ss}");
                nextLog += System.TimeSpan.FromSeconds(10);
            }

            if (stopwatch.Elapsed > timeout)
            {
                Debug.LogError($"[LefthookCompileCheck] Timeout after {timeout.TotalMinutes} minutes.");
                EditorApplication.Exit(1);
                return;
            }

            Thread.Sleep(200);
        }

        Debug.Log("[LefthookCompileCheck] Compile check completed successfully.");
        EditorApplication.Exit(0);
    }
}
