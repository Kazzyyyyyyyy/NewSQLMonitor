using Accessibility;
using Microsoft.Win32;
using NewSQLMonitor;
using System;
using System.Collections.Generic;
using System.Data.Entity.Core.Mapping;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NewSQLMonitor {

    public partial class Settings : Page {


        //init
        SettingsData _settingsData = new SettingsData();
        SettingsData.SettingsValues _settingsVal;

        public MainWindow _mainWindow = Application.Current.MainWindow as MainWindow;

        public Settings() {

            InitializeComponent();

            _settingsVal = _mainWindow._settingsVal;

            SyncSettingsAndGUI();
        }


        //LoadData
        void SyncSettingsAndGUI() {

            GUI_programmLogFilePathTextBox.Text = _settingsVal.ProgrammLogFilePath;
            GUI_loadOnlyStringRowsComboBox.SelectedIndex = _settingsVal.LoadOnlyStringRows ? 0 : 1;
            GUI_autoUpdateComboBox.SelectedIndex = _settingsVal.AutoUpdate ? 0 : 1;
            GUI_autoUpdateIntervalTextBox.Text = _settingsVal.AutoUpdateInterval.ToString();
            GUI_showIDPrimaryKey.SelectedIndex = _settingsVal.ShowIDPrimaryKey ? 0 : 1;
            GUI_loadLastDBFilePath.SelectedIndex = _settingsVal.LoadLastDBFilePath ? 0 : 1;
            GUI_spaceRowsWithASCII.Text = _settingsVal.SpaceRowsWithASCII.Length != 0 ? _settingsVal.SpaceRowsWithASCII : "null";
            GUI_createAndSaveUndoFiles.SelectedIndex = _settingsVal.CreateAndSaveUndoFiles ? 0 : 1;

            if (MainWindow.DBFilePath.Length > 0) {
                LoadFileInfo(MainWindow.DBFilePath);
                GUI_dbFilePathTextBox.Text = @$"{MainWindow.DBFilePath}";
            }
            else {
                GUI_dbFilePathTextBox.Text = "null";
                ManageGUIOnLoad(0);
            }

            EventHappened = false;
        }


        //GUI
        void ManageGUIOnLoad(int mode) {

            switch (mode) {
                //file info
                case 0:
                    GUI_fileInfoListView.Visibility = Visibility.Hidden;
                    break;
                case 1:
                    GUI_fileInfoListView.Visibility = Visibility.Visible;
                    break;
            }
        }


        //SaveSettings
        public void SaveSettingsManager() {

            if (!EventHappened) {
                return;
            }

            var saveSettings = new SettingsData.SettingsValues();

            try {
                saveSettings.ProgrammLogFilePath = GUI_programmLogFilePathTextBox.Text;
                saveSettings.LastDBFilePath = GUI_dbFilePathTextBox.Text;
                saveSettings.LoadOnlyStringRows = bool.Parse(GUI_loadOnlyStringRowsComboBox.SelectionBoxItem.ToString().ToLower());
                saveSettings.AutoUpdate = bool.Parse(GUI_autoUpdateComboBox.SelectionBoxItem.ToString().ToLower());
                saveSettings.AutoUpdateInterval = double.TryParse(GUI_autoUpdateIntervalTextBox.Text, out double parsed) && parsed >= 0.5 && parsed <= double.MaxValue ? parsed : _settingsVal.AutoUpdateInterval;
                saveSettings.ShowIDPrimaryKey = bool.Parse(GUI_showIDPrimaryKey.SelectionBoxItem.ToString().ToLower());
                saveSettings.LoadLastDBFilePath = bool.Parse(GUI_loadLastDBFilePath.Text.ToString().ToLower());
                saveSettings.SpaceRowsWithASCII = GUI_spaceRowsWithASCII.Text != "null" ? GUI_spaceRowsWithASCII.Text : string.Empty;
                saveSettings.CreateAndSaveUndoFiles = bool.Parse(GUI_createAndSaveUndoFiles.Text.ToString().ToLower());

                _settingsData.SaveSettings(JsonSerializer.Serialize(saveSettings));

                _mainWindow.ReloadManager();
                EventHappened = false;
            }
            catch (Exception ex) {
                _mainWindow.ProgrammLog($"(Settings / SaveSettingsManager): {ex}", true);
            }
        }


        //Events
        bool EventHappened;
        void GUI_TextChanged(object sender = null, RoutedEventArgs e = null) { EventHappened = true; }
        void GUI_SelectionChanged(object sender, SelectionChangedEventArgs e) { EventHappened = true; }
        

        //FileManagement
        void GUI_ReloadDB_Button_Click(object sender, RoutedEventArgs e) {

            SaveSettingsManager();
            SyncSettingsAndGUI();

            _mainWindow.OutPutBar("SQLConnection reloaded");
        }

        void GUI_LoadFileInfo_Button_Click(object sender, RoutedEventArgs e) {

            string FileToLoadInfo = GUI_dbFilePathTextBox.Text;
            if (FileToLoadInfo.Length > 0 && FileToLoadInfo != MainWindow.DBFilePath && File.Exists(FileToLoadInfo)) {
                LoadFileInfo(FileToLoadInfo); 
            }
            else if (FileToLoadInfo.Length == 0) { 
                GUI_dbFilePathTextBox.Text = MainWindow.DBFilePath; 
            }

            EventHappened = false;
        }

        void LoadFileInfo(string filePath) {

            GUI_fileInfoListView.Items.Clear();
            FileInfo fileInfo = new(filePath);

            GUI_fileInfoListView.Items.Add($"DBLoadTime: { _mainWindow.DBLoadTimer.ElapsedMilliseconds.ToString() }ms");
            GUI_fileInfoListView.Items.Add($"Size: { (int.Parse(fileInfo.Length.ToString()) / 1024) }kb");
            GUI_fileInfoListView.Items.Add($"IsReadOnly: { fileInfo.IsReadOnly }");
            GUI_fileInfoListView.Items.Add($"FileType: { fileInfo.Attributes }");
            GUI_fileInfoListView.Items.Add($"LastWriteTime: { fileInfo.LastWriteTime.Day }/{ fileInfo.LastWriteTime.Month }/{ fileInfo.LastWriteTime.Year } ({ fileInfo.LastWriteTime.Hour }:{ fileInfo.LastWriteTime.Minute })");

            ManageGUIOnLoad(1);
        }


        //Functions
        void GUI_autoUpdateIntervalButtons_Click(object sender, RoutedEventArgs e) {
            if(double.TryParse(GUI_autoUpdateIntervalTextBox.Text, out double parsedDouble)) {
                if (sender == GUI_autoUpdateIntervalPlusButton && parsedDouble < double.MaxValue) {
                    parsedDouble+=0.5;
                    GUI_autoUpdateIntervalTextBox.Text = parsedDouble.ToString();
                }
                else if (sender == GUI_autoUpdateIntervalMinusButton && parsedDouble > 1) {
                    parsedDouble-=0.5;
                    GUI_autoUpdateIntervalTextBox.Text = parsedDouble.ToString();
                }
            }
        }

        private void GUI_changeFilePath_Click(object sender, RoutedEventArgs e) {

            OpenFileDialog fileSearch = new();
            fileSearch.Title = "SELECT NEW SQLite_db";

            if(fileSearch.ShowDialog() == true) {
                GUI_TextChanged();

                if ((Button)sender == GUI_changeDBFilePath) {
                    GUI_dbFilePathTextBox.Text = _settingsData.ValidFilePathAndExtension(fileSearch.FileName, ".db") && fileSearch.FileName != _settingsVal.LastDBFilePath ? _settingsData.FilePathProperSpacing(@$"{fileSearch.FileName}") : _settingsVal.LastDBFilePath; 
                    return;
                }

                GUI_programmLogFilePathTextBox.Text = _settingsData.ValidFilePathAndExtension(fileSearch.FileName, ".txt") && fileSearch.FileName != _settingsVal.ProgrammLogFilePath ? $@"{fileSearch.FileName}" : $@"{_settingsVal.ProgrammLogFilePath}";

            }
        }

        private void GUI_openLocalAppDataFolder_Button_Click(object sender, RoutedEventArgs e) {
            Process.Start("explorer.exe", $@"C:\Users\{Environment.UserName}\AppData\Local\SQLMonitor");
        }
    }
}