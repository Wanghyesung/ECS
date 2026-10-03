using System.IO;
using UnityEditor;

public static class BuildAutomator
{
    [MenuItem("Build/Windows")]
    public static void Build()
    {
        string strBuildPath = "Build/Windows/ORBITAL.exe";
        Directory.CreateDirectory(Path.GetDirectoryName(strBuildPath));

        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/00_Scene/Loby/LobbyScene.unity" },
            locationPathName = strBuildPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };

        BuildPipeline.BuildPlayer(buildPlayerOptions);
    }
}
