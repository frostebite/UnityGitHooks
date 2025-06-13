using UnityEditor;
using UnityEngine;

public class LeftHookWindow : EditorWindow {
    
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
    // show ongui
    private void OnGUI()
    {
        if (GUILayout.Button("Install Lefthook"))
        {
            // run lefthook install command
            System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo();
            start.FileName = "npm";
            start.Arguments = "install lefthook --save-dev";
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.CreateNoWindow = true;
            System.Diagnostics.Process process = System.Diagnostics.Process.Start(start);
            process.WaitForExit();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            Debug.Log(output);
            Debug.Log(error);
            Close();
        }
        
        // lefthook install button
        // run lefthook install command
        if (GUILayout.Button("Install Lefthook to this repo"))
        {
            System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo();
            start.FileName = "lefthook";
            start.Arguments = "install";
            start.UseShellExecute = false;
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;
            start.CreateNoWindow = true;
            System.Diagnostics.Process process = System.Diagnostics.Process.Start(start);
            process.WaitForExit();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            Debug.Log(output);
            Debug.Log(error);
        }
        
        if (GUILayout.Button("Close"))
        {
            Close();
        }
    }
}