using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace AstralUI.Editor
{
    public static partial class AstralUIBuilder
    {
        const string Root = "Assets/AstralUI/";
        static AstralCatalog catalog;
        static Color Ink = new Color(.10f,.15f,.29f), Muted = new Color(.43f,.49f,.65f), Blue = new Color(.40f,.48f,1f), Gold = new Color(.96f,.74f,.34f);
        [MenuItem("Tools/Astral UI/Build Four Screens")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before rebuilding.");
            // Never overwrite an unsaved user scene.
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            ImportArt(); CreateCatalog();
            if(catalog.font == null || !catalog.font.dynamic) throw new InvalidOperationException("The CJK font must be complete and import as a dynamic font.");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("UI Camera", typeof(Camera), typeof(AudioListener));
            camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = new Color(.07f,.09f,.17f);
            camera.tag = "MainCamera";
            var canvas = new GameObject("Astral Lobby", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(AstralApp));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600,800); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            var fill=Img(canvas.transform,"ViewportBackdrop","background_ruins",0,0,1600,800,new Color(.35f,.4f,.6f)); Stretch(fill.rectTransform);
            var safe = Rect(canvas.transform,"Safe Area",0,0,1600,800); Stretch(safe); safe.gameObject.AddComponent<AstralSafeArea>();
            // Center the reference composition. Expand scaler guarantees the entire design fits 16:9 and wide phones.
            var stage = Rect(safe,"Stage",0,0,1600,800); stage.anchorMin=stage.anchorMax=new Vector2(.5f,.5f); stage.pivot=new Vector2(.5f,.5f); stage.anchoredPosition=Vector2.zero;
            var home = BuildHome(stage); SavePage(home,"HomeScreen");
            var equip = BuildEquipment(stage); SavePage(equip,"EquipmentScreen");
            var character = BuildCharacter(stage); SavePage(character,"CharacterScreen");
            var battle = BuildBattle(stage); SavePage(battle,"StageScreen");
            var app = canvas.GetComponent<AstralApp>(); app.catalog=catalog;
            app.pages=new GameObject[] {home.gameObject,equip.gameObject,character.gameObject,battle.gameObject};
            equip.gameObject.SetActive(false); character.gameObject.SetActive(false); battle.gameObject.SetActive(false);
            BuildTop(stage); var combat=BuildCombat(stage); SavePage(combat,"CombatHUD"); combat.gameObject.SetActive(false); canvas.AddComponent<AstralCombat>(); BuildOverlay(stage);
            new GameObject("EventSystem",typeof(EventSystem),typeof(StandaloneInputModule));
            PrefabUtility.SaveAsPrefabAsset(canvas,Root+"Prefabs/AstralLobby.prefab");
            EditorSceneManager.SaveScene(scene,Root+"Scenes/AstralLobby.scene");
            CreateReusableControls();
            var scenes=EditorBuildSettings.scenes.Where(s=>s.path!=Root+"Scenes/AstralLobby.scene").ToList();
            scenes.Insert(0,new EditorBuildSettingsScene(Root+"Scenes/AstralLobby.scene",true)); EditorBuildSettings.scenes=scenes.ToArray();
            AssetDatabase.SaveAssets();
            Selection.activeGameObject=canvas;
            File.WriteAllText("Documentation/AstralUI/build-result.json","{\"built\":true,\"pages\":4,\"buttons\":"+canvas.GetComponentsInChildren<Button>(true).Length+"}");
            Debug.Log("[AstralUI] Four screens, reusable controls, catalog and scene generated successfully.");
        }
        static void CreateReusableControls()
        {
            string[] names={"PrimaryButton","SecondaryButton","LightPanel","DarkPanel","GoldEquipmentCard","SelectedCard"};
            string[] sprites={"button_primary","button_secondary","panel_light","panel_dark","card_gold","card_selected"};
            for(int i=0;i<names.Length;i++)
            {
                RectTransform r;
                if(i<2)r=Btn(null,names[i],"按钮","close",0,0,0,280,64,sprites[i]);
                else r=Img(null,names[i],sprites[i],0,0,i<4?480:150,i<4?300:160,null,true).rectTransform;
                PrefabUtility.SaveAsPrefabAsset(r.gameObject,Root+"Prefabs/"+names[i]+".prefab");Object.DestroyImmediate(r.gameObject);
            }
        }
        static void ImportArt()
        {
            AssetDatabase.Refresh();
            foreach(var path in Directory.GetFiles(Root+"Art").Where(p=>p.EndsWith(".png")||p.EndsWith(".jpg")))
            {
                var ti=(TextureImporter)AssetImporter.GetAtPath(path); if(ti==null)continue;
                ti.textureType=TextureImporterType.Sprite; ti.spriteImportMode=SpriteImportMode.Single; ti.alphaIsTransparency=true;
                ti.mipmapEnabled=false; ti.isReadable=false; ti.wrapMode=TextureWrapMode.Clamp; ti.filterMode=FilterMode.Bilinear;
                ti.spritePixelsPerUnit=100; ti.maxTextureSize=2048; ti.textureCompression=TextureImporterCompression.CompressedHQ;
                var settings=new TextureImporterSettings();ti.ReadTextureSettings(settings);settings.spriteMeshType=SpriteMeshType.FullRect;ti.SetTextureSettings(settings);
                var name=Path.GetFileNameWithoutExtension(path);
                if(name.StartsWith("panel_")||name.StartsWith("button_")||name.StartsWith("card_")||name=="tab") ti.spriteBorder = name=="tab" ? new Vector4(32,24,62,24):new Vector4(32,32,32,32);
                if(name=="divider")ti.spriteBorder=new Vector4(20,4,20,4);
                if(name.StartsWith("icon_")||ti.spriteBorder!=Vector4.zero)ti.textureCompression=TextureImporterCompression.Uncompressed;
                var mobile=new TextureImporterPlatformSettings { name="Android",overridden=true,maxTextureSize=name.StartsWith("icon_")?128:2048,format=TextureImporterFormat.ASTC_6x6,compressionQuality=100 };
                ti.SetPlatformTextureSettings(mobile);mobile.name="iPhone";ti.SetPlatformTextureSettings(mobile);ti.SaveAndReimport();
            }
        }
        static void CreateCatalog()
        {
            catalog=AssetDatabase.LoadAssetAtPath<AstralCatalog>(Root+"Data/AstralCatalog.asset");
            if(catalog==null){catalog=ScriptableObject.CreateInstance<AstralCatalog>();AssetDatabase.CreateAsset(catalog,Root+"Data/AstralCatalog.asset");}
            catalog.font=AssetDatabase.LoadAssetAtPath<Font>(Root+"Fonts/NotoSansCJKsc-Regular.otf");
            catalog.art=Directory.GetFiles(Root+"Art").Where(p=>p.EndsWith(".png")||p.EndsWith(".jpg")).Select(p=>AssetDatabase.LoadAssetAtPath<Sprite>(p)).ToArray();
            catalog.characters=new[]{
                new CharacterData{name="星璃",english="SEIRI",id="seiri",element="虚空 / 强袭",quote="在无数次的轮回里，\n每一刻都记得你的名字。",hp=4876,attack=1280,defense=692,skill="星界裁决",talent="命运回响",accent=new Color(.71f,.42f,1)},
                new CharacterData{name="汐音",english="SHION",id="miku",element="流风 / 辅助",quote="循着风的声音，\n我们终会再度相逢。",hp=4250,attack=1060,defense=780,skill="碧海鸣奏",talent="风的眷顾",accent=new Color(.21f,.85f,.82f)},
                new CharacterData{name="月白",english="LUNA",id="luna",element="霜华 / 术师",quote="愿这片星海，\n替你收藏所有温柔。",hp=3980,attack=1492,defense=604,skill="永夜星典",talent="月之祝福",accent=new Color(.54f,.73f,1)},
                new CharacterData{name="绯烬",english="EMBER",id="ember",element="烈焰 / 先锋",quote="即使世界只剩灰烬，\n我也会为你燃起黎明。",hp=5280,attack=1345,defense=718,skill="赤焰断空",talent="不灭余烬",accent=new Color(1,.32f,.43f)},
                new CharacterData{name="霁羽",english="JIYE",id="jiye",element="曦光 / 游侠",quote="长夜终有尽头，\n我会为你留住第一缕晨光。",hp=4320,attack=1428,defense=635,skill="晨星逐羽",talent="黎明誓言",accent=new Color(1,.83f,.47f)},
                new CharacterData{name="鸢夜",english="YAYA",id="yaya",element="虚空 / 星术师",quote="群星从不说谎，\n而我愿意相信你的答案。",hp=4068,attack=1516,defense=612,skill="星仪寂灭",talent="永夜观测",accent=new Color(.72f,.43f,1)},
                new CharacterData{name="苍岚",english="CANGLAN",id="canglan",element="寒霜 / 剑卫",quote="风雪会记住来路，\n我的剑会守住你的归途。",hp=5620,attack=1260,defense=856,skill="霜锋断流",talent="不动寒心",accent=new Color(.40f,.74f,1)},
                new CharacterData{name="曜辰",english="YAOCHEN",id="yaochen",element="烈阳 / 守护",quote="站到我身后，\n让黎明替我们结束这场长夜。",hp=6180,attack=1178,defense=924,skill="曜日贯星",talent="炽阳壁垒",accent=new Color(1,.70f,.26f)} };
            string[] names={"虚空裁决","晨曦誓约","霜月流光","赤夜终焉","天穹之翼","潮汐双刃","虚界之核","日轮圣环","蚀星之握","终夜镰影","月咏星杖","焚天壁垒"};
            string[] types={"武器","武器","武器","武器","武器","武器","圣遗物","圣遗物","装甲","武器","武器","装甲"};
            catalog.equipment=new EquipmentData[12];
            for(int i=0;i<12;i++)catalog.equipment[i]=new EquipmentData{name=names[i],id="equipment_"+i.ToString("00"),type=types[i],attack=695-i*21,rarity=i%3==0?5:4,crit=12+i%4,critDamage=36+i*2,skill=i==0?"虚空之力":"星能共鸣",description=i==0?"造成伤害时，有 20% 概率触发虚空斩击，\n对目标造成 150% 攻击力的额外伤害。\n（冷却 8 秒）":"命中有 20% 概率追加 80% 攻击伤害（8 秒冷却）。\n终结技获得 12% 最大生命护盾，持续 6 秒。"};
            string[] cn={"艾尔废墟","幽影森林","霜语冰原","灼热荒漠","虚空之城"},en={"AEIR RUINS","SHADOW FOREST","FROSTLAND","BURNING DESERT","VOID CITY"},bg={"ruins","forest","frost","desert","void"};
            catalog.chapters=new ChapterData[5];
            for(int i=0;i<5;i++)catalog.chapters[i]=new ChapterData{name=cn[i],english=en[i],background="background_"+bg[i],description=new[]{"曾经繁荣的天空都市，如今只剩残垣。\n在破碎的光影中，仍有未知的力量苏醒。","树影深处回荡着古老的低语，\n跟随微光，寻找被遗忘的契约。","被永恒冰雪覆盖的圣堂，\n正等待一颗足以融化寒冬的心。","烈焰吞没了昔日的王城，\n灰烬之下，不屈的意志仍在燃烧。","跨越破碎的时空边界，\n星海尽头，命运将迎来最后的抉择。"}[i]};
            EditorUtility.SetDirty(catalog);
        }
        static RectTransform Rect(Transform parent,string name,float x,float y,float w,float h)
        {
            var go=new GameObject(name,typeof(RectTransform));var r=go.GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        static void Stretch(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;}
        static Image Img(Transform p,string n,string art,float x,float y,float w,float h,Color? tint=null,bool slice=false)
        {var r=Rect(p,n,x,y,w,h);var im=r.gameObject.AddComponent<Image>();im.sprite=catalog.Sprite(art);im.color=tint??Color.white;im.raycastTarget=false;if(slice){im.type=Image.Type.Sliced;im.pixelsPerUnitMultiplier=1.7f;}return im;}
        static Text Txt(Transform p,string n,string value,float x,float y,float w,float h,int size,Color? color=null,TextAnchor align=TextAnchor.MiddleLeft,bool bold=false)
        {var r=Rect(p,n,x,y,w,h);var t=r.gameObject.AddComponent<Text>();t.text=value;t.font=catalog.font;t.fontSize=size;t.color=color??Ink;t.alignment=align;t.raycastTarget=false;t.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;t.horizontalOverflow=HorizontalWrapMode.Wrap;t.verticalOverflow=VerticalWrapMode.Overflow;return t;}
        static RectTransform Btn(Transform p,string n,string text,string action,int arg,float x,float y,float w,float h,string art="button_secondary",int size=26,Color? color=null)
        {
            var im=Img(p,n,art,x,y,w,h,null,true);im.raycastTarget=true;var b=im.gameObject.AddComponent<Button>();b.targetGraphic=im;
            var colors=b.colors;colors.highlightedColor=new Color(.87f,.91f,1);colors.pressedColor=new Color(.68f,.72f,1);colors.disabledColor=new Color(.5f,.5f,.55f,.55f);b.colors=colors;
            var a=im.gameObject.AddComponent<AstralAction>();a.action=action;a.argument=arg;
            if(!string.IsNullOrEmpty(text))Txt(im.transform,n+"Label",text,8,0,w-16,h,size,color??(art=="button_primary"?Color.white:Ink),TextAnchor.MiddleCenter,true);
            b.navigation=new Navigation{mode=Navigation.Mode.None};return im.rectTransform;
        }
        static void Icon(Transform p,string art,float x,float y,float size,Color? color=null){Img(p,art,"icon_"+art,x,y,size,size,color);}
        static void Line(Transform p,float x,float y,float w,Color? c=null){Img(p,"Hairline","",x,y,w,1,c??new Color(.52f,.60f,.82f,.25f));}
        static RectTransform Page(Transform p,string n){var r=Rect(p,n,0,0,1600,800);r.gameObject.AddComponent<CanvasGroup>();return r;}
        static void Backdrop(Transform p,string art="background_ruins",Color? c=null){Img(p,"Backdrop",art,0,0,1600,800,c);}
        static void Shape(Transform p,string name,float x,float y,float w,float h,float slant,Color? c=null)
        {var r=Rect(p,name,x,y,w,h);var sh=r.gameObject.AddComponent<AstralShape>();sh.slant=slant;sh.color=c??new Color(.98f,.99f,1,.96f);sh.raycastTarget=false;}
        static void SavePage(RectTransform page,string name)
        {PrefabUtility.SaveAsPrefabAssetAndConnect(page.gameObject,Root+"Prefabs/"+name+".prefab",InteractionMode.AutomatedAction);}
        static RectTransform BuildHome(Transform p)
        {
            var r=Page(p,"Home");Backdrop(r);
            Img(r,"HomeLeftShade","panel_dark",0,0,228,800,new Color(.25f,.29f,.45f,.55f),true);
            var hero=Img(r,"HomeHero","character_seiri",25,-110,1020,1530);hero.gameObject.AddComponent<AstralAmbient>().amplitude=4;AstralFrameAnimationTool.Mount(hero.gameObject);
            var avatar=Btn(r,"Profile","","profile",0,24,16,116,116,"card_gold");Img(avatar,"HomeAvatar","portrait_seiri",7,6,102,103);
            Txt(r,"PlayerLevel","Lv. 40",152,26,220,26,20,Color.white);Txt(r,"PlayerName","星月的旅人",152,54,240,35,27,Color.white,TextAnchor.MiddleLeft,true);Txt(r,"UID","UID: 10025678",152,94,220,20,16,new Color(.81f,.86f,1));
            string[] labels={"任务","邮件","活动","图鉴","成就","设置"},eng={"TASK","MAIL","EVENT","GALLERY","ACHIEVE","SETTINGS"},ic={"quest","mail","event","gallery","talent","settings"},ac={"quest","mail","event","gallery","achievement","settings"};
            for(int i=0;i<6;i++){var b=Btn(r,"HomeMenu"+i,"",ac[i],0,26,164+i*80,156,68,"panel_dark");Icon(b,ic[i],10,15,36);Txt(b,"Title",labels[i],63,8,80,30,26,Color.white);Txt(b,"English",eng[i],64,39,88,18,12,new Color(.74f,.80f,.94f));}
            Txt(r,"LogoKicker","O T H E R S I D E  /  彼 岸 计 划",995,137,560,24,16,new Color(.15f,.22f,.36f),TextAnchor.MiddleCenter);
            var title=Txt(r,"GameTitle","异境牌师",975,158,590,142,96,new Color(.04f,.10f,.20f),TextAnchor.MiddleCenter,true);title.fontStyle=FontStyle.BoldAndItalic;
            Txt(r,"GameSubtitle","OTHERWORLD CARD MASTER",1018,293,512,28,19,Ink,TextAnchor.MiddleCenter);
            Line(r,1050,334,448,new Color(.15f,.22f,.40f,.5f));Txt(r,"Tagline","每一次选择，都是命运的赌注",993,345,554,36,25,Ink,TextAnchor.MiddleCenter);
            string[] cards={"角色","装备","关卡"},sub={"CHARACTER","EQUIPMENT","BATTLE"},sprites={"portrait_seiri","equipment_00","background_ruins"};
            for(int i=0;i<3;i++)
            {
                var b=Btn(r,"HomeCard"+i,"","navigate",new[]{2,1,3}[i],744+i*274,418,262,237,i==0?"card_purple":"card_selected");
                var mask=Rect(b,"ArtMask",10,10,242,214);mask.gameObject.AddComponent<RectMask2D>();
                if(i==0)Img(mask,"CardArt",sprites[i],-15,-36,274,278);else if(i==1)Img(mask,"CardArt",sprites[i],42,0,170,183);else Img(mask,"CardArt",sprites[i],-89,0,430,215);
                Img(b,"CardShade","panel_dark",10,155,242,72,new Color(.35f,.40f,.65f,.86f),true);
                Txt(b,"Title",cards[i],24,161,190,44,39,Color.white,TextAnchor.MiddleLeft,true);Txt(b,"English",sub[i],24,204,208,19,16,new Color(.78f,.83f,1));
            }
            var banner=Btn(r,"EventBanner","","event",0,28,672,488,108,"panel_dark");
            var bm=Rect(banner,"ArtMask",5,5,475,98);bm.gameObject.AddComponent<RectMask2D>();Img(bm,"EventCharacter","character_luna",-60,-15,298,447);
            Txt(banner,"Limited","限时活动  /  LIMITED EVENT",225,12,246,24,16,Color.white);Txt(banner,"EventTitle","深渊的来信",217,38,264,43,34,Color.white,TextAnchor.MiddleLeft,true);Txt(banner,"EventDate","2026.09.16 — 2026.10.07",223,81,245,18,13,new Color(.8f,.85f,1));
            var start=Btn(r,"StartGame","开始游戏","navigate",3,1200,692,366,87,"button_primary",44);Icon(start,"event",285,24,43);
            Txt(r,"HomeVersion","VER. 1.0   /   星界连接稳定",752,756,400,26,15,new Color(.87f,.91f,1));
            return r;
        }
        static void BuildTop(Transform p)
        {
            var bar=Rect(p,"TopBar",0,0,1600,72);
            Img(bar,"TopGlass","panel_dark",0,0,1600,69,new Color(.29f,.36f,.55f,.91f),true);
            var home=Btn(bar,"BackHome","返回主界面","navigate",0,18,5,284,59,"panel_dark",25,Color.white);Icon(home,"home",11,8,41);home.Find("BackHomeLabel").GetComponent<RectTransform>().anchoredPosition=new Vector2(48,0);home.Find("BackHomeLabel").GetComponent<RectTransform>().sizeDelta=new Vector2(227,59);
            var currencies=new[]{"crystal","energy","coin"};var values=new[]{"5780","240","125.6K"};
            for(int i=0;i<3;i++){var b=Btn(bar,"Currency"+i,"","currency",i,813+i*166,13,157,41,"panel_dark");Icon(b,currencies[i],-8,-5,46);Txt(b,"CurrencyValue"+i,values[i]+" +",38,0,108,39,22,Color.white,TextAnchor.MiddleCenter);}
            for(int i=0;i<3;i++){var b=Btn(bar,"TopIcon"+i,"",new[]{"friends","mail","menu"}[i],0,1342+i*75,10,64,47,"panel_dark");Icon(b,new[]{"friends","mail","menu"}[i],14,6,35);}
        }
        static void Sidebar(Transform p,string prefix,string[] labels,string[] english,string[] icons,string action,float x,float y,float width,float height)
        {
            for(int i=0;i<labels.Length;i++)
            {var b=Btn(p,prefix+i,"",action,i,x,y+i*height,width,height-5,i==0?"button_secondary":"panel_dark");Icon(b,icons[i],21,19,40);Txt(b,prefix+"Text"+i,labels[i],83,10,width-92,39,28,i==0?Ink:Color.white,TextAnchor.MiddleLeft,true);Txt(b,prefix+"En"+i,english[i],84,48,width-92,20,14,i==0?Muted:new Color(.73f,.79f,.9f));}
        }
        static RectTransform BuildEquipment(Transform p)
        {
            var r=Page(p,"Equipment");Backdrop(r,"background_ruins",new Color(.77f,.83f,1));
            Img(r,"EquipmentWash","panel_light",213,72,955,728,new Color(1,1,1,.46f),true);
            Shape(r,"DetailsGlass",602,94,576,706,0);
            Img(r,"InventoryGlass","panel_light",1137,77,448,710,Color.white,true);
            Img(r,"EquipmentSide","panel_dark",0,70,215,730,new Color(.40f,.46f,.64f,.85f),true);
            Sidebar(r,"EquipTab",new[]{"装备","强化","升星","套装","分解"},new[]{"EQUIP","ENHANCE","RANK UP","SET","DISMANTLE"},new[]{"sword","enhance","star","set","dismantle"},"equipTab",7,108,207,103);
            Txt(r,"EquipmentWatermark","E Q U I P M E N T",253,118,335,28,22,new Color(.89f,.92f,1,.6f));
            var weapon=Img(r,"EquipmentHero","equipment_00",212,140,392,545);weapon.preserveAspect=true;weapon.gameObject.AddComponent<AstralAmbient>().amplitude=9;
            Txt(r,"EquipmentElement","虚空 · 共鸣武装",257,696,294,36,22,Color.white,TextAnchor.MiddleCenter);Txt(r,"EquipmentSerial","ASTRAL ARMORY   /   No. 001",242,738,332,23,14,new Color(.76f,.82f,.98f),TextAnchor.MiddleCenter);
            Txt(r,"EqName","虚空裁决",640,120,422,48,39,Ink,TextAnchor.MiddleLeft,true);
            Txt(r,"EqStars","★★★★★",640,172,350,34,28,Gold);
            Txt(r,"EqLevel","Lv. 80 / 100",640,222,343,36,26,Ink);var lk=Btn(r,"EquipmentLock","","lock",0,1066,222,49,43,"button_secondary");Icon(lk,"lock",12,8,26);
            string[] attrs={"攻击","暴击率","暴击伤害","元素精通"};string[] icons={"sword","shield","talent","set"};string[] vals={"695","12.0%","36.0%","48"};
            for(int i=0;i<4;i++){Icon(r,icons[i],646,276+i*45,27,new Color(.38f,.45f,.68f));Txt(r,"EqAttributeLabel"+i,attrs[i],687,269+i*45,236,44,24,Muted);Txt(r,"EqAttribute"+i,vals[i],944,269+i*45,162,44,25,Ink,TextAnchor.MiddleRight);Line(r,646,314+i*45,463);}
            Img(r,"SkillHeader","panel_light",631,476,484,42,new Color(.91f,.94f,1),true);Txt(r,"EquipSkillHeading","装备特效",649,478,450,37,25,Ink,TextAnchor.MiddleLeft,true);
            Txt(r,"EqSkill","虚空之力",648,532,437,32,26,Ink,TextAnchor.MiddleLeft,true);
            Txt(r,"EqDescription","造成伤害时，有 20% 概率触发虚空斩击，\n对目标造成 150% 攻击力的额外伤害。\n（冷却 8 秒）",648,571,460,106,22,Muted);
            Btn(r,"EquipToggle","卸下","equip",0,643,716,216,62,"button_secondary",28);
            Btn(r,"EquipUpgrade","强化","upgrade",0,878,716,236,62,"button_primary",28);
            Btn(r,"FilterAll","全部 ▾","filter",0,1154,93,199,45,"button_secondary",22);
            var f1=Btn(r,"FilterWeapon","","filter",1,1360,93,64,45,"button_secondary");Icon(f1,"sword",19,8,27,new Color(.35f,.42f,.65f));
            var f2=Btn(r,"FilterRelic","","filter",2,1430,93,64,45,"button_secondary");Icon(f2,"set",19,8,27,new Color(.35f,.42f,.65f));
            var f3=Btn(r,"FilterArmor","","filter",3,1500,93,64,45,"button_secondary");Icon(f3,"shield",19,8,27,new Color(.35f,.42f,.65f));
            for(int i=0;i<12;i++)
            {
                int col=i%3,row=i/3;var b=Btn(r,"EquipmentSlot"+i,"","equipment",i,1154+col*139,156+row*147,131,137,i%3==0?"card_gold":"card_purple");
                var icon=Img(b,"EquipmentSlotIcon"+i,"equipment_"+i.ToString("00"),13,7,104,100);icon.preserveAspect=true;
                Txt(b,"EquipmentSlotLevel"+i,"Lv. 80",10,108,111,24,17,Color.white,TextAnchor.MiddleCenter);
                var lockIcon=Img(b,"EquipmentSlotLock"+i,"icon_lock",105,9,16,19);lockIcon.gameObject.SetActive(i<3);
                Img(b,"EquipmentSlotSelected"+i,"card_selected",0,0,131,137,new Color(.70f,.78f,1,.3f),true).gameObject.SetActive(i==0);
                Txt(b,"EquipmentSlotEquipped"+i,"E",7,5,19,24,15,Gold,TextAnchor.MiddleCenter,true);
            }
            Txt(r,"InventoryCount","持有装备  12 / 200",1170,751,374,30,20,Muted,TextAnchor.MiddleRight);
            return r;
        }
        static RectTransform BuildCharacter(Transform p)
        {
            var r=Page(p,"Character");Backdrop(r,"background_ruins",new Color(.76f,.83f,1));
            Img(r,"CharLeftGlass","panel_light",0,70,162,730,new Color(1,1,1,.98f),true);
            var hero=Img(r,"CharacterHero","character_seiri",30,50,920,1380);hero.gameObject.AddComponent<AstralAmbient>().amplitude=5;
            var roster=Rect(r,"CharacterRoster",8,104,147,554);
            var viewport=Img(roster,"RosterViewport","",0,0,147,554,new Color(1,1,1,.002f));viewport.raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect(viewport.transform,"RosterContent",0,0,147,catalog.characters.Length*125+8);
            var scroll=roster.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport.rectTransform;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=38;scroll.decelerationRate=.1f;
            for(int i=0;i<catalog.characters.Length;i++){var b=Btn(content,"CharacterSlot"+i,"","character",i,20,6+i*125,107,108,i==0?"card_gold":"button_secondary");Img(b,"Portrait", "portrait_"+catalog.characters[i].id,7,7,93,94);}
            Txt(r,"RosterHint","滑动浏览 · "+catalog.characters.Length+" 位",8,669,147,26,17,Muted,TextAnchor.MiddleCenter);
            var archive=Btn(r,"Roster","","gallery",0,47,710,65,65,"button_secondary");Icon(archive,"menu",17,17,31,new Color(.4f,.47f,.68f));
            Shape(r,"CharacterGlass",850,70,588,730,-80,new Color(.98f,.99f,1,.97f));
            Txt(r,"CharacterWatermark","C H A R A C T E R",1000,93,392,47,34,new Color(.75f,.80f,.91f,.65f),TextAnchor.MiddleRight);
            Txt(r,"CharName","星璃",896,168,355,58,47,Ink,TextAnchor.MiddleLeft,true);Txt(r,"CharEnglish","SEIRI   △ △ △ △",899,232,344,31,20,Muted);
            var heart=Btn(r,"Affinity","","affinity",0,1298,172,73,76,"button_secondary");Icon(heart,"heart",17,13,39,new Color(1,.5f,.65f));Txt(heart,"AffinityValue","10",19,20,36,28,19,Color.white,TextAnchor.MiddleCenter,true);
            Txt(r,"CharLevel","Lv. 80 / 80",898,281,346,36,31,Ink);Line(r,898,328,476);
            Txt(r,"CharSectionTitle","基础属性",908,345,436,36,26,Ink,TextAnchor.MiddleLeft,true);
            var stats=Rect(r,"CharacterStats",898,390,470,286);
            string[] labels={"生命","攻击","防御","暴击率","暴击伤害","元素精通"},icons={"heart","sword","shield","talent","event","set"},values={"4876","1280","692","15.0%","180.0%","32"};
            for(int i=0;i<6;i++){Icon(stats,icons[i],7,i*45+7,27,new Color(.40f,.47f,.68f));Txt(stats,"CharAttrLabel"+i,labels[i],52,i*45,248,39,25,Muted);Txt(stats,"CharAttr"+i,values[i],305,i*45,162,39,26,Ink,TextAnchor.MiddleRight);Line(stats,0,i*45+42,470);}
            var lore=Rect(r,"CharacterLore",899,397,468,272);Txt(lore,"CharLoreHeading","星界裁决",0,0,468,48,32,Ink,TextAnchor.MiddleLeft,true);Txt(lore,"CharLoreBody","",0,67,468,180,25,Muted);lore.gameObject.SetActive(false);
            Btn(r,"CharacterDetails","属性详情   ›","characterDetail",0,893,691,486,55,"button_secondary",25);
            Img(r,"QuoteShade","panel_dark",183,647,532,114,new Color(.35f,.39f,.55f,.6f),true);Txt(r,"CharQuote","「在无数次的轮回里，\n   每一刻都记得你的名字。」",207,659,491,89,25,Color.white);
            Img(r,"CharRightGlass","panel_light",1436,70,164,730,Color.white,true);
            string[] labelsR={"总览","技能","命之痕","时装"},english={"OVERVIEW","SKILL","TALENT","COSTUME"},icR={"costume","skill","talent","costume"};
            for(int i=0;i<4;i++){var b=Btn(r,"CharacterTab"+i,"","characterTab",i,1445,79+i*158,148,151,i==0?"panel_dark":"panel_light");Icon(b,icR[i],49,16,48,i==0?new Color(.73f,.5f,1):new Color(.36f,.43f,.63f));Txt(b,"CharTabTitle"+i,labelsR[i],0,78,148,39,27,i==0?Color.white:Ink,TextAnchor.MiddleCenter,true);Txt(b,"CharTabEnglish"+i,english[i],0,118,148,23,14,i==0?Color.white:Muted,TextAnchor.MiddleCenter);}
            return r;
        }
        static RectTransform BuildBattle(Transform p)
        {
            var r=Page(p,"Battle");Img(r,"BattleBackdrop","background_ruins",0,0,1600,800);
            Img(r,"ChapterRail","panel_dark",0,71,335,729,new Color(.26f,.34f,.51f,.83f),true);
            for(int i=0;i<5;i++){var b=Btn(r,"Chapter"+i,"","chapter",i,15,100+i*109,303,100,i==0?"tab":"panel_dark");Txt(b,"ChapterNumber"+i,(i+1).ToString("00"),20,12,66,62,40,i==0?Muted:Color.white);Txt(b,"ChapterTitle"+i,catalog.chapters[i].name,101,12,188,40,28,i==0?Ink:Color.white,TextAnchor.MiddleLeft,true);Txt(b,"ChapterEn"+i,catalog.chapters[i].english,102,58,184,24,14,i==0?Muted:new Color(.72f,.79f,.92f));}
            Txt(r,"WorldLabel","WORLD EXPLORATION",42,721,287,29,16,new Color(.8f,.84f,1));Txt(r,"WorldCoord","35°42′N  /  139°41′E",44,752,290,22,14,new Color(.62f,.71f,.87f));
            Shape(r,"BattleGlass",903,70,697,730,-155);
            Txt(r,"BattleWatermark","S T O R Y  /  星 界 旅 程",978,91,562,30,19,Muted);
            Txt(r,"ChapterHeading","01  艾尔废墟",952,142,589,52,39,Ink,TextAnchor.MiddleLeft,true);
            Txt(r,"ChapterDescription",catalog.chapters[0].description,976,216,567,84,25,Muted);
            Txt(r,"ProgressLabel","关卡进度",970,318,169,37,24,Ink,TextAnchor.MiddleLeft,true);
            Img(r,"ProgressTrack","",1141,335,287,5,new Color(.73f,.78f,.9f));Img(r,"ProgressFill","",1141,335,172,5,Blue);
            Txt(r,"ProgressValue","3 / 5",1454,315,115,40,31,Blue,TextAnchor.MiddleRight);
            for(int i=0;i<5;i++)
            {var b=Btn(r,"Stage"+i,"","stage",i,911+i*134,385,124,146,i==3?"card_selected":"card_gold");Img(b,"StageArt"+i,"background_ruins",9,9,106,128,new Color(.5f,.57f,.75f));Img(b,"StageFrame"+i,i==3?"card_selected":"card_gold",0,0,124,146,Color.white,true).fillCenter=false;Txt(b,"StageNumber"+i,"1-"+(i+1),14,10,94,27,21,Color.white);Icon(b,i==4?"lock":"event",42,52,41,i<3?Gold:Color.white);Txt(b,"StageStatus"+i,i<3?"CLEAR":i==3?"挑战":"LOCKED",6,115,112,23,17,i<3?Gold:Color.white,TextAnchor.MiddleCenter,true);}
            Txt(r,"SelectedStage","1-4   断裂的天桥",934,548,600,34,24,Ink,TextAnchor.MiddleCenter);
            for(int i=0;i<3;i++){var b=Btn(r,"Difficulty"+i,"","difficulty",i,896+i*229,607,218,75,i==0?"button_primary":"button_secondary");Txt(b,"DifficultyTitle"+i,new[]{"普通","困难","噩梦"}[i],0,5,218,38,29,i==0?Color.white:Ink,TextAnchor.MiddleCenter,true);Txt(b,"DifficultyEn"+i,new[]{"NORMAL","HARD","NIGHTMARE"}[i],0,44,218,22,13,i==0?Color.white:Muted,TextAnchor.MiddleCenter);}
            Btn(r,"Challenge","开始挑战","challenge",0,1080,711,314,72,"button_primary",37);
            var cost=Btn(r,"ChallengeCost","","currency",1,1395,711,179,72,"button_secondary");Icon(cost,"energy",18,19,39);Txt(cost,"EnergyCost","× 10",66,15,101,41,28,Ink,TextAnchor.MiddleCenter);
            return r;
        }
        static void BuildOverlay(Transform p)
        {
            // Small glowing accent line and reusable frame samples are actual sprites with borders.
            Img(p,"BottomAccent","divider",635,794,325,5);
            var transition=Rect(p,"Transition",0,0,1600,800);var tg=transition.gameObject.AddComponent<CanvasGroup>();tg.alpha=0;tg.blocksRaycasts=false;
            Shape(transition,"Sweep",0,0,1800,800,-230,new Color(.64f,.68f,1,.7f));
            var toast=Img(p,"Toast","panel_dark",494,86,612,60,Color.white,true);var cg=toast.gameObject.AddComponent<CanvasGroup>();cg.alpha=0;cg.blocksRaycasts=false;Txt(toast.transform,"ToastText","",16,4,580,50,24,Color.white,TextAnchor.MiddleCenter);
            var modal=Rect(p,"Modal",0,0,1600,800);var dim=Img(modal,"Dim","",0,0,1600,800,new Color(.025f,.04f,.12f,.77f));dim.raycastTarget=true;
            var panel=Img(modal,"ModalPanel","panel_light",407,177,786,470,Color.white,true);
            Txt(panel.transform,"ModalKicker","A S T R A L  L I N K",46,25,570,29,17,Muted);
            Txt(panel.transform,"ModalTitle","提示",46,67,660,55,37,Ink,TextAnchor.MiddleLeft,true);
            Line(panel.transform,45,135,692);Txt(panel.transform,"ModalBody","",47,153,688,173,26,Muted);
            for(int i=0;i<4;i++)Btn(panel.transform,"ModalOption"+i,"确定","modalOption",i,44+i*178,362,165, sixty(),i==0?"button_primary":"button_secondary",23);
            var close=Btn(panel.transform,"CloseModal","","close",0,709,28,43,43,"button_secondary");Icon(close,"close",11,11,21,new Color(.39f,.46f,.69f));modal.gameObject.SetActive(false);
        }
        static int sixty(){return 60;}
    }
}
