using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AstralUI.Editor
{
    /// <summary>Project-local build and QA automation. Only responds to an explicit command file.</summary>
    [InitializeOnLoad]
    public static class AstralEditorBridge
    {
        const string Command="Temp/astral-command.txt";
        static double nextPoll;
        static int captureCountdown;
        static string captureName;
        static AstralEditorBridge(){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}
        static void OnLog(string condition,string stack,LogType type)
        {if(type==LogType.Error||type==LogType.Exception)File.AppendAllText("Temp/astral-errors.log",condition+"\n"+stack+"\n");}
        static void Tick()
        {
            if(captureCountdown>0){EditorApplication.QueuePlayerLoopUpdate();captureCountdown--;if(captureCountdown==0)Capture(captureName);}
            if(EditorApplication.timeSinceStartup<nextPoll||EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            nextPoll=EditorApplication.timeSinceStartup+.3;
            if(!File.Exists(Command))return;
            var command=File.ReadAllText(Command).Trim();File.Delete(Command);
            try
            {
                if(command=="build")AstralUIBuilder.Build();
                else if(command=="play"){SetGameSize(1600,800);EditorApplication.isPlaying=true;}
                else if(command=="stop")EditorApplication.isPlaying=false;
                else if(command=="refresh")AssetDatabase.Refresh();
                else if(command=="frame-anim")AstralFrameAnimationTool.Build();
                else if(command.StartsWith("page:")){AstralApp.Instance.Navigate(int.Parse(command.Substring(5)));}
                else if(command.StartsWith("click:")){AstralApp.Instance.Node(command.Substring(6)).GetComponent<Button>().onClick.Invoke();}
                else if(command.StartsWith("capture:")){captureName=command.Substring(8);captureCountdown=8;}
                else if(command=="test")AstralRuntimeQA.Run();
                else if(command=="restore-project-identity"){PlayerSettings.productName="AI直出二次元风格UI";PlayerSettings.companyName="DefaultCompany";AssetDatabase.SaveAssets();}
                else if(command=="build-player")AstralPlayerBuild.Build();
                else if(command=="combat-upgrade-test")AstralCombatQA.RunUpgradeRegression();
                else if(command=="restart-test")AstralRestartQA.Run();
                else if(command=="unlock-test")AstralUnlockQA.Run();
                else if(command=="cooldown-test")AstralCooldownQA.Run();
                else if(command=="feedback-test")AstralFeedbackQA.Run();
                else if(command=="combat-test")AstralCombatQA.Run();
                else if(command=="combat-start")AstralApp.Instance.GetComponent<AstralCombat>().Begin();
                else if(command.StartsWith("combat-time:"))AstralApp.Instance.GetComponent<AstralCombat>().QAClock(float.Parse(command.Substring(12)));
                else if(command=="combat-upgrade")AstralApp.Instance.GetComponent<AstralCombat>().QAExperience(1000);
                else if(command=="combat-leave")AstralApp.Instance.GetComponent<AstralCombat>().Leave();
                else if(command.StartsWith("size:")){var size=command.Substring(5).Split('x');SetGameSize(int.Parse(size[0]),int.Parse(size[1]));}
                else throw new InvalidOperationException("Unknown editor command: "+command);
                File.WriteAllText("Temp/astral-command-result.txt","OK "+command);
            }
            catch(Exception e){File.WriteAllText("Temp/astral-command-result.txt",e.ToString());Debug.LogException(e);}
        }
        static void SetGameSize(int width,int height)
        {
            var assembly=typeof(UnityEditor.Editor).Assembly;
            var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
            var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            var instance=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null,null);
            var groupType=sizesType.GetProperty("currentGroupType").GetValue(instance,null);
            var group=sizesType.GetMethod("GetGroup").Invoke(instance,new[]{groupType});
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");
            var enumType=assembly.GetType("UnityEditor.GameViewSizeType");
            var ctor=sizeType.GetConstructor(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new[]{enumType,typeof(int),typeof(int),typeof(string)},null);
            var size=ctor.Invoke(new object[]{Enum.Parse(enumType,"FixedResolution"),width,height,"Astral "+width+"x"+height});
            group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
            int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
            var gameType=assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(gameType);
            gameType.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,count-1,null);
            view.Focus();
        }
        static void Capture(string name)
        {
            Directory.CreateDirectory("Documentation/AstralUI/Screenshots");
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot("Documentation/AstralUI/Screenshots/"+name+".png");
            Debug.Log("[AstralUI] Screenshot requested: "+name);
        }
    }
}
