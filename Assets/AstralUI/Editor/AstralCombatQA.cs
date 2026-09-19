using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace AstralUI.Editor
{
    public static class AstralCombatQA
    {
        static readonly List<string> rows=new List<string>();
        static string backup;
        static AstralApp app;static AstralCombat run;
        static void Check(string title,bool condition){rows.Add((condition?"PASS ":"FAIL ")+title);if(!condition)throw new Exception(title);}
        public static void Run(){app=AstralApp.Instance;run=app.GetComponent<AstralCombat>();backup=JsonUtility.ToJson(app.State);rows.Clear();app.StartCoroutine(Suite());}
        public static void RunUpgradeRegression()
        {
            app=AstralApp.Instance;run=app.GetComponent<AstralCombat>();
            backup=JsonUtility.ToJson(app.State);rows.Clear();
            var randomState=UnityEngine.Random.state;
            try
            {
                if(run.Running)run.Leave();
                app.State.energy=9999;app.State.character=0;app.State.chapter=0;app.State.stage=0;app.State.difficulty=0;
                run.Begin();
                run.QAExperience(9);run.Simulate(0);
                Check("below threshold grants no level or choice",run.Level==1&&!run.Choosing&&run.PendingUpgrades==0);
                run.QAExperience(1);run.Simulate(0);
                Check("1 to 2 grants exactly one choice",run.Level==2&&run.Choosing&&run.PendingUpgrades==1);
                int total=run.Ranks.Sum();
                run.Choose(-1);run.Choose(3);
                Check("invalid choices do not consume rewards",run.PendingUpgrades==1&&run.Ranks.Sum()==total);
                run.Command(13);
                Check("reroll does not change level or reward count",run.Level==2&&run.PendingUpgrades==1);
                run.Choose(0);run.Choose(0);run.Simulate(0);
                Check("selection and duplicate click never grant levels or reopen",run.Level==2&&!run.Choosing&&run.PendingUpgrades==0&&run.Ranks.Sum()==total+1);
                run.Leave();run.Begin();
                // Reproduce the 30-second pickup burst through real pooled loot and collection code.
                var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var drop=typeof(AstralCombat).GetMethod("Drop",flags);
                for(int i=0;i<17;i++)drop.Invoke(run,new object[]{new Vector2(10,0),3,0});
                var collect=typeof(AstralCombat).GetMethod("CollectAllXP",flags);
                collect.Invoke(run,null);collect.Invoke(run,null);
                var updateLoot=typeof(AstralCombat).GetMethod("UpdateLoot",flags);
                var feedback=typeof(AstralCombat).GetMethod("UpdateXPFeedback",flags);
                for(int i=0;i<80;i++)updateLoot.Invoke(run,new object[]{.05f});
                // Same-frame batch delivery checks the progression queue independently of presentation.
                var flights=(System.Collections.IEnumerable)typeof(AstralCombat).GetField("xpFlights",flags).GetValue(run);
                float batch=0;foreach(var flight in flights){var type=flight.GetType();if((bool)type.GetField("live").GetValue(flight)){batch+=(float)type.GetField("amount").GetValue(flight);type.GetField("live").SetValue(flight,false);}}
                run.QAExperience(batch);run.Simulate(0);
                Check("51 collected XP settles directly to level 4 with three rewards",run.Level==4&&run.PendingUpgrades==3&&run.Choosing);
                Check("HUD shows actual level and remaining rewards",app.Node("CombatName").GetComponent<Text>().text.Contains("Lv. 4")&&app.Node("UpgradeTitle").GetComponent<Text>().text.Contains("待选 3 次"));
                total=run.Ranks.Sum();float elapsed=run.Elapsed;var position=run.PlayerPosition;
                for(int i=0;i<3;i++)
                {
                    run.Simulate(.05f);
                    Check("choice freezes combat "+i,run.Elapsed==elapsed);
                    run.Choose(0);run.Choose(0);
                    Check("one reward consumed per choice "+i,run.Level==4&&run.PendingUpgrades==2-i&&run.Ranks.Sum()==total+i+1);
                    if(i<2){run.Move(Vector2.right,.05f);run.Simulate(.05f);Check("queued rewards block movement and simulation "+i,run.Elapsed==elapsed&&run.PlayerPosition==position&&run.Choosing);}
                }
                run.Simulate(0);
                Check("three rewards exhausted without a fourth popup",!run.Choosing&&run.Level==4);
                // 51 - (10 + 18 + 22) = 1 XP left; level 4 requires another 25.
                run.QAExperience(24);run.Simulate(0);
                Check("overflow retained but below next threshold",run.Level==4&&!run.Choosing);
                run.QAExperience(1);run.Simulate(0);
                Check("4 to 5 grants exactly one choice",run.Level==5&&run.PendingUpgrades==1);
                run.Choose(0);run.Simulate(0);
                Check("level 5 choice does not grant level 6",run.Level==5&&!run.Choosing);
                run.QAExperience(30);run.Simulate(0);
                Check("5 to 6 requires its own XP and grants one choice",run.Level==6&&run.PendingUpgrades==1);
                run.QAFinish(false);
                Check("finish clears unclaimed rewards",run.Finished&&!run.Choosing&&run.PendingUpgrades==0);
                run.Leave();run.Begin();
                Check("new run resets level and reward queue",run.Level==1&&run.PendingUpgrades==0&&!run.Choosing);
            }
            catch(Exception e){rows.Add(e.ToString());Debug.LogException(e);}
            finally
            {
                if(run.Running)run.Leave();
                JsonUtility.FromJsonOverwrite(backup,app.State);app.Save();app.CloseModal();app.ShowImmediate(0);
                UnityEngine.Random.state=randomState;
                File.WriteAllLines("Documentation/AstralUI/upgrade-regression-qa.txt",rows);
            }
        }
        static IEnumerator Suite()
        {
            bool ok=true;try{Core();}catch(Exception e){ok=false;rows.Add(e.ToString());Debug.LogException(e);}
            if(ok)
            {
                // Six simulated minutes, including enemy attacks, loot, automatic weapons and all wave phases.
                // Health is replenished for this throughput test; win/loss damage is independently tested below.
                run.Begin();if(run.Paused)run.Command(0);run.Ranks[6]=5;
                yield return null;yield return new WaitForEndOfFrame();PointerChecks("battle");
                var pad=app.Node("CombatJoystick").GetComponent<AstralJoystick>();var rt=(RectTransform)pad.transform;
                var pointer=new PointerEventData(EventSystem.current){pointerId=42,position=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center+Vector2.right*55))};
                ExecuteEvents.Execute(pad.gameObject,pointer,ExecuteEvents.pointerDownHandler);
                Check("touch joystick receives drag direction",pad.Value.x>.8f);ExecuteEvents.Execute(pad.gameObject,pointer,ExecuteEvents.pointerUpHandler);Check("touch release resets movement",pad.Value==Vector2.zero);
                run.Command(0);yield return null;yield return new WaitForEndOfFrame();PointerChecks("pause");run.Command(0);
                run.QAExperience(10);run.Simulate(.05f);yield return null;yield return new WaitForEndOfFrame();PointerChecks("upgrade");run.Choose(0);
                float start=Time.realtimeSinceStartup;int ticks=0;
                while(ticks<7200&&!run.Finished)
                {
                    if(run.Choosing)run.Choose(0);if(run.Paused)run.Command(0);
                    typeof(AstralCombat).GetMethod("UpdateXPFeedback",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(run,new object[]{.05f});
                    float angle=ticks*.007f;var target=new Vector2(Mathf.Cos(angle)*8,Mathf.Sin(angle)*3.5f);run.Move((target-run.PlayerPosition).normalized,.05f);
                    run.Simulate(.05f);if(ticks%100==0){run.QACooldowns();run.Ultimate();run.QAHeal();}
                    ticks++;if(ticks==3200){yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();ScreenCapture.CaptureScreenshot("Documentation/AstralUI/Screenshots/14-combat-skills.png");yield return null;}if(ticks%240==0)yield return null;
                }
                rows.Add("STRESS ticks="+ticks+" simulated="+run.Elapsed.ToString("0.0")+"s kills="+run.Kills+" level="+run.Level+" liveEnemies="+run.ActiveEnemies+" wall="+(Time.realtimeSinceStartup-start).ToString("0.00")+"s");
                try{Check("sustained waves and attacks produce kills",run.Kills>50);Check("experience collected produces multiple upgrades",run.Level>3);Check("enemy pool stays bounded",run.ActiveEnemies<=106);}catch(Exception e){rows.Add(e.Message);}
                if(!run.Finished)run.QAFinish(false);yield return new WaitForSecondsRealtime(.5f);yield return new WaitForEndOfFrame();PointerChecks("result");Check("terminal result excludes level-up state",run.Finished&&!run.Choosing&&!run.Paused&&!app.Node("CombatUpgrade").gameObject.activeSelf);ScreenCapture.CaptureScreenshot("Documentation/AstralUI/Screenshots/15-combat-result.png");yield return null;yield return new WaitForEndOfFrame();run.Leave();
            }
            JsonUtility.FromJsonOverwrite(backup,app.State);app.Save();app.CloseModal();app.ShowImmediate(0);File.WriteAllLines("Documentation/AstralUI/combat-qa.txt",rows);Debug.Log("[AstralCombat] QA completed. User save restored.");
        }
        static void PointerChecks(string state)
        {
            Canvas.ForceUpdateCanvases();foreach(var button in app.Node("CombatHUD").GetComponentsInChildren<Button>(false))
            {
                if(!button.interactable)continue;
                if(state=="pause"&&button.name!="CombatResume"&&button.name!="CombatRetreat")continue;
                if(state=="result"&&button.name!="CombatReturn"&&button.name!="CombatRetry")continue;
                if(state=="upgrade"&&!button.name.StartsWith("UpgradeCard")&&button.name!="CombatReroll")continue;
                var r=(RectTransform)button.transform;var data=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center))};var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);var top=hits.Count>0?hits[0].gameObject.GetComponentInParent<Button>():null;
                Check("pointer "+state+" "+button.name,top==button);
            }
        }
        static void Core()
        {
            if(run.Running)run.Leave();app.State.energy=9999;app.State.chapter=0;app.State.stage=3;app.State.difficulty=0;app.State.progress[0]=3;app.State.character=0;app.State.equipped[0]=0;app.State.levels[0]=80;
            int energy=app.State.energy;run.Begin();Check("energy paid exactly once at entry",app.State.energy==energy-10);run.Begin();Check("duplicate entry ignored",app.State.energy==energy-10);
            Check("equipped level and character talent enter attack formula",Mathf.Abs(run.Attack-(1280+695+20*8)/35f*1.12f)<.01f);
            Check("equipment critical stats transfer",Mathf.Abs(run.CriticalChance-.27f)<.001f&&Mathf.Abs(run.CriticalMultiplier-2.16f)<.001f);
            Check("entry does not unlock stage",app.State.progress[0]==3);float t=run.Elapsed;run.Command(0);run.Simulate(.05f);Check("pause freezes simulation",run.Elapsed==t);run.Command(0);
            var pos=run.PlayerPosition;run.Move(Vector2.right,.5f);Check("movement changes player position",run.PlayerPosition.x>pos.x+1);run.QACooldowns();run.Dash();float x=run.PlayerPosition.x;run.Move(Vector2.zero,.05f);Check("dash moves in last direction",run.PlayerPosition.x>x+.5f);
            run.QAExperience(100);run.Simulate(.05f);Check("XP opens three choice screen",run.Choosing&&run.Choices.Length==3);Check("three choices are distinct",run.Choices.Distinct().Count()==3);int choice=run.Choices[0],rank=run.Ranks[choice];t=run.Elapsed;run.Simulate(.05f);Check("selection freezes battle",t==run.Elapsed);run.Choose(0);Check("selected upgrade applied once",run.Ranks[choice]==rank+1&&!run.Choosing);run.Choose(0);Check("double click cannot duplicate upgrade",run.Ranks[choice]==rank+1);
            for(int i=0;i<12;i++)run.Ranks[i]=5;run.QAExperience(1000);run.Simulate(.05f);Check("capped builds use three different supplies",run.Choosing&&run.Choices.Distinct().Count()==3&&run.Choices.All(i=>i>=12));run.Choose(0);
            run.QADamage(999999);Check("lethal damage enters defeat screen",run.Finished&&app.Node("CombatResult").gameObject.activeSelf);Check("defeat preserves stage lock",app.State.progress[0]==3);int coins=app.State.coins;run.QAFinish(false);Check("defeat reward cannot duplicate",coins==app.State.coins);run.Leave();
            run.Begin();run.QAClock(300);run.Simulate(.05f);Check("boss spawns at five minutes",app.Node("CombatBoss").gameObject.activeSelf);run.QAKillEnemies();Check("boss death produces victory and unlock",run.Finished&&app.State.progress[0]==4);int crystals=app.State.crystals;coins=app.State.coins;run.QAFinish(true);Check("victory reward cannot duplicate",crystals==app.State.crystals&&coins==app.State.coins);run.Leave();
            for(int c=0;c<8;c++){app.State.character=c;run.Begin();run.QACooldowns();run.Ultimate();Check("character "+c+" distinct starter and usable ultimate",run.Ranks.Count(r=>r==1)==1&&run.HP>0);run.QAFinish(false);run.Leave();}
            // No equipment has a measurably smaller attack than upgraded equipment.
            app.State.character=0;app.State.equipped[0]=-1;run.Begin();float naked=run.Attack;run.QAFinish(false);run.Leave();app.State.equipped[0]=0;run.Begin();Check("equipment changes actual combat attack",run.Attack>naked*1.5f);run.QAFinish(false);run.Leave();
        }
    }
}
