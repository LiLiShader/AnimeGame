using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

namespace AstralUI
{
    public partial class AstralCombat
    {
        public bool SkillDetailsOpen { get; private set; }
        bool detailWasPaused;
        Sprite xpCrystal;Texture2D xpTexture;
        AudioClip xpPickupClip,xpArrivalClip;
        float shownXP,xpFlash,pickupSoundAt,arrivalSoundAt;
        int shownLevel=1;
        class XPFlight { public Image image;public Image[] trail;public Vector2 start;public float age,amount;public bool live; }
        readonly List<XPFlight> xpFlights=new List<XPFlight>();
        static readonly Color Amber=new Color(1,.77f,.25f), Ice=new Color(.48f,.9f,1);

        // Additive UI migration keeps existing scene and prefab layouts intact.
        public static void EnsureFeedbackUI(AstralApp owner)
        {
            var all=owner.GetComponentsInChildren<Transform>(true);
            var hud=all.FirstOrDefault(t=>t.name=="CombatHUD");
            if(hud==null)return;
            Transform Find(string name)=>all.First(t=>t.name==name);
            RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
            {
                var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);
                rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(x,-y);rt.sizeDelta=new Vector2(w,h);return rt;
            }
            Image Picture(Transform parent,string name,float x,float y,float w,float h,Color color,string art="")
            {
                var im=Rect(parent,name,x,y,w,h).gameObject.AddComponent<Image>();im.color=color;im.raycastTarget=false;
                if(art!=""){im.sprite=owner.catalog.Sprite(art);im.type=Image.Type.Sliced;}return im;
            }
            Text Label(Transform parent,string name,string text,float x,float y,float w,float h,int size,Color color,TextAnchor align=TextAnchor.MiddleLeft)
            {
                var t=Rect(parent,name,x,y,w,h).gameObject.AddComponent<Text>();t.font=owner.catalog.font;t.text=text;t.fontSize=size;t.color=color;t.alignment=align;t.raycastTarget=false;t.supportRichText=true;return t;
            }
            for(int i=0;i<12;i++)
            {
                var frame=Find("RunSkillFrame"+i);frame.GetComponent<Image>().raycastTarget=true;
                var button=frame.GetComponent<Button>()??frame.gameObject.AddComponent<Button>();button.targetGraphic=frame.GetComponent<Image>();button.navigation=new Navigation{mode=Navigation.Mode.None};
                var action=frame.GetComponent<AstralAction>()??frame.gameObject.AddComponent<AstralAction>();action.action="combat";action.argument=30+i;
                if(i<6&&i!=4&&Find("RunSkillIcon"+i).Find("RunSkillCooldown"+i)==null)
                {
                    var icon=Find("RunSkillIcon"+i);
                    var mask=Picture(icon,"RunSkillCooldown"+i,0,0,46,46,new Color(.015f,.025f,.07f,.78f));
                    var seconds=Label(icon,"RunSkillCooldownText"+i,"",0,0,46,46,18,Color.white,TextAnchor.MiddleCenter);
                    var outline=seconds.gameObject.AddComponent<Outline>();outline.effectColor=new Color(0,0,0,.95f);outline.effectDistance=new Vector2(1,-1);
                    mask.gameObject.SetActive(false);seconds.gameObject.SetActive(false);
                }
            }
            if(hud.Find("CombatSkillDetails")!=null)return;
            var track=Find("CombatXPTrack").GetComponent<Image>();track.color=new Color(.025f,.035f,.09f,.98f);track.rectTransform.anchoredPosition=new Vector2(22,-115);track.rectTransform.sizeDelta=new Vector2(1556,18);
            var fill=Find("CombatXP").GetComponent<Image>();fill.color=Amber;fill.rectTransform.sizeDelta=new Vector2(1,12);fill.rectTransform.anchoredPosition=new Vector2(22,-118);
            Picture(hud,"XPTopEdge",22,114,1556,1,new Color(1,.82f,.44f,.8f));
            Picture(hud,"XPBottomEdge",22,133,1556,1,new Color(1,.82f,.44f,.55f));
            Picture(hud,"XPFrontGlow",22,112,8,23,new Color(1,.95f,.72f));
            Label(hud,"XPProgressLabel","星尘 EXP  0 / 10",28,135,390,25,16,Amber);
            Rect(hud,"XPFlightLayer",0,0,1600,800);
            foreach(string node in new[]{"XPTopEdge","XPBottomEdge","XPFrontGlow","XPProgressLabel","XPFlightLayer"})hud.Find(node).SetSiblingIndex(Find("CombatUpgrade").GetSiblingIndex());
            var detail=Rect(hud,"CombatSkillDetails",0,0,1600,800);
            Picture(detail,"SkillDetailDim",0,0,1600,800,new Color(.02f,.025f,.075f,.94f)).raycastTarget=true;
            Label(detail,"SkillDetailKicker","R E S O N A N C E  /  共 鸣 档 案",300,90,1000,30,19,Ice,TextAnchor.MiddleCenter);
            Picture(detail,"SkillDetailIcon",768,132,64,64,Color.white);
            Label(detail,"SkillDetailTitle","",350,201,900,66,38,Color.white,TextAnchor.MiddleCenter);
            for(int i=0;i<2;i++)
            {
                var card=Rect(detail,"SkillDetailCard"+i,260+i*550,282,530,330);
                Picture(card,"SkillDetailPlate"+i,0,0,530,330,new Color(.16f,.21f,.38f),"panel_dark");
                Picture(card,"SkillDetailAccent"+i,22,0,486,3,i==0?Ice:Amber);
                Label(card,"SkillDetailRank"+i,"",30,15,470,50,25,i==0?Ice:Amber);
                Label(card,"SkillDetailBody"+i,"",30,77,470,228,22,new Color(.86f,.9f,1),TextAnchor.UpperLeft);
            }
            Label(detail,"SkillDetailHint","查看期间战斗暂停 · 数值已计入当前共鸣加成",300,625,1000,28,18,new Color(.64f,.71f,.87f),TextAnchor.MiddleCenter);
            var close=Picture(detail,"SkillDetailClose",645,674,310,64,Color.white,"button_primary");close.raycastTarget=true;
            close.gameObject.AddComponent<Button>().targetGraphic=close;
            var closeAction=close.gameObject.AddComponent<AstralAction>();closeAction.action="combat";closeAction.argument=20;
            Label(close.transform,"SkillDetailCloseLabel","返回战斗  /  ESC",0,0,310,64,23,Color.white,TextAnchor.MiddleCenter);
            detail.gameObject.SetActive(false);
        }
        readonly Image[] cooldownMasks=new Image[6];
        readonly Text[] cooldownTexts=new Text[6];
        void UpdateSkillCooldownUI()
        {
            for(int id=0;id<6;id++)
            {
                if(id==4)continue; // Orbiting blades and the remaining passive skills have no cast cooldown.
                if(cooldownMasks[id]==null)
                {
                    cooldownMasks[id]=Img("RunSkillCooldown"+id);
                    cooldownTexts[id]=app.Node("RunSkillCooldownText"+id).GetComponent<Text>();
                }
                float remaining=Mathf.Max(0,skillTimers[id]);
                bool cooling=Ranks[id]>0&&remaining>0&&skillCooldowns[id]>0;
                var mask=cooldownMasks[id];var label=cooldownTexts[id];
                mask.gameObject.SetActive(cooling);label.gameObject.SetActive(cooling);
                if(!cooling)continue;
                // The denominator is the duration at cast time, including cooldown reduction.
                // Learning a passive mid-cycle must not jump the progress indicator.
                mask.rectTransform.sizeDelta=new Vector2(46,46*Mathf.Clamp01(remaining/skillCooldowns[id]));
                label.text=(Mathf.Ceil(remaining*10)/10).ToString("0.0");
            }
        }
        public void ShowSkillDetails(int id)
        {
            if(!Running||Finished||Choosing||PendingUpgrades>0||id<0||id>=12||Ranks[id]<=0)return;
            if(!SkillDetailsOpen)detailWasPaused=Paused;
            SkillDetailsOpen=true;Paused=true;SetActive("CombatNotice",false);
            Txt("SkillDetailTitle",SkillNames[id]);Img("SkillDetailIcon").sprite=Art("combat_skill_"+id);
            int rank=Ranks[id];bool max=rank>=5;
            SetActive("SkillDetailCard1",!max);
            ((RectTransform)app.Node("SkillDetailCard0")).anchoredPosition=new Vector2(max?535:260,-282);
            for(int i=0;i<(max?1:2);i++)
            {
                Txt("SkillDetailRank"+i,(i==0?"当前效果":"下一级效果")+"  /  Lv. "+(rank+i)+(max?"  MAX":""));
                string body=SkillDetailDescription(id,rank+i);
                Txt("SkillDetailBody"+i,Regex.Replace(body,@"\d+(?:\.\d+)?%?",m=>"<color="+(i==0?"#7CE6FF":"#FFD16B")+">"+m.Value+"</color>"));
            }
            Txt("SkillDetailCloseLabel",detailWasPaused?"返回暂停菜单  /  ESC":"返回战斗  /  ESC");
            SetActive("CombatSkillDetails",true);
        }
        public void CloseSkillDetails()
        {
            if(!SkillDetailsOpen)return;
            SkillDetailsOpen=false;SetActive("CombatSkillDetails",false);SetActive("CombatNotice",true);Paused=detailWasPaused;
        }
        public string SkillDetailDescription(int id,int rank)
        {
            float cd=1-Ranks[10]*.07f;
            string Sec(float value)=>(value*cd).ToString("0.00")+" 秒";
            switch(id)
            {
                case 0:return "每轮飞刃："+((rank+1)*(rank==5&&Ranks[11]>0?2:1))+" 枚\n单枚伤害："+(90+rank*12)+"% 攻击\n释放间隔："+Sec(2.5f)+"\n满阶 + 命定一击：飞刃数量翻倍";
                case 1:return "冰环半径："+(2.4f+rank*.35f).ToString("0.00")+" 米\n伤害："+(100+rank*20)+"% 攻击\n释放间隔："+Sec(5.5f)+"\n减速持续：3 秒"+(rank==5?" · 冻结：2 秒":"\n满阶追加冻结：2 秒");
                case 2:return "最多连锁："+((2+rank)*(rank==5&&Ranks[10]>0?2:1))+" 个目标\n伤害："+(120+rank*18)+"% 攻击\n释放间隔："+Sec(3.6f)+"\n满阶 + 时隙回响：连锁数量翻倍";
                case 3:return "每轮陨星："+(1+rank/2)+" 枚\n单枚伤害："+(180+rank*40)+"% 攻击\n爆炸半径："+(1.35f+rank*.12f).ToString("0.00")+" 米\n释放间隔："+Sec(4.6f);
                case 4:return "环绕飞刃："+(2+rank)+" 柄\n单次伤害："+(35+rank*14)+"% 攻击\n伤害间隔：0.26 秒\n环绕半径：1.9 米";
                case 5:return "持续时间："+(2+rank*.35f).ToString("0.00")+" 秒\n每跳伤害："+(35+rank*10)+"% 攻击\n伤害间隔：0.4 秒\n区域半径："+(1.8f+rank*.18f).ToString("0.00")+" 米\n释放间隔："+Sec(7);
                case 6:return "每秒恢复："+(rank*.7f).ToString("0.0")+"% 最大生命\n当前生命上限："+MaxHP.ToString("0")+"\n每秒回复："+(MaxHP*.007f*rank).ToString("0.0")+" 生命";
                case 7:return "最大生命提升："+(rank*15)+"%\n生命上限："+(baseHP*(1+rank*.15f)).ToString("0.0")+"\n学习时回复：15% 基础生命";
                case 8:return "移动速度提升："+(rank*9)+"%\n移动速度："+(3.35f*(1+rank*.09f)).ToString("0.00")+" 米 / 秒";
                case 9:return "拾取范围提升："+(rank*40)+"%\n拾取半径："+(2*(1+rank*.4f)).ToString("0.0")+" 米\n经验获取提升："+(rank*10)+"%\n靠近晶体后自动吸入";
                case 10:return "攻击与技能冷却缩减："+(rank*7)+"%\n终结技冷却："+(18*(1-rank*.07f)).ToString("0.00")+" 秒\n闪避冷却："+(3.5f*(1-rank*.07f)).ToString("0.00")+" 秒\n共鸣：满阶天穹雷链数量翻倍";
                default:return "暴击率加成："+(rank*5)+"%\n当前暴击率："+(Mathf.Min(.85f,baseCrit+rank*.05f)*100).ToString("0")+"%\n暴击伤害："+((baseCritDamage+rank*.12f)*100).ToString("0")+"%\n共鸣：满阶星陨飞刃数量翻倍";
            }
        }
        Sprite ExperienceCrystal()
        {
            if(xpCrystal!=null)return xpCrystal;
            const int size=48;xpTexture=new Texture2D(size,size,TextureFormat.RGBA32,false);xpTexture.filterMode=FilterMode.Bilinear;
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float dx=(x-23.5f)/17f,dy=(y-23.5f)/22f,d=Mathf.Abs(dx)+Mathf.Abs(dy);
                pixels[y*size+x]=d>1?Color.clear:d>.83f?new Color(.17f,.08f,.015f):d>.70f?new Color(1,.95f,.65f):dx<0?new Color(1,.92f,.40f):new Color(1,.56f,.065f);
            }
            xpTexture.SetPixels(pixels);xpTexture.Apply();xpCrystal=Sprite.Create(xpTexture,new Rect(0,0,size,size),new Vector2(.5f,.5f),48);return xpCrystal;
        }
        void ResetFeedback()
        {
            SkillDetailsOpen=false;detailWasPaused=false;SetActive("CombatNotice",true);if(app.Node("CombatSkillDetails")!=null)SetActive("CombatSkillDetails",false);
            // Clear children as well as the list: UI flights may also survive a script reload.
            var flightLayer=app.Node("XPFlightLayer");
            if(flightLayer!=null)foreach(Transform child in flightLayer){child.gameObject.SetActive(false);Destroy(child.gameObject);}
            xpFlights.Clear();
            shownXP=experience;shownLevel=Mathf.Max(1,Level);xpFlash=0;pickupSoundAt=arrivalSoundAt=-1;
            if(xpPickupClip==null){xpPickupClip=Tone("Crystal pickup",880,.08f);xpArrivalClip=Tone("XP arrival",1320,.11f);}
        }
        void QueueExperience(float amount)
        {
            var flight=xpFlights.Find(f=>!f.live);
            if(flight==null)
            {
                if(xpFlights.Count>=36){xpFlights.Find(f=>f.live).amount+=amount;return;}
                var go=new GameObject("XP Flight",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));go.transform.SetParent(app.Node("XPFlightLayer"),false);
                var im=go.GetComponent<Image>();im.sprite=ExperienceCrystal();im.raycastTarget=false;im.rectTransform.anchorMin=im.rectTransform.anchorMax=new Vector2(0,1);im.rectTransform.sizeDelta=new Vector2(25,32);
                flight=new XPFlight{image=im,trail=new Image[3]};
                for(int i=0;i<3;i++)
                {
                    var tail=Instantiate(im,im.transform.parent);tail.name="XP Trail";tail.color=new Color(1,.76f,.24f,.45f-i*.1f);tail.raycastTarget=false;tail.gameObject.SetActive(false);flight.trail[i]=tail;
                }
                im.transform.SetAsLastSibling();xpFlights.Add(flight);
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)app.Node("XPFlightLayer"),cam.WorldToScreenPoint(position+Vector2.up*.5f),null,out flight.start);
            flight.age=0;flight.amount=amount;flight.live=true;flight.image.rectTransform.anchoredPosition=flight.start;flight.image.gameObject.SetActive(true);
            if(Time.unscaledTime>=pickupSoundAt){Fx(position,4,1.2f,.22f,Amber);Play(xpPickupClip);pickupSoundAt=Time.unscaledTime+.06f;}
        }
        static float XPThreshold(int level)=>level==1?10:10+level*4;
        void UpdateXPFeedback(float dt)
        {
            if(!Running||Finished||Paused)return;
            if(!Choosing&&PendingUpgrades==0)
            {
                foreach(var flight in xpFlights)
                {
                    if(!flight.live)continue;
                    flight.age+=dt;float t=Mathf.Clamp01(flight.age/(app.State.reducedMotion?.08f:.48f));
                    float ratio=shownXP/XPThreshold(shownLevel);var target=new Vector2(22+1556*Mathf.Clamp01(ratio),-124);
                    var control=flight.start+new Vector2(100,150);
                    flight.image.rectTransform.anchoredPosition=(1-t)*(1-t)*flight.start+2*(1-t)*t*control+t*t*target;
                    flight.image.rectTransform.localScale=Vector3.one*Mathf.Lerp(1,.5f,t);
                    for(int i=0;i<flight.trail.Length;i++)
                    {
                        var tail=flight.trail[i];tail.gameObject.SetActive(!app.State.reducedMotion&&t<1);
                        float u=Mathf.Max(0,t-(i+1)*.035f);
                        tail.rectTransform.anchoredPosition=(1-u)*(1-u)*flight.start+2*(1-u)*u*control+u*u*target;
                        tail.rectTransform.localScale=Vector3.one*(.55f-i*.12f);
                    }
                    if(t<1)continue;
                    flight.live=false;flight.image.gameObject.SetActive(false);experience+=flight.amount;xpFlash=1;
                    if(Time.unscaledTime>=arrivalSoundAt){Play(xpArrivalClip);arrivalSoundAt=Time.unscaledTime+.07f;}
                    SettleExperience();if(PendingUpgrades>0){OpenUpgrade();break;}
                }
            }
            // Animate through each crossed threshold instead of lerping backwards on level-up.
            float targetXP=shownLevel<Level?XPThreshold(shownLevel):experience;
            shownXP=Mathf.MoveTowards(shownXP,targetXP,Mathf.Max(25,XPThreshold(shownLevel)*4)*dt);
            if(shownLevel<Level&&shownXP>=XPThreshold(shownLevel)){shownLevel++;shownXP=0;xpFlash=1;}
            xpFlash=Mathf.Max(0,xpFlash-dt*3);
            float width=1556*Mathf.Clamp01(shownXP/XPThreshold(shownLevel));
            Bar("CombatXP",1556,shownXP/XPThreshold(shownLevel));Img("CombatXP").color=Color.Lerp(Amber,new Color(1,1,.87f),xpFlash);
            var tip=(RectTransform)app.Node("XPFrontGlow");tip.anchoredPosition=new Vector2(20+width,-112);tip.localScale=new Vector3(1+xpFlash*1.5f,1+xpFlash*.25f,1);
            Txt("XPProgressLabel","星尘 EXP  "+experience.ToString("0.#")+" / "+nextXP.ToString("0")+"   ·   Lv. "+Level);
        }
    }
}
