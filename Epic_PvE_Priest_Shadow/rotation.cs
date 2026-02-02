using System.Linq;
using System.Diagnostics;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO; // Used for Licensing
using InfernoWow.API; //needed to access Inferno API
using System.Net;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace InfernoWow.Modules{
#region SettingClasses

    public class EpicSetting{
        static List<EpicTab> Tabs = new List<EpicTab>();
        static List<EpicTab>[] Minitabs = new List<EpicTab>[15];

        public string Label;
        public string VariableName;
        public int Tab;
        public int Minitab;
        public int Line;

        public int NumberOfBits;

        public string GetterFunctionName;
        public int Shift;

        public virtual string GetCustomFunctionSnippet(){
            return "";
        }

        public virtual string GetSettingGetterSnippet(){
            return "";
        }

        public static void SetTabName(int id, string name){
            Tabs.Add(new EpicTab(id, name));
            Minitabs[id-1] = new List<EpicTab>();
        }

        public static void SetMinitabName(int tabid, int id, string name){
            Minitabs[tabid-1].Add(new EpicTab(id, name));
        }

        public static string CreateCustomFunction(string mainToggle, string addonName, List<EpicSetting> settings, List<EpicToggle> toggles, string additionalLines, string epicSettingAddonName){
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
                    minitabFunctionSnippet += (epicSettingAddonName+".InitMiniTabs("+(j+1));
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

            settingFunctionSnippet += (epicSettingAddonName+".InitButtonMain(\""+mainToggle+"\", \""+addonName+"\")\n");

            foreach(EpicToggle t in toggles){
                settingFunctionSnippet += t.GetCustomFunctionSnippet() + "\n";
            }

            customFunction += "if GetCVar(\"setup"+epicSettingAddonName+"CVAR\") == nil then RegisterCVar( \"setup"+epicSettingAddonName+"CVAR\", 0 ) end\n" +
                            "if GetCVar(\"setup"+epicSettingAddonName+"CVAR\") == '0' then\n" +
                            epicSettingAddonName+".InitTabs("+tabFunctionSnippet+")\n" +
                            minitabFunctionSnippet +
                            settingFunctionSnippet +
                            additionalLines +
                            "SetCVar(\"setup"+epicSettingAddonName+"CVAR\", 1);\n" +
                            "end\n" +
                            "return 0";

            customFunction = customFunction.Replace("_AddonName_", epicSettingAddonName);

            return customFunction;
        }

        public static List<string> CreateSettingsGetters(List<EpicSetting> settings, List<EpicToggle> toggles, string epicSettingAddonName){
            List<string> customFunctions = new List<string>();

            int numberOfBits = 0;
            int functionNumber = 1;
            string function = "local Settings = 0\nlocal multiplier = 1\n";
            foreach(EpicSetting es in settings){
                es.Shift = numberOfBits;
                numberOfBits += es.NumberOfBits;
                if(numberOfBits >= 24){
                    es.Shift = 0;
                    numberOfBits = es.NumberOfBits;
                    function += "return Settings";
                    function = function.Replace("_AddonName_", epicSettingAddonName);
                    customFunctions.Add(function);
                    function = "local Settings = 0\nlocal multiplier = 1\n";
                    functionNumber++;
                }

                // if (functionNumber == 2)
                //         Inferno.PrintMessage(es.VariableName + " Shifting " + numberOfBits, Color.Purple);

                function += es.GetSettingGetterSnippet();
                es.GetterFunctionName = "SettingsGetter"+functionNumber;
            }
            foreach(EpicToggle t in toggles){
                t.Shift = numberOfBits;
                numberOfBits += t.NumberOfBits;
                if(numberOfBits >= 24){
                    t.Shift = 0;
                    numberOfBits = t.NumberOfBits;
                    function += "return Settings";
                    function = function.Replace("_AddonName_", epicSettingAddonName);
                    customFunctions.Add(function);
                    function = "local Settings = 0\nlocal multiplier = 1\n";
                    functionNumber++;
                }

                function += t.GetSettingGetterSnippet();
                t.GetterFunctionName = "SettingsGetter"+functionNumber;
            }

            function += "return Settings";

            function = function.Replace("_AddonName_", epicSettingAddonName);

            customFunctions.Add(function);

            return customFunctions;
        }

        public static int GetSliderSetting(List<EpicSetting> settings, string name){
            foreach(EpicSetting es in settings){
                if(es.VariableName == name){
                    //Inferno.PrintMessage("Found the setting " + (Inferno.CustomFunction(es.GetterFunctionName)>>es.Shift), Color.Purple);
                    // if (es.VariableName == "DesperatePrayerHP")
                    //     Inferno.PrintMessage("Found the setting " + es.GetterFunctionName + " " + es.Shift + " " +es.NumberOfBits, Color.Purple);
                    return (Inferno.CustomFunction(es.GetterFunctionName)>>es.Shift)&(((int)Math.Pow(2,es.NumberOfBits))-1);
                }
            }
            return 0;
        }
        public static bool GetCheckboxSetting(List<EpicSetting> settings, string name){
            foreach(EpicSetting es in settings){
                if(es.VariableName == name){
                    //Inferno.PrintMessage("Found the setting " + (Inferno.CustomFunction(es.GetterFunctionName)>>es.Shift), Color.Purple);
                    // if (es.VariableName == "DesperatePrayerHP")
                    //     Inferno.PrintMessage("Found the setting " + es.GetterFunctionName + " " + es.Shift + " " +es.NumberOfBits, Color.Purple);
                    return ((Inferno.CustomFunction(es.GetterFunctionName)>>es.Shift)&(((int)Math.Pow(2,es.NumberOfBits))-1)) == 1;
                }
            }
            return false;
        }
        public static string GetDropdownSetting(List<EpicSetting> settings, string name){
            foreach(EpicSetting es in settings){
                if(es.VariableName == name){
                    //Inferno.PrintMessage(name, Color.Purple);
                    //Inferno.PrintMessage("Found the setting " + (Inferno.CustomFunction(es.GetterFunctionName)>>es.Shift), Color.Purple);
                    // if (es.VariableName == "DesperatePrayerHP")
                    //     Inferno.PrintMessage("Found the setting " + es.GetterFunctionName + " " + es.Shift + " " +es.NumberOfBits, Color.Purple);
                    int index = (Inferno.CustomFunction(es.GetterFunctionName)>>es.Shift)&(((int)Math.Pow(2,es.NumberOfBits))-1);
                    string output = "";
                    int i = 0;
                    EpicDropdownSetting esd = es as EpicDropdownSetting;
                    foreach(string s in esd.Options){
                        if(index == i){
                            output = s;
                            break;
                        }
                        i++;
                    }

                    return output;
                }
            }
            return "";
        }
        public static bool GetHeldKey(List<EpicSetting> settings, string name){
            foreach(EpicSetting es in settings){
                if(es.VariableName == name){
                    return ((Inferno.CustomFunction(es.GetterFunctionName)>>es.Shift)&(((int)Math.Pow(2,es.NumberOfBits))-1)) == 1;
                }
            }
            return false;
        }
        public static bool GetToggle(List<EpicToggle> toggles, string name){
            foreach(EpicToggle t in toggles){
                if(t.VariableName == name){
                    return ((Inferno.CustomFunction(t.GetterFunctionName)>>t.Shift)&(((int)Math.Pow(2,t.NumberOfBits))-1)) == 1;
                }
            }
            return false;
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
            this.NumberOfBits = 0;
        }

        public override string GetCustomFunctionSnippet(){
            return "_AddonName_.AddLabel("+Tab+", "+Minitab+", "+Line+", \""+Label+"\")";
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
            this.NumberOfBits = 0;
        }

        public override string GetCustomFunctionSnippet(){
            return "_AddonName_.AddTextbox("+Tab+", "+Minitab+", "+Line+", \""+VariableName+"\", \""+Label+"\", \""+Default+"\")";
        }
    }

    public class EpicHeldKeySetting : EpicSetting{
        public string Default;

        public EpicHeldKeySetting(int tab, int minitab, int line, string variableName, string label, string defaultValue){
            this.Label = label;
            this.VariableName = variableName;
            this.Default = defaultValue;
            this.Tab = tab;
            this.Minitab = minitab;
            this.Line = line;
            this.NumberOfBits = 1;
        }

        public override string GetCustomFunctionSnippet(){
            return "_AddonName_.AddHeldKeyTextbox("+Tab+", "+Minitab+", "+Line+", \""+VariableName+"\", \""+Label+"\", \""+Default+"\")";
        }
        public override string GetSettingGetterSnippet(){

            return "if _AddonName_.HeldKeys[\""+VariableName+"\"].value == true then\nSettings = Settings + multiplier\nend\nmultiplier = multiplier * 2\n";
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

            int power = 1;
            int flag2 = 1;

            while(flag2 < Max){
                flag2 = flag2 + (int)Math.Pow(2, power);
                power++;
            }

            this.NumberOfBits = power;
        }

        public override string GetCustomFunctionSnippet(){
            return "_AddonName_.AddSlider("+Tab+", "+Minitab+", "+Line+", \""+VariableName+"\", \""+Label+"\", "+Min+", "+Max+", "+Default+")";
        }

        public override string GetSettingGetterSnippet(){
            return "if _AddonName_.Settings[\""+VariableName+"\"] then\nSettings = Settings + (multiplier * _AddonName_.Settings[\""+VariableName+"\"])\nend\nmultiplier = multiplier * 2^"+NumberOfBits+"\n";
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
            this.NumberOfBits = 1;
        }

        public override string GetCustomFunctionSnippet(){
            return "_AddonName_.AddCheckbox("+Tab+", "+Minitab+", "+Line+", \""+VariableName+"\", \""+Label+"\", "+Default.ToString().ToLower()+")";
        }

        public override string GetSettingGetterSnippet(){

            return "if _AddonName_.Settings[\""+VariableName+"\"] == true then\nSettings = Settings + multiplier\nend\nmultiplier = multiplier * 2\n";
        }
    }
    public class EpicDropdownSetting : EpicSetting{
        public List<string> Options;
        string Default;
        
        public EpicDropdownSetting(int tab, int minitab, int line, string variableName, string label, List<string> options, string defaultValue){
            this.Label = label;
            this.VariableName = variableName;
            this.Options = options;
            this.Default = defaultValue;
            this.Tab = tab;
            this.Minitab = minitab;
            this.Line = line;

            int i = 0;
            int flag = 1;
            int power = 1;
            foreach(string s in Options){
                if(i > flag){
                    flag = flag + (int)Math.Pow(2, power);
                    power++;
                }
                i++;
            }

            this.NumberOfBits = power;
        }

        public override string GetCustomFunctionSnippet(){
            string options = "";
            foreach(string s in Options){
                options += ", \""+ s +"\"";
            }
            return "_AddonName_.AddDropdown("+Tab+", "+Minitab+", "+Line+", \""+VariableName+"\", \""+Label+"\", \""+Default+"\""+options+")";
        }

        public override string GetSettingGetterSnippet(){
            string getter = "";
            int i = 0;
            foreach(string s in Options){
                getter += "if _AddonName_.Settings[\""+VariableName+"\"] == \""+s+"\" then\nSettings = Settings + (multiplier * "+i+")\nend\n";
                i++;
            }

            getter += "multiplier = multiplier * 2^"+NumberOfBits+"\n";

            return getter;
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
            this.NumberOfBits = 0;
        }

        public override string GetCustomFunctionSnippet(){
            return "_AddonName_.AddGroupDropdown("+Tab+", "+Minitab+", "+Line+", \""+VariableName+"\", \""+Label+"\", "+IncludeHealers.ToString().ToLower()+", "+IncludeDamagers.ToString().ToLower()+", "+IncludeTanks.ToString().ToLower()+", "+IncludePlayer.ToString().ToLower()+")";
        }
    }

    public class EpicToggle{
        public bool Default;
        public string Label;
        public string VariableName;
        public string Explanation;
        public int NumberOfBits;
        public string GetterFunctionName;
        public int Shift;

        public EpicToggle(string variableName, string label, bool defaultValue, string explanation){
            this.Default = defaultValue;
            this.Label = label;
            this.VariableName = variableName;
            this.Explanation = explanation;
            this.NumberOfBits = 1;
        }

        public virtual string GetCustomFunctionSnippet(){
            return "_AddonName_.InitToggle(\""+Label+"\", \""+VariableName+"\", "+Default.ToString().ToLower()+", \""+Explanation+"\")\n";
        }

        public virtual string GetSettingGetterSnippet(){
            return "if _AddonName_.Toggles[\""+VariableName+"\"] == true then\nSettings = Settings + multiplier\nend\nmultiplier = multiplier * 2\n";
        }
    }

#endregion

    public class BloodDeathknight : Rotation
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
        private static string TemperedPotion_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Gemäßigter Trank";
                case "Español":
                    return "Poción templada";
                case "Français":
                    return "Potion tempérée";
                case "Italiano":
                    return "Pozione Temprata";
                case "Português Brasileiro":
                    return "Poção Temperada";
                case "Русский":
                    return "Охлажденное зелье";
                case "한국어":
                    return "절제된 물약";
                case "简体中文":
                    return "淬火药水";
                default:
                    return "Tempered Potion";
            }
        }
        private static string PotionofUnwaveringFocus_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Trank des felsenfesten Fokus";
                case "Español":
                    return "Poción de concentración inquebrantable";
                case "Français":
                    return "Potion de concentration inébranlable";
                case "Italiano":
                    return "Pozione del Focus Incrollabile";
                case "Português Brasileiro":
                    return "Poção da Concentração Inabalável";
                case "Русский":
                    return "Зелье предельной концентрации";
                case "한국어":
                    return "흔들림 없는 집중의 물약";
                case "简体中文":
                    return "专心致志药水";
                default:
                    return "Potion of Unwavering Focus";
            }
        }
        private static string InvigoratingHealingPotion_ItemName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Stärkender Heiltrank";
        case "Español":
            return "Poción de sanación vigorizante";
        case "Français":
            return "Potion de soins revigorante";
        case "Italiano":
            return "Pozione di Cura Rinvigorente";
        case "Português Brasileiro":
            return "Poção de Cura Envigorante";
        case "Русский":
            return "Бодрящее лечебное зелье";
        case "한국어":
            return "활력의 치유 물약";
        case "简体中文":
            return "焕生治疗药水";
        default:
            return "Invigorating Healing Potion";
    }
}

        private static string AlgariHealingPotion_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Algarischer Heiltrank";
                case "Español":
                    return "Poción de sanación algariana";
                case "Français":
                    return "Potion de soins algarie";
                case "Italiano":
                    return "Pozione di Cura Algari";
                case "Português Brasileiro":
                    return "Poção de Cura Algari";
                case "Русский":
                    return "Алгарийское лечебное зелье";
                case "한국어":
                    return "알가르 치유 물약";
                case "简体中文":
                    return "阿加治疗药水";
                default:
                    return "Invigorating Healing Potion";
            }
        }
        private static string Stoneform_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Steingestalt";
                case "Español":
                    return "Forma de piedra";
                case "Français":
                    return "Forme de pierre";
                case "Italiano":
                    return "Forma di Pietra";
                case "Português Brasileiro":
                    return "Forma de Pedra";
                case "Русский":
                    return "Каменная форма";
                case "한국어":
                    return "석화";
                case "简体中文":
                    return "石像形态";
                default:
                    return "Stoneform";
            }
        }
        private static string WillToSurvive_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Überlebenswille";
                case "Español":
                    return "Lucha por la supervivencia";
                case "Français":
                    return "Volonté de survie";
                case "Italiano":
                    return "Volontà di Sopravvivenza";
                case "Português Brasileiro":
                    return "Desejo de Sobreviver";
                case "Русский":
                    return "Воля к жизни";
                case "한국어":
                    return "삶의 의지";
                case "简体中文":
                    return "生存意志";
                default:
                    return "Will to Survive";
            }
        }
        private static string Fireblood_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Feuerblut";
                case "Español":
                    return "Sangrardiente";
                case "Français":
                    return "Sang de feu";
                case "Italiano":
                    return "Sangue Infuocato";
                case "Português Brasileiro":
                    return "Sangue de Fogo";
                case "Русский":
                    return "Огненная кровь";
                case "한국어":
                    return "불꽃피";
                case "简体中文":
                    return "烈焰之血";
                default:
                    return "Fireblood";
            }
        }
        private static string WarStomp_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Kriegsdonner";
                case "Español":
                    return "Pisotón de guerra";
                case "Français":
                    return "Choc martial";
                case "Italiano":
                    return "Zoccolo di Guerra";
                case "Português Brasileiro":
                    return "Pisada de Guerra";
                case "Русский":
                    return "Громовая поступь";
                case "한국어":
                    return "전투 발구르기";
                case "简体中文":
                    return "战争践踏";
                default:
                    return "War Stomp";
            }
        }
        private static string WillOfTheForsaken_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Wille der Verlassenen";
                case "Español":
                    return "Voluntad de los Renegados";
                case "Français":
                    return "Volonté des Réprouvés";
                case "Italiano":
                    return "Volontà dei Reietti";
                case "Português Brasileiro":
                    return "Determinação dos Renegados";
                case "Русский":
                    return "Воля Отрекшихся";
                case "한국어":
                    return "포세이큰의 의지";
                case "简体中文":
                    return "被遗忘者的意志";
                default:
                    return "Will of the Forsaken";
            }
        }
        private static string ArcanePulse_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Arkaner Puls";
                case "Español":
                    return "Pulso Arcano";
                case "Français":
                    return "Impulsion arcanique";
                case "Italiano":
                    return "Impulso Arcano";
                case "Português Brasileiro":
                    return "Pulso Arcano";
                case "Русский":
                    return "Чародейский импульс";
                case "한국어":
                    return "비전 파동";
                case "简体中文":
                    return "奥术脉冲";
                default:
                    return "Arcane Pulse";
            }
        }
        private static string BullRush_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Aufs Geweih nehmen";
                case "Español":
                    return "Embestida astada";
                case "Français":
                    return "Charge de taureau";
                case "Italiano":
                    return "Scatto del Toro";
                case "Português Brasileiro":
                    return "Investida do Touro";
                case "Русский":
                    return "Бычий натиск";
                case "한국어":
                    return "황소 돌진";
                case "简体中文":
                    return "蛮牛冲撞";
                default:
                    return "Bull Rush";
            }
        }
        private static string AncestralCall_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Ruf der Ahnen";
                case "Español":
                    return "Llamada ancestral";
                case "Français":
                    return "Appel ancestral";
                case "Italiano":
                    return "Richiamo Ancestrale";
                case "Português Brasileiro":
                    return "Chamado Ancestral";
                case "Русский":
                    return "Призыв предков";
                case "한국어":
                    return "고대의 부름";
                case "简体中文":
                    return "先祖召唤";
                default:
                    return "Ancestral Call";
            }
        }
        private static string AzeriteSurge_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Azeritwoge";
                case "Español":
                    return "Oleada de azerita";
                case "Français":
                    return "Afflux d’azérite";
                case "Italiano":
                    return "Impeto di Azerite";
                case "Português Brasileiro":
                    return "Surto de Azerita";
                case "Русский":
                    return "Выброс азерита";
                case "한국어":
                    return "아제라이트 쇄도";
                case "简体中文":
                    return "艾泽里特涌动";
                default:
                    return "Azerite Surge";
            }
        }




        ///<summary>spell=28730</summary>
        private static string ArcaneTorrent_SpellName()
        {
            switch (Language)
            {
                case "English": return "Arcane Torrent";
                case "Deutsch": return "Arkaner Strom";
                case "Español": return "Torrente Arcano";
                case "Français": return "Torrent arcanique";
                case "Italiano": return "Torrente Arcano";
                case "Português Brasileiro": return "Torrente Arcana";
                case "Русский": return "Волшебный поток";
                case "한국어": return "비전 격류";
                case "简体中文": return "奥术洪流";
                default: return "Arcane Torrent";
            }
        }

        ///<summary>spell=325283</summary>
        private static string AscendedBlast_SpellName()
        {
            switch (Language)
            {
                case "English": return "Ascended Blast";
                case "Deutsch": return "Woge des Aufstiegs";
                case "Español": return "Explosión ascendida";
                case "Français": return "Déflagration d’ascension";
                case "Italiano": return "Detonazione Ascesa";
                case "Português Brasileiro": return "Impacto Ascendido";
                case "Русский": return "Взрыв перерождения";
                case "한국어": return "승천의 작렬";
                case "简体中文": return "晋升冲击";
                default: return "Ascended Blast";
            }
        }

        ///<summary>spell=325020</summary>
        private static string AscendedNova_SpellName()
        {
            switch (Language)
            {
                case "English": return "Ascended Nova";
                case "Deutsch": return "Nova des Aufstiegs";
                case "Español": return "Nova ascendida";
                case "Français": return "Nova transcendée";
                case "Italiano": return "Nova Ascesa";
                case "Português Brasileiro": return "Nova Ascendida";
                case "Русский": return "Кольцо перерождения";
                case "한국어": return "승천의 회오리";
                case "简体中文": return "晋升新星";
                default: return "Ascended Nova";
            }
        }

        ///<summary>spell=312411</summary>
        private static string BagOfTricks_SpellName()
        {
            switch (Language)
            {
                case "English": return "Bag of Tricks";
                case "Deutsch": return "Trickkiste";
                case "Español": return "Bolsa de trucos";
                case "Français": return "Sac à malice";
                case "Italiano": return "Borsa di Trucchi";
                case "Português Brasileiro": return "Bolsa de Truques";
                case "Русский": return "Набор хитростей";
                case "한국어": return "비장의 묘수";
                case "简体中文": return "袋里乾坤";
                default: return "Bag of Tricks";
            }
        }

        ///<summary>spell=120360</summary>
        private static string Barrage_SpellName()
        {
            switch (Language)
            {
                case "English": return "Barrage";
                case "Deutsch": return "Sperrfeuer";
                case "Español": return "Tromba";
                case "Français": return "Barrage";
                case "Italiano": return "Sbarramento";
                case "Português Brasileiro": return "Barragem";
                case "Русский": return "Шквал";
                case "한국어": return "탄막";
                case "简体中文": return "弹幕射击";
                default: return "Barrage";
            }
        }

        ///<summary>spell=26297</summary>
        private static string Berserking_SpellName()
        {
            switch (Language)
            {
                case "English": return "Berserking";
                case "Deutsch": return "Berserker";
                case "Español": return "Rabiar";
                case "Français": return "Berserker";
                case "Italiano": return "Berserker";
                case "Português Brasileiro": return "Berserk";
                case "Русский": return "Берсерк";
                case "한국어": return "광폭화";
                case "简体中文": return "狂暴";
                default: return "Berserking";
            }
        }

        ///<summary>spell=33697</summary>
        private static string BloodFury_SpellName()
        {
            switch (Language)
            {
                case "English": return "Blood Fury";
                case "Deutsch": return "Kochendes Blut";
                case "Español": return "Furia sangrienta";
                case "Français": return "Fureur sanguinaire";
                case "Italiano": return "Furia Sanguinaria";
                case "Português Brasileiro": return "Fúria Sangrenta";
                case "Русский": return "Кровавое неистовство";
                case "한국어": return "피의 격노";
                case "简体中文": return "血性狂怒";
                default: return "Blood Fury";
            }
        }

        ///<summary>spell=325013</summary>
        private static string BoonOfTheAscended_SpellName()
        {
            switch (Language)
            {
                case "English": return "Boon of the Ascended";
                case "Deutsch": return "Segen der Aufgestiegenen";
                case "Español": return "Bendición de los Ascendidos";
                case "Français": return "Faveur des transcendés";
                case "Italiano": return "Dono degli Ascesi";
                case "Português Brasileiro": return "Dom dos Ascendidos";
                case "Русский": return "Благословение перерожденных";
                case "한국어": return "승천자의 은혜";
                case "简体中文": return "晋升者之赐";
                default: return "Boon of the Ascended";
            }
        }

        ///<summary>spell=341374</summary>
        private static string Damnation_SpellName()
        {
            switch (Language)
            {
                case "English": return "Damnation";
                case "Deutsch": return "Bezichtigung";
                case "Español": return "Condenación";
                case "Français": return "Damnation";
                case "Italiano": return "Dannazione";
                case "Português Brasileiro": return "Danação";
                case "Русский": return "Проклятие";
                case "한국어": return "아비규환";
                case "简体中文": return "咒罚";
                default: return "Damnation";
            }
        }

        ///<summary>spell=391109</summary>
        private static string DarkAscension_SpellName()
        {
            switch (Language)
            {
                case "English": return "Dark Ascension";
                case "Deutsch": return "Dunkler Aufstieg";
                case "Español": return "Ascensión oscura";
                case "Français": return "Sombre ascension";
                case "Italiano": return "Ascensione Oscura";
                case "Português Brasileiro": return "Ascensão Sombria";
                case "Русский": return "Темное вознесение";
                case "한국어": return "어둠의 승천";
                case "简体中文": return "黑暗升华";
                default: return "Dark Ascension";
            }
        }

        ///<summary>spell=341207</summary>
        private static string DarkThought_SpellName()
        {
            switch (Language)
            {
                case "English": return "Dark Thought";
                case "Deutsch": return "Dunkler Gedanke";
                case "Español": return "Pensamiento oscuro";
                case "Français": return "Sombre pensée";
                case "Italiano": return "Pensiero Oscuro";
                case "Português Brasileiro": return "Pensamento Sombrio";
                case "Русский": return "Темная мысль";
                case "한국어": return "어둠의 생각";
                case "简体中文": return "黑暗思维";
                default: return "Dark Thought";
            }
        }

        ///<summary>spell=263346</summary>
        private static string DarkVoid_SpellName()
        {
            switch (Language)
            {
                case "English": return "Dark Void";
                case "Deutsch": return "Dunkle Leere";
                case "Español": return "Vacío oscuro";
                case "Français": return "Vide sombre";
                case "Italiano": return "Vuoto Oscuro";
                case "Português Brasileiro": return "Caos Sombrio";
                case "Русский": return "Темная Бездна";
                case "한국어": return "암흑 공허";
                case "简体中文": return "幽暗虚无";
                default: return "Dark Void";
            }
        }

        ///<summary>spell=19236</summary>
        private static string DesperatePrayer_SpellName()
        {
            switch (Language)
            {
                case "English": return "Desperate Prayer";
                case "Deutsch": return "Verzweifeltes Gebet";
                case "Español": return "Rezo desesperado";
                case "Français": return "Prière du désespoir";
                case "Italiano": return "Preghiera Disperata";
                case "Português Brasileiro": return "Prece Desesperada";
                case "Русский": return "Молитва отчаяния";
                case "한국어": return "구원의 기도";
                case "简体中文": return "绝望祷言";
                default: return "Desperate Prayer";
            }
        }

        ///<summary>spell=335467</summary>
        private static string DevouringPlague_SpellName()
        {
            switch (Language)
            {
                case "English": return "Devouring Plague";
                case "Deutsch": return "Verschlingende Seuche";
                case "Español": return "Peste devoradora";
                case "Français": return "Peste dévorante";
                case "Italiano": return "Piaga Divoratrice";
                case "Português Brasileiro": return "Peste Devoradora";
                case "Русский": return "Всепожирающая чума";
                case "한국어": return "파멸의 역병";
                case "简体中文": return "噬灵疫病";
                default: return "Devouring Plague";
            }
        }

        ///<summary>spell=528</summary>
        private static string DispelMagic_SpellName()
        {
            switch (Language)
            {
                case "English": return "Dispel Magic";
                case "Deutsch": return "Magiebannung";
                case "Español": return "Disipar magia";
                case "Français": return "Dissipation de la magie";
                case "Italiano": return "Dissoluzione Magica";
                case "Português Brasileiro": return "Dissipar Magia";
                case "Русский": return "Рассеивание заклинаний";
                case "한국어": return "마법 무효화";
                case "简体中文": return "驱散魔法";
                default: return "Dispel Magic";
            }
        }

        ///<summary>spell=47585</summary>
        private static string Dispersion_SpellName()
        {
            switch (Language)
            {
                case "English": return "Dispersion";
                case "Deutsch": return "Dispersion";
                case "Español": return "Dispersión";
                case "Français": return "Dispersion";
                case "Italiano": return "Dispersione";
                case "Português Brasileiro": return "Dispersão";
                case "Русский": return "Слияние с Тьмой";
                case "한국어": return "분산";
                case "简体中文": return "消散";
                default: return "Dispersion";
            }
        }

        ///<summary>spell=122121</summary>
        private static string DivineStar_SpellName()
        {
            switch (Language)
            {
                case "English": return "Divine Star";
                case "Deutsch": return "Göttlicher Stern";
                case "Español": return "Estrella divina";
                case "Français": return "Étoile divine";
                case "Italiano": return "Stella Divina";
                case "Português Brasileiro": return "Estrela Divina";
                case "Русский": return "Божественная звезда";
                case "한국어": return "천상의 별";
                case "简体中文": return "神圣之星";
                default: return "Divine Star";
            }
        }

        ///<summary>spell=20589</summary>
        private static string EscapeArtist_SpellName()
        {
            switch (Language)
            {
                case "English": return "Escape Artist";
                case "Deutsch": return "Entfesselungskünstler";
                case "Español": return "Artista del escape";
                case "Français": return "Maître de l’évasion";
                case "Italiano": return "Artista della Fuga";
                case "Português Brasileiro": return "Artista da Fuga";
                case "Русский": return "Мастер побега";
                case "한국어": return "탈출의 명수";
                case "简体中文": return "逃命专家";
                default: return "Escape Artist";
            }
        }

        ///<summary>spell=327661</summary>
        private static string FaeGuardians_SpellName()
        {
            switch (Language)
            {
                case "English": return "Fae Guardians";
                case "Deutsch": return "Faewächter";
                case "Español": return "Sílfides guardianas";
                case "Français": return "Gardiens faë";
                case "Italiano": return "Guardiani Silfi";
                case "Português Brasileiro": return "Guardiões Feérios";
                case "Русский": return "Волшебные стражи";
                case "한국어": return "페이 수호자";
                case "简体中文": return "法夜守护者";
                default: return "Fae Guardians";
            }
        }

        ///<summary>spell=28880</summary>
        private static string GiftOfTheNaaru_SpellName()
        {
            switch (Language)
            {
                case "English": return "Gift of the Naaru";
                case "Deutsch": return "Gabe der Naaru";
                case "Español": return "Ofrenda de los naaru";
                case "Français": return "Don des Naaru";
                case "Italiano": return "Dono dei Naaru";
                case "Português Brasileiro": return "Dádiva dos Naarus";
                case "Русский": return "Дар наару";
                case "한국어": return "나루의 선물";
                case "简体中文": return "纳鲁的赐福";
                default: return "Gift of the Naaru";
            }
        }

        ///<summary>spell=120644</summary>
        private static string Halo_SpellName()
        {
            switch (Language)
            {
                case "English": return "Halo";
                case "Deutsch": return "Strahlenkranz";
                case "Español": return "Halo";
                case "Français": return "Halo";
                case "Italiano": return "Aureola";
                case "Português Brasileiro": return "Halo";
                case "Русский": return "Сияние";
                case "한국어": return "후광";
                case "简体中文": return "光晕";
                default: return "Halo";
            }
        }

        ///<summary>item=5512</summary>
        private static string Healthstone_SpellName()
        {
            switch (Language)
            {
                case "English": return "Healthstone";
                case "Deutsch": return "Gesundheitsstein";
                case "Español": return "Piedra de salud";
                case "Français": return "Pierre de soins";
                case "Italiano": return "Pietra della Salute";
                case "Português Brasileiro": return "Pedra de Vida";
                case "Русский": return "Камень здоровья";
                case "한국어": return "생명석";
                case "简体中文": return "治疗石";
                default: return "Healthstone";
            }
        }

        ///<summary>spell=20271</summary>
        private static string Judgment_SpellName()
        {
            switch (Language)
            {
                case "English": return "Judgment";
                case "Deutsch": return "Richturteil";
                case "Español": return "Sentencia";
                case "Français": return "Jugement";
                case "Italiano": return "Giudizio";
                case "Português Brasileiro": return "Julgamento";
                case "Русский": return "Правосудие";
                case "한국어": return "심판";
                case "简体中文": return "审判";
                default: return "Judgment";
            }
        }

        ///<summary>spell=73325</summary>
        private static string LeapOfFaith_SpellName()
        {
            switch (Language)
            {
                case "English": return "Leap of Faith";
                case "Deutsch": return "Glaubenssprung";
                case "Español": return "Salto de fe";
                case "Français": return "Saut de foi";
                case "Italiano": return "Balzo della Fede";
                case "Português Brasileiro": return "Salto da Fé";
                case "Русский": return "Духовное рвение";
                case "한국어": return "신의의 도약";
                case "简体中文": return "信仰飞跃";
                default: return "Leap of Faith";
            }
        }

        ///<summary>spell=255647</summary>
        private static string LightsJudgment_SpellName()
        {
            switch (Language)
            {
                case "English": return "Light's Judgment";
                case "Deutsch": return "Urteil des Lichts";
                case "Español": return "Sentencia de la Luz";
                case "Français": return "Jugement de la Lumière";
                case "Italiano": return "Giudizio della Luce";
                case "Português Brasileiro": return "Julgamento da Luz";
                case "Русский": return "Правосудие Света";
                case "한국어": return "빛의 심판";
                case "简体中文": return "圣光裁决者";
                default: return "Light's Judgment";
            }
        }

        ///<summary>spell=32375</summary>
        private static string MassDispel_SpellName()
        {
            switch (Language)
            {
                case "English": return "Mass Dispel";
                case "Deutsch": return "Massenbannung";
                case "Español": return "Disipación en masa";
                case "Français": return "Dissipation de masse";
                case "Italiano": return "Dissoluzione di Massa";
                case "Português Brasileiro": return "Dissipação em Massa";
                case "Русский": return "Массовое рассеивание";
                case "한국어": return "대규모 무효화";
                case "简体中文": return "群体驱散";
                default: return "Mass Dispel";
            }
        }

        ///<summary>spell=8092</summary>
        private static string MindBlast_SpellName()
        {
            switch (Language)
            {
                case "English": return "Mind Blast";
                case "Deutsch": return "Gedankenschlag";
                case "Español": return "Explosión mental";
                case "Français": return "Attaque mentale";
                case "Italiano": return "Detonazione Mentale";
                case "Português Brasileiro": return "Impacto Mental";
                case "Русский": return "Взрыв разума";
                case "한국어": return "정신 분열";
                case "简体中文": return "心灵震爆";
                default: return "Mind Blast";
            }
        }

        ///<summary>spell=205369</summary>
        private static string MindBomb_SpellName()
        {
            switch (Language)
            {
                case "English": return "Mind Bomb";
                case "Deutsch": return "Gedankenbombe";
                case "Español": return "Bomba mental";
                case "Français": return "Explosion mentale";
                case "Italiano": return "Bomba Mentale";
                case "Português Brasileiro": return "Bomba Psíquica";
                case "Русский": return "Мыслебомба";
                case "한국어": return "정신 폭탄";
                case "简体中文": return "心灵炸弹";
                default: return "Mind Bomb";
            }
        }

        ///<summary>spell=605</summary>
        private static string MindControl_SpellName()
        {
            switch (Language)
            {
                case "English": return "Mind Control";
                case "Deutsch": return "Gedankenkontrolle";
                case "Español": return "Control mental";
                case "Français": return "Contrôle mental";
                case "Italiano": return "Controllo Mentale";
                case "Português Brasileiro": return "Controle Mental";
                case "Русский": return "Контроль над разумом";
                case "한국어": return "정신 지배";
                case "简体中文": return "精神控制";
                default: return "Mind Control";
            }
        }

        ///<summary>spell=15407</summary>
        private static string MindFlay_SpellName()
        {
            switch (Language)
            {
                case "English": return "Mind Flay";
                case "Deutsch": return "Gedankenschinden";
                case "Español": return "Tortura mental";
                case "Français": return "Fouet mental";
                case "Italiano": return "Flagello Mentale";
                case "Português Brasileiro": return "Açoite Mental";
                case "Русский": return "Пытка разума";
                case "한국어": return "정신의 채찍";
                case "简体中文": return "精神鞭笞";
                default: return "Mind Flay";
            }
        }

        ///<summary>spell=391403</summary>
        private static string MindFlay_Insanity_SpellName()
        {
            switch (Language)
            {
                case "English": return "Mind Flay: Insanity";
                case "Deutsch": return "Gedankenschinden: Wahnsinn";
                case "Español": return "Tortura mental: demencia";
                case "Français": return "Fouet mental : insanité";
                case "Italiano": return "Flagello Mentale: Pazzia";
                case "Português Brasileiro": return "Açoite Mental: Insanidade";
                case "Русский": return "Пытка разума: безумие";
                case "한국어": return "정신의 채찍: 광기";
                case "简体中文": return "精神鞭笞：狂";
                default: return "Mind Flay: Insanity";
            }
        }

        ///<summary>spell=48045</summary>
        private static string MindSear_SpellName()
        {
            switch (Language)
            {
                case "English": return "Mind Sear";
                case "Deutsch": return "Gedankenexplosion";
                case "Español": return "Abrasamiento mental";
                case "Français": return "Incandescence mentale";
                case "Italiano": return "Risonanza Mentale";
                case "Português Brasileiro": return "Calcinação Mental";
                case "Русский": return "Иссушение разума";
                case "한국어": return "정신 불태우기";
                case "简体中文": return "精神灼烧";
                default: return "Mind Sear";
            }
        }

        ///<summary>spell=73510</summary>
        private static string MindSpike_SpellName()
        {
            switch (Language)
            {
                case "English": return "Mind Spike";
                case "Deutsch": return "Gedankenstachel";
                case "Español": return "Púa mental";
                case "Français": return "Pointe mentale";
                case "Italiano": return "Aculeo Mentale";
                case "Português Brasileiro": return "Aguilhão Mental";
                case "Русский": return "Пронзание разума";
                case "한국어": return "정신의 쐐기";
                case "简体中文": return "心灵尖刺";
                default: return "Mind Spike";
            }
        }

        ///<summary>spell=407466</summary>
        private static string MindSpike_Insanity_SpellName()
        {
            switch (Language)
            {
                case "English": return "Mind Spike: Insanity";
                case "Deutsch": return "Gedankenstachel: Wahnsinn";
                case "Español": return "Púa mental: demencia";
                case "Français": return "Pointe mentale : insanité";
                case "Italiano": return "Aculeo Mentale: Pazzia";
                case "Português Brasileiro": return "Aguilhão Mental: Insanidade";
                case "Русский": return "Пронзание разума: безумие";
                case "한국어": return "정신의 쐐기: 광기";
                case "简体中文": return "心灵尖刺：狂";
                default: return "Mind Spike: Insanity";
            }
        }

        ///<summary>spell=200174</summary>
        private static string Mindbender_SpellName()
        {
            switch (Language)
            {
                case "English": return "Mindbender";
                case "Deutsch": return "Geistbeuger";
                case "Español": return "Dominamentes";
                case "Français": return "Torve-esprit";
                case "Italiano": return "Plagiamente";
                case "Português Brasileiro": return "Dobramentes";
                case "Русский": return "Подчинитель разума";
                case "한국어": return "환각의 마귀";
                case "简体中文": return "摧心魔";
                default: return "Mindbender";
            }
        }

        ///<summary>spell=323673</summary>
        private static string Mindgames_SpellName()
        {
            switch (Language)
            {
                case "English": return "Mindgames";
                case "Deutsch": return "Gedankenspiele";
                case "Español": return "Juegos mentales";
                case "Français": return "Jeux d’esprit";
                case "Italiano": return "Giochi Mentali";
                case "Português Brasileiro": return "Jogos Mentais";
                case "Русский": return "Игры разума";
                case "한국어": return "정신 조작";
                case "简体中文": return "控心术";
                default: return "Mindgames";
            }
        }

        ///<summary>spell=10060</summary>
        private static string PowerInfusion_SpellName()
        {
            switch (Language)
            {
                case "English": return "Power Infusion";
                case "Deutsch": return "Seele der Macht";
                case "Español": return "Infusión de poder";
                case "Français": return "Infusion de puissance";
                case "Italiano": return "Infusione di Potere";
                case "Português Brasileiro": return "Infusão de Poder";
                case "Русский": return "Придание сил";
                case "한국어": return "마력 주입";
                case "简体中文": return "能量灌注";
                default: return "Power Infusion";
            }
        }

        ///<summary>spell=21562</summary>
        private static string PowerWordFortitude_SpellName()
        {
            switch (Language)
            {
                case "English": return "Power Word: Fortitude";
                case "Deutsch": return "Machtwort: Seelenstärke";
                case "Español": return "Palabra de poder: entereza";
                case "Français": return "Mot de pouvoir : Robustesse";
                case "Italiano": return "Parola del Potere: Fermezza";
                case "Português Brasileiro": return "Palavra de Poder: Fortitude";
                case "Русский": return "Слово силы: Стойкость";
                case "한국어": return "신의 권능: 인내";
                case "简体中文": return "真言术：韧";
                default: return "Power Word: Fortitude";
            }
        }

        ///<summary>spell=17</summary>
        private static string PowerWord_Shield_SpellName()
        {
            switch (Language)
            {
                case "English": return "Power Word: Shield";
                case "Deutsch": return "Machtwort: Schild";
                case "Español": return "Palabra de poder: escudo";
                case "Français": return "Mot de pouvoir : Bouclier";
                case "Italiano": return "Parola del Potere: Scudo";
                case "Português Brasileiro": return "Palavra de Poder: Escudo";
                case "Русский": return "Слово силы: Щит";
                case "한국어": return "신의 권능: 보호막";
                case "简体中文": return "真言术：盾";
                default: return "Power Word: Shield";
            }
        }

        ///<summary>spell=64044</summary>
        private static string PsychicHorror_SpellName()
        {
            switch (Language)
            {
                case "English": return "Psychic Horror";
                case "Deutsch": return "Psychisches Entsetzen";
                case "Español": return "Horror psíquico";
                case "Français": return "Horreur psychique";
                case "Italiano": return "Orrore Psichico";
                case "Português Brasileiro": return "Terror Psíquico";
                case "Русский": return "Глубинный ужас";
                case "한국어": return "정신적 두려움";
                case "简体中文": return "心灵惊骇";
                default: return "Psychic Horror";
            }
        }

        ///<summary>spell=8122</summary>
        private static string PsychicScream_SpellName()
        {
            switch (Language)
            {
                case "English": return "Psychic Scream";
                case "Deutsch": return "Psychischer Schrei";
                case "Español": return "Alarido psíquico";
                case "Français": return "Cri psychique";
                case "Italiano": return "Urlo Psichico";
                case "Português Brasileiro": return "Grito Psíquico";
                case "Русский": return "Ментальный крик";
                case "한국어": return "영혼의 절규";
                case "简体中文": return "心灵尖啸";
                default: return "Psychic Scream";
            }
        }

        ///<summary>spell=213634</summary>
        private static string PurifyDisease_SpellName()
        {
            switch (Language)
            {
                case "English": return "Purify Disease";
                case "Deutsch": return "Krankheit läutern";
                case "Español": return "Purificar enfermedad";
                case "Français": return "Purifier la maladie";
                case "Italiano": return "Purificazione Malattia";
                case "Português Brasileiro": return "Purificar Doença";
                case "Русский": return "Очищение от болезни";
                case "한국어": return "질병 정화";
                case "简体中文": return "净化疾病";
                default: return "Purify Disease";
            }
        }

        ///<summary>spell=69041</summary>
        private static string RocketBarrage_SpellName()
        {
            switch (Language)
            {
                case "English": return "Rocket Barrage";
                case "Deutsch": return "Raketenbeschuss";
                case "Español": return "Tromba de cohetes";
                case "Français": return "Barrage de fusées";
                case "Italiano": return "Raffica di Razzi";
                case "Português Brasileiro": return "Barragem de Foguetes";
                case "Русский": return "Ракетный обстрел";
                case "한국어": return "로켓 연발탄";
                case "简体中文": return "火箭弹幕";
                default: return "Rocket Barrage";
            }
        }

        ///<summary>spell=341385</summary>
        private static string SearingNightmare_SpellName()
        {
            switch (Language)
            {
                case "English": return "Searing Nightmare";
                case "Deutsch": return "Sengender Alptraum";
                case "Español": return "Pesadilla abrasadora";
                case "Français": return "Cauchemar brûlant";
                case "Italiano": return "Incubo Rovente";
                case "Português Brasileiro": return "Pesadelo Calcinante";
                case "Русский": return "Иссушающий кошмар";
                case "한국어": return "불타는 악몽";
                case "简体中文": return "灼烧梦魇";
                default: return "Searing Nightmare";
            }
        }

        ///<summary>spell=9484</summary>
        private static string ShackleUndead_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shackle Undead";
                case "Deutsch": return "Untote fesseln";
                case "Español": return "Encadenar no-muerto";
                case "Français": return "Entraves des Morts-vivants";
                case "Italiano": return "Incatena Non Morto";
                case "Português Brasileiro": return "Agrilhoar Morto-vivo";
                case "Русский": return "Сковывание нежити";
                case "한국어": return "언데드 속박";
                case "简体中文": return "束缚亡灵";
                default: return "Shackle Undead";
            }
        }

        ///<summary>spell=205385</summary>
        private static string ShadowCrash_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shadow Crash";
                case "Deutsch": return "Schattengeschoss";
                case "Español": return "Fragor de las Sombras";
                case "Français": return "Déferlante d’ombre";
                case "Italiano": return "Schianto d'Ombra";
                case "Português Brasileiro": return "Colisão de Sombras";
                case "Русский": return "Темное сокрушение";
                case "한국어": return "어둠 붕괴";
                case "简体中文": return "暗影冲撞";
                default: return "Shadow Crash";
            }
        }

        ///<summary>spell=299268</summary>
        private static string ShadowMend_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shadow Mend";
                case "Deutsch": return "Schattenheilung";
                case "Español": return "Alivio de las Sombras";
                case "Français": return "Guérison de l’ombre";
                case "Italiano": return "Cura d'Ombra";
                case "Português Brasileiro": return "Recomposição Sombria";
                case "Русский": return "Темное восстановление";
                case "한국어": return "어둠의 치유";
                case "简体中文": return "暗影愈合";
                default: return "Shadow Mend";
            }
        }

        ///<summary>spell=32379</summary>
        private static string ShadowWord_Death_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shadow Word: Death";
                case "Deutsch": return "Schattenwort: Tod";
                case "Español": return "Palabra de las Sombras: muerte";
                case "Français": return "Mot de l’ombre : Mort";
                case "Italiano": return "Parola d'Ombra: Morte";
                case "Português Brasileiro": return "Palavra Sombria: Morte";
                case "Русский": return "Слово Тьмы: Смерть";
                case "한국어": return "어둠의 권능: 죽음";
                case "简体中文": return "暗言术：灭";
                default: return "Shadow Word: Death";
            }
        }

        ///<summary>spell=589</summary>
        private static string ShadowWord_Pain_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shadow Word: Pain";
                case "Deutsch": return "Schattenwort: Schmerz";
                case "Español": return "Palabra de las Sombras: dolor";
                case "Français": return "Mot de l’ombre : Douleur";
                case "Italiano": return "Parola d'Ombra: Dolore";
                case "Português Brasileiro": return "Palavra Sombria: Dor";
                case "Русский": return "Слово Тьмы: Боль";
                case "한국어": return "어둠의 권능: 고통";
                case "简体中文": return "暗言术：痛";
                default: return "Shadow Word: Pain";
            }
        }

        ///<summary>spell=34433</summary>
        private static string Shadowfiend_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shadowfiend";
                case "Deutsch": return "Schattengeist";
                case "Español": return "Maligno de las Sombras";
                case "Français": return "Ombrefiel";
                case "Italiano": return "Spirito d'Ombra";
                case "Português Brasileiro": return "Demônio das Sombras";
                case "Русский": return "Исчадие Тьмы";
                case "한국어": return "어둠의 마귀";
                case "简体中文": return "暗影魔";
                default: return "Shadowfiend";
            }
        }

        ///<summary>spell=232698</summary>
        private static string Shadowform_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shadowform";
                case "Deutsch": return "Schattengestalt";
                case "Español": return "Forma de las Sombras";
                case "Français": return "Forme d'Ombre";
                case "Italiano": return "Forma d'Ombra";
                case "Português Brasileiro": return "Forma de Sombra";
                case "Русский": return "Облик Тьмы";
                case "한국어": return "어둠의 형상";
                case "简体中文": return "暗影形态";
                default: return "Shadowform";
            }
        }

        ///<summary>spell=58984</summary>
        private static string Shadowmeld_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shadowmeld";
                case "Deutsch": return "Schattenmimik";
                case "Español": return "Fusión de las sombras";
                case "Français": return "Camouflage dans l'ombre";
                case "Italiano": return "Fondersi nelle Ombre";
                case "Português Brasileiro": return "Fusão Sombria";
                case "Русский": return "Слиться с тенью";
                case "한국어": return "그림자 숨기";
                case "简体中文": return "影遁";
                default: return "Shadowmeld";
            }
        }

        ///<summary>spell=15487</summary>
        private static string Silence_SpellName()
        {
            switch (Language)
            {
                case "English": return "Silence";
                case "Deutsch": return "Stille";
                case "Español": return "Silencio";
                case "Français": return "Silence";
                case "Italiano": return "Silenzio";
                case "Português Brasileiro": return "Silêncio";
                case "Русский": return "Безмолвие";
                case "한국어": return "침묵";
                case "简体中文": return "沉默";
                default: return "Silence";
            }
        }

        ///<summary>spell=324724</summary>
        private static string UnholyNova_SpellName()
        {
            switch (Language)
            {
                case "English": return "Unholy Nova";
                case "Deutsch": return "Unheilige Nova";
                case "Español": return "Nova profana";
                case "Français": return "Nova impie";
                case "Italiano": return "Nova Empia";
                case "Português Brasileiro": return "Nova Profana";
                case "Русский": return "Нечестивое кольцо";
                case "한국어": return "부정한 폭발";
                case "简体中文": return "邪恶新星";
                default: return "Unholy Nova";
            }
        }

        ///<summary>spell=15286</summary>
        private static string VampiricEmbrace_SpellName()
        {
            switch (Language)
            {
                case "English": return "Vampiric Embrace";
                case "Deutsch": return "Vampirumarmung";
                case "Español": return "Abrazo vampírico";
                case "Français": return "Étreinte vampirique";
                case "Italiano": return "Abbraccio Vampirico";
                case "Português Brasileiro": return "Abraço Vampírico";
                case "Русский": return "Объятия вампира";
                case "한국어": return "흡혈의 선물";
                case "简体中文": return "吸血鬼的拥抱";
                default: return "Vampiric Embrace";
            }
        }

        ///<summary>spell=34914</summary>
        private static string VampiricTouch_SpellName()
        {
            switch (Language)
            {
                case "English": return "Vampiric Touch";
                case "Deutsch": return "Vampirberührung";
                case "Español": return "Toque vampírico";
                case "Français": return "Toucher vampirique";
                case "Italiano": return "Tocco Vampirico";
                case "Português Brasileiro": return "Toque Vampírico";
                case "Русский": return "Прикосновение вампира";
                case "한국어": return "흡혈의 손길";
                case "简体中文": return "吸血鬼之触";
                default: return "Vampiric Touch";
            }
        }

        ///<summary>spell=205448</summary>
        private static string VoidBolt_SpellName()
        {
            switch (Language)
            {
                case "English": return "Void Bolt";
                case "Deutsch": return "Leerenblitz";
                case "Español": return "Descarga del Vacío";
                case "Français": return "Éclair de Vide";
                case "Italiano": return "Dardo del Vuoto";
                case "Português Brasileiro": return "Seta Caótica";
                case "Русский": return "Стрела Бездны";
                case "한국어": return "공허의 화살";
                case "简体中文": return "虚空箭";
                default: return "Void Bolt";
            }
        }
                private static string PowerWordShield_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Machtwort: Schild";
                case "Español":
                    return "Palabra de poder: escudo";
                case "Français":
                    return "Mot de pouvoir : Bouclier";
                case "Italiano":
                    return "Parola del Potere: Scudo";
                case "Português Brasileiro":
                    return "Palavra de Poder: Escudo";
                case "Русский":
                    return "Слово силы: Щит";
                case "한국어":
                    return "신의 권능: 보호막";
                case "简体中文":
                    return "真言术：盾";
                default:
                    return "Power Word: Shield";
            }
        }

        ///<summary>spell=228260</summary>
        private static string VoidEruption_SpellName()
        {
            switch (Language)
            {
                case "English": return "Void Eruption";
                case "Deutsch": return "Leereneruption";
                case "Español": return "Erupción del Vacío";
                case "Français": return "Éruption du Vide";
                case "Italiano": return "Eruzione del Vuoto";
                case "Português Brasileiro": return "Erupção do Caos";
                case "Русский": return "Извержение Бездны";
                case "한국어": return "공허 방출";
                case "简体中文": return "虚空爆发";
                default: return "Void Eruption";
            }
        }

        ///<summary>spell=263165</summary>
        private static string VoidTorrent_SpellName()
        {
            switch (Language)
            {
                case "English": return "Void Torrent";
                case "Deutsch": return "Leerenstrom";
                case "Español": return "Torrente del Vacío";
                case "Français": return "Torrent du Vide";
                case "Italiano": return "Torrente del Vuoto";
                case "Português Brasileiro": return "Torrente do Caos";
                case "Русский": return "Поток Бездны";
                case "한국어": return "공허의 격류";
                case "简体中文": return "虚空洪流";
                default: return "Void Torrent";
            }
        }

        ///<summary>spell=194249</summary>
        private static string Voidform_SpellName()
        {
            switch (Language)
            {
                case "English": return "Voidform";
                case "Deutsch": return "Leerengestalt";
                case "Español": return "Forma del Vacío";
                case "Français": return "Forme du Vide";
                case "Italiano": return "Forma del Vuoto";
                case "Português Brasileiro": return "Forma do Caos";
                case "Русский": return "Облик Бездны";
                case "한국어": return "공허의 형상";
                case "简体中文": return "虚空形态";
                default: return "Voidform";
            }
        }
        private static string Fade_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Verblassen";
                case "Español":
                    return "Desvanecerse";
                case "Français":
                    return "Disparition";
                case "Italiano":
                    return "Trasparenza";
                case "Português Brasileiro":
                    return "Desvanecer";
                case "Русский":
                    return "Уход в тень";
                case "한국어":
                    return "소실";
                case "简体中文":
                    return "渐隐术";
                default:
                    return "Fade";
            }
        }
        private static string LeapofFaith_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Glaubenssprung";
                case "Español":
                    return "Salto de fe";
                case "Français":
                    return "Saut de foi";
                case "Italiano":
                    return "Balzo della Fede";
                case "Português Brasileiro":
                    return "Salto da Fé";
                case "Русский":
                    return "Духовное рвение";
                case "한국어":
                    return "신의의 도약";
                case "简体中文":
                    return "信仰飞跃";
                default:
                    return "Leap of Faith";
            }
        }
        private static string Voidwraith_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Leergespenst";
        case "Español":
            return "Ánima del Vacío";
        case "Français":
            return "Âme en peine du Vide";
        case "Italiano":
            return "Presenza del Vuoto";
        case "Português Brasileiro":
            return "Aparição Caótica";
        case "Русский":
            return "Призрак Бездны";
        case "한국어":
            return "공허의 망령";
        case "简体中文":
            return "虚空幽灵";
        default:
            return "Voidwraith";
    }
}
private static string VoidBlast_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Leerenschlag";
        case "Español":
            return "Explosión del Vacío";
        case "Français":
            return "Trait de Vide";
        case "Italiano":
            return "Detonazione del Vuoto";
        case "Português Brasileiro":
            return "Impacto do Caos";
        case "Русский":
            return "Вспышка Бездны";
        case "한국어":
            return "공허의 폭발";
        case "简体中文":
            return "虚空冲击";
        default:
            return "Void Blast";
    }
}

        private static string BodyAndSoul_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Körper und Geist";
                case "Español":
                    return "Cuerpo y mente";
                case "Français":
                    return "Corps et âme";
                case "Italiano":
                    return "Anima e Corpo";
                case "Português Brasileiro":
                    return "Corpo e Alma";
                case "Русский":
                    return "Тело и душа";
                case "한국어":
                    return "육체와 영혼";
                case "简体中文":
                    return "身心合一";
                default:
                    return "Body and Soul";
            }
        }
         private static string AngelicFeather_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Engelsfeder";
                case "Español":
                    return "Pluma angélica";
                case "Français":
                    return "Plume angélique";
                case "Italiano":
                    return "Piuma Angelica";
                case "Português Brasileiro":
                    return "Pena Angelical";
                case "Русский":
                    return "Божественное перышко";
                case "한국어":
                    return "천사의 깃털";
                case "简体中文":
                    return "天堂之羽";
                default:
                    return "Angelic Feather";
            }
        }
        private static string FlashHeal_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Blitzheilung";
                case "Español":
                    return "Sanación relámpago";
                case "Français":
                    return "Soins rapides";
                case "Italiano":
                    return "Cura Rapida";
                case "Português Brasileiro":
                    return "Cura Célere";
                case "Русский":
                    return "Быстрое исцеление";
                case "한국어":
                    return "순간 치유";
                case "简体中文":
                    return "快速治疗";
                default:
                    return "Flash Heal";
            }
        }
        private static string CosmicAscension_NPCName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Kosmischer Aufstieg";
        case "Español":
            return "Ascensión cósmica";
        case "Français":
            return "Ascension cosmique";
        case "Italiano":
            return "Ascensione Cosmica";
        case "Português Brasileiro":
            return "Ascensão Cósmica";
        case "Русский":
            return "Космическое вознесение";
        case "한국어":
            return "우주의 승천";
        case "简体中文":
            return "星宇飞升";
        default:
            return "Cosmic Ascension";
    }
}
private static string DevouringRift_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Leerenriss";
        case "Español":
            return "Falla de Vacío";
        case "Français":
            return "Faille du Vide";
        case "Italiano":
            return "Fenditura del Vuoto";
        case "Português Brasileiro":
            return "Fissura do Caos";
        case "Русский":
            return "Разлом Бездны";
        case "한국어":
            return "공허의 균열";
        case "简体中文":
            return "虚空裂隙";
        default:
            return "Void Rift";
    }
}
private static string DemonicHealthstone_ItemlName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Dämonischer Gesundheitsstein";
        case "Español":
            return "Piedra de salud demoníaca";
        case "Français":
            return "Pierre de soins démoniaque";
        case "Italiano":
            return "Pietra della Salute Demoniaca";
        case "Português Brasileiro":
            return "Pedra de Vida Demoníaca";
        case "Русский":
            return "Демонический камень здоровья";
        case "한국어":
            return "악마의 생명석";
        case "简体中文":
            return "恶魔治疗石";
        default:
            return "Demonic Healthstone";
    }
}
        ///<summary>spell=357214</summary>
        private static string WingBuffet_SpellName()
        {
            switch (Language)
            {
                case "English": return "Wing Buffet";
                case "Deutsch": return "Flügelstoß";
                case "Español": return "Sacudida de alas";
                case "Français": return "Frappe des ailes";
                case "Italiano": return "Battito d'Ali";
                case "Português Brasileiro": return "Bofetada de Asa";
                case "Русский": return "Взмах крыльями";
                case "한국어": return "폭풍 날개";
                case "简体中文": return "飞翼打击";
                default: return "Wing Buffet";
            }
        }
private static string VoidVolley_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Leerensalve";
        case "Español":
            return "Salva del Vacío";
        case "Français":
            return "Salve du Vide";
        case "Italiano":
            return "Raffica del Vuoto";
        case "Português Brasileiro":
            return "Salva do Caos";
        case "Русский":
            return "Залп Бездны";
        case "한국어":
            return "공허 연사";
        case "简体中文":
            return "虚空齐射";
        default:
            return "Void Volley";
    }
}
        
    private static string Bloodlust_BloodlustEffectName()
    {
        switch (Language)
        {
            case "Deutsch":
                return "Kampfrausch";
            case "Español":
                return "Ansia de sangre";
            case "Français":
                return "Furie sanguinaire";
            case "Italiano":
                return "Brama di Sangue";
            case "Português Brasileiro":
                return "Sede de Sangue";
            case "Русский":
                return "Жажда крови";
            case "한국어":
                return "피의 욕망";
            case "简体中文":
                return "嗜血";
            default:
                return "Bloodlust";
        }
    }
    private static string Heroism_BloodlustEffectName()
    {
        switch (Language)
        {
            case "Deutsch":
                return "Heldentum";
            case "Español":
                return "Heroísmo";
            case "Français":
                return "Héroïsme";
            case "Italiano":
                return "Eroismo";
            case "Português Brasileiro":
                return "Heroísmo";
            case "Русский":
                return "Героизм";
            case "한국어":
                return "영웅심";
            case "简体中文":
                return "英勇";
            default:
                return "Heroism";
        }
    }
    private static string TimeWarp_BloodlustEffectName()
    {
        switch (Language)
        {
            case "Deutsch":
                return "Zeitkrümmung";
            case "Español":
                return "Distorsión temporal";
            case "Français":
                return "Distorsion temporelle";
            case "Italiano":
                return "Distorsione Temporale";
            case "Português Brasileiro":
                return "Distorção Temporal";
            case "Русский":
                return "Искажение времени";
            case "한국어":
                return "시간 왜곡";
            case "简体中文":
                return "时间扭曲";
            default:
                return "Time Warp";
        }
    }
    private static string PrimalRage_BloodlustEffectName()
    {
        switch (Language)
        {
            case "Deutsch":
                return "Urtümliche Wut";
            case "Español":
                return "Rabia primigenia";
            case "Français":
                return "Rage primordiale";
            case "Italiano":
                return "Rabbia Primordiale";
            case "Português Brasileiro":
                return "Fúria Primata";
            case "Русский":
                return "Исступление";
            case "한국어":
                return "원초적 분노";
            case "简体中文":
                return "原始暴怒";
            default:
                return "Primal Rage";
        }
    }
    private static string DrumsofDeathlyFerocity_BloodlustEffectName()
    {
        switch (Language)
        {
            case "Deutsch":
                return "Trommeln der tödlichen Wildheit";
            case "Español":
                return "Tambores de ferocidad mortífera";
            case "Français":
                return "Tambours de férocité mortelle";
            case "Italiano":
                return "Tamburi della Ferocia Letale";
            case "Português Brasileiro":
                return "Tambores da Ferocidade Mortífera";
            case "Русский":
                return "Барабаны смертельной свирепости";
            case "한국어":
                return "치명적인 야성의 북";
            case "简体中文":
                return "死亡凶蛮战鼓";
            default:
                return "Drums of Deathly Ferocity";
        }
    }


        #endregion

        //Copy This
        string EpicSettingsAddonName = "YourAddonIsBroken";
        public static string ReadAddonName(){
            string fileName = AppDomain.CurrentDomain.BaseDirectory + @"Epic\AddonName.txt";
            string text = File.ReadAllText(fileName);
            return text;
        }
        //--------------------------------------------------------------------

        int LastBossModIdCasted = 0;

        List<EpicToggle> EpicToggles = new List<EpicToggle>();
        List<EpicSetting> EpicSettings = new List<EpicSetting>();

        List<string> GroupUnits;
        private List<string> m_RaceList = new List<string> { "human", "earthen","dwarf", "nightelf", "gnome", "draenei", "pandaren", "orc", "scourge", "tauren", "troll", "bloodelf", "goblin", "worgen", "voidelf", "lightforgeddraenei", "highmountaintauren", "nightborne", "zandalaritroll", "magharorc", "kultiran", "darkirondwarf", "vulpera", "mechagnome" };

        List<string> FriendlyDebuffMagic = ReadFriendlyDebuffMagicList();
        List<string> FriendlyDebuffPoison = ReadFriendlyDebuffPoisonList();
        List<string> FriendlyDebuffDisease = ReadFriendlyDebuffDiseaseList();
        List<string> ForceCombatNPC = ForceCombatNPCList();

        public static List<string> ReadFriendlyDebuffDiseaseList(){

            string fileName = AppDomain.CurrentDomain.BaseDirectory + @"Rotations\Epic_Retail_Lists\FriendlyDebuffDisease.txt";

            string[] lines = File.ReadAllLines(fileName);

            return lines.ToList();
        }

        public static List<string> ReadFriendlyDebuffPoisonList(){
            string fileName = AppDomain.CurrentDomain.BaseDirectory + @"Rotations\Epic_Retail_Lists\FriendlyDebuffPoison.txt";

            string[] lines = File.ReadAllLines(fileName);

            return lines.ToList();
        }
        public static List<string> ReadFriendlyDebuffMagicList(){
            string fileName = AppDomain.CurrentDomain.BaseDirectory + @"Rotations\Epic_Retail_Lists\FriendlyDebuffMagic.txt";

            string[] lines = File.ReadAllLines(fileName);

            return lines.ToList();
        }
        
        public static List<string> ForceCombatNPCList(){
            string fileName = AppDomain.CurrentDomain.BaseDirectory + @"Rotations\Epic_Retail_Lists\ForceCombatNPC.txt";

            string[] lines = File.ReadAllLines(fileName);

            return lines.ToList();
        }

        List<string> MouseoverQueues;
        List<string> CursorQueues;
        List<string> PlayerQueues;
        List<string> FocusQueues;
        List<string> TargetQueues;


        List<string> BuffList;
        List<string> BloodlustEffects;

         Dictionary<int, string> SpellCasts = new Dictionary<int, string>();
         Dictionary<int, string> MacroCasts = new Dictionary<int, string>();

        Dictionary<int, string> Queues = new Dictionary<int, string>();

        bool HealthstoneUsed = false;

        Stopwatch[] DispelPartyHelper = new Stopwatch[4];
        Stopwatch DispelPlayerHelper = new Stopwatch();
        Stopwatch SwapTargetHelper = new Stopwatch();
        Stopwatch CastDelay = new Stopwatch();
        Stopwatch DeathAndDecay = new Stopwatch();
        Stopwatch StandingHelper = new Stopwatch();
        Stopwatch MovingHelper = new Stopwatch();

        Stopwatch DebugHelper = new Stopwatch();

        

        bool authorized = false;    
        private string URL = "http://185.163.125.160:8000/check/";
        private string URL_Trial = "http://185.163.125.160:8000/check/";
        private string type= "pve_aio";
        private string bothType= "both_aio";
        private string trialType= "pve_trial";
        private string bothTrialType= "both_trial";
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
            Stream buf = client.OpenRead(URL+Inferno.GetHWID()+"/"+type);
            StreamReader sr = new StreamReader(buf);
            string s = sr.ReadToEnd();
            if( s != "false"){
                string info = (string)Inferno.GetHWID()+ this.type;
                var result = Decrypt(s, strKey);
                string date = result.Substring(result.Length-10);
                bool test = result.Substring(0,result.Length-10).Equals(info);
                if(test){
                    Inferno.PrintMessage("You have a valid License, Thank you for your support .", Color.Blue);
                    Inferno.PrintMessage("License Valid Until: " + date, Color.Blue);
                    authorized = true;
                    return;
                }
            }
            else{// Test Trial access
                buf = client.OpenRead(URL_Trial+Inferno.GetHWID()+"/"+trialType);
                sr = new StreamReader(buf);
                s = sr.ReadToEnd();
                if( s != "false"){
                    string info = (string)Inferno.GetHWID()+ this.trialType;
                    var result = Decrypt(s, strKey);
                    string date = result.Substring(result.Length-10);
                    bool test = result.Substring(0,result.Length-10).Equals(info);
                    if(test){
                        Inferno.PrintMessage("You have a valid License, Thank you for your support .", Color.Blue);
                        Inferno.PrintMessage("License Valid Until: " + date, Color.Blue);
                        authorized = true;
                        return;
                    }
                }else{
                    buf = client.OpenRead(URL_Trial+Inferno.GetHWID()+"/"+bothType);
                    sr = new StreamReader(buf);
                    s = sr.ReadToEnd();
                    if( s != "false"){
                        string info = (string)Inferno.GetHWID()+ this.bothType;
                        var result = Decrypt(s, strKey);
                        string date = result.Substring(result.Length-10);
                        bool test = result.Substring(0,result.Length-10).Equals(info);
                        if(test){
                            Inferno.PrintMessage("You have a valid License, Thank you for your support .", Color.Blue);
                            Inferno.PrintMessage("License Valid Until: " + date, Color.Blue);
                            authorized = true;
                            return;
                        }
                    }else{
                        buf = client.OpenRead(URL_Trial+Inferno.GetHWID()+"/"+bothTrialType);
                        sr = new StreamReader(buf);
                        s = sr.ReadToEnd();
                        if( s != "false"){
                            string info = (string)Inferno.GetHWID()+ this.bothTrialType;
                            var result = Decrypt(s, strKey);
                            string date = result.Substring(result.Length-10);
                            bool test = result.Substring(0,result.Length-10).Equals(info);
                            if(test){
                                Inferno.PrintMessage("You have a valid License, Thank you for your support .", Color.Blue);
                                Inferno.PrintMessage("License Valid Until: " + date, Color.Blue);
                                authorized = true;
                                return;
                            }
                        }
                    }
                }
            }
            // if we go here it is unauthorized
            authorized = false;
            Inferno.PrintMessage("You Don't have Active License.", Color.Red);
            Inferno.PrintMessage("Contact BoomK for more info.", Color.Red);
        }

        public override void LoadSettings(){
            Settings.Add(new Setting("Game Client Language", new List<string>(){"English", "Deutsch", "Español", "Français", "Italiano", "Português Brasileiro", "Русский", "한국어", "简体中文"}, "English"));
            Settings.Add(new Setting("Race:", m_RaceList, "orc"));
            Settings.Add(new Setting("Latency: ", 0, 1000, 20));
            Settings.Add(new Setting("Quick Delay: ", 50, 1000, 50));
            Settings.Add(new Setting("Slow Delay: ", 50, 1000, 100));
            //DEBUG ADD
            Settings.Add(new Setting("Use Debug:", false));
        }

        public override void Initialize(){
            Check();
            if(authorized){
            }

            //Copy This
            EpicSettingsAddonName = ReadAddonName();

            //DEBUG ADD
            if(GetCheckBox("Use Debug:")){
            Inferno.DebugMode();
            }
            

            Language = GetDropDown("Game Client Language");

            Inferno.PrintMessage("Epic Rotations Shadow Priest", Color.Purple);
            Inferno.PrintMessage("Phase1 (Midnight)", Color.Purple);
            Inferno.PrintMessage("By BoomK", Color.Purple);
            Inferno.PrintMessage(" ");
            
            Inferno.PrintMessage("---------------------------------", Color.Blue);
            Inferno.PrintMessage("For list of commands Join Discord", Color.Blue);
            Inferno.PrintMessage("---------------------------------", Color.Blue);
            Inferno.PrintMessage(" ");
            Inferno.PrintMessage("https://discord.gg/SAZmqEYXwc", Color.Purple);
            Inferno.PrintMessage(" ");
            Inferno.PrintMessage("---------------------------------", Color.Blue);
            Inferno.PrintMessage("For list of commands Join Discord", Color.Blue);
            Inferno.PrintMessage("---------------------------------", Color.Blue);
            Inferno.PrintMessage(" ");
            Inferno.PrintMessage("---------------------------------", Color.Blue);

            //Inferno.PrintMessage("Found Settings Addon: "+EpicSettingsAddonName, Color.Purple);

            Inferno.PrintMessage(" ");
            Inferno.PrintMessage("---------------------------------", Color.Blue);

            Inferno.Latency = GetSlider("Latency: ");
            Inferno.QuickDelay = GetSlider("Quick Delay: ");
            Inferno.SlowDelay  = GetSlider("Slow Delay: ");
            #region Racial Spells
            if (GetDropDown("Race:") == "draenei")
            {
                Spellbook.Add(GiftOfTheNaaru_SpellName()); //28880
            }
            if (GetDropDown("Race:") == "earthen")
            {
                Spellbook.Add(AzeriteSurge_SpellName()); //28880
            }

            if (GetDropDown("Race:") == "dwarf")
            {
                Spellbook.Add(Stoneform_SpellName()); //20594
            }

            if (GetDropDown("Race:") == "gnome")
            {
                Spellbook.Add(EscapeArtist_SpellName()); //20589
            }

            if (GetDropDown("Race:") == "human")
            {
                Spellbook.Add(WillToSurvive_SpellName()); //59752
            }

            if (GetDropDown("Race:") == "lightforgeddraenei")
            {
                Spellbook.Add(LightsJudgment_SpellName()); //255647
            }

            if (GetDropDown("Race:") == "darkirondwarf")
            {
                Spellbook.Add(Fireblood_SpellName()); //265221
            }

            if (GetDropDown("Race:") == "goblin")
            {
                Spellbook.Add(RocketBarrage_SpellName()); //69041
            }

            if (GetDropDown("Race:") == "tauren")
            {
                Spellbook.Add(WarStomp_SpellName()); //20549
            }

            if (GetDropDown("Race:") == "troll")
            {
                Spellbook.Add(Berserking_SpellName()); //26297
            }

            if (GetDropDown("Race:") == "scourge")
            {
                Spellbook.Add(WillOfTheForsaken_SpellName()); //7744
            }

            if (GetDropDown("Race:") == "nightborne")
            {
                Spellbook.Add(ArcanePulse_SpellName()); //260364
            }

            if (GetDropDown("Race:") == "highmountaintauren")
            {
                Spellbook.Add(BullRush_SpellName()); //255654
            }

            if (GetDropDown("Race:") == "magharorc")
            {
                Spellbook.Add(AncestralCall_SpellName()); //274738
            }

            if (GetDropDown("Race:") == "vulpera")
            {
                Spellbook.Add(BagOfTricks_SpellName()); //312411
            }

            if (GetDropDown("Race:") == "orc")
            {
                Spellbook.Add(BloodFury_SpellName()); //2057233702, 33697
            }

            if (GetDropDown("Race:") == "bloodelf")
            {
                Spellbook.Add(ArcaneTorrent_SpellName()); //28730, 25046, 50613, 6917980483, 129597
            }

            if (GetDropDown("Race:") == "nightelf")
            {
                Spellbook.Add(Shadowmeld_SpellName()); //58984
            }
            #endregion
            
            //Spells
            BuffList = new List<string>(){};

            BloodlustEffects = new List<string>
            {
                Bloodlust_BloodlustEffectName() , Heroism_BloodlustEffectName(), TimeWarp_BloodlustEffectName(), PrimalRage_BloodlustEffectName(), DrumsofDeathlyFerocity_BloodlustEffectName()
            };

            //Talents
            Talents.Add("123456");
   
            // MacroCasts.Add(1, "TopTrinket");
            // MacroCasts.Add(2, "TopTrinketPlayer");
            // MacroCasts.Add(3, "TopTrinketCursor");
            // MacroCasts.Add(4, "TopTrinketFocus");
            // MacroCasts.Add(5, "BottomTrinket");
            // MacroCasts.Add(6, "BottomTrinketPlayer");
            // MacroCasts.Add(7, "BottomTrinketCursor");
            // MacroCasts.Add(8, "BottomTrinketFocus");
            // MacroCasts.Add(100, "Next");

            //MacroCasts.Add(26, "MO_"+AdaptiveSwarm_SpellName());
            // MacroCasts.Add(21, "Healthstone");
            // MacroCasts.Add(50, "HealingPotion");
            // MacroCasts.Add(500, "DPSPotion");
            //MacroCasts.Add(1, "TopTrinket");
            //MacroCasts.Add(2, "BottomTrinket");
                        //Copy pots
            MacroCasts.Add(212265, "DPSPotion");
            MacroCasts.Add(212264, "DPSPotion");
            MacroCasts.Add(212263, "DPSPotion");
            MacroCasts.Add(212259, "DPSPotion");
            MacroCasts.Add(212257, "DPSPotion");
            MacroCasts.Add(212258, "DPSPotion");
            MacroCasts.Add(232805, "BestInSlotCaster");
            MacroCasts.Add(232526, "BestInSlotCaster");
            MacroCasts.Add(473402, "BestInSlotCaster");
            MacroCasts.Add(3, "BestInSlotCaster");

            //Pair ids with casts
            //SpellCasts.Add(SpellId, Example_SpellName());
                //SpellCasts.Add(325013 , BoonOfTheAscended_SpellName()); //325013
                SpellCasts.Add(325020 , AscendedNova_SpellName()); //325020
                SpellCasts.Add(325283 , AscendedBlast_SpellName()); //325283
                SpellCasts.Add(324724 , UnholyNova_SpellName()); //324724
                //SpellCasts.Add(327661 , FaeGuardians_SpellName()); //327661
                SpellCasts.Add(323673 , Mindgames_SpellName()); //323673
                SpellCasts.Add(15487 , Silence_SpellName()); //15487
                SpellCasts.Add(19236 , DesperatePrayer_SpellName()); //19236
                SpellCasts.Add(528 , DispelMagic_SpellName()); //528
                SpellCasts.Add(122121 , DivineStar_SpellName()); //122121
                SpellCasts.Add(120644 , Halo_SpellName()); //120644
                //SpellCasts.Add(73325 , LeapOfFaith_SpellName()); //73325
                SpellCasts.Add(32375 , MassDispel_SpellName()); //32375
                SpellCasts.Add(8092 , MindBlast_SpellName()); //8092
                //SpellCasts.Add(605 , MindControl_SpellName()); //605
                SpellCasts.Add(10060 , PowerInfusion_SpellName()); //10060
                SpellCasts.Add(21562 , PowerWordFortitude_SpellName()); //21562
                SpellCasts.Add(17 , PowerWord_Shield_SpellName()); //17
                SpellCasts.Add(8122 , PsychicScream_SpellName()); //8122
                SpellCasts.Add(9484 , ShackleUndead_SpellName()); //9484
                SpellCasts.Add(32379 , ShadowWord_Death_SpellName()); //32379
                SpellCasts.Add(589 , ShadowWord_Pain_SpellName()); //589
                SpellCasts.Add(341374 , Damnation_SpellName()); //341374
                SpellCasts.Add(391109 , DarkAscension_SpellName()); //391109
                SpellCasts.Add(263346 , DarkVoid_SpellName()); //263346
                SpellCasts.Add(335467 , DevouringPlague_SpellName()); //335467
                SpellCasts.Add(47585 , Dispersion_SpellName()); //47585
                SpellCasts.Add(205369 , MindBomb_SpellName()); //205369
                SpellCasts.Add(15407 , MindFlay_SpellName()); //15407
                SpellCasts.Add(391403 , MindFlay_Insanity_SpellName()); //391403
                SpellCasts.Add(48045 , MindSear_SpellName()); //48045
                SpellCasts.Add(73510 , MindSpike_SpellName()); //73510
                SpellCasts.Add(407466 , MindSpike_Insanity_SpellName()); //407466
                SpellCasts.Add(200174 , Mindbender_SpellName()); //200174
                //SpellCasts.Add(64044 , PsychicHorror_SpellName()); //64044
                SpellCasts.Add(213634 , PurifyDisease_SpellName()); //213634
                SpellCasts.Add(341385 , SearingNightmare_SpellName()); //341385
                SpellCasts.Add(205385 , ShadowCrash_SpellName()); //205385
                SpellCasts.Add(457042 , ShadowCrash_SpellName()); //457042
                SpellCasts.Add(34433 , Shadowfiend_SpellName()); //34433
                SpellCasts.Add(232698 , Shadowform_SpellName()); //232698
                SpellCasts.Add(15286 , VampiricEmbrace_SpellName()); //15286
                SpellCasts.Add(34914 , VampiricTouch_SpellName()); //34914
                SpellCasts.Add(205448 , VoidBolt_SpellName()); //205448
                SpellCasts.Add(228260 , VoidEruption_SpellName()); //228260
                SpellCasts.Add(263165 , VoidTorrent_SpellName()); //263165
                SpellCasts.Add(194249 , Voidform_SpellName()); //194249
                SpellCasts.Add(451235 , Voidwraith_SpellName()); //451235
                SpellCasts.Add(450983 , VoidBlast_SpellName()); //450983
                SpellCasts.Add(232633  , ArcaneTorrent_SpellName()); //232633 
                SpellCasts.Add(1242173  , VoidVolley_SpellName()); //1242173 
                

            foreach(var s in SpellCasts){
                if(!Spellbook.Contains(s.Value))
                    Spellbook.Add(s.Value);
            }

            foreach(string b in BuffList){
                Buffs.Add(b);
            }

            foreach (string Buff in BloodlustEffects)
            {
                Buffs.Add(Buff);
            }



            // these macros will always have cusX to cusY so we can change the body in customfunction
            Macros.Add("PowerInfusionName1", "/stopcast\\n/cast [@Name1] Power Infusion"); //Cus1
            Macros.Add("PowerInfusionName2", "/stopcast\\n/cast [@Name2] Power Infusion"); //Cus2
            Macros.Add("PowerInfusionName3", "/stopcast\\n/cast [@Name3] Power Infusion"); //Cus3
            Macros.Add("HealingPotion", "/use Healing Potion"); //Cus4
            Macros.Add("DPSPotion", "/use DPS Potion"); //Cus5

            MacroCasts.Add(1, "TopTrinket");
            MacroCasts.Add(2, "BottomTrinket");

             Macros.Add("TemperedPot", "/use "+TemperedPotion_SpellName()); 
             Macros.Add("UnwaveringFocusPot", "/use "+PotionofUnwaveringFocus_SpellName()); 


            //Lists with spells to use with queues
            MouseoverQueues = new List<string>(){
                //LeapofFaith_SpellName(),
                Silence_SpellName(),
                PowerInfusion_SpellName()
            

            };
            CursorQueues = new List<string>(){
                ShadowCrash_SpellName(),
                MassDispel_SpellName(),
                
            };
            PlayerQueues = new List<string>(){
                PowerWord_Shield_SpellName(),
                AngelicFeather_SpellName(),
                Dispersion_SpellName(),
                VampiricEmbrace_SpellName(),
                PowerWordFortitude_SpellName(),
                FlashHeal_SpellName(),
                Fade_SpellName(),
                PsychicScream_SpellName(),
                PowerInfusion_SpellName(),
                //WingBuffet_SpellName()
       
            };
            FocusQueues = new List<string>(){
                PowerInfusion_SpellName(),


            };
            TargetQueues = new List<string>(){
                Silence_SpellName(),
                DispelMagic_SpellName(),
                ShackleUndead_SpellName(),
                PurifyDisease_SpellName(),
            };


            //A Macro that resets all spell queues to false
            Macros.Add("ResetQueues", "/"+EpicSettingsAddonName+" resetqueues");

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
                epicQueues += "if "+EpicSettingsAddonName+".Queues[\""+"mouseover"+s.ToLower()+"\"] then queue = "+q+" end\n";
                //Part of the Custom function to generate a list of commands inside the Settings window
                epicQueuesInfo += (EpicSettingsAddonName+".AddCommandInfo(5, \"/"+EpicSettingsAddonName+" cast mouseover "+s+"\")\n");
                q++;
                //Adding the spell to the spellbook
                if(!Spellbook.Contains(s))
                    Spellbook.Add(s);
            }
            foreach(string s in CursorQueues){
                Macros.Add("C_"+s, "/cast [@cursor] "+s);
                Queues.Add(q, "C_"+s);
                epicQueues += "if "+EpicSettingsAddonName+".Queues[\""+"cursor"+s.ToLower()+"\"] then queue = "+q+" end\n";
                epicQueuesInfo += (EpicSettingsAddonName+".AddCommandInfo(4, \"/"+EpicSettingsAddonName+" cast cursor "+s+"\")\n");
                q++;
                if(!Spellbook.Contains(s))
                    Spellbook.Add(s);
            }
            foreach(string s in PlayerQueues){
                Macros.Add("P_"+s, "/cast [@player] "+s);
                Queues.Add(q, "P_"+s);
                epicQueues += "if "+EpicSettingsAddonName+".Queues[\""+"player"+s.ToLower()+"\"] then queue = "+q+" end\n";
                epicQueuesInfo += (EpicSettingsAddonName+".AddCommandInfo(2, \"/"+EpicSettingsAddonName+" cast player "+s+"\")\n");
                q++;
                if(!Spellbook.Contains(s))
                    Spellbook.Add(s);
            }
            foreach(string s in FocusQueues){
                Macros.Add("F_"+s, "/cast [@focus] "+s);
                Queues.Add(q, "F_"+s);
                epicQueues += "if "+EpicSettingsAddonName+".Queues[\""+"focus"+s.ToLower()+"\"] then queue = "+q+" end\n";
                epicQueuesInfo += (EpicSettingsAddonName+".AddCommandInfo(6, \"/"+EpicSettingsAddonName+" cast focus "+s+"\")\n");
                q++;
                if(!Spellbook.Contains(s))
                    Spellbook.Add(s);
            }
            foreach(string s in TargetQueues){
                Queues.Add(q, s);
                epicQueues += "if "+EpicSettingsAddonName+".Queues[\""+"target"+s.ToLower()+"\"] then queue = "+q+" end\n";
                epicQueuesInfo += (EpicSettingsAddonName+".AddCommandInfo(3, \"/"+EpicSettingsAddonName+" cast target "+s+"\")\n");
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
            EpicToggles.Add(new EpicToggle("ooc", "OOC", true, "To use out of combat spells"));
   

            ////Setting up Tabs
            //EpicSetting.SetTabName(1, "Disable Spells");
            //EpicSetting.SetTabName(2, "Utility");
            //EpicSetting.SetTabName(3, "Defensive");
            //EpicSetting.SetTabName(4, "Offensive");
            //EpicSetting.SetTabName(5, "BossMod");

            ////Setting up Minitabs
            //EpicSetting.SetMinitabName(1, 1, "Single");
            //EpicSetting.SetMinitabName(1, 2, "AOE");
            //EpicSetting.SetMinitabName(1, 3, "Cooldowns");

            //EpicSetting.SetMinitabName(2, 1, "General");
            //EpicSetting.SetMinitabName(2, 2, "M+ Affix");

            //EpicSetting.SetMinitabName(3, 1, "Defensives");
            //EpicSetting.SetMinitabName(3, 2, "Defensives2");

            //EpicSetting.SetMinitabName(4, 1, "General");

            //EpicSetting.SetMinitabName(5, 1, "General");

   


            //EpicSettings.Add(new EpicCheckboxSetting(2, 1, 0, "UseUnterupt", "Use Auto Interupt", true));
            //EpicSettings.Add(new EpicCheckboxSetting(2, 1, 0, "UseUnteruptMO", "Use Auto Interupt Mouseover", true));

            //EpicSettings.Add(new EpicCheckboxSetting(2, 1, 1, "UseStun1", "Use Grip for Interupt", false));
            //EpicSettings.Add(new EpicCheckboxSetting(2, 1, 1, "UseStun2", "Use Asphyxiate for Interupt", false));

            //EpicSettings.Add(new EpicSliderSetting(2, 1, 2, "InteruptAboveMS", "Interupt Above MS", 250, 1000, 335));
            //EpicSettings.Add(new EpicSliderSetting(2, 1, 3, "InteruptMaxCastRemaining", "Interupt Max Cast Remaining", 50, 700, 200));
            //EpicSettings.Add(new EpicSliderSetting(2, 1, 4, "SlowDown", "Slow Down", 100, 500, 150));

            //EpicSettings.Add(new EpicCheckboxSetting(2, 1, 1, "DispelDebuffs", "Dispel Debuffs", true));

                        //M+ Affixes
            //EpicSettings.Add(new EpicLabelSetting(2, 2, 0, "Xal'atath's Bargain: Ascendant"));
            //EpicSettings.Add(new EpicCheckboxSetting(2, 2, 1, "UseMeleeAbility", "Use Blinding Sleet in Melee", false)); // EpicSetting.GetCheckboxSetting(EpicSettings, "DispelDebuffs")
            //EpicSettings.Add(new EpicSliderSetting(2, 2, 1, "CosmicTargetCount", "Cosmic Ascension Count for Blinding Sleet", 0, 10, 2)); // EpicSetting.GetSliderSetting(EpicSettings, "MovementDelay")

            //EpicSettings.Add(new EpicLabelSetting(2, 2, 3, "Mists of Tirna Scithe"));
            //EpicSettings.Add(new EpicCheckboxSetting(2, 2, 4, "UseIllusionaryVulpin", "Use Asphyxiate Illusionary Vulpin Auto", false));
            //EpicSettings.Add(new EpicCheckboxSetting(2, 2, 5, "UseIllusionaryVulpinMO", "Use Asphyxiate Illusionary Vulpin @Mouseover", false));


            //EpicSettings.Add(new EpicCheckboxSetting(3, 1, 0, "UseHealingPotion", "Use Healing Potion", false));
            //EpicSettings.Add(new EpicDropdownSetting(3, 1, 0, "HealingPotionName", "Healing Potion Name", new List<string>(){"Invigorating Healing Potion"}, "Invigorating Healing Potion"));
            //EpicSettings.Add(new EpicSliderSetting(3, 1, 0, "HealingPotionHP", "@ HP", 1, 100, 25));

            //EpicSettings.Add(new EpicCheckboxSetting(3, 1, 1, "UseHealthstone", "Use Healthstone", true));
            //EpicSettings.Add(new EpicSliderSetting(3, 1, 1, "HealthstoneHP", "@ HP", 1, 100, 60));

            //EpicSettings.Add(new EpicCheckboxSetting(3, 1, 2, "UseLichborne", "Use Lichborne", true));
            //EpicSettings.Add(new EpicSliderSetting(3, 1, 2, "LichborneHP", " @HP", 1, 100, 40));
            //EpicSettings.Add(new EpicCheckboxSetting(3, 1, 3, "UseDeathPact", "Use Death Pact", true));
            //EpicSettings.Add(new EpicSliderSetting(3, 1, 3, "DeathPactHP", " @HP", 1, 100, 30));
            //EpicSettings.Add(new EpicCheckboxSetting(3, 1, 4, "UseAntiMagicShell", "Use Anti-Magic Shell", true));
            //EpicSettings.Add(new EpicSliderSetting(3, 1, 4, "AntiMagicShellHP", " @HP", 1, 100, 15));
            //EpicSettings.Add(new EpicCheckboxSetting(3, 1, 5, "UseSacrificialPact", "Use Sacrificial Pact", true));
            //EpicSettings.Add(new EpicSliderSetting(3, 1, 5, "SacrificialPactHP", " @HP", 1, 100, 20));
            //EpicSettings.Add(new EpicCheckboxSetting(3, 1, 6, "UseIceboundFortitude", "Use Icebound Fortitude", true));
            //EpicSettings.Add(new EpicSliderSetting(3, 1, 6, "IceboundFortitudeHP", " @HP", 1, 100, 30));
            //EpicSettings.Add(new EpicCheckboxSetting(3, 1, 7, "UseDeathStrike", "Use Death Strike", true));
            //EpicSettings.Add(new EpicSliderSetting(3, 1, 7, "DeathStrikeHP", " @HP", 1, 100, 60));

            //EpicSettings.Add(new EpicCheckboxSetting(3, 2, 0, "UseRuneTap", "Use Rune Tap", true));
            //EpicSettings.Add(new EpicSliderSetting(3, 2, 0, "RuneTapHP", " @HP", 1, 100, 30));
            //EpicSettings.Add(new EpicCheckboxSetting(3, 2, 1, "UseTombstone", "Use Tombstone", true));
            //EpicSettings.Add(new EpicSliderSetting(3, 2, 1, "TombstoneHP", " @HP", 1, 100, 35));
            //EpicSettings.Add(new EpicCheckboxSetting(3, 2, 2, "UseVampiricBlood", "Use Vampiric Blood", true));
            //EpicSettings.Add(new EpicSliderSetting(3, 2, 2, "VampiricBloodHP", " @HP", 1, 100, 35));
            //EpicSettings.Add(new EpicCheckboxSetting(3, 2, 3, "UseDancingRuneWeapon", "Use Dancing Rune Weapon", true));
            //EpicSettings.Add(new EpicSliderSetting(3, 2, 3, "DancingRuneWeaponHP", " @HP", 1, 100, 60));

            //EpicSettings.Add(new EpicCheckboxSetting(4, 1, 1, "DeathAndDecayMoving", "Dont Cast Death And Decay while moving", true));
            //EpicSettings.Add(new EpicSliderSetting(4, 1, 2, "DeathAndDecayStopped", "Death And Decay cast if standing in MS", 250, 3000, 500));





          

            //EpicSettings.Add(new EpicDropdownSetting(4, 1, 0, "DeathandDecayUsage", "Death and Decay Usage", new List<string>(){"Cursor", "Enemy under cursor", "Confirmation", "Player"}, "Player"));
            //EpicSettings.Add(new EpicDropdownSetting(4, 1, 1, "AntiMagicZoneUsage", "Anti-Magic Zone Usage", new List<string>(){"Cursor", "Enemy under cursor", "Confirmation", "Player"}, "Player"));
            //EpicSettings.Add(new EpicDropdownSetting(4, 1, 2, "DeathsDueUsage", "Death's Due Usage", new List<string>(){"Cursor", "Enemy under cursor", "Confirmation", "Player"}, "Player"));


     

            //Settings.Add(new Setting("Death's Due Cast:", m_CastingList, "Player"));

            //To add spells to disable you need exact spell name from hekili for ex lightning_shield 
            //EpicSettings.Add(new EpicLabelSetting(1, 1, 0,"Check spell to Disable it"));
            //EpicSettings.Add(new EpicHekiliAbilityDisableCheckboxSetting(1, 1, 1, "UseLightningShield", "Lightning Shield", false, 262, "lightning_shield"));
            //EpicSettings.Add(new EpicLabelSetting(1, 2, 0,"Check spell to Disable it"));
            //EpicSettings.Add(new EpicHekiliAbilityDisableCheckboxSetting(1, 2, 1, "UseLightningShield", "Lightning Shield", false, 262, "lightning_shield"));
            //EpicSettings.Add(new EpicLabelSetting(1, 2, 0,"Check spell to Disable it"));
            //EpicSettings.Add(new EpicHekiliAbilityDisableCheckboxSetting(1, 3, 1, "UseLightningShield", "Lightning Shield", false, 262, "lightning_shield"));


            


            Macros.Add("focusplayer", "/focus player");
            Macros.Add("focustarget", "/focus target");

            for(int i = 1; i <= 4; i++){
                Macros.Add("focusparty"+i, "/focus party"+i);
            }
            for(int i = 1; i <= 20; i++){
                Macros.Add("focusraid"+i, "/focus raid"+i);
            }


            //Item Macros


            Macros.Add("Healthstone", "/use "+ Healthstone_ItemName()+"\\n/use "+DemonicHealthstone_ItemlName());
            Items.Add(DemonicHealthstone_ItemlName());


            Macros.Add("TopTrinket", "/use 13");
            Macros.Add("TopTrinketPlayer", "/use [@player] 13");
            Macros.Add("TopTrinketCursor", "/use [@cursor] 13");
            Macros.Add("TopTrinketFocus", "/use [@focus] 13");
            Macros.Add("BottomTrinket", "/use 14");
            Macros.Add("BottomTrinketPlayer", "/use [@player] 14");
            Macros.Add("BottomTrinketCursor", "/use [@cursor] 14");
            Macros.Add("BottomTrinketFocus", "/use [@focus] 14");
            Macros.Add("NextEnemy", "/targetenemy");

            Items.Add(TemperedPotion_SpellName());
            Items.Add(PotionofUnwaveringFocus_SpellName());
            Items.Add(InvigoratingHealingPotion_ItemName());
            Items.Add(Healthstone_ItemName());

            //Getting the addon name
            string addonName = Inferno.GetAddonName().ToLower();
            if (Inferno.GetAddonName().Length >= 5){
                addonName = Inferno.GetAddonName().Substring(0, 5).ToLower();
            }

            //Usage: CreateCustomFunction(LabelForInfernoToggleButton, FiveLettersOfAddonName, ListOfSettings, ListOfToggles)
            //copy this
            CustomFunctions.Add("SetupEpicSettings", EpicSetting.CreateCustomFunction("Toggle", addonName, EpicSettings, EpicToggles, epicQueuesInfo, EpicSettingsAddonName));

            //A function set up to return which spell is queued
            CustomFunctions.Add("GetEpicQueues", epicQueues);




            CustomFunctions.Add("CooldownsToggle", "local Usage = 0\n" +
            "if "+EpicSettingsAddonName+".Toggles[\"cds\"] then\n"+
                "Usage = 1\n"+
            "end\n" +
            "return Usage");

            CustomFunctions.Add("MouseoverExists", "if not UnitExists('mouseover') then return 0; end if UnitExists('mouseover') and not UnitIsDead('mouseover') then return 1 else return 0; end");

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
            


            CustomFunctions.Add("GetToggleToggle", "local Settings = 0\n" +
            "if "+EpicSettingsAddonName+".Toggles[\"toggle\"] then\n"+
                "Settings = 1\n" +
            "end\n" +
            "return Settings");


            int someiterator = 1;
            //copy this
            foreach(string s in EpicSetting.CreateSettingsGetters(EpicSettings, EpicToggles, EpicSettingsAddonName)){
                CustomFunctions.Add("SettingsGetter"+someiterator, s);
                someiterator++;
            }



            //REMOVE | REPLACE THE SPELLID FUNCTIONS WITH THE NEW ONE, I DON'T KNOW WHAT THAT IS, SO I DIDN'T DO IT HERE

            CustomFunctions.Add("SpellId", "local nextSpell = C_AssistedCombat.GetNextCastSpell() if nextSpell ~= 0 and nextSpell ~= nil then return nextSpell else return 9000000 end");
            CustomFunctions.Add("SpellId2", "local nextSpell = C_AssistedCombat.GetNextCastSpell() if nextSpell ~= 0 and nextSpell ~= nil then return nextSpell else return 9000000 end");
                string npcListForceCombat = "{";
                for(int i=0; i < ForceCombatNPC.Count; i++){
                    npcListForceCombat += "\""+ForceCombatNPC.ElementAt(i)+"\"";
                    if(i < ForceCombatNPC.Count-1)
                        npcListForceCombat += ", ";
                }
                npcListForceCombat += "}";
                CustomFunctions.Add("badtarget", "local check=0; " +
                "local dummyList = "+npcListForceCombat+"\n"+
                "local isDummy = false\n"+
                "if UnitExists(\"target\") then\n"+
                    "for l, k in pairs(dummyList) do\n"+
                        "if UnitName(\"target\") == k then\n"+
                            "isDummy = true\n" +
                        "end\n" +
                    "end\n"+
                "end\n"+
                "if (not UnitCanAttack(\"player\", \"target\") or not UnitAffectingCombat(\"target\") or not C_Item.IsItemInRange(32698, \"target\") or UnitDetailedThreatSituation(\"player\", \"target\") == nil) and (not isDummy) then\n" +
                    "check = 1\n"+
                "end\n" +
                "return check");
                
                CustomFunctions.Add("forcedtarget", "local check=0; " +
                "local dummyList = "+npcListForceCombat+"\n"+
                "local isDummy = false\n"+
                "if UnitExists(\"target\") then\n"+
                    "for l, k in pairs(dummyList) do\n"+
                        "if UnitName(\"target\") == k then\n"+
                            "isDummy = true\n" +
                        "end\n" +
                    "end\n"+
                "end\n"+
                "if isDummy then\n" +
                    "check = 1\n"+
                "end\n" +
                "return check");



                //CustomFunctions.Add("MouseoverInterruptableCastingElapsed", 
                //"if UnitExists(\"mouseover\") and UnitCanAttack(\"player\", \"mouseover\") and UnitAffectingCombat(\"mouseover\") and C_Spell.IsSpellInRange(\""+Asphyxiate_SpellName()+"\",\"mouseover\") then\n"+
                    //"local _, _, _, startTime, endTime, _, _, interrupt = UnitCastingInfo(\"mouseover\"); " + // /run print(UnitCastingInfo("mouseover"))
                    //"if startTime ~= nil and not interrupt then\n"+
                        //"return GetTime()*1000 - startTime\n"+
                    //"end\n" +
                //"end\n" +
                //"return 0");

                //CustomFunctions.Add("MouseoverInterruptableCastingRemaining", 
                //"if UnitExists(\"mouseover\") and UnitCanAttack(\"player\", \"mouseover\") and UnitAffectingCombat(\"mouseover\") and C_Spell.IsSpellInRange(\""+Asphyxiate_SpellName()+"\",\"mouseover\") then\n"+
                    //"local _, _, _, startTime, endTime, _, _, interrupt = UnitCastingInfo(\"mouseover\"); " +
                    //"if startTime ~= nil and not interrupt then\n"+
                        //"return endTime - GetTime()*1000\n"+
                    //"end\n" +
                //"end\n" +
                //"return 0");



            DebugHelper.Start();
            SwapTargetHelper.Start();
            CastDelay.Start();
            StandingHelper.Start();
            MovingHelper.Start();

        }

        public void Focus(string targetString, int delayAfter = 200){
            if (targetString.Contains("party")){
                Inferno.Cast("focus"+targetString, true);
            }else if (targetString.Contains("raid")){
                Inferno.Cast("focus"+targetString, true);
            }else if (targetString == "player"){
                Inferno.Cast("focusplayer", true);
            }
            
            System.Threading.Thread.Sleep(delayAfter);
        }

        public bool UnitIsFocus(string targetString){
            int PartyUnitIsFocus = Inferno.CustomFunction("PartyUnitIsFocus");
            int RaidUnitIsFocus = Inferno.CustomFunction("RaidUnitIsFocus");
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


        public void UpdateGroupUnits(){
            if(GroupUnits == null){
                GroupUnits = new List<string>();
            }
            //For player, group just consists of the player
            if(Inferno.GroupSize() == 0){
                if(!GroupUnits.Contains("player"))
                    GroupUnits.Add("player");
            }else if(Inferno.GroupSize() > 5){
                if(!GroupUnits.Contains("player"))
                    GroupUnits.Add("player");
                for(int i = 1; i <= Inferno.GroupSize(); i++){
                    if(!GroupUnits.Contains("raid"+i))
                        GroupUnits.Add("raid"+i);
                }
            }else{
                if(!GroupUnits.Contains("player"))
                    GroupUnits.Add("player");
                for(int i = 1; i < Inferno.GroupSize(); i++){
                    if(!GroupUnits.Contains("party"+i))
                        GroupUnits.Add("party"+i);
                }
            }
        }

        public int AverageGroupHP(){
            if(GroupUnits.Where(x => Inferno.Health(x) > 0).Count() <= 0)
                return 0;
            return (int)(GroupUnits.Where(x => Inferno.Health(x) > 0).Average(x => Inferno.Health(x)));
        }

        public override bool CombatTick(){
            if(authorized){




                bool ToggleToggle = Inferno.CustomFunction("GetToggleToggle") == 1;

  
                bool MouseoverExists = Inferno.CustomFunction("MouseoverExists") == 1;
                int SpellID1 = Inferno.CustomFunction("SpellId");
                int SpellID2 = Inferno.CustomFunction("SpellId2");
                //int MouseoverInterruptableCastingElapsed = Inferno.CustomFunction("MouseoverInterruptableCastingElapsed");
                //int MouseoverInterruptableCastingRemaining = Inferno.CustomFunction("MouseoverInterruptableCastingRemaining");

                int epicQueue = Inferno.CustomFunction("GetEpicQueues");

                //bool UseUnterupt = EpicSetting.GetCheckboxSetting(EpicSettings, "UseUnterupt");
                //bool UseUnteruptMO = EpicSetting.GetCheckboxSetting(EpicSettings, "UseUnteruptMO");
                //int InteruptAboveMS = EpicSetting.GetSliderSetting(EpicSettings, "InteruptAboveMS");
                //int InteruptMaxCastRemaining = EpicSetting.GetSliderSetting(EpicSettings, "InteruptMaxCastRemaining");
                var TargetHealth = Inferno.Health("target");
                var TargetExactHealth = Inferno.TargetCurrentHP();
                if(TargetHealth < 0){
                TargetHealth = 999999;
                    }
                
                bool Fighting = Inferno.Range("target") <= 25 && (Inferno.TargetIsEnemy() && ((TargetHealth > 0 || TargetExactHealth > 0) || Inferno.UnitID("target") == 229537)) || (Inferno.CustomFunction("forcedtarget") == 1);


                bool BadTarget = Inferno.CustomFunction("badtarget") == 1;

                //bool DeathAndDecayMoving = EpicSetting.GetCheckboxSetting(EpicSettings, "DeathAndDecayMoving");
                //int DeathAndDecayStopped = EpicSetting.GetSliderSetting(EpicSettings, "DeathAndDecayStopped");
                                

                //int TopTrinketCD = Inferno.TrinketCooldown(13);
                //bool TopTrinketCDUp = TopTrinketCD <= 10;
                //int BottomTrinketCD = Inferno.TrinketCooldown(14);
                //bool BottomTrinketCDUp = BottomTrinketCD <= 10;

                //Talents
                //You need to intialise it as well 
                bool ExampleTalent = Inferno.Talent(123456);

                //Bloodlust
                bool BloodlustUp = false;
                foreach (string BloodlustEffect in BloodlustEffects)
                {
                    if (Inferno.HasBuff(BloodlustEffect))
                    {
                        BloodlustUp = true;
                        break;
                    }
                }

                //Update Units
                UpdateGroupUnits();
                bool Moving = Inferno.PlayerIsMoving();
                if(Moving){
                    StandingHelper.Restart();
                }else{
					MovingHelper.Restart();
				}
                if(!ToggleToggle){
                    return false;
                }
                if (Inferno.CastingID("player") > 0 || Inferno.IsChanneling("player")){
                    return false;
                }




                string queueSpell = "";
                //Inferno.PrintMessage("Queue "+epicQueue, Color.Purple);
                if(epicQueue != 0){
                    if (Queues.TryGetValue(epicQueue, out queueSpell)){
                        string spellname = queueSpell;
                        //Handling Mouseover queue spells
                        if(queueSpell.Substring(0, 3) == "MO_"){
                            spellname = queueSpell.Substring(3);
                            if(Inferno.LastCast() != spellname){
                                //Cast
                                Inferno.Cast(queueSpell, true);
                                Inferno.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                            }
                            if(Inferno.LastCast() == spellname || Inferno.SpellCooldown(spellname) > 2000){
                                Inferno.Cast("ResetQueues", true);
                            }
                        //Handling Cursor and Player queue spells
                        }else if(queueSpell.Substring(0, 2) == "C_" || queueSpell.Substring(0, 2) == "P_"){
                            spellname = queueSpell.Substring(2);
                            if(Inferno.LastCast() != spellname){
                                //Cast
                                Inferno.Cast(queueSpell, true);
                                Inferno.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                            }
                            if(Inferno.LastCast() == spellname || Inferno.SpellCooldown(spellname) > 2000){
                                Inferno.Cast("ResetQueues", true);
                            }
                        //Handling Focus queue spells
                        }else if(queueSpell.Substring(0, 2) == "F_"){
                            spellname = queueSpell.Substring(2);
                            if(Inferno.LastCast() != spellname){
                                //Cast
                                Inferno.Cast(queueSpell, true);
                                Inferno.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                            }
                            if(Inferno.LastCast() == spellname || Inferno.SpellCooldown(spellname) > 2000){
                                Inferno.Cast("ResetQueues", true);
                            }
                        //Handling Target queue spells
                        }else{
                            if(Inferno.LastCast() != spellname){
                                //Cast
                                Inferno.Cast(queueSpell);
                                Inferno.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                            }
                            if(Inferno.LastCast() == spellname || Inferno.SpellCooldown(spellname) > 2000){
                                Inferno.Cast("ResetQueues", true);
                            }
                        }
                    }
                    return false;
                }




                #region Racials
                    //Racials
                    if (SpellID1 == 28880 && Inferno.CanCast(GiftOfTheNaaru_SpellName(), "player", true, true))
                    {

                        Inferno.Cast(GiftOfTheNaaru_SpellName());
                        return true;
                    }
                    if (SpellID1 == 436344 && Inferno.CanCast(AzeriteSurge_SpellName(), "player", true, true))
                    {

                        Inferno.Cast(AzeriteSurge_SpellName());
                        return true;
                    }

                    if (SpellID1 == 20594 && Inferno.CanCast(Stoneform_SpellName(), "player", true, true))
                    {

                        Inferno.Cast(Stoneform_SpellName());
                        return true;
                    }

                    if (SpellID1 == 20589 && Inferno.CanCast(EscapeArtist_SpellName(), "player", true, true))
                    {

                        Inferno.Cast(EscapeArtist_SpellName());
                        return true;
                    }

                    if (SpellID1 == 59752 && Inferno.CanCast(WillToSurvive_SpellName(), "player", true, true))
                    {

                        Inferno.Cast(WillToSurvive_SpellName());
                        return true;
                    }

                    if (SpellID1 == 255647 && Inferno.CanCast(LightsJudgment_SpellName(), "player", true, true))
                    {

                        Inferno.Cast(LightsJudgment_SpellName());
                        return true;
                    }

                    if (SpellID1 == 265221 && Inferno.CanCast(Fireblood_SpellName(), "player", true, true))
                    {

                        Inferno.Cast(Fireblood_SpellName());
                        return true;
                    }

                    if (SpellID1 == 69041 && Inferno.CanCast(RocketBarrage_SpellName(), "player", true, true))
                    {

                        Inferno.Cast(RocketBarrage_SpellName());
                        return true;
                    }

                    if (SpellID1 == 20549 && Inferno.CanCast(WarStomp_SpellName(), "player", true, true))
                    {

                        Inferno.Cast(WarStomp_SpellName());
                        return true;
                    }

                    if (SpellID1 == 7744 && Inferno.CanCast(WillOfTheForsaken_SpellName(), "player", true, true))
                    {

                        Inferno.Cast(WillOfTheForsaken_SpellName());
                        return true;
                    }

                    if (SpellID1 == 260364 && Inferno.CanCast(ArcanePulse_SpellName(), "player", true, true))
                    {
    
                        Inferno.Cast(ArcanePulse_SpellName());
                        return true;
                    }

                    if (SpellID1 == 255654 && Inferno.CanCast(BullRush_SpellName(), "player", true, true))
                    {

                        Inferno.Cast(BullRush_SpellName());
                        return true;
                    }

                    if (SpellID1 == 312411 && Inferno.CanCast(BagOfTricks_SpellName(), "player", true, true))
                    {
   
                        Inferno.Cast(BagOfTricks_SpellName());
                        return true;
                    }

                    if ((SpellID1 == 20572 || SpellID1 == 33702 || SpellID1 == 33697) && Inferno.CanCast(BloodFury_SpellName(), "player", true, true))
                    {
      
                        Inferno.Cast(BloodFury_SpellName());
                        return true;
                    }

                    if (SpellID1 == 26297 && Inferno.CanCast(Berserking_SpellName(), "player", false, true))
                    {

                        Inferno.Cast(Berserking_SpellName());
                        return true;
                    }

                    if (SpellID1 == 274738 && Inferno.CanCast(AncestralCall_SpellName(), "player", false, true))
                    {

                        Inferno.Cast(AncestralCall_SpellName());
                        return true;
                    }

                    if ((SpellID1 == 28730 || SpellID1 == 25046 || SpellID1 == 50613 || SpellID1 == 69179 || SpellID1 == 80483 || SpellID1 == 129597) && Inferno.CanCast(ArcaneTorrent_SpellName(), "player", true, false))
                    {
 
                        Inferno.Cast(ArcaneTorrent_SpellName());
                        return true;
                    }

                    if (SpellID1 == 58984 && Inferno.CanCast(Shadowmeld_SpellName(), "player", false, true))
                    {
                        Inferno.Cast(Shadowmeld_SpellName());
                        return true;
                    }
                    #endregion



                #region Interupt
                //Random rnd = new Random ();
                //if(Inferno.IsInterruptable() && Inferno.CanCast(MindFreeze_SpellName(), "target") && UseUnterupt && Inferno.CastingElapsed("target") > InteruptAboveMS + rnd.Next(100) && Inferno.CastingRemaining("target") > InteruptMaxCastRemaining ){
                    //Inferno.Cast(MindFreeze_SpellName());
                    //return true;
                //}

                //if(Inferno.CanCast(MindFreeze_SpellName(), "mouseover") && UseUnteruptMO && MouseoverInterruptableCastingElapsed > InteruptAboveMS + rnd.Next(100) && MouseoverInterruptableCastingRemaining > InteruptMaxCastRemaining ){
                    //Inferno.Cast("MO_"+MindFreeze_SpellName(), true);
                    //return true;
                //}

                //bool UseStun1 = EpicSetting.GetCheckboxSetting(EpicSettings, "UseStun1");//grip
                //bool UseStun2 = EpicSetting.GetCheckboxSetting(EpicSettings, "UseStun2");//Asphyxiate
                //if(Inferno.SpellCooldown(MindFreeze_SpellName()) > 1500){
                    //if(Inferno.IsInterruptable() && Inferno.CanCast(DeathGrip_SpellName(), "target") && UseStun1 && Inferno.CastingElapsed("target") > InteruptAboveMS + rnd.Next(100) && Inferno.CastingRemaining("target") > InteruptMaxCastRemaining ){
                        //Inferno.Cast(DeathGrip_SpellName());
                        //return true;
                    //}
                    //if(Inferno.CanCast(DeathGrip_SpellName(), "mouseover") && UseStun1 && UseUnteruptMO && MouseoverInterruptableCastingElapsed > InteruptAboveMS + rnd.Next(100) && MouseoverInterruptableCastingRemaining > InteruptMaxCastRemaining ){
                        //Inferno.Cast("MO_"+DeathGrip_SpellName(), true);
                        //return true;
                    //}
                    //if(Inferno.IsInterruptable() && Inferno.CanCast(Asphyxiate_SpellName(), "target") && UseStun2 && Inferno.CastingElapsed("target") > InteruptAboveMS + rnd.Next(100) && Inferno.CastingRemaining("target") > InteruptMaxCastRemaining ){
                        //Inferno.Cast(Asphyxiate_SpellName());
                        //return true;
                    //}
                    //if(Inferno.CanCast(Asphyxiate_SpellName(), "mouseover") && UseStun2 && UseUnteruptMO && MouseoverInterruptableCastingElapsed > InteruptAboveMS + rnd.Next(100) && MouseoverInterruptableCastingRemaining > InteruptMaxCastRemaining ){
                        //Inferno.Cast("MO_"+Asphyxiate_SpellName(), true);
                        //return true;
                    //}
                //}
                #endregion


                //bool CooldownsToggle = Inferno.CustomFunction("CooldownsToggle") == 1;



                #region Defensives


                //if (Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "LichborneHP") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseLichborne") && Inferno.SpellCooldown(Lichborne_SpellName()) < 1000) {
                    //if (Inferno.CanCast(Lichborne_SpellName(), "player")){
                        //Inferno.Cast("P_"+Lichborne_SpellName(), true);
                        //return true;
                    //}
                    ////return false;
                //}
                ////if(Inferno.CanCast(DeathPact_SpellName(), "player") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseDeathPact")) {
                    ////if (Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "DeathPactHP")) {
                        ////Inferno.Cast(DeathPact_SpellName());
                        ////return true;
                    ////}
                ////}
                //if (Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "DeathPactHP") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseDeathPact") && Inferno.SpellCooldown(DeathPact_SpellName()) < 1000) {
                    //if (Inferno.CanCast(DeathPact_SpellName(), "player")) {
                        //Inferno.Cast("P_"+DeathPact_SpellName(), true);
                        //return true;
                    //}
                    ////return false;
                //}
                ////if(Inferno.CanCast(AntimagicShell_SpellName(), "player") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseAntiMagicShell")) {
                    ////if (Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "AntiMagicShellHP")) {
                        ////Inferno.Cast(AntimagicShell_SpellName());
                        ////return true;
                    ////}
                ////}
                //if (Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "AntiMagicShellHP") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseAntiMagicShell") && Inferno.SpellCooldown(AntimagicShell_SpellName()) < 1000) {
                    //if (Inferno.CanCast(AntimagicShell_SpellName(), "player")) {
                        //Inferno.Cast("P_"+AntimagicShell_SpellName(), true);
                        //return true;
                    //}
                    ////return false;
                //}
                ////if(Inferno.CanCast(SacrificialPact_SpellName(), "player") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseSacrificialPact")) {
                    ////if (Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "SacrificialPactHP")) {
                        ////Inferno.Cast(SacrificialPact_SpellName());
                        ////return true;
                    ////}
                ////}
                //if (Inferno.Power() > 20 && Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "SacrificialPactHP") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseSacrificialPact") && Inferno.SpellCooldown(SacrificialPact_SpellName()) < 1000) {
                    //if (Inferno.CanCast(SacrificialPact_SpellName(), "player")) {
                        //Inferno.Cast("P_"+SacrificialPact_SpellName(), true);
                        //return true;
                    //}
                    ////return false;
                //}
                ////if(Inferno.CanCast(IceboundFortitude_SpellName(), "player") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseIceboundFortitude")) {
                    ////if (Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "IceboundFortitudeHP")) {
                        ////Inferno.Cast(IceboundFortitude_SpellName());
                        ////return true;
                    ////}
                ////}
                //if (Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "IceboundFortitudeHP") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseIceboundFortitude") && Inferno.SpellCooldown(IceboundFortitude_SpellName()) < 1000) {
                    //if (Inferno.CanCast(IceboundFortitude_SpellName(), "player")) {
                        //Inferno.Cast("P_"+IceboundFortitude_SpellName(), true);
                        //return true;
                    //}
                    ////return false;
                //}
//                
//
                //if (Inferno.Power() > 35 && Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "DeathStrikeHP") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseDeathStrike") && Inferno.SpellCooldown(DeathStrike_SpellName()) < 1000) {
                    //if (Inferno.CanCast(DeathStrike_SpellName(), "player")) {
                        //Inferno.Cast(DeathStrike_SpellName());
                        //return true;
                    //}
                    ////return false;
                //}
                ////if(Inferno.CanCast(RuneTap_SpellName(), "player") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseRuneTap")) {
                    ////if (Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "RuneTapHP")) {
                        ////Inferno.Cast(RuneTap_SpellName());
                        ////return true;
                    ////}
                ////}
                //if (Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "RuneTapHP") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseRuneTap") && Inferno.SpellCooldown(RuneTap_SpellName()) < 1000) {
                    //if (Inferno.CanCast(RuneTap_SpellName(), "player")) {
                        //Inferno.Cast(RuneTap_SpellName());
                        //return true;
                    //}
                    ////return false;
                //}
                ////if(Inferno.CanCast(Tombstone_SpellName(), "player") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseTombstone")) {
                    ////if (Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "TombstoneHP")) {
                        ////Inferno.Cast(Tombstone_SpellName());
                        ////return true;
                    ////}
                ////}
                //if (Inferno.HasBuff(BoneShield_SpellName()) && Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "TombstoneHP") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseTombstone") && Inferno.SpellCooldown(Tombstone_SpellName()) < 1000) {
                    //if (Inferno.CanCast(Tombstone_SpellName(), "player")) {
                        //Inferno.Cast("P_"+Tombstone_SpellName(), true);
                        //return true;
                    //}
                    ////return false;
                //}
                ////if(Inferno.CanCast(VampiricBlood_SpellName(), "player") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseVampiricBlood")) {
                    ////if (Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "VampiricBloodHP")) {
                        ////Inferno.Cast(VampiricBlood_SpellName());
                        ////return true;
                    ////}
                ////}
                //if (Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "VampiricBloodHP") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseVampiricBlood") && Inferno.SpellCooldown(VampiricBlood_SpellName()) < 1000) {
                    //if (Inferno.CanCast(VampiricBlood_SpellName(), "player")) {
                        //Inferno.Cast(VampiricBlood_SpellName());
                        //return true;
                    //}
                    ////return false;
                //}
                ////if(Inferno.CanCast(DancingRuneWeapon_SpellName(), "player") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseDancingRuneWeapon")) {
                    ////if (Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "DancingRuneWeaponHP")) {
                        ////Inferno.Cast(DancingRuneWeapon_SpellName());
                        ////return true;
                    ////}
                ////}
                //if (Inferno.Health() <= EpicSetting.GetSliderSetting(EpicSettings, "DancingRuneWeaponHP") && EpicSetting.GetCheckboxSetting(EpicSettings, "UseDancingRuneWeapon") && Inferno.SpellCooldown(DancingRuneWeapon_SpellName()) < 1000) {
                    //if (Inferno.CanCast(DancingRuneWeapon_SpellName(), "player")) {
                        //Inferno.Cast(DancingRuneWeapon_SpellName());
                        //return true;
                    //}
                    ////return false;
                //}
                #endregion

                if(AverageGroupHP() > 10){
                    //cast
                }


                #region Spell Suggestion
                if(CastDelay.ElapsedMilliseconds < EpicSetting.GetSliderSetting(EpicSettings, "SlowDown")){
                    return false;
                }


                


                string castSpell = "";
                if(Fighting){
                    if (SpellCasts.TryGetValue(SpellID1, out castSpell)){
                        Inferno.Cast(castSpell);
                        return true;
                    }else{
                        if (MacroCasts.TryGetValue(SpellID1, out castSpell)){
                            Inferno.Cast(castSpell, true);
                            return true;
                        }else{
                            if(SpellID1 > 0)
                                Inferno.PrintMessage("Couldn't find the spell/macro with id: "+SpellID1, Color.Purple);
                        }
                    }
                }

                #endregion





            }
            return false;
        }

        public override bool OutOfCombatTick(){
            if(authorized){

                HealthstoneUsed = false;
                var TargetHealth = Inferno.Health("target");
                var TargetExactHealth = Inferno.TargetCurrentHP();
                if(TargetHealth < 0){
                TargetHealth = 999999;
                }
                bool Fighting = Inferno.Range("target") <= 25 && (Inferno.TargetIsEnemy() || (Inferno.CustomFunction("forcedtarget") == 1)) && ((TargetHealth > 0 || TargetExactHealth > 0) || Inferno.UnitID("target") == 229537);


                bool ToggleToggle = Inferno.CustomFunction("GetToggleToggle") == 1;


                
                int SpellID1 = Inferno.CustomFunction("SpellId");
                int SpellID2 = Inferno.CustomFunction("SpellId2");


                int epicQueue = Inferno.CustomFunction("GetEpicQueues");
                bool OOCToggle = EpicSetting.GetToggle(EpicToggles, "ooc");

                //Update Units
                UpdateGroupUnits();

                if(!ToggleToggle){
                    return false;
                }

                    string queueSpell = "";
                    if(epicQueue != 0){
                        if (Queues.TryGetValue(epicQueue, out queueSpell)){
                            string spellname = queueSpell;
                            //Handling Mouseover queue spells
                            if(queueSpell.Substring(0, 3) == "MO_"){
                                spellname = queueSpell.Substring(3);
                                if(Inferno.LastCast() != spellname){
                                    //Cast
                                    Inferno.Cast(queueSpell, true);
                                    Inferno.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                                }else{
                                    Inferno.Cast("ResetQueues", true);
                                }
                            //Handling Cursor and Player queue spells
                            }else if(queueSpell.Substring(0, 2) == "C_" || queueSpell.Substring(0, 2) == "P_"){
                                spellname = queueSpell.Substring(2);
                                if(Inferno.LastCast() != spellname){
                                    //Cast
                                    Inferno.Cast(queueSpell, true);
                                    Inferno.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                                }else{
                                    Inferno.Cast("ResetQueues", true);
                                }
                            //Handling Focus queue spells
                            }else if(queueSpell.Substring(0, 2) == "F_"){
                                spellname = queueSpell.Substring(2);
                                if(Inferno.LastCast() != spellname){
                                    //Cast
                                    Inferno.Cast(queueSpell, true);
                                    Inferno.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                                }else{
                                    Inferno.Cast("ResetQueues", true);
                                }
                            //Handling Target queue spells
                            }else{
                                if(Inferno.LastCast() != spellname){
                                    //Cast
                                    Inferno.Cast(queueSpell);
                                    Inferno.PrintMessage("Casting queue for "+queueSpell, Color.Purple);
                                }else{
                                    Inferno.Cast("ResetQueues", true);
                                }
                            }
                        }
                    }
                
                if(OOCToggle && Fighting){



                    #region Spell Suggestion

                    string castSpell = "";
                    if (SpellCasts.TryGetValue(SpellID1, out castSpell)&& Fighting){
                        Inferno.Cast(castSpell);
                        return true;
                    }else{
                        if (MacroCasts.TryGetValue(SpellID1, out castSpell)){
                            Inferno.Cast(castSpell, true);
                            return true;
                        }else{
                            if(SpellID1 > 0)
                                Inferno.PrintMessage("Couldn't find the spell/macro with id: "+SpellID1, Color.Purple);
                        }
                    }

                    #endregion


                }
            }
            return false;
        }

    }
}

