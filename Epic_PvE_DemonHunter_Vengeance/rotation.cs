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
        private static string ArcaneTorrent_SpellName(string Language = "English")
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

        ///<summary>spell=320341</summary>
        private static string BulkExtraction_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Bulk Extraction";
                case "Deutsch": return "Massenextraktion";
                case "Español": return "Extracción en masa";
                case "Français": return "Extraction de masse";
                case "Italiano": return "Estrazione Forzata";
                case "Português Brasileiro": return "Extração em Massa";
                case "Русский": return "Массовое извлечение";
                case "한국어": return "대량 추출";
                case "简体中文": return "噬众";
                default: return "Bulk Extraction";
            }
        }

        ///<summary>spell=179057</summary>
        private static string ChaosNova_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Chaos Nova";
                case "Deutsch": return "Chaosnova";
                case "Español": return "Nova de caos";
                case "Français": return "Nova du chaos";
                case "Italiano": return "Nova del Caos";
                case "Português Brasileiro": return "Nova do Caos";
                case "Русский": return "Кольцо Хаоса";
                case "한국어": return "혼돈의 회오리";
                case "简体中文": return "混乱新星";
                default: return "Chaos Nova";
            }
        }

        ///<summary>spell=278326</summary>
        private static string ConsumeMagic_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Consume Magic";
                case "Deutsch": return "Magie aufzehren";
                case "Español": return "Consumo de magia";
                case "Français": return "Manavore";
                case "Italiano": return "Consumo Magia";
                case "Português Brasileiro": return "Consumir Magia";
                case "Русский": return "Поглощение магии";
                case "한국어": return "마법 삼키기";
                case "简体中文": return "吞噬魔法";
                default: return "Consume Magic";
            }
        }

        ///<summary>spell=196718</summary>
        private static string Darkness_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Darkness";
                case "Deutsch": return "Dunkelheit";
                case "Español": return "Oscuridad";
                case "Français": return "Ténèbres";
                case "Italiano": return "Oscurità";
                case "Português Brasileiro": return "Trevas";
                case "Русский": return "Мрак";
                case "한국어": return "어둠";
                case "简体中文": return "黑暗";
                default: return "Darkness";
            }
        }

        ///<summary>spell=203720</summary>
        private static string DemonSpikes_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Demon Spikes";
                case "Deutsch": return "Dämonenstachel";
                case "Español": return "Púas de demonio";
                case "Français": return "Pointes démoniaques";
                case "Italiano": return "Aculei Demoniaci";
                case "Português Brasileiro": return "Espinhos Demoníacos";
                case "Русский": return "Демонические шипы";
                case "한국어": return "악마 쐐기";
                case "简体中文": return "恶魔尖刺";
                default: return "Demon Spikes";
            }
        }

        ///<summary>spell=183752</summary>
        private static string Disrupt_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Disrupt";
                case "Deutsch": return "Unterbrechen";
                case "Español": return "Interrumpir";
                case "Français": return "Ébranlement";
                case "Italiano": return "Distruzione";
                case "Português Brasileiro": return "Interromper";
                case "Русский": return "Прерывание";
                case "한국어": return "분열";
                case "简体中文": return "瓦解";
                default: return "Disrupt";
            }
        }
        private static string SigilofSpite_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Zeichen der Bosheit";
                case "Español":
                    return "Sigilo de maldad";
                case "Français":
                    return "Cachet de dépit";
                case "Italiano":
                    return "Sigillo della Perfidia";
                case "Português Brasileiro":
                    return "Signo da Malevolência";
                case "Русский":
                    return "Печать злобы";
                case "한국어":
                    return "원한의 인장";
                case "简体中文":
                    return "怨念咒符";
                default:
                    return "Sigil of Spite";
            }
        }

        ///<summary>spell=390163</summary>
        private static string ElysianDecree_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Elysian Decree";
                case "Deutsch": return "Elysischer Erlass";
                case "Español": return "Decreto elisio";
                case "Français": return "Décret élyséen";
                case "Italiano": return "Decreto Elisio";
                case "Português Brasileiro": return "Decreto Elísio";
                case "Русский": return "Элизийский декрет";
                case "한국어": return "하늘의 칙령";
                case "简体中文": return "极乐敕令";
                default: return "Elysian Decree";
            }
        }

        ///<summary>spell=395160</summary>
        private static string Eruption_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Eruption";
                case "Deutsch": return "Eruption";
                case "Español": return "Erupción";
                case "Français": return "Eruption";
                case "Italiano": return "Eruzione";
                case "Português Brasileiro": return "Erupção";
                case "Русский": return "Извержение";
                case "한국어": return "분출";
                case "简体中文": return "喷发";
                default: return "Eruption";
            }
        }

        ///<summary>spell=232893</summary>
        private static string Felblade_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Felblade";
                case "Deutsch": return "Teufelsklinge";
                case "Español": return "Hoja mácula";
                case "Français": return "Gangrelame";
                case "Italiano": return "Vilspada";
                case "Português Brasileiro": return "Lâmina Vil";
                case "Русский": return "Клинок Скверны";
                case "한국어": return "지옥칼";
                case "简体中文": return "邪能之刃";
                default: return "Felblade";
            }
        }

        ///<summary>spell=212084</summary>
        private static string FelDevastation_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Fel Devastation";
                case "Deutsch": return "Dämonische Verwüstung";
                case "Español": return "Devastación vil";
                case "Français": return "Dévastation gangrenée";
                case "Italiano": return "Vildevastazione";
                case "Português Brasileiro": return "Devastação Vil";
                case "Русский": return "Опустошение Скверной";
                case "한국어": return "지옥 황폐";
                case "简体中文": return "邪能毁灭";
                default: return "Fel Devastation";
            }
        }

        ///<summary>spell=211881</summary>
        private static string FelEruption_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Fel Eruption";
                case "Deutsch": return "Teufelseruption";
                case "Español": return "Erupción vil";
                case "Français": return "Éruption gangrenée";
                case "Italiano": return "Vileruzione";
                case "Português Brasileiro": return "Erupção Vil";
                case "Русский": return "Извержение Скверны";
                case "한국어": return "지옥 분출";
                case "简体中文": return "邪能爆发";
                default: return "Fel Eruption";
            }
        }

        ///<summary>spell=204021</summary>
        private static string FieryBrand_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Fiery Brand";
                case "Deutsch": return "Flammendes Brandmal";
                case "Español": return "Marca ígnea";
                case "Français": return "Marque enflammée";
                case "Italiano": return "Marchiatura Ardente";
                case "Português Brasileiro": return "Marca Ardente";
                case "Русский": return "Огненное клеймо";
                case "한국어": return "불타는 낙인";
                case "简体中文": return "烈火烙印";
                default: return "Fiery Brand";
            }
        }

        ///<summary>spell=263642</summary>
        private static string Fracture_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Fracture";
                case "Deutsch": return "Fraktur";
                case "Español": return "Fractura";
                case "Français": return "Fracture";
                case "Italiano": return "Frattura";
                case "Português Brasileiro": return "Fratura";
                case "Русский": return "Разлом";
                case "한국어": return "균열";
                case "简体中文": return "破裂";
                default: return "Fracture";
            }
        }

        ///<summary>item=5512</summary>
        private static string Healthstone_SpellName(string Language = "English")
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

        ///<summary>spell=258920</summary>
        private static string ImmolationAura_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Immolation Aura";
                case "Deutsch": return "Feuerbrandaura";
                case "Español": return "Aura de inmolación";
                case "Français": return "Aura d’immolation";
                case "Italiano": return "Rogo Rovente";
                case "Português Brasileiro": return "Aura de Imolação";
                case "Русский": return "Обжигающий жар";
                case "한국어": return "제물의 오라";
                case "简体中文": return "献祭光环";
                default: return "Immolation Aura";
            }
        }

        ///<summary>spell=217832</summary>
        private static string Imprison_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Imprison";
                case "Deutsch": return "Einkerkern";
                case "Español": return "Encarcelar";
                case "Français": return "Emprisonnement";
                case "Italiano": return "Imprigionamento";
                case "Português Brasileiro": return "Aprisionar";
                case "Русский": return "Пленение";
                case "한국어": return "감금";
                case "简体中文": return "禁锢";
                default: return "Imprison";
            }
        }

        ///<summary>spell=189110</summary>
        private static string InfernalStrike_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Infernal Strike";
                case "Deutsch": return "Infernoeinschlag";
                case "Español": return "Golpe infernal";
                case "Français": return "Frappe infernale";
                case "Italiano": return "Assalto Infernale";
                case "Português Brasileiro": return "Golpe Infernal";
                case "Русский": return "Инфернальный удар";
                case "한국어": return "불지옥 일격";
                case "简体中文": return "地狱火撞击";
                default: return "Infernal Strike";
            }
        }

        ///<summary>spell=191427 187827</summary>
        private static string Metamorphosis_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Metamorphosis";
                case "Deutsch": return "Metamorphose";
                case "Español": return "Metamorfosis";
                case "Français": return "Métamorphose";
                case "Italiano": return "Metamorfosi Demoniaca";
                case "Português Brasileiro": return "Metamorfose";
                case "Русский": return "Метаморфоза";
                case "한국어": return "탈태";
                case "简体中文": return "恶魔变形";
                default: return "Metamorphosis";
            }
        }

        ///<summary>spell=58984</summary>
        private static string Shadowmeld_SpellName(string Language = "English")
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

        ///<summary>spell=203782</summary>
        private static string Shear_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Shear";
                case "Deutsch": return "Abscheren";
                case "Español": return "Hender";
                case "Français": return "Entaille";
                case "Italiano": return "Recisione";
                case "Português Brasileiro": return "Talhar";
                case "Русский": return "Иссечение";
                case "한국어": return "절단";
                case "简体中文": return "裂魂";
                default: return "Shear";
            }
        }

        ///<summary>spell=202138</summary>
        private static string SigilOfChains_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Sigil of Chains";
                case "Deutsch": return "Zeichen der Ketten";
                case "Español": return "Sigilo de cadenas";
                case "Français": return "Sigil de chaînes";
                case "Italiano": return "Sigillo delle Catene";
                case "Português Brasileiro": return "Signo dos Grilhões";
                case "Русский": return "Печать цепей";
                case "한국어": return "사슬의 인장";
                case "简体中文": return "锁链咒符";
                default: return "Sigil of Chains";
            }
        }

        ///<summary>spell=204596</summary>
        private static string SigilOfFlame_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Sigil of Flame";
                case "Deutsch": return "Zeichen der Flamme";
                case "Español": return "Sigilo de llamas";
                case "Français": return "Sigil de feu";
                case "Italiano": return "Sigillo della Fiamma";
                case "Português Brasileiro": return "Signo da Chama";
                case "Русский": return "Печать огня";
                case "한국어": return "불꽃의 인장";
                case "简体中文": return "烈焰咒符";
                default: return "Sigil of Flame";
            }
        }

        ///<summary>spell=389813</summary>
        private static string SigilOfMisery_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Sigil of Misery";
                case "Deutsch": return "Zeichen des Elends";
                case "Español": return "Sigilo de desgracia";
                case "Français": return "Sigil de supplice";
                case "Italiano": return "Sigillo della Miseria";
                case "Português Brasileiro": return "Signo da Aflição";
                case "Русский": return "Печать страдания";
                case "한국어": return "불행의 인장";
                case "简体中文": return "悲苦咒符";
                default: return "Sigil of Misery";
            }
        }

        ///<summary>spell=202137</summary>
        private static string SigilOfSilence_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Sigil of Silence";
                case "Deutsch": return "Zeichen der Stille";
                case "Español": return "Sigilo de silencio";
                case "Français": return "Sigil de silence";
                case "Italiano": return "Sigillo del Silenzio";
                case "Português Brasileiro": return "Signo do Silêncio";
                case "Русский": return "Печать немоты";
                case "한국어": return "침묵의 인장";
                case "简体中文": return "沉默咒符";
                default: return "Sigil of Silence";
            }
        }

        ///<summary>spell=15487</summary>
        private static string Silence_SpellName(string Language = "English")
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

        ///<summary>spell=263648</summary>
        private static string SoulBarrier_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Soul Barrier";
                case "Deutsch": return "Seelenbarriere";
                case "Español": return "Barrera de alma";
                case "Français": return "Barrière d’âme";
                case "Italiano": return "Barriera d'Anima";
                case "Português Brasileiro": return "Barreira de Almas";
                case "Русский": return "Призрачный барьер";
                case "한국어": return "영혼 방벽";
                case "简体中文": return "灵魂壁障";
                default: return "Soul Barrier";
            }
        }

        ///<summary>spell=207407</summary>
        private static string SoulCarver_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Soul Carver";
                case "Deutsch": return "Seelenschnitzer";
                case "Español": return "Trinchador de almas";
                case "Français": return "Déchirement d’âme";
                case "Italiano": return "Intagliatore d'Anime";
                case "Português Brasileiro": return "Entalhador de Alma";
                case "Русский": return "Разрубатель душ";
                case "한국어": return "영혼 저미기";
                case "简体中文": return "灵魂切削";
                default: return "Soul Carver";
            }
        }
        private static string SoulSunder_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Seelenvernichtung";
        case "Español":
            return "Hender alma";
        case "Français":
            return "Scission de l’âme";
        case "Italiano":
            return "Fenditura dell'Anima";
        case "Português Brasileiro":
            return "Cisão da Alma";
        case "Русский":
            return "Раскол душ";
        case "한국어":
            return "영혼 가르기";
        case "简体中文":
            return "灵魂割裂";
        default:
            return "Soul Sunder";
    }
}


        ///<summary>spell=228477</summary>
        private static string SoulCleave_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Soul Cleave";
                case "Deutsch": return "Seelenspalter";
                case "Español": return "Rajar alma";
                case "Français": return "Division de l’âme";
                case "Italiano": return "Fendente d'Anima";
                case "Português Brasileiro": return "Cutilada da Alma";
                case "Русский": return "Раскалывание душ";
                case "한국어": return "영혼 베어내기";
                case "简体中文": return "灵魂裂劈";
                default: return "Soul Cleave";
            }
        }

        ///<summary>spell=247454</summary>
        private static string SpiritBomb_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Spirit Bomb";
                case "Deutsch": return "Seelenbombe";
                case "Español": return "Bomba de espíritu";
                case "Français": return "Bombe spirituelle";
                case "Italiano": return "Bomba Spirituale";
                case "Português Brasileiro": return "Bomba Espiritual";
                case "Русский": return "Взрывная душа";
                case "한국어": return "영혼 폭탄";
                case "简体中文": return "幽魂炸弹";
                default: return "Spirit Bomb";
            }
        }

        ///<summary>spell=370965</summary>
        private static string TheHunt_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "The Hunt";
                case "Deutsch": return "Die Jagd";
                case "Español": return "La caza";
                case "Français": return "La traque";
                case "Italiano": return "A Caccia";
                case "Português Brasileiro": return "A Caçada";
                case "Русский": return "Охота";
                case "한국어": return "사냥";
                case "简体中文": return "恶魔追击";
                default: return "The Hunt";
            }
        }

        ///<summary>spell=185123 204157</summary>
        private static string ThrowGlaive_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Throw Glaive";
                case "Deutsch": return "Gleve werfen";
                case "Español": return "Lanzar guja";
                case "Français": return "Lancer de glaive";
                case "Italiano": return "Lancio Lama";
                case "Português Brasileiro": return "Arremessar Glaive";
                case "Русский": return "Бросок боевого клинка";
                case "한국어": return "글레이브 투척";
                case "简体中文": return "投掷利刃";
                default: return "Throw Glaive";
            }
        }
        private static string GlaiveFlurry_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Glevenhagel";
                case "Español":
                    return "Aluvión de gujas";
                case "Français":
                    return "Rafale de glaive";
                case "Italiano":
                    return "Lame Vorticose";
                case "Português Brasileiro":
                    return "Rajada de Glaives";
                case "Русский":
                    return "Шквал глеф";
                case "한국어":
                    return "몰아치는 전투검";
                case "简体中文":
                    return "战刃乱舞";
                default:
                    return "Glaive Flurry";
            }
        }

        ///<summary>spell=185245</summary>
        private static string Torment_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Torment";
                case "Deutsch": return "Folter";
                case "Español": return "Tormento";
                case "Français": return "Tourment";
                case "Italiano": return "Tormento";
                case "Português Brasileiro": return "Tormento";
                case "Русский": return "Мучение";
                case "한국어": return "고문";
                case "简体中文": return "折磨";
                default: return "Torment";
            }
        }
        private static string ReaversGlaive_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Gleve des Reißers";
        case "Español":
            return "Guja de atracador";
        case "Français":
            return "Glaive de saccageur";
        case "Italiano":
            return "Lame del Razziatore";
        case "Português Brasileiro":
            return "Glaive do Aniquilador";
        case "Русский":
            return "Клинок разорителя";
        case "한국어":
            return "파괴자의 글레이브";
        case "简体中文":
            return "收割者战刃";
        default:
            return "Reaver's Glaive";
    }
}


        ///<summary>spell=198793</summary>
        private static string VengefulRetreat_SpellName(string Language = "English")
        {
            switch (Language)
            {
                case "English": return "Vengeful Retreat";
                case "Deutsch": return "Rachsüchtiger Rückzug";
                case "Español": return "Retirada vengativa";
                case "Français": return "Retraite vengeresse";
                case "Italiano": return "Ritiro Vendicativo";
                case "Português Brasileiro": return "Retirada Vingativa";
                case "Русский": return "Коварное отступление";
                case "한국어": return "복수의 퇴각";
                case "简体中文": return "复仇回避";
                default: return "Vengeful Retreat";
            }
        }
        private static string SpiritBurst_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Geistexplosion";
        case "Español":
            return "Ráfaga de espíritu";
        case "Français":
            return "Explosion spirituelle";
        case "Italiano":
            return "Getto dello Spirito";
        case "Português Brasileiro":
            return "Estouro Espiritual";
        case "Русский":
            return "Импульс духа";
        case "한국어":
            return "정신력 폭주";
        case "简体中文":
            return "灵魂爆裂";
        default:
            return "Spirit Burst";
    }
}
private static string GiftOfTheNaaru_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Gabe der Naaru";
                case "Español":
                    return "Ofrenda de los naaru";
                case "Français":
                    return "Don des Naaru";
                case "Italiano":
                    return "Dono dei Naaru";
                case "Português Brasileiro":
                    return "Dádiva dos Naarus";
                case "Русский":
                    return "Дар наару";
                case "한국어":
                    return "나루의 선물";
                case "简体中文":
                    return "纳鲁的赐福";
                default:
                    return "Gift of the Naaru";
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
        private static string EscapeArtist_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Entfesselungskünstler";
                case "Español":
                    return "Artista del escape";
                case "Français":
                    return "Maître de l’évasion";
                case "Italiano":
                    return "Artista della Fuga";
                case "Português Brasileiro":
                    return "Artista da Fuga";
                case "Русский":
                    return "Мастер побега";
                case "한국어":
                    return "탈출의 명수";
                case "简体中文":
                    return "逃命专家";
                default:
                    return "Escape Artist";
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
        private static string LightsJudgment_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Urteil des Lichts";
                case "Español":
                    return "Sentencia de la Luz";
                case "Français":
                    return "Jugement de la Lumière";
                case "Italiano":
                    return "Giudizio della Luce";
                case "Português Brasileiro":
                    return "Julgamento da Luz";
                case "Русский":
                    return "Правосудие Света";
                case "한국어":
                    return "빛의 심판";
                case "简体中文":
                    return "圣光裁决者";
                default:
                    return "Light's Judgment";
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
        private static string RocketBarrage_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Raketenbeschuss";
                case "Español":
                    return "Tromba de cohetes";
                case "Français":
                    return "Barrage de fusées";
                case "Italiano":
                    return "Raffica di Razzi";
                case "Português Brasileiro":
                    return "Barragem de Foguetes";
                case "Русский":
                    return "Ракетный обстрел";
                case "한국어":
                    return "로켓 연발탄";
                case "简体中文":
                    return "火箭弹幕";
                default:
                    return "Rocket Barrage";
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
        private static string Berserking_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Berserker";
                case "Español":
                    return "Rabiar";
                case "Français":
                    return "Berserker";
                case "Italiano":
                    return "Berserker";
                case "Português Brasileiro":
                    return "Berserk";
                case "Русский":
                    return "Берсерк";
                case "한국어":
                    return "광폭화";
                case "简体中文":
                    return "狂暴";
                default:
                    return "Berserking";
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
        private static string BagOfTricks_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Trickkiste";
                case "Español":
                    return "Bolsa de trucos";
                case "Français":
                    return "Sac à malice";
                case "Italiano":
                    return "Borsa di Trucchi";
                case "Português Brasileiro":
                    return "Bolsa de Truques";
                case "Русский":
                    return "Набор хитростей";
                case "한국어":
                    return "비장의 묘수";
                case "简体中文":
                    return "袋里乾坤";
                default:
                    return "Bag of Tricks";
            }
        }
        private static string BloodFury_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Kochendes Blut";
                case "Español":
                    return "Furia sangrienta";
                case "Français":
                    return "Fureur sanguinaire";
                case "Italiano":
                    return "Furia Sanguinaria";
                case "Português Brasileiro":
                    return "Fúria Sangrenta";
                case "Русский":
                    return "Кровавое неистовство";
                case "한국어":
                    return "피의 격노";
                case "简体中文":
                    return "血性狂怒";
                default:
                    return "Blood Fury";
            }
        }
        private static string ArcaneTorrent_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Arkaner Strom";
                case "Español":
                    return "Torrente Arcano";
                case "Français":
                    return "Torrent arcanique";
                case "Italiano":
                    return "Torrente Arcano";
                case "Português Brasileiro":
                    return "Torrente Arcana";
                case "Русский":
                    return "Волшебный поток";
                case "한국어":
                    return "비전 격류";
                case "简体中文":
                    return "奥术洪流";
                default:
                    return "Arcane Torrent";
            }
        }
        private static string Shadowmeld_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Schattenmimik";
                case "Español":
                    return "Fusión de las sombras";
                case "Français":
                    return "Camouflage dans l'ombre";
                case "Italiano":
                    return "Fondersi nelle Ombre";
                case "Português Brasileiro":
                    return "Fusão Sombria";
                case "Русский":
                    return "Слиться с тенью";
                case "한국어":
                    return "그림자 숨기";
                case "简体中文":
                    return "影遁";
                default:
                    return "Shadowmeld";
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
        private static string SigilofDoom_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Zeichen der Verdammnis";
                case "Español":
                    return "Sigilo de fatalidad";
                case "Français":
                    return "Cachet funeste";
                case "Italiano":
                    return "Sigillo della Rovina";
                case "Português Brasileiro":
                    return "Signo da Ruína";
                case "Русский":
                    return "Печать рока";
                case "한국어":
                    return "파멸의 인장";
                case "简体中文":
                    return "末日咒符";
                default:
                    return "Sigil of Doom";
            }
        }

    private static string ConsumingFire_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Verzehrendes Feuer";
        case "Español":
            return "Fuego acuciante";
        case "Français":
            return "Feu dévorant";
        case "Italiano":
            return "Fuoco Logorante";
        case "Português Brasileiro":
            return "Fogo Engolfante";
        case "Русский":
            return "Пожирающий огонь";
        case "한국어":
            return "집어삼키는 불길";
        case "简体中文":
            return "吞噬之焰";
        default:
            return "Consuming Fire";
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

            Inferno.PrintMessage("Epic Rotations Vengeance DemonHunter", Color.Purple);
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
                SpellCasts.Add(389813 , SigilOfMisery_SpellName());//389813
                SpellCasts.Add(202138 , SigilOfChains_SpellName());//202138
                SpellCasts.Add(202137 , SigilOfSilence_SpellName());//202137
                SpellCasts.Add(204596 , SigilOfFlame_SpellName());//204596
                SpellCasts.Add(191427 , Metamorphosis_SpellName());//191427
                SpellCasts.Add(187827 , Metamorphosis_SpellName());//187827
                SpellCasts.Add(390163 , SigilofSpite_SpellName());//390163
                SpellCasts.Add(189110 , InfernalStrike_SpellName());//189110

                SpellCasts.Add(183752 , Disrupt_SpellName());//183752
                SpellCasts.Add(196718 , Darkness_SpellName());//196718
                SpellCasts.Add(203720 , DemonSpikes_SpellName());//203720

                SpellCasts.Add(179057 , ChaosNova_SpellName());//179057
                SpellCasts.Add(211881 , FelEruption_SpellName());//211881
                SpellCasts.Add(217832 , Imprison_SpellName());//217832

                SpellCasts.Add(278326 , ConsumeMagic_SpellName());//278326
                SpellCasts.Add(370965 , TheHunt_SpellName());//370965
                SpellCasts.Add(198793 , VengefulRetreat_SpellName());//198793

                SpellCasts.Add(203782 , Shear_SpellName());//203782
                SpellCasts.Add(228477 , SoulCleave_SpellName());//228477
                SpellCasts.Add(185123 , ThrowGlaive_SpellName());//185123
                SpellCasts.Add(204157 , ThrowGlaive_SpellName());//204157
                SpellCasts.Add(185245 , Torment_SpellName());//185245
                SpellCasts.Add(320341 , BulkExtraction_SpellName());//320341
                SpellCasts.Add(258920 , ImmolationAura_SpellName());//258920
                SpellCasts.Add(232893 , Felblade_SpellName());//232893
                SpellCasts.Add(212084 , FelDevastation_SpellName());//212084
                SpellCasts.Add(452486 , FelDevastation_SpellName());//452486 
                SpellCasts.Add(204021 , FieryBrand_SpellName());//204021
                SpellCasts.Add(263642 , Fracture_SpellName());//263642
                SpellCasts.Add(263648 , SoulBarrier_SpellName());//263648
                SpellCasts.Add(207407 , SoulCarver_SpellName());//207407
                SpellCasts.Add(247454 , SpiritBomb_SpellName());//247454
                SpellCasts.Add(452437 , SpiritBurst_SpellName());//452437
                SpellCasts.Add(452436 , SoulSunder_SpellName());//452436
                SpellCasts.Add(442294 , ReaversGlaive_SpellName());//442294
                SpellCasts.Add(452490 , SigilofDoom_SpellName());//452490
                SpellCasts.Add(452487 , ConsumingFire_SpellName());//452487
                SpellCasts.Add(202719 , ArcaneTorrent_SpellName());//202719



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

            //Lists with spells to use with queues
            MouseoverQueues = new List<string>(){
                Disrupt_SpellName(),
                Imprison_SpellName(),
                TheHunt_SpellName(),
                FieryBrand_SpellName(),
                Torment_SpellName(),
                ThrowGlaive_SpellName(),
                

            };
            CursorQueues = new List<string>(){
                SigilofSpite_SpellName(),
                SigilOfFlame_SpellName(),
                SigilOfSilence_SpellName(),
                SigilOfMisery_SpellName(),
                SigilOfChains_SpellName(),
                InfernalStrike_SpellName(),
                SigilofDoom_SpellName()
            };
            PlayerQueues = new List<string>(){
                SigilofSpite_SpellName(),
                SigilOfFlame_SpellName(),
                SigilOfSilence_SpellName(),
                SigilOfMisery_SpellName(),
                SigilOfChains_SpellName(),
                Metamorphosis_SpellName(),
                InfernalStrike_SpellName(),
                Darkness_SpellName(),
                DemonSpikes_SpellName(),
                VengefulRetreat_SpellName(),
                BulkExtraction_SpellName(),
                FelDevastation_SpellName(),
                SigilofDoom_SpellName(),
                ImmolationAura_SpellName(),
                ChaosNova_SpellName(),
                SpiritBomb_SpellName()
            };
            FocusQueues = new List<string>(){
                Torment_SpellName(),
            };
            TargetQueues = new List<string>(){
                Disrupt_SpellName(),
                ChaosNova_SpellName(),
                Imprison_SpellName(),
                TheHunt_SpellName(),
                FieryBrand_SpellName(),
                Torment_SpellName(),
                ChaosNova_SpellName(),
                Felblade_SpellName(),
                Fracture_SpellName(),
                TheHunt_SpellName(),
                TheHunt_SpellName(),
                ThrowGlaive_SpellName(),

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

