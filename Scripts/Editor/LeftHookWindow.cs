using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class LeftHookWindow : EditorWindow
{
    private bool isRunning;
    private string statusMessage = string.Empty;

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

    private void OnGUI()
    {
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
        }
    }
}

