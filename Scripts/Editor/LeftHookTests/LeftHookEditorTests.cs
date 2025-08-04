using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Net;
using System.Reflection;

public class LeftHookEditorTests
{
    [Test, Category("LefthookPreferences")]
    public void PortDefaultsTo8080()
    {
        var original = GitHookPreferences.Port;
        try
        {
            EditorPrefs.DeleteKey("UnityGitHooks_Port");
            Assert.AreEqual(8080, GitHookPreferences.Port);
        }
        finally
        {
            GitHookPreferences.Port = original;
        }
    }

    [Test, Category("LefthookPreferences")]
    public void PortCanBeUpdated()
    {
        var original = GitHookPreferences.Port;
        try
        {
            GitHookPreferences.Port = 9090;
            Assert.AreEqual(9090, GitHookPreferences.Port);
        }
        finally
        {
            GitHookPreferences.Port = original;
        }
    }

    [Test, Category("LefthookCore")]
    public void EnqueueProcessesActions()
    {
        var executed = false;
        UnityLefthook.Enqueue(() => executed = true);

        var update = typeof(UnityLefthook).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Static);
        update.Invoke(null, null);

        Assert.IsTrue(executed);
    }

    [Test, Category("LefthookCore")]
    public void EnqueuedActionsExecuteInOrder()
    {
        var order = new List<int>();
        UnityLefthook.Enqueue(() => order.Add(1));
        UnityLefthook.Enqueue(() => order.Add(2));
        UnityLefthook.Enqueue(() => order.Add(3));

        var update = typeof(UnityLefthook).GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Static);
        update.Invoke(null, null);

        CollectionAssert.AreEqual(new[] {1, 2, 3}, order);
    }

    [Test, Category("LefthookCore")]
    public void HttpListenerUsesConfiguredPort()
    {
        var originalPort = GitHookPreferences.Port;
        try
        {
            GitHookPreferences.Port = 12345;
            var listener = new GitHookHttpListener();
            listener.Start();

            var listenerField = typeof(GitHookHttpListener).GetField("listener", BindingFlags.NonPublic | BindingFlags.Static);
            var httpListener = (HttpListener)listenerField.GetValue(null);

            Assert.IsTrue(httpListener.Prefixes.Contains("http://localhost:12345/"));

            listener.OnApplicationQuit();
        }
        finally
        {
            GitHookPreferences.Port = originalPort;
        }
    }

    [Test, Category("LefthookPreferences")]
    public void SettingsProviderIsConfigured()
    {
        var provider = GitHookPreferences.CreateSettingsProvider();
        Assert.AreEqual("Preferences/Unity Git Hooks", provider.settingsPath);
        Assert.AreEqual(SettingsScope.User, provider.scope);
    }
}
