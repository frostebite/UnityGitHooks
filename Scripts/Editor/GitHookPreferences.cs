using UnityEditor;

public static class GitHookPreferences
{
    private const string PortKey = "UnityGitHooks_Port";
    private const string BackgroundProjectEnabledKey = "UnityGitHooks_BackgroundProjectEnabled";
    private const string BackgroundProjectSuffixKey = "UnityGitHooks_BackgroundProjectSuffix";

    public static int Port
    {
        get => EditorPrefs.GetInt(PortKey, 8080);
        set => EditorPrefs.SetInt(PortKey, value);
    }

    public static bool BackgroundProjectEnabled
    {
        get => EditorPrefs.GetBool(BackgroundProjectEnabledKey, false);
        set => EditorPrefs.SetBool(BackgroundProjectEnabledKey, value);
    }

    public static string BackgroundProjectSuffix
    {
        get => EditorPrefs.GetString(BackgroundProjectSuffixKey, "-BackgroundWorker");
        set => EditorPrefs.SetString(BackgroundProjectSuffixKey, value);
    }

    [SettingsProvider]
    public static SettingsProvider CreateSettingsProvider()
    {
        return new SettingsProvider("Preferences/Unity Git Hooks", SettingsScope.User)
        {
            guiHandler = searchContext =>
            {
                Port = EditorGUILayout.IntField("Port", Port);
                
                EditorGUILayout.Space(10);
                EditorGUILayout.LabelField("Background Project", EditorStyles.boldLabel);
                
                BackgroundProjectEnabled = EditorGUILayout.Toggle("Enable Background Project", BackgroundProjectEnabled);
                
                EditorGUI.BeginDisabledGroup(!BackgroundProjectEnabled);
                BackgroundProjectSuffix = EditorGUILayout.TextField("Project Suffix", BackgroundProjectSuffix);
                
                if (BackgroundProjectEnabled)
                {
                    EditorGUILayout.HelpBox("Background project mode requires rclone to be installed. When enabled, the entire repository folder will be synced to a background project before running jobs.", MessageType.Info);
                }
                EditorGUI.EndDisabledGroup();
            }
        };
    }
}
