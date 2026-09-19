using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace AstralUI.Editor
{
    public static class AstralRuntimeQA
    {
        static readonly List<string> runtimeErrors=new List<string>();
        static void CaptureError(string condition,string stack,LogType type){if(type==LogType.Exception||type==LogType.Error)runtimeErrors.Add(condition);}
        public static void Run()
        {
            var app=AstralApp.Instance;
            if(app==null)throw new Exception("Enter Play mode first.");
            runtimeErrors.Clear();Application.logMessageReceived+=CaptureError;RunCore();app.StartCoroutine(PointerChecks(app));
        }
        static System.Collections.IEnumerator PointerChecks(AstralApp app)
        {
            var rows=new List<string>();
            for(int page=0;page<4;page++)
            {
                app.ShowImmediate(page);
                yield return null;
                yield return new WaitForEndOfFrame();
                foreach(var button in app.GetComponentsInChildren<Button>(false))
                {
                    if(!button.interactable)continue;
                    var rt=(RectTransform)button.transform;
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,rt.TransformPoint(rt.rect.center))};
                    var mask=button.GetComponentInParent<RectMask2D>();if(mask!=null&&!RectTransformUtility.RectangleContainsScreenPoint(mask.rectTransform,pointer.position,null))continue;
                    var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
                    var top=hits.Count>0?hits[0].gameObject.GetComponentInParent<Button>():null;
                    rows.Add((top==button?"PASS ":"FAIL ")+"pointer hit page "+page+" "+button.name+(top==button?"":" (top="+(top!=null?top.name:"none")+")"));
                }
            }
            app.ShowImmediate(0);
            yield return new WaitForSecondsRealtime(.5f);
            Application.logMessageReceived-=CaptureError;
            rows.Add(runtimeErrors.Count==0?"PASS no runtime exceptions":"FAIL runtime exceptions: "+string.Join("; ",runtimeErrors));
            File.AppendAllLines("Documentation/AstralUI/runtime-qa.txt",rows);
            File.AppendAllText("Documentation/AstralUI/runtime-qa.txt","Pointer validation completed.\n");
        }
        static void RunCore()
        {
            var app=AstralApp.Instance;if(app==null)throw new Exception("Enter Play mode first.");
            var results=new List<string>();string backup=JsonUtility.ToJson(app.State);
            Action<string,bool> check=(name,ok)=>{results.Add((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);};
            Action<string> click=n=>app.Node(n).GetComponent<Button>().onClick.Invoke();
            try
            {
                check("4 populated page prefabs",app.pages.Length==4&&app.pages[0].transform.childCount>20);
                check("one active audio listener",UnityEngine.Object.FindObjectsOfType<AudioListener>().Length==1);
                check("all art references resolved",Array.TrueForAll(app.catalog.art,s=>s!=null));
                foreach(var graphic in app.GetComponentsInChildren<Graphic>(true))check("renderer "+graphic.name,graphic.GetComponent<CanvasRenderer>()!=null);
                app.ShowImmediate(2);click("CharacterSlot2");check("character selection updates artwork and name",app.State.character==2&&app.Node("CharName").GetComponent<Text>().text=="月白"&&app.Node("CharacterHero").GetComponent<Image>().sprite.name=="character_luna");
                click("CharacterTab1");check("skill tab switches content",app.Node("CharacterLore").gameObject.activeSelf&&!app.Node("CharacterStats").gameObject.activeSelf);
                app.ShowImmediate(1);click("EquipmentSlot4");check("equipment selection detail",app.State.equipment==4&&app.Node("EqName").GetComponent<Text>().text=="天穹之翼");
                if(app.State.equipped[2]==4)click("EquipToggle");click("EquipToggle");check("equipment ownership transfer",app.State.equipped[2]==4);
                int level=app.State.levels[4],gold=app.State.coins;click("EquipUpgrade");check("upgrade requires confirmation",app.State.levels[4]==level);click("ModalOption0");check("upgrade increments level and deducts gold",app.State.levels[4]==level+1&&app.State.coins<gold);
                bool locked=app.State.locked[4];click("EquipmentLock");check("equipment lock toggles",app.State.locked[4]!=locked);
                click("FilterRelic");check("inventory filter excludes weapons",!app.Node("EquipmentSlot0").gameObject.activeSelf&&app.Node("EquipmentSlot6").gameObject.activeSelf);click("FilterAll");
                app.ShowImmediate(3);app.SelectChapter(0);app.State.progress[0]=3;app.State.stage=3;app.RefreshAll();click("Stage4");check("locked stage cannot be selected",app.State.stage==3);
                app.State.energy=100;click("Challenge");check("challenge opens real combat confirmation",app.Node("ModalBody").GetComponent<Text>().text.Contains("5 分钟"));app.CloseModal();
                for(int i=0;i<8;i++){app.SelectCharacter(i);check("eight character selection "+i,app.State.character==i&&app.Node("CharacterHero").GetComponent<Image>().sprite.name=="character_"+app.catalog.characters[i].id);}
                app.State.energy=0;click("Challenge");check("insufficient stamina blocks challenge",!app.Node("Modal").gameObject.activeSelf);app.State.energy=100;
                app.State.mailClaimed=false;int coins=app.State.coins;app.Dispatch("mail");click("ModalOption0");int once=app.State.coins;app.Dispatch("mail");click("ModalOption0");check("mail rewards cannot be claimed twice",once==coins+10000&&app.State.coins==once);
                app.Save();check("save serialization",PlayerPrefs.GetString("AstralUI.Save.v1").Contains("progress"));
                int sliced=0;foreach(var image in app.GetComponentsInChildren<Image>(true))if(image.type==Image.Type.Sliced&&image.sprite!=null&&image.sprite.border!=Vector4.zero)sliced++;
                check("nine-slice sprites configured on reusable UI",sliced>50);
                int buttons=0;foreach(var b in app.GetComponentsInChildren<Button>(true)){check("button route "+b.name,b.GetComponent<AstralAction>()!=null);buttons++;}results.Add("Button count: "+buttons+"; sliced images: "+sliced);
            }
            catch(Exception e){results.Add("EXCEPTION "+e);Debug.LogException(e);}
            finally
            {
                JsonUtility.FromJsonOverwrite(backup,app.State);app.CloseModal();app.ShowImmediate(0);app.Save();
                Directory.CreateDirectory("Documentation/AstralUI");File.WriteAllLines("Documentation/AstralUI/runtime-qa.txt",results);
                Debug.Log("[AstralUI] Runtime QA complete: "+results.Count+" checks.");
            }
        }
    }
}
