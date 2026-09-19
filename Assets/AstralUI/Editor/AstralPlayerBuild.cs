using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
namespace AstralUI.Editor
{
    public static class AstralPlayerBuild
    {
        [MenuItem("Tools/Astral UI/Build macOS Game")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop Play mode before building player.");
            string previousProduct=PlayerSettings.productName,previousCompany=PlayerSettings.companyName;
            try{
            Directory.CreateDirectory("Builds");PlayerSettings.productName="异境牌师 · 星界幸存者";PlayerSettings.companyName="Astral Studio";PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=800;PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=false;PlayerSettings.resizableWindow=true;
            var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/AstralUI/Scenes/AstralLobby.scene"},locationPathName="Builds/AstralSurvivors.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.None});
            File.WriteAllText("Documentation/AstralUI/player-build.txt",result.summary.result+"\nErrors: "+result.summary.totalErrors+"\nWarnings: "+result.summary.totalWarnings+"\nBytes: "+result.summary.totalSize+"\nDuration: "+result.summary.totalTime);
            if(result.summary.result!=BuildResult.Succeeded)throw new System.Exception("Player build failed: "+result.summary.result);
            }finally{PlayerSettings.productName=previousProduct;PlayerSettings.companyName=previousCompany;AssetDatabase.SaveAssets();}
        }
    }
}
