using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class LeftHookEditorTests
{
    [Test, Category("LefthookCore")]
    public void ContainsLefthookYml()
    {
        // check if project has lefthook.yml
        Assert.IsTrue(System.IO.File.Exists(
            System.IO.Path.Combine(Application.dataPath, "../lefthook.yml")
        ));
    }
    [Test, Category("LefthookCore")]
    public void Recompile()
    {
        // recompile unity code in editor script, check successful
        AssetDatabase.Refresh();
        Assert.IsTrue(!EditorApplication.isCompiling);
    }
    
    [Test, Category("LefthookFail")]
    public void AlwaysFailTest()
    {
        Assert.IsTrue(false);
    }
}