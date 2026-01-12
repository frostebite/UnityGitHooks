using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class LeftHookWindow : EditorWindow
{
    private bool isRunning;
    private string statusMessage = string.Empty;
    private bool lefthookInstalled;
    private bool repoHooksInstalled;
    private bool hooksInstalled;
    private bool listenerRunning;
    private bool debugLoggingEnabled;
    private bool lefthookDirExists;
    private bool lefthookInitialized;
    private string lefthookVersion = string.Empty;
    private string projectRoot = string.Empty;
    private Vector2 scrollPosition;

    [MenuItem("Window/GitHooks/Lefthook Status")]
    public static void ShowWindow()
    {
        // Check if window already exists
        var window = GetWindow<LeftHookWindow>("Lefthook Status");
        if (window != null)
        {
            window.Focus();
        }
    }

    private void OnEnable()
    {
        CheckStatus();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        EditorGUILayout.Space(5);
        
        // Title
        EditorGUILayout.LabelField("Lefthook Status", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);
        
        // Overall Status
        EditorGUILayout.LabelField("Status", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        DrawStatusRow("Initialized", lefthookInitialized, lefthookInitialized ? "Yes" : "No", true);
        EditorGUI.indentLevel--;
        
        EditorGUILayout.Space(5);
        
        // Installation Section
        EditorGUILayout.LabelField("Installation", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        DrawStatusRow("Lefthook", lefthookInstalled, lefthookInstalled ? $"Installed ({lefthookVersion})" : "Not installed");
        EditorGUI.indentLevel--;
        
        EditorGUILayout.Space(5);
        
        // Configuration Section
        EditorGUILayout.LabelField("Configuration", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        DrawStatusRow("Config File", repoHooksInstalled, repoHooksInstalled ? "Found (lefthook.yml)" : "Missing");
        DrawStatusRow("Git Hooks", hooksInstalled, hooksInstalled ? "Installed" : "Not installed");
        DrawStatusRow("Listener", listenerRunning, listenerRunning ? "Running" : "Stopped");
        EditorGUI.indentLevel--;
        
        EditorGUILayout.Space(5);
        
        // Debug & Logs Section
        EditorGUILayout.LabelField("Debug & Logs", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        DrawStatusRow("Debug Logging", debugLoggingEnabled, debugLoggingEnabled ? "Enabled" : "Disabled");
        DrawStatusRow(".lefthook Directory", lefthookDirExists, lefthookDirExists ? "Exists" : "Not found");
        EditorGUI.indentLevel--;
        
        EditorGUILayout.Space(5);
        
        // Project Information
        EditorGUILayout.LabelField("Project", EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        if (!string.IsNullOrEmpty(projectRoot))
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Root:", GUILayout.Width(60));
            EditorGUILayout.SelectableLabel(projectRoot, EditorStyles.textField);
            EditorGUILayout.EndHorizontal();
        }
        EditorGUI.indentLevel--;
        
        EditorGUILayout.Space(10);
        
        // Status message
        if (!string.IsNullOrEmpty(statusMessage))
        {
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        }
        
        EditorGUILayout.Space(5);
        
        // Action Buttons
        EditorGUI.BeginDisabledGroup(isRunning);
        
        if (GUILayout.Button("Refresh Status", GUILayout.Height(25)))
        {
            CheckStatus();
        }
        
        EditorGUILayout.Space(5);
        
        EditorGUILayout.LabelField("Actions", EditorStyles.boldLabel);
        
        if (!lefthookInstalled)
        {
            if (GUILayout.Button("Install Lefthook (npm)", GUILayout.Height(25)))
            {
                RunCommand("npm", "install lefthook --save-dev", closeOnFinish: false);
            }
        }
        
        if (!repoHooksInstalled)
        {
            if (GUILayout.Button("Install Hooks to Repository", GUILayout.Height(25)))
            {
                RunCommand("lefthook", "install", closeOnFinish: false);
            }
        }
        
        if (repoHooksInstalled && !hooksInstalled)
        {
            EditorGUILayout.HelpBox("Config file exists but hooks are not installed. Click 'Install Hooks to Repository' to install them.", MessageType.Warning);
        }
        
        EditorGUI.BeginDisabledGroup(UnityLefthook.Listener == null);
        if (listenerRunning)
        {
            if (GUILayout.Button("Stop Listener", GUILayout.Height(25)))
            {
                UnityLefthook.Listener.Stop();
                CheckStatus();
            }
        }
        else
        {
            if (GUILayout.Button("Start Listener", GUILayout.Height(25)))
            {
                UnityLefthook.Listener.Start();
                CheckStatus();
            }
        }
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.Space(5);
        
        if (GUILayout.Button("Open Preferences", GUILayout.Height(25)))
        {
            SettingsService.OpenUserPreferences("Preferences/Unity Git Hooks");
        }
        
        EditorGUI.EndDisabledGroup();
        
        EditorGUILayout.EndScrollView();
    }
    
    private void DrawStatusRow(string label, bool status, string statusText, bool isHeader = false)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label + ":", GUILayout.Width(150));
        
        var originalColor = GUI.color;
        var originalStyle = EditorStyles.miniLabel;
        if (isHeader)
        {
            GUI.color = status ? new Color(0.2f, 1f, 0.2f) : new Color(1f, 0.5f, 0.2f);
            EditorGUILayout.LabelField(statusText, EditorStyles.boldLabel);
        }
        else
        {
            GUI.color = status ? new Color(0.3f, 1f, 0.3f) : new Color(1f, 0.3f, 0.3f);
            EditorGUILayout.LabelField(statusText, EditorStyles.miniLabel);
        }
        GUI.color = originalColor;
        
        EditorGUILayout.EndHorizontal();
    }

    private async void RunCommand(string fileName, string arguments, bool closeOnFinish = false)
    {
        isRunning = true;
        statusMessage = $"Running {fileName} {arguments}...";
        Repaint();

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var output = new StringBuilder();
            var error = new StringBuilder();

            await Task.Run(() =>
            {
                using (var process = new Process { StartInfo = startInfo })
                {
                    process.OutputDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            output.AppendLine(e.Data);
                        }
                    };
                    process.ErrorDataReceived += (s, e) =>
                    {
                        if (e.Data != null)
                        {
                            error.AppendLine(e.Data);
                        }
                    };

                    process.Start();
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    process.WaitForExit();
                }
            });

            if (output.Length > 0)
            {
                Debug.Log(output.ToString());
            }

            if (error.Length > 0)
            {
                Debug.LogError(error.ToString());
            }

            statusMessage = "Command completed.";
            if (closeOnFinish)
            {
                Close();
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError(ex.Message);
            statusMessage = "Command failed.";
        }
        finally
        {
            isRunning = false;
            Repaint();
            CheckStatus();
        }
    }

    private async void CheckStatus()
    {
        isRunning = true;
        statusMessage = "Checking status...";
        Repaint();

        await Task.Run(() =>
        {
            lefthookInstalled = TryGetLefthookVersion(out lefthookVersion);
            projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            repoHooksInstalled = File.Exists(Path.Combine(projectRoot, "lefthook.yml"));
            hooksInstalled = CheckHooksInstalled(projectRoot);
            lefthookDirExists = Directory.Exists(Path.Combine(projectRoot, ".lefthook"));
            debugLoggingEnabled = CheckDebugLoggingEnabled();
            lefthookInitialized = CheckLefthookInitialized(projectRoot, lefthookInstalled, repoHooksInstalled, hooksInstalled);
        });
        
        // Check listener status on main thread
        EditorApplication.delayCall += () =>
        {
            listenerRunning = UnityLefthook.Listener != null && UnityLefthook.Listener.IsRunning;
            statusMessage = string.Empty;
            isRunning = false;
            Repaint();
        };
    }
    
    private bool CheckHooksInstalled(string projectRoot)
    {
        try
        {
            var hooksDir = Path.Combine(projectRoot, ".git", "hooks");
            if (!Directory.Exists(hooksDir))
            {
                return false;
            }
            
            // Check for common lefthook hooks
            var hookFiles = new[] { "pre-commit", "pre-push", "commit-msg" };
            foreach (var hookFile in hookFiles)
            {
                var hookPath = Path.Combine(hooksDir, hookFile);
                if (File.Exists(hookPath))
                {
                    // Read a portion of the file to check if it's a lefthook hook
                    try
                    {
                        var content = File.ReadAllText(hookPath);
                        if (content.Contains("lefthook") || content.Contains("LEFTHOOK"))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // If we can't read the file, skip it
                    }
                }
            }
        }
        catch
        {
            // Silently fail
        }
        
        return false;
    }
    
    private bool CheckDebugLoggingEnabled()
    {
        try
        {
            var debugEnv = Environment.GetEnvironmentVariable("LEFTHOOK_DEBUG");
            return !string.IsNullOrEmpty(debugEnv) && 
                   (debugEnv.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    debugEnv.Equals("1", StringComparison.OrdinalIgnoreCase) ||
                    debugEnv.Equals("yes", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
    
    private bool CheckLefthookInitialized(string projectRoot, bool lefthookInstalled, bool configExists, bool hooksInstalled)
    {
        // Lefthook is considered initialized if:
        // 1. Lefthook executable is installed
        // 2. Config file exists (lefthook.yml)
        // 3. Git hooks are installed (hooks exist in .git/hooks/)
        return lefthookInstalled && configExists && hooksInstalled;
    }

    private bool TryGetLefthookVersion(out string version)
    {
        version = string.Empty;
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "lefthook",
                Arguments = "version",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (var process = Process.Start(startInfo))
            {
                process?.WaitForExit();
                if (process != null && process.ExitCode == 0)
                {
                    version = process.StandardOutput.ReadToEnd().Trim();
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }
}

