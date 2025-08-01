using NewSQLMonitor;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Data.SQLite;
using System.Diagnostics;
using System.Diagnostics.Eventing.Reader;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace NewSQLMonitor {

    public partial class MainWindow : Window {

        //init
        public Settings _settings;

        public SettingsData.SettingsValues _settingsVal = new();
        SettingsData _settingsData = new();

        public SQLiteConnection sqlConnect;
        public static string DBFilePath { get; set; } = string.Empty;

        public Stopwatch DBLoadTimer = new();

        //C:\Users\staer\Downloads\chinook\chinook.db
        //C:\Users\staer\Desktop\SqLiteTest\Neuer Ordner\taskSave.db
        //C:\Users\staer\Desktop\DCbot\UserPoints.db

        //C:\\Users\\staer\\source\\repos\\NewSQLMonitor\\NewSQLMonitor\\programmLog.txt

        //Main
        public MainWindow() {

            InitializeComponent();

            _settingsVal = _settingsData.LoadSettings() ?? _settingsData.CreateAndRepairSettingsFile();

            if(_settingsVal.LoadLastDBFilePath) { 
                DBFilePath = _settingsVal.LastDBFilePath; 
            }

            SQL_connectionManager();

            if (_settingsData.SettingsRestored) {
                OutPutBar("SettingsFile deleted or corrupted, values reset to standard");
            }

            //MakeCustomError();

            GUI_navigationComboBox.SelectedIndex = 0; //start page
        }

        void MakeCustomError() {
            try {

                var conTest = new SqlConnection("asdasd");
            }
            catch (Exception ex) {
                ProgrammLog($"(MainWindow / MakeCustomError): {ex}", true, "Custom error for testing");
            }
        }


        //LogFile handling
        public void ProgrammLog(string log, bool print = false, string customPrintMessage = "") {

            if(!File.Exists(_settingsVal.ProgrammLogFilePath)) { 
                File.Create(_settingsVal.ProgrammLogFilePath).Dispose(); 
            }

            if (print) { 
                OutPutBar(customPrintMessage != string.Empty ? customPrintMessage : log); 
            }

            using(var streamWriter = new StreamWriter(_settingsVal.ProgrammLogFilePath, true)) {
                streamWriter.WriteLine($"{ log }\n({ DateTime.Now.ToString("yyyy.MM.dd") } || { DateTime.Now.Hour }:{ DateTime.Now.Minute })\n");
            }
        }


        //Functions
        public void ReloadManager() { 
            
            if(DBFilePath != _settingsVal.LastDBFilePath) { 
                DBFilePath = _settingsVal.LastDBFilePath; 
            }
            
            SQL_connectionManager(); 
        }

        public void OutPutBar(string outp, bool rightOutPutBlock = false) {
            if (rightOutPutBlock) {
                GUI_outPutRightTextBlock.Text = outp;
                return;
            }
            GUI_outPutTextBlock.Text = outp;
        }


        //Events
        //Navigation
        public int NaviSelectedIndex;
        private void GUI_navigationComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {

            if(NaviSelectedIndex == 0) {
                GUI_outPutRightTextBlock.Text = string.Empty;
            }
            else if (NaviSelectedIndex == 2 && _settings != null) {
                _settings.SaveSettingsManager();
            }

            NavigationManager();
        }

        public void NavigationManager() {

            //Muss hier weil sonst 1000x error wenn ich DB switchen und ein Table geladen ist
            if (NaviSelectedIndex == 0) { 
                DBControlLogic.LoadedTable = string.Empty; 
            }

            switch (GUI_navigationComboBox.SelectedIndex) {
                case 0:
                    MainFrame.Navigate(new DBControl());
                    NaviSelectedIndex = 0;
                    break;

                case 1:
                    MainFrame.Navigate(_settings = new Settings());
                    NaviSelectedIndex = 2;
                    break;
            }
        }


        //SQL
        public void SQL_connectionManager() {

            if(!_settingsData.ValidFilePathAndExtension(DBFilePath, ".db")) { 
                OutPutBar("DB file path not valid");
                DBFilePath = string.Empty; 
                return; 
            }

            if (DBFilePath == string.Empty) { 
                OutPutBar("No DB connected"); 
                return; 
            }

            if (sqlConnect != null) { 
                sqlConnect.Close(); 
            }

            try {
                DBLoadTimer.Start();

                sqlConnect = new SQLiteConnection($"Data Source={DBFilePath}");
                sqlConnect.Open();

                DBLoadTimer.Stop();

                OutPutBar("SQL connected");
            }
            catch (Exception ex) { 
                ProgrammLog($"(MainWindow / SQL_connectionManager): {ex}", true);
            }
        }
    }
}