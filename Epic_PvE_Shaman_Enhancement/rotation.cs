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
                private static string Bloodlust_SpellName()//2825
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
        ///<summary>spell=274738</summary>
        private static string AncestralCall_SpellName()
        {
            switch (Language)
            {
                case "English": return "Ancestral Call";
                case "Deutsch": return "Ruf der Ahnen";
                case "Español": return "Llamada ancestral";
                case "Français": return "Appel ancestral";
                case "Italiano": return "Richiamo Ancestrale";
                case "Português Brasileiro": return "Chamado Ancestral";
                case "Русский": return "Призыв предков";
                case "한국어": return "고대의 부름";
                case "简体中文": return "先祖召唤";
                default: return "Ancestral Call";
            }
        }

        ///<summary>spell=108281</summary>
        private static string AncestralGuidance_SpellName()
        {
            switch (Language)
            {
                case "English": return "Ancestral Guidance";
                case "Deutsch": return "Führung der Ahnen";
                case "Español": return "Guía ancestral";
                case "Français": return "Soutien ancestral";
                case "Italiano": return "Guida Ancestrale";
                case "Português Brasileiro": return "Conselho dos Ancestrais";
                case "Русский": return "Наставления предков";
                case "한국어": return "고대의 인도";
                case "简体中文": return "先祖指引";
                default: return "Ancestral Guidance";
            }
        }

        ///<summary>spell=260364</summary>
        private static string ArcanePulse_SpellName()
        {
            switch (Language)
            {
                case "English": return "Arcane Pulse";
                case "Deutsch": return "Arkaner Puls";
                case "Español": return "Pulso Arcano";
                case "Français": return "Impulsion arcanique";
                case "Italiano": return "Impulso Arcano";
                case "Português Brasileiro": return "Pulso Arcano";
                case "Русский": return "Чародейский импульс";
                case "한국어": return "비전 파동";
                case "简体中文": return "奥术脉冲";
                default: return "Arcane Pulse";
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

        ///<summary>spell=114050</summary>
        private static string Ascendance_SpellName()
        {
            switch (Language)
            {
                case "English": return "Ascendance";
                case "Deutsch": return "Aszendenz";
                case "Español": return "Ascensión";
                case "Français": return "Ascendance";
                case "Italiano": return "Ascesa";
                case "Português Brasileiro": return "Ascendência";
                case "Русский": return "Перерождение";
                case "한국어": return "승천";
                case "简体中文": return "升腾";
                default: return "Ascendance";
            }
        }

        ///<summary>spell=108271</summary>
        private static string AstralShift_SpellName()
        {
            switch (Language)
            {
                case "English": return "Astral Shift";
                case "Deutsch": return "Astralverschiebung";
                case "Español": return "Cambio astral";
                case "Français": return "Transfert astral";
                case "Italiano": return "Sentiero Astrale";
                case "Português Brasileiro": return "Transição Astral";
                case "Русский": return "Астральный сдвиг";
                case "한국어": return "영혼 이동";
                case "简体中文": return "星界转移";
                default: return "Astral Shift";
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



        ///<summary>spell=255654</summary>
        private static string BullRush_SpellName()
        {
            switch (Language)
            {
                case "English": return "Bull Rush";
                case "Deutsch": return "Aufs Geweih nehmen";
                case "Español": return "Embestida astada";
                case "Français": return "Charge de taureau";
                case "Italiano": return "Scatto del Toro";
                case "Português Brasileiro": return "Investida do Touro";
                case "Русский": return "Бычий натиск";
                case "한국어": return "황소 돌진";
                case "简体中文": return "蛮牛冲撞";
                default: return "Bull Rush";
            }
        }

        ///<summary>spell=192058</summary>
        private static string CapacitorTotem_SpellName()
        {
            switch (Language)
            {
                case "English": return "Capacitor Totem";
                case "Deutsch": return "Totem der Energiespeicherung";
                case "Español": return "Tótem capacitador";
                case "Français": return "Totem condensateur";
                case "Italiano": return "Totem della Condensazione Elettrica";
                case "Português Brasileiro": return "Totem Capacitor";
                case "Русский": return "Тотем конденсации";
                case "한국어": return "축전 토템";
                case "简体中文": return "电能图腾";
                default: return "Capacitor Totem";
            }
        }

        ///<summary>spell=320674</summary>
        private static string ChainHarvest_SpellName()
        {
            switch (Language)
            {
                case "English": return "Chain Harvest";
                case "Deutsch": return "Kettenernte";
                case "Español": return "Cosecha en cadena";
                case "Français": return "Moisson en chaîne";
                case "Italiano": return "Raccolto a Catena";
                case "Português Brasileiro": return "Ceifa Encadeada";
                case "Русский": return "Цепная жатва";
                case "한국어": return "연쇄 수확";
                case "简体中文": return "收割链";
                default: return "Chain Harvest";
            }
        }

        ///<summary>spell=1064</summary>
        private static string ChainHeal_SpellName()
        {
            switch (Language)
            {
                case "English": return "Chain Heal";
                case "Deutsch": return "Kettenheilung";
                case "Español": return "Sanación en cadena";
                case "Français": return "Salve de guérison";
                case "Italiano": return "Catena di Guarigione";
                case "Português Brasileiro": return "Cura Encadeada";
                case "Русский": return "Цепное исцеление";
                case "한국어": return "연쇄 치유";
                case "简体中文": return "治疗链";
                default: return "Chain Heal";
            }
        }

        ///<summary>spell=188443</summary>
        private static string ChainLightning_SpellName()
        {
            switch (Language)
            {
                case "English": return "Chain Lightning";
                case "Deutsch": return "Kettenblitzschlag";
                case "Español": return "Cadena de relámpagos";
                case "Français": return "Chaîne d’éclairs";
                case "Italiano": return "Catena di Fulmini";
                case "Português Brasileiro": return "Cadeia de Raios";
                case "Русский": return "Цепная молния";
                case "한국어": return "연쇄 번개";
                case "简体中文": return "闪电链";
                default: return "Chain Lightning";
            }
        }

        ///<summary>spell=51886</summary>
        private static string CleanseSpirit_SpellName()
        {
            switch (Language)
            {
                case "English": return "Cleanse Spirit";
                case "Deutsch": return "Geistläuterung";
                case "Español": return "Limpiar espíritu";
                case "Français": return "Purifier l'esprit";
                case "Italiano": return "Purificazione dello Spirito";
                case "Português Brasileiro": return "Purificar Espírito";
                case "Русский": return "Очищение духа";
                case "한국어": return "영혼 정화";
                case "简体中文": return "净化灵魂";
                default: return "Cleanse Spirit";
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


        ///<summary>spell=187874</summary>
        private static string CrashLightning_SpellName()
        {
            switch (Language)
            {
                case "English": return "Crash Lightning";
                case "Deutsch": return "Tobende Blitze";
                case "Español": return "Estallido de relámpagos";
                case "Français": return "Impact foudroyant";
                case "Italiano": return "Schianto di Fulmini";
                case "Português Brasileiro": return "Raio Poderoso";
                case "Русский": return "Сокрушающая молния";
                case "한국어": return "낙뢰";
                case "简体中文": return "毁灭闪电";
                default: return "Crash Lightning";
            }
        }

        ///<summary>spell=384352</summary>
        private static string DoomWinds_SpellName()
        {
            switch (Language)
            {
                case "English": return "Doom Winds";
                case "Deutsch": return "Schicksalswinde";
                case "Español": return "Vientos de fatalidad";
                case "Français": return "Vents funestes";
                case "Italiano": return "Venti del Fato";
                case "Português Brasileiro": return "Ventos da Perdição";
                case "Русский": return "Ветра рока";
                case "한국어": return "파멸의 바람";
                case "简体中文": return "毁灭之风";
                default: return "Doom Winds";
            }
        }

        ///<summary>spell=198103</summary>
        private static string EarthElemental_SpellName()
        {
            switch (Language)
            {
                case "English": return "Earth Elemental";
                case "Deutsch": return "Erdelementar";
                case "Español": return "Elemental de Tierra";
                case "Français": return "Élémentaire de terre";
                case "Italiano": return "Elementale della Terra";
                case "Português Brasileiro": return "Elemental da Terra";
                case "Русский": return "Элементаль земли";
                case "한국어": return "대지의 정령";
                case "简体中文": return "土元素";
                default: return "Earth Elemental";
            }
        }

        ///<summary>spell=974</summary>
        private static string EarthShield_SpellName()
        {
            switch (Language)
            {
                case "English": return "Earth Shield";
                case "Deutsch": return "Erdschild";
                case "Español": return "Escudo de tierra";
                case "Français": return "Bouclier de terre";
                case "Italiano": return "Scudo di Terra";
                case "Português Brasileiro": return "Escudo da Terra";
                case "Русский": return "Щит земли";
                case "한국어": return "대지의 보호막";
                case "简体中文": return "大地之盾";
                default: return "Earth Shield";
            }
        }

        ///<summary>spell=2484</summary>
        private static string EarthbindTotem_SpellName()
        {
            switch (Language)
            {
                case "English": return "Earthbind Totem";
                case "Deutsch": return "Totem der Erdbindung";
                case "Español": return "Tótem Nexo Terrestre";
                case "Français": return "Totem de lien terrestre";
                case "Italiano": return "Totem del Vincolo Terrestre";
                case "Português Brasileiro": return "Totem de Prisão Terrena";
                case "Русский": return "Тотем оков земли";
                case "한국어": return "속박의 토템";
                case "简体中文": return "地缚图腾";
                default: return "Earthbind Totem";
            }
        }

        ///<summary>spell=117014</summary>
        private static string ElementalBlast_SpellName()
        {
            switch (Language)
            {
                case "English": return "Elemental Blast";
                case "Deutsch": return "Elementarschlag";
                case "Español": return "Explosión elemental";
                case "Français": return "Explosion élémentaire";
                case "Italiano": return "Detonazione Elementale";
                case "Português Brasileiro": return "Impacto Elemental";
                case "Русский": return "Удар духов стихий";
                case "한국어": return "정기 작렬";
                case "简体中文": return "元素冲击";
                default: return "Elemental Blast";
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

        ///<summary>spell=328923</summary>
        private static string FaeTransfusion_SpellName()
        {
            switch (Language)
            {
                case "English": return "Fae Transfusion";
                case "Deutsch": return "Faetransfusion";
                case "Español": return "Transfusión de sílfide";
                case "Français": return "Transfusion faërique";
                case "Italiano": return "Trasfusione dei Silfi";
                case "Português Brasileiro": return "Transfusão Feéria";
                case "Русский": return "Волшебное переливание";
                case "한국어": return "페이 수혈";
                case "简体中文": return "法夜输灵";
                default: return "Fae Transfusion";
            }
        }

        ///<summary>spell=196884</summary>
        private static string FeralLunge_SpellName()
        {
            switch (Language)
            {
                case "English": return "Feral Lunge";
                case "Deutsch": return "Wildes Anspringen";
                case "Español": return "Embestida feral";
                case "Français": return "Impulsion farouche";
                case "Italiano": return "Affondo Ferino";
                case "Português Brasileiro": return "Estocada Feral";
                case "Русский": return "Свирепый выпад";
                case "한국어": return "야생의 약진";
                case "简体中文": return "狂野扑击";
                default: return "Feral Lunge";
            }
        }

        ///<summary>spell=51533</summary>
        private static string FeralSpirit_SpellName()
        {
            switch (Language)
            {
                case "English": return "Feral Spirit";
                case "Deutsch": return "Wildgeist";
                case "Español": return "Espíritu feral";
                case "Français": return "Esprit farouche";
                case "Italiano": return "Lupi Spirituali";
                case "Português Brasileiro": return "Espírito Feral";
                case "Русский": return "Дух дикого зверя";
                case "한국어": return "야수 정령";
                case "简体中文": return "野性狼魂";
                default: return "Feral Spirit";
            }
        }

        ///<summary>spell=333974</summary>
        private static string FireNova_SpellName()
        {
            switch (Language)
            {
                case "English": return "Fire Nova";
                case "Deutsch": return "Feuernova";
                case "Español": return "Nova de Fuego";
                case "Français": return "Nova de feu";
                case "Italiano": return "Esplosione di Fuoco";
                case "Português Brasileiro": return "Nova de Fogo";
                case "Русский": return "Кольцо огня";
                case "한국어": return "불꽃 회오리";
                case "简体中文": return "火焰新星";
                default: return "Fire Nova";
            }
        }

        ///<summary>spell=265221</summary>
        private static string Fireblood_SpellName()
        {
            switch (Language)
            {
                case "English": return "Fireblood";
                case "Deutsch": return "Feuerblut";
                case "Español": return "Sangrardiente";
                case "Français": return "Sang de feu";
                case "Italiano": return "Sangue Infuocato";
                case "Português Brasileiro": return "Sangue de Fogo";
                case "Русский": return "Огненная кровь";
                case "한국어": return "불꽃피";
                case "简体中文": return "烈焰之血";
                default: return "Fireblood";
            }
        }

        ///<summary>spell=188389</summary>
        private static string FlameShock_SpellName()
        {
            switch (Language)
            {
                case "English": return "Flame Shock";
                case "Deutsch": return "Flammenschock";
                case "Español": return "Choque de llamas";
                case "Français": return "Horion de flamme";
                case "Italiano": return "Folgore del Fuoco";
                case "Português Brasileiro": return "Choque Flamejante";
                case "Русский": return "Огненный шок";
                case "한국어": return "화염 충격";
                case "简体中文": return "烈焰震击";
                default: return "Flame Shock";
            }
        }

        ///<summary>spell=318038</summary>
        private static string FlametongueWeapon_SpellName()
        {
            switch (Language)
            {
                case "English": return "Flametongue Weapon";
                case "Deutsch": return "Waffe der Flammenzunge";
                case "Español": return "Arma Lengua de Fuego";
                case "Français": return "Arme langue de feu";
                case "Italiano": return "Arma della Lingua di Fuoco";
                case "Português Brasileiro": return "Arma de Labaredas";
                case "Русский": return "Оружие языка пламени";
                case "한국어": return "불꽃혓바닥 무기";
                case "简体中文": return "火舌武器";
                default: return "Flametongue Weapon";
            }
        }

        ///<summary>spell=196840</summary>
        private static string FrostShock_SpellName()
        {
            switch (Language)
            {
                case "English": return "Frost Shock";
                case "Deutsch": return "Frostschock";
                case "Español": return "Choque de Escarcha";
                case "Français": return "Horion de givre";
                case "Italiano": return "Folgore del Gelo";
                case "Português Brasileiro": return "Choque Gélido";
                case "Русский": return "Ледяной шок";
                case "한국어": return "냉기 충격";
                case "简体中文": return "冰霜震击";
                default: return "Frost Shock";
            }
        }

        ///<summary>spell=2645</summary>
        private static string GhostWolf_SpellName()
        {
            switch (Language)
            {
                case "English": return "Ghost Wolf";
                case "Deutsch": return "Geisterwolf";
                case "Español": return "Lobo fantasmal";
                case "Français": return "Loup fantôme";
                case "Italiano": return "Lupo Spettrale";
                case "Português Brasileiro": return "Lobo Fantasma";
                case "Русский": return "Призрачный волк";
                case "한국어": return "늑대 정령";
                case "简体中文": return "幽魂之狼";
                default: return "Ghost Wolf";
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

        ///<summary>spell=378773</summary>
        private static string GreaterPurge_SpellName()
        {
            switch (Language)
            {
                case "English": return "Greater Purge";
                case "Deutsch": return "Großes Reinigen";
                case "Español": return "Purgar superior";
                case "Français": return "Purge supérieure";
                case "Italiano": return "Epurazione Superiore";
                case "Português Brasileiro": return "Expurgo Maior";
                case "Русский": return "Великое очищение";
                case "한국어": return "상급 정화";
                case "简体中文": return "强效净化术";
                default: return "Greater Purge";
            }
        }

        ///<summary>spell=5394</summary>
        private static string HealingStreamTotem_SpellName()
        {
            switch (Language)
            {
                case "English": return "Healing Stream Totem";
                case "Deutsch": return "Totem des heilenden Flusses";
                case "Español": return "Tótem Corriente de sanación";
                case "Français": return "Totem guérisseur";
                case "Italiano": return "Totem del Flusso Vitale";
                case "Português Brasileiro": return "Totem de Torrente Curativa";
                case "Русский": return "Тотем исцеляющего потока";
                case "한국어": return "치유의 토템";
                case "简体中文": return "治疗之泉图腾";
                default: return "Healing Stream Totem";
            }
        }

        ///<summary>spell=8004</summary>
        private static string HealingSurge_SpellName()
        {
            switch (Language)
            {
                case "English": return "Healing Surge";
                case "Deutsch": return "Heilende Woge";
                case "Español": return "Oleada de sanación";
                case "Français": return "Afflux de soins";
                case "Italiano": return "Eruzione Benefica";
                case "Português Brasileiro": return "Maré Curativa";
                case "Русский": return "Исцеляющий всплеск";
                case "한국어": return "치유의 파도";
                case "简体中文": return "治疗之涌";
                default: return "Healing Surge";
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

        ///<summary>spell=32182</summary>
        private static string Heroism_SpellName()
        {
            switch (Language)
            {
                case "English": return "Heroism";
                case "Deutsch": return "Heldentum";
                case "Español": return "Heroísmo";
                case "Français": return "Héroïsme";
                case "Italiano": return "Eroismo";
                case "Português Brasileiro": return "Heroísmo";
                case "Русский": return "Героизм";
                case "한국어": return "영웅심";
                case "简体中文": return "英勇";
                default: return "Heroism";
            }
        }

        ///<summary>spell=51514</summary>
        private static string Hex_SpellName()
        {
            switch (Language)
            {
                case "English": return "Hex";
                case "Deutsch": return "Verhexen";
                case "Español": return "Maleficio";
                case "Français": return "Maléfice";
                case "Italiano": return "Maleficio";
                case "Português Brasileiro": return "Bagata";
                case "Русский": return "Сглаз";
                case "한국어": return "사술";
                case "简体中文": return "妖术";
                default: return "Hex";
            }
        }

        ///<summary>spell=342240</summary>
        private static string IceStrike_SpellName()
        {
            switch (Language)
            {
                case "English": return "Ice Strike";
                case "Deutsch": return "Eisstoß";
                case "Español": return "Golpe de hielo";
                case "Français": return "Frappe de glace";
                case "Italiano": return "Assalto di Ghiaccio";
                case "Português Brasileiro": return "Golpe Glacial";
                case "Русский": return "Ледяной клинок";
                case "한국어": return "한랭의 일격";
                case "简体中文": return "霜刃打击";
                default: return "Ice Strike";
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

        ///<summary>spell=51505</summary>
        private static string LavaBurst_SpellName()
        {
            switch (Language)
            {
                case "English": return "Lava Burst";
                case "Deutsch": return "Lavaeruption";
                case "Español": return "Ráfaga de lava";
                case "Français": return "Explosion de lave";
                case "Italiano": return "Getto di Lava";
                case "Português Brasileiro": return "Estouro de Lava";
                case "Русский": return "Выброс лавы";
                case "한국어": return "용암 폭발";
                case "简体中文": return "熔岩爆裂";
                default: return "Lava Burst";
            }
        }

        ///<summary>spell=60103</summary>
        private static string LavaLash_SpellName()
        {
            switch (Language)
            {
                case "English": return "Lava Lash";
                case "Deutsch": return "Lavapeitsche";
                case "Español": return "Latigazo de lava";
                case "Français": return "Fouet de lave";
                case "Italiano": return "Sferzata Lavica";
                case "Português Brasileiro": return "Açoite de Lava";
                case "Русский": return "Вскипание лавы";
                case "한국어": return "용암 채찍";
                case "简体中文": return "熔岩猛击";
                default: return "Lava Lash";
            }
        }

        ///<summary>spell=188196</summary>
        private static string LightningBolt_SpellName()
        {
            switch (Language)
            {
                case "English": return "Lightning Bolt";
                case "Deutsch": return "Blitzschlag";
                case "Español": return "Descarga de relámpagos";
                case "Français": return "Éclair";
                case "Italiano": return "Dardo Fulminante";
                case "Português Brasileiro": return "Raio";
                case "Русский": return "Молния";
                case "한국어": return "번개 화살";
                case "简体中文": return "闪电箭";
                default: return "Lightning Bolt";
            }
        }

        ///<summary>spell=305483</summary>
        private static string LightningLasso_SpellName()
        {
            switch (Language)
            {
                case "English": return "Lightning Lasso";
                case "Deutsch": return "Blitzschlaglasso";
                case "Español": return "Lazo de relámpagos";
                case "Français": return "Lasso de foudre";
                case "Italiano": return "Lazo Fulminante";
                case "Português Brasileiro": return "Laço de Raio";
                case "Русский": return "Молния-лассо";
                case "한국어": return "번개 올가미";
                case "简体中文": return "闪电磁索";
                default: return "Lightning Lasso";
            }
        }

        ///<summary>spell=192106</summary>
        private static string LightningShield_SpellName()
        {
            switch (Language)
            {
                case "English": return "Lightning Shield";
                case "Deutsch": return "Blitzschlagschild";
                case "Español": return "Escudo de relámpagos";
                case "Français": return "Bouclier de foudre";
                case "Italiano": return "Scudo di Fulmini";
                case "Português Brasileiro": return "Escudo de Raios";
                case "Русский": return "Щит молний";
                case "한국어": return "번개 보호막";
                case "简体中文": return "闪电之盾";
                default: return "Lightning Shield";
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

        ///<summary>spell=187880</summary>
        private static string MaelstromWeapon_SpellName()
        {
            switch (Language)
            {
                case "English": return "Maelstrom Weapon";
                case "Deutsch": return "Waffe des Mahlstroms";
                case "Español": return "Arma vorágine";
                case "Français": return "Arme du Maelström";
                case "Italiano": return "Arma del Maelstrom";
                case "Português Brasileiro": return "Arma da Voragem";
                case "Русский": return "Оружие Водоворота";
                case "한국어": return "소용돌이치는 무기";
                case "简体中文": return "漩涡武器";
                default: return "Maelstrom Weapon";
            }
        }

        ///<summary>spell=378081</summary>
        private static string NaturesSwiftness_SpellName()
        {
            switch (Language)
            {
                case "English": return "Nature's Swiftness";
                case "Deutsch": return "Schnelligkeit der Natur";
                case "Español": return "Presteza de la Naturaleza";
                case "Français": return "Rapidité de la nature";
                case "Italiano": return "Rapidità della Natura";
                case "Português Brasileiro": return "Rapidez da Natureza";
                case "Русский": return "Природная стремительность";
                case "한국어": return "자연의 신속함";
                case "简体中文": return "自然迅捷";
                default: return "Nature's Swiftness";
            }
        }

        ///<summary>spell=73899</summary>
        private static string PrimalStrike_SpellName()
        {
            switch (Language)
            {
                case "English": return "Primal Strike";
                case "Deutsch": return "Urtümlicher Schlag";
                case "Español": return "Golpe primigenio";
                case "Français": return "Frappe primordiale";
                case "Italiano": return "Assalto Primordiale";
                case "Português Brasileiro": return "Golpe Primevo";
                case "Русский": return "Стихийный удар";
                case "한국어": return "원시의 일격";
                case "简体中文": return "根源打击";
                default: return "Primal Strike";
            }
        }

        ///<summary>spell=326059</summary>
        private static string PrimordialWave_SpellName()
        {
            switch (Language)
            {
                case "English": return "Primordial Wave";
                case "Deutsch": return "Urzeitliche Welle";
                case "Español": return "Oleada primigenia";
                case "Français": return "Vague primordiale";
                case "Italiano": return "Ondata Primordiale";
                case "Português Brasileiro": return "Onda Primordial";
                case "Русский": return "Первозданная волна";
                case "한국어": return "태고의 파도";
                case "简体中文": return "始源之潮";
                default: return "Primordial Wave";
            }
        }

        ///<summary>spell=370</summary>
        private static string Purge_SpellName()
        {
            switch (Language)
            {
                case "English": return "Purge";
                case "Deutsch": return "Reinigen";
                case "Español": return "Purgar";
                case "Français": return "Purge";
                case "Italiano": return "Epurazione";
                case "Português Brasileiro": return "Expurgar";
                case "Русский": return "Развеивание магии";
                case "한국어": return "정화";
                case "简体中文": return "净化术";
                default: return "Purge";
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

        ///<summary>spell=20594</summary>
        private static string Stoneform_SpellName()
        {
            switch (Language)
            {
                case "English": return "Stoneform";
                case "Deutsch": return "Steingestalt";
                case "Español": return "Forma de piedra";
                case "Français": return "Forme de pierre";
                case "Italiano": return "Forma di Pietra";
                case "Português Brasileiro": return "Forma de Pedra";
                case "Русский": return "Каменная форма";
                case "한국어": return "석화";
                case "简体中文": return "石像形态";
                default: return "Stoneform";
            }
        }

        ///<summary>spell=191634</summary>
        private static string Stormkeeper_SpellName()
        {
            switch (Language)
            {
                case "English": return "Stormkeeper";
                case "Deutsch": return "Sturmhüter";
                case "Español": return "Guardia de la Tormenta";
                case "Français": return "Gardien des tempêtes";
                case "Italiano": return "Custode della Tempesta";
                case "Português Brasileiro": return "Guardião da Tempestade";
                case "Русский": return "Хранитель бурь";
                case "한국어": return "폭풍수호자";
                case "简体中文": return "风暴守护者";
                default: return "Stormkeeper";
            }
        }

        ///<summary>spell=17364</summary>
        private static string Stormstrike_SpellName()
        {
            switch (Language)
            {
                case "English": return "Stormstrike";
                case "Deutsch": return "Sturmschlag";
                case "Español": return "Golpe de tormenta";
                case "Français": return "Frappe-tempête";
                case "Italiano": return "Assalto della Tempesta";
                case "Português Brasileiro": return "Ataque da Tempestade";
                case "Русский": return "Удар бури";
                case "한국어": return "폭풍의 일격";
                case "简体中文": return "风暴打击";
                default: return "Stormstrike";
            }
        }

        ///<summary>spell=197214</summary>
        private static string Sundering_SpellName()
        {
            switch (Language)
            {
                case "English": return "Sundering";
                case "Deutsch": return "Zerschlagung";
                case "Español": return "Hendimiento";
                case "Français": return "Fracture";
                case "Italiano": return "Scissione";
                case "Português Brasileiro": return "Cisão";
                case "Русский": return "Раскол";
                case "한국어": return "세계의 분리";
                case "简体中文": return "裂地术";
                default: return "Sundering";
            }
        }
                ///<summary>spell=51490</summary>
        private static string Thunderstorm_SpellName()
        {
            switch (Language)
            {
                case "English": return "Thunderstorm";
                case "Deutsch": return "Gewitter";
                case "Español": return "Tormenta de truenos";
                case "Français": return "Orage";
                case "Italiano": return "Esplosione Tonante";
                case "Português Brasileiro": return "Tempestade Relampejante";
                case "Русский": return "Гром и молния";
                case "한국어": return "천둥폭풍";
                case "简体中文": return "雷霆风暴";
                default: return "Thunderstorm";
            }
        }

        ///<summary>spell=204366</summary>
        private static string Thundercharge_SpellName()
        {
            switch (Language)
            {
                case "English": return "Thundercharge";
                case "Deutsch": return "Donnerladung";
                case "Español": return "Carga de trueno";
                case "Français": return "Charge de tonnerre";
                case "Italiano": return "Carica Tonante";
                case "Português Brasileiro": return "Carga Trovejante";
                case "Русский": return "Грозовой заряд";
                case "한국어": return "천둥충전";
                case "简体中文": return "雷霆充能";
                default: return "Thundercharge";
            }
        }

        ///<summary>spell=8143</summary>
        private static string TremorTotem_SpellName()
        {
            switch (Language)
            {
                case "English": return "Tremor Totem";
                case "Deutsch": return "Totem des Erdstoßes";
                case "Español": return "Tótem de tremor";
                case "Français": return "Totem de séisme";
                case "Italiano": return "Totem del Tremore";
                case "Português Brasileiro": return "Totem Sísmico";
                case "Русский": return "Тотем трепета";
                case "한국어": return "진동의 토템";
                case "简体中文": return "战栗图腾";
                default: return "Tremor Totem";
            }
        }

        ///<summary>spell=324386</summary>
        private static string VesperTotem_SpellName()
        {
            switch (Language)
            {
                case "English": return "Vesper Totem";
                case "Deutsch": return "Vespertotem";
                case "Español": return "Tótem de campana de vísperas";
                case "Français": return "Totem carillonneur";
                case "Italiano": return "Totem del Vespro";
                case "Português Brasileiro": return "Totem de Véspera";
                case "Русский": return "Тотем вечернего колокола";
                case "한국어": return "만과 토템";
                case "简体中文": return "暮钟图腾";
                default: return "Vesper Totem";
            }
        }

        ///<summary>spell=20549</summary>
        private static string WarStomp_SpellName()
        {
            switch (Language)
            {
                case "English": return "War Stomp";
                case "Deutsch": return "Kriegsdonner";
                case "Español": return "Pisotón de guerra";
                case "Français": return "Choc martial";
                case "Italiano": return "Zoccolo di Guerra";
                case "Português Brasileiro": return "Pisada de Guerra";
                case "Русский": return "Громовая поступь";
                case "한국어": return "전투 발구르기";
                case "简体中文": return "战争践踏";
                default: return "War Stomp";
            }
        }

        ///<summary>spell=7744</summary>
        private static string WillOfTheForsaken_SpellName()
        {
            switch (Language)
            {
                case "English": return "Will of the Forsaken";
                case "Deutsch": return "Wille der Verlassenen";
                case "Español": return "Voluntad de los Renegados";
                case "Français": return "Volonté des Réprouvés";
                case "Italiano": return "Volontà dei Reietti";
                case "Português Brasileiro": return "Determinação dos Renegados";
                case "Русский": return "Воля Отрекшихся";
                case "한국어": return "포세이큰의 의지";
                case "简体中文": return "被遗忘者的意志";
                default: return "Will of the Forsaken";
            }
        }

        ///<summary>spell=59752</summary>
        private static string WillToSurvive_SpellName()
        {
            switch (Language)
            {
                case "English": return "Will to Survive";
                case "Deutsch": return "Überlebenswille";
                case "Español": return "Lucha por la supervivencia";
                case "Français": return "Volonté de survie";
                case "Italiano": return "Volontà di Sopravvivenza";
                case "Português Brasileiro": return "Desejo de Sobreviver";
                case "Русский": return "Воля к жизни";
                case "한국어": return "삶의 의지";
                case "简体中文": return "生存意志";
                default: return "Will to Survive";
            }
        }

        ///<summary>spell=192077</summary>
        private static string WindRushTotem_SpellName()
        {
            switch (Language)
            {
                case "English": return "Wind Rush Totem";
                case "Deutsch": return "Totem des Windsturms";
                case "Español": return "Tótem de Carga de viento";
                case "Français": return "Totem de bouffée de vent";
                case "Italiano": return "Totem del Soffio di Vento";
                case "Português Brasileiro": return "Totem de Rajada de Vento";
                case "Русский": return "Тотем ветряного порыва";
                case "한국어": return "바람 질주 토템";
                case "简体中文": return "狂风图腾";
                default: return "Wind Rush Totem";
            }
        }

        ///<summary>spell=57994</summary>
        private static string WindShear_SpellName()
        {
            switch (Language)
            {
                case "English": return "Wind Shear";
                case "Deutsch": return "Windstoß";
                case "Español": return "Corte de viento";
                case "Français": return "Cisaille de vent";
                case "Italiano": return "Raffica di Vento";
                case "Português Brasileiro": return "Rajada de Vento";
                case "Русский": return "Пронизывающий ветер";
                case "한국어": return "날카로운 바람";
                case "简体中文": return "风剪";
                default: return "Wind Shear";
            }
        }

        ///<summary>spell=8512</summary>
        private static string WindfuryTotem_SpellName()
        {
            switch (Language)
            {
                case "English": return "Windfury Totem";
                case "Deutsch": return "Totem des Windzorns";
                case "Español": return "Tótem Viento furioso";
                case "Français": return "Totem Furie-des-vents";
                case "Italiano": return "Totem della Furia del Vento";
                case "Português Brasileiro": return "Totem de Fúria dos Ventos";
                case "Русский": return "Тотем неистовства ветра";
                case "한국어": return "질풍의 토템";
                case "简体中文": return "风怒图腾";
                default: return "Windfury Totem";
            }
        }

        ///<summary>spell=33757</summary>
        private static string WindfuryWeapon_SpellName()
        {
            switch (Language)
            {
                case "English": return "Windfury Weapon";
                case "Deutsch": return "Waffe des Windzorns";
                case "Español": return "Arma Viento furioso";
                case "Français": return "Arme Furie-des-vents";
                case "Italiano": return "Arma della Furia del Vento";
                case "Português Brasileiro": return "Arma de Fúria dos Ventos";
                case "Русский": return "Оружие неистовства ветра";
                case "한국어": return "질풍의 무기";
                case "简体中文": return "风怒武器";
                default: return "Windfury Weapon";
            }
        }

        ///<summary>spell=115356</summary>
        private static string Windstrike_SpellName()
        {
            switch (Language)
            {
                case "English": return "Windstrike";
                case "Deutsch": return "Windschlag";
                case "Español": return "Golpe de viento";
                case "Français": return "Frappevent";
                case "Italiano": return "Assalto del Vento";
                case "Português Brasileiro": return "Golpe de Vento";
                case "Русский": return "Удар ветра";
                case "한국어": return "바람의 일격";
                case "简体中文": return "风切";
                default: return "Windstrike";
            }
        }
                ///<summary>spell=108285</summary>
        private static string TotemicRecall_SpellName()
        {
            switch (Language)
            {
                case "English": return "Totemic Recall";
                case "Deutsch": return "Totemischer Rückruf";
                case "Español": return "Regreso totémico";
                case "Français": return "Rappel totémique";
                case "Italiano": return "Richiamo dei Totem";
                case "Português Brasileiro": return "Revocação Totêmica";
                case "Русский": return "Возвращение тотемов";
                case "한국어": return "토템의 귀환";
                case "简体中文": return "收回图腾";
                default: return "Totemic Recall";
            }
        }
                ///<summary>spell=79206</summary>
        private static string SpiritwalkersGrace_SpellName()
        {
            switch (Language)
            {
                case "English": return "Spiritwalker's Grace";
                case "Deutsch": return "Gunst des Geistwandlers";
                case "Español": return "Gracia del caminaespíritus";
                case "Français": return "Grâce du marcheur des esprits";
                case "Italiano": return "Grazia dello Spiritista";
                case "Português Brasileiro": return "Graça do Andarilho Espiritual";
                case "Русский": return "Благосклонность предков";
                case "한국어": return "영혼나그네의 은총";
                case "简体中文": return "灵魂行者的恩赐";
                default: return "Spiritwalker's Grace";
            }
        }
                private static string Skyfury_SpellName()//462854
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Himmelszorn";
                case "Español":
                    return "Furia del cielo";
                case "Français":
                    return "Fureur-du-ciel";
                case "Italiano":
                    return "Furia del Cielo";
                case "Português Brasileiro":
                    return "Fúria Celeste";
                case "Русский":
                    return "Небесная ярость";
                case "한국어":
                    return "하늘의 격노";
                case "简体中文":
                    return "天怒";
                default:
                    return "Skyfury";
            }
        }
                ///<summary>spell=452201</summary>454015 454009 
        private static string Tempest_SpellName()
        {
            switch (Language)
            {
                case "English": return "Tempest";
                case "Deutsch": return "Sturmgewitter";
                case "Español": return "Tempestad";
                case "Français": return "Tempête";
                case "Italiano": return "Tempesta";
                case "Português Brasileiro": return "Tormenta";
                case "Русский": return "Буря";
                case "한국어": return "폭풍";
                case "简体中文": return "狂风怒号";
                default: return "Tempest";
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
        private static string IllusionaryVulpin_NPCName()
    {
        switch (Language)
        {
            case "Deutsch":
                return "Illusionärer Vulpin";
            case "Español":
                return "Vulpino ilusorio";
            case "Français":
                return "Vulpin illusoire";
            case "Italiano":
                return "Volpino Illusorio";
            case "Português Brasileiro":
                return "Vulpin Ilusório";
            case "Русский":
                return "Иллюзорный лисохвост";
            case "한국어":
                return "환영 여우";
            case "简体中文":
                return "幻影仙狐";
            default:
                return "Illusionary Vulpin";
        }
    }
    private static string PoisonCleansingTotem_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Totem der Giftreinigung";
        case "Español":
            return "Tótem de limpieza contraveneno";
        case "Français":
            return "Totem de purification du poison";
        case "Italiano":
            return "Totem della Purificazione del Veleno";
        case "Português Brasileiro":
            return "Totem Antiveneno";
        case "Русский":
            return "Тотем противоядия";
        case "한국어":
            return "독 정화 토템";
        case "简体中文":
            return "清毒图腾";
        default:
            return "Poison Cleansing Totem";
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
private static string SurgingTotem_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Wogendes Totem";
        case "Español":
            return "Tótem emergente";
        case "Français":
            return "Totem déferlant";
        case "Italiano":
            return "Totem Crescente";
        case "Português Brasileiro":
            return "Totem de Irrupção";
        case "Русский":
            return "Пульсирующий тотем";
        case "한국어":
            return "쇄도하는 토템";
        case "简体中文":
            return "涌动图腾";
        default:
            return "Surging Totem";
    }
}
private static string VoltaicBlaze_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Voltaiklohe";
        case "Español":
            return "Llamarada voltaica";
        case "Français":
            return "Brasier voltaïque";
        case "Italiano":
            return "Fiammata Voltaica";
        case "Português Brasileiro":
            return "Labareda Voltaica";
        case "Русский":
            return "Гальваническое пламя";
        case "한국어":
            return "전격의 불길";
        case "简体中文":
            return "流电炽焰";
        default:
            return "Voltaic Blaze";
    }
}
private static string StoneBulwarkTotem_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Totem des Steinbollwerks";
        case "Español":
            return "Tótem de Baluarte pétreo";
        case "Français":
            return "Totem rempart de pierre";
        case "Italiano":
            return "Totem del Baluardo di Pietra";
        case "Português Brasileiro":
            return "Totem de Baluarte Pétreo";
        case "Русский":
            return "Тотем каменной преграды";
        case "한국어":
            return "돌의 보루 토템";
        case "简体中文":
            return "石壁图腾";
        default:
            return "Stone Bulwark Totem";
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
private static string PrimordialStorm_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Urzeitlicher Sturm";
        case "Español":
            return "Tormenta primigenia";
        case "Français":
            return "Tempête primordiale";
        case "Italiano":
            return "Tempesta Primordiale";
        case "Português Brasileiro":
            return "Tempestade Primordial";
        case "Русский":
            return "Первозданная буря";
        case "한국어":
            return "태고의 폭풍";
        case "简体中文":
            return "始源风暴";
        default:
            return "Primordial Storm";
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

            Inferno.PrintMessage("Epic Rotations Enhancement Shaman", Color.Purple);
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
            //SpellCasts.AddSpellId, Example_SpellName());
            SpellCasts.Add(375982,PrimordialWave_SpellName()); //375982 
            SpellCasts.Add(452201,Tempest_SpellName()); //452201
            //SpellCasts.Add(108281, AncestralGuidance_SpellName()); //108281
            SpellCasts.Add(108271, AstralShift_SpellName()); //108271
            //SpellCasts.Add(2825, Bloodlust_SpellName()); //2825
            SpellCasts.Add(192058, CapacitorTotem_SpellName()); //192058
            //SpellCasts.Add(1064, ChainHeal_SpellName()); //1064
            SpellCasts.Add(188443, ChainLightning_SpellName()); //188443
            SpellCasts.Add(198103, EarthElemental_SpellName()); //198103
            //SpellCasts.Add(2484, EarthbindTotem_SpellName()); //2484
            SpellCasts.Add(188389, FlameShock_SpellName()); //188389
            SpellCasts.Add(470411, FlameShock_SpellName()); //470411
            SpellCasts.Add(196840, FrostShock_SpellName()); //196840
            SpellCasts.Add(2645, GhostWolf_SpellName()); //2645
            SpellCasts.Add(378773, GreaterPurge_SpellName()); //378773
            SpellCasts.Add(5394, HealingStreamTotem_SpellName()); //5394
            SpellCasts.Add(8004, HealingSurge_SpellName()); //8004
            //SpellCasts.Add(32182, Heroism_SpellName()); //32182
            //SpellCasts.Add(32182, Heroism_SpellName()); //32182
            SpellCasts.Add(51514, Hex_SpellName()); //51514
            SpellCasts.Add(188196, LightningBolt_SpellName()); //188196
            SpellCasts.Add(454009, LightningBolt_SpellName()); //454009
            //SpellCasts.Add(305483, LightningLasso_SpellName()); //305483
            SpellCasts.Add(192106, LightningShield_SpellName()); //192106
            SpellCasts.Add(378081, NaturesSwiftness_SpellName()); //378081
            SpellCasts.Add(73899, PrimalStrike_SpellName()); //73899
            SpellCasts.Add(370, Purge_SpellName()); //370
            SpellCasts.Add(8143, TremorTotem_SpellName()); //8143
            SpellCasts.Add(114051, Ascendance_SpellName()); //114051
            SpellCasts.Add(51886, CleanseSpirit_SpellName()); //51886
            SpellCasts.Add(187874, CrashLightning_SpellName()); //187874
            SpellCasts.Add(384352, DoomWinds_SpellName()); //384352 
            SpellCasts.Add(974, EarthShield_SpellName()); //974
            SpellCasts.Add(117014, ElementalBlast_SpellName()); //117014
            SpellCasts.Add(196884, FeralLunge_SpellName()); //196884
            SpellCasts.Add(51533, FeralSpirit_SpellName()); //51533
            SpellCasts.Add(333974, FireNova_SpellName()); //333974
            SpellCasts.Add(342240, IceStrike_SpellName()); //342240
            SpellCasts.Add(470194, IceStrike_SpellName()); //470194
            SpellCasts.Add(51505, LavaBurst_SpellName()); //51505
            SpellCasts.Add(60103, LavaLash_SpellName()); //60103
            SpellCasts.Add(320137, Stormkeeper_SpellName()); //320137
            SpellCasts.Add(17364, Stormstrike_SpellName()); //17364
            SpellCasts.Add(197214, Sundering_SpellName()); //197214
            SpellCasts.Add(204366, Thundercharge_SpellName()); //204366
            SpellCasts.Add(192077, WindRushTotem_SpellName()); //192077
            SpellCasts.Add(8512, WindfuryTotem_SpellName()); //8512
            SpellCasts.Add(33757, WindfuryWeapon_SpellName()); //33757
            SpellCasts.Add(115356, Windstrike_SpellName()); //115356
            SpellCasts.Add( 57994, WindShear_SpellName()); // 57994
            //SpellCasts.Add( 33697, BloodFury_SpellName()); // 33697
            SpellCasts.Add( 462854, Skyfury_SpellName()); // 462854
            //SpellCasts.Add( 26297 , Berserking_SpellName()); // 26297 
            //No space
            //SpellCasts.Add( 444995, SurgingTotem_SpellName()); // 444995
            SpellCasts.Add( 470053, VoltaicBlaze_SpellName()); // 470053
            SpellCasts.Add( 470057, VoltaicBlaze_SpellName()); // 470057
            SpellCasts.Add( 1218090, PrimordialStorm_SpellName()); // 1218090

        
           

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
            Macros.Add("HealingPotion", "/use Healing Potion"); //Cus1
            Macros.Add("DPSPotion", "/use DPS Potion"); //Cus2
            



            MacroCasts.Add(1, "TopTrinket");
            MacroCasts.Add(2, "BottomTrinket");
            MacroCasts.Add(444995, "SurgingTotemMacro"); //444995
            MacroCasts.Add(318038, "FlameTongueWeapon"); //318038
            MacroCasts.Add(342240, "IceStrikeSpellName"); //342240
            MacroCasts.Add( 33697, "BloodFury"); //342240
            MacroCasts.Add(26297 , "Berserking"); //342240
            

            

  




            //Lists with spells to use with queues
            MouseoverQueues = new List<string>(){
                Purge_SpellName(),
                GreaterPurge_SpellName(),
                HealingSurge_SpellName(),
                CleanseSpirit_SpellName(),
                WindShear_SpellName(),
                Hex_SpellName(),
                //LightningLasso_SpellName(),


            };
            CursorQueues = new List<string>(){
                CapacitorTotem_SpellName(),
                //EarthbindTotem_SpellName(),
                WindRushTotem_SpellName(),
                
            };
            PlayerQueues = new List<string>(){

                HealingSurge_SpellName(),
                //AncestralGuidance_SpellName(),
                SpiritwalkersGrace_SpellName(),
                AstralShift_SpellName(),
                EarthElemental_SpellName(),
                TremorTotem_SpellName(),
                TotemicRecall_SpellName(),
                WindRushTotem_SpellName(),
                WindfuryTotem_SpellName(),
                Thunderstorm_SpellName(),
                PoisonCleansingTotem_SpellName(),
                StoneBulwarkTotem_SpellName(),
                HealingStreamTotem_SpellName(),
                FeralLunge_SpellName(),
                DoomWinds_SpellName(),
                CapacitorTotem_SpellName(),
                Sundering_SpellName(),


       
            };
            FocusQueues = new List<string>(){
                CleanseSpirit_SpellName(),
                EarthShield_SpellName(),

            };
            TargetQueues = new List<string>(){
            PrimordialWave_SpellName(),
            Tempest_SpellName(),
            FlameShock_SpellName(),
            FrostShock_SpellName(),
            PrimalStrike_SpellName(),
            Purge_SpellName(),
            CleanseSpirit_SpellName(),
            DoomWinds_SpellName(),
            FeralLunge_SpellName(),
            FeralSpirit_SpellName(),
            FireNova_SpellName(),
            IceStrike_SpellName(),
            LavaBurst_SpellName(),
            LavaLash_SpellName(),
            VoltaicBlaze_SpellName(),
            Ascendance_SpellName(),
         
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

