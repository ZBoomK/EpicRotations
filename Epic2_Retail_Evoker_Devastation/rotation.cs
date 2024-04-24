using System.Linq;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO; // Used for Licensing
using AimsharpWow.API; //needed to access Aimsharp API
using System.Net;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace AimsharpWow.Modules{
#region SettingClasses
    public class EpicSetting{
        static List<EpicTab> Tabs = new List<EpicTab>();
        static List<EpicTab>[] Minitabs = new List<EpicTab>[15];

        public string Label;
        public string VariableName;
        public int Tab;
        public int Minitab;
        public int Line;

        public virtual string GetCustomFunctionSnippet(){
            return "";
        }

        public static void SetTabName(int id, string name){
            Tabs.Add(new EpicTab(id, name));
            Minitabs[id-1] = new List<EpicTab>();
        }

        public static void SetMinitabName(int tabid, int id, string name){
            Minitabs[tabid-1].Add(new EpicTab(id, name));
        }

        public static string CreateCustomFunction(string mainToggle, string addonName, List<EpicSetting> settings, List<EpicToggle> toggles, string additionalLines){
            string customFunction = "";

            string tabFunctionSnippet = "";
            string minitabFunctionSnippet = "";
            string settingFunctionSnippet = "";

            Tabs.Sort((x, y) => x.ID.CompareTo(y.ID));

            int i = 0;
            foreach(EpicTab t in Tabs){
                tabFunctionSnippet += "\""+t.Label+"\"";
                i++;
                if(i != Tabs.Count())
                    tabFunctionSnippet += ", ";
            }

            for(int j = 0; j < Minitabs.Count(); j++){
                if(Minitabs[j] != null){
                    minitabFunctionSnippet += "EpicSettings.InitMiniTabs("+(j+1);
                    Minitabs[j].Sort((x, y) => x.ID.CompareTo(y.ID));
                    foreach(EpicTab t in Minitabs[j]){
                        minitabFunctionSnippet += ", \""+t.Label+"\"";
                    }
                    minitabFunctionSnippet += ")\n";
                }
            }

            foreach(EpicSetting es in settings){
                settingFunctionSnippet += es.GetCustomFunctionSnippet() + "\n";
            }

            settingFunctionSnippet += "EpicSettings.InitButtonMain(\""+mainToggle+"\", \""+addonName+"\")\n";

            foreach(EpicToggle t in toggles){
                settingFunctionSnippet += t.GetCustomFunctionSnippet() + "\n";
            }

            customFunction += "if GetCVar(\"setupEpicSettingsCVAR\") == nil then RegisterCVar( \"setupEpicSettingsCVAR\", 0 ) end\n" +
                            "if GetCVar(\"setupEpicSettingsCVAR\") == '0' then\n" +
                            "EpicSettings.InitTabs("+tabFunctionSnippet+")\n" +
                            minitabFunctionSnippet +
                            settingFunctionSnippet +
                            additionalLines +
                            "SetCVar(\"setupEpicSettingsCVAR\", 1);\n" +
                            "end\n" +
                            "return 0";

            return customFunction;
        }
    }

    public class EpicTab{
        public int ID;
        public string Label;

        public EpicTab(int id, string label){
            this.ID = id;
            this.Label = label;
        }
    }

    public class EpicLabelSetting : EpicSetting{

        public EpicLabelSetting(int tab, int minitab, int line, string label){
            this.Label = label;
            this.Tab = tab;
            this.Minitab = minitab;
            this.Line = line;
        }

        public override string GetCustomFunctionSnippet(){
            return "EpicSettings.AddLabel("+Tab+", "+Minitab+", "+Line+", \""+Label+"\")";
        }
    }

    public class EpicTextboxSetting : EpicSetting{
        public string Default;

        public EpicTextboxSetting(int tab, int minitab, int line, string variableName, string label, string defaultValue){
            this.Label = label;
            this.VariableName = variableName;
            this.Default = defaultValue;
            this.Tab = tab;
            this.Minitab = minitab;
            this.Line = line;
        }

        public override string GetCustomFunctionSnippet(){
            return "EpicSettings.AddTextbox("+Tab+", "+Minitab+", "+Line+", \""+VariableName+"\", \""+Label+"\", \""+Default+"\")";
        }
    }

    public class EpicSliderSetting : EpicSetting{
        public int Min;
        public int Max;
        public int Default;

        public EpicSliderSetting(int tab, int minitab, int line, string variableName, string label, int min, int max, int defaultValue){
            this.Label = label;
            this.VariableName = variableName;
            this.Min = min;
            this.Max = max;
            this.Default = defaultValue;
            this.Tab = tab;
            this.Minitab = minitab;
            this.Line = line;
        }

        public override string GetCustomFunctionSnippet(){
            return "EpicSettings.AddSlider("+Tab+", "+Minitab+", "+Line+", \""+VariableName+"\", \""+Label+"\", "+Min+", "+Max+", "+Default+")";
        }
    }

    public class EpicCheckboxSetting : EpicSetting{
        bool Default;

        public EpicCheckboxSetting(int tab, int minitab, int line, string variableName, string label, bool defaultValue){
            this.Label = label;
            this.VariableName = variableName;
            this.Default = defaultValue;
            this.Tab = tab;
            this.Minitab = minitab;
            this.Line = line;
        }

        public override string GetCustomFunctionSnippet(){
            return "EpicSettings.AddCheckbox("+Tab+", "+Minitab+", "+Line+", \""+VariableName+"\", \""+Label+"\", "+Default.ToString().ToLower()+")";
        }
    }

    public class EpicDropdownSetting : EpicSetting{
        List<string> Options;
        string Default;
        
        public EpicDropdownSetting(int tab, int minitab, int line, string variableName, string label, List<string> options, string defaultValue){
            this.Label = label;
            this.VariableName = variableName;
            this.Options = options;
            this.Default = defaultValue;
            this.Tab = tab;
            this.Minitab = minitab;
            this.Line = line;
        }

        public override string GetCustomFunctionSnippet(){
            string options = "";
            foreach(string s in Options){
                options += ", \""+ s +"\"";
            }
            return "EpicSettings.AddDropdown("+Tab+", "+Minitab+", "+Line+", \""+VariableName+"\", \""+Label+"\", \""+Default+"\""+options+")";
        }
    }

    public class EpicGroupDropdownSetting : EpicSetting{
        bool IncludeHealers;
        bool IncludeDamagers;
        bool IncludeTanks;
        bool IncludePlayer;

        public EpicGroupDropdownSetting(int tab, int minitab, int line, string variableName, string label, bool includeHealers, bool includeDamagers, bool includeTanks, bool includePlayer){
            this.Label = label;
            this.VariableName = variableName;
            this.Tab = tab;
            this.Minitab = minitab;
            this.Line = line;
            this.IncludeHealers = includeHealers;
            this.IncludeDamagers = includeDamagers;
            this.IncludeTanks = includeTanks;
            this.IncludePlayer = includePlayer;
        }

        public override string GetCustomFunctionSnippet(){
            return "EpicSettings.AddGroupDropdown("+Tab+", "+Minitab+", "+Line+", \""+VariableName+"\", \""+Label+"\", "+IncludeHealers.ToString().ToLower()+", "+IncludeDamagers.ToString().ToLower()+", "+IncludeTanks.ToString().ToLower()+", "+IncludePlayer.ToString().ToLower()+")";
        }
    }

    public class EpicToggle{
        public bool Default;
        public string Label;
        public string VariableName;
        public string Explanation;

        public EpicToggle(string variableName, string label, bool defaultValue, string explanation){
            this.Default = defaultValue;
            this.Label = label;
            this.VariableName = variableName;
            this.Explanation = explanation;
        }

        public string GetCustomFunctionSnippet(){
            return "EpicSettings.InitToggle(\""+Label+"\", \""+VariableName+"\", "+Default.ToString().ToLower()+", \""+Explanation+"\")\n";
        }
    }


#endregion

    public class Blank : Rotation
    {
        #region Names
        public static string Language = "English";
        private static string Healthstone_ItemName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Gesundheitsstein";
                case "Español":
                    return "Piedra de salud";
                case "Français":
                    return "Pierre de soins";
                case "Italiano":
                    return "Pietra della Salute";
                case "Português Brasileiro":
                    return "Pedra de Vida";
                case "Русский":
                    return "Камень здоровья";
                case "한국어":
                    return "생명석";
                case "简体中文":
                    return "治疗石";
                default:
                    return "Healthstone";
            }
        }
        private static string TailSwipe_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Schwanzfeger";
                case "Español":
                    return "Flagelo de cola";
                case "Français":
                    return "Claque caudale";
                case "Italiano":
                    return "Spazzata di Coda";
                case "Português Brasileiro":
                    return "Revés com a Cauda";
                case "Русский":
                    return "Удар хвостом";
                case "한국어":
                    return "꼬리 휘둘러치기";
                case "简体中文":
                    return "扫尾";
                default:
                    return "Tail Swipe";
            }
        }
        private static string WingBuffet_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Schwanzfeger";
                case "Español":
                    return "Flagelo de cola";
                case "Français":
                    return "Claque caudale";
                case "Italiano":
                    return "Spazzata di Coda";
                case "Português Brasileiro":
                    return "Revés com a Cauda";
                case "Русский":
                    return "Удар хвостом";
                case "한국어":
                    return "꼬리 휘둘러치기";
                case "简体中文":
                    return "扫尾";
                default:
                    return "Tail Swipe";
            }
        }
        private static string AzureStrike_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Azurstoß";
                case "Español":
                    return "Ataque azur";
                case "Français":
                    return "Frappe d’azur";
                case "Italiano":
                    return "Assalto Azzurro";
                case "Português Brasileiro":
                    return "Ataque Lazúli";
                case "Русский":
                    return "Лазурный удар";
                case "한국어":
                    return "하늘빛 일격";
                case "简体中文":
                    return "碧蓝打击";
                default:
                    return "Azure Strike";
            }
        }
        private static string BlessingoftheBronze_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Segen der Bronzenen";
                case "Español":
                    return "Bendición de bronce";
                case "Français":
                    return "Bénédiction du bronze";
                case "Italiano":
                    return "Benedizione del Bronzo";
                case "Português Brasileiro":
                    return "Bênção do Bronze";
                case "Русский":
                    return "Дар бронзовых драконов";
                case "한국어":
                    return "청동용군단의 축복";
                case "简体中文":
                    return "青铜龙的祝福";
                default:
                    return "Blessing of the Bronze";
            }
        }
        private static string DeepBreath_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Tiefer Atem";
                case "Español":
                    return "Aliento profundo";
                case "Français":
                    return "Souffle profond";
                case "Italiano":
                    return "Alito del Drago";
                case "Português Brasileiro":
                    return "Respiração Profunda";
                case "Русский":
                    return "Глубокий вдох";
                case "한국어":
                    return "깊은 숨결";
                case "简体中文":
                    return "深呼吸";
                default:
                    return "Deep Breath";
            }
        }
        private static string Disintegrate_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Desintegration";
                case "Español":
                    return "Desintegrar";
                case "Français":
                    return "Désintégration";
                case "Italiano":
                    return "Disintegrazione";
                case "Português Brasileiro":
                    return "Desintegrar";
                case "Русский":
                    return "Дезинтеграция";
                case "한국어":
                    return "파열";
                case "简体中文":
                    return "裂解";
                default:
                    return "Disintegrate";
            }
        }
        private static string EmeraldBlossom_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Smaragdblüte";
                case "Español":
                    return "Flor esmeralda";
                case "Français":
                    return "Arbre d’émeraude";
                case "Italiano":
                    return "Bocciolo di Smeraldo";
                case "Português Brasileiro":
                    return "Flor de Esmeralda";
                case "Русский":
                    return "Изумрудный цветок";
                case "한국어":
                    return "에메랄드 꽃";
                case "简体中文":
                    return "翡翠之花";
                default:
                    return "Emerald Blossom";
            }
        }
        private static string FireBreath_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Feueratem";
                case "Español":
                    return "Aliento de Fuego";
                case "Français":
                    return "Souffle de feu";
                case "Italiano":
                    return "Soffio di Fuoco";
                case "Português Brasileiro":
                    return "Sopro de Fogo";
                case "Русский":
                    return "Огненное дыхание";
                case "한국어":
                    return "불의 숨결";
                case "简体中文":
                    return "火焰吐息";
                default:
                    return "Fire Breath";
            }
        }
        private static string LivingFlame_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Lebende Flamme";
                case "Español":
                    return "Llama viva";
                case "Français":
                    return "Flamme vivante";
                case "Italiano":
                    return "Fiamma Vivente";
                case "Português Brasileiro":
                    return "Chama Viva";
                case "Русский":
                    return "Живой жар";
                case "한국어":
                    return "살아있는 불꽃";
                case "简体中文":
                    return "活化烈焰";
                default:
                    return "Living Flame";
            }
        }
        private static string OppressingRoar_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Tyrannisierendes Brüllen";
                case "Español":
                    return "Rugido opresor";
                case "Français":
                    return "Rugissement oppressant";
                case "Italiano":
                    return "Ruggito Opprimente";
                case "Português Brasileiro":
                    return "Rugido Opressivo";
                case "Русский":
                    return "Угнетающий рык";
                case "한국어":
                    return "탄압의 포효";
                case "简体中文":
                    return "压迫怒吼";
                default:
                    return "Oppressing Roar";
            }
        }
        private static string Expunge_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Entgiften";
                case "Español":
                    return "Expurgar";
                case "Français":
                    return "Éliminer";
                case "Italiano":
                    return "Espulsione";
                case "Português Brasileiro":
                    return "Expungir";
                case "Русский":
                    return "Нейтрализация";
                case "한국어":
                    return "말소";
                case "简体中文":
                    return "净除";
                default:
                    return "Expunge";
            }
        }
        private static string Sleepwalk_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Schlafwandeln";
                case "Español":
                    return "Sonambulismo";
                case "Français":
                    return "Somnambulisme";
                case "Italiano":
                    return "Sonnambulismo";
                case "Português Brasileiro":
                    return "Sonambulismo";
                case "Русский":
                    return "Лунатизм";
                case "한국어":
                    return "몽유병";
                case "简体中文":
                    return "梦游";
                default:
                    return "Sleep Walk";
            }
        }
        private static string AncientFlame_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Uralte Flamme";
                case "Español":
                    return "Llama ancestral";
                case "Français":
                    return "Flamme antique";
                case "Italiano":
                    return "Fiamma Antica";
                case "Português Brasileiro":
                    return "Chama Antiga";
                case "Русский":
                    return "Древнее пламя";
                case "한국어":
                    return "고대 불꽃";
                case "简体中文":
                    return "上古之火";
                default:
                    return "Ancient Flame";
            }
        }
        private static string BlastFurnace_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Schmelzofen";
                case "Español":
                    return "Horno de fundición";
                case "Français":
                    return "Haut-fourneau";
                case "Italiano":
                    return "Altoforno";
                case "Português Brasileiro":
                    return "Fornalha Explosiva";
                case "Русский":
                    return "Горнило";
                case "한국어":
                    return "격노의 가열로";
                case "简体中文":
                    return "爆裂熔炉";
                default:
                    return "Blast Furnace";
            }
        }
        private static string LeapingFlames_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Springende Flammen";
                case "Español":
                    return "Llamas saltarinas";
                case "Français":
                    return "Flammes bondissantes";
                case "Italiano":
                    return "Fiamme Imprevedibili";
                case "Português Brasileiro":
                    return "Chamas Saltantes";
                case "Русский":
                    return "Прыгучее пламя";
                case "한국어":
                    return "화염 도약";
                case "简体中文":
                    return "飞扑烈焰";
                default:
                    return "Leaping Flames";
            }
        }
        private static string ObsidianScales_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Obsidianschuppen";
                case "Español":
                    return "Escamas obsidiana";
                case "Français":
                    return "Écailles d’obsidienne";
                case "Italiano":
                    return "Scaglie d'Ossidiana";
                case "Português Brasileiro":
                    return "Escamas de Obsidiana";
                case "Русский":
                    return "Обсидиановая чешуя";
                case "한국어":
                    return "흑요석 비늘";
                case "简体中文":
                    return "黑曜鳞片";
                default:
                    return "Obsidian Scales";
            }
        }
        private static string ScarletAdaptation_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Scharlachrote Anpassung";
                case "Español":
                    return "Adaptación escarlata";
                case "Français":
                    return "Adaptation écarlate";
                case "Italiano":
                    return "Adattamento Scarlatto";
                case "Português Brasileiro":
                    return "Adaptação Escarlate";
                case "Русский":
                    return "Алая адаптация";
                case "한국어":
                    return "진홍빛 적응";
                case "简体中文":
                    return "绯红适性";
                default:
                    return "Scarlet Adaptation";
            }
        }
        private static string SourceofMagic_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Quell der Magie";
                case "Español":
                    return "Fuente de magia";
                case "Français":
                    return "Source de magie";
                case "Italiano":
                    return "Sorgente della Magia";
                case "Português Brasileiro":
                    return "Fonte de Magia";
                case "Русский":
                    return "Магический источник";
                case "한국어":
                    return "마법의 원천";
                case "简体中文":
                    return "魔力之源";
                default:
                    return "Source of Magic";
            }
        }
        private static string TiptheScales_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Zeitdruck";
                case "Español":
                    return "Inclinar la balanza";
                case "Français":
                    return "Retour de bâton";
                case "Italiano":
                    return "Ago della Bilancia";
                case "Português Brasileiro":
                    return "Jogo Virado";
                case "Русский":
                    return "Смещение равновесия";
                case "한국어":
                    return "전세역전";
                case "简体中文":
                    return "扭转天平";
                default:
                    return "Tip the Scales";
            }
        }
        private static string Unravel_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Zunichte machen";
                case "Español":
                    return "Deshacer";
                case "Français":
                    return "Fragilisation magique";
                case "Italiano":
                    return "Disvelamento";
                case "Português Brasileiro":
                    return "Desvelar";
                case "Русский":
                    return "Разрушение магии";
                case "한국어":
                    return "해체";
                case "简体中文":
                    return "拆解";
                default:
                    return "Unravel";
            }
        }
        private static string VerdantEmbrace_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Tiefgrüne Umarmung";
                case "Español":
                    return "Abrazo verde";
                case "Français":
                    return "Étreinte verdoyante";
                case "Italiano":
                    return "Abbraccio Lussureggiante";
                case "Português Brasileiro":
                    return "Abraço Verdejante";
                case "Русский":
                    return "Живительные объятия";
                case "한국어":
                    return "신록의 품";
                case "简体中文":
                    return "青翠之拥";
                default:
                    return "Verdant Embrace";
            }
        }
        private static string CauterizingFlame_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Erneuernde Flammen";
                case "Español":
                    return "Llamarada de renovación";
                case "Français":
                    return "Brasier de rénovation";
                case "Italiano":
                    return "Fiammata Curativa";
                case "Português Brasileiro":
                    return "Labareda Renovadora";
                case "Русский":
                    return "Обновляющее пламя";
                case "한국어":
                    return "소생의 불길";
                case "简体中文":
                    return "新生光焰";
                default:
                    return "Renewing Blaze";
            }
        }
        private static string Quell_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Unterdrücken";
                case "Español":
                    return "Sofocar";
                case "Français":
                    return "Apaisement";
                case "Italiano":
                    return "Sedazione";
                case "Português Brasileiro":
                    return "Supressão";
                case "Русский":
                    return "Подавление";
                case "한국어":
                    return "진압";
                case "简体中文":
                    return "镇压";
                default:
                    return "Quell";
            }
        }
        private static string RefreshingHealing_PotionName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Erfrischender Heiltrank";
                case "Español":
                    return "Poción de sanación refrescante";
                case "Français":
                    return "Potion de soins rafraîchissante";
                case "Italiano":
                    return "Pozione di Cura Rinfrescante";
                case "Português Brasileiro":
                    return "Poção de Cura Revigorante";
                case "Русский":
                    return "Освежающее лечебное зелье";
                case "한국어":
                    return "원기회복의 치유 물약";
                case "简体中文":
                    return "振奋治疗药水";
                default:
                    return "Refreshing Healing Potion";
            }
        }
        private static string FleetingUltimatePower_PotionName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Flüchtiger Elementartrank der ultimativen Macht";
                case "Español":
                    return "Poción elemental fugaz de poder definitivo";
                case "Français":
                    return "Potion élémentaire fugace de puissance ultime";
                case "Italiano":
                    return "Pozione Elementale Sfuggente della Potenza Suprema";
                case "Português Brasileiro":
                    return "Poção Elemental Fugaz do Poder Extremo";
                case "Русский":
                    return "Быстродействующее зелье великой мощи стихий";
                case "한국어":
                    return "덧없는 궁극의 힘의 정기 물약";
                case "简体中文":
                    return "飞逝元素究极强能药水";
                default:
                    return "Fleeting Elemental Potion of Ultimate Power";
            }
        }
        private static string FleetingPower_PotionName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Flüchtiger Elementartrank der Macht";
                case "Español":
                    return "Poción elemental de poder fugaz";
                case "Français":
                    return "Potion élémentaire fugace de puissance";
                case "Italiano":
                    return "Pozione Elementale Sfuggente del Potere";
                case "Português Brasileiro":
                    return "Poção Elemental Fugaz do Poder";
                case "Русский":
                    return "Быстродействующее зелье мощи стихий";
                case "한국어":
                    return "덧없는 힘의 정기 물약";
                case "简体中文":
                    return "飞逝元素强能药水";
                default:
                    return "Fleeting Elemental Potion of Power";
            }
        }
        private static string UltimatePower_PotionName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Elementartrank der ultimativen Macht";
                case "Español":
                    return "Poción elemental de poder definitivo";
                case "Français":
                    return "Potion élémentaire de puissance ultime";
                case "Italiano":
                    return "Pozione Elementale della Potenza Suprema";
                case "Português Brasileiro":
                    return "Poção Elemental do Poder Extremo";
                case "Русский":
                    return "Зелье великой мощи стихий";
                case "한국어":
                    return "궁극의 힘의 정기 물약";
                case "简体中文":
                    return "元素究极强能药水";
                default:
                    return "Elemental Potion of Ultimate Power";
            }
        }
        private static string Power_PotionName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Elementartrank der Macht";
                case "Español":
                    return "Poción elemental de poder";
                case "Français":
                    return "Potion élémentaire de puissance";
                case "Italiano":
                    return "Pozione Elementale del Potere";
                case "Português Brasileiro":
                    return "Poção Elemental de Poder";
                case "Русский":
                    return "Зелье мощи стихий";
                case "한국어":
                    return "힘의 정기 물약";
                case "简体中文":
                    return "元素强能药水";
                default:
                    return "Elemental Potion of Power";
            }
        }
        private static string ShockingDisclosure_PotionName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Trank der schockierenden Offenbarung";
                case "Español":
                    return "Poción de revelación fulminante";
                case "Français":
                    return "Potion de révélation choquante";
                case "Italiano":
                    return "Pozione della Rivelazione Folgorante";
                case "Português Brasileiro":
                    return "Poção da Revelação Chocante";
                case "Русский":
                    return "Зелье шокирующего разоблачения";
                case "한국어":
                    return "충격적인 폭로의 물약";
                case "简体中文":
                    return "震击揭示药水";
                default:
                    return "Potion of Shocking Disclosure";
            }
        }
        private static string RenewingBlaze_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Erneuernde Flammen";
                case "Español":
                    return "Llamarada de renovación";
                case "Français":
                    return "Brasier de rénovation";
                case "Italiano":
                    return "Fiammata Curativa";
                case "Português Brasileiro":
                    return "Labareda Renovadora";
                case "Русский":
                    return "Обновляющее пламя";
                case "한국어":
                    return "소생의 불길";
                case "简体中文":
                    return "新生光焰";
                default:
                    return "Renewing Blaze";
            }
        }
        private static string Dragonrage_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Drachenwut";
                case "Español":
                    return "Ira de dragón";
                case "Français":
                    return "Rage draconique";
                case "Italiano":
                    return "Rabbia del Drago";
                case "Português Brasileiro":
                    return "Raiva Dragônica";
                case "Русский":
                    return "Ярость дракона";
                case "한국어":
                    return "용의 분노";
                case "简体中文":
                    return "狂龙之怒";
                default:
                    return "Dragonrage";
            }
        }
        private static string ShatteringStar_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Zerschmetternder Stern";
                case "Español":
                    return "Estrella devastadora";
                case "Français":
                    return "Étoile fracassante";
                case "Italiano":
                    return "Stella Frantumante";
                case "Português Brasileiro":
                    return "Estrela Estilhaçante";
                case "Русский":
                    return "Сокрушающая звезда";
                case "한국어":
                    return "산산이 부서지는 별";
                case "简体中文":
                    return "碎裂星辰";
                default:
                    return "Shattering Star";
            }
        }
        private static string EternitySurge_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Ewigkeitswoge";
                case "Español":
                    return "Oleada de eternidad";
                case "Français":
                    return "Afflux d’éternité";
                case "Italiano":
                    return "Impeto dell'Eternità";
                case "Português Brasileiro":
                    return "Surto da Eternidade";
                case "Русский":
                    return "Всплеск вечности";
                case "한국어":
                    return "영원의 쇄도";
                case "简体中文":
                    return "永恒之涌";
                default:
                    return "Eternity Surge";
            }
        }
        private static string Hover_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Schweben";
                case "Español":
                    return "Flotar";
                case "Français":
                    return "Survoler";
                case "Italiano":
                    return "Volo Sospeso";
                case "Português Brasileiro":
                    return "Pairar";
                case "Русский":
                    return "Бреющий полет";
                case "한국어":
                    return "부양";
                case "简体中文":
                    return "悬空";
                default:
                    return "Hover";
            }
        }
        private static string Pyre_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Scheiterhaufen";
                case "Español":
                    return "Pira";
                case "Français":
                    return "Bûcher";
                case "Italiano":
                    return "Pira";
                case "Português Brasileiro":
                    return "Pira";
                case "Русский":
                    return "Погребальный костер";
                case "한국어":
                    return "기염";
                case "简体中文":
                    return "葬火";
                default:
                    return "Pyre";
            }
        }
        private static string DreamwalkersHealing_PotionName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Heiltrank des Traumwandlers";
                case "Español":
                    return "Poción de sanación de la Caminasueños";
                case "Français":
                    return "Potion de soins de Marcherêve";
                case "Italiano":
                    return "Pozione di Cura del Vagasogni";
                case "Português Brasileiro":
                    return "Poção de Cura do Andassonho";
                case "Русский":
                    return "Лечебное зелье сновидца";
                case "한국어":
                    return "꿈나그네의 치유 물약";
                case "简体中文":
                    return "梦行者治疗药水";
                default:
                    return "Dreamwalker's Healing Potion";
            }
        }

        #endregion

        //--------------------------------------------------------------------
        List<EpicToggle> EpicToggles = new List<EpicToggle>();
        List<EpicSetting> EpicSettings = new List<EpicSetting>();

        List<string> MouseoverQueues;
        List<string> CursorQueues;
        List<string> PlayerQueues;
        List<string> FocusQueues;
        List<string> TargetQueues;

        List<string> BuffList;

         Dictionary<int, string> SpellCasts = new Dictionary<int, string>();
         Dictionary<int, string> MacroCasts = new Dictionary<int, string>();
         Dictionary<int, string> PlayerCasts = new Dictionary<int, string>();
         Dictionary<int, string> FocusCasts = new Dictionary<int, string>();
         Dictionary<int, string> MouseoverCasts = new Dictionary<int, string>();
         Dictionary<int, string> CursorCasts = new Dictionary<int, string>();

        Dictionary<int, string> Queues = new Dictionary<int, string>();


        bool authorized = false;    
        private string URL = "http://185.163.127.236:8000/df/check/";
        private string URL_Trial = "http://185.163.127.236:8000/df/checkTrial/";
        private string type= "dfwindwalker";
        private string strKey ="<RSAKeyValue><Modulus>ln/dSJU0ouoixovGHWHqZx+Eti3tfa0B+F7R5wylhEKYFJTvoo+Sc1M5StZxqepPEKLbn8R+4sHK774eUt2pLGPOF15bDeXMSyyRCqJUy3Zar3+hq879hvIUzzSX7Fsnog53GGyOu/lTr+XqBJC5FqTpJpkgm1QCS/C3aXc6HTE=</Modulus><Exponent>AQAB</Exponent><P>vuEzOU7ZQEUrK1bzwLRDLx0AbvAev+z3q/ffbBgL9p43P8OU6//TsRKEQGSrbrx4GWhMjAR9/1TpuwV8tyCyrQ==</P><Q>ydf9Y0D8Q5+MoSQSMWfyMQLrvKqySFzIB/FmTQ1AowfpZc/QgNuDDtjkEWjPK6vCsmKA/OxSRUyuUUOPNBzpFQ==</Q><DP>aEcpL8amoxjmg4/GLGGOTn++i9y8P8eaaqVItonQh1NaBYi4o9En+hWOkIsuqJln1yGGp/uQRdxCsDxILNc9JQ==</DP><DQ>h7tWavNdcIAPSqF+FnlHFYxYSFQldaHm5eiAmdoKmFeOrWd1V+HFnStfGxH3Fu/3CoxRH0QwAugQ5RbgavPyDQ==</DQ><InverseQ>bvayq/hZGHZF1xXMhjEZbjuC296l+CiRJjI2V01YhCa+Rqkm1njROpgN82ip21V8HjGHktU2h5kenUExrfZ6Xg==</InverseQ><D>Ga5nhQV69irkdMNwst5cyKya8Zh8PHQbkDWj9VyV2PAhMe/pRXDg8X972Q6nVjKWu9TCi+yUp16g4dCsLYFIJvk8A5WBt1UTlnUwxKuKeGgCLocl5Uq/bkooVT1ExciYMAxFQKv8AZOufPpj9VHcIevWjyWhNjIhUwkxoRJsa+0=</D></RSAKeyValue>";

        private string Decrypt(string strEntryText,string strPrivateKey)
        {            
            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            rsa.FromXmlString(strPrivateKey);
            string newStr = strEntryText.Replace("\"", "");
            byte[] byteEntry = Convert.FromBase64String(newStr);
            byte[] byteText = rsa.Decrypt(byteEntry, false);
            return Encoding.UTF8.GetString(byteText);
        }

        public void Check(){
            WebClient client = new WebClient();
            Stream buf = client.OpenRead(URL+Aimsharp.GetAimsharpID()+"/"+Aimsharp.GetHWID()+"/"+type);
            StreamReader sr = new StreamReader(buf);
            string s = sr.ReadToEnd();
            if( s != "false"){
                string info = (string)Aimsharp.GetAimsharpID()+(string)Aimsharp.GetHWID()+ this.type;
                var result = Decrypt(s, strKey);
                string date = result.Substring(result.Length-10);
                bool test = result.Substring(0,result.Length-10).Equals(info);
                if(test){
                    Aimsharp.PrintMessage("You have a valid License, Thank you for your support .", Color.Blue);
                    Aimsharp.PrintMessage("License Valid Until: " + date, Color.Blue);
                    authorized = true;
                    return;
                }
            }
            else{// Test Trial access
                buf = client.OpenRead(URL_Trial+Aimsharp.GetAimsharpID()+"/"+Aimsharp.GetHWID()+"/"+type);
                sr = new StreamReader(buf);
                s = sr.ReadToEnd();
                if( s != "false"){
                    string info = (string)Aimsharp.GetAimsharpID()+(string)Aimsharp.GetHWID()+ this.type;
                    var result = Decrypt(s, strKey);
                    string date = result.Substring(result.Length-10);
                    bool test = result.Substring(0,result.Length-10).Equals(info);
                    if(test){
                        Aimsharp.PrintMessage("You have a valid License, Thank you for your support .", Color.Blue);
                        Aimsharp.PrintMessage("License Valid Until: " + date, Color.Blue);
                        authorized = true;
                        return;
                    }
                }
            }
            // if we go here it is unauthorized
            authorized = false;
            Aimsharp.PrintMessage("You Don't have the right to use this masterpiece.", Color.Red);
            Aimsharp.PrintMessage("No Money! No Honey :D Contact Gojira / XaMaX / BoomK / XKaneto for more info.", Color.Red);
        }

        public override void LoadSettings(){
            Settings.Add(new Setting("Game Client Language", new List<string>(){"English", "Deutsch", "Español", "Français", "Italiano", "Português Brasileiro", "Русский", "한국어", "简体中文"}, "English"));
            Settings.Add(new Setting("Latency: ", 0, 1000, 20));
            Settings.Add(new Setting("Quick Delay: ", 50, 1000, 50));
            Settings.Add(new Setting("Slow Delay: ", 50, 1000, 100));
        }

        public override void Initialize(){
            Check();
            if(authorized){
            }
            Language = GetDropDown("Game Client Language");

            Aimsharp.PrintMessage("Epic Rotations Devastation Evoker", Color.Purple);
            Aimsharp.PrintMessage("X v10.2.01  (Dragonflight)", Color.Purple);
            Aimsharp.PrintMessage("By BoomK", Color.Purple);
            Aimsharp.PrintMessage(" ");
            
            Aimsharp.PrintMessage("---------------------------------", Color.Blue);
            Aimsharp.PrintMessage("For list of commands Join Discord", Color.Blue);
            Aimsharp.PrintMessage("---------------------------------", Color.Blue);
            Aimsharp.PrintMessage(" ");
            Aimsharp.PrintMessage("https://discord.gg/SAZmqEYXwc", Color.Purple);
            Aimsharp.PrintMessage(" ");
            Aimsharp.PrintMessage("---------------------------------", Color.Blue);
            Aimsharp.PrintMessage("For list of commands Join Discord", Color.Blue);
            Aimsharp.PrintMessage("---------------------------------", Color.Blue);
            Aimsharp.PrintMessage(" ");
            Aimsharp.PrintMessage("---------------------------------", Color.Blue);

            Aimsharp.Latency = GetSlider("Latency: ");
            Aimsharp.QuickDelay = GetSlider("Quick Delay: ");
            Aimsharp.SlowDelay  = GetSlider("Slow Delay: ");

            //Spells
            BuffList = new List<string>(){"Power Infusion", "Symbol of Hope"};

            //Pair ids with casts
            SpellCasts.Add(1, TailSwipe_SpellName());
            SpellCasts.Add(2, WingBuffet_SpellName());
            SpellCasts.Add(3, AzureStrike_SpellName());
            SpellCasts.Add(4, BlessingoftheBronze_SpellName());
            SpellCasts.Add(5, DeepBreath_SpellName());
            SpellCasts.Add(6, Disintegrate_SpellName());
            SpellCasts.Add(7, EmeraldBlossom_SpellName());
            SpellCasts.Add(96, "Fire Breath");
            SpellCasts.Add(8, LivingFlame_SpellName());
            SpellCasts.Add(102, OppressingRoar_SpellName());
            SpellCasts.Add(103, Expunge_SpellName());
            SpellCasts.Add(104, Sleepwalk_SpellName());
            SpellCasts.Add(9, AncientFlame_SpellName());
            SpellCasts.Add(10, BlastFurnace_SpellName());
            SpellCasts.Add(11, LeapingFlames_SpellName());
            SpellCasts.Add(12, ObsidianScales_SpellName());
            SpellCasts.Add(13, ScarletAdaptation_SpellName());
            SpellCasts.Add(14, SourceofMagic_SpellName());
            SpellCasts.Add(15, TiptheScales_SpellName());
            SpellCasts.Add(16, Unravel_SpellName());
            SpellCasts.Add(17, VerdantEmbrace_SpellName());
            SpellCasts.Add(30, Quell_SpellName());
            SpellCasts.Add(105, RenewingBlaze_SpellName());
            SpellCasts.Add(58, Dragonrage_SpellName());
            SpellCasts.Add(73, ShatteringStar_SpellName());
            SpellCasts.Add(97, EternitySurge_SpellName());
            SpellCasts.Add(107, CauterizingFlame_SpellName());
            SpellCasts.Add(69, Pyre_SpellName());
            SpellCasts.Add(106, Hover_SpellName());






            MacroCasts.Add(1, "TopTrinket");
            MacroCasts.Add(2, "TopTrinketPlayer");
            MacroCasts.Add(3, "TopTrinketCursor");
            MacroCasts.Add(4, "TopTrinketFocus");
            MacroCasts.Add(5, "BottomTrinket");
            MacroCasts.Add(6, "BottomTrinketPlayer");
            MacroCasts.Add(7, "BottomTrinketCursor");
            MacroCasts.Add(8, "BottomTrinketFocus");
            MacroCasts.Add(37, "Healthstone");
            MacroCasts.Add(100, "Next");
            MacroCasts.Add(500, "DPSPotion");
            MacroCasts.Add(33, "RefreshingHealingPotion");

            MacroCasts.Add(38, "MO_"+AzureStrike_SpellName());
            MacroCasts.Add(39, "C_"+DeepBreath_SpellName());
            MacroCasts.Add(40, "F_"+CauterizingFlame_SpellName());
            MacroCasts.Add(41, "F_"+EmeraldBlossom_SpellName());
            MacroCasts.Add(29, "P_"+VerdantEmbrace_SpellName());
            MacroCasts.Add(31, "P_"+EmeraldBlossom_SpellName());
            MacroCasts.Add(43, "F_"+LivingFlame_SpellName());
            MacroCasts.Add(45, "MO_"+Quell_SpellName());
            MacroCasts.Add(10, "F_"+VerdantEmbrace_SpellName());  
            MacroCasts.Add(34, "MO_"+Expunge_SpellName());
            MacroCasts.Add(35, "F_"+Expunge_SpellName());
            MacroCasts.Add(36, "MO_"+Sleepwalk_SpellName());
            MacroCasts.Add(22, "F_"+SourceofMagic_SpellName());
            MacroCasts.Add(42, "FireBreathMacro");
            MacroCasts.Add(11, "EternitySurgeMacro"); 
            MacroCasts.Add(23, "SourceofMagicMacro");


            foreach(var s in SpellCasts){
                if(!Spellbook.Contains(s.Value))
                    Spellbook.Add(s.Value);
            }
            foreach(string b in BuffList){
                Buffs.Add(b);
            }

            Talents.Add("373466");

            //Macros
            // these macros will always have cusX to cusY so we can change the body in customfunction
            Macros.Add("HealingPotion", "/use Healing Potion"); //Cus1
            Macros.Add("DPSPotion", "/use DPS Potion"); //Cus2
            Macros.Add("SourceofMagicMacro", "/cast [@None] "+SourceofMagic_SpellName());

            //Lists with spells to use with queues
            MouseoverQueues = new List<string>(){
                AzureStrike_SpellName(),
                Quell_SpellName(),
                Expunge_SpellName(),
                Sleepwalk_SpellName(),
                CauterizingFlame_SpellName(),
                };
            CursorQueues = new List<string>(){
                DeepBreath_SpellName()
            };
            PlayerQueues = new List<string>(){
                VerdantEmbrace_SpellName(),
                EmeraldBlossom_SpellName(),
                RenewingBlaze_SpellName(),
            };
            FocusQueues = new List<string>(){
                CauterizingFlame_SpellName(),
                EmeraldBlossom_SpellName(),
                LivingFlame_SpellName(),
                VerdantEmbrace_SpellName(),
                Expunge_SpellName(),
                SourceofMagic_SpellName(),

            };
            TargetQueues = new List<string>(){
                TailSwipe_SpellName(),
                WingBuffet_SpellName(),
                AzureStrike_SpellName(),
                Disintegrate_SpellName(),
                LivingFlame_SpellName(),
                OppressingRoar_SpellName(),
                Sleepwalk_SpellName(),
                Unravel_SpellName(),
                Quell_SpellName(),

            };

            //A Macro that resets all spell queues to false
            Macros.Add("ResetQueues", "/epic resetqueues");

            int q = 1;
            string epicQueues = "local queue = 0\n";
            string epicQueuesInfo = "";
            //Go through the spells' lists and handle creating macros, setting up custom functions and adding them to the spellbook
            foreach(string s in MouseoverQueues){
                //Create a Mouseover macro with a name MO_SpellName
                Macros.Add("MO_"+s, "/cast [@mouseover] "+s);
                //Associate the created Macro with a number that will be returned by the custom function
                Queues.Add(q, "MO_"+s);
                //Part of the Custom function to check if the queue is set
                epicQueues += "if EpicSettings.Queues[\""+"mouseover"+s.ToLower()+"\"] then queue = "+q+" end\n";
                //Part of the Custom function to generate a list of commands inside the Settings window
                epicQueuesInfo += "EpicSettings.AddCommandInfo(5, \"/epic cast mouseover "+s+"\")\n";
                q++;
                //Adding the spell to the spellbook
                if(!Spellbook.Contains(s))
                    Spellbook.Add(s);
            }
            foreach(string s in CursorQueues){
                Macros.Add("C_"+s, "/cast [@cursor] "+s);
                Queues.Add(q, "C_"+s);
                epicQueues += "if EpicSettings.Queues[\""+"cursor"+s.ToLower()+"\"] then queue = "+q+" end\n";
                epicQueuesInfo += "EpicSettings.AddCommandInfo(4, \"/epic cast cursor "+s+"\")\n";
                q++;
                if(!Spellbook.Contains(s))
                    Spellbook.Add(s);
            }
            foreach(string s in PlayerQueues){
                Macros.Add("P_"+s, "/cast [@player] "+s);
                Queues.Add(q, "P_"+s);
                epicQueues += "if EpicSettings.Queues[\""+"player"+s.ToLower()+"\"] then queue = "+q+" end\n";
                epicQueuesInfo += "EpicSettings.AddCommandInfo(2, \"/epic cast player "+s+"\")\n";
                q++;
                if(!Spellbook.Contains(s))
                    Spellbook.Add(s);
            }
            foreach(string s in FocusQueues){
                Macros.Add("F_"+s, "/cast [@focus] "+s);
                Queues.Add(q, "F_"+s);
                epicQueues += "if EpicSettings.Queues[\""+"focus"+s.ToLower()+"\"] then queue = "+q+" end\n";
                epicQueuesInfo += "EpicSettings.AddCommandInfo(6, \"/epic cast focus "+s+"\")\n";
                q++;
                if(!Spellbook.Contains(s))
                    Spellbook.Add(s);
            }
            foreach(string s in TargetQueues){
                Queues.Add(q, s);
                epicQueues += "if EpicSettings.Queues[\""+"target"+s.ToLower()+"\"] then queue = "+q+" end\n";
                epicQueuesInfo += "EpicSettings.AddCommandInfo(3, \"/epic cast target "+s+"\")\n";
                q++;
                if(!Spellbook.Contains(s))
                    Spellbook.Add(s);
            }

            epicQueues += "return queue";

            //Currently you can have max 15 toggles registered
            //Usage: EpicToggle(VariableName, Label, Default, Explanation)
            //You'd access the toggles with lua: EpicSettings.Toggles["Variable"]
            //You can toggle them with a slash command /epic VariableName
            //VariableName is not case sensitive
            //Explanation is for toggle's usage info (visible inside the settings' Commands tab)
            EpicToggles.Add(new EpicToggle("ooc", "OOC", false, "To use out of combat spells"));
            EpicToggles.Add(new EpicToggle("aoe", "AOE", true, "To use AOE"));
            EpicToggles.Add(new EpicToggle("cds", "Cooldowns", true, "To use Cooldowns"));
            EpicToggles.Add(new EpicToggle("dispel", "Dispel", true, "To use Dispels"));
            EpicToggles.Add(new EpicToggle("heal", "Heal", true, "To use Heal"));

            //Setting up Tabs
            EpicSetting.SetTabName(1, "General");
            EpicSetting.SetTabName(2, "Defensive");
            EpicSetting.SetTabName(3, "Cooldown");
            EpicSetting.SetTabName(4, "Healing");

            //Setting up Minitabs
            EpicSetting.SetMinitabName(1, 1, "General");
            EpicSetting.SetMinitabName(1, 2, "Movement");
            EpicSetting.SetMinitabName(1, 3, "Dispel");
            EpicSetting.SetMinitabName(1, 4, "Defensive");
            EpicSetting.SetMinitabName(1, 5, "Interrupts");
            EpicSetting.SetMinitabName(1, 6, "Season 2");
            EpicSetting.SetMinitabName(2, 1, "Personal");
            EpicSetting.SetMinitabName(3, 1, "Cooldowns");
            EpicSetting.SetMinitabName(4, 1, "General");

            //TODO Document Updates
            // Commons.FocusUnit now returns EL.ReturnFocus, 50 = player, 60 = target, 51-54 = party1-4, 1-40 = raid1-40
            // Commons.AreUnitsBelowHealthPercentage() now take in two parameters: HPBelow and MinPlayers
            // Added PressFocus, PressMouseover and PressPlayer inside Core.lua, they return Variables(SpellIDs) for Focus,Mouseover,Player macros

            EpicSettings.Add(new EpicCheckboxSetting(1, 1, 0, "UseRacials", "Use Racials", true));
            EpicSettings.Add(new EpicCheckboxSetting(1, 1, 1, "UseHealingPotion", "Use Healing Potion", false));
            EpicSettings.Add(new EpicDropdownSetting(1, 1, 1, "HealingPotionName", "Healing Potion Name", new List<string>(){"Dreamwalker's Healing Potion"}, "Dreamwalker's Healing Potion"));
            EpicSettings.Add(new EpicSliderSetting(1, 1, 1, "HealingPotionHP", "@ HP", 1, 100, 25));


            EpicSettings.Add(new EpicCheckboxSetting(1, 4, 1, "UseHealthstone", "Use Healthstone", true));
            EpicSettings.Add(new EpicSliderSetting(1, 4, 1, "HealthstoneHP", "@ HP", 1, 100, 60));

            EpicSettings.Add(new EpicCheckboxSetting(1, 5, 1, "InterruptWithStun", "Use Stun for interrupt", true));
            EpicSettings.Add(new EpicCheckboxSetting(1, 5, 1, "InterruptOnlyWhitelist", "Interrupt only Whitelisted spells", true));
            EpicSettings.Add(new EpicSliderSetting(1, 5, 1, "InterruptThreshold", "Interrupt @ spell cast %", 1, 100, 50));


            EpicSettings.Add(new EpicCheckboxSetting(1, 1, 3, "UseBlessingOfTheBronze", "Use Blessing of the Bronze", true));

            EpicSettings.Add(new EpicCheckboxSetting(1, 3, 1, "DispelDebuffs", "Dispel Debuffs", true));
            EpicSettings.Add(new EpicCheckboxSetting(1, 3, 2, "DispelBuffs", "Dispel Buffs", true));
            EpicSettings.Add(new EpicCheckboxSetting(1, 3, 3, "UseOppressingRoar", "Use Oppressing Roar", true));


            EpicSettings.Add(new EpicDropdownSetting(3, 1, 1, "SourceOfMagicUsage", "Source of Magic Settings", new List<string>(){"Auto", "Selected", "False"}, "Auto"));
            EpicSettings.Add(new EpicGroupDropdownSetting(3, 1, 1, "SourceOfMagicName", "Select unit", true, false, false, false));

            EpicSettings.Add(new EpicDropdownSetting(4, 1, 1, "VerdantEmbraceUsage", "Verdant Embrace Usage:", new List<string>(){"Everyone", "Not Tank", "Player Only", "False"}, "Not Tank"));
            EpicSettings.Add(new EpicSliderSetting(4, 1, 1, "VerdantEmbraceHP", "Verdant Embrace @ HP%", 0, 100, 55));

            EpicSettings.Add(new EpicDropdownSetting(4, 1,2, "EmeraldBlossomUsage", "Emerald Blossom Usage:", new List<string>(){"Everyone", "Player Only", "False"}, "Everyone"));
            EpicSettings.Add(new EpicSliderSetting(4, 1, 2, "EmeraldBlossomHP", "Emerald Blossom @ HP%", 0, 100, 55));

            EpicSettings.Add(new EpicCheckboxSetting(2, 1, 1, "UseRenewingBlaze", "Use Renewing Blaze", true));
            EpicSettings.Add(new EpicSliderSetting(2, 1, 1, "RenewingBlazeHP", "Renewing Blaze @ HP", 1, 100, 40));

            EpicSettings.Add(new EpicCheckboxSetting(2, 1, 2, "UseObsidianScales", "Use Obsidian Scales", true));
            EpicSettings.Add(new EpicSliderSetting(2, 1, 2, "ObsidianScalesHP", "Obsidian Scales @ HP", 1, 100, 40));
            

            EpicSettings.Add(new EpicCheckboxSetting(1, 2, 1, "UseHover", "Use Hover", true));
            EpicSettings.Add(new EpicSliderSetting(1, 2, 1, "HoverTime", "When moving for (seconds)", 1, 10, 3));

            //Settings.Add(new EpicDropdownSetting(1, 1, 1, "LandslideUsage", "Landslide Usage:", new List<string>(){"@Cursor", "Confirmation", "Manual"}, "Manual"));
            
            Macros.Add("focusplayer", "/focus player");
            Macros.Add("focustarget", "/focus target");

            for(int i = 1; i <= 4; i++){
                Macros.Add("focusparty"+i, "/focus party"+i);
            }
            for(int i = 1; i <= 20; i++){
                Macros.Add("focusraid"+i, "/focus raid"+i);
            }

            //Spell Macros
            Macros.Add("Next", "/targetenemy");
            Macros.Add("DeepBreathCursor", "/cast [@cursor] "+ DeepBreath_SpellName());
            Macros.Add("EternitySurgeMacro", "/cast "+ EternitySurge_SpellName());
            Macros.Add("FireBreathMacro", "/cast [@player] "+ FireBreath_SpellName());

            //Item Macros
            Macros.Add("Healthstone", "/use "+ Healthstone_ItemName());
            Macros.Add("TopTrinket", "/use 13");
            Macros.Add("TopTrinketPlayer", "/use [@player] 13");
            Macros.Add("TopTrinketCursor", "/use [@cursor] 13");
            Macros.Add("TopTrinketFocus", "/use [@focus] 13");
            Macros.Add("BottomTrinket", "/use 14");
            Macros.Add("BottomTrinketPlayer", "/use [@player] 14");
            Macros.Add("BottomTrinketCursor", "/use [@cursor] 14");
            Macros.Add("BottomTrinketFocus", "/use [@focus] 14");
            Macros.Add("RefreshingHealingPotion", "/cast "+ RefreshingHealing_PotionName());

            //Getting the addon name
            string addonName = Aimsharp.GetAddonName().ToLower();
            if (Aimsharp.GetAddonName().Length >= 5){
                addonName = Aimsharp.GetAddonName().Substring(0, 5).ToLower();
            }

            //Usage: CreateCustomFunction(LabelForAimsharpToggleButton, FiveLettersOfAddonName, ListOfSettings, ListOfToggles)
            CustomFunctions.Add("SetupEpicSettings", EpicSetting.CreateCustomFunction("Toggle", addonName, EpicSettings, EpicToggles, epicQueuesInfo));

            //A function set up to return which spell is queued
            CustomFunctions.Add("GetEpicQueues", epicQueues);

            //A function that updates Macrosä bodies with the Settings textboxes, as long as macro name(CusX) doesnt change
            CustomFunctions.Add("UpdateMacros", "" +
            "if not UnitAffectingCombat(\"player\") then\n" +
                "if EpicSettings.Settings[\"HealingPotionName\"] then\n"+
                    "if EpicSettings.Settings[\"HealingPotionName\"] == \"Dreamwalker's Healing Potion\" then \n" +
                        "if GetMacroBody(\"Cus1\") ~= \"/use "+DreamwalkersHealing_PotionName()+"\" then\n" +
                            "EditMacro(\"Cus1\", nil, nil, \"/use "+DreamwalkersHealing_PotionName()+"\", 1, 1)\n" +
                        "end\n" +
                    "end\n" +
                "end\n"+
                "if EpicSettings.Settings[\"DPSPotionName\"] then\n"+
                    "local potionName = \"\"\n" +
                    "if EpicSettings.Settings[\"DPSPotionName\"] == \"Fleeting Ultimate Power\" then \n" +
                        "potionName = \""+FleetingUltimatePower_PotionName()+"\"\n" +
                    "end\n" +
                    "if EpicSettings.Settings[\"DPSPotionName\"] == \"Fleeting Power\" then \n" +
                        "potionName = \""+FleetingPower_PotionName()+"\"\n" +
                    "end\n" +
                    "if EpicSettings.Settings[\"DPSPotionName\"] == \"Ultimate Power\" then \n" +
                        "potionName = \""+UltimatePower_PotionName()+"\"\n" +
                    "end\n" +
                    "if EpicSettings.Settings[\"DPSPotionName\"] == \"Shocking Disclosure\" then \n" +
                        "potionName = \""+ShockingDisclosure_PotionName()+"\"\n" +
                    "end\n" +
                    "if EpicSettings.Settings[\"DPSPotionName\"] == \"Power\" then \n" +
                        "potionName = \""+Power_PotionName()+"\"\n" +
                    "end\n" +
                    "if GetMacroBody(\"Cus2\") ~= \"/use \"..potionName then\n" +
                        "EditMacro(\"Cus2\", nil, nil, \"/use \"..potionName, 1, 1)\n" +
                    "end\n" +
                "end\n"+
                "if EpicSettings.Settings[\"SourceOfMagicName\"] then\n"+
                    "if GetMacroBody(\"Cus3\") ~= \"/cast [@\"..EpicSettings.Settings[\"SourceOfMagicName\"]..\"] "+SourceofMagic_SpellName()+"\" then\n" +
                        "EditMacro(\"Cus3\", nil, nil, \"/cast [@\"..EpicSettings.Settings[\"SourceOfMagicName\"]..\"] "+SourceofMagic_SpellName()+"\", 1, 1)\n" +
                    "end\n" +
                "end\n"+
            "end\n"+
            "return 0");


            CustomFunctions.Add("CooldownsToggle", "local Usage = 0\n" +
            "if EpicSettings.Toggles[\"cds\"] then\n"+
                "Usage = 1\n"+
            "end\n" +
            "return Usage");


            CustomFunctions.Add("PartyUnitIsFocus", "local foc=0; " +
            "\nif UnitExists('focus') and UnitIsUnit('party1','focus') then foc = 1; end" +
            "\nif UnitExists('focus') and UnitIsUnit('party2','focus') then foc = 2; end" +
            "\nif UnitExists('focus') and UnitIsUnit('party3','focus') then foc = 3; end" +
            "\nif UnitExists('focus') and UnitIsUnit('party4','focus') then foc = 4; end" +
            "\nif UnitExists('focus') and UnitIsUnit('player','focus') then foc = 99; end" +
            "\nreturn foc");

            CustomFunctions.Add("RaidUnitIsFocus", "local foc=0; " +
            "\nif UnitExists('focus') and UnitIsUnit('raid1','focus') then foc = 1; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid2','focus') then foc = 2; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid3','focus') then foc = 3; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid4','focus') then foc = 4; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid5','focus') then foc = 5; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid6','focus') then foc = 6; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid7','focus') then foc = 7; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid8','focus') then foc = 8; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid9','focus') then foc = 9; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid10','focus') then foc = 10; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid11','focus') then foc = 11; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid12','focus') then foc = 12; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid13','focus') then foc = 13; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid14','focus') then foc = 14; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid15','focus') then foc = 15; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid16','focus') then foc = 16; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid17','focus') then foc = 17; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid18','focus') then foc = 18; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid19','focus') then foc = 19; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid20','focus') then foc = 20; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid21','focus') then foc = 21; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid22','focus') then foc = 22; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid23','focus') then foc = 23; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid24','focus') then foc = 24; end" +
            "\nif UnitExists('focus') and UnitIsUnit('raid25','focus') then foc = 25; end" +
            "\nreturn foc");
            
            CustomFunctions.Add("SpellId", "local EL = EpicLib \n" +
            "function RunEpicLib() \n" +                
                "if EL then \n" +
                    "local x = EL.EpicSettingsS \n" +
                    "if x then \n" +
                        "return tonumber(x) \n" +
                    "else \n" +
                        "return 0 \n" +
                    "end \n" +
                "else \n" +
                    "return 9999999999 \n" +
                "end \n" +
            "end \n" +
                "local y = RunEpicLib() \n" +
                "return y");

            CustomFunctions.Add("MacroId", "local EL = EpicLib \n" +
            "function RunEpicLib() \n" +                
                "if EL then \n" +
                    "local x = EL.EpicSettingsM \n" +
                    "if x then \n" +
                        "return tonumber(x) \n" +
                    "else \n" +
                        "return 0 \n" +
                    "end \n" +
                "else \n" +
                    "return 9999999999 \n" +
                "end \n" +
            "end \n" +
                "local y = RunEpicLib() \n" +
                "return y");


                CustomFunctions.Add("ReturnFocus", "local EL = EpicLib \n" +
                "function CheckEpicFocus() \n" +                
                    "if EL then \n" +
                        "local focusResult = EL.ReturnFocus \n" +
                        "if focusResult then \n" +
                            "return tonumber(focusResult) \n" +
                        "else \n" +
                            "return 0 \n" +
                        "end \n" +
                    "else \n" +
                        "return 9999999999 \n" +
                    "end \n" +
                "end \n" +
                    "local FocusReturn = CheckEpicFocus() \n" +
                    "return FocusReturn");

        }

        public void Focus(string targetString, int delayAfter = 200){
            if (targetString.Contains("party")){
                Aimsharp.Cast("focus"+targetString, true);
            }else if (targetString.Contains("raid")){
                Aimsharp.Cast("focus"+targetString, true);
            }else if (targetString == "player"){
                Aimsharp.Cast("focusplayer", true);
            }
            
            System.Threading.Thread.Sleep(delayAfter);
        }

        public bool UnitIsFocus(string targetString){
            int PartyUnitIsFocus = Aimsharp.CustomFunction("PartyUnitIsFocus");
            int RaidUnitIsFocus = Aimsharp.CustomFunction("RaidUnitIsFocus");
            if (targetString.Contains("party")){
                if(targetString == "party"+PartyUnitIsFocus){
                    return true;
                }
            }else if (targetString.Contains("raid")){
                if(targetString == "raid"+RaidUnitIsFocus){
                    return true;
                }
            }
            else if (targetString == "player"){
                if(PartyUnitIsFocus == 99){
                    return true;
                }
            }
            return false;
        }

        public override bool CombatTick(){
            if(authorized){
                bool TwinsOfTheSunPriestessTalent = Aimsharp.Talent(373466);

                int epicQueue = Aimsharp.CustomFunction("GetEpicQueues");
                string queueSpell = "";
                //Aimsharp.PrintMessage("Queue "+epicQueue, Color.Purple);
                if(epicQueue != 0){
                    if (Queues.TryGetValue(epicQueue, out queueSpell)){
                        string spellname = queueSpell;
                        //Handling Mouseover queue spells
                        if(queueSpell.Substring(0, 3) == "MO_"){
                            spellname = queueSpell.Substring(3);
                            if(Aimsharp.CanCast(spellname, "mouseover") && Aimsharp.LastCast() != spellname){
                                //Cast
                                Aimsharp.Cast(queueSpell, true);
                                Aimsharp.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                            }
                            if(Aimsharp.LastCast() == spellname || Aimsharp.SpellCooldown(spellname) > 2000){
                                Aimsharp.Cast("ResetQueues", true);
                            }
                        //Handling Cursor and Player queue spells
                        }else if(queueSpell.Substring(0, 2) == "C_" || queueSpell.Substring(0, 2) == "P_"){
                            spellname = queueSpell.Substring(2);
                            if(Aimsharp.CanCast(spellname, "player") && Aimsharp.LastCast() != spellname){
                                //Cast
                                Aimsharp.Cast(queueSpell, true);
                                Aimsharp.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                            }
                            if(Aimsharp.LastCast() == spellname || Aimsharp.SpellCooldown(spellname) > 2000){
                                Aimsharp.Cast("ResetQueues", true);
                            }
                        //Handling Focus queue spells
                        }else if(queueSpell.Substring(0, 2) == "F_"){
                            spellname = queueSpell.Substring(2);
                            if(Aimsharp.CanCast(spellname, "focus") && Aimsharp.LastCast() != spellname){
                                //Cast
                                Aimsharp.Cast(queueSpell, true);
                                Aimsharp.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                            }
                            if(Aimsharp.LastCast() == spellname || Aimsharp.SpellCooldown(spellname) > 2000){
                                Aimsharp.Cast("ResetQueues", true);
                            }
                        //Handling Target queue spells
                        }else{
                            if(Aimsharp.CanCast(spellname) && Aimsharp.LastCast() != spellname){
                                //Cast
                                Aimsharp.Cast(queueSpell);
                                Aimsharp.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                            }
                            if(Aimsharp.LastCast() == spellname || Aimsharp.SpellCooldown(spellname) > 2000){
                                Aimsharp.Cast("ResetQueues", true);
                            }
                        }
                    }
                    return false;
                }

                int returnFocus = Aimsharp.CustomFunction("ReturnFocus");

                if(returnFocus == 50){
                    Aimsharp.Cast("focusplayer", true);
                    return true;
                }else if(returnFocus == 60){
                    Aimsharp.Cast("focustarget", true);
                    return true;
                }else if(returnFocus > 50){
                    Aimsharp.Cast("focusparty"+(returnFocus-50), true);
                    return true;
                }else if(returnFocus > 0){
                    Aimsharp.Cast("focusraid"+returnFocus, true);
                    return true;
                }

                bool CooldownsToggle = Aimsharp.CustomFunction("CooldownsToggle") == 1;



                //Spells ahead

                int SpellId = Aimsharp.CustomFunction("SpellId");
                int MacroId = Aimsharp.CustomFunction("MacroId");

                string castMacro = "";
                if (MacroCasts.TryGetValue(MacroId, out castMacro)){
                    Aimsharp.PrintMessage("Casting Macro: "+MacroId, Color.Purple);
                    Aimsharp.Cast(castMacro, true);
                    return true;
                }else{
                    if(MacroId > 0)
                        Aimsharp.PrintMessage("Couldn't find the macro with id: "+MacroId, Color.Purple);
                }

                string castSpell = "";
                if (SpellCasts.TryGetValue(SpellId, out castSpell)){
                    Aimsharp.PrintMessage("Casting Spell: "+SpellId, Color.Purple);
                    Aimsharp.Cast(castSpell);
                    return true;
                }else{
                    if(SpellId > 0)
                        Aimsharp.PrintMessage("Couldn't find the spell with id: "+SpellId, Color.Purple);
                }

            }
            return false;
        }

        public override bool OutOfCombatTick(){
            if(authorized){
                bool TwinsOfTheSunPriestessTalent = Aimsharp.Talent(373466);

                int epicQueue = Aimsharp.CustomFunction("GetEpicQueues");
                string queueSpell = "";
                                if(epicQueue != 0){
                    if (Queues.TryGetValue(epicQueue, out queueSpell)){
                        string spellname = queueSpell;
                        //Handling Mouseover queue spells
                        if(queueSpell.Substring(0, 3) == "MO_"){
                            spellname = queueSpell.Substring(3);
                            if(Aimsharp.CanCast(spellname, "mouseover") && Aimsharp.LastCast() != spellname){
                                //Cast
                                Aimsharp.Cast(queueSpell, true);
                                Aimsharp.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                            }else{
                                Aimsharp.Cast("ResetQueues", true);
                            }
                        //Handling Cursor and Player queue spells
                        }else if(queueSpell.Substring(0, 2) == "C_" || queueSpell.Substring(0, 2) == "P_"){
                            spellname = queueSpell.Substring(2);
                            if(Aimsharp.CanCast(spellname, "player") && Aimsharp.LastCast() != spellname){
                                //Cast
                                Aimsharp.Cast(queueSpell, true);
                                Aimsharp.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                            }else{
                                Aimsharp.Cast("ResetQueues", true);
                            }
                        //Handling Focus queue spells
                        }else if(queueSpell.Substring(0, 2) == "F_"){
                            spellname = queueSpell.Substring(2);
                            if(Aimsharp.CanCast(spellname, "focus") && Aimsharp.LastCast() != spellname){
                                //Cast
                                Aimsharp.Cast(queueSpell, true);
                                Aimsharp.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                            }else{
                                Aimsharp.Cast("ResetQueues", true);
                            }
                        //Handling Target queue spells
                        }else{
                            if(Aimsharp.CanCast(spellname) && Aimsharp.LastCast() != spellname){
                                //Cast
                                Aimsharp.Cast(queueSpell);
                                Aimsharp.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                            }else{
                                Aimsharp.Cast("ResetQueues", true);
                            }
                        }
                    }
                }

                int returnFocus = Aimsharp.CustomFunction("ReturnFocus");

                if(returnFocus == 50){
                    Aimsharp.Cast("focusplayer", true);
                    return true;
                }else if(returnFocus == 60){
                    Aimsharp.Cast("focustarget", true);
                    return true;
                }else if(returnFocus > 50){
                    Aimsharp.Cast("focusparty"+(returnFocus-50), true);
                    return true;
                }else if(returnFocus > 0){
                    Aimsharp.Cast("focusraid"+returnFocus, true);
                    return true;
                }

                //Spells ahead

                int SpellId = Aimsharp.CustomFunction("SpellId");
                int MacroId = Aimsharp.CustomFunction("MacroId");

                string castMacro = "";
                if (MacroCasts.TryGetValue(MacroId, out castMacro)){
                    Aimsharp.Cast(castMacro, true);
                    return true;
                }else{
                    if(MacroId > 0)
                        Aimsharp.PrintMessage("Couldn't find the macro with id: "+MacroId, Color.Purple);
                }

                string castSpell = "";
                if (SpellCasts.TryGetValue(SpellId, out castSpell)){
                    Aimsharp.Cast(castSpell);
                    return true;
                }else{
                    if(SpellId > 0)
                        Aimsharp.PrintMessage("Couldn't find the spell with id: "+SpellId, Color.Purple);
                }

            }
            return false;
        }

    }
}

