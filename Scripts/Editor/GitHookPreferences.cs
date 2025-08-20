using UnityEditor;

public static class GitHookPreferences
{
    private const string PortKey = "UnityGitHooks_Port";

    public static int Port
    {
        get => EditorPrefs.GetInt(PortKey, 8080);
        set => EditorPrefs.SetInt(PortKey, value);
    }

    [SettingsProvider]
    public static SettingsProvider CreateSettingsProvider()
    {
        return new SettingsProvider("Preferences/Unity Git Hooks", SettingsScope.User)
        {
            guiHandler = searchContext =>
            {
                Port = EditorGUILayout.IntField("Port", Port);
            }
        };
    }
}
