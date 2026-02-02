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

        ///<summary>spell=316958</summary>
        private static string AshenHallow_SpellName()
        {
            switch (Language)
            {
                case "English": return "Ashen Hallow";
                case "Deutsch": return "Ascheweihung";
                case "Español": return "Santificación cinérea";
                case "Français": return "Bénédiction cendrée";
                case "Italiano": return "Santificazione Cinerea";
                case "Português Brasileiro": return "Consagração Cinzenta";
                case "Русский": return "Пепельное освящение";
                case "한국어": return "잿빛 신성화";
                case "简体中文": return "红烬圣土";
                default: return "Ashen Hallow";
            }
        }

        ///<summary>spell=384376 31884</summary>
        private static string AvengingWrath_SpellName()
        {
            switch (Language)
            {
                case "English": return "Avenging Wrath";
                case "Deutsch": return "Zornige Vergeltung";
                case "Español": return "Cólera vengativa";
                case "Français": return "Courroux vengeur";
                case "Italiano": return "Ira Vendicatrice";
                case "Português Brasileiro": return "Ira Vingativa";
                case "Русский": return "Гнев карателя";
                case "한국어": return "응징의 격노";
                case "简体中文": return "复仇之怒";
                default: return "Avenging Wrath";
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

        ///<summary>spell=335305</summary>
        private static string BarbedShackles_SpellName()
        {
            switch (Language)
            {
                case "English": return "Barbed Shackles";
                case "Deutsch": return "Gezackte Fesseln";
                case "Español": return "Grilletes con púas";
                case "Français": return "Entraves barbelées";
                case "Italiano": return "Manette con Barbigli";
                case "Português Brasileiro": return "Grilhões Serrilhados";
                case "Русский": return "Выщербленные кандалы";
                case "한국어": return "날카로운 족쇄";
                case "简体中文": return "尖刺镣铐";
                default: return "Barbed Shackles";
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

        ///<summary>spell=358774</summary>
        private static string BindingsOfMisery_SpellName()
        {
            switch (Language)
            {
                case "English": return "Bindings of Misery";
                case "Deutsch": return "Bindungen des Elends";
                case "Español": return "Ataduras de miseria";
                case "Français": return "Liens de malheur";
                case "Italiano": return "Vincoli della Miseria";
                case "Português Brasileiro": return "Amarras do Sofrimento";
                case "Русский": return "Оковы скорби";
                case "한국어": return "괴로운 구속";
                case "简体中文": return "痛苦之链";
                default: return "Bindings of Misery";
            }
        }

        ///<summary>spell=184575</summary>
        private static string BladeOfJustice_SpellName()
        {
            switch (Language)
            {
                case "English": return "Blade of Justice";
                case "Deutsch": return "Klinge der Gerechtigkeit";
                case "Español": return "Hoja de justicia";
                case "Français": return "Lame de justice";
                case "Italiano": return "Lama della Giustizia";
                case "Português Brasileiro": return "Lâmina da Justiça";
                case "Русский": return "Клинок справедливости";
                case "한국어": return "심판의 칼날";
                case "简体中文": return "公正之剑";
                default: return "Blade of Justice";
            }
        }

        ///<summary>spell=328622</summary>
        private static string BlessingOfAutumn_SpellName()
        {
            switch (Language)
            {
                case "English": return "Blessing of Autumn";
                case "Deutsch": return "Segen des Herbstes";
                case "Español": return "Bendición de otoño";
                case "Français": return "Bénédiction de l’automne";
                case "Italiano": return "Benedizione dell'Autunno";
                case "Português Brasileiro": return "Bênção do Outono";
                case "Русский": return "Благословение осени";
                case "한국어": return "가을의 축복";
                case "简体中文": return "暮秋祝福";
                default: return "Blessing of Autumn";
            }
        }

        ///<summary>spell=1044</summary>
        private static string BlessingOfFreedom_SpellName()
        {
            switch (Language)
            {
                case "English": return "Blessing of Freedom";
                case "Deutsch": return "Segen der Freiheit";
                case "Español": return "Bendición de libertad";
                case "Français": return "Bénédiction de liberté";
                case "Italiano": return "Benedizione della Libertà";
                case "Português Brasileiro": return "Bênção da Liberdade";
                case "Русский": return "Благословенная свобода";
                case "한국어": return "자유의 축복";
                case "简体中文": return "自由祝福";
                default: return "Blessing of Freedom";
            }
        }

        ///<summary>spell=1022</summary>
        private static string BlessingOfProtection_SpellName()
        {
            switch (Language)
            {
                case "English": return "Blessing of Protection";
                case "Deutsch": return "Segen des Schutzes";
                case "Español": return "Bendición de protección";
                case "Français": return "Bénédiction de protection";
                case "Italiano": return "Benedizione della Protezione";
                case "Português Brasileiro": return "Bênção de Proteção";
                case "Русский": return "Благословение защиты";
                case "한국어": return "보호의 축복";
                case "简体中文": return "保护祝福";
                default: return "Blessing of Protection";
            }
        }

        ///<summary>spell=6940</summary>
        private static string BlessingOfSacrifice_SpellName()
        {
            switch (Language)
            {
                case "English": return "Blessing of Sacrifice";
                case "Deutsch": return "Segen der Aufopferung";
                case "Español": return "Bendición de sacrificio";
                case "Français": return "Bénédiction de sacrifice";
                case "Italiano": return "Benedizione del Sacrificio";
                case "Português Brasileiro": return "Bênção do Sacrifício";
                case "Русский": return "Жертвенное благословение";
                case "한국어": return "희생의 축복";
                case "简体中文": return "牺牲祝福";
                default: return "Blessing of Sacrifice";
            }
        }

        ///<summary>spell=328282</summary>
        private static string BlessingOfSpring_SpellName()
        {
            switch (Language)
            {
                case "English": return "Blessing of Spring";
                case "Deutsch": return "Segen des Frühlings";
                case "Español": return "Bendición de primavera";
                case "Français": return "Bénédiction du printemps";
                case "Italiano": return "Benedizione della Primavera";
                case "Português Brasileiro": return "Bênção da Primavera";
                case "Русский": return "Благословение весны";
                case "한국어": return "봄의 축복";
                case "简体中文": return "阳春祝福";
                default: return "Blessing of Spring";
            }
        }

        ///<summary>spell=328620</summary>
        private static string BlessingOfSummer_SpellName()
        {
            switch (Language)
            {
                case "English": return "Blessing of Summer";
                case "Deutsch": return "Segen des Sommers";
                case "Español": return "Bendición de verano";
                case "Français": return "Bénédiction de l’été";
                case "Italiano": return "Benedizione dell'Estate";
                case "Português Brasileiro": return "Bênção do Verão";
                case "Русский": return "Благословение лета";
                case "한국어": return "여름의 축복";
                case "简体中文": return "仲夏祝福";
                default: return "Blessing of Summer";
            }
        }

        ///<summary>spell=328281</summary>
        private static string BlessingOfWinter_SpellName()
        {
            switch (Language)
            {
                case "English": return "Blessing of Winter";
                case "Deutsch": return "Segen des Winters";
                case "Español": return "Bendición de invierno";
                case "Français": return "Bénédiction de l’hiver";
                case "Italiano": return "Benedizione dell'Inverno";
                case "Português Brasileiro": return "Bênção do Inverno";
                case "Русский": return "Благословение зимы";
                case "한국어": return "겨울의 축복";
                case "简体中文": return "凛冬祝福";
                default: return "Blessing of Winter";
            }
        }

        ///<summary>spell=115750</summary>
        private static string BlindingLight_SpellName()
        {
            switch (Language)
            {
                case "English": return "Blinding Light";
                case "Deutsch": return "Blendendes Licht";
                case "Español": return "Luz cegadora";
                case "Français": return "Lumière aveuglante";
                case "Italiano": return "Luce Accecante";
                case "Português Brasileiro": return "Luz Ofuscante";
                case "Русский": return "Слепящий свет";
                case "한국어": return "눈부신 빛";
                case "简体中文": return "盲目之光";
                default: return "Blinding Light";
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

        ///<summary>spell=213644</summary>
        private static string CleanseToxins_SpellName()
        {
            switch (Language)
            {
                case "English": return "Cleanse Toxins";
                case "Deutsch": return "Toxine läutern";
                case "Español": return "Limpiar toxinas";
                case "Français": return "Purification des toxines";
                case "Italiano": return "Purificazione dalle Tossine";
                case "Português Brasileiro": return "Purificar Toxinas";
                case "Русский": return "Очищение от токсинов";
                case "한국어": return "독소 정화";
                case "简体中文": return "清毒术";
                default: return "Cleanse Toxins";
            }
        }

        ///<summary>spell=26573</summary>
        private static string Consecration_SpellName()
        {
            switch (Language)
            {
                case "English": return "Consecration";
                case "Deutsch": return "Weihe";
                case "Español": return "Consagración";
                case "Français": return "Consécration";
                case "Italiano": return "Consacrazione";
                case "Português Brasileiro": return "Consagração";
                case "Русский": return "Освящение";
                case "한국어": return "신성화";
                case "简体中文": return "奉献";
                default: return "Consecration";
            }
        }

        ///<summary>spell=231895</summary>
        private static string Crusade_SpellName()
        {
            switch (Language)
            {
                case "English": return "Crusade";
                case "Deutsch": return "Kreuzzug";
                case "Español": return "Cruzada";
                case "Français": return "Croisade";
                case "Italiano": return "Crociata";
                case "Português Brasileiro": return "Cruzada";
                case "Русский": return "Священная война";
                case "한국어": return "성전";
                case "简体中文": return "征伐";
                default: return "Crusade";
            }
        }

        ///<summary>spell=35395</summary>
        private static string CrusaderStrike_SpellName()
        {
            switch (Language)
            {
                case "English": return "Crusader Strike";
                case "Deutsch": return "Kreuzfahrerstoß";
                case "Español": return "Golpe de cruzado";
                case "Français": return "Frappe du croisé";
                case "Italiano": return "Assalto del Crociato";
                case "Português Brasileiro": return "Golpe do Cruzado";
                case "Русский": return "Удар воина Света";
                case "한국어": return "성전사의 일격";
                case "简体中文": return "十字军打击";
                default: return "Crusader Strike";
            }
        }

        ///<summary>spell=198034</summary>
        private static string DivineHammer_SpellName()
        {
            switch (Language)
            {
                case "English": return "Divine Hammer";
                case "Deutsch": return "Göttlicher Hammer";
                case "Español": return "Martillo divino";
                case "Français": return "Marteau divin";
                case "Italiano": return "Martello Divino";
                case "Português Brasileiro": return "Martelo Divino";
                case "Русский": return "Божественный молот";
                case "한국어": return "천상의 망치";
                case "简体中文": return "神圣之锤";
                default: return "Divine Hammer";
            }
        }

        ///<summary>spell=498</summary>
        private static string DivineProtection_SpellName()
        {
            switch (Language)
            {
                case "English": return "Divine Protection";
                case "Deutsch": return "Göttlicher Schutz";
                case "Español": return "Protección divina";
                case "Français": return "Protection divine";
                case "Italiano": return "Protezione Divina";
                case "Português Brasileiro": return "Proteção Divina";
                case "Русский": return "Божественная защита";
                case "한국어": return "신의 가호";
                case "简体中文": return "圣佑术";
                default: return "Divine Protection";
            }
        }

        ///<summary>spell=642</summary>
        private static string DivineShield_SpellName()
        {
            switch (Language)
            {
                case "English": return "Divine Shield";
                case "Deutsch": return "Gottesschild";
                case "Español": return "Escudo divino";
                case "Français": return "Bouclier divin";
                case "Italiano": return "Scudo Divino";
                case "Português Brasileiro": return "Escudo Divino";
                case "Русский": return "Божественный щит";
                case "한국어": return "천상의 보호막";
                case "简体中文": return "圣盾术";
                default: return "Divine Shield";
            }
        }

        ///<summary>spell=190784</summary>
        private static string DivineSteed_SpellName()
        {
            switch (Language)
            {
                case "English": return "Divine Steed";
                case "Deutsch": return "Göttliches Ross";
                case "Español": return "Corcel divino";
                case "Français": return "Palefroi divin";
                case "Italiano": return "Destriero Divino";
                case "Português Brasileiro": return "Corcel Divino";
                case "Русский": return "Божественный скакун";
                case "한국어": return "천상의 군마";
                case "简体中文": return "神圣马驹";
                default: return "Divine Steed";
            }
        }

        ///<summary>spell=53385</summary>
        private static string DivineStorm_SpellName()
        {
            switch (Language)
            {
                case "English": return "Divine Storm";
                case "Deutsch": return "Göttlicher Sturm";
                case "Español": return "Tormenta divina";
                case "Français": return "Tempête divine";
                case "Italiano": return "Tempesta Divina";
                case "Português Brasileiro": return "Tempestade Divina";
                case "Русский": return "Божественная буря";
                case "한국어": return "천상의 폭풍";
                case "简体中文": return "神圣风暴";
                default: return "Divine Storm";
            }
        }

        ///<summary>spell=375576</summary>
        private static string DivineToll_SpellName()
        {
            switch (Language)
            {
                case "English": return "Divine Toll";
                case "Deutsch": return "Göttlicher Glockenschlag";
                case "Español": return "Estrago divino";
                case "Français": return "Glas divin";
                case "Italiano": return "Rintocco Divino";
                case "Português Brasileiro": return "Preço Divino";
                case "Русский": return "Божественный благовест";
                case "한국어": return "천상의 종";
                case "简体中文": return "圣洁鸣钟";
                default: return "Divine Toll";
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

        ///<summary>spell=343527</summary>
        private static string ExecutionSentence_SpellName()
        {
            switch (Language)
            {
                case "English": return "Execution Sentence";
                case "Deutsch": return "Todesurteil";
                case "Español": return "Sentencia de ejecución";
                case "Français": return "Condamnation à mort";
                case "Italiano": return "Sentenza d'Esecuzione";
                case "Português Brasileiro": return "Pena de Morte";
                case "Русский": return "Смертный приговор";
                case "한국어": return "사형 선고";
                case "简体中文": return "处决宣判";
                default: return "Execution Sentence";
            }
        }

        ///<summary>spell=383185</summary>
        private static string Exorcism_SpellName()
        {
            switch (Language)
            {
                case "English": return "Exorcism";
                case "Deutsch": return "Exorzismus";
                case "Español": return "Exorcismo";
                case "Français": return "Exorcisme";
                case "Italiano": return "Esorcismo";
                case "Português Brasileiro": return "Exorcismo";
                case "Русский": return "Экзорцизм";
                case "한국어": return "퇴마술";
                case "简体中文": return "驱邪术";
                default: return "Exorcism";
            }
        }

        ///<summary>spell=205191</summary>
        private static string EyeForAnEye_SpellName()
        {
            switch (Language)
            {
                case "English": return "Eye for an Eye";
                case "Deutsch": return "Auge um Auge";
                case "Español": return "Ojo por ojo";
                case "Français": return "Œil pour œil";
                case "Italiano": return "Occhio per Occhio";
                case "Português Brasileiro": return "Olho por Olho";
                case "Русский": return "Око за око";
                case "한국어": return "눈에는 눈";
                case "简体中文": return "以眼还眼";
                default: return "Eye for an Eye";
            }
        }

        ///<summary>spell=343721 343721</summary>
        private static string FinalReckoning_SpellName()
        {
            switch (Language)
            {
                case "English": return "Final Reckoning";
                case "Deutsch": return "Letzte Abrechnung";
                case "Español": return "Juicio definitivo";
                case "Français": return "Rétorsion finale";
                case "Italiano": return "Regolamento di Conti Finale";
                case "Português Brasileiro": return "Ajuste de Contas Final";
                case "Русский": return "Последний расчет";
                case "한국어": return "최후의 집행";
                case "简体中文": return "最终清算";
                default: return "Final Reckoning";
            }
        }

        ///<summary>spell=383328</summary>
        private static string FinalVerdict_SpellName()
        {
            switch (Language)
            {
                case "English": return "Final Verdict";
                case "Deutsch": return "Letztes Urteil";
                case "Español": return "Veredicto final";
                case "Français": return "Verdict final";
                case "Italiano": return "Verdetto Finale";
                case "Português Brasileiro": return "Veredito Final";
                case "Русский": return "Окончательный приговор";
                case "한국어": return "최후의 선고";
                case "简体中文": return "最终审判";
                default: return "Final Verdict";
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

        ///<summary>spell=19750</summary>
        private static string FlashOfLight_SpellName()
        {
            switch (Language)
            {
                case "English": return "Flash of Light";
                case "Deutsch": return "Lichtblitz";
                case "Español": return "Destello de Luz";
                case "Français": return "Éclair lumineux";
                case "Italiano": return "Lampo di Luce";
                case "Português Brasileiro": return "Clarão de Luz";
                case "Русский": return "Вспышка Света";
                case "한국어": return "빛의 섬광";
                case "简体中文": return "圣光闪现";
                default: return "Flash of Light";
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

        ///<summary>spell=328180</summary>
        private static string GrippingInfection_SpellName()
        {
            switch (Language)
            {
                case "English": return "Gripping Infection";
                case "Deutsch": return "Packende Infektion";
                case "Español": return "Infección atenazante";
                case "Français": return "Infection enveloppante";
                case "Italiano": return "Infezione Avvinghiante";
                case "Português Brasileiro": return "Infecção Dominante";
                case "Русский": return "Цепкая инфекция";
                case "한국어": return "옭아매는 감염";
                case "简体中文": return "攫握感染";
                default: return "Gripping Infection";
            }
        }

        ///<summary>spell=853</summary>
        private static string HammerOfJustice_SpellName()
        {
            switch (Language)
            {
                case "English": return "Hammer of Justice";
                case "Deutsch": return "Hammer der Gerechtigkeit";
                case "Español": return "Martillo de Justicia";
                case "Français": return "Marteau de la justice";
                case "Italiano": return "Martello della Giustizia";
                case "Português Brasileiro": return "Martelo da Justiça";
                case "Русский": return "Молот правосудия";
                case "한국어": return "심판의 망치";
                case "简体中文": return "制裁之锤";
                default: return "Hammer of Justice";
            }
        }

        ///<summary>spell=24275</summary>
        private static string HammerOfWrath_SpellName()
        {
            switch (Language)
            {
                case "English": return "Hammer of Wrath";
                case "Deutsch": return "Hammer des Zorns";
                case "Español": return "Martillo de cólera";
                case "Français": return "Marteau de courroux";
                case "Italiano": return "Martello dell'Ira";
                case "Português Brasileiro": return "Martelo da Ira";
                case "Русский": return "Молот гнева";
                case "한국어": return "천벌의 망치";
                case "简体中文": return "愤怒之锤";
                default: return "Hammer of Wrath";
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

        ///<summary>spell=105809</summary>
        private static string HolyAvenger_SpellName()
        {
            switch (Language)
            {
                case "English": return "Holy Avenger";
                case "Deutsch": return "Heiliger Rächer";
                case "Español": return "Vengador sagrado";
                case "Français": return "Vengeur sacré";
                case "Italiano": return "Vendicatore Sacro";
                case "Português Brasileiro": return "Vingador Sagrado";
                case "Русский": return "Святой каратель";
                case "한국어": return "신성한 복수자";
                case "简体中文": return "神圣复仇者";
                default: return "Holy Avenger";
            }
        }

        ///<summary>spell=391054</summary>
        private static string Intercession_SpellName()
        {
            switch (Language)
            {
                case "English": return "Intercession";
                case "Deutsch": return "Fürbitte";
                case "Español": return "Intercesión";
                case "Français": return "Intercession";
                case "Italiano": return "Intercessione";
                case "Português Brasileiro": return "Intercessão";
                case "Русский": return "Заступничество";
                case "한국어": return "중재";
                case "简体中文": return "代祷";
                default: return "Intercession";
            }
        }

        ///<summary>spell=20271 20271</summary>
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

        ///<summary>spell=215661</summary>
        private static string JusticarsVengeance_SpellName()
        {
            switch (Language)
            {
                case "English": return "Justicar's Vengeance";
                case "Deutsch": return "Rache des Rechtsprechers";
                case "Español": return "Venganza de justicar";
                case "Français": return "Vengeance du justicier";
                case "Italiano": return "Vendetta del Giustiziere";
                case "Português Brasileiro": return "Vingança do Justicar";
                case "Русский": return "Отмщение вершителя правосудия";
                case "한국어": return "심판관의 복수";
                case "简体中文": return "审判官复仇";
                default: return "Justicar's Vengeance";
            }
        }

        ///<summary>spell=633</summary>
        private static string LayOnHands_SpellName()
        {
            switch (Language)
            {
                case "English": return "Lay on Hands";
                case "Deutsch": return "Handauflegung";
                case "Español": return "Imposición de manos";
                case "Français": return "Imposition des mains";
                case "Italiano": return "Mano Celestiale";
                case "Português Brasileiro": return "Impor as Mãos";
                case "Русский": return "Возложение рук";
                case "한국어": return "신의 축복";
                case "简体中文": return "圣疗术";
                default: return "Lay on Hands";
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

        ///<summary>spell=384052</summary>
        private static string RadiantDecree_SpellName()
        {
            switch (Language)
            {
                case "English": return "Radiant Decree";
                case "Deutsch": return "Strahlender Erlass";
                case "Español": return "Decreto radiante";
                case "Français": return "Décret radieux";
                case "Italiano": return "Decreto Radioso";
                case "Português Brasileiro": return "Decreto Radiante";
                case "Русский": return "Светозарный указ";
                case "한국어": return "광휘의 칙령";
                case "简体中文": return "光辉敕令";
                default: return "Radiant Decree";
            }
        }

        ///<summary>spell=96231</summary>
        private static string Rebuke_SpellName()
        {
            switch (Language)
            {
                case "English": return "Rebuke";
                case "Deutsch": return "Zurechtweisung";
                case "Español": return "Reprimenda";
                case "Français": return "Réprimandes";
                case "Italiano": return "Predica";
                case "Português Brasileiro": return "Repreensão";
                case "Русский": return "Укор";
                case "한국어": return "비난";
                case "简体中文": return "责难";
                default: return "Rebuke";
            }
        }

        ///<summary>spell=20066</summary>
        private static string Repentance_SpellName()
        {
            switch (Language)
            {
                case "English": return "Repentance";
                case "Deutsch": return "Buße";
                case "Español": return "Arrepentimiento";
                case "Français": return "Repentir";
                case "Italiano": return "Penitenza";
                case "Português Brasileiro": return "Contrição";
                case "Русский": return "Покаяние";
                case "한국어": return "참회";
                case "简体中文": return "忏悔";
                default: return "Repentance";
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

        ///<summary>spell=152262</summary>
        private static string Seraphim_SpellName()
        {
            switch (Language)
            {
                case "English": return "Seraphim";
                case "Deutsch": return "Seraphim";
                case "Español": return "Serafín";
                case "Français": return "Séraphin";
                case "Italiano": return "Serafino";
                case "Português Brasileiro": return "Serafim";
                case "Русский": return "Серафим";
                case "한국어": return "고위 천사";
                case "简体中文": return "炽天使";
                default: return "Seraphim";
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

        ///<summary>spell=184662</summary>
        private static string ShieldOfVengeance_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shield of Vengeance";
                case "Deutsch": return "Schild der Vergeltung";
                case "Español": return "Escudo de venganza";
                case "Français": return "Bouclier du vengeur";
                case "Italiano": return "Scudo della Vendetta";
                case "Português Brasileiro": return "Escudo de Vingança";
                case "Русский": return "Щит возмездия";
                case "한국어": return "복수의 방패";
                case "简体中文": return "复仇之盾";
                default: return "Shield of Vengeance";
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

        ///<summary>spell=406647</summary>
        private static string TemplarSlash_SpellName()
        {
            switch (Language)
            {
                case "English": return "Templar Slash";
                case "Deutsch": return "Templerschlitzer";
                case "Español": return "Tajo de templario";
                case "Français": return "Taillade du templier";
                case "Italiano": return "Fendente del Templare";
                case "Português Brasileiro": return "Talho Templário";
                case "Русский": return "Резкий удар храмовника";
                case "한국어": return "기사단의 베기";
                case "简体中文": return "圣殿骑士斩击";
                default: return "Templar Slash";
            }
        }

        ///<summary>spell=407480</summary>
        private static string TemplarStrike_SpellName()
        {
            switch (Language)
            {
                case "English": return "Templar Strike";
                case "Deutsch": return "Templerstoß";
                case "Español": return "Golpe de templario";
                case "Français": return "Frappe du templier";
                case "Italiano": return "Assalto del Templare";
                case "Português Brasileiro": return "Golpe Templário";
                case "Русский": return "Удары храмовника";
                case "한국어": return "기사단의 공세";
                case "简体中文": return "圣殿骑士打击";
                default: return "Templar Strike";
            }
        }

        ///<summary>spell=85256</summary>
        private static string TemplarsVerdict_SpellName()
        {
            switch (Language)
            {
                case "English": return "Templar's Verdict";
                case "Deutsch": return "Urteil des Templers";
                case "Español": return "Veredicto del templario";
                case "Français": return "Verdict du templier";
                case "Italiano": return "Verdetto dei Templari";
                case "Português Brasileiro": return "Veredito do Templário";
                case "Русский": return "Вердикт храмовника";
                case "한국어": return "기사단의 선고";
                case "简体中文": return "圣殿骑士的裁决";
                default: return "Templar's Verdict";
            }
        }

        ///<summary>spell=10326</summary>
        private static string TurnEvil_SpellName()
        {
            switch (Language)
            {
                case "English": return "Turn Evil";
                case "Deutsch": return "Böses vertreiben";
                case "Español": return "Ahuyentar el mal";
                case "Français": return "Renvoi du mal";
                case "Italiano": return "Repulsione del Male";
                case "Português Brasileiro": return "Esconjurar o Mal";
                case "Русский": return "Изгнание зла";
                case "한국어": return "악령 퇴치";
                case "简体中文": return "超度邪恶";
                default: return "Turn Evil";
            }
        }

        ///<summary>spell=328204</summary>
        private static string VanquishersHammer_SpellName()
        {
            switch (Language)
            {
                case "English": return "Vanquisher's Hammer";
                case "Deutsch": return "Hammer des Bezwingers";
                case "Español": return "Martillo del vencedor";
                case "Français": return "Marteau du vainqueur";
                case "Italiano": return "Martello del Dominatore";
                case "Português Brasileiro": return "Martelo do Subjugador";
                case "Русский": return "Молот покорителя";
                case "한국어": return "제압자의 망치";
                case "简体中文": return "征服者之锤";
                default: return "Vanquisher's Hammer";
            }
        }

        ///<summary>spell=255937</summary>
        private static string WakeOfAshes_SpellName()
        {
            switch (Language)
            {
                case "English": return "Wake of Ashes";
                case "Deutsch": return "Aschewelle";
                case "Español": return "Estela de cenizas";
                case "Français": return "Traînée de cendres";
                case "Italiano": return "Scia di Ceneri";
                case "Português Brasileiro": return "Rastro de Cinzas";
                case "Русский": return "Испепеляющий след";
                case "한국어": return "파멸의 재";
                case "简体中文": return "灰烬觉醒";
                default: return "Wake of Ashes";
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

        ///<summary>spell=85673</summary>
        private static string WordOfGlory_SpellName()
        {
            switch (Language)
            {
                case "English": return "Word of Glory";
                case "Deutsch": return "Wort der Herrlichkeit";
                case "Español": return "Palabra de gloria";
                case "Français": return "Mot de gloire";
                case "Italiano": return "Parola di Gloria";
                case "Português Brasileiro": return "Palavra de Glória";
                case "Русский": return "Торжество";
                case "한국어": return "영광의 서약";
                case "简体中文": return "荣耀圣令";
                default: return "Word of Glory";
            }
        }

        ///<summary>spell=190984</summary>
        private static string Wrath_SpellName()
        {
            switch (Language)
            {
                case "English": return "Wrath";
                case "Deutsch": return "Zorn";
                case "Español": return "Cólera";
                case "Français": return "Colère";
                case "Italiano": return "Ira Silvana";
                case "Português Brasileiro": return "Ira";
                case "Русский": return "Гнев";
                case "한국어": return "천벌";
                case "简体中文": return "愤怒";
                default: return "Wrath";
            }
        }
                private static string Forbearance_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Vorahnung";
                case "Español":
                    return "Abstinencia";
                case "Français":
                    return "Longanimité";
                case "Italiano":
                    return "Rigorosità";
                case "Português Brasileiro":
                    return "Abstinência";
                case "Русский":
                    return "Воздержанность";
                case "한국어":
                    return "참을성";
                case "简体中文":
                    return "自律";
                default:
                    return "Forbearance";
            }
        }
                private static string HammerOfLight_SpellName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Hammer des Lichts";
                case "Español":
                    return "Martillo de Luz";
                case "Français":
                    return "Marteau de la Lumière";
                case "Italiano":
                    return "Martello della Luce";
                case "Português Brasileiro":
                    return "Martelo de Luz";
                case "Русский":
                    return "Молот Света";
                case "한국어":
                    return "빛의 망치";
                case "简体中文":
                    return "圣光之锤";
                default:
                    return "Hammer of Light";
            }
        }

        private static string EyeOfTyr_SpellName()
        {
            switch (Language)
            {
                case "English": return "Eye of Tyr";
                case "Deutsch": return "Auge von Tyr";
                case "Español": return "Ojo de Tyr";
                case "Français": return "Œil de Tyr";
                case "Italiano": return "Occhio di Tyr";
                case "Português Brasileiro": return "Olho de Tyr";
                case "Русский": return "Око Тира";
                case "한국어": return "티르의 눈";
                case "简体中文": return "提尔之眼";
                default: return "Eye of Tyr";
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
private static string IceSickles_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Eissicheln";
        case "Español":
            return "Hoces de hielo";
        case "Français":
            return "Faux-de-Glace";
        case "Italiano":
            return "Falcetti di Ghiaccio";
        case "Português Brasileiro":
            return "Gadanhas de Gelo";
        case "Русский":
            return "Ледяные серпы";
        case "한국어":
            return "얼음 낫";
        case "简体中文":
            return "寒冰镰刀";
        default:
            return "Ice Sickles";
    }
}
private static string BestinSlots_ItemName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Spieloplatt";
        case "Español":
            return "Palanca de la suerte";
        case "Français":
            return "As-des-Machines";
        case "Italiano":
            return "Leva della Slot";
        case "Português Brasileiro":
            return "Vale Cada Níquel";
        case "Русский":
            return "БИС";
        case "한국어":
            return "종결 무기";
        case "简体中文":
            return "最上品";
        default:
            return "Best-in-Slots";
    }
}

        private static string MawOfTheVoid_ItemName()
        {
            switch (Language)
            {
                case "Deutsch":
                    return "Maul der Leere";
                case "Español":
                    return "Fauces del Vacío";
                case "Français":
                    return "Gueule du Vide";
                case "Italiano":
                    return "Fauci del Vuoto";
                case "Português Brasileiro":
                    return "Garganta do Caos";
                case "Русский":
                    return "Чрево Пустоты";
                case "한국어":
                    return "공허의 구렁텅이";
                case "简体中文":
                    return "虛空之喉";
                default:
                    return "Maw of the Void";
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

            Inferno.PrintMessage("Epic Rotations Retribution Paladin", Color.Purple);
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
                SpellCasts.Add(375576 ,DivineToll_SpellName()); //304971, 375576
                SpellCasts.Add(316958 ,AshenHallow_SpellName()); //316958
                SpellCasts.Add(328204 ,VanquishersHammer_SpellName()); //328204
                SpellCasts.Add(328620 ,BlessingOfSummer_SpellName()); //328620
                SpellCasts.Add(328622 ,BlessingOfAutumn_SpellName()); //328622
                SpellCasts.Add(328281 ,BlessingOfWinter_SpellName()); //328281
                SpellCasts.Add(328282 ,BlessingOfSpring_SpellName()); //328282
                SpellCasts.Add(96231 ,Rebuke_SpellName()); //96231
                SpellCasts.Add(31884 ,AvengingWrath_SpellName()); //31884, 384376
                SpellCasts.Add(1044 ,BlessingOfFreedom_SpellName()); //1044
                SpellCasts.Add(1022 ,BlessingOfProtection_SpellName()); //1022
                SpellCasts.Add(6940 ,BlessingOfSacrifice_SpellName()); //6940
                SpellCasts.Add(26573 ,Consecration_SpellName()); //26573
                SpellCasts.Add(35395 ,CrusaderStrike_SpellName()); //35395
                SpellCasts.Add(642 ,DivineShield_SpellName()); //642
                SpellCasts.Add(190784 ,DivineSteed_SpellName()); //190784
                SpellCasts.Add(19750 ,FlashOfLight_SpellName()); //19750
                SpellCasts.Add(853 ,HammerOfJustice_SpellName()); //853
                SpellCasts.Add(24275 ,HammerOfWrath_SpellName()); //24275
                SpellCasts.Add(391054 ,Intercession_SpellName()); //391054
                SpellCasts.Add(20271 ,Judgment_SpellName()); //20271
                SpellCasts.Add(633 ,LayOnHands_SpellName()); //633
                SpellCasts.Add(85256 ,TemplarsVerdict_SpellName()); //85256
                SpellCasts.Add(85673 ,WordOfGlory_SpellName()); //85673
                SpellCasts.Add(184575 ,BladeOfJustice_SpellName()); //184575
                SpellCasts.Add(115750 ,BlindingLight_SpellName()); //115750
                SpellCasts.Add(213644 ,CleanseToxins_SpellName()); //213644
                SpellCasts.Add(231895 ,Crusade_SpellName()); //231895
                SpellCasts.Add(198034 ,DivineHammer_SpellName()); //198034
                SpellCasts.Add(498 , DivineProtection_SpellName()); //498
                SpellCasts.Add(53385 , DivineStorm_SpellName()); //53385
                SpellCasts.Add(343527 ,ExecutionSentence_SpellName()); //343527
                SpellCasts.Add(383185 ,Exorcism_SpellName()); //383185
                SpellCasts.Add(205191 ,EyeForAnEye_SpellName()); //205191
                SpellCasts.Add(343721 ,FinalReckoning_SpellName()); //343721
                SpellCasts.Add(383328 ,FinalVerdict_SpellName()); //383328
                SpellCasts.Add(105809 ,HolyAvenger_SpellName()); //105809
                SpellCasts.Add(215661 ,JusticarsVengeance_SpellName()); //215661
                SpellCasts.Add(384052 ,RadiantDecree_SpellName()); //384052
                SpellCasts.Add(20066 ,Repentance_SpellName()); //20066
                SpellCasts.Add(152262 ,Seraphim_SpellName()); //152262
                SpellCasts.Add(184662 ,ShieldOfVengeance_SpellName()); //184662
                SpellCasts.Add(406647 ,TemplarSlash_SpellName()); //406647
                SpellCasts.Add(407480 ,TemplarStrike_SpellName()); //407480
                SpellCasts.Add(255937 , WakeOfAshes_SpellName()); //255937
                SpellCasts.Add(427445 , WakeOfAshes_SpellName()); //427445 //hammer of light
                SpellCasts.Add(427453 , HammerOfLight_SpellName()); //255937
                SpellCasts.Add(155145 , ArcaneTorrent_SpellName()); //155145
                SpellCasts.Add(255647 , LightsJudgment_SpellName()); //255647
   
                
                

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

             Macros.Add("TemperedPot", "/use "+TemperedPotion_SpellName()); 
             Macros.Add("UnwaveringFocusPot", "/use "+PotionofUnwaveringFocus_SpellName()); 

            Macros.Add("BestInSlotCaster", "/use "+BestinSlots_ItemName()); 
            //Lists with spells to use with queues
            MouseoverQueues = new List<string>(){
                Rebuke_SpellName(),
                BlessingOfFreedom_SpellName(),
                BlessingOfSacrifice_SpellName(),
                Intercession_SpellName(),
                WordOfGlory_SpellName(),
                Repentance_SpellName(),
                HammerOfJustice_SpellName(),
                BlessingOfProtection_SpellName(),
                CleanseToxins_SpellName(),

            };
            CursorQueues = new List<string>(){
                AshenHallow_SpellName(),
                FinalReckoning_SpellName(),
            };
            PlayerQueues = new List<string>(){
                BlessingOfFreedom_SpellName(),
                BlessingOfProtection_SpellName(),
                Consecration_SpellName(),
                DivineShield_SpellName(),
                DivineSteed_SpellName(),
                FlashOfLight_SpellName(),
                LayOnHands_SpellName(),
                WordOfGlory_SpellName(),
                DivineProtection_SpellName(),
                HolyAvenger_SpellName(),
                Seraphim_SpellName(),
                ShieldOfVengeance_SpellName(),
                CleanseToxins_SpellName(),
                FinalReckoning_SpellName(),
                BlindingLight_SpellName(),
            };
            FocusQueues = new List<string>(){
                CleanseToxins_SpellName(),
                LayOnHands_SpellName(),
                BlessingOfSacrifice_SpellName(),
                BlessingOfProtection_SpellName(),
                Intercession_SpellName(),
                WordOfGlory_SpellName(),
            };
            TargetQueues = new List<string>(){
                HammerOfJustice_SpellName(),
                Rebuke_SpellName(),
                DivineToll_SpellName(),
                AshenHallow_SpellName(),
                CrusaderStrike_SpellName(),
                HammerOfWrath_SpellName(),
                Intercession_SpellName(),
                CleanseToxins_SpellName(),
                Exorcism_SpellName(),

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

