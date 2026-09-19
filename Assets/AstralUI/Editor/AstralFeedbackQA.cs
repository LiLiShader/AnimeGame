using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace AstralUI.Editor
{
    public static class AstralFeedbackQA
    {
        static readonly List<string> rows=new List<string>();
        static AstralApp app;static AstralCombat run;
        static BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(string name,bool ok){rows.Add((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);}
        static object Field(string name)=>typeof(AstralCombat).GetField(name,flags).GetValue(run);
        static void Call(string name,params object[] args)=>typeof(AstralCombat).GetMethod(name,flags).Invoke(run,args);
        static void Click(string name)=>app.Node(name).GetComponent<Button>().onClick.Invoke();
        public static void Run(){app=AstralApp.Instance;run=app.GetComponent<AstralCombat>();app.StartCoroutine(Suite());}
        static void Pointer(string name)
        {
            Canvas.ForceUpdateCanvases();var rt=(RectTransform)app.Node(name);
            var ev=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center))};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(ev,hits);
            Check("pointer hits "+name,hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==rt.GetComponent<Button>());
        }
        static IEnumerator Suite()
        {
            string backup=JsonUtility.ToJson(app.State);var random=UnityEngine.Random.state;rows.Clear();
            try
            {
            bool ok=true;
            try{Core();}catch(Exception e){rows.Add(e.ToString());Debug.LogException(e);ok=false;}
            if(ok)
            {
                yield return null;yield return new WaitForEndOfFrame();Pointer("RunSkillFrame0");
                run.Ranks[0]=3;run.ShowSkillDetails(0);yield return new WaitForEndOfFrame();Pointer("SkillDetailClose");ScreenCapture.CaptureScreenshot("Documentation/AstralUI/Screenshots/16-skill-detail.png");yield return null;
                run.CloseSkillDetails();run.Ranks[0]=5;run.ShowSkillDetails(0);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Documentation/AstralUI/Screenshots/17-skill-max.png");yield return null;
                run.CloseSkillDetails();
                for(int i=0;i<25;i++)Call("Drop",new Vector2(-6+i%8*1.6f,-2+i/8*1.7f),3,0);
                run.QAExperience(5);run.Simulate(0);Call("UpdateXPFeedback",.15f);
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Documentation/AstralUI/Screenshots/18-xp-feedback.png");yield return null;
                Call("QueueExperience",1f);Call("UpdateXPFeedback",.22f);
                yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Documentation/AstralUI/Screenshots/21-xp-flight.png");yield return null;
                run.Leave();app.State.progress[0]=4;app.SelectChapter(1);app.ShowImmediate(3);
                yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Documentation/AstralUI/Screenshots/19-stage-locked.png");yield return null;
                app.SelectChapter(0);yield return new WaitForSecondsRealtime(.35f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Documentation/AstralUI/Screenshots/20-stage-available.png");yield return null;
            }
            }
            finally
            {
            if(run.Running)run.Leave();JsonUtility.FromJsonOverwrite(backup,app.State);app.Save();app.CloseModal();app.ShowImmediate(0);UnityEngine.Random.state=random;
            File.WriteAllLines("Documentation/AstralUI/feedback-qa.txt",rows);Debug.Log("Feedback QA completed; save restored.");
            }
        }
        static void Core()
        {
            if(run.Running)run.Leave();app.State.energy=9999;Array.Clear(app.State.progress,0,15);app.State.character=0;app.State.chapter=0;app.State.difficulty=0;app.State.stage=0;app.State.reducedMotion=false;
            app.RefreshAll();
            Check("fresh progression opens only first normal stage",app.CanEnterStage(0,0,0)&&!app.CanEnterStage(0,0,1)&&!app.CanEnterStage(1,0,0)&&!app.CanEnterStage(0,1,0));
            app.SelectChapter(1);
            Check("locked chapter has disabled challenge and stages",!app.Node("Challenge").GetComponent<Button>().interactable&&!app.Node("Stage0").GetComponent<Button>().interactable&&app.Node("StageStatus0").GetComponent<Text>().text=="LOCKED");
            int energy=app.State.energy;run.Begin();Check("direct combat entry cannot bypass chapter lock",!run.Running&&app.State.energy==energy);
            app.SelectChapter(0);Check("switching back clears stale lock toast",app.Node("Toast").GetComponent<CanvasGroup>().alpha==0);
            Check("locked difficulty visibly explains requirement",!app.Node("Difficulty1").GetComponent<Button>().interactable&&app.Node("DifficultyEn1").GetComponent<Text>().text.Contains("解锁"));
            app.State.difficulty=1;run.Begin();Check("direct entry cannot bypass difficulty lock",!run.Running&&app.State.energy==energy);
            app.State.difficulty=0;app.State.stage=4;run.Begin();Check("direct entry cannot bypass stage lock",!run.Running&&app.State.energy==energy);
            app.State.progress[0]=4;app.State.stage=4;app.RefreshAll();
            Check("four clears unlock fifth stage but not next chapter",app.CanEnterStage(0,0,4)&&!app.ChapterUnlocked(1));
            run.Begin();run.QAFinish(true);run.Leave();
            Check("fifth victory unlocks next chapter and hard mode",app.State.progress[0]==5&&app.ChapterUnlocked(1)&&app.DifficultyUnlocked(0,1)&&!app.DifficultyUnlocked(0,2));
            app.State.progress[1]=5;Check("hard completion unlocks nightmare",app.DifficultyUnlocked(0,2));
            app.SelectChapter(0);Check("all cleared icons are gold",app.Node("Stage3").Find("event").GetComponent<Image>().color==new Color(.96f,.74f,.34f));
            Array.Clear(app.State.progress,0,15);app.State.chapter=0;app.State.stage=0;app.State.difficulty=0;run.Begin();
            Click("RunSkillFrame0");Check("unlocked icon opens paused detail",run.SkillDetailsOpen&&run.Paused);
            float time=run.Elapsed;var pos=run.PlayerPosition;run.Simulate(.05f);run.Move(Vector2.right,.05f);run.Dash();run.Ultimate();
            Check("detail freezes simulation and movement",run.Elapsed==time&&run.PlayerPosition==pos);
            Check("current and next panels show highlighted numbers",app.Node("SkillDetailCard1").gameObject.activeSelf&&app.Node("SkillDetailBody0").GetComponent<Text>().text.Contains("<color=#7CE6FF>")&&app.Node("SkillDetailRank1").GetComponent<Text>().text.Contains("Lv. 2"));
            Click("SkillDetailClose");Check("close resumes previously running combat",!run.SkillDetailsOpen&&!run.Paused);
            run.ShowSkillDetails(1);Check("locked skill cannot open detail",!run.SkillDetailsOpen&&!app.Node("RunSkillFrame1").GetComponent<Button>().interactable);
            run.Command(0);run.ShowSkillDetails(0);run.CloseSkillDetails();Check("closing detail preserves previous pause",run.Paused);run.Command(0);
            run.Ranks[0]=5;run.ShowSkillDetails(0);Check("max rank shows single centered panel",!app.Node("SkillDetailCard1").gameObject.activeSelf&&((RectTransform)app.Node("SkillDetailCard0")).anchoredPosition.x==535);run.CloseSkillDetails();
            // The former periodic vacuum is exercised across several 30-second boundaries.
            Call("Drop",new Vector2(12,5),3,0);for(int i=0;i<90;i++)Call("SpawnWaves",1f);
            Call("UpdateLoot",.05f);Check("distant XP stays uncollected after 90 seconds",(float)Field("experience")==0&&run.Level==1);
            var pool=(IList)Field("loot");var item=pool[0];Check("periodic wave never attracts distant XP",!(bool)item.GetType().GetField("attracting").GetValue(item));
            Call("Drop",Vector2.zero,3,0);Call("UpdateLoot",.05f);Check("pickup starts flight before awarding XP",(float)Field("experience")==0);
            run.ShowSkillDetails(0);Call("UpdateXPFeedback",1f);Check("detail also freezes in-flight XP",(float)Field("experience")==0);run.CloseSkillDetails();
            Call("UpdateXPFeedback",.5f);Check("UI arrival awards pickup exactly once",(float)Field("experience")==3);Call("UpdateXPFeedback",1f);Check("completed flight cannot award twice",(float)Field("experience")==3);
            Call("CollectAllXP");Check("special vacuum still attracts remote crystals",(bool)item.GetType().GetField("attracting").GetValue(item));
            for(int i=0;i<80;i++)Call("UpdateLoot",.05f);Call("UpdateXPFeedback",.5f);Check("special pickup awards correct XP",(float)Field("experience")==6);
            run.Leave();run.Begin();Check("new run clears XP flights and detail",!run.SkillDetailsOpen&&(float)Field("experience")==0);
            // Bounded effect pool merges overflow without losing or duplicating rewards.
            for(int i=0;i<50;i++)Call("QueueExperience",.1f);Call("UpdateXPFeedback",1f);
            Check("pickup burst preserves XP with bounded pool",Mathf.Abs((float)Field("experience")-5)<.001f&&((IList)Field("xpFlights")).Count<=36);
            run.Leave();run.Begin();
        }
    }
}
