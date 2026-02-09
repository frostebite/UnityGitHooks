using UnityEditor;
using UnityEngine;
#if HAS_BACKGROUND_PROJECT
using UnityBackgroundProject;
#endif

/// <summary>
/// Git hook preferences for UnityLefthook.
/// Background project settings delegate to UnityBackgroundProject module when available.
/// </summary>
public static class GitHookPreferences
{
    private const string PortKey = "UnityGitHooks_Port";

    /// <summary>
    /// HTTP listener port for Active Workspace Mode.
    /// </summary>
    public static int Port
    {
        get => EditorPrefs.GetInt(PortKey, 8080);
        set => EditorPrefs.SetInt(PortKey, value);
    }

#if HAS_BACKGROUND_PROJECT
    /// <summary>
    /// Whether background project mode is enabled.
    /// Delegates to BackgroundProjectSettings.
    /// </summary>
    public static bool BackgroundProjectEnabled
    {
        get => BackgroundProjectSettings.Enabled;
        set => BackgroundProjectSettings.Enabled = value;
    }

    /// <summary>
    /// Suffix for background project folder name.
    /// Delegates to BackgroundProjectSettings.
    /// </summary>
    public static string BackgroundProjectSuffix
    {
        get => BackgroundProjectSettings.Suffix;
        set => BackgroundProjectSettings.Suffix = value;
    }
#endif

    [SettingsProvider]
    public static SettingsProvider CreateSettingsProvider()
    {
        return new SettingsProvider("Preferences/Unity Git Hooks", SettingsScope.User)
        {
            label = "Git Hooks",
            guiHandler = searchContext =>
            {
                EditorGUILayout.Space(10);

                // Active Workspace Mode settings (Lefthook-specific)
                EditorGUILayout.LabelField("Active Workspace Mode (Experimental)", EditorStyles.boldLabel);
                Port = EditorGUILayout.IntField("HTTP Listener Port", Port);
                EditorGUILayout.HelpBox(
                    "Active Workspace Mode runs tests in the current Unity editor via HTTP. " +
                    "This is experimental and may cause editor instability.",
                    MessageType.Info);

#if HAS_BACKGROUND_PROJECT
                EditorGUILayout.Space(15);

                // Background Project Mode - delegate to BackgroundProjectSettings UI
                EditorGUILayout.LabelField("Background Project Mode (Recommended)", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(
                    "Background Project settings have moved to a dedicated module.\n" +
                    "Use Edit > Preferences > Unity Background Project for full settings.",
                    MessageType.Info);

                EditorGUILayout.Space(5);

                // Show quick toggle and link
                BackgroundProjectEnabled = EditorGUILayout.Toggle("Enable Background Project", BackgroundProjectEnabled);

                if (BackgroundProjectEnabled)
                {
                    EditorGUI.indentLevel++;
                    EditorGUILayout.LabelField("Suffix:", BackgroundProjectSuffix);
                    EditorGUILayout.LabelField("Path:", BackgroundProjectSettings.GetBackgroundProjectPath() ?? "(unknown)");
                    EditorGUI.indentLevel--;
                }

                EditorGUILayout.Space(5);

                if (GUILayout.Button("Open Background Project Settings"))
                {
                    SettingsService.OpenUserPreferences("Preferences/Unity Background Project");
                }
#endif
            },
            keywords = new[] { "git", "hooks", "lefthook", "background", "project", "port" }
        };
    }
}
