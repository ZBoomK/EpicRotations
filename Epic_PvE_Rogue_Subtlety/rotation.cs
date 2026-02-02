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

///<summary>spell=8676</summary>
        private static string Ambush_SpellName()
        {
            switch (Language)
            {
                case "English": return "Ambush";
                case "Deutsch": return "Hinterhalt";
                case "Español": return "Emboscada";
                case "Français": return "Embuscade";
                case "Italiano": return "Imboscata";
                case "Português Brasileiro": return "Emboscar";
                case "Русский": return "Внезапный удар";
                case "한국어": return "매복";
                case "简体中文": return "伏击";
                default: return "Ambush";
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

        ///<summary>spell=53</summary>
        private static string Backstab_SpellName()
        {
            switch (Language)
            {
                case "English": return "Backstab";
                case "Deutsch": return "Meucheln";
                case "Español": return "Puñalada";
                case "Français": return "Attaque sournoise";
                case "Italiano": return "Pugnalata alle Spalle";
                case "Português Brasileiro": return "Punhalada pelas Costas";
                case "Русский": return "Удар в спину";
                case "한국어": return "기습";
                case "简体中文": return "背刺";
                default: return "Backstab";
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

        ///<summary>spell=319175</summary>
        private static string BlackPowder_SpellName()
        {
            switch (Language)
            {
                case "English": return "Black Powder";
                case "Deutsch": return "Schwarzpulver";
                case "Español": return "Pólvora negra";
                case "Français": return "Poudre noire";
                case "Italiano": return "Polvere Nera";
                case "Português Brasileiro": return "Pólvora Negra";
                case "Русский": return "Черный порох";
                case "한국어": return "검은 화약";
                case "简体中文": return "黑火药";
                default: return "Black Powder";
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

        ///<summary>spell=328085</summary>
        private static string Blindside_SpellName()
        {
            switch (Language)
            {
                case "English": return "Blindside";
                case "Deutsch": return "Wunder Punkt";
                case "Español": return "Punto ciego";
                case "Français": return "Angle mort";
                case "Italiano": return "Lato Cieco";
                case "Português Brasileiro": return "Ponto Cego";
                case "Русский": return "Слепая зона";
                case "한국어": return "사각 지대";
                case "简体中文": return "侧袭";
                default: return "Blindside";
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

        ///<summary>spell=1833</summary>
        private static string CheapShot_SpellName()
        {
            switch (Language)
            {
                case "English": return "Cheap Shot";
                case "Deutsch": return "Fieser Trick";
                case "Español": return "Golpe bajo";
                case "Français": return "Coup bas";
                case "Italiano": return "Colpo Basso";
                case "Português Brasileiro": return "Golpe Baixo";
                case "Русский": return "Подлый трюк";
                case "한국어": return "비열한 습격";
                case "简体中文": return "偷袭";
                default: return "Cheap Shot";
            }
        }
private static string CoupdeGrace_SpellName()
{
    switch (Language)
    {
        case "Deutsch":
            return "Gnadenstoß";
        case "Español":
            return "Golpe de gracia";
        case "Français":
            return "Achèvement";
        case "Italiano":
            return "Colpo di Grazia";
        case "Português Brasileiro":
            return "Golpe de Misericórdia";
        case "Русский":
            return "Удар милосердия";
        case "한국어":
            return "최후의 일격";
        case "简体中文":
            return "致命一击";
        default:
            return "Coup de Grace";
    }
}
        ///<summary>spell=31224</summary>
        private static string CloakOfShadows_SpellName()
        {
            switch (Language)
            {
                case "English": return "Cloak of Shadows";
                case "Deutsch": return "Mantel der Schatten";
                case "Español": return "Capa de las Sombras";
                case "Français": return "Cape d'ombre";
                case "Italiano": return "Manto d'Ombra";
                case "Português Brasileiro": return "Manto das Sombras";
                case "Русский": return "Плащ теней";
                case "한국어": return "그림자 망토";
                case "简体中文": return "暗影斗篷";
                default: return "Cloak of Shadows";
            }
        }

        ///<summary>spell=382245</summary>
        private static string ColdBlood_SpellName()
        {
            switch (Language)
            {
                case "English": return "Cold Blood";
                case "Deutsch": return "Kaltblütigkeit";
                case "Español": return "Sangre fría";
                case "Français": return "Sang froid";
                case "Italiano": return "Sangue Freddo";
                case "Português Brasileiro": return "Sangue Frio";
                case "Русский": return "Хладнокровие";
                case "한국어": return "냉혈";
                case "简体中文": return "冷血";
                default: return "Cold Blood";
            }
        }

        ///<summary>spell=185311</summary>
        private static string CrimsonVial_SpellName()
        {
            switch (Language)
            {
                case "English": return "Crimson Vial";
                case "Deutsch": return "Blutrote Phiole";
                case "Español": return "Vial carmesí";
                case "Français": return "Fiole cramoisie";
                case "Italiano": return "Fiala Cremisi";
                case "Português Brasileiro": return "Frasco Rubro";
                case "Русский": return "Алый фиал";
                case "한국어": return "진홍색 약병";
                case "简体中文": return "猩红之瓶";
                default: return "Crimson Vial";
            }
        }

        ///<summary>spell=1725</summary>
        private static string Distract_SpellName()
        {
            switch (Language)
            {
                case "English": return "Distract";
                case "Deutsch": return "Ablenken";
                case "Español": return "Distraer";
                case "Français": return "Distraction";
                case "Italiano": return "Distrazione";
                case "Português Brasileiro": return "Distração";
                case "Русский": return "Отвлечение";
                case "한국어": return "혼란";
                case "简体中文": return "扰乱";
                default: return "Distract";
            }
        }

        ///<summary>spell=385616</summary>
        private static string EchoingReprimand_SpellName()
        {
            switch (Language)
            {
                case "English": return "Echoing Reprimand";
                case "Deutsch": return "Widerhallender Tadel";
                case "Español": return "Reprimenda resonante";
                case "Français": return "Réprimande retentissante";
                case "Italiano": return "Rimprovero Rimbombante";
                case "Português Brasileiro": return "Reprimenda Ecoante";
                case "Русский": return "Продолжительная отповедь";
                case "한국어": return "울려퍼지는 문책";
                case "简体中文": return "申斥回响";
                default: return "Echoing Reprimand";
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

        ///<summary>spell=5277</summary>
        private static string Evasion_SpellName()
        {
            switch (Language)
            {
                case "English": return "Evasion";
                case "Deutsch": return "Entrinnen";
                case "Español": return "Evasión";
                case "Français": return "Évasion";
                case "Italiano": return "Evasione";
                case "Português Brasileiro": return "Evasão";
                case "Русский": return "Ускользание";
                case "한국어": return "회피";
                case "简体中文": return "闪避";
                default: return "Evasion";
            }
        }

        ///<summary>spell=196819</summary>
        private static string Eviscerate_SpellName()
        {
            switch (Language)
            {
                case "English": return "Eviscerate";
                case "Deutsch": return "Ausweiden";
                case "Español": return "Eviscerar";
                case "Français": return "Eviscération";
                case "Italiano": return "Sventramento";
                case "Português Brasileiro": return "Eviscerar";
                case "Русский": return "Потрошение";
                case "한국어": return "절개";
                case "简体中文": return "刺骨";
                default: return "Eviscerate";
            }
        }

        ///<summary>spell=51723</summary>
        private static string FanOfKnives_SpellName()
        {
            switch (Language)
            {
                case "English": return "Fan of Knives";
                case "Deutsch": return "Dolchfächer";
                case "Español": return "Abanico de cuchillos";
                case "Français": return "Éventail de couteaux";
                case "Italiano": return "Ventaglio di Lame";
                case "Português Brasileiro": return "Leque de Facas";
                case "Русский": return "Веер клинков";
                case "한국어": return "칼날 부채";
                case "简体中文": return "刀扇";
                default: return "Fan of Knives";
            }
        }

        ///<summary>spell=1966</summary>
        private static string Feint_SpellName()
        {
            switch (Language)
            {
                case "English": return "Feint";
                case "Deutsch": return "Finte";
                case "Español": return "Amago";
                case "Français": return "Feinte";
                case "Italiano": return "Finta";
                case "Português Brasileiro": return "Finta";
                case "Русский": return "Уловка";
                case "한국어": return "교란";
                case "简体中文": return "佯攻";
                default: return "Feint";
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

        ///<summary>spell=323654</summary>
        private static string Flagellation_SpellName()
        {
            switch (Language)
            {
                case "English": return "Flagellation";
                case "Deutsch": return "Geißelung";
                case "Español": return "Flagelación";
                case "Français": return "Flagellation";
                case "Italiano": return "Flagellazione";
                case "Português Brasileiro": return "Flagelação";
                case "Русский": return "Флагелляция";
                case "한국어": return "채찍질";
                case "简体中文": return "狂热鞭笞";
                default: return "Flagellation";
            }
        }

        ///<summary>spell=703</summary>
        private static string Garrote_SpellName()
        {
            switch (Language)
            {
                case "English": return "Garrote";
                case "Deutsch": return "Erdrosseln";
                case "Español": return "Garrote";
                case "Français": return "Garrot";
                case "Italiano": return "Garrota";
                case "Português Brasileiro": return "Garrote";
                case "Русский": return "Гаррота";
                case "한국어": return "목조르기";
                case "简体中文": return "锁喉";
                default: return "Garrote";
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

        ///<summary>spell=200758</summary>
        private static string Gloomblade_SpellName()
        {
            switch (Language)
            {
                case "English": return "Gloomblade";
                case "Deutsch": return "Düstere Klinge";
                case "Español": return "Hoja de la penumbra";
                case "Français": return "Triste-lame";
                case "Italiano": return "Lama Tenebrosa";
                case "Português Brasileiro": return "Lâmina Lúgubre";
                case "Русский": return "Клинок мрака";
                case "한국어": return "어둠칼날";
                case "简体中文": return "幽暗之刃";
                default: return "Gloomblade";
            }
        }

        ///<summary>spell=426591</summary>
        private static string GoremawsBite_SpellName()
        {
            switch (Language)
            {
                case "English": return "Goremaw's Bite";
                case "Deutsch": return "Blutschlunds Biss";
                case "Español": return "Mordedura de Buchegore";
                case "Français": return "Morsure de Gueulétripe";
                case "Italiano": return "Morso di Faucinere";
                case "Português Brasileiro": return "Mordida do Gorjavil";
                case "Русский": return "Укус Кровавой Пасти";
                case "한국어": return "피아귀의 이빨";
                case "简体中文": return "赤喉之咬";
                default: return "Goremaw's Bite";
            }
        }

        ///<summary>spell=1776</summary>
        private static string Gouge_SpellName()
        {
            switch (Language)
            {
                case "English": return "Gouge";
                case "Deutsch": return "Solarplexus";
                case "Español": return "Gubia";
                case "Français": return "Suriner";
                case "Italiano": return "Sfregio Oculare";
                case "Português Brasileiro": return "Esfaquear";
                case "Русский": return "Парализующий удар";
                case "한국어": return "후려치기";
                case "简体中文": return "凿击";
                default: return "Gouge";
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

        ///<summary>spell=408</summary>
        private static string KidneyShot_SpellName()
        {
            switch (Language)
            {
                case "English": return "Kidney Shot";
                case "Deutsch": return "Nierenhieb";
                case "Español": return "Golpe en los riñones";
                case "Français": return "Aiguillon perfide";
                case "Italiano": return "Colpo ai Reni";
                case "Português Brasileiro": return "Golpe no Rim";
                case "Русский": return "Удар по почкам";
                case "한국어": return "급소 가격";
                case "简体中文": return "肾击";
                default: return "Kidney Shot";
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

        ///<summary>spell=137619</summary>
        private static string MarkedForDeath_SpellName()
        {
            switch (Language)
            {
                case "English": return "Marked for Death";
                case "Deutsch": return "Todesurteil";
                case "Español": return "Marcado para morir";
                case "Français": return "Désigné pour mourir";
                case "Italiano": return "Marchio della Morte";
                case "Português Brasileiro": return "Marcado para Morrer";
                case "Русский": return "Метка смерти";
                case "한국어": return "죽음의 표적";
                case "简体中文": return "死亡标记";
                default: return "Marked for Death";
            }
        }

        ///<summary>spell=1329</summary>
        private static string Mutilate_SpellName()
        {
            switch (Language)
            {
                case "English": return "Mutilate";
                case "Deutsch": return "Verstümmeln";
                case "Español": return "Mutilar";
                case "Français": return "Estropier";
                case "Italiano": return "Mutilazione";
                case "Português Brasileiro": return "Mutilar";
                case "Русский": return "Расправа";
                case "한국어": return "절단";
                case "简体中文": return "毁伤";
                default: return "Mutilate";
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

        ///<summary>spell=1943</summary>
        private static string Rupture_SpellName()
        {
            switch (Language)
            {
                case "English": return "Rupture";
                case "Deutsch": return "Blutung";
                case "Español": return "Ruptura";
                case "Français": return "Rupture";
                case "Italiano": return "Perforazione";
                case "Português Brasileiro": return "Ruptura";
                case "Русский": return "Рваная рана";
                case "한국어": return "파열";
                case "简体中文": return "割裂";
                default: return "Rupture";
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

        ///<summary>spell=280719</summary>
        private static string SecretTechnique_SpellName()
        {
            switch (Language)
            {
                case "English": return "Secret Technique";
                case "Deutsch": return "Geheimtechnik";
                case "Español": return "Técnica secreta";
                case "Français": return "Technique secrète";
                case "Italiano": return "Tecnica Segreta";
                case "Português Brasileiro": return "Técnica Secreta";
                case "Русский": return "Тайный прием";
                case "한국어": return "은밀한 기술";
                case "简体中文": return "影分身";
                default: return "Secret Technique";
            }
        }

        ///<summary>spell=328305</summary>
        private static string Sepsis_SpellName()
        {
            switch (Language)
            {
                case "English": return "Sepsis";
                case "Deutsch": return "Sepsis";
                case "Español": return "Sepsis";
                case "Français": return "Septicémie";
                case "Italiano": return "Sepsi";
                case "Português Brasileiro": return "Sepse";
                case "Русский": return "Сепсис";
                case "한국어": return "피고름";
                case "简体中文": return "败血刃伤";
                default: return "Sepsis";
            }
        }

        ///<summary>spell=328547</summary>
        private static string SerratedBoneSpike_SpellName()
        {
            switch (Language)
            {
                case "English": return "Serrated Bone Spike";
                case "Deutsch": return "Gezackter Knochenstachel";
                case "Español": return "Púa ósea dentada";
                case "Français": return "Pointe d’os dentelée";
                case "Italiano": return "Aculeo Osseo Seghettato";
                case "Português Brasileiro": return "Espigão Ósseo Serrilhado";
                case "Русский": return "Зазубренный костяной шип";
                case "한국어": return "톱니 뼈 가시";
                case "简体中文": return "锯齿骨刺";
                default: return "Serrated Bone Spike";
            }
        }

        ///<summary>spell=121471</summary>
        private static string ShadowBlades_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shadow Blades";
                case "Deutsch": return "Schattenklingen";
                case "Español": return "Hojas de las Sombras";
                case "Français": return "Lames de l’ombre";
                case "Italiano": return "Lame d'Ombra";
                case "Português Brasileiro": return "Lâminas Sombrias";
                case "Русский": return "Теневые клинки";
                case "한국어": return "어둠의 칼날";
                case "简体中文": return "暗影之刃";
                default: return "Shadow Blades";
            }
        }

        ///<summary>spell=185313</summary>
        private static string ShadowDance_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shadow Dance";
                case "Deutsch": return "Schattentanz";
                case "Español": return "Danza de las Sombras";
                case "Français": return "Danse de l’ombre";
                case "Italiano": return "Danza dell'Ombra";
                case "Português Brasileiro": return "Dança das Sombras";
                case "Русский": return "Танец теней";
                case "한국어": return "어둠의 춤";
                case "简体中文": return "暗影之舞";
                default: return "Shadow Dance";
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

        ///<summary>spell=36554</summary>
        private static string Shadowstep_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shadowstep";
                case "Deutsch": return "Schattenschritt";
                case "Español": return "Paso de las Sombras";
                case "Français": return "Pas de l’ombre";
                case "Italiano": return "Passo nell'Ombra";
                case "Português Brasileiro": return "Passo Furtivo";
                case "Русский": return "Шаг сквозь тень";
                case "한국어": return "그림자 밟기";
                case "简体中文": return "暗影步";
                default: return "Shadowstep";
            }
        }

        ///<summary>spell=185438</summary>
        private static string Shadowstrike_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shadowstrike";
                case "Deutsch": return "Schattenschlag";
                case "Español": return "Embate de las Sombras";
                case "Français": return "Frappe-ténèbres";
                case "Italiano": return "Assalto d'Ombra";
                case "Português Brasileiro": return "Golpe Sombrio";
                case "Русский": return "Удар Тьмы";
                case "한국어": return "그림자 일격";
                case "简体中文": return "暗影打击";
                default: return "Shadowstrike";
            }
        }

        ///<summary>spell=5938</summary>
        private static string Shiv_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shiv";
                case "Deutsch": return "Tückische Klinge";
                case "Español": return "Puyazo";
                case "Français": return "Kriss";
                case "Italiano": return "Stilettata";
                case "Português Brasileiro": return "Estocada";
                case "Русский": return "Отравляющий укол";
                case "한국어": return "독칼";
                case "简体中文": return "毒刃";
                default: return "Shiv";
            }
        }

        ///<summary>spell=197835</summary>
        private static string ShurikenStorm_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shuriken Storm";
                case "Deutsch": return "Shurikensturm";
                case "Español": return "Tormenta de shuriken";
                case "Français": return "Tempête de shurikens";
                case "Italiano": return "Tempesta di Shuriken";
                case "Português Brasileiro": return "Tempestade de Shurikens";
                case "Русский": return "Шквал сюрикэнов";
                case "한국어": return "표창 폭풍";
                case "简体中文": return "袖剑风暴";
                default: return "Shuriken Storm";
            }
        }

        ///<summary>spell=277925</summary>
        private static string ShurikenTornado_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shuriken Tornado";
                case "Deutsch": return "Shurikentornado";
                case "Español": return "Tornado de shuriken";
                case "Français": return "Tornade de shurikens";
                case "Italiano": return "Tornado di Shuriken";
                case "Português Brasileiro": return "Tornado de Shurikens";
                case "Русский": return "Торнадо из сюрикэнов";
                case "한국어": return "표창 회오리";
                case "简体中文": return "袖剑旋风";
                default: return "Shuriken Tornado";
            }
        }

        ///<summary>spell=114014</summary>
        private static string ShurikenToss_SpellName()
        {
            switch (Language)
            {
                case "English": return "Shuriken Toss";
                case "Deutsch": return "Shurikenwurf";
                case "Español": return "Lanzamiento de shuriken";
                case "Français": return "Lancer de shuriken";
                case "Italiano": return "Lancio dello Shuriken";
                case "Português Brasileiro": return "Lançar Shuriken";
                case "Русский": return "Бросок сюрикэна";
                case "한국어": return "표창 투척";
                case "简体中文": return "飞镖投掷";
                default: return "Shuriken Toss";
            }
        }

        ///<summary>spell=315496</summary>
        private static string SliceAndDice_SpellName()
        {
            switch (Language)
            {
                case "English": return "Slice and Dice";
                case "Deutsch": return "Zerhäckseln";
                case "Español": return "Hacer picadillo";
                case "Français": return "Débiter";
                case "Italiano": return "Fendenti Furiosi";
                case "Português Brasileiro": return "Retalhar";
                case "Русский": return "Мясорубка";
                case "한국어": return "난도질";
                case "简体中文": return "切割";
                default: return "Slice and Dice";
            }
        }

        ///<summary>spell=2983</summary>
        private static string Sprint_SpellName()
        {
            switch (Language)
            {
                case "English": return "Sprint";
                case "Deutsch": return "Sprinten";
                case "Español": return "Sprint";
                case "Français": return "Sprint";
                case "Italiano": return "Scatto";
                case "Português Brasileiro": return "Disparada";
                case "Русский": return "Спринт";
                case "한국어": return "전력 질주";
                case "简体中文": return "疾跑";
                default: return "Sprint";
            }
        }

        ///<summary>spell=1784</summary>
        private static string Stealth_SpellName()
        {
            switch (Language)
            {
                case "English": return "Stealth";
                case "Deutsch": return "Verstohlenheit";
                case "Español": return "Sigilo";
                case "Français": return "Camouflage";
                case "Italiano": return "Furtività";
                case "Português Brasileiro": return "Furtividade";
                case "Русский": return "Незаметность";
                case "한국어": return "은신";
                case "简体中文": return "潜行";
                default: return "Stealth";
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

        ///<summary>spell=108208</summary>
        private static string Subterfuge_SpellName()
        {
            switch (Language)
            {
                case "English": return "Subterfuge";
                case "Deutsch": return "Trickbetrug";
                case "Español": return "Subterfugio";
                case "Français": return "Subterfuge";
                case "Italiano": return "Sotterfugio";
                case "Português Brasileiro": return "Subterfúgio";
                case "Русский": return "Увертка";
                case "한국어": return "기만";
                case "简体中文": return "诡诈";
                default: return "Subterfuge";
            }
        }

        ///<summary>spell=212283</summary>
        private static string SymbolsOfDeath_SpellName()
        {
            switch (Language)
            {
                case "English": return "Symbols of Death";
                case "Deutsch": return "Symbole des Todes";
                case "Español": return "Símbolos de la Muerte";
                case "Français": return "Symboles de mort";
                case "Italiano": return "Simboli di Morte";
                case "Português Brasileiro": return "Símbolos da Morte";
                case "Русский": return "Символы смерти";
                case "한국어": return "죽음의 상징";
                case "简体中文": return "死亡符记";
                default: return "Symbols of Death";
            }
        }

        ///<summary>spell=381623</summary>
        private static string ThistleTea_SpellName()
        {
            switch (Language)
            {
                case "English": return "Thistle Tea";
                case "Deutsch": return "Disteltee";
                case "Español": return "Té de cardo";
                case "Français": return "Thé de chardon";
                case "Italiano": return "Tè di Sboccialesto";
                case "Português Brasileiro": return "Chá de Cardo";
                case "Русский": return "Скорополоховый чай";
                case "한국어": return "엉겅퀴 차";
                case "简体中文": return "菊花茶";
                default: return "Thistle Tea";
            }
        }

        ///<summary>spell=1856</summary>
        private static string Vanish_SpellName()
        {
            switch (Language)
            {
                case "English": return "Vanish";
                case "Deutsch": return "Verschwinden";
                case "Español": return "Esfumarse";
                case "Français": return "Disparition";
                case "Italiano": return "Sparizione";
                case "Português Brasileiro": return "Sumir";
                case "Русский": return "Исчезновение";
                case "한국어": return "소멸";
                case "简体中文": return "消失";
                default: return "Vanish";
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
                ///<summary>spell=5761</summary>
        private static string NumbingPoison_SpellName()
        {
            switch (Language)
            {
                case "English": return "Numbing Poison";
                case "Deutsch": return "Lähmendes Gift";
                case "Español": return "Veneno entumecedor";
                case "Français": return "Poison ankylosant";
                case "Italiano": return "Veleno Anestetizzante";
                case "Português Brasileiro": return "Veneno Entorpecente";
                case "Русский": return "Замедляющий яд";
                case "한국어": return "마취 독";
                case "简体中文": return "迟钝药膏";
                default: return "Numbing Poison";
            }
        }
        ///<summary>spell=315584</summary>
        private static string InstantPoison_SpellName()
        {
            switch (Language)
            {
                case "English": return "Instant Poison";
                case "Deutsch": return "Sofort wirkendes Gift";
                case "Español": return "Veneno instantáneo";
                case "Français": return "Poison instantané";
                case "Italiano": return "Veleno Istantaneo";
                case "Português Brasileiro": return "Veneno Instantâneo";
                case "Русский": return "Быстродействующий яд";
                case "한국어": return "순간 효과 독";
                case "简体中文": return "速效药膏";
                default: return "Instant Poison";
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
        private static string AmplifyingPoison_SpellName()//381664
            {
                switch (Language)
                {
                    case "Deutsch":
                        return "Verstärkendes Gift";
                    case "Español":
                        return "Veneno amplificador";
                    case "Français":
                        return "Poison amplifiant";
                    case "Italiano":
                        return "Veleno Amplificante";
                    case "Português Brasileiro":
                        return "Veneno Amplificador";
                    case "Русский":
                        return "Усиление яда";
                    case "한국어":
                        return "증폭의 독";
                    case "简体中文":
                        return "增效药膏";
                    default:
                        return "Amplifying Poison";
                }
            }
            private static string DeadlyPoison_SpellName()//2823
            {
                switch (Language)
                {
                    case "Deutsch":
                        return "Tödliches Gift";
                    case "Español":
                        return "Veneno mortal";
                    case "Français":
                        return "Poison mortel";
                    case "Italiano":
                        return "Veleno Mortale";
                    case "Português Brasileiro":
                        return "Veneno Mortal";
                    case "Русский":
                        return "Смертоносный яд";
                    case "한국어":
                        return "맹독";
                    case "简体中文":
                        return "夺命药膏";
                    default:
                        return "Deadly Poison";
                }
            }
            private static string AtrophicPoison_SpellName()//381637
            {
                switch (Language)
                {
                    case "Deutsch":
                        return "Atrophisches Gift";
                    case "Español":
                        return "Veneno atrófico";
                    case "Français":
                        return "Poison atrophiant";
                    case "Italiano":
                        return "Veleno Atrofizzante";
                    case "Português Brasileiro":
                        return "Veneno Atrófico";
                    case "Русский":
                        return "Атрофический яд";
                    case "한국어":
                        return "위축의 독";
                    case "简体中文":
                        return "萎缩药膏";
                    default:
                        return "Atrophic Poison";
                }
            }
            private static string cripplingPoison_SpellName()//3408
            {
                switch (Language)
                {
                    case "Deutsch":
                        return "Verkrüppelndes Gift";
                    case "Español":
                        return "Veneno entorpecedor";
                    case "Français":
                        return "Poison affaiblissant";
                    case "Italiano":
                        return "Veleno Paralizzante";
                    case "Português Brasileiro":
                        return "Veneno Debilitante";
                    case "Русский":
                        return "Калечащий яд";
                    case "한국어":
                        return "신경 마취 독";
                    case "简体中文":
                        return "减速药膏";
                    default:
                        return "Crippling Poison";
                }
            }
            private static string TricksoftheTrade_SpellName()
            {
                switch (Language)
                {
                    case "Deutsch":
                        return "Schurkenhandel";
                    case "Español":
                        return "Secretos del oficio";
                    case "Français":
                        return "Ficelles du métier";
                    case "Italiano":
                        return "Trucchi del Mestiere";
                    case "Português Brasileiro":
                        return "Truques do Ofício";
                    case "Русский":
                        return "Маленькие хитрости";
                    case "한국어":
                        return "속임수 거래";
                    case "简体中文":
                        return "嫁祸诀窍";
                    default:
                        return "Tricks of the Trade";
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

            Inferno.PrintMessage("Epic Rotations Rogue Subtlety", Color.Purple);
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
                SpellCasts.Add(385616, EchoingReprimand_SpellName()); //385616,323547
                SpellCasts.Add(384631, Flagellation_SpellName()); //323654 , 384631
                SpellCasts.Add(385408, Sepsis_SpellName()); //328305 , 385408
                SpellCasts.Add(385424, SerratedBoneSpike_SpellName()); //328547 , 385424
                SpellCasts.Add(1766, Kick_SpellName()); //1766
                SpellCasts.Add(8676, Ambush_SpellName()); //8676
                SpellCasts.Add(1833, CheapShot_SpellName()); //1833
                SpellCasts.Add(185311, CrimsonVial_SpellName()); //185311
                SpellCasts.Add(1725, Distract_SpellName()); //1725
                SpellCasts.Add(196819, Eviscerate_SpellName()); //196819
                SpellCasts.Add(408, KidneyShot_SpellName()); //408
                SpellCasts.Add(315496, SliceAndDice_SpellName()); //315496
                SpellCasts.Add(2983, Sprint_SpellName()); //2983
                SpellCasts.Add(1784, Stealth_SpellName()); //1784
                SpellCasts.Add(1856, Vanish_SpellName()); //1856
                SpellCasts.Add(2094, Blind_SpellName()); //2094
                SpellCasts.Add(31224, CloakOfShadows_SpellName()); //31224
                SpellCasts.Add(382245, ColdBlood_SpellName()); //382245
                SpellCasts.Add(5277, Evasion_SpellName()); //5277
                SpellCasts.Add(1966, Feint_SpellName()); //1966
                SpellCasts.Add(1776, Gouge_SpellName()); //1776
                SpellCasts.Add(137619, MarkedForDeath_SpellName()); //137619
                SpellCasts.Add(6770, Sap_SpellName()); //6770
                SpellCasts.Add(185313, ShadowDance_SpellName()); //185313
                SpellCasts.Add(36554, Shadowstep_SpellName()); //36554
                SpellCasts.Add(5938, Shiv_SpellName()); //5938
                SpellCasts.Add(381623, ThistleTea_SpellName()); //381623
                SpellCasts.Add(53, Backstab_SpellName()); //53
                SpellCasts.Add(319175, BlackPowder_SpellName()); //319175
                SpellCasts.Add(200758, Gloomblade_SpellName()); //200758
                SpellCasts.Add(1943, Rupture_SpellName()); //1943
                SpellCasts.Add(280719, SecretTechnique_SpellName()); //280719
                SpellCasts.Add(121471, ShadowBlades_SpellName()); //121471
                SpellCasts.Add(185438, Shadowstrike_SpellName()); //185438
                SpellCasts.Add(197835, ShurikenStorm_SpellName()); //197835
                SpellCasts.Add(277925, ShurikenTornado_SpellName()); //277925
                SpellCasts.Add(114014, ShurikenToss_SpellName()); //114014
                SpellCasts.Add(212283, SymbolsOfDeath_SpellName()); //212283
                SpellCasts.Add(381664,AmplifyingPoison_SpellName());//381664
                SpellCasts.Add(2823,DeadlyPoison_SpellName());//2823
                SpellCasts.Add(381637,AtrophicPoison_SpellName());//381637
                SpellCasts.Add(3408,cripplingPoison_SpellName());//3408
                SpellCasts.Add(315584,InstantPoison_SpellName());//315584
                SpellCasts.Add(5761, NumbingPoison_SpellName());//5761
                SpellCasts.Add(441423, Eviscerate_SpellName());//441423 Coup
                SpellCasts.Add(441776, Eviscerate_SpellName());//441776 Coup



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
                Kick_SpellName(),
                Ambush_SpellName(),
                CheapShot_SpellName(),
                KidneyShot_SpellName(),
                Blind_SpellName(),
                Gouge_SpellName(),
                Sap_SpellName(),
                Shadowstep_SpellName(),
                TricksoftheTrade_SpellName()
                

            };
            CursorQueues = new List<string>(){
                Distract_SpellName(),

            };
            PlayerQueues = new List<string>(){
                CrimsonVial_SpellName(),
                SliceAndDice_SpellName(),
                Sprint_SpellName(),
                Stealth_SpellName(),
                Vanish_SpellName(),
                CloakOfShadows_SpellName(),
                Evasion_SpellName(),
                Feint_SpellName(),
            };
            FocusQueues = new List<string>(){
                Kick_SpellName(),
                Blind_SpellName(),
            };
            TargetQueues = new List<string>(){
                Kick_SpellName(),
                Ambush_SpellName(),
                CheapShot_SpellName(),
                KidneyShot_SpellName(),
                Blind_SpellName(),
                Gouge_SpellName(),
                Sap_SpellName(),
                Shadowstep_SpellName(),

         
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

