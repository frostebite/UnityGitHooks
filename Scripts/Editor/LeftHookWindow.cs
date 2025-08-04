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
    private bool listenerRunning;
    private string lefthookVersion = string.Empty;

    [MenuItem("Window/GitHooks/Install Window")]
    public static void ShowWindow()
    {
        // Check if window already exists
        var window = GetWindow<LeftHookWindow>("LeftHook");
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
        EditorGUILayout.LabelField("Lefthook", lefthookInstalled ? $"Installed ({lefthookVersion})" : "Not installed");
        EditorGUILayout.LabelField("Config", repoHooksInstalled ? "Found" : "Missing");
        EditorGUILayout.LabelField("Listener", listenerRunning ? "Running" : "Stopped");

        if (GUILayout.Button("Refresh Status"))
        {
            CheckStatus();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField(statusMessage);

        EditorGUI.BeginDisabledGroup(isRunning);
        if (GUILayout.Button("Install Lefthook"))
        {
            RunCommand("npm", "install lefthook --save-dev", closeOnFinish: true);
        }

        if (GUILayout.Button("Install Lefthook to this repo"))
        {
            RunCommand("lefthook", "install");
        }

        EditorGUI.BeginDisabledGroup(UnityLefthook.Listener == null);
        if (listenerRunning)
        {
            if (GUILayout.Button("Stop Listener"))
            {
                UnityLefthook.Listener.Stop();
                CheckStatus();
            }
        }
        else
        {
            if (GUILayout.Button("Start Listener"))
            {
                UnityLefthook.Listener.Start();
                CheckStatus();
            }
        }
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("Close"))
        {
            Close();
        }
        EditorGUI.EndDisabledGroup();
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
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            repoHooksInstalled = File.Exists(Path.Combine(projectRoot, "lefthook.yml"));
            listenerRunning = UnityLefthook.Listener != null && UnityLefthook.Listener.IsRunning;
        });

        statusMessage = string.Empty;
        isRunning = false;
        Repaint();
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

