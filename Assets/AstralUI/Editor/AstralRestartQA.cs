using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace AstralUI.Editor
{
    public static class AstralRestartQA
    {
        static readonly BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        static AstralApp app;static AstralCombat run;static List<string> rows;
        static object Field(string name)=>typeof(AstralCombat).GetField(name,flags).GetValue(run);
        static void Call(string name,params object[] args)=>typeof(AstralCombat).GetMethod(name,flags).Invoke(run,args);
        static void Check(string name,bool ok){rows.Add((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);}
        public static void Run(){app=AstralApp.Instance;run=app.GetComponent<AstralCombat>();app.StartCoroutine(Suite());}
        static IEnumerator Suite()
        {
            string backup=JsonUtility.ToJson(app.State);var random=UnityEngine.Random.state;rows=new List<string>();
            try
            {
                bool ok=true;try{Core();}catch(Exception e){ok=false;rows.Add(e.ToString());Debug.LogException(e);}
                if(ok){yield return null;yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Documentation/AstralUI/Screenshots/23-clean-restart.png");yield return null;}
            }
            finally
            {
                if(run.Running)run.Leave();JsonUtility.FromJsonOverwrite(backup,app.State);app.Save();app.CloseModal();app.ShowImmediate(0);UnityEngine.Random.state=random;
                File.WriteAllLines("Documentation/AstralUI/restart-qa.txt",rows);
            }
        }
        static void Core()
        {
            if(run.Running)run.Leave();app.State.energy=9999;app.State.character=0;app.State.chapter=0;app.State.difficulty=0;app.State.stage=0;
            for(int mode=0;mode<5;mode++)
            {
                run.Begin();run.Ranks[4]=run.Ranks[5]=5;run.QAClock(150);
                var enemy=typeof(AstralCombat).GetMethod("SpawnEnemy",flags).Invoke(run,new object[]{0});enemy.GetType().GetField("spawn").SetValue(enemy,0f);
                Call("UpdateAttacks",0f);Call("Drop",new Vector2(9,2),30,0);Call("QueueExperience",3f);Call("Floating",Vector2.zero,"OLD",Color.white,24);
                var oldWorld=(Transform)Field("world");var oldFlights=app.Node("XPFlightLayer").Cast<Transform>().Where(t=>t.gameObject.activeSelf).ToArray();
                Check("test seeds active black hole and orbit "+mode,((IList)Field("zones")).Count>0&&oldWorld.GetComponentsInChildren<SpriteRenderer>().Any(r=>r.name.StartsWith("Orbiting Blade")));
                if(mode==0||mode==1)
                {
                    // Reproduce managed pools disappearing while native Unity objects remain alive.
                    foreach(var name in new[]{"enemies","shots","loot","effects","zones","orbit","numbers","xpFlights"})((IList)Field(name)).Clear();
                    if(mode==1)typeof(AstralCombat).GetField("world",flags).SetValue(run,null);
                    run.Leave();run.Begin();
                }
                if(mode==2){run.Command(0);run.Command(3);run.Command(3);run.Command(4);run.Begin();}
                if(mode==3){run.QADamage(999999);run.Command(5);}
                if(mode==4){run.QAFinish(true);run.Command(5);}
                Check("old world disabled immediately "+mode,!oldWorld.gameObject.activeSelf);
                Check("old XP flights disabled "+mode,oldFlights.All(t=>!t.gameObject.activeSelf));
                Check("one active world after restart "+mode,run.gameObject.scene.GetRootGameObjects().Count(g=>g.name=="Astral Combat World (pooled)"&&g.activeSelf)==1);
                Check("fresh level health time and XP "+mode,run.Level==1&&run.Elapsed==0&&run.Kills==0&&run.HP==run.MaxHP&&(float)Field("experience")==0&&run.PendingUpgrades==0);
                Check("only character starter skill retained "+mode,run.Ranks[0]==1&&run.Ranks.Sum()==1);
                Check("combat pools have no previous actors or zones "+mode,new[]{"enemies","shots","loot","effects","zones","xpFlights"}.All(n=>((IList)Field(n)).Count==0));
                Call("UpdateAttacks",0f);
                Check("unlearned orbit and black hole stay inactive "+mode,!((Transform)Field("world")).GetComponentsInChildren<SpriteRenderer>().Any(r=>r.name.StartsWith("Orbiting Blade")||r.name=="Telegraphed Area"));
                Check("damage number pool rebuilt "+mode,((IList)Field("numbers")).Count==48&&app.Node("DamageNumber0").GetComponent<Text>().text=="");
                if(mode<4)run.Leave();
            }
        }
    }
}
