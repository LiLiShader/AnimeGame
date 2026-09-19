using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
namespace AstralUI.Editor
{
    public static class AstralUnlockQA
    {
        public static void Run()
        {
            var app=AstralApp.Instance;var run=app.GetComponent<AstralCombat>();
            string backup=JsonUtility.ToJson(app.State);var random=UnityEngine.Random.state;var rows=new List<string>();
            void Check(string name,bool ok){rows.Add((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);}
            try
            {
                if(run.Running)run.Leave();Array.Clear(app.State.progress,0,15);app.State.progress[0]=4;
                app.State.chapter=0;app.State.stage=4;app.State.difficulty=0;app.State.energy=9999;
                app.RefreshAll();Check("four stages do not count as full chapter",!app.ChapterUnlocked(1)&&!app.DifficultyUnlocked(0,1));
                app.State.progress[0]=3;app.State.stage=3;
                run.Begin();run.QAClock(300);run.Simulate(0);run.QAKillEnemies();
                Check("fourth victory explains remaining stage",app.State.progress[0]==4&&app.Node("ResultBody").GetComponent<Text>().text.Contains("4 / 5")&&app.Node("CombatRetryLabel").GetComponent<Text>().text=="挑战下一关");
                run.Command(5);Check("next stage action advances instead of replaying",run.Running&&!run.Finished&&app.State.stage==4);
                run.QAClock(300);run.Simulate(0);run.QAKillEnemies();
                Check("actual final boss death records fifth clear",run.Finished&&app.State.progress[0]==5);
                Check("final result names unlocked difficulty and map",app.Node("ResultBody").GetComponent<Text>().text.Contains("已解锁：困难难度 / "+app.catalog.chapters[1].name));
                Check("victory unlocks hard and next map immediately",app.DifficultyUnlocked(0,1)&&app.ChapterUnlocked(1));
                run.Leave();Check("hard button refreshed after leaving",app.Node("Difficulty1").GetComponent<Button>().interactable);
                app.SelectDifficulty(1);Check("hard selection and entry are allowed",app.State.difficulty==1&&app.CanEnterStage(0,1,0));
                app.SelectChapter(1);Check("next chapter selection and entry are allowed",app.CanEnterStage(1,0,0)&&app.Node("Challenge").GetComponent<Button>().interactable);
                app.Save();typeof(AstralApp).GetMethod("Load",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app,null);app.RefreshAll();
                Check("unlocks survive actual PlayerPrefs reload",app.State.progress[0]==5&&app.DifficultyUnlocked(0,1)&&app.CanEnterStage(1,0,0));
                app.SelectChapter(0);app.SelectStage(4);run.Begin();run.QAClock(300);run.Simulate(0);run.QAKillEnemies();run.Leave();
                Check("return selects next unfinished stage",app.State.stage==4);
                Check("replaying final stage preserves unlocks",app.State.progress[0]==5&&app.ChapterUnlocked(1));
            }
            catch(Exception e){rows.Add(e.ToString());Debug.LogException(e);}
            finally
            {
                if(run.Running)run.Leave();JsonUtility.FromJsonOverwrite(backup,app.State);app.Save();app.CloseModal();app.ShowImmediate(0);UnityEngine.Random.state=random;
                File.WriteAllLines("Documentation/AstralUI/unlock-qa.txt",rows);
            }
        }
    }
}
