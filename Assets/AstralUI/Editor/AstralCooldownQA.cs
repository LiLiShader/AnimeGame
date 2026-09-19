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
    public static class AstralCooldownQA
    {
        static readonly List<string> rows=new List<string>();
        static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static AstralApp app;static AstralCombat run;
        static void Check(string text,bool ok){rows.Add((ok?"PASS ":"FAIL ")+text);if(!ok)throw new Exception(text);}
        static object Call(string name,params object[] args)=>typeof(AstralCombat).GetMethod(name,flags).Invoke(run,args);
        static float[] Values(string name)=>(float[])typeof(AstralCombat).GetField(name,flags).GetValue(run);
        public static void Run(){app=AstralApp.Instance;run=app.GetComponent<AstralCombat>();app.StartCoroutine(Suite());}
        static IEnumerator Suite()
        {
            string backup=JsonUtility.ToJson(app.State);var random=UnityEngine.Random.state;rows.Clear();
            try
            {
                bool ok=true;try{Core();}catch(Exception e){ok=false;rows.Add(e.ToString());Debug.LogException(e);}
                if(ok)
                {
                    yield return null;yield return new WaitForEndOfFrame();
                    var rt=(RectTransform)app.Node("RunSkillFrame0");var hits=new List<RaycastResult>();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center))},hits);
                    Check("cooldown overlay preserves icon click target",hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==rt.GetComponent<Button>());
                    ScreenCapture.CaptureScreenshot("Documentation/AstralUI/Screenshots/22-skill-cooldowns.png");yield return null;
                }
            }
            finally
            {
                if(run.Running)run.Leave();JsonUtility.FromJsonOverwrite(backup,app.State);app.Save();app.CloseModal();app.ShowImmediate(0);UnityEngine.Random.state=random;
                File.WriteAllLines("Documentation/AstralUI/cooldown-qa.txt",rows);
            }
        }
        static void Core()
        {
            if(run.Running)run.Leave();app.State.energy=9999;app.State.character=0;app.State.chapter=0;app.State.stage=0;app.State.difficulty=0;run.Begin();
            var ids=new[]{0,1,2,3,5};float[] basis={2.5f,5.5f,3.6f,4.6f,0,7};
            Check("ready skill has no cooldown overlay",!app.Node("RunSkillCooldown0").gameObject.activeSelf);
            Check("orbit and passive skills have no cooldown overlay",app.Node("RunSkillCooldown4")==null&&app.Node("RunSkillCooldown10")==null);
            foreach(int id in ids)run.Ranks[id]=1;run.Ranks[10]=2;Call("RefreshRanks");
            var enemy=Call("SpawnEnemy",0);enemy.GetType().GetField("spawn").SetValue(enemy,0f);enemy.GetType().GetField("hp").SetValue(enemy,1000000f);
            Call("UpdateAttacks",0f);Call("UpdateSkillCooldownUI");var timers=Values("skillTimers");var durations=Values("skillCooldowns");
            foreach(int id in ids)Check("actual cast starts reduced cooldown "+id,Mathf.Abs(timers[id]-basis[id]*.86f)<.001f&&durations[id]==timers[id]&&app.Node("RunSkillCooldown"+id).gameObject.activeSelf);
            float duration=durations[0];Call("UpdateAttacks",duration*.5f);Call("UpdateSkillCooldownUI");
            Check("mask tracks half remaining cooldown",Mathf.Abs(((RectTransform)app.Node("RunSkillCooldown0")).sizeDelta.y-23)<.01f);
            Check("timer displays rounded-up tenths",app.Node("RunSkillCooldownText0").GetComponent<Text>().text=="1.1");
            run.Ranks[10]=5;Call("UpdateSkillCooldownUI");Check("mid-cycle reduction does not distort progress",durations[0]==duration&&Mathf.Abs(((RectTransform)app.Node("RunSkillCooldown0")).sizeDelta.y-23)<.01f);
            float remaining=timers[0];run.Command(0);run.Simulate(.05f);Call("UpdateSkillCooldownUI");Check("pause freezes cooldown",timers[0]==remaining);run.Command(0);
            run.ShowSkillDetails(0);run.Simulate(.05f);Check("skill details freeze cooldown",timers[0]==remaining);run.CloseSkillDetails();
            run.QAExperience(10);run.Simulate(0);run.Simulate(.05f);Check("upgrade choice freezes cooldown",run.Choosing&&timers[0]==remaining);run.Choose(0);
            run.Ranks[10]=5;timers[0]=0;Call("UpdateAttacks",0f);Check("next cast uses updated reduction",Mathf.Abs(durations[0]-2.5f*.65f)<.001f);
            enemy.GetType().GetField("live").SetValue(enemy,false);Call("UpdateAttacks",10f);Call("UpdateSkillCooldownUI");Check("ready without target stays clear",!app.Node("RunSkillCooldown0").gameObject.activeSelf&&!app.Node("RunSkillCooldownText0").gameObject.activeSelf);
            run.Ranks[1]=0;timers[1]=3;Call("UpdateSkillCooldownUI");Check("locked skill never shows cooldown",!app.Node("RunSkillCooldown1").gameObject.activeSelf);
            run.Leave();run.Begin();Check("new run resets cooldown state",Values("skillCooldowns")[0]==0&&!app.Node("RunSkillCooldown0").gameObject.activeSelf);
            foreach(int id in ids){run.Ranks[id]=1;durations[id]=basis[id];timers[id]=basis[id]*(.2f+id*.12f);}run.Ranks[4]=run.Ranks[10]=1;Call("RefreshRanks");Call("UpdateSkillCooldownUI");
        }
    }
}
