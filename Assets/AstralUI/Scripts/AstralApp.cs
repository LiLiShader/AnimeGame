using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AstralUI
{
    public class AstralApp : MonoBehaviour
    {
        public static AstralApp Instance { get; private set; }
        public AstralCatalog catalog;
        public GameObject[] pages;
        public AstralSave State { get; private set; }
        public int CurrentPage { get; private set; }
        public bool IsTransitioning { get; private set; }
        const string SaveKey = "AstralUI.Save.v1";
        readonly Dictionary<string, Transform> nodes = new Dictionary<string, Transform>();
        Action[] modalActions = Array.Empty<Action>();
        int equipmentFilter, characterTab;
        Coroutine toastRoutine, selectionRoutine;
        AudioSource audioSource;
        AudioClip clickClip;
        static readonly Color Ink = new Color(.10f,.15f,.29f), Muted = new Color(.43f,.49f,.65f), Gold = new Color(.96f,.74f,.34f);
        bool initialized;
        void Awake(){Initialize();}
        void OnEnable()
        {
            if(!Application.isPlaying)return;
            if(!initialized||nodes.Count==0||State==null){initialized=false;Initialize();ShowImmediate(0);}
            Instance=this;
        }
        void OnDisable(){if(Application.isPlaying)Save();}
        void Initialize()
        {
            if(initialized)return;
            initialized=true;
            Instance=this;
            AstralCombat.EnsureFeedbackUI(this);
            nodes.Clear();
            Application.runInBackground=true;
            foreach(Transform child in GetComponentsInChildren<Transform>(true)) if(!nodes.ContainsKey(child.name))nodes.Add(child.name,child);
            Load();
            foreach(var action in GetComponentsInChildren<AstralAction>(true))
            {var captured=action;action.GetComponent<Button>().onClick.RemoveAllListeners();action.GetComponent<Button>().onClick.AddListener(()=>Dispatch(captured.action,captured.argument));}
            audioSource=GetComponent<AudioSource>();if(audioSource==null)audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;
            clickClip=AudioClip.Create("UI soft chime",1764,1,22050,false);var samples=new float[1764];
            for(int i=0;i<samples.Length;i++){float t=i/22050f;samples[i]=Mathf.Sin(2*Mathf.PI*1120*t)*Mathf.Exp(-t*70)*.07f;}
            clickClip.SetData(samples,0);
        }
        void Start(){ShowImmediate(0);RefreshAll();}
        void OnDestroy(){if(Instance==this)Instance=null;if(clickClip!=null)Destroy(clickClip);}
        void OnApplicationPause(bool paused){if(paused)Save();}
        void OnApplicationQuit(){Save();}
        void Update()
        {
            if(GetComponent<AstralCombat>()!=null&&GetComponent<AstralCombat>().Running)return;
            if(Input.GetKeyDown(KeyCode.Escape)) { if(Node("Modal").gameObject.activeSelf)CloseModal();else Navigate(0); }
        }
        public Transform Node(string name){return nodes.TryGetValue(name,out var n)?n:null;}
        Text Text(string name){return Node(name).GetComponent<Text>();}
        Image Image(string name){return Node(name).GetComponent<Image>();}
        void SetText(string name,string text){var n=Node(name);if(n!=null)n.GetComponent<Text>().text=text;}
        void Sprite(string name,string id){Image(name).sprite=catalog.Sprite(id);}
        void Active(string name,bool value){Node(name).gameObject.SetActive(value);}
        void Load()
        {
            try { State=JsonUtility.FromJson<AstralSave>(PlayerPrefs.GetString(SaveKey,"")); } catch { State=null; }
            if(State==null||State.levels==null||State.levels.Length!=12||State.owned==null||State.owned.Length!=12||State.locked==null||State.locked.Length!=12||State.progress==null||State.progress.Length!=15)State=new AstralSave();
            MigrateRoster();
            State.character=Mathf.Clamp(State.character,0,catalog.characters.Length-1);State.equipment=Mathf.Clamp(State.equipment,0,11);State.chapter=Mathf.Clamp(State.chapter,0,4);State.difficulty=Mathf.Clamp(State.difficulty,0,2);State.stage=Mathf.Clamp(State.stage,0,4);
            for(int i=0;i<12;i++)State.levels[i]=Mathf.Clamp(State.levels[i],1,100);
            for(int i=0;i<15;i++)State.progress[i]=Mathf.Clamp(State.progress[i],0,5);
            State.energy=Mathf.Max(0,State.energy);State.coins=Mathf.Max(0,State.coins);State.crystals=Mathf.Max(0,State.crystals);
            if(!State.owned[State.equipment])State.equipment=Array.FindIndex(State.owned,b=>b);
            if(State.equipment<0){State.owned[0]=true;State.equipment=0;}
        }
        void MigrateRoster()
        {
            // Expand older four-character saves without replacing progress, currencies or equipment.
            int count=catalog.characters.Length;
            int oldEquipmentCount=State.equipped==null?0:State.equipped.Length;
            int oldAffinityCount=State.affinity==null?0:State.affinity.Length;
            Array.Resize(ref State.equipped,count);
            Array.Resize(ref State.affinity,count);
            for(int i=oldEquipmentCount;i<count;i++)State.equipped[i]=-1;
            for(int i=oldAffinityCount;i<count;i++)State.affinity[i]=1;
            for(int i=0;i<count;i++)if(State.equipped[i]<-1||State.equipped[i]>=State.owned.Length||(State.equipped[i]>=0&&!State.owned[State.equipped[i]]))State.equipped[i]=-1;
        }
        void RevealCharacter()
        {
            var scroll=Node("CharacterRoster").GetComponent<ScrollRect>();
            float max=scroll.content.rect.height-scroll.viewport.rect.height;
            if(max<=0)return;
            float target=Mathf.Clamp(State.character*125-180,0,max);
            scroll.verticalNormalizedPosition=1-target/max;
        }
        public void Save(){if(State!=null){PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(State));PlayerPrefs.Save();}}
        public void Dispatch(string action,int arg=0)
        {
            if(IsTransitioning)return;
            if(!State.muted&&clickClip!=null)audioSource.PlayOneShot(clickClip);
            switch(action)
            {
                case "combat":GetComponent<AstralCombat>().Command(arg);break;
                case "navigate":Navigate(arg);break;
                case "character":SelectCharacter(arg);break;
                case "equipment":SelectEquipment(arg);break;
                case "characterTab":characterTab=arg;RefreshCharacter();AnimateSelection("CharacterLore");break;
                case "characterDetail":CharacterDetail();break;
                case "equipTab":EquipTab(arg);break;
                case "equip":ToggleEquip();break;
                case "upgrade":Upgrade();break;
                case "lock":State.locked[State.equipment]=!State.locked[State.equipment];RefreshEquipment();Save();Toast(State.locked[State.equipment]?"装备已锁定，不会被分解":"装备已解锁");break;
                case "filter":equipmentFilter=arg;RefreshEquipment();Toast(new[]{"显示全部装备","筛选：武器","筛选：圣遗物","筛选：装甲"}[arg]);break;
                case "chapter":SelectChapter(arg);break;
                case "stage":SelectStage(arg);break;
                case "difficulty":SelectDifficulty(arg);break;
                case "challenge":Challenge();break;
                case "affinity":Affinity();break;
                case "profile":Modal("星月的旅人","指挥官等级 40  ·  UID 10025678\n当前助理："+catalog.characters[State.character].name+"\n旅途中的每一次选择，都将成为新的星光。",new[]{"更换助理"},new Action[]{()=>Navigate(2)});break;
                case "quest":Quest();break;
                case "mail":Mail();break;
                case "event":Modal("深渊的来信","限时剧情 · 2026.09.16 — 2026.10.07\n月白在遗落的星典中发现了一封来信。\n前往艾尔废墟，寻找深渊的回响。",new[]{"前往探索","查看月白"},new Action[]{()=>{State.chapter=0;Navigate(3);},()=>{SelectCharacter(2);Navigate(2);}});break;
                case "gallery":Modal("星界图鉴","已邂逅角色 "+catalog.characters.Length+" / "+catalog.characters.Length+"\n已收集装备 "+State.owned.Count(b=>b)+" / 12\n已探索区域 "+UnlockedChapters()+" / 5",new[]{"角色档案","装备图鉴"},new Action[]{()=>Navigate(2),()=>Navigate(1)});break;
                case "achievement":Modal("旅途成就","初次邂逅 · 收集全体同行者  ✓\n星界武库 · 持有十二种共鸣武装\n开拓之路 · 已通关 "+State.progress.Sum()+" 个关卡\n继续挑战，书写属于你的异境篇章。",new[]{"继续旅途"},new Action[]{()=>Navigate(3)});break;
                case "settings":Settings();break;
                case "friends":Modal("同行者","月白  ·  在线 / 正在研读星典\n汐音  ·  在线 / 幽影森林探索中\n绯烬  ·  离线 / 最后出现于艾尔废墟\n（本地演示名册）",new[]{"查看角色"},new Action[]{()=>Navigate(2)});break;
                case "menu":Modal("快捷导航","前往角色档案、共鸣武库或星界旅程。",new[]{"主界面","角色","装备","关卡"},new Action[]{()=>Navigate(0),()=>Navigate(2),()=>Navigate(1),()=>Navigate(3)});break;
                case "currency":Currency(arg);break;
                case "close":CloseModal();break;
                case "modalOption":if(arg<modalActions.Length){var callback=modalActions[arg];CloseModal();callback?.Invoke();}break;
            }
        }
        public void ShowImmediate(int index)
        {
            CurrentPage=Mathf.Clamp(index,0,3);
            for(int i=0;i<pages.Length;i++){pages[i].SetActive(i==CurrentPage);var g=pages[i].GetComponent<CanvasGroup>();g.alpha=1;g.blocksRaycasts=true;g.interactable=true;((RectTransform)pages[i].transform).anchoredPosition=Vector2.zero;}
            Active("BackHome",CurrentPage!=0);
            // The home profile occupies the upper-left; the shared bar keeps only its currency side.
            var top=(RectTransform)Node("TopGlass");top.anchoredPosition=new Vector2(CurrentPage==0?795:0,0);top.sizeDelta=new Vector2(CurrentPage==0?805:1600,69);
            var tr=Node("Transition").GetComponent<CanvasGroup>();tr.alpha=0;tr.blocksRaycasts=false;
            RefreshAll();
        }
        public void Navigate(int index)
        {
            if(IsTransitioning||index<0||index>3)return;
            CloseModal();if(index==CurrentPage){RefreshAll();return;}
            StartCoroutine(Transition(index));
        }
        IEnumerator Transition(int next)
        {
            IsTransitioning=true;
            var overlay=Node("Transition").GetComponent<CanvasGroup>();overlay.blocksRaycasts=true;
            var old=pages[CurrentPage].GetComponent<CanvasGroup>();old.interactable=false;
            float duration=State.reducedMotion?.09f:.22f;
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime){float k=t/duration;old.alpha=1-k;((RectTransform)old.transform).anchoredPosition=new Vector2(-46*k,0);overlay.alpha=Mathf.Sin(k*Mathf.PI)*.30f;yield return null;}
            pages[CurrentPage].SetActive(false);CurrentPage=next;pages[next].SetActive(true);RefreshAll();if(next==2)RevealCharacter();
            Active("BackHome",next!=0);var top=(RectTransform)Node("TopGlass");top.anchoredPosition=new Vector2(next==0?795:0,0);top.sizeDelta=new Vector2(next==0?805:1600,69);
            var page=pages[next].GetComponent<CanvasGroup>();page.interactable=false;
            for(float t=0;t<duration;t+=Time.unscaledDeltaTime){float k=1-Mathf.Pow(1-t/duration,3);page.alpha=k;((RectTransform)page.transform).anchoredPosition=new Vector2((1-k)*75,0);overlay.alpha=(1-k)*.28f;yield return null;}
            page.alpha=1;page.interactable=true;((RectTransform)page.transform).anchoredPosition=Vector2.zero;overlay.alpha=0;overlay.blocksRaycasts=false;IsTransitioning=false;Save();
        }
        public void RefreshAll(){RefreshCurrencies();RefreshCharacter();RefreshEquipment();RefreshBattle();}
        void RefreshCurrencies(){SetText("CurrencyValue0",State.crystals+" +");SetText("CurrencyValue1",State.energy+" +");SetText("CurrencyValue2",(State.coins/1000f).ToString("0.0")+"K +");}
        public void SelectCharacter(int index)
        {
            if(index<0||index>=catalog.characters.Length)return;State.character=index;characterTab=0;RefreshCharacter();RefreshEquipment();RevealCharacter();AnimateSelection("CharacterHero");Save();
        }
        void RefreshCharacter()
        {
            var c=catalog.characters[State.character];Sprite("CharacterHero","character_"+c.id);Sprite("HomeHero","character_"+c.id);Sprite("HomeAvatar","portrait_"+c.id);
            SetText("CharName",c.name);SetText("CharEnglish",c.english+"   △ △ △ △");SetText("CharQuote","「"+c.quote+"」");SetText("AffinityValue",State.affinity[State.character].ToString());
            int e=State.equipped[State.character];int bonus=e>=0?EquipmentAttack(e):0;
            string[] vals={c.hp.ToString(),(c.attack+bonus).ToString(),c.defense.ToString(),(15+(e>=0?catalog.equipment[e].crit:0)).ToString("0.0")+"%",(180+(e>=0?catalog.equipment[e].critDamage:0)).ToString("0.0")+"%","32"};for(int i=0;i<6;i++)SetText("CharAttr"+i,vals[i]);
            for(int i=0;i<catalog.characters.Length;i++)Sprite("CharacterSlot"+i,i==State.character?"card_gold":"button_secondary");
            for(int i=0;i<4;i++){Sprite("CharacterTab"+i,i==characterTab?"panel_dark":"panel_light");Text("CharTabTitle"+i).color=i==characterTab?Color.white:Ink;Text("CharTabEnglish"+i).color=i==characterTab?Color.white:Muted;}
            Active("CharacterStats",characterTab==0);Active("CharacterLore",characterTab!=0);
            SetText("CharSectionTitle",new[]{"基础属性","战斗技能","命之痕","角色时装"}[characterTab]);
            SetText("CharLoreHeading",characterTab==1?c.skill:characterTab==2?c.talent:"星界行装 · 原初");
            SetText("CharLoreBody",characterTab==1?AstralCombat.CharacterSkillDescription(State.character):characterTab==2?"已解锁 1 / 6\n进入战斗时，获得 12% 攻击力提升。\n与同行者并肩作战，唤醒更多命运的回响。":"当前穿戴：原初行装\n以星界丝织与共鸣金属制成的战术礼装。\n已拥有 1 / 1 套时装。");
            SetText("CharacterDetailsLabel",characterTab==0?"属性详情   ›":characterTab==1?"技能详情   ›":characterTab==2?"命痕详情   ›":"时装详情   ›");
        }
        void CharacterDetail()
        {
            var c=catalog.characters[State.character];int e=State.equipped[State.character];
            Modal(c.name+" · "+new[]{"属性详情","技能档案","命之痕","时装档案"}[characterTab],characterTab==0?"定位："+c.element+"\n基础攻击："+c.attack+"  / 装备加成："+(e>=0?EquipmentAttack(e):0)+"\n当前装备："+(e>=0?catalog.equipment[e].name:"未装备")+"\n好感等级："+State.affinity[State.character]:Text("CharLoreBody").text,new[]{"查看装备","返回"},new Action[]{()=>Navigate(1),null});
        }
        void Affinity(){var c=catalog.characters[State.character];Modal(c.name+" · 羁绊",c.quote+"\n\n赠送星光礼物：消耗 1000 金币，好感度 +1。",new[]{"赠送礼物","取消"},new Action[]{()=>{if(State.coins<1000){Toast("金币不足");return;}State.coins-=1000;State.affinity[State.character]++;RefreshAll();Save();Toast("羁绊加深，好感度 +1");},null});}
        public void SelectEquipment(int index){if(index<0||index>=12||!State.owned[index])return;State.equipment=index;RefreshEquipment();AnimateSelection("EquipmentHero");Save();}
        int EquipmentAttack(int index){return catalog.equipment[index].attack+(State.levels[index]-60)*8;}
        void RefreshEquipment()
        {
            int index=State.equipment;var e=catalog.equipment[index];Sprite("EquipmentHero",e.id);SetText("EqName",e.name);SetText("EqStars",new string('★',e.rarity));SetText("EqLevel","Lv. "+State.levels[index]+" / 100");
            string[] values={EquipmentAttack(index).ToString(),e.crit.ToString("0.0")+"%",e.critDamage.ToString("0.0")+"%",(48+(State.levels[index]-60)/2).ToString()};for(int i=0;i<4;i++)SetText("EqAttribute"+i,values[i]);
            SetText("EqSkill",e.skill);SetText("EqDescription",e.description);SetText("EquipmentElement",e.type+" · 共鸣武装");SetText("EquipmentSerial","ASTRAL ARMORY   /   No. "+(index+1).ToString("000"));
            SetText("EquipToggleLabel",State.equipped[State.character]==index?"卸下":"装备");SetText("EquipUpgradeLabel",State.levels[index]>=100?"已满级":"强化");Node("EquipUpgrade").GetComponent<Button>().interactable=State.levels[index]<100;
            Image("EquipmentLock").color=State.locked[index]?new Color(1,.87f,.53f):Color.white;
            int visible=0;string filter=equipmentFilter==1?"武器":equipmentFilter==2?"圣遗物":"装甲";
            for(int i=0;i<12;i++)
            {
                bool show=State.owned[i]&&(equipmentFilter==0||catalog.equipment[i].type==filter);Active("EquipmentSlot"+i,show);if(!show)continue;
                ((RectTransform)Node("EquipmentSlot"+i)).anchoredPosition=new Vector2(1154+(visible%3)*139,-156-(visible/3)*147);visible++;
                Active("EquipmentSlotSelected"+i,i==index);Active("EquipmentSlotLock"+i,State.locked[i]);Active("EquipmentSlotEquipped"+i,Array.IndexOf(State.equipped,i)>=0);SetText("EquipmentSlotLevel"+i,"Lv. "+State.levels[i]);
                Sprite("EquipmentSlot"+i,i==index?"card_selected":catalog.equipment[i].rarity==5?"card_gold":"card_purple");
            }
            SetText("FilterAllLabel",new[]{"全部 ▾","武器 ▾","圣遗物 ▾","装甲 ▾"}[equipmentFilter]);SetText("InventoryCount","显示 "+visible+"  /  持有 "+State.owned.Count(b=>b)+" 件");
        }
        void ToggleEquip()
        {
            int e=State.equipment,c=State.character;
            if(State.equipped[c]==e){State.equipped[c]=-1;Toast("已卸下 "+catalog.equipment[e].name);}
            else {for(int i=0;i<State.equipped.Length;i++)if(State.equipped[i]==e)State.equipped[i]=-1;State.equipped[c]=e;Toast(catalog.characters[c].name+" 已装备 "+catalog.equipment[e].name);}
            RefreshAll();Save();
        }
        void Upgrade()
        {
            int id=State.equipment,level=State.levels[id],cost=800+level*20;
            if(level>=100){Toast("装备已达到最高等级");return;}
            Modal("装备强化",catalog.equipment[id].name+"   Lv. "+level+" → "+(level+1)+"\n攻击力  +8\n消耗金币："+cost+"  /  当前持有："+State.coins,new[]{"确认强化","取消"},new Action[]{()=>{if(State.coins<cost){Toast("金币不足，请完成任务或关卡");return;}if(State.levels[id]!=level)return;State.coins-=cost;State.levels[id]++;RefreshAll();Save();AnimateSelection("EquipmentHero");Toast("强化成功 · 攻击力 +8");},null});
        }
        void EquipTab(int i)
        {
            for(int k=0;k<5;k++){Sprite("EquipTab"+k,k==i?"button_secondary":"panel_dark");Text("EquipTabText"+k).color=k==i?Ink:Color.white;Text("EquipTabEn"+k).color=k==i?Muted:Color.white;}
            switch(i){case 0:RefreshEquipment();break;case 1:Upgrade();break;case 2:Modal("升星预览",catalog.equipment[State.equipment].name+"\n当前稀有度："+new string('★',catalog.equipment[State.equipment].rarity)+"\n共鸣核心：0 / 3\n挑战噩梦难度以寻找升星材料。",new[]{"前往关卡","返回"},new Action[]{()=>Navigate(3),null});break;
                case 3:Modal("星界共鸣套装","2 件套：元素伤害提升 12%\n4 件套：释放技能后攻击力提升 18%\n\n当前武装："+(State.equipped[State.character]>=0?catalog.equipment[State.equipped[State.character]].name:"未装备"),new[]{"查看角色"},new Action[]{()=>Navigate(2)});break;
                case 4:Recycle();break;}
        }
        void Recycle()
        {
            int id=State.equipment;if(State.locked[id]){Toast("此装备已锁定，请先点击详情右上角解锁");return;}if(Array.IndexOf(State.equipped,id)>=0){Toast("已穿戴的装备不能分解，请先卸下");return;}if(State.owned.Count(b=>b)<=1){Toast("至少保留一件装备");return;}
            Modal("分解装备",catalog.equipment[id].name+" 将从背包移除。\n可获得 3000 金币。\n这是本地演示存档操作，可在设置中重置。",new[]{"确认分解","取消"},new Action[]{()=>{State.owned[id]=false;State.coins+=3000;State.equipment=Array.FindIndex(State.owned,b=>b);RefreshAll();Save();Toast("分解完成 · 金币 +3000");},null});
        }
        int ProgressIndex {get{return State.chapter*3+State.difficulty;}}
        public bool ChapterUnlocked(int c)
        {
            if(c<0||c>=catalog.chapters.Length)return false;
            for(int i=0;i<c;i++)if(State.progress[i*3]<5)return false;
            return true;
        }
        public bool DifficultyUnlocked(int c,int d)
        {
            if(!ChapterUnlocked(c)||d<0||d>2)return false;
            for(int i=0;i<d;i++)if(State.progress[c*3+i]<5)return false;
            return true;
        }
        public bool CanEnterStage(int c,int d,int s)
        {return s>=0&&s<5&&DifficultyUnlocked(c,d)&&s<=State.progress[c*3+d];}
        void ClearToast()
        {if(toastRoutine!=null)StopCoroutine(toastRoutine);Node("Toast").GetComponent<CanvasGroup>().alpha=0;}

        int UnlockedChapters(){return Enumerable.Range(0,5).Count(ChapterUnlocked);}
        public void SelectChapter(int index)
        {
            if(index<0||index>4)return;ClearToast();State.chapter=index;State.difficulty=0;State.stage=Mathf.Min(4,State.progress[index*3]);RefreshBattle();AnimateSelection("BattleBackdrop");Save();
            if(!ChapterUnlocked(index))Toast("区域预览 · 通关上一章普通难度全部 5 关后解锁挑战");
        }
        public void SelectStage(int index)
        {
            if(!CanEnterStage(State.chapter,State.difficulty,index)){Toast("请先解锁当前章节、难度及前一关卡");return;}
            ClearToast();
            State.stage=index;RefreshBattle();Save();
        }
        public void SelectDifficulty(int value)
        {
            if(!DifficultyUnlocked(State.chapter,value)){Toast("通关上一章节普通难度，以及本章前一难度的全部关卡后解锁");return;}
            ClearToast();
            State.difficulty=value;State.stage=Mathf.Min(4,State.progress[ProgressIndex]);RefreshBattle();Save();
        }
        void RefreshBattle()
        {
            if(!DifficultyUnlocked(State.chapter,State.difficulty))State.difficulty=0;
            State.stage=Mathf.Clamp(State.stage,0,Mathf.Min(4,State.progress[ProgressIndex]));
            var chapter=catalog.chapters[State.chapter];Sprite("BattleBackdrop",chapter.background);SetText("ChapterHeading",(State.chapter+1).ToString("00")+"  "+chapter.name);SetText("ChapterDescription",chapter.description);
            int progress=State.progress[ProgressIndex];SetText("ProgressValue",progress+" / 5");((RectTransform)Node("ProgressFill")).sizeDelta=new Vector2(287*progress/5f,5);
            for(int i=0;i<5;i++)
            {
                bool selected=i==State.chapter;Sprite("Chapter"+i,selected?"tab":"panel_dark");Text("ChapterTitle"+i).color=selected?Ink:Color.white;Text("ChapterNumber"+i).color=selected?Muted:Color.white;Text("ChapterEn"+i).color=selected?Muted:new Color(.72f,.79f,.92f);
                bool chapterOpen=ChapterUnlocked(i);
                SetText("ChapterEn"+i,chapterOpen?catalog.chapters[i].english:"锁定 · 上章普通5关");
                Text("ChapterTitle"+i).color=selected?Ink:chapterOpen?Color.white:new Color(.62f,.66f,.76f);
                bool locked=!CanEnterStage(State.chapter,State.difficulty,i);
                Node("Stage"+i).GetComponent<Button>().interactable=!locked;
                Sprite("Stage"+i,i==State.stage?"card_selected":i<progress?"card_gold":"card_purple");Sprite("StageFrame"+i,i==State.stage?"card_selected":i<progress?"card_gold":"card_purple");Sprite("StageArt"+i,chapter.background);Image("StageArt"+i).color=locked?new Color(.24f,.28f,.38f):new Color(.68f,.72f,.86f);
                SetText("StageNumber"+i,(State.chapter+1)+"-"+(i+1));SetText("StageStatus"+i,locked?"LOCKED":i<progress?"CLEAR":"挑战");Text("StageStatus"+i).color=locked?new Color(.65f,.68f,.78f):i<progress?Gold:Color.white;
                var stageIcon=Node("Stage"+i).GetComponentsInChildren<Image>(true).FirstOrDefault(im=>im.name=="event"||im.name=="lock");if(stageIcon!=null){stageIcon.sprite=catalog.Sprite(locked?"icon_lock":"icon_event");stageIcon.color=locked?new Color(.74f,.78f,.88f):i<progress?Gold:Color.white;}
            }
            string[] stageNames={"寂静之门","旧都回廊","坠星广场","断裂的天桥","核心禁域"};SetText("SelectedStage",(State.chapter+1)+"-"+(State.stage+1)+"   "+stageNames[State.stage]+(!ChapterUnlocked(State.chapter)?"  / 区域未解锁":""));
            for(int i=0;i<3;i++)
            {
                bool unlocked=DifficultyUnlocked(State.chapter,i),selected=i==State.difficulty;
                Sprite("Difficulty"+i,selected&&unlocked?"button_primary":"button_secondary");
                Node("Difficulty"+i).GetComponent<Button>().interactable=unlocked;
                Text("DifficultyTitle"+i).color=!unlocked?Muted:selected?Color.white:Ink;
                Text("DifficultyEn"+i).color=unlocked&&selected?Color.white:Muted;
                SetText("DifficultyEn"+i,unlocked?new[]{"NORMAL","HARD","NIGHTMARE"}[i]:!ChapterUnlocked(State.chapter)?"章节尚未解锁":i==1?"普通通关 "+State.progress[State.chapter*3]+"/5 后解锁":"困难通关 "+State.progress[State.chapter*3+1]+"/5 后解锁");
            }
            bool canEnter=CanEnterStage(State.chapter,State.difficulty,State.stage);
            Node("Challenge").GetComponent<Button>().interactable=canEnter;
            SetText("ChallengeLabel",canEnter?"开始挑战":"区域未解锁");
            SetText("EnergyCost","× "+(10+State.difficulty*5));
        }
        void Challenge()
        {
            if(!CanEnterStage(State.chapter,State.difficulty,State.stage)){Toast("当前关卡尚未解锁");return;}
            int cost=10+State.difficulty*5; if(State.energy<cost){Toast("能量不足，可在顶栏补给");return;}
            var c=catalog.characters[State.character];
            Modal("出击确认",catalog.chapters[State.chapter].name+"  "+(State.chapter+1)+"-"+(State.stage+1)+"\n出战："+c.name+"  /  能量："+cost+"\n生存 5 分钟，击败深渊领主。装备与技能随行。",new[]{"进入战场","取消"},new Action[]{()=>{if(State.energy<cost)return;GetComponent<AstralCombat>().Begin();},null});
        }
        void Quest(){Modal("每日任务",State.questClaimed?"今日探索补给已领取。\n继续完成关卡可获得更多金币与星晶。":"登入异境  1 / 1  ✓\n邂逅同行者  8 / 8  ✓\n可领取：星晶 120 / 金币 5000",new[]{State.questClaimed?"前往挑战":"领取奖励"},new Action[]{()=>{if(State.questClaimed){Navigate(3);return;}State.questClaimed=true;State.crystals+=120;State.coins+=5000;RefreshCurrencies();Save();Toast("任务奖励已领取");}});}
        void Mail(){Modal("星界邮件",State.mailClaimed?"收件箱已清空。\n愿你的旅途始终有星光相伴。":"致新任指挥官：\n欢迎来到异境。请收下这份探索补给。\n附件：能量 ×60 / 金币 ×10000",new[]{State.mailClaimed?"关闭":"领取附件"},new Action[]{()=>{if(State.mailClaimed)return;State.mailClaimed=true;State.energy+=60;State.coins+=10000;RefreshCurrencies();Save();Toast("附件已领取 · 能量 +60");}});}
        void Currency(int type)
        {
            if(type==1)Modal("能量补给","当前能量："+State.energy+"\n消耗 50 星晶兑换 60 能量。\n仅使用本地演示货币。",new[]{"兑换补给","取消"},new Action[]{()=>{if(State.crystals<50){Toast("星晶不足");return;}State.crystals-=50;State.energy+=60;RefreshCurrencies();Save();Toast("能量 +60");},null});
            else Modal(type==0?"星晶":"金币",type==0?"当前星晶："+State.crystals+"\n通过每日任务与首次通关获得。":"当前金币："+State.coins+"\n用于强化装备与赠送角色礼物。\n通过任务、邮件与关卡获得。",new[]{"每日任务","前往探索"},new Action[]{Quest,()=>Navigate(3)});
        }
        void Settings(){Modal("系统设置","音效："+(State.muted?"关闭":"开启")+"  /  动画："+(State.reducedMotion?"简化":"完整")+"\n游戏进度自动保存在本机。\n重置会清除本游戏的本地进度。",new[]{"切换音效","切换动画","重置进度","关闭"},new Action[]{()=>{State.muted=!State.muted;Save();Settings();},()=>{State.reducedMotion=!State.reducedMotion;Save();Settings();},()=>Modal("重置游戏进度","确认将所有角色、装备与关卡状态恢复初始值？",new[]{"确认重置","取消"},new Action[]{()=>{State=new AstralSave();equipmentFilter=characterTab=0;RefreshAll();Save();Toast("游戏进度已重置");},null}),null});}
        public void Modal(string title,string body,string[] options,Action[] callbacks)
        {
            modalActions=callbacks;SetText("ModalTitle",title);SetText("ModalBody",body);
            for(int i=0;i<4;i++){Active("ModalOption"+i,i<options.Length);if(i<options.Length)SetText("ModalOption"+i+"Label",options[i]);}
            Active("Modal",true);AnimateSelection("ModalPanel");
        }
        public void CloseModal(){Active("Modal",false);modalActions=Array.Empty<Action>();}
        public void Toast(string message){SetText("ToastText",message);if(toastRoutine!=null)StopCoroutine(toastRoutine);toastRoutine=StartCoroutine(ToastAnimation());}
        IEnumerator ToastAnimation()
        {
            var cg=Node("Toast").GetComponent<CanvasGroup>();cg.alpha=1;yield return new WaitForSecondsRealtime(2.3f);
            for(float t=0;t<.3f;t+=Time.unscaledDeltaTime){cg.alpha=1-t/.3f;yield return null;}cg.alpha=0;
        }
        void AnimateSelection(string node)
        {
            if(State.reducedMotion||!Node(node).gameObject.activeInHierarchy)return;
            // Independent fade coroutines are guarded by a version so rapid taps settle on the final selection.
            if(selectionRoutine!=null)StopCoroutine(selectionRoutine);
            foreach(string name in new[]{"CharacterHero","EquipmentHero","BattleBackdrop","ModalPanel","CharacterLore"}){var g=Node(name).GetComponent<CanvasGroup>();if(g!=null)g.alpha=1;}
            selectionRoutine=StartCoroutine(SelectionFade(Node(node)));
        }
        IEnumerator SelectionFade(Transform target)
        {
            var cg=target.GetComponent<CanvasGroup>();if(cg==null)cg=target.gameObject.AddComponent<CanvasGroup>();
            for(float t=0;t<.24f;t+=Time.unscaledDeltaTime){cg.alpha=Mathf.SmoothStep(.15f,1,t/.24f);yield return null;}cg.alpha=1;
        }
    }
}
