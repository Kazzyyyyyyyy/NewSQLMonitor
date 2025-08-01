using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace NewSQLMonitor {

    public class SettingsData {

        //init
        MainWindow _mainWindow = Application.Current.MainWindow as MainWindow;
        readonly string SettingsFilePath = @$"C:\Users\{Environment.UserName}\AppData\Local\SQLMonitor\programmSettings.json";


        //Settings
        public class SettingsValues {
            public string ProgrammLogFilePath { get; set; } = @$"C:\Users\{Environment.UserName}\AppData\Local\SQLMonitor\programmErrorLog.txt";
            public string LastDBFilePath { get; set; } = string.Empty;
            public bool AutoUpdate { get; set; } = false;
            public double AutoUpdateInterval { get; set; } = 3;
            public bool LoadOnlyStringRows { get; set; } = false;
            public bool ShowIDPrimaryKey { get; set; } = true;
            public bool LoadLastDBFilePath { get; set; } = false;
            public string SpaceRowsWithASCII { get; set; } = string.Empty;
            public bool CreateAndSaveUndoFiles { get; set; } = false;

        }


        //Functions 
        public bool SettingsRestored = false;
        public SettingsValues CreateAndRepairSettingsFile() {

            if(!Directory.Exists(@$"C:\Users\{Environment.UserName}\AppData\Local\SQLMonitor")) {
                Directory.CreateDirectory($@"C:\Users\{Environment.UserName}\AppData\Local\SQLMonitor");
            }

            if (File.Exists(SettingsFilePath)) { 
                File.Delete(SettingsFilePath); 
            }

            File.Create(SettingsFilePath).Dispose();

            var saveSettings = new SettingsValues();

            SaveSettings(JsonSerializer.Serialize<SettingsValues>(saveSettings), false);

            SettingsRestored = true;
            return LoadSettings();
        }

        public SettingsValues LoadSettings() {


            try {
                string json = File.ReadAllText(SettingsFilePath);

                return JsonSerializer.Deserialize<SettingsValues>(json);
            }
            catch {
                return null; 
            }
        }

        public void SaveSettings(string serializedData, bool reload = true) {

            File.WriteAllText(SettingsFilePath, serializedData);

            if (reload) {
                _mainWindow._settingsVal = LoadSettings() ?? CreateAndRepairSettingsFile();
                _mainWindow.OutPutBar("SettingsFile deleted or corrupted, values reset to standard");
            }
        }

        public bool ValidFilePathAndExtension(string path, string ext) {
            return File.Exists(path) && Path.GetExtension(path) == ext ? true : false;
        }

        public string FilePathProperSpacing(string filePath) {

            string returnString = string.Empty; 

            for(int i = 0; i < filePath.Length; i++) {
                if (filePath[i].ToString() == @"\" && returnString.Last().ToString() == @"\") { continue; }
                returnString += filePath[i].ToString();
            }

            return returnString; 
        }
    }
}
