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



        ///<summary>spell=100784</summary>
        private static string BlackoutKick_SpellName()
        {
            switch (Language)
            {
                case "English": return "Blackout Kick";
                case "Deutsch": return "Blackout-Tritt";
                case "Español": return "Patada oscura";
                case "Français": return "Frappe du voile noir";
                case "Italiano": return "Calcio dell'Oscuramento";
                case "Português Brasileiro": return "Chute Blecaute";
                case "Русский": return "Нокаутирующий удар";
                case "한국어": return "후려차기";
                case "简体中文": return "幻灭踢";
                default: return "Blackout Kick";
            }
        }

        ///<summary>spell=116768</summary>
        private static string BlackoutKicki_SpellName()
        {
            switch (Language)
            {
                case "English": return "Blackout Kick!";
                case "Deutsch": return "Blackout-Tritt!";
                case "Español": return "¡Patada oscura!";
                case "Français": return "Frappe du voile noir !";
                case "Italiano": return "Calcio dell'Oscuramento!";
                case "Português Brasileiro": return "Chute Blecaute!";
                case "Русский": return "Нокаутирующий удар!";
                case "한국어": return "후려차기!";
                case "简体中文": return "幻灭踢！";
                default: return "Blackout Kick!";
            }
        }

        ///<summary>spell=2094</summary>
        private static string Blind_SpellName()
        {
            switch (Language)
            {
                case "English": return "Blind";
                case "Deutsch": return "Blenden";
                case "Español": return "Ceguera";
                case "Français": return "Cécité";
                case "Italiano": return "Accecamento";
                case "Português Brasileiro": return "Cegar";
                case "Русский": return "Ослепление";
                case "한국어": return "실명";
                case "简体中文": return "致盲";
                default: return "Blind";
            }
        }



        ///<summary>spell=386276</summary>
        private static string BonedustBrew_SpellName()
        {
            switch (Language)
            {
                case "English": return "Bonedust Brew";
                case "Deutsch": return "Knochenstaubgebräu";
                case "Español": return "Brebaje de polvohueso";
                case "Français": return "Breuvage poussière-d’os";
                case "Italiano": return "Birra di Polvere d'Ossa";
                case "Português Brasileiro": return "Cerveja Pó de Osso";
                case "Русский": return "Отвар из костяной пыли";
                case "한국어": return "골분주";
                case "简体中文": return "骨尘酒";
                default: return "Bonedust Brew";
            }
        }



        ///<summary>spell=123986</summary>
        private static string ChiBurst_SpellName()
        {
            switch (Language)
            {
                case "English": return "Chi Burst";
                case "Deutsch": return "Chistoß";
                case "Español": return "Ráfaga de chi";
                case "Français": return "Explosion de chi";
                case "Italiano": return "Scarica del Chi";
                case "Português Brasileiro": return "Estouro de Chi";
                case "Русский": return "Выброс ци";
                case "한국어": return "기의 파동";
                case "简体中文": return "真气爆裂";
                default: return "Chi Burst";
            }
        }

        ///<summary>spell=115008</summary>
        private static string ChiTorpedo_SpellName()
        {
            switch (Language)
            {
                case "English": return "Chi Torpedo";
                case "Deutsch": return "Chitorpedo";
                case "Español": return "Torpedo de chi";
                case "Français": return "Torpille de chi";
                case "Italiano": return "Dardo del Chi";
                case "Português Brasileiro": return "Torpedo de Chi";
                case "Русский": return "Ци-полет";
                case "한국어": return "기공탄";
                case "简体中文": return "真气突";
                default: return "Chi Torpedo";
            }
        }

        ///<summary>spell=115098</summary>
        private static string ChiWave_SpellName()
        {
            switch (Language)
            {
                case "English": return "Chi Wave";
                case "Deutsch": return "Chiwelle";
                case "Español": return "Ola de chi";
                case "Français": return "Onde de chi";
                case "Italiano": return "Ondata del Chi";
                case "Português Brasileiro": return "Onda de Chi";
                case "Русский": return "Волна ци";
                case "한국어": return "기의 물결";
                case "简体中文": return "真气波";
                default: return "Chi Wave";
            }
        }

        ///<summary>spell=117952</summary>
        private static string CracklingJadeLightning_SpellName()
        {
            switch (Language)
            {
                case "English": return "Crackling Jade Lightning";
                case "Deutsch": return "Knisternder Jadeblitz";
                case "Español": return "Relámpago crepitante de jade";
                case "Français": return "Éclair de jade crépitant";
                case "Italiano": return "Fulmine di Giada Crepitante";
                case "Português Brasileiro": return "Raio Jade Crepitante";
                case "Русский": return "Сверкающая нефритовая молния";
                case "한국어": return "짜릿한 비취 번개";
                case "简体中文": return "碎玉闪电";
                default: return "Crackling Jade Lightning";
            }
        }

        ///<summary>spell=122278</summary>
        private static string DampenHarm_SpellName()
        {
            switch (Language)
            {
                case "English": return "Dampen Harm";
                case "Deutsch": return "Schaden dämpfen";
                case "Español": return "Mitigar daño";
                case "Français": return "Atténuation du mal";
                case "Italiano": return "Diminuzione del Dolore";
                case "Português Brasileiro": return "Atenuar Ferimento";
                case "Русский": return "Смягчение удара";
                case "한국어": return "해악 감퇴";
                case "简体中文": return "躯不坏";
                default: return "Dampen Harm";
            }
        }

        ///<summary>spell=325201</summary>
        private static string DanceOfChiji_SpellName()
        {
            switch (Language)
            {
                case "English": return "Dance of Chi-Ji";
                case "Deutsch": return "Chi-Jis Tanz";
                case "Español": return "Danza de Chi-Ji";
                case "Français": return "Danse de Chi Ji";
                case "Italiano": return "Danza di Chi-Ji";
                case "Português Brasileiro": return "Dança de Chi-Ji";
                case "Русский": return "Танец Чи-Цзи";
                case "한국어": return "츠지의 춤";
                case "简体中文": return "赤精之舞";
                default: return "Dance of Chi-Ji";
            }
        }

        ///<summary>spell=218164</summary>
        private static string Detox_SpellName()
        {
            switch (Language)
            {
                case "English": return "Detox";
                case "Deutsch": return "Entgiftung";
                case "Español": return "Depuración";
                case "Français": return "Détoxification";
                case "Italiano": return "Disintossicazione";
                case "Português Brasileiro": return "Desintoxicação";
                case "Русский": return "Детоксикация";
                case "한국어": return "해독";
                case "简体中文": return "清创生血";
                default: return "Detox";
            }
        }

        ///<summary>spell=122783</summary>
        private static string DiffuseMagic_SpellName()
        {
            switch (Language)
            {
                case "English": return "Diffuse Magic";
                case "Deutsch": return "Magiediffusion";
                case "Español": return "Difuminar magia";
                case "Français": return "Diffusion de la magie";
                case "Italiano": return "Dispersione della Magia";
                case "Português Brasileiro": return "Magia Difusa";
                case "Русский": return "Распыление магии";
                case "한국어": return "마법 해소";
                case "简体中文": return "散魔功";
                default: return "Diffuse Magic";
            }
        }

        ///<summary>spell=116095</summary>
        private static string Disable_SpellName()
        {
            switch (Language)
            {
                case "English": return "Disable";
                case "Deutsch": return "Lähmen";
                case "Español": return "Incapacitar";
                case "Français": return "Handicap";
                case "Italiano": return "Inabilitazione";
                case "Português Brasileiro": return "Desativar";
                case "Русский": return "Вывести из строя";
                case "한국어": return "결박";
                case "简体中文": return "金刚震";
                default: return "Disable";
            }
        }



        ///<summary>spell=322101</summary>
        private static string ExpelHarm_SpellName()
        {
            switch (Language)
            {
                case "English": return "Expel Harm";
                case "Deutsch": return "Schadensumleitung";
                case "Español": return "Expulsar daño";
                case "Français": return "Extraction du mal";
                case "Italiano": return "Espulsione del Dolore";
                case "Português Brasileiro": return "Expelir o Mal";
                case "Русский": return "Устранение вреда";
                case "한국어": return "해악 축출";
                case "简体中文": return "移花接木";
                default: return "Expel Harm";
            }
        }

        ///<summary>spell=326860</summary>
        private static string FallenOrder_SpellName()
        {
            switch (Language)
            {
                case "English": return "Fallen Order";
                case "Deutsch": return "Gefallener Orden";
                case "Español": return "Orden caída";
                case "Français": return "Ordre des défunts";
                case "Italiano": return "Ordine Caduto";
                case "Português Brasileiro": return "Ordem Decaída";
                case "Русский": return "Павший орден";
                case "한국어": return "망자의 연맹";
                case "简体中文": return "陨落僧众";
                default: return "Fallen Order";
            }
        }



        ///<summary>spell=113656</summary>
        private static string FistsOfFury_SpellName()
        {
            switch (Language)
            {
                case "English": return "Fists of Fury";
                case "Deutsch": return "Furorfäuste";
                case "Español": return "Puños de furia";
                case "Français": return "Poings de fureur";
                case "Italiano": return "Pugni della Furia";
                case "Português Brasileiro": return "Punhos da Fúria";
                case "Русский": return "Неистовые кулаки";
                case "한국어": return "분노의 주먹";
                case "简体中文": return "怒雷破";
                default: return "Fists of Fury";
            }
        }

        ///<summary>spell=101545</summary>
        private static string FlyingSerpentKick_SpellName()
        {
            switch (Language)
            {
                case "English": return "Flying Serpent Kick";
                case "Deutsch": return "Fliegender Schlangentritt";
                case "Español": return "Patada del dragón volador";
                case "Français": return "Coup du serpent volant";
                case "Italiano": return "Calcio Volante della Serpe";
                case "Português Brasileiro": return "Chute Voador da Serpente";
                case "Русский": return "Удар летящего змея";
                case "한국어": return "비룡차기";
                case "简体中文": return "翔龙在天";
                default: return "Flying Serpent Kick";
            }
        }

        ///<summary>spell=388917</summary>
        private static string FortifyingBrew_SpellName()
        {
            switch (Language)
            {
                case "English": return "Fortifying Brew";
                case "Deutsch": return "Stärkendes Gebräu";
                case "Español": return "Brebaje reconstituyente";
                case "Français": return "Boisson fortifiante";
                case "Italiano": return "Birra Fortificante";
                case "Português Brasileiro": return "Cerveja Fortificante";
                case "Русский": return "Укрепляющий отвар";
                case "한국어": return "강화주";
                case "简体中文": return "壮胆酒";
                default: return "Fortifying Brew";
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

        ///<summary>spell=123904</summary>
        private static string InvokeXuenTheWhiteTiger_SpellName()
        {
            switch (Language)
            {
                case "English": return "Invoke Xuen, the White Tiger";
                case "Deutsch": return "Xuen den Weißen Tiger beschwören";
                case "Español": return "Invocar a Xuen, el Tigre Blanco";
                case "Français": return "Invocation de Xuen, le Tigre blanc";
                case "Italiano": return "Invocazione: Xuen, la Tigre Bianca";
                case "Português Brasileiro": return "Evocar Xuen, o Tigre Branco";
                case "Русский": return "Призыв Сюэня, Белого Тигра";
                case "한국어": return "백호 쉬엔의 원령";
                case "简体中文": return "白虎下凡";
                default: return "Invoke Xuen, the White Tiger";
            }
        }

        ///<summary>spell=388197</summary>
        private static string JadefireStomp_SpellName()
        {
            switch (Language)
            {
                case "English": return "Jadefire Stomp";
                case "Deutsch": return "Jadefeuerstampfen";
                case "Español": return "Pisotón de fuego de jade";
                case "Français": return "Piétinement de jadefeu";
                case "Italiano": return "Urto di Giadafulgida";
                case "Português Brasileiro": return "Pisão Flamejade";
                case "Русский": return "Нефритовый топот";
                case "한국어": return "비취불꽃 진각";
                case "简体中文": return "碧火踏";
                default: return "Jadefire Stomp";
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

        ///<summary>spell=1766</summary>
        private static string Kick_SpellName()
        {
            switch (Language)
            {
                case "English": return "Kick";
                case "Deutsch": return "Tritt";
                case "Español": return "Patada";
                case "Français": return "Coup de pied";
                case "Italiano": return "Calcio";
                case "Português Brasileiro": return "Chute";
                case "Русский": return "Пинок";
                case "한국어": return "발차기";
                case "简体中文": return "脚踢";
                default: return "Kick";
            }
        }

        ///<summary>spell=119381</summary>
        private static string LegSweep_SpellName()
        {
            switch (Language)
            {
                case "English": return "Leg Sweep";
                case "Deutsch": return "Fußfeger";
                case "Español": return "Barrido de pierna";
                case "Français": return "Balayement de jambe";
                case "Italiano": return "Calcio a Spazzata";
                case "Português Brasileiro": return "Rasteira";
                case "Русский": return "Круговой удар ногой";
                case "한국어": return "팽이 차기";
                case "简体中文": return "扫堂腿";
                default: return "Leg Sweep";
            }
        }



        ///<summary>spell=115078</summary>
        private static string Paralysis_SpellName()
        {
            switch (Language)
            {
                case "English": return "Paralysis";
                case "Deutsch": return "Paralyse";
                case "Español": return "Parálisis";
                case "Français": return "Paralysie";
                case "Italiano": return "Paralisi";
                case "Português Brasileiro": return "Paralisia";
                case "Русский": return "Паралич";
                case "한국어": return "마비";
                case "简体中文": return "分筋错骨";
                default: return "Paralysis";
            }
        }

        ///<summary>spell=116844</summary>
        private static string RingOfPeace_SpellName()
        {
            switch (Language)
            {
                case "English": return "Ring of Peace";
                case "Deutsch": return "Ring des Friedens";
                case "Español": return "Anillo de paz";
                case "Français": return "Anneau de paix";
                case "Italiano": return "Circolo di Pace";
                case "Português Brasileiro": return "Anel da Paz";
                case "Русский": return "Круг мира";
                case "한국어": return "평화의 고리";
                case "简体中文": return "平心之环";
                default: return "Ring of Peace";
            }
        }

        ///<summary>spell=107428</summary>
        private static string RisingSunKick_SpellName()
        {
            switch (Language)
            {
                case "English": return "Rising Sun Kick";
                case "Deutsch": return "Tritt der aufgehenden Sonne";
                case "Español": return "Patada del sol naciente";
                case "Français": return "Coup de pied du soleil levant";
                case "Italiano": return "Calcio del Sole Nascente";
                case "Português Brasileiro": return "Chute do Sol Nascente";
                case "Русский": return "Удар восходящего солнца";
                case "한국어": return "해오름차기";
                case "简体中文": return "旭日东升踢";
                default: return "Rising Sun Kick";
            }
        }


        ///<summary>spell=116847</summary>
        private static string RushingJadeWind_SpellName()
        {
            switch (Language)
            {
                case "English": return "Rushing Jade Wind";
                case "Deutsch": return "Rauschender Jadewind";
                case "Español": return "Viento de jade impetuoso";
                case "Français": return "Vent de jade fulgurant";
                case "Italiano": return "Tornado di Giada";
                case "Português Brasileiro": return "Vento Impetuoso de Jade";
                case "Русский": return "Порыв нефритового ветра";
                case "한국어": return "비취 돌풍";
                case "简体中文": return "碧玉疾风";
                default: return "Rushing Jade Wind";
            }
        }

        ///<summary>spell=6770</summary>
        private static string Sap_SpellName()
        {
            switch (Language)
            {
                case "English": return "Sap";
                case "Deutsch": return "Kopfnuss";
                case "Español": return "Porrazo";
                case "Français": return "Assommer";
                case "Italiano": return "Tramortimento";
                case "Português Brasileiro": return "Aturdir";
                case "Русский": return "Ошеломление";
                case "한국어": return "혼절시키기";
                case "简体中文": return "闷棍";
                default: return "Sap";
            }
        }

        ///<summary>spell=152173</summary>
        private static string Serenity_SpellName()
        {
            switch (Language)
            {
                case "English": return "Serenity";
                case "Deutsch": return "Gleichmut";
                case "Español": return "Serenidad";
                case "Français": return "Sérénité";
                case "Italiano": return "Serenità";
                case "Português Brasileiro": return "Serenidade";
                case "Русский": return "Безмятежность";
                case "한국어": return "평안";
                case "简体中文": return "屏气凝神";
                default: return "Serenity";
            }
        }



        ///<summary>spell=116705</summary>
        private static string SpearHandStrike_SpellName()
        {
            switch (Language)
            {
                case "English": return "Spear Hand Strike";
                case "Deutsch": return "Speerhandstoß";
                case "Español": return "Golpe de mano de lanza";
                case "Français": return "Pique de main";
                case "Italiano": return "Compressione Tracheale";
                case "Português Brasileiro": return "Golpe Mão de Lança";
                case "Русский": return "Рука-копье";
                case "한국어": return "손날 찌르기";
                case "简体中文": return "切喉手";
                default: return "Spear Hand Strike";
            }
        }

        ///<summary>spell=101546</summary>
        private static string SpinningCraneKick_SpellName()
        {
            switch (Language)
            {
                case "English": return "Spinning Crane Kick";
                case "Deutsch": return "Wirbelnder Kranichtritt";
                case "Español": return "Patada giratoria de la grulla";
                case "Français": return "Coup tournoyant de la grue";
                case "Italiano": return "Calcio Rotante della Gru";
                case "Português Brasileiro": return "Chute Giratório da Garça";
                case "Русский": return "Танцующий журавль";
                case "한국어": return "회전 학다리차기";
                case "简体中文": return "神鹤引项踢";
                default: return "Spinning Crane Kick";
            }
        }



        ///<summary>spell=137639</summary>
        private static string StormEarthAndFire_SpellName()
        {
            switch (Language)
            {
                case "English": return "Storm, Earth, and Fire";
                case "Deutsch": return "Sturm, Erde und Feuer";
                case "Español": return "Tormenta, Tierra y Fuego";
                case "Français": return "Tempête, Terre et Feu";
                case "Italiano": return "Tempesta, Terra e Fuoco";
                case "Português Brasileiro": return "Tempestade, Terra e Fogo";
                case "Русский": return "Буря, земля и огонь";
                case "한국어": return "폭풍과 대지와 불";
                case "简体中文": return "风火雷电";
                default: return "Storm, Earth, and Fire";
            }
        }

        ///<summary>spell=221771</summary>
        private static string StormEarthAndFire_Fixate_SpellName()
        {
            switch (Language)
            {
                case "English": return "Storm, Earth, and Fire: Fixate";
                case "Deutsch": return "Sturm, Erde und Feuer: Fixieren";
                case "Español": return "Tormenta, Tierra y Fuego: Fijar";
                case "Français": return "Fixation de Tempête, Terre et Feu";
                case "Italiano": return "Tempesta, Terra e Fuoco: Ossessione";
                case "Português Brasileiro": return "Tempestade, Terra e Fogo: Fixar";
                case "Русский": return "Буря, земля и огонь: сосредоточение внимания";
                case "한국어": return "폭풍과 대지와 불: 시선 고정";
                case "简体中文": return "风火雷电：锁定";
                default: return "Storm, Earth, and Fire: Fixate";
            }
        }

        ///<summary>spell=392983</summary>
        private static string StrikeOfTheWindlord_SpellName()
        {
            switch (Language)
            {
                case "English": return "Strike of the Windlord";
                case "Deutsch": return "Schlag des Windlords";
                case "Español": return "Golpe del Señor del Viento";
                case "Français": return "Frappe du seigneur des Vents";
                case "Italiano": return "Assalto del Signore del Vento";
                case "Português Brasileiro": return "Golpe do Senhor dos Ventos";
                case "Русский": return "Удар Владыки Ветра";
                case "한국어": return "바람의 군주의 일격";
                case "简体中文": return "风领主之击";
                default: return "Strike of the Windlord";
            }
        }

        ///<summary>spell=388686</summary>
        private static string SummonWhiteTigerStatue_SpellName()
        {
            switch (Language)
            {
                case "English": return "Summon White Tiger Statue";
                case "Deutsch": return "Weiße Tigerstatue beschwören";
                case "Español": return "Invocar estatua del Tigre Blanco";
                case "Français": return "Invocation de statue de tigre blanc";
                case "Italiano": return "Evocazione: Statua della Tigre Bianca";
                case "Português Brasileiro": return "Evocar Estátua do Tigre Branco";
                case "Русский": return "Призыв статуи белого тигра";
                case "한국어": return "백호 조각상 소환";
                case "简体中文": return "召唤白虎雕像";
                default: return "Summon White Tiger Statue";
            }
        }

        ///<summary>spell=100780</summary>
        private static string TigerPalm_SpellName()
        {
            switch (Language)
            {
                case "English": return "Tiger Palm";
                case "Deutsch": return "Tigerklaue";
                case "Español": return "Palma del tigre";
                case "Français": return "Paume du tigre";
                case "Italiano": return "Palmo della Tigre";
                case "Português Brasileiro": return "Palma do Tigre";
                case "Русский": return "Лапа тигра";
                case "한국어": return "범의 장풍";
                case "简体中文": return "猛虎掌";
                default: return "Tiger Palm";
            }
        }

        ///<summary>spell=116841</summary>
        private static string TigersLust_SpellName()
        {
            switch (Language)
            {
                case "English": return "Tiger's Lust";
                case "Deutsch": return "Tigerrausch";
                case "Español": return "Deseo del tigre";
                case "Français": return "Soif du tigre";
                case "Italiano": return "Brama della Tigre";
                case "Português Brasileiro": return "Luxúria do Tigre";
                case "Русский": return "Тигриное рвение";
                case "한국어": return "범의 욕망";
                case "简体中文": return "迅如猛虎";
                default: return "Tiger's Lust";
            }
        }

        ///<summary>spell=322109</summary>
        private static string TouchOfDeath_SpellName()
        {
            switch (Language)
            {
                case "English": return "Touch of Death";
                case "Deutsch": return "Berührung des Todes";
                case "Español": return "Toque de la muerte";
                case "Français": return "Toucher mortel";
                case "Italiano": return "Tocco della Morte";
                case "Português Brasileiro": return "Toque da Morte";
                case "Русский": return "Смертельное касание";
                case "한국어": return "절명의 손길";
                case "简体中文": return "轮回之触";
                default: return "Touch of Death";
            }
        }

        ///<summary>spell=122470</summary>
        private static string TouchOfKarma_SpellName()
        {
            switch (Language)
            {
                case "English": return "Touch of Karma";
                case "Deutsch": return "Karmaberührung";
                case "Español": return "Toque de karma";
                case "Français": return "Toucher du karma";
                case "Italiano": return "Tocco del Karma";
                case "Português Brasileiro": return "Toque do Karma";
                case "Русский": return "Закон кармы";
                case "한국어": return "업보의 손아귀";
                case "简体中文": return "业报之触";
                default: return "Touch of Karma";
            }
        }

        ///<summary>spell=101643</summary>
        private static string Transcendence_SpellName()
        {
            switch (Language)
            {
                case "English": return "Transcendence";
                case "Deutsch": return "Transzendenz";
                case "Español": return "Transcendencia";
                case "Français": return "Transcendance";
                case "Italiano": return "Trascendenza";
                case "Português Brasileiro": return "Transcendência";
                case "Русский": return "Трансцендентность";
                case "한국어": return "해탈";
                case "简体中文": return "魂体双分";
                default: return "Transcendence";
            }
        }

        ///<summary>spell=119996</summary>
        private static string Transcendence_Transfer_SpellName()
        {
            switch (Language)
            {
                case "English": return "Transcendence: Transfer";
                case "Deutsch": return "Transzendenz: Transfer";
                case "Español": return "Transcendencia: Transferencia";
                case "Français": return "Transcendance : Transfert";
                case "Italiano": return "Trascendenza: Trasferimento";
                case "Português Brasileiro": return "Transcendência: Transferência";
                case "Русский": return "Трансцендентность: перенос";
                case "한국어": return "해탈: 전환";
                case "简体中文": return "魂体双分：转移";
                default: return "Transcendence: Transfer";
            }
        }

        ///<summary>spell=116670</summary>
        private static string Vivify_SpellName()
        {
            switch (Language)
            {
                case "English": return "Vivify";
                case "Deutsch": return "Beleben";
                case "Español": return "Vivificar";
                case "Français": return "Vivifier";
                case "Italiano": return "Vivificazione";
                case "Português Brasileiro": return "Vivificar";
                case "Русский": return "Оживить";
                case "한국어": return "생기 충전";
                case "简体中文": return "活血术";
                default: return "Vivify";
            }
        }



        ///<summary>spell=310454</summary>
        private static string WeaponsOfOrder_SpellName()
        {
            switch (Language)
            {
                case "English": return "Weapons of Order";
                case "Deutsch": return "Waffen der Ordnung";
                case "Español": return "Armas de orden";
                case "Français": return "Armes de l’Ordre";
                case "Italiano": return "Armi dell'Ordine";
                case "Português Brasileiro": return "Armas de Ordem";
                case "Русский": return "Оружие ордена";
                case "한국어": return "질서의 무기";
                case "简体中文": return "精序兵戈";
                default: return "Weapons of Order";
            }
        }

        ///<summary>spell=152175</summary>
        private static string WhirlingDragonPunch_SpellName()
        {
            switch (Language)
            {
                case "English": return "Whirling Dragon Punch";
                case "Deutsch": return "Wirbelnder Drachenschlag";
                case "Español": return "Puñetazo giratorio del dragón";
                case "Français": return "Coup de poing du dragon tourbillonnant";
                case "Italiano": return "Pugno Rotante del Drago";
                case "Português Brasileiro": return "Soco Giratório do Dragão";
                case "Русский": return "Удар крутящегося дракона";
                case "한국어": return "소용돌이 용의 주먹";
                case "简体中文": return "升龙霸";
                default: return "Whirling Dragon Punch";
            }
        }




        
private static string CelestialConduit_SpellName()//443028
{
    switch (Language)
    {
        case "Deutsch":
            return "Erhabene Leitung";
        case "Español":
            return "Conducto celestial";
        case "Français":
            return "Canalisation astrale";
        case "Italiano":
            return "Condotto Celestiale";
        case "Português Brasileiro":
            return "Conduíte Celestial";
        case "Русский":
            return "Небесный проводник";
        case "한국어":
            return "천신합일";
        case "简体中文":
            return "天神御身";
        default:
            return "Celestial Conduit";
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
private static string SlicingWinds_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Schneidende Winde";
        case "Español":
            return "Vientos cortantes";
        case "Français":
            return "Vents tranchants";
        case "Italiano":
            return "Venti Taglienti";
        case "Português Brasileiro":
            return "Ventos Cortantes";
        case "Русский":
            return "Пронизывающие ветра";
        case "한국어":
            return "날카로운 바람";
        case "简体中文":
            return "切削之风";
        default:
            return "Slicing Winds";
    }
}
private static string VivaciousVivification_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Lebhafte Belebung";
        case "Español":
            return "Vivificación vivaz";
        case "Français":
            return "Vivification vivace";
        case "Italiano":
            return "Vivificazione Vivace";
        case "Português Brasileiro":
            return "Vivificação Vivaz";
        case "Русский":
            return "Живенькое оживление";
        case "한국어":
            return "쾌활한 생기화";
        case "简体中文":
            return "活力苏醒";
        default:
            return "Vivacious Vivification";
    }
}
private static string TheEmperorsCapacitor_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Der kaiserliche Kondensator";
        case "Español":
            return "Condensador del Emperador";
        case "Français":
            return "Condensateur de l’empereur";
        case "Italiano":
            return "Condensatore dell'Imperatore";
        case "Português Brasileiro":
            return "Capacitor do Imperador";
        case "Русский":
            return "Императорский конденсатор";
        case "한국어":
            return "황제의 축전기";
        case "简体中文":
            return "皇帝的容电器";
        default:
            return "The Emperor's Capacitor";
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

            Inferno.PrintMessage("Epic Rotations  Windwalker Monk", Color.Purple);
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
                                //Covenants
                SpellCasts.Add(387184 , WeaponsOfOrder_SpellName());//387184
                SpellCasts.Add(386276 , BonedustBrew_SpellName());//386276
                SpellCasts.Add(100784 , BlackoutKick_SpellName());//100784
                SpellCasts.Add(326860, FallenOrder_SpellName()); //326860
                SpellCasts.Add(388193, JadefireStomp_SpellName()); //388193
                SpellCasts.Add(116705, SpearHandStrike_SpellName()); //116705
                SpellCasts.Add(123986, ChiBurst_SpellName()); //123986
                SpellCasts.Add(115008, ChiTorpedo_SpellName()); //115008
                SpellCasts.Add(115098, ChiWave_SpellName()); //115098
                SpellCasts.Add(117952, CracklingJadeLightning_SpellName()); //117952
                SpellCasts.Add(122278, DampenHarm_SpellName()); //122278
                SpellCasts.Add(218164, Detox_SpellName()); //218164
                SpellCasts.Add(122783, DiffuseMagic_SpellName()); //122783
                SpellCasts.Add(322101, ExpelHarm_SpellName()); //322101
                SpellCasts.Add(113656, FistsOfFury_SpellName()); //113656
                SpellCasts.Add(101545, FlyingSerpentKick_SpellName()); //101545
                SpellCasts.Add(388917, FortifyingBrew_SpellName()); //388917
                SpellCasts.Add(123904, InvokeXuenTheWhiteTiger_SpellName()); //123904
                SpellCasts.Add(119381, LegSweep_SpellName()); //119381
                SpellCasts.Add(115078, Paralysis_SpellName()); //115078
                SpellCasts.Add(116844, RingOfPeace_SpellName()); //116844
                SpellCasts.Add(107428, RisingSunKick_SpellName()); //107428
                SpellCasts.Add(116847, RushingJadeWind_SpellName()); //116847
                SpellCasts.Add(152173, Serenity_SpellName()); //152173
                SpellCasts.Add(101546, SpinningCraneKick_SpellName()); //101546
                SpellCasts.Add(221771, StormEarthAndFire_Fixate_SpellName()); //221771
                SpellCasts.Add(137639, StormEarthAndFire_SpellName()); //137639
                SpellCasts.Add(392983, StrikeOfTheWindlord_SpellName()); //392983
                SpellCasts.Add(388686, SummonWhiteTigerStatue_SpellName()); //388686
                SpellCasts.Add(100780, TigerPalm_SpellName()); //100780
                SpellCasts.Add(116841, TigersLust_SpellName()); //116841
                SpellCasts.Add(322109, TouchOfDeath_SpellName()); //322109
                SpellCasts.Add(122470, TouchOfKarma_SpellName()); //122470
                SpellCasts.Add(119996, Transcendence_Transfer_SpellName()); //119996
                SpellCasts.Add(101643, Transcendence_SpellName()); //101643
                SpellCasts.Add(116670, Vivify_SpellName()); //116670
                SpellCasts.Add(152175, WhirlingDragonPunch_SpellName()); //152175
                SpellCasts.Add(443028, CelestialConduit_SpellName()); //443028
                SpellCasts.Add(461404, ChiBurst_SpellName()); //461404
                SpellCasts.Add(1217413, SlicingWinds_SpellName()); //1217413


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
                SpearHandStrike_SpellName(),
                Paralysis_SpellName(),
                TouchOfKarma_SpellName(),
                TouchOfDeath_SpellName(),
                Detox_SpellName(),
                CracklingJadeLightning_SpellName()

            };
            CursorQueues = new List<string>(){
                BonedustBrew_SpellName(),
                SummonWhiteTigerStatue_SpellName(),
                RingOfPeace_SpellName(),
                
            };
            PlayerQueues = new List<string>(){
                BonedustBrew_SpellName(),
                SummonWhiteTigerStatue_SpellName(),
                RingOfPeace_SpellName(),
                Detox_SpellName(),
                DampenHarm_SpellName(),
                DiffuseMagic_SpellName(),
                FortifyingBrew_SpellName(),
                LegSweep_SpellName(),
                ExpelHarm_SpellName(),
                WeaponsOfOrder_SpellName(),
                TigersLust_SpellName(),
                Vivify_SpellName(),
                SlicingWinds_SpellName()


       
            };
            FocusQueues = new List<string>(){
                SpearHandStrike_SpellName(),
                Detox_SpellName()

            };
            TargetQueues = new List<string>(){
                SpearHandStrike_SpellName(),
                Detox_SpellName(),
                Paralysis_SpellName(),
                TouchOfKarma_SpellName(),
                TouchOfDeath_SpellName(),
                InvokeXuenTheWhiteTiger_SpellName(),
                StormEarthAndFire_SpellName(),
                CracklingJadeLightning_SpellName()
                
         
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

