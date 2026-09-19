using UnityEngine;
using UnityEngine.UI;
namespace AstralUI.Editor
{
    public static partial class AstralUIBuilder
    {
        static RectTransform BuildCombat(Transform p)
        {
            var r=Page(p,"CombatHUD");
            Img(r,"CombatTopGlass","panel_dark",12,10,1576,105,new Color(.6f,.68f,.85f,.95f),true);
            Img(r,"CombatPortraitFrame","card_gold",25,19,87,88,null,true);Img(r,"CombatPortrait","portrait_seiri",32,26,73,74);
            Txt(r,"CombatName","星璃 / Lv. 1",130,18,420,32,25,Color.white,TextAnchor.MiddleLeft,true);
            Img(r,"CombatHPTrack","",131,61,370,14,new Color(.13f,.18f,.29f));Img(r,"CombatHP","",131,61,370,14,new Color(.42f,.94f,.82f));
            Txt(r,"CombatHPText","400 / 400",130,81,371,24,15,new Color(.75f,.91f,.94f),TextAnchor.MiddleRight);
            Txt(r,"CombatClock","00:00",623,16,354,48,42,Color.white,TextAnchor.MiddleCenter,true);
            Txt(r,"CombatWave","艾尔废墟 · 生存至 05:00",547,72,506,25,18,new Color(.77f,.82f,1),TextAnchor.MiddleCenter);
            Txt(r,"CombatCounters","击破 0   /   金币 0",1070,30,376,36,23,Color.white,TextAnchor.MiddleRight);
            Btn(r,"CombatPause","Ⅱ","combat",0,1493,26,66,65,"button_secondary",30);
            Img(r,"CombatXPTrack","",22,115,1556,5,new Color(.18f,.23f,.37f));Img(r,"CombatXP","",22,115,1,5,new Color(.68f,.49f,1));
            var boss=Rect(r,"CombatBoss",464,139,672,59);Img(boss,"BossPlate","panel_dark",0,0,672,59,null,true);Txt(boss,"BossName","深渊领主 · ASTERION",16,4,640,27,19,Color.white,TextAnchor.MiddleCenter);Img(boss,"BossHPTrack","",20,40,632,6,new Color(.19f,.17f,.3f));Img(boss,"BossHP","",20,40,632,6,new Color(.91f,.30f,.49f));boss.gameObject.SetActive(false);
            Txt(r,"CombatNotice","",398,233,804,66,39,Color.white,TextAnchor.MiddleCenter,true);
            Txt(r,"CombatHelp","WASD / 方向键 移动    空格 闪避    E 终结技    ESC 暂停",360,745,881,26,18,new Color(.80f,.86f,1),TextAnchor.MiddleCenter);
            var skillbar=Img(r,"CombatSkillbar","panel_dark",390,664,816,73,new Color(.60f,.68f,.88f,.85f),true);
            for(int i=0;i<12;i++){var slot=Img(skillbar.transform,"RunSkillFrame"+i,"card_purple",10+i*66,5,62,63,null,true);var icon=Img(slot.transform,"RunSkillIcon"+i,"combat_skill_"+i,8,5,46,46);icon.preserveAspect=true;Txt(slot.transform,"RunSkillRank"+i,"—",0,43,62,19,14,Color.white,TextAnchor.MiddleCenter,true);}
            var pad=Img(r,"CombatJoystick","combat_fx_4",63,571,167,167,new Color(.64f,.81f,1,.48f));pad.raycastTarget=true;pad.gameObject.AddComponent<AstralJoystick>();
            Img(pad.transform,"JoystickKnob","combat_fx_6",59,59,49,49,new Color(.80f,.69f,1,.9f));
            Txt(r,"JoystickHint","拖动移动",63,744,167,27,19,new Color(.84f,.9f,1),TextAnchor.MiddleCenter);
            var ult=Btn(r,"CombatUltimate","","combat",2,1375,607,153,133,"card_purple");Img(ult,"UltimateIcon","combat_skill_0",43,8,67,67);Txt(ult,"UltimateLabel","终结技 [E]",8,90,137,28,20,Color.white,TextAnchor.MiddleCenter,true);
            var dash=Btn(r,"CombatDash","","combat",1,1236,654,115,84,"button_secondary");Txt(dash,"DashLabel","闪避 [空格]",5,10,105,63,18,Ink,TextAnchor.MiddleCenter,true);
            var floats=Rect(r,"CombatNumbers",0,0,1600,800);for(int i=0;i<48;i++)Txt(floats,"DamageNumber"+i,"",0,0,150,50,24,Color.white,TextAnchor.MiddleCenter,true);
            // Upgrades are proper editable card objects, separated from the world simulation.
            var up=Rect(r,"CombatUpgrade",0,0,1600,800);Img(up,"UpgradeDim","",0,0,1600,800,new Color(.035f,.045f,.12f,.90f)).raycastTarget=true;
            Txt(up,"UpgradeKicker","S T E L L A R  B L E S S I N G",390,75,820,30,21,new Color(.67f,.74f,1),TextAnchor.MiddleCenter);
            Txt(up,"UpgradeTitle","星界赐福",350,117,900,72,56,Color.white,TextAnchor.MiddleCenter,true);
            Txt(up,"UpgradeSubtitle","等级提升 · 选择一项共鸣，改写你的战斗流派",330,198,940,35,23,new Color(.73f,.80f,.95f),TextAnchor.MiddleCenter);
            for(int i=0;i<3;i++)
            {
                var card=Btn(up,"UpgradeCard"+i,"","combat",10+i,230+i*389,265,363,364,"panel_light");
                Txt(card,"UpgradeTag"+i,"共鸣觉醒",27,24,309,27,20,Muted,TextAnchor.MiddleCenter);
                Img(card,"UpgradeIcon"+i,"combat_skill_"+i,130,71,103,103).preserveAspect=true;
                Txt(card,"UpgradeName"+i,"星陨飞刃",23,191,317,48,32,Ink,TextAnchor.MiddleCenter,true);
                Txt(card,"UpgradeDescription"+i,"",33,248,297,81,22,Muted,TextAnchor.UpperCenter);
            }
            Btn(up,"CombatReroll","重掷 · 剩余 3","combat",13,618,679,364,61,"button_primary",25);up.gameObject.SetActive(false);
            var pause=Rect(r,"CombatPausePanel",0,0,1600,800);Img(pause,"PauseDim","",0,0,1600,800,new Color(.035f,.045f,.12f,.88f)).raycastTarget=true;
            Img(pause,"PausePanel","panel_light",430,170,740,452,null,true);Txt(pause,"PauseTitle","时光暂歇",473,205,654,65,45,Ink,TextAnchor.MiddleCenter,true);Txt(pause,"PauseBody","",488,292,624,165,24,Muted,TextAnchor.MiddleCenter);
            Btn(pause,"CombatResume","继续战斗","combat",0,484,511,296,65,"button_primary",27);Btn(pause,"CombatRetreat","撤离战场","combat",3,814,511,296,65,"button_secondary",27);pause.gameObject.SetActive(false);
            var end=Rect(r,"CombatResult",0,0,1600,800);Img(end,"ResultDim","",0,0,1600,800,new Color(.035f,.045f,.12f,.9f)).raycastTarget=true;
            Img(end,"ResultHero","character_seiri",27,17,531,797);
            Img(end,"ResultPanel","panel_light",567,111,883,576,null,true);
            Txt(end,"ResultKicker","E C H O E S  O F  T H E  S T A R S",614,150,786,36,23,Muted,TextAnchor.MiddleCenter);
            Txt(end,"ResultTitle","破晓凯旋",611,205,792,74,56,Ink,TextAnchor.MiddleCenter,true);
            Txt(end,"ResultBody","",655,313,704,197,29,Muted,TextAnchor.MiddleCenter);
            Btn(end,"CombatReturn","返回关卡","combat",4,643,563,324,70,"button_secondary",29);Btn(end,"CombatRetry","再次出击","combat",5,1010,563,324,70,"button_primary",29);end.gameObject.SetActive(false);
            return r;
        }
    }
}
