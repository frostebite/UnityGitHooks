using UnityEditor;
using UnityEngine;
using System.IO;

public class GitHooksApiConfigWindow : EditorWindow
{
    private const string ConfigFileName = "gitHooksApiConfig.json";
    private string apiUrl = string.Empty;
    private string authToken = string.Empty;

    [MenuItem("Window/GitHooks/API Config")]
    public static void ShowWindow()
    {
        GetWindow<GitHooksApiConfigWindow>("Git Hooks API");
    }

    private void OnEnable()
    {
        var path = GetConfigPath();
        if (File.Exists(path))
        {
            try
            {
                var json = File.ReadAllText(path);
                var cfg = JsonUtility.FromJson<Config>(json);
                apiUrl = cfg.url;
                authToken = cfg.token;
            }
            catch { }
        }
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Git Hooks API Settings", EditorStyles.boldLabel);
        apiUrl = EditorGUILayout.TextField("API URL", apiUrl);
        authToken = EditorGUILayout.TextField("Auth Token", authToken);

        if (GUILayout.Button("Save"))
        {
            var cfg = new Config { url = apiUrl, token = authToken };
            var json = JsonUtility.ToJson(cfg, true);
            File.WriteAllText(GetConfigPath(), json);
            AssetDatabase.Refresh();
        }
    }

    private static string GetConfigPath()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.Combine(projectRoot, ConfigFileName);
    }

    [System.Serializable]
    private class Config
    {
        public string url;
        public string token;
    }
}
