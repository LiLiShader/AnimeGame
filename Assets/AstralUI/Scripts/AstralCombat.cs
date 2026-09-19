using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace AstralUI
{
    /// <summary>Native 2D survivor simulation. Actors, projectiles, loot and effects reuse bounded pools.</summary>
    public partial class AstralCombat : MonoBehaviour
    {
        public bool Running {get;private set;}
        public bool Finished {get;private set;}
        public bool Paused {get;private set;}
        public bool Choosing {get;private set;}
        public float Elapsed {get;private set;}
        public float HP {get;private set;}
        public float MaxHP {get;private set;}
        public float Attack {get;private set;}
        public float CriticalChance {get;private set;}
        public float CriticalMultiplier {get;private set;}
        public int Level {get;private set;}
        public int PendingUpgrades {get;private set;}
        public int Kills {get;private set;}
        public int[] Ranks {get;private set;}=new int[12];
        public int[] Choices {get;private set;}=new int[3];
        public Vector2 PlayerPosition => position;
        public int ActiveEnemies {get{int n=0;foreach(var e in enemies)if(e.live)n++;return n;}}
        public static readonly string[] SkillNames={"星陨飞刃","霜华领域","天穹雷链","赤焰流星","环星剑阵","虚空引力","月下花语","星光壁垒","流风羽翼","星尘引力","时隙回响","命定一击"};
        static readonly int[] Starter={0,2,1,3,0,5,4,2};
        static readonly string[] CharacterEffects={"全场斩击，造成 600% 攻击伤害。","回复 35% 生命，并发动全场雷击。","冻结全场 4 秒，造成 400% 伤害。","降下 12 枚陨星，每枚造成 350% 范围伤害。","发射 24 支穿透箭，造成 300% 伤害。","生成 6 秒黑洞，持续吸引并伤害敌人。","获得 25% 生命护盾，并发动冰霜斩击。","获得 35% 生命护盾，并发动烈阳轰击。"};
        public static string CharacterSkillDescription(int index){return "终结技 · "+CharacterEffects[Mathf.Clamp(index,0,7)]+"\n冷却 18 秒；局内按 E 或点击终结技。\n命痕：进入战斗后攻击提升 12%。";}
        public static string UpgradeDescription(int id,int rank)
        {
            switch(id){
                case 0:return "每轮额外发射 "+(rank+1)+" 枚飞刃\n满阶 + 暴击共鸣：双倍齐射";
                case 1:return "周期冰环，范围 "+(2.4f+rank*.35f).ToString("0.0")+" 米\n造成伤害并减速，满阶冻结";
                case 2:return "每轮连锁命中 "+(2+rank)+" 个目标\n满阶 + 冷却共鸣：雷链超载";
                case 3:return "每轮召唤 "+(1+rank/2)+" 枚陨星\n每枚造成 "+(180+rank*40)+"% 范围伤害";
                case 4:return (2+rank)+" 柄环绕飞刃\n靠近敌人时持续造成伤害";
                case 5:return "周期生成引力黑洞\n吸引敌人，持续 "+(2+rank*.35f).ToString("0.0")+" 秒";
                case 6:return "每秒恢复 "+(rank*.7f).ToString("0.0")+"% 最大生命\n恢复效果在战斗中持续生效";
                case 7:return "最大生命 +"+(rank*15)+"%\n立即回复 15% 基础生命";
                case 8:return "移动速度 +"+(rank*9)+"%\n更轻松地穿过敌群";
                case 9:return "拾取范围 +"+(rank*40)+"%\n经验获取 +"+(rank*10)+"%";
                case 10:return "攻击与技能冷却 -"+(rank*7)+"%\n终结技也获得冷却加速";
                default:return "暴击率 +"+(rank*5)+"%\n暴击伤害 +"+(rank*12)+"%";
            }
        }
        class Actor{public SpriteRenderer art,shadow;public bool live,elite;public Vector2 p,v;public float hp,max,speed,clock,slow,freeze,flash,spawn;public int type;}
        class Shot{public SpriteRenderer art;public bool live,hostile;public Vector2 p,v;public float damage,life,radius;public int pierce;public HashSet<Actor> hit=new HashSet<Actor>();}
        class Loot{public SpriteRenderer art,glow;public bool live,attracting;public float pullSpeed;public Vector2 p;public int value,type;}
        class Effect{public SpriteRenderer art;public bool live;public Vector2 p;public float life,total,size;public Color color;}
        class Zone{public Vector2 p;public float warning,remaining,radius,damage,tick;public int kind;public SpriteRenderer art;}
        class Number{public Text text;public Vector2 p;public float life;}
        readonly List<Actor> enemies=new List<Actor>();
        readonly List<Shot> shots=new List<Shot>();
        readonly List<Loot> loot=new List<Loot>();
        readonly List<Effect> effects=new List<Effect>();
        readonly List<Zone> zones=new List<Zone>();
        readonly List<Number> numbers=new List<Number>();
        readonly List<SpriteRenderer> orbit=new List<SpriteRenderer>();
        readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        readonly float[] skillTimers=new float[6];
        readonly float[] skillCooldowns=new float[6];
        AstralApp app;Transform world;Camera cam;SpriteRenderer hero,halo,ground;AstralJoystick joystick;
        Vector2 position,lastDirection=Vector2.right,dashDirection;
        float baseHP,defense,baseCrit,baseCritDamage,experience,nextXP,spawnClock,attackClock,invulnerable,dashTime,dashCooldown,ultimateCooldown,equipmentCooldown,shield,totalDamage,noticeTime,hudClock,orbitTick,shake;
        float stageScale, shieldTime, eliteClock, hazardClock;int character,equipment,chapter,stage,difficulty,runCoins,rerolls,rewardGold,rewardCrystals,numberCursor;
        bool bossSpawned,won,retreatConfirm;Actor boss;
        AudioSource sfx,music;AudioClip hitClip,levelClip,ultimateClip,musicClip;
        public void Begin()
        {
            if(Running&&!Finished)return;
            app=GetComponent<AstralApp>();if(!app.CanEnterStage(app.State.chapter,app.State.difficulty,app.State.stage)){app.Toast("当前关卡或难度尚未解锁");return;}int cost=10+app.State.difficulty*5;
            if(app.State.energy<cost){app.Toast("能量不足，请返回局外补给");return;}
            StopAllCoroutines();ClearBattleWorld();
            app.State.energy-=cost;app.Save();app.CloseModal();
            character=app.State.character;equipment=app.State.equipped[character];chapter=app.State.chapter;stage=app.State.stage;difficulty=app.State.difficulty;
            var c=app.catalog.characters[character];var e=equipment>=0?app.catalog.equipment[equipment]:null;
            baseHP=c.hp/12f;MaxHP=baseHP;HP=MaxHP;Attack=(c.attack+(e==null?0:e.attack+(app.State.levels[equipment]-60)*8))/35f*1.12f;
            defense=Mathf.Clamp(c.defense/3000f,0,.5f);baseCrit=.15f+(e==null?0:e.crit/100);baseCritDamage=1.8f+(e==null?0:e.critDamage/100);CriticalChance=baseCrit;CriticalMultiplier=baseCritDamage;
            stageScale=1+chapter*.16f+stage*.06f+difficulty*.32f;
            Ranks=new int[12];Ranks[Starter[character]]=1;Array.Clear(skillTimers,0,6);Array.Clear(skillCooldowns,0,6);
            position=Vector2.zero;lastDirection=Vector2.right;Elapsed=0;numberCursor=0;hudClock=0;dashDirection=Vector2.zero;Level=1;PendingUpgrades=0;Kills=0;experience=0;nextXP=10;runCoins=0;rerolls=3;
            eliteClock=60;hazardClock=25;shieldTime=0;spawnClock=.6f;attackClock=.2f;invulnerable=2;dashTime=0;dashCooldown=0;ultimateCooldown=3;equipmentCooldown=0;shield=0;totalDamage=0;orbitTick=0;shake=0;bossSpawned=false;boss=null;
            Finished=Paused=Choosing=won=retreatConfirm=false;Running=true;rewardGold=rewardCrystals=0;
            for(int i=0;i<app.pages.Length;i++)app.pages[i].SetActive(false);
            SetActive("TopBar",false);SetActive("ViewportBackdrop",false);SetActive("BottomAccent",false);SetActive("CombatHUD",true);
            foreach(string n in new[]{"CombatPausePanel","CombatUpgrade","CombatResult","CombatBoss"})SetActive(n,false);
            Img("CombatPortrait").sprite=Art("portrait_"+c.id);Img("ResultHero").sprite=Art("character_"+c.id);Img("UltimateIcon").sprite=Art("combat_skill_"+Starter[character]);
            joystick=app.Node("CombatJoystick").GetComponent<AstralJoystick>();
            SetupWorld();SetupSound();ResetFeedback();music.mute=app.State.muted;if(!music.isPlaying)music.Play();
            Notice("裂隙开启 · 活下去",3);RefreshRanks();UpdateHUD();
        }
        Sprite Art(string id){if(!sprites.TryGetValue(id,out var s)){s=app.catalog.Sprite(id);sprites[id]=s;}return s;}
        Image Img(string n){return app.Node(n).GetComponent<Image>();}
        void Txt(string n,string v){app.Node(n).GetComponent<Text>().text=v;}
        void SetActive(string n,bool b){app.Node(n).gameObject.SetActive(b);}
        void Bar(string n,float width,float ratio){var r=(RectTransform)app.Node(n);r.sizeDelta=new Vector2(width*Mathf.Clamp01(ratio),r.sizeDelta.y);}
        SpriteRenderer Render(string name,string sprite,float size,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(world,false);var r=go.AddComponent<SpriteRenderer>();r.sprite=Art(sprite);r.sortingOrder=order;Scale(r,size);return r;
        }
        void Scale(SpriteRenderer r,float size){if(r.sprite!=null)r.transform.localScale=Vector3.one*(size/Mathf.Max(r.sprite.bounds.size.x,r.sprite.bounds.size.y));}
        void ClearBattleWorld()
        {
            // Managed pool lists can be lost on an editor script reload while scene objects survive.
            // Retire the owned root itself, including any objects no longer present in those lists.
            foreach(var root in gameObject.scene.GetRootGameObjects())
            {
                if(root.name!="Astral Combat World (pooled)")continue;
                root.SetActive(false);Destroy(root);
            }
            world=null;hero=halo=ground=null;boss=null;
            enemies.Clear();shots.Clear();loot.Clear();effects.Clear();zones.Clear();orbit.Clear();numbers.Clear();
            if(app!=null)for(int i=0;i<48;i++)
            {
                var node=app.Node("DamageNumber"+i);if(node!=null)node.GetComponent<Text>().text="";
            }
        }
        void SetupWorld()
        {
            if(world==null){world=new GameObject("Astral Combat World (pooled)").transform;ground=Render("Generated Ruins Arena","combat_ground",34,-1000);ground.transform.position=Vector3.zero;hero=Render("Player","combat_hero_"+character,1.85f,0);halo=Render("Player Aura","combat_fx_4",1.15f,-100);halo.color=new Color(.48f,.86f,1,.55f);for(int i=0;i<7;i++)orbit.Add(Render("Orbiting Blade "+i,"combat_fx_1",.8f,100));}
            world.gameObject.SetActive(true);ground.sprite=Art(chapter==0?"combat_ground":"combat_ground_"+chapter);Scale(ground,34);hero.sprite=Art("combat_hero_"+character);Scale(hero,1.85f);hero.color=Color.white;
            foreach(var e in enemies){e.live=false;e.art.gameObject.SetActive(false);e.shadow.gameObject.SetActive(false);}foreach(var s in shots){s.live=false;s.art.gameObject.SetActive(false);}foreach(var l in loot){l.live=false;l.art.gameObject.SetActive(false);if(l.glow!=null)l.glow.gameObject.SetActive(false);}foreach(var f in effects){f.live=false;f.art.gameObject.SetActive(false);}foreach(var z in zones)Destroy(z.art.gameObject);zones.Clear();
            if(numbers.Count==0)for(int i=0;i<48;i++)numbers.Add(new Number{text=app.Node("DamageNumber"+i).GetComponent<Text>()});foreach(var n in numbers){n.life=0;n.text.text="";}
            cam=Camera.main;cam.orthographic=true;cam.transform.position=new Vector3(0,0,-10);cam.orthographicSize=Mathf.Max(8.5f,17f/cam.aspect);
            // Background follows the arena theme; a tinted overscan backdrop prevents empty edges on tall displays.
            foreach(var o in orbit)o.gameObject.SetActive(false);
            hero.transform.position=position;halo.transform.position=(Vector3)position+Vector3.down*.55f;
        }
        void Update()
        {
            if(!Running)return;
            if(Input.GetKeyDown(KeyCode.Escape)&&SkillDetailsOpen)CloseSkillDetails();
            else if(Input.GetKeyDown(KeyCode.Escape)&&!Finished&&!Choosing)Command(0);
            if(!Paused&&!Choosing&&!Finished)
            {
                if(Input.GetKeyDown(KeyCode.Space))Dash();if(Input.GetKeyDown(KeyCode.E))Ultimate();
                var input=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));if(joystick.Value.sqrMagnitude>.01f)input=joystick.Value;
                Move(input,Mathf.Min(Time.deltaTime,.05f));Simulate(Mathf.Min(Time.deltaTime,.05f));
            }
            UpdateSkillCooldownUI();
            UpdateXPFeedback(Time.unscaledDeltaTime);
            hudClock-=Time.unscaledDeltaTime;if(hudClock<=0){hudClock=.08f;UpdateHUD();}
            if(noticeTime>0){noticeTime-=Time.unscaledDeltaTime;app.Node("CombatNotice").GetComponent<Text>().color=new Color(1,1,1,Mathf.Min(1,noticeTime));}
            cam.orthographicSize=Mathf.Max(8.5f,17f/cam.aspect);
            shake=Mathf.Max(0,shake-Time.unscaledDeltaTime);cam.transform.position=new Vector3(0,0,-10)+(app.State.reducedMotion?Vector3.zero:new Vector3(Mathf.Sin(Time.unscaledTime*81),Mathf.Cos(Time.unscaledTime*67),0)*shake*.17f);
        }
        public void Move(Vector2 input,float dt)
        {
            if(!Running||Paused||Choosing||Finished||PendingUpgrades>0)return;
            if(input.sqrMagnitude>1)input.Normalize();if(input.sqrMagnitude>.01f)lastDirection=input.normalized;
            float speed=3.35f*(1+Ranks[8]*.09f);if(dashTime>0){position+=dashDirection*13*dt;dashTime-=dt;}else position+=input*speed*dt;
            position.x=Mathf.Clamp(position.x,-14.6f,14.6f);position.y=Mathf.Clamp(position.y,-6.3f,5.6f);
            hero.transform.position=new Vector3(position.x,position.y+(input.sqrMagnitude>.01f?Mathf.Sin(Elapsed*18)*.035f:0),0);hero.flipX=lastDirection.x<0;hero.sortingOrder=50-(int)(position.y*10);
            hero.transform.rotation=Quaternion.Euler(0,0,input.x*-3);halo.transform.position=(Vector3)position+Vector3.down*.53f;
        }
        public void Simulate(float dt)
        {
            if(!Running||Paused||Choosing||Finished)return;
            // Drain earned choices before advancing combat or collecting more loot.
            if(PendingUpgrades>0){OpenUpgrade();return;}
            dt=Mathf.Clamp(dt,0,.05f);Elapsed+=dt;invulnerable-=dt;dashCooldown-=dt;ultimateCooldown-=dt;equipmentCooldown-=dt;
            if(shield>0){shieldTime-=dt;if(shieldTime<=0)shield=0;}
            hero.color=invulnerable>0?new Color(1,1,1,.65f+.35f*Mathf.Sin(Elapsed*36)):Color.white;
            if(Ranks[6]>0)HP=Mathf.Min(MaxHP,HP+MaxHP*.007f*Ranks[6]*dt);
            SpawnWaves(dt);UpdateEnemies(dt);if(Finished)return;UpdateAttacks(dt);if(Finished)return;UpdateShots(dt);if(Finished)return;UpdateZones(dt);if(Finished)return;UpdateLoot(dt);UpdateEffects(dt);UpdateNumbers(dt);
            if(!Finished){SettleExperience();if(PendingUpgrades>0)OpenUpgrade();}
        }
        void SpawnWaves(float dt)
        {
            spawnClock-=dt;
            if(spawnClock<=0){spawnClock=Mathf.Max(.25f,1.5f-Elapsed*.0033f)/(1+difficulty*.13f);int count=Elapsed>170?3:Elapsed>75?2:1;for(int i=0;i<count;i++)if(ActiveEnemies<105){int max=Elapsed<22?1:Elapsed<50?2:Elapsed<85?3:5;int type=UnityEngine.Random.Range(0,max);SpawnEnemy(type);}}
            eliteClock-=dt;if(eliteClock<=0&&Elapsed<300&&ActiveEnemies<105){eliteClock=60;var elite=SpawnEnemy(2);elite.elite=true;elite.max*=5;elite.hp=elite.max;elite.speed*=1.35f;Scale(elite.art,1.9f);Notice("精英来袭 · 击败获得星界宝箱",3);}
            hazardClock-=dt;if(hazardClock<=0&&chapter>0){hazardClock=25-chapter*2;AddZone(position+lastDirection*1.5f,1.4f,1.6f,.15f,22*stageScale,2);}
            if(!bossSpawned&&Elapsed>=300){bossSpawned=true;boss=SpawnEnemy(5);SetActive("CombatBoss",true);Notice("警告 · 深渊领主降临",4);Play(ultimateClip);}
        }
        Actor SpawnEnemy(int type)
        {
            Actor e=enemies.Find(a=>!a.live);if(e==null){e=new Actor{art=Render("Pooled Enemy","combat_enemy_0",1,0),shadow=Render("Enemy Spawn Rune","combat_fx_7",1,-80)};enemies.Add(e);}
            int edge=UnityEngine.Random.Range(0,4);e.p=edge==0?new Vector2(-14.3f,UnityEngine.Random.Range(-6f,5.2f)):edge==1?new Vector2(14.3f,UnityEngine.Random.Range(-6f,5.2f)):edge==2?new Vector2(UnityEngine.Random.Range(-14f,14f),5.5f):new Vector2(UnityEngine.Random.Range(-14f,14f),-6.4f);
            if(type==5)e.p=new Vector2(0,4.8f);
            e.live=true;e.elite=false;e.type=type;e.v=Vector2.zero;e.slow=e.freeze=e.flash=0;e.spawn=1.05f;e.clock=2+UnityEngine.Random.value*2;
            e.max=e.hp=(type==5?7500:new[]{42,32,160,75,115}[type])*(1+Elapsed*.0024f)*stageScale;e.speed=type==5?.65f:new[]{.9f,1.8f,.63f,.65f,1.2f}[type];e.speed*=1+Elapsed*.0006f;
            e.art.sprite=Art("combat_enemy_"+type);Scale(e.art,type==5?2.75f:type==2?1.45f:1.25f);e.art.color=Color.white;e.art.gameObject.SetActive(true);e.shadow.gameObject.SetActive(true);Scale(e.shadow,type==5?3:1.3f);e.art.transform.position=e.p;e.shadow.transform.position=e.p;
            return e;
        }
        void UpdateEnemies(float dt)
        {
            foreach(var e in enemies)
            {
                if(!e.live)continue;e.flash-=dt;e.slow-=dt;e.freeze-=dt;e.spawn-=dt;
                if(e.spawn>0){e.art.color=new Color(1,1,1,.3f);e.shadow.color=new Color(1,.25f,.48f,.6f);continue;}
                e.shadow.color=new Color(.06f,.05f,.13f,.4f);Scale(e.shadow,e.type==5?1.8f:.65f);e.shadow.transform.position=(Vector3)e.p+Vector3.down*.38f;
                Vector2 delta=position-e.p;float distance=delta.magnitude;
                if(e.freeze<=0){e.clock-=dt;float speed=e.speed*(e.slow>0?.42f:1);Vector2 direction=delta.normalized;
                    if(e.type==3){direction*=distance<4?-1:distance>6?1:0;if(e.clock<=0){e.clock=2.8f;EnemyShot(e.p,delta.normalized,4.2f,18*stageScale);}}
                    if(e.type==4){if(e.clock<=0){e.clock=3.8f;e.v=delta.normalized;AddZone(e.p+e.v*1.8f,.65f,.55f,.05f,0,3);}if(e.clock<3.25f&&e.clock>2.7f){direction=e.v;speed=7;}else if(e.clock>3.25f)speed=.2f;}
                    if(e.type==5&&e.clock<=0){e.clock=e.hp<e.max*.5f?2.3f:3.6f;int count=e.hp<e.max*.5f?14:9;for(int i=0;i<count;i++){float a=i*Mathf.PI*2/count+Elapsed;EnemyShot(e.p,new Vector2(Mathf.Cos(a),Mathf.Sin(a)),2.6f,24*stageScale);}AddZone(position,1.6f,1.25f,.2f,44*stageScale,2);if(e.hp<e.max*.5f)AddZone(position+lastDirection*2.2f,1.2f,1.6f,.2f,35*stageScale,2);}
                    // Light local separation avoids enemies collapsing into an unreadable single point.
                    Vector2 separate=Vector2.zero;int seen=0;foreach(var other in enemies){if(other==e||!other.live)continue;var d=e.p-other.p;float sq=d.sqrMagnitude;if(sq<.4f&&sq>.0001f){separate+=d.normalized*(.65f-Mathf.Sqrt(sq));if(++seen>=5)break;}}
                    e.p+=(direction*speed+separate*2.5f)*dt;
                }
                e.p.x=Mathf.Clamp(e.p.x,-15,15);e.p.y=Mathf.Clamp(e.p.y,-6.8f,6.5f);
                e.art.transform.position=(Vector3)e.p+Vector3.up*Mathf.Sin(Elapsed*7+e.max)*.045f;e.art.flipX=delta.x<0;e.art.sortingOrder=45-(int)(e.p.y*10);
                e.art.color=e.flash>0?new Color(1,.46f,.66f):e.freeze>0?new Color(.39f,.81f,1):e.elite?new Color(1,.78f,.38f):Color.white;
                if(distance<(e.type==5?1.15f:.64f))HurtPlayer((e.type==5?42:22)*stageScale);
            }
        }
        Actor Nearest(Vector2 p,float range=100)
        {Actor best=null;float sq=range*range;foreach(var e in enemies)if(e.live&&e.spawn<=0){float d=(e.p-p).sqrMagnitude;if(d<sq){sq=d;best=e;}}return best;}
        void UpdateAttacks(float dt)
        {
            float cooldown=1-Ranks[10]*.07f;attackClock-=dt;
            var target=Nearest(position);
            if(attackClock<=0&&target!=null){attackClock=.66f*cooldown;Fire(position,(target.p-position).normalized,Attack,0,9,1,2.1f);if(character==4)Fire(position,Rotate((target.p-position).normalized,7),Attack*.7f,0,9,1,2.1f);}
            for(int id=0;id<6;id++)
            {
                skillTimers[id]-=dt;if(Ranks[id]==0||skillTimers[id]>0||target==null||id==4)continue;int rank=Ranks[id];
                if(id==0){skillCooldowns[id]=skillTimers[id]=2.5f*cooldown;int n=rank+1;if(rank==5&&Ranks[11]>0)n*=2;for(int j=0;j<n;j++)Fire(position,Rotate((target.p-position).normalized,(j-(n-1)/2f)*9),Attack*(.9f+rank*.12f),1,8,rank>=5?3:1,2.2f);}
                if(id==1){skillCooldowns[id]=skillTimers[id]=5.5f*cooldown;Area(position,2.4f+rank*.35f,Attack*(1+rank*.2f),rank==5?2:0,3);Fx(position,4,5+rank*.6f,.6f,new Color(.55f,.85f,1,.8f));}
                if(id==2){skillCooldowns[id]=skillTimers[id]=3.6f*cooldown;int n=2+rank;if(rank==5&&Ranks[10]>0)n*=2;var used=new HashSet<Actor>();Vector2 from=position;for(int j=0;j<n;j++){Actor e=null;float best=49;foreach(var enemy in enemies)if(enemy.live&&enemy.spawn<=0&&!used.Contains(enemy)){float d=(enemy.p-from).sqrMagnitude;if(d<best){best=d;e=enemy;}}if(e==null)break;used.Add(e);Beam(from,e.p,new Color(.62f,.85f,1));Hit(e,Attack*(1.2f+rank*.18f));from=e.p;}}
                if(id==3){skillCooldowns[id]=skillTimers[id]=4.6f*cooldown;for(int j=0;j<1+rank/2;j++){var e=j==0?target:RandomEnemy();if(e!=null)AddZone(e.p,1.35f+rank*.12f,.65f,.1f,Attack*(1.8f+rank*.4f),0);}}
                if(id==5){skillCooldowns[id]=skillTimers[id]=7*cooldown;AddZone(target.p,1.8f+rank*.18f,0,2+rank*.35f,Attack*(.35f+rank*.1f),1);}
            }
            orbitTick-=dt;for(int i=0;i<orbit.Count;i++){bool enabled=Ranks[4]>0&&i<2+Ranks[4];orbit[i].gameObject.SetActive(enabled);if(!enabled)continue;float angle=Elapsed*2.2f+i*Mathf.PI*2/(2+Ranks[4]);Vector2 p=position+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*1.9f;orbit[i].transform.position=p;orbit[i].transform.rotation=Quaternion.Euler(0,0,angle*Mathf.Rad2Deg+90);if(orbitTick<=0)Area(p,.58f,Attack*(.35f+Ranks[4]*.14f),0,0);}
            if(orbitTick<=0)orbitTick=.26f;
        }
        Actor RandomEnemy(){if(enemies.Count==0)return null;for(int i=0;i<12;i++){var e=enemies[UnityEngine.Random.Range(0,enemies.Count)];if(e.live&&e.spawn<=0)return e;}return Nearest(position);}
        static Vector2 Rotate(Vector2 v,float degree){float a=degree*Mathf.Deg2Rad;return new Vector2(v.x*Mathf.Cos(a)-v.y*Mathf.Sin(a),v.x*Mathf.Sin(a)+v.y*Mathf.Cos(a));}
        void Fire(Vector2 p,Vector2 direction,float damage,int fx,float speed,int pierce,float life,bool hostile=false)
        {
            var shot=shots.Find(s=>!s.live);if(shot==null){if(shots.Count>=320)return;shot=new Shot{art=Render("Pooled Projectile","combat_fx_0",.6f,150)};shots.Add(shot);}
            shot.live=true;shot.hostile=hostile;shot.p=p;shot.v=direction*speed;shot.damage=damage;shot.life=life;shot.pierce=pierce;shot.radius=hostile?.22f:.24f;shot.hit.Clear();shot.art.sprite=Art("combat_fx_"+fx);Scale(shot.art,hostile?.47f:.7f);shot.art.color=hostile?new Color(1,.56f,.68f):Color.white;shot.art.transform.position=p;shot.art.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(direction.y,direction.x)*Mathf.Rad2Deg);shot.art.gameObject.SetActive(true);
        }
        void EnemyShot(Vector2 p,Vector2 direction,float speed,float damage){Fire(p,direction,damage,3,speed,1,7,true);}
        void UpdateShots(float dt)
        {
            foreach(var s in shots){if(!s.live)continue;s.life-=dt;s.p+=s.v*dt;if(s.life<=0||Mathf.Abs(s.p.x)>17||Mathf.Abs(s.p.y)>9){s.live=false;s.art.gameObject.SetActive(false);continue;}
                if(s.hostile){if((s.p-position).sqrMagnitude<.4f*.4f){HurtPlayer(s.damage);s.life=0;}}
                else foreach(var e in enemies){if(!e.live||e.spawn>0||s.hit.Contains(e))continue;float radius=e.type==5?1f:.45f;if((e.p-s.p).sqrMagnitude<(radius+s.radius)*(radius+s.radius)){s.hit.Add(e);Hit(e,s.damage);if(--s.pierce<=0){s.life=0;break;}}}
                s.art.transform.position=s.p;
            }
        }
        void Area(Vector2 p,float radius,float damage,float freeze=0,float slow=0)
        {foreach(var e in enemies)if(e.live&&e.spawn<=0&&(e.p-p).sqrMagnitude<radius*radius){Hit(e,damage);e.freeze=Mathf.Max(e.freeze,e.type==5?freeze*.25f:freeze);e.slow=Mathf.Max(e.slow,slow);}}
        void Hit(Actor e,float damage,bool proc=true)
        {
            if(Finished||!e.live)return;bool critical=UnityEngine.Random.value<CriticalChance;if(critical)damage*=CriticalMultiplier;if(e.type==2)damage*=.75f;
            e.hp-=damage;e.flash=.11f;totalDamage+=damage;Floating(e.p,Mathf.RoundToInt(damage).ToString(),critical?new Color(1,.84f,.35f):Color.white,critical?29:22);
            if(proc&&equipment>=0&&equipmentCooldown<=0&&UnityEngine.Random.value<.2f){equipmentCooldown=8;if(equipment==0){Fx(e.p,1,2.6f,.35f,new Color(.78f,.47f,1));Hit(e,Attack*1.5f,false);}else{Fx(e.p,5,1.4f,.3f,Color.cyan);Hit(e,Attack*.8f,false);}}
            if(e.live&&e.hp<=0){e.live=false;e.art.gameObject.SetActive(false);e.shadow.gameObject.SetActive(false);Kills++;runCoins+=e.type==5?200:2;Fx(e.p,5,e.type==5?4:1.2f,.3f,new Color(.63f,.53f,1));Drop(e.p,e.type==5?50:e.type==2?6:3,0);if(e.elite)Drop(e.p,1,2);if(UnityEngine.Random.value<.024f)Drop(e.p,1,1);if(e==boss)Finish(true);}
        }
        void HurtPlayer(float damage)
        {
            if(invulnerable>0||Finished)return;damage*=1-defense;float absorbed=Mathf.Min(shield,damage);shield-=absorbed;damage-=absorbed;HP=Mathf.Max(0,HP-damage);invulnerable=.65f;shake=.55f;Floating(position,"−"+Mathf.CeilToInt(damage),new Color(1,.35f,.46f),30);Play(hitClip);if(HP<=0)Finish(false);
        }
        void AddZone(Vector2 p,float radius,float warning,float duration,float damage,int kind)
        {if(zones.Count>=40)return;var z=new Zone{p=p,radius=radius,warning=warning,remaining=duration,damage=damage,kind=kind,tick=0,art=Render("Telegraphed Area",kind==1?"combat_fx_6":"combat_fx_7",radius*2,-60)};z.art.transform.position=p;zones.Add(z);}
        void UpdateZones(float dt)
        {
            for(int i=zones.Count-1;i>=0;i--){var z=zones[i];if(z.warning>0){z.warning-=dt;z.art.color=new Color(z.kind==0?1:1,z.kind==0?.72f:.25f,.45f,.35f+.25f*Mathf.Sin(Elapsed*16));continue;}z.remaining-=dt;z.tick-=dt;
                if(z.kind==1){z.art.color=new Color(.69f,.48f,1,.7f);z.art.transform.Rotate(0,0,dt*55);foreach(var e in enemies)if(e.live&&e.spawn<=0&&(e.p-z.p).sqrMagnitude<z.radius*z.radius*2.2f)e.p=Vector2.MoveTowards(e.p,z.p,dt*(e.type==5?.2f:1.4f));if(z.tick<=0){z.tick=.4f;Area(z.p,z.radius,z.damage,0,1);}}
                else if(z.tick<=0){z.tick=10;if(z.kind==0){Area(z.p,z.radius,z.damage);Fx(z.p,2,z.radius*2,.45f,new Color(1,.68f,.38f));shake=.16f;}if(z.kind==2){if((position-z.p).sqrMagnitude<z.radius*z.radius)HurtPlayer(z.damage);Fx(z.p,7,z.radius*2,.35f,new Color(1,.22f,.4f));}}
                if(z.remaining<=0){Destroy(z.art.gameObject);zones.RemoveAt(i);}
            }
        }
        void Drop(Vector2 p,int value,int type)
        {
            var l=loot.Find(x=>!x.live);
            if(l==null)
            {
                if(loot.Count>=220){foreach(var existing in loot)if(existing.live&&existing.type==type){existing.value+=value;return;}return;}
                l=new Loot{art=Render("Pooled Loot","icon_crystal",.32f,20),glow=Render("Loot Gold Halo","combat_fx_4",.8f,19)};loot.Add(l);
            }
            l.live=true;l.attracting=false;l.pullSpeed=4;l.p=p;l.value=value;l.type=type;
            l.art.sprite=type==0?ExperienceCrystal():Art(type==1?"icon_heart":"equipment_07");
            Scale(l.art,type==0?.48f:.55f);l.art.color=Color.white;l.art.transform.position=p;l.art.gameObject.SetActive(true);
            l.glow.color=new Color(1,.66f,.12f,.48f);l.glow.transform.position=p;l.glow.gameObject.SetActive(type==0);
        }
        void UpdateLoot(float dt)
        {
            foreach(var l in loot)
            {
                if(!l.live)continue;
                float range=2f*(1+Ranks[9]*.4f);
                if((l.p-position).sqrMagnitude<range*range)l.attracting=true;
                if(l.attracting){l.pullSpeed=Mathf.Min(26,l.pullSpeed+dt*35);l.p=Vector2.MoveTowards(l.p,position,dt*l.pullSpeed);}
                if((l.p-position).sqrMagnitude<.16f)
                {
                    // Retire before a chest attracts the rest of the pool; each crystal is awarded once.
                    l.live=false;l.art.gameObject.SetActive(false);l.glow.gameObject.SetActive(false);
                    if(l.type==0)QueueExperience(l.value*(1+Ranks[9]*.1f));
                    else if(l.type==2){runCoins+=80*l.value;CollectAllXP();HP=Mathf.Min(MaxHP,HP+MaxHP*.15f);Notice("精英宝箱 · 金币 / 治愈 / 星尘汇聚",3);}
                    else{HP=Mathf.Min(MaxHP,HP+MaxHP*.15f*l.value);Floating(position,"治愈",new Color(.44f,1,.72f),27);}
                }
                else
                {
                    l.art.transform.position=(Vector3)l.p+Vector3.up*Mathf.Sin(Elapsed*4+l.p.x)*.065f;
                    l.glow.transform.position=l.p;Scale(l.glow,.7f+Mathf.Sin(Elapsed*3+l.p.x)*.1f);
                }
            }
        }
        Effect Fx(Vector2 p,int sprite,float size,float duration,Color color)
        {
            var f=effects.Find(x=>!x.live);if(f==null){if(effects.Count>=100)return null;f=new Effect{art=Render("Pooled Effect","combat_fx_5",1,170)};effects.Add(f);}f.live=true;f.p=p;f.life=f.total=duration;f.size=size;f.color=color;f.art.sprite=Art("combat_fx_"+sprite);f.art.color=color;f.art.transform.position=p;f.art.transform.rotation=Quaternion.identity;Scale(f.art,size*.7f);f.art.gameObject.SetActive(true);return f;
        }
        void Beam(Vector2 a,Vector2 b,Color color)
        {var f=Fx((a+b)*.5f,0,(b-a).magnitude,.22f,color);if(f!=null)f.art.transform.rotation=Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg);}
        void UpdateEffects(float dt){foreach(var f in effects)if(f.live){f.life-=dt;if(f.life<=0){f.live=false;f.art.gameObject.SetActive(false);}else{f.art.color=new Color(f.color.r,f.color.g,f.color.b,f.color.a*f.life/f.total);Scale(f.art,f.size*(1.25f-f.life/f.total*.5f));}}}
        void Floating(Vector2 p,string text,Color color,int size){if(numbers.Count==0)return;var n=numbers[numberCursor++%numbers.Count];n.life=.65f;n.p=p+Vector2.up*.55f;n.text.text=text;n.text.fontSize=size;n.text.color=color;}
        void UpdateNumbers(float dt){foreach(var n in numbers)if(n.life>0){n.life-=dt;if(n.life<=0){n.text.text="";continue;}n.p+=Vector2.up*dt*.9f;var screen=cam.WorldToScreenPoint(n.p);var parent=(RectTransform)n.text.transform.parent;RectTransformUtility.ScreenPointToLocalPointInRectangle(parent,screen,null,out var p);n.text.rectTransform.anchoredPosition=p-new Vector2(75,25);var c=n.text.color;c.a=Mathf.Min(1,n.life*3);n.text.color=c;}}
        void CollectAllXP(){foreach(var item in loot)if(item.live&&item.type==0)item.attracting=true;Fx(position,4,5,.8f,new Color(1,.8f,.3f));}
        void Notice(string text,float duration=2){Txt("CombatNotice",text);noticeTime=duration;}
        void SettleExperience()
        {
            // XP owns level progression; opening/choosing a reward must never grant levels.
            while(experience>=nextXP)
            {
                experience-=nextXP;Level++;nextXP=10+Level*4;PendingUpgrades++;
            }
        }
        void OpenUpgrade()
        {
            if(!Running||Finished||Paused||Choosing||PendingUpgrades<=0)return;
            Choosing=true;SetActive("CombatUpgrade",true);Txt("UpgradeTitle","星界赐福 · Lv. "+Level+" · 待选 "+PendingUpgrades+" 次");RollChoices();UpdateHUD();Play(levelClip);AnimatePanel("CombatUpgrade");
        }
        public void RollChoices()
        {
            var eligible=new List<int>();for(int i=0;i<12;i++)if(Ranks[i]<5)eligible.Add(i);
            for(int i=0;i<3;i++){int id;if(eligible.Count>0){int at=UnityEngine.Random.Range(0,eligible.Count);id=eligible[at];eligible.RemoveAt(at);}else id=12+i;Choices[i]=id;
                Img("UpgradeIcon"+i).sprite=Art(id<12?"combat_skill_"+id:id==12?"icon_heart":id==13?"icon_coin":"icon_shield");Txt("UpgradeName"+i,id<12?SkillNames[id]:new[]{"生命补给","星界赏金","临时护盾"}[id-12]);Txt("UpgradeTag"+i,id<12?(Ranks[id]==0?"新共鸣 / NEW":Ranks[id]==4?"最终觉醒 / MAX":"共鸣进阶")+"   Lv. "+(Ranks[id]+1):"生存补给 / SUPPLY");Txt("UpgradeDescription"+i,id<12?UpgradeDescription(id,Ranks[id]+1):id==12?"立即回复 35% 最大生命":id==13?"本局获得 120 金币":"获得 35% 最大生命的护盾");}
            Txt("CombatRerollLabel","重掷 · 剩余 "+rerolls);app.Node("CombatReroll").GetComponent<Button>().interactable=rerolls>0;
        }
        public void Choose(int index)
        {
            if(!Running||Finished||!Choosing||PendingUpgrades<=0||index<0||index>2)return;
            PendingUpgrades--;int id=Choices[index];
            if(id<12&&Ranks[id]<5){Ranks[id]++;if(id==7){MaxHP=baseHP*(1+Ranks[7]*.15f);HP=Mathf.Min(MaxHP,HP+baseHP*.15f);}CriticalChance=Mathf.Min(.85f,baseCrit+Ranks[11]*.05f);CriticalMultiplier=baseCritDamage+Ranks[11]*.12f;}
            else if(id==12)HP=Mathf.Min(MaxHP,HP+MaxHP*.35f);else if(id==13)runCoins+=120;else if(id==14){shield+=MaxHP*.35f;shieldTime=12;}
            Choosing=false;SetActive("CombatUpgrade",false);RefreshRanks();Notice(id<12?SkillNames[id]+" · 共鸣已生效":"星光补给已获取");Fx(position,4,3,.55f,Color.cyan);invulnerable=Mathf.Max(invulnerable,1);
        }
        void RefreshRanks(){for(int i=0;i<12;i++){app.Node("RunSkillFrame"+i).GetComponent<Button>().interactable=Ranks[i]>0;Img("RunSkillIcon"+i).color=Ranks[i]>0?Color.white:new Color(.6f,.65f,.8f,.23f);Txt("RunSkillRank"+i,Ranks[i]>0?new string('·',Ranks[i]):"—");}}
        public void Dash()
        {if(!Running||Finished||Paused||Choosing||PendingUpgrades>0||dashCooldown>0)return;dashCooldown=3.5f*(1-Ranks[10]*.07f);dashTime=.2f;dashDirection=lastDirection;invulnerable=.38f;Fx(position,4,1.8f,.35f,new Color(.55f,.78f,1,.6f));}
        public void Ultimate()
        {
            if(!Running||Finished||Paused||Choosing||PendingUpgrades>0||ultimateCooldown>0)return;ultimateCooldown=18*(1-Ranks[10]*.07f);Notice(app.catalog.characters[character].skill+" !",2);Play(ultimateClip);shake=.6f;
            shieldTime=6;if(equipment>0)shield=Mathf.Max(shield,MaxHP*.12f);
            if(character==0){Area(position,40,Attack*6);Fx(position,1,12,.6f,new Color(.85f,.58f,1));}
            if(character==1){HP=Mathf.Min(MaxHP,HP+MaxHP*.35f);Area(position,40,Attack*3.2f);Fx(position,4,15,.7f,Color.cyan);}
            if(character==2){Area(position,40,Attack*4,4,5);Fx(position,4,17,.8f,new Color(.55f,.8f,1));}
            if(character==3){for(int i=0;i<12;i++){var e=RandomEnemy();AddZone(e==null?position+UnityEngine.Random.insideUnitCircle*4:e.p,2,.2f+i*.08f,.1f,Attack*3.5f,0);}}
            if(character==4){for(int i=0;i<24;i++)Fire(position,Rotate(Vector2.right,i*15),Attack*3,0,11,6,2);}
            if(character==5){var e=Nearest(position);AddZone(e==null?position:e.p,4,0,6,Attack*.8f,1);}
            if(character==6){shield=Mathf.Max(shield,MaxHP*.25f);Area(position,7,Attack*5,3,3);Fx(position,1,12,.65f,new Color(.55f,.82f,1));}
            if(character==7){shield=Mathf.Max(shield,MaxHP*.35f);Area(position,8,Attack*5);Fx(position,5,12,.65f,new Color(1,.82f,.4f));}
        }
        public void Command(int id)
        {
            if(!Running)return;
            if(id==20){CloseSkillDetails();return;}
            if(id>=30&&id<42){ShowSkillDetails(id-30);return;}
            if(SkillDetailsOpen)return;
            if(id==0&&!Finished&&!Choosing){Paused=!Paused;retreatConfirm=false;SetActive("CombatPausePanel",Paused);Txt("CombatRetreatLabel","撤离战场");if(Paused){Txt("PauseBody","攻击 "+Attack.ToString("0.0")+" · 暴击 "+(CriticalChance*100).ToString("0")+"% · 减伤 "+(defense*100).ToString("0")+"%\n装备："+(equipment<0?"未装备":app.catalog.equipment[equipment].name)+"\nWASD / 摇杆移动，空格闪避，E 终结技。\n撤离保留击破金币，不会解锁关卡。");AnimatePanel("CombatPausePanel");}}
            if(id==1)Dash();if(id==2)Ultimate();
            if(id==3&&Paused&&!Finished){if(!retreatConfirm){retreatConfirm=true;Txt("CombatRetreatLabel","确认撤离");}else Finish(false);}
            if(id==4&&Finished)Leave();
            if(id==5&&Finished){int cost=10+difficulty*5;if(app.State.energy<cost){Notice("能量不足，请返回关卡补给",3);return;}int nextStage=won&&stage<4?stage+1:stage;Leave();app.State.chapter=chapter;app.State.difficulty=difficulty;app.State.stage=nextStage;Begin();}
            if(id>=10&&id<=12)Choose(id-10);
            if(id==13&&Choosing&&rerolls>0){rerolls--;RollChoices();AnimatePanel("CombatUpgrade");}
        }
        void AnimatePanel(string node){if(!app.State.reducedMotion)StartCoroutine(PanelIn(app.Node(node).gameObject));}
        System.Collections.IEnumerator PanelIn(GameObject panel)
        {var g=panel.GetComponent<CanvasGroup>();if(g==null)g=panel.AddComponent<CanvasGroup>();for(float t=0;t<.22f;t+=Time.unscaledDeltaTime){g.alpha=Mathf.SmoothStep(0,1,t/.22f);yield return null;}g.alpha=1;}
        void UpdateHUD()
        {
            if(!Running)return;UpdateSkillCooldownUI();Txt("CombatName",app.catalog.characters[character].name+"  /  Lv. "+Level);Txt("CombatClock",((int)Elapsed/60).ToString("00")+":"+((int)Elapsed%60).ToString("00"));Txt("CombatWave",app.catalog.chapters[chapter].name+" · "+(bossSpawned?"击败深渊领主":"领主降临倒计时 "+Mathf.Max(0,300-(int)Elapsed)+"s"));
            Bar("CombatHP",370,HP/MaxHP);Txt("CombatHPText",Mathf.CeilToInt(HP)+" / "+Mathf.CeilToInt(MaxHP)+(shield>0?"  盾 "+Mathf.CeilToInt(shield):""));Txt("CombatCounters","击破 "+Kills+"   /   金币 "+runCoins);
            Txt("UltimateLabel",ultimateCooldown>0?Mathf.CeilToInt(ultimateCooldown)+"s":"终结技 [E]");Txt("DashLabel",dashCooldown>0?"闪避\n"+dashCooldown.ToString("0.0")+"s":"闪避 [空格]");
            if(boss!=null&&boss.live)Bar("BossHP",632,boss.hp/boss.max);
        }
        void Finish(bool victory)
        {
            if(Finished)return;Finished=true;ResetFeedback();Paused=false;Choosing=false;PendingUpgrades=0;won=victory;SetActive("CombatPausePanel",false);SetActive("CombatUpgrade",false);SetActive("CombatResult",true);
            int progressId=chapter*3+difficulty;bool first=victory&&stage>=app.State.progress[progressId];
            rewardGold=runCoins+(victory?(first?2000:500):100);rewardCrystals=first?60:0;
            if(victory)app.State.progress[progressId]=Mathf.Max(app.State.progress[progressId],stage+1);
            app.State.coins+=rewardGold;app.State.crystals+=rewardCrystals;app.State.totalRuns++;app.State.totalKills+=Kills;if(victory)app.State.victories++;app.State.bestSurvival=Mathf.Max(app.State.bestSurvival,Elapsed);app.Save();
            string progressText="";
            if(victory)
            {
                string difficultyName=new[]{"普通","困难","噩梦"}[difficulty];
                int cleared=app.State.progress[progressId];
                progressText="\n"+difficultyName+"通关进度 "+cleared+" / 5";
                if(cleared<5)progressText+=" · 下一关 "+(chapter+1)+"-"+(cleared+1);
                else
                {
                    string unlock=difficulty<2?new[]{"困难","噩梦"}[difficulty]+"难度":"";
                    if(difficulty==0&&chapter+1<app.catalog.chapters.Length)unlock+=(unlock.Length>0?" / ":"")+app.catalog.chapters[chapter+1].name;
                    progressText+=unlock.Length>0?"\n已解锁："+unlock:" · 全部通关";
                }
            }
            Txt("CombatRetryLabel",victory&&stage<4?"挑战下一关":"再次出击");
            var resultBody=app.Node("ResultBody").GetComponent<Text>();resultBody.fontSize=23;resultBody.rectTransform.anchoredPosition=new Vector2(655,-300);resultBody.rectTransform.sizeDelta=new Vector2(704,240);
            Txt("ResultTitle",victory?"破晓凯旋":HP<=0?"星火未熄":"整装再出发");Txt("ResultBody",(victory?"深渊领主已击败":"旅程暂歇，成长仍会留下")+"\n存活 "+((int)Elapsed/60).ToString("00")+":"+((int)Elapsed%60).ToString("00")+"  ·  击破 "+Kills+"  ·  Lv. "+Level+"\n总伤害 "+Mathf.RoundToInt(totalDamage).ToString("N0")+"\n金币 +"+rewardGold+(rewardCrystals>0?"  /  星晶 +"+rewardCrystals:"")+progressText);AnimatePanel("CombatResult");Play(levelClip);
        }
        public void Leave()
        {
            if(!Running)return;Running=false;ResetFeedback();Paused=Choosing=false;PendingUpgrades=0;StopAllCoroutines();ClearBattleWorld();if(music!=null)music.Stop();
            if(Finished&&won){app.State.chapter=chapter;app.State.difficulty=difficulty;app.State.stage=Mathf.Min(4,app.State.progress[chapter*3+difficulty]);}
            SetActive("CombatHUD",false);SetActive("TopBar",true);SetActive("ViewportBackdrop",true);SetActive("BottomAccent",true);app.ShowImmediate(3);app.Save();
        }
        void OnApplicationFocus(bool focus){if(!focus&&Running&&!Paused&&!Choosing&&!Finished)Command(0);}
        void OnDestroy(){if(world!=null)Destroy(world.gameObject);if(xpCrystal!=null)Destroy(xpCrystal);if(xpTexture!=null)Destroy(xpTexture);foreach(var clip in new[]{hitClip,levelClip,ultimateClip,musicClip,xpPickupClip,xpArrivalClip})if(clip!=null)Destroy(clip);}
        void SetupSound()
        {
            if(sfx!=null)return;sfx=gameObject.AddComponent<AudioSource>();music=gameObject.AddComponent<AudioSource>();music.loop=true;music.volume=.18f;sfx.volume=.35f;
            hitClip=Tone("Hit",155,.10f);levelClip=Tone("Blessing",660,.32f);ultimateClip=Tone("Resonance",220,.45f);
            const int rate=22050;var data=new float[rate*8];float[] notes={220,261.63f,329.63f,392,440,392,329.63f,261.63f};for(int i=0;i<data.Length;i++){float t=i/(float)rate;float beat=t%1;int note=(int)t;data[i]=(.12f*Mathf.Sin(2*Mathf.PI*notes[note]*t)*Mathf.Exp(-beat*5)+.04f*Mathf.Sin(2*Mathf.PI*110*t)+.035f*Mathf.Sin(2*Mathf.PI*164.81f*t))*Mathf.Min(1,t*10)*Mathf.Min(1,(8-t)*10);}
            musicClip=AudioClip.Create("Original Astral ambient loop",data.Length,1,rate,false);musicClip.SetData(data,0);music.clip=musicClip;
        }
        AudioClip Tone(string title,float frequency,float seconds){int rate=22050;var d=new float[(int)(rate*seconds)];for(int i=0;i<d.Length;i++){float t=i/(float)rate;d[i]=Mathf.Sin(2*Mathf.PI*frequency*t)*Mathf.Exp(-t*12)*.28f;}var clip=AudioClip.Create(title,d.Length,1,rate,false);clip.SetData(d,0);return clip;}
        void Play(AudioClip clip){if(!app.State.muted&&sfx!=null&&clip!=null)sfx.PlayOneShot(clip);}
#if UNITY_EDITOR
        // Editor QA hooks never ship in players. QA restores the user's complete save afterwards.
        public void QAExperience(float value){experience+=value;}
        public void QAClock(float seconds){Elapsed=seconds;}
        public void QAFinish(bool victory){Finish(victory);}
        public void QADamage(float damage){invulnerable=0;HurtPlayer(damage);}
        public void QAHeal(){HP=MaxHP;}
        public void QACooldowns(){ultimateCooldown=dashCooldown=0;}
        public void QAKillEnemies(){foreach(var e in enemies)if(e.live)Hit(e,1000000,false);}
#endif
    }
}
