using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

public class MyBuildPostprocessor
{
    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget buildTarget, string path)
    {
        Debug.Print("Doing the post build fix that is required to get iOS builds actually building in xcode, remove all this if/when they fix it! - Seth");
        if (buildTarget != BuildTarget.iOS)
            return;

        #if UNITY_IOS
        
        var projectPath = path + "/Unity-iPhone.xcodeproj/project.pbxproj";
        //UNITY 2022.2.X FIX https://forum.unity.com/threads/xcode-build-error-after-upgrading-to-2022-2-0.1371966/
        var projRaw = File.ReadAllText(projectPath);
        projRaw = projRaw.Replace("chmod +x \\\"$IL2CPP\\\"",
            "chmod -R +x *\\nchmod +x \\\"$IL2CPP\\\"");
        projRaw = Regex.Replace(projRaw, "--data-folder=\\\\\"([^\"]*)\\\\\"",
            "--data-folder=\\\"$PROJECT_DIR/Data/Managed\\\"");
        File.WriteAllText(projectPath, projRaw);

        //ENABLE BITCODE FALSE FIX

        var pbxProject = new PBXProject();
        pbxProject.ReadFromFile(projectPath);
        foreach (var targetGuid in new[] { pbxProject.GetUnityMainTargetGuid(), pbxProject.GetUnityFrameworkTargetGuid() })
        {
            pbxProject.SetBuildProperty(targetGuid, "ENABLE_BITCODE", "NO");
        }
        pbxProject.WriteToFile(projectPath);

        #endif
    }
}
