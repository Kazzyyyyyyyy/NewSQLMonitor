using Accessibility;
using Microsoft.Win32;
using NewSQLMonitor;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity.Core.Mapping;
using System.Data.Entity.Core.Metadata.Edm;
using System.Data.SQLite;
using System.Diagnostics;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Net.Http.Headers;
using System.Reflection.Metadata.Ecma335;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace NewSQLMonitor {

    public partial class DBControl : Page {

        //Init main
        static MainWindow _mainWindow = Application.Current.MainWindow as MainWindow;
        static DBControlLogic _dbCLogic;
        public static DispatcherTimer _timer;

        public DBControl() {

            InitializeComponent();

            _dbCLogic = new(this, _mainWindow);

            this.SizeChanged += DBControlWindow_SizeChanged;
            this.KeyDown += KeyDown_Event;
            this.Loaded += MainWindow_Loaded; //CHATGPT

            if (MainWindow.DBFilePath != string.Empty) {
                LoadSettings();
                _dbCLogic.DBFilePathBackupInit();
                ManageAutoUpdater();
                _dbCLogic.GetTables();
            }

        }


        //Functions
        static bool AutoUpdate;
        static double AutoUpdateInterval;
        public void LoadSettings() {

            AutoUpdate = _mainWindow._settingsVal.AutoUpdate;
            AutoUpdateInterval = _mainWindow._settingsVal.AutoUpdateInterval;

            _dbCLogic.DBFilePathBackupInit(); 

            foreach(string read in _dbCLogic.DBFilePathBackup) {
                if(File.Exists(read)) {
                    _dbCLogic.UndoFileLoadNum++;
                }
            }

            if(_dbCLogic.UndoFileLoadNum > 1) {
                _dbCLogic.UndoFileLoadNum--;
            }
        }

        static int TableSelectedLine = -1, RowSelectedLine = -1;
        void ManageAutoUpdater() {

            _timer = new();
            _timer.Interval = TimeSpan.FromSeconds(AutoUpdateInterval);

            _timer.Tick += (sender, e) => AutoReloadAll();

            if (AutoUpdate && !_timer.IsEnabled) { 
                _timer.Start(); 
                return; 
            }

            _timer.Stop();
        }


        void AutoReloadAll() {

            TableSelectedLine = GUI_tableListBox.SelectedIndex;
            RowSelectedLine = GUI_rowListBox.SelectedIndex;

            _dbCLogic.GetTables();
            if (DBControlLogic.LoadedTable == string.Empty) { 
                SyncSelectedListLines(TableSelectedLine, RowSelectedLine);
                return; 
            }

            _dbCLogic.LoadTableRows();

            SyncSelectedListLines(TableSelectedLine, RowSelectedLine);
        }

        void SyncSelectedListLines(int tableListIndex, int rowListIndex) {
            if (tableListIndex > -1) {
                GUI_tableListBox.SelectedIndex = tableListIndex;
            }
            if (rowListIndex > -1) {
                GUI_rowListBox.SelectedIndex = rowListIndex;
            }
        }

        //GUI events
        void MainWindow_Loaded(object sender, RoutedEventArgs e) {

            MonitorScrollBar(GUI_rowListBox);
            MonitorScrollBar(GUI_tableListBox);
        }

        static void MonitorScrollBar(ListBox listBox) {

            var scrollViewer = GetScrollViewer(listBox);
            if (scrollViewer != null) {
                DependencyPropertyDescriptor
                    .FromProperty(ScrollViewer.ComputedVerticalScrollBarVisibilityProperty, typeof(ScrollViewer))
                    .AddValueChanged(scrollViewer, (s, e) => VerticalScrollBarChanged(s, e, listBox));
            }
        }
        
        static void VerticalScrollBarChanged(object sender, EventArgs e, ListBox listBox) {

            var scrollViewer = sender as ScrollViewer;
            if (scrollViewer != null) {
                if (scrollViewer.ComputedVerticalScrollBarVisibility == Visibility.Visible) {
                    listBox.HorizontalAlignment = HorizontalAlignment.Stretch;
                }
                else {
                    listBox.HorizontalAlignment = HorizontalAlignment.Left;
                }
            }
        }

        static ScrollViewer GetScrollViewer(DependencyObject depObj) {

            if (depObj is ScrollViewer) {
                return (ScrollViewer)depObj;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++) {
                var child = VisualTreeHelper.GetChild(depObj, i);
                var result = GetScrollViewer(child);
                if (result != null) { return result; }
            }
            return null;
        }

        void DBControlWindow_SizeChanged(object sender, SizeChangedEventArgs e) {

            double newMaxHeight = this.ActualHeight - 135;

            GUI_rowListBox.MaxHeight = newMaxHeight;
            GUI_tableListBox.MaxHeight = newMaxHeight;
        }

        //Events
        void KeyDown_Event(object sender, KeyEventArgs e) {

            if (!(Keyboard.IsKeyDown(Key.LeftCtrl) && e.Key == Key.C)) { return; }

            if (GUI_tableListBox.SelectedIndex != -1) {
                Clipboard.SetText(GUI_tableListBox.SelectedItem.ToString());
                return;
            }
            else if (GUI_rowListBox.SelectedIndex != -1) {
                Clipboard.SetText(GUI_rowListBox.SelectedItem.ToString());
                return;
            }
        }

        void GUI_confirmButton_Click(object sender, RoutedEventArgs e) {

            if (GUI_tableCMDComboBox.SelectedIndex != -1) {
                _dbCLogic.TableCMDManager();
                return;
            }
            else if (GUI_rowCMDComboBox.SelectedIndex != -1) {
                _dbCLogic.RowCMDManager();
                return;
            }
        }

        public static List<string> SQL_dataTypes = new() { "TEXT", "INTEGER", "REAL", "BLOB", "DATE", "TIME", "BOOLEAN", "," };
        public static List<string> SQL_constraints = new() { "NOT NULL", "PRIMARY KEY", "AUTOINCREMENT", "UNIQUE", "FOREIGN KEY", "CHECK" };
        static bool GUI_changeSelection = true;
        void GUI_tableCMDComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {

            if (_mainWindow.sqlConnect == null) {
                _mainWindow.OutPutBar("Load a DB before using commands");
                GUI_tableCMDComboBox.SelectedIndex = -1;
                return;
            }

            ListBoxSwitchEvent(null, GUI_rowCMDComboBox, ref GUI_changeSelection);

            _mainWindow.GUI_outPutRightTextBlock.Text = string.Empty;

            if (GUI_tableCMDComboBox.SelectedIndex == 1) {

                _dbCLogic.ManageGUI(7);
                GUI_stringBuilderOutPut.Text = _dbCLogic.CreateTableString[1];

                foreach(string read in SQL_dataTypes) { 
                    GUI_dataTypeAndTableInfoListBox.Items.Add(read);
                }
                
                foreach(string read in SQL_constraints) { 
                    GUI_constraintsListBox.Items.Add(read); 
                }

                return;
            }
            else if(GUI_tableCMDComboBox.SelectedIndex == 5) {
                _mainWindow.GUI_outPutRightTextBlock.Text = "Undos left: " + _dbCLogic.UndoFileLoadNum.ToString();
            }

            _dbCLogic.ManageGUI(8);
        }

        void GUI_rowCMDComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {

            if (DBControlLogic.LoadedTable == string.Empty || _mainWindow.sqlConnect == null) {
                _mainWindow.OutPutBar("Load a table first");
                GUI_rowCMDComboBox.SelectedIndex = -1;
                return;
            }

            ListBoxSwitchEvent(null, GUI_tableCMDComboBox, ref GUI_changeSelection);

            if (GUI_rowCMDComboBox.SelectedIndex == 0) {

                _dbCLogic.AddRowString = $"INSERT INTO {DBControlLogic.LoadedTable}() VALUES ()";
                GUI_stringBuilderOutPut.Text = _dbCLogic.AddRowString;

                _dbCLogic.ManageGUI(7); 
                GUI_constraintsListBox.Visibility = Visibility.Hidden;

                foreach(string read in GUI_tableInfoListView.Items) {
                    GUI_dataTypeAndTableInfoListBox.Items.Add(read);
                }

                return;
            }
            else if(GUI_rowCMDComboBox.SelectedIndex == 1) {
                _dbCLogic.EditRowString[1] = _dbCLogic.EditRowString[1] += DBControlLogic.LoadedTable + " SET ";
                GUI_stringBuilderOutPut.Text = _dbCLogic.EditRowString[1];

                _dbCLogic.ManageGUI(7);
                GUI_constraintsListBox.Visibility = Visibility.Hidden;

                foreach (string read in GUI_tableInfoListView.Items) {
                    GUI_dataTypeAndTableInfoListBox.Items.Add(read);
                }

                return; 
            }

            _dbCLogic.ManageGUI(8);
        }

        void GUI_stringBuilderConfirmButton_Click(object sender, RoutedEventArgs e) {

            if (GUI_rowCMDComboBox.SelectedIndex == 0) {
                _dbCLogic.AddRowStringBuilder();

                GUI_inputTextBox.Clear();
                return;
            }
            else if(GUI_rowCMDComboBox.SelectedIndex == 1) {
                _dbCLogic.EditRowStringBuilder();
                
                GUI_inputTextBox.Clear();
                return; 
            }
            else if (GUI_tableCMDComboBox.SelectedIndex == 1) {
                _dbCLogic.CreateTableStringBuilder();

                GUI_inputTextBox.Clear();
                return;
            }
        }

        void GUI_stringBuilderUndoButton_Click(object sender, RoutedEventArgs e) {

            if (GUI_rowCMDComboBox.SelectedIndex == 0) {
                _dbCLogic.AddRowStringParts[0] = _dbCLogic.AddRowStringParts[2];
                _dbCLogic.AddRowStringParts[1] = _dbCLogic.AddRowStringParts[3];

                GUI_stringBuilderOutPut.Text = $"INSERT INTO {DBControlLogic.LoadedTable}({_dbCLogic.AddRowStringParts[0]}) VALUES ({_dbCLogic.AddRowStringParts[1]})";
                return;
            }
            else if(GUI_rowCMDComboBox.SelectedIndex == 1) {
                _dbCLogic.EditRowString[1] = _dbCLogic.EditRowString[0];
                GUI_stringBuilderOutPut.Text = _dbCLogic.EditRowString[1];
                return;
            }
            else if (GUI_tableCMDComboBox.SelectedIndex == 1) {
                _dbCLogic.CreateTableString[1] = _dbCLogic.CreateTableString[0];
                GUI_stringBuilderOutPut.Text = _dbCLogic.CreateTableString[1];
                return;
            }
            
        }

        void GUI_stringBuilderDeleteButton_Click(object sender, RoutedEventArgs e) {

            if (GUI_rowCMDComboBox.SelectedIndex == 0) {
                _dbCLogic.AddRowString = $"INSERT INTO {DBControlLogic.LoadedTable} () VALUES ('')";
                GUI_stringBuilderOutPut.Text = _dbCLogic.AddRowString;
                _dbCLogic.AddRowStringParts = new() { string.Empty, string.Empty, string.Empty, string.Empty };
                return;
            }
            else if (GUI_rowCMDComboBox.SelectedIndex == 1) {
                _dbCLogic.EditRowString[0] = string.Empty;
                _dbCLogic.EditRowString[1] = $"UPDATE {DBControlLogic.LoadedTable} SET";
                GUI_stringBuilderOutPut.Text = _dbCLogic.EditRowString[1];
                return;
            }
            else if (GUI_tableCMDComboBox.SelectedIndex == 1) {
                _dbCLogic.CreateTableString.Clear();
                GUI_stringBuilderOutPut.Text = "CREATE TABLE IF NOT EXISTS ";
                return;
            }
        }

        static bool GUI_changeSelectionListBoxes = true;
        void GUI_dataTypeAndTableInfoListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            ListBoxSwitchEvent(GUI_constraintsListBox, null, ref GUI_changeSelectionListBoxes);
        }

        void GUI_constraintsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            ListBoxSwitchEvent(GUI_dataTypeAndTableInfoListBox, null, ref GUI_changeSelectionListBoxes);
        }

        static bool GUI_tableAndRowListBoxSelectionChanged = true;
        void GUI_tableListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            _mainWindow.GUI_outPutRightTextBlock.Text = string.Empty;
            ListBoxSwitchEvent(GUI_rowListBox, null, ref GUI_tableAndRowListBoxSelectionChanged);
        }

        void GUI_rowListBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {

            ListBoxSwitchEvent(GUI_tableListBox, null, ref GUI_tableAndRowListBoxSelectionChanged);

            if (GUI_rowListBox.SelectedIndex == -1) { return; }

            if (GUI_rowListBox.SelectedItem.ToString().Split(':').Last() == _mainWindow._settingsVal.SpaceRowsWithASCII) {
                _mainWindow.OutPutBar("", true);
                return;
            }
            else if (_dbCLogic.LoadedTableAttributes.Count == 1) {
                _mainWindow.OutPutBar($"{_dbCLogic.LoadedTableAttributes[0]} | { _dbCLogic.GetCorrectLineIndex(GUI_rowListBox, GUI_rowListBox.SelectedIndex) }", true);
                return;
            }
            else if (GUI_rowListBox.SelectedIndex <= _dbCLogic.LoadedTableAttributes.Count) {
                _mainWindow.OutPutBar($"{_dbCLogic.LoadedTableAttributes[GUI_rowListBox.SelectedIndex]} | { _dbCLogic.GetCorrectLineIndex(GUI_rowListBox, GUI_rowListBox.SelectedIndex) }", true);
                return;
            }

            int endIndex = -1;
            for (int i = GUI_rowListBox.SelectedIndex; i < GUI_rowListBox.Items.Count; i--, endIndex++) {
                if (GUI_rowListBox.Items[i].ToString().Split(':').Last() == _mainWindow._settingsVal.SpaceRowsWithASCII) {
                    _mainWindow.OutPutBar($"{_dbCLogic.LoadedTableAttributes[endIndex]} | { _dbCLogic.GetCorrectLineIndex(GUI_rowListBox, GUI_rowListBox.SelectedIndex) }", true);
                    return;
                }
            }
        }

        static void ListBoxSwitchEvent(ListBox listBox, ComboBox comboBox, ref bool switchBool) { 
            
            if(listBox == null) {
                if (switchBool && comboBox.SelectedIndex != -1) {
                    switchBool = false;
                    comboBox.SelectedIndex = -1;
                }
                else {
                    switchBool = true;
                }
                return; 
            }

            if (switchBool && listBox.SelectedIndex != -1) {
                switchBool = false;
                listBox.SelectedIndex = -1;
            }
            else { 
                switchBool = true; 
            }
        }
    }


    public class DBControlLogic {

        //init 2
        static DBControl _dbControl;
        static MainWindow _mainWindow;

        public DBControlLogic(DBControl cont, MainWindow mw) {
            _dbControl = cont;
            _mainWindow = mw;
        }


        //SQL strings
        static string SQL_getAllTablesString = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'"; 
        static string SQL_readAllRowsFromTableString(string fromTable) { return $"SELECT * FROM {fromTable}"; }
        static string SQL_dropTableString(string tableName) { return $"DROP TABLE {tableName}"; }
        static string SQL_renameTableString(string tableName, string newName) { return $"ALTER TABLE {tableName} RENAME TO {newName}"; }
        static string SQL_tableDataString(string tableName) { return $"SELECT sql FROM sqlite_master WHERE type = 'table' AND name = '{tableName}'"; }
        static string SQL_getTableInfoString(string name) { return $"PRAGMA table_info ({name})"; }
        static string SQL_removeAllRowsString(string tableName) { return $"DELETE FROM {tableName}"; }
        static string SQL_removeRowString(string tableName, string rowid, string PrimaryKeyName = "") {
            if (PrimaryKeyName != string.Empty) {
                return $"DELETE FROM {tableName} WHERE {PrimaryKeyName} = '{rowid}'";
            }

            return $"DELETE FROM {tableName} WHERE rowid = {long.Parse(rowid)}";
        }
        
        //GUI
        public void ManageGUI(int action) {

            switch (action) {

                //Tables
                case 1:
                    _dbControl.GUI_tableListBox.Visibility = Visibility.Hidden;
                    _dbControl.GUI_tableStandardTextBlock.Visibility = Visibility.Visible;
                    break;
                case 2:
                    _dbControl.GUI_tableStandardTextBlock.Visibility = Visibility.Hidden;
                    _dbControl.GUI_tableListBox.Visibility = Visibility.Visible;
                    break;

                //Rows
                case 3:
                    _dbControl.GUI_rowListBox.Visibility = Visibility.Hidden;
                    _dbControl.GUI_rowStandardTextBlock.Visibility = Visibility.Visible;
                    _dbControl.GUI_rowNumberTextBlock.Text = string.Empty;
                    break;
                case 4:
                    _dbControl.GUI_rowStandardTextBlock.Visibility = Visibility.Hidden;
                    _dbControl.GUI_rowListBox.Visibility = Visibility.Visible;
                    break;

                //TableInfo
                case 5:
                    _dbControl.GUI_tableInfoGrid.Visibility = Visibility.Visible;
                    _dbControl.GUI_tableInfoHeadLine.Visibility = Visibility.Visible;
                    _dbControl.GUI_tableInfoLoadedTable.Visibility = Visibility.Visible;
                    break;
                case 6:
                    _dbControl.GUI_tableInfoGrid.Visibility = Visibility.Hidden;
                    _dbControl.GUI_tableInfoHeadLine.Visibility = Visibility.Hidden;
                    _dbControl.GUI_tableInfoLoadedTable.Visibility = Visibility.Hidden;
                    _dbControl.GUI_rowNumberTextBlock.Text = string.Empty;
                    break;

                //StringBuilder
                case 7:
                    _dbControl.GUI_stringBuilderHeadlineTextBlock.Visibility = Visibility.Visible;
                    _dbControl.GUI_stringBuilderGrid.Visibility = Visibility.Visible;
                    _dbControl.GUI_stringBuilderConfirmButton.Visibility = Visibility.Visible;
                    _dbControl.GUI_stringBuilderUndoButton.Visibility = Visibility.Visible;
                    _dbControl.GUI_stringBuilderDeleteButton.Visibility = Visibility.Visible;
                    _dbControl.GUI_stringBuilderOutPutGrid.Visibility = Visibility.Visible;
                    _dbControl.GUI_constraintsListBox.Visibility = Visibility.Visible;
                    break;
                case 8:
                    _dbControl.GUI_dataTypeAndTableInfoListBox.Items.Clear();
                    _dbControl.GUI_constraintsListBox.Items.Clear();

                    _dbControl.GUI_stringBuilderHeadlineTextBlock.Visibility = Visibility.Hidden;
                    _dbControl.GUI_stringBuilderGrid.Visibility = Visibility.Hidden;
                    _dbControl.GUI_stringBuilderConfirmButton.Visibility = Visibility.Hidden;
                    _dbControl.GUI_stringBuilderUndoButton.Visibility = Visibility.Hidden;
                    _dbControl.GUI_stringBuilderDeleteButton.Visibility = Visibility.Hidden;
                    _dbControl.GUI_stringBuilderOutPutGrid.Visibility = Visibility.Hidden;
                    break;

            }
        }


        //Rows
        public void RowCMDManager() {

            if (LoadedTable == string.Empty) { 
                _mainWindow.OutPutBar("Load a table first"); 
                return;
            }

            switch (_dbControl.GUI_rowCMDComboBox.SelectedIndex) {
                case 0:
                    AddRow();
                    break;

                case 1:
                    EditRow();
                    break; 

                case 2:
                    RemoveRow();
                    break;

                case 3:
                    FindListLine(_dbControl.GUI_rowListBox);
                    ReloadRows = false;
                    break;
            }

            GetTables();

            if (ReloadRows) { 
                LoadTableRows(); 
            }

            ReloadRows = true;
        }


        public List<string> EditRowString = new() { string.Empty, "UPDATE " };
        public void EditRowStringBuilder() {

            if(!_mainWindow._settingsVal.ShowIDPrimaryKey) { _mainWindow.OutPutBar("Primary key must be present"); return; }

            string inp = _dbControl.GUI_inputTextBox.Text;

            EditRowString[0] = EditRowString[1];

            if (_dbControl.GUI_dataTypeAndTableInfoListBox.SelectedIndex != -1 && EditRowString[1].Length == $"UPDATE {LoadedTable} SET ".Length && !EditRowString[1].Contains(TablePragmaNames[_dbControl.GUI_dataTypeAndTableInfoListBox.SelectedIndex])) {
                EditRowString[1] += TablePragmaNames[_dbControl.GUI_dataTypeAndTableInfoListBox.SelectedIndex];
            }
            else if (_dbControl.GUI_dataTypeAndTableInfoListBox.SelectedIndex != -1 && EditRowString[1].Length > $"UPDATE {LoadedTable} SET ".Length && !EditRowString[1].Contains(TablePragmaNames[_dbControl.GUI_dataTypeAndTableInfoListBox.SelectedIndex])) {
                EditRowString[1] += $", {TablePragmaNames[_dbControl.GUI_dataTypeAndTableInfoListBox.SelectedIndex]}";
            }

            if (inp.Length > 0) {
                EditRowString[1] += $" = '{inp}'";
            }

            if(_dbControl.GUI_rowListBox.SelectedIndex != -1) {
                EditRowString[1] += $" WHERE {PrimaryKeyName} = '{_dbControl.GUI_rowListBox.SelectedItem.ToString()}'"; 
            }

            _dbControl.GUI_stringBuilderOutPut.Text = EditRowString[1]; 
        }

        void EditRow() {
            
            if (EditRowString[1] != _dbControl.GUI_stringBuilderOutPut.Text) {
                EditRowString[1] = _dbControl.GUI_stringBuilderOutPut.Text;
            }

            try {
                UndoManager();
                using (var cmd = new SQLiteCommand(EditRowString[1], _mainWindow.sqlConnect)) {
                    cmd.ExecuteNonQuery();
                    _mainWindow.OutPutBar("Row updated");
                    ResetAfterStringBuilderUse();
                }
            }
            catch (Exception ex) { _mainWindow.ProgrammLog($"(DBControl / AddRow) {ex}", true); }
        }

        public string AddRowString;
        public List<string> AddRowStringParts = new() { string.Empty, string.Empty, string.Empty, string.Empty };
        public static List<string> TablePragmaNames = new();
        public List<string> LoadedTableAttributes = new();
        public void AddRowStringBuilder() {

            string inp = _dbControl.GUI_inputTextBox.Text;

            AddRowStringParts[2] = AddRowStringParts[0]; 
            AddRowStringParts[3] = AddRowStringParts[1];

            if (_dbControl.GUI_dataTypeAndTableInfoListBox.SelectedIndex != -1 && AddRowStringParts[0].Length == 0 && !AddRowStringParts[0].Contains(TablePragmaNames[_dbControl.GUI_dataTypeAndTableInfoListBox.SelectedIndex])) {
                AddRowStringParts[0] += TablePragmaNames[_dbControl.GUI_dataTypeAndTableInfoListBox.SelectedIndex];
            }
            else if (_dbControl.GUI_dataTypeAndTableInfoListBox.SelectedIndex != -1 && AddRowStringParts[0].Length > 0 && !AddRowStringParts[0].Contains(TablePragmaNames[_dbControl.GUI_dataTypeAndTableInfoListBox.SelectedIndex])) {
                AddRowStringParts[0] += $", {TablePragmaNames[_dbControl.GUI_dataTypeAndTableInfoListBox.SelectedIndex]}";
            }

            if (inp.Length > 0 && AddRowStringParts[1].Length == 0) { 
                AddRowStringParts[1] += $"'{inp}'";
            }
            else if (inp.Length > 0 && AddRowStringParts[1].Length > 0) {
                AddRowStringParts[1] += $", '{inp}'";
            }

            AddRowString = $"INSERT INTO {LoadedTable}({AddRowStringParts[0]}) VALUES ({AddRowStringParts[1]})";
            _dbControl.GUI_stringBuilderOutPut.Text = AddRowString;

        }

        void AddRow() {

            if (AddRowString != _dbControl.GUI_stringBuilderOutPut.Text) {
                AddRowString = _dbControl.GUI_stringBuilderOutPut.Text;
            }

            try {
                UndoManager();
                using (var cmd = new SQLiteCommand(AddRowString, _mainWindow.sqlConnect)) {
                    cmd.ExecuteNonQuery();
                    _mainWindow.OutPutBar("Row added");
                    ResetAfterStringBuilderUse();
                }
            }
            catch (Exception ex) { _mainWindow.ProgrammLog($"(DBControl / AddRow) {ex}", true); }
        }

        static List<string> RowRemoveValuesList = new();
        static bool ReloadRows = true;
        void RemoveRow() {

            try {
                if (_dbControl.GUI_inputTextBox.Text == "(all)" && _dbControl.GUI_tableListBox.SelectedIndex != -1) {
                    UndoManager();
                    using (var cmd = new SQLiteCommand(SQL_removeAllRowsString(_dbControl.GUI_tableListBox.SelectedItem.ToString()), _mainWindow.sqlConnect)) {
                        cmd.ExecuteNonQuery();
                        ReloadRows = false;
                        _mainWindow.OutPutBar($"All rows from table '{_dbControl.GUI_tableListBox.SelectedItem}' removed");
                        return;
                    }
                }
            }
            catch (Exception ex) {
                _mainWindow.ProgrammLog($"(DBControl / RemoveRow) {ex}", true);
                return; 
            }

            if (_dbControl.GUI_rowListBox.SelectedIndex == -1 || _dbControl.GUI_rowListBox.SelectedItem.ToString().Split(":").Last() == _mainWindow._settingsVal.SpaceRowsWithASCII) {
                _mainWindow.OutPutBar("Select a valid row to remove"); return;
            }

            try {

                UndoManager();
                using (var cmd = new SQLiteCommand(SQL_removeRowString(LoadedTable, RowRemoveValuesList[_dbControl.GUI_rowListBox.SelectedIndex], PrimaryKeyName), _mainWindow.sqlConnect)) {
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { 
                _mainWindow.ProgrammLog($"(DBControl / RemoveRow) {ex}", true); 
            }
        }


        //Tables
        public void TableCMDManager() {

            switch (_dbControl.GUI_tableCMDComboBox.SelectedIndex) {

                case 0:
                    if (_dbControl.GUI_tableListBox.SelectedIndex == -1) { _mainWindow.OutPutBar("Select a table to load"); return; }
                    if (_dbControl.GUI_tableListBox.SelectedItem.ToString() == LoadedTable) { _mainWindow.OutPutBar($"Table '{LoadedTable}' already loaded"); return; }
                    LoadedTable = _dbControl.GUI_tableListBox.SelectedItem.ToString();
                    LoadTableRows(true, true);
                    return;

                case 1:
                    CreateTable();
                    break;

                case 2:

                    if (_dbControl.GUI_tableListBox.SelectedIndex == -1) { RemoveTable(); }
                    else { RemoveTable(_dbControl.GUI_tableListBox.SelectedItem.ToString()); }

                    GetTables();

                    if (LoadedTable != string.Empty) { LoadTableRows(); return; }

                    _dbControl.GUI_rowListBox.Items.Clear();
                    ManageGUI(3); ManageGUI(6);

                    return;

                case 3:
                    if (_dbControl.GUI_tableListBox.SelectedIndex == -1) { _mainWindow.OutPutBar("Select a table to rename"); return; }
                    RenameTable(_dbControl.GUI_tableListBox.SelectedItem.ToString());
                    break;

                case 4:
                    FindListLine(_dbControl.GUI_tableListBox);
                    break;

                case 5:
                    UndoManager(false);
                    break;
            }

            GetTables();
            if (LoadedTable != string.Empty) { LoadTableRows(); }
        } 

        void CreateTableInfoLoadedTableString(string txt) {

            double GUI_columnWidth = _dbControl.GUI_columnOne.ActualWidth;

            _dbControl.GUI_tableInfoLoadedTable.ToolTip = null;

            _dbControl.GUI_tableInfoLoadedTable.Text = $"'{txt}'";
            _dbControl.GUI_tableInfoLoadedTable.UpdateLayout();

            if (_dbControl.GUI_tableInfoLoadedTable.ActualWidth + 90 <= GUI_columnWidth) {
                return;
            }

            for(int i = txt.Length; i > 0; i--) {
                if (_dbControl.GUI_tableInfoLoadedTable.ActualWidth + 90 > GUI_columnWidth) {
                    
                    txt = txt.Remove(txt.Length - 1);

                    _dbControl.GUI_tableInfoLoadedTable.Text = $"'{txt}'";
                    _dbControl.GUI_tableInfoLoadedTable.UpdateLayout();
                    continue;
                }

                txt = txt.Remove(txt.Length - 1);
                txt = txt.Remove(txt.Length - 2);
                txt = txt.Remove(txt.Length - 3);
                txt += "....";

                _dbControl.GUI_tableInfoLoadedTable.Text = $"'{txt}'";
                break; 
            }

            _dbControl.GUI_tableInfoLoadedTable.ToolTip = LoadedTable;

        }

        public static string LoadedTable { get; set; } = "";
        static int rowNum;
        static bool UseRowID = false;
        static int PrimaryKeyAt = -1;
        static string PrimaryKeyName = string.Empty;
        static bool GotPrimaryKey = false;

        public void LoadTableRows(bool PrintLoadedMSG = false, bool LoadInfo = false) {

            if (!_dbControl.GUI_tableListBox.Items.Contains(LoadedTable)) {
                _dbControl.GUI_tableInfoLoadedTable.Text += " (DELETED)";
                LoadedTable = string.Empty;
                return;
            }

            rowNum = 0;
            try {
                using (var cmd = new SQLiteCommand(SQL_readAllRowsFromTableString(LoadedTable), _mainWindow.sqlConnect)) {
                    using (var reader = cmd.ExecuteReader()) {

                        bool removeLastLine = false;
                        _dbControl.GUI_rowListBox.Items.Clear();

                        if (LoadInfo) {
                            RowRemoveValuesList.Clear();
                            LoadedTableAttributes.Clear();
                        }

                        CreateTableInfoLoadedTableString(LoadedTable);

                        if (!reader.HasRows) {
                            ManageGUI(3);
                            LoadTableInfo(LoadedTable);
                            if (PrintLoadedMSG) { _mainWindow.OutPutBar($"Table '{LoadedTable}' and TableInfo loaded"); }
                            return;
                        }

                        bool transferTableAttributeLists = true;
                        if (LoadInfo) { 
                            LoadTableInfo(LoadedTable); 
                        }

                        ManageGUI(4);
                        while (reader.Read()) {
                            rowNum++;

                            int loadedRowNum = 0;
                            for (int i = 0; i < reader.FieldCount; i++) {
                                object value = reader.GetValue(i);

                                if (value is not string && _mainWindow._settingsVal.LoadOnlyStringRows) continue; 
                                if (i == PrimaryKeyAt && !_mainWindow._settingsVal.ShowIDPrimaryKey) continue; 

                                if (LoadInfo) RowRemoveValuesList.Add(reader.GetValue(UseRowID ? 0 : PrimaryKeyAt).ToString());
                                

                                if (transferTableAttributeLists && LoadInfo) {
                                    LoadedTableAttributes.Add(TablePragmaNames[i]);
                                    if (i == reader.FieldCount - 1) {
                                        transferTableAttributeLists = false;
                                    }
                                }

                                _dbControl.GUI_rowListBox.Items.Add(value);
                                loadedRowNum++;

                                if (i == reader.FieldCount - 1 && loadedRowNum > 1 ) {
                                    _dbControl.GUI_rowListBox.Items.Add(_mainWindow._settingsVal.SpaceRowsWithASCII);
                                    RowRemoveValuesList.Add(string.Empty);
                                    removeLastLine = true;
                                }
                            }
                        }

                        if(_dbControl.GUI_rowListBox.Items.Count == 0) {
                            ManageGUI(3);
                            _dbControl.GUI_rowStandardTextBlock.Text = "Disable filter funktions in settings to see";
                        }

                        if (removeLastLine) {
                            _dbControl.GUI_rowListBox.Items.RemoveAt(_dbControl.GUI_rowListBox.Items.Count - 1);
                        }
                    }
                }

                _dbControl.GUI_rowNumberTextBlock.Text = $"({rowNum}x)";
                if (rowNum == 0) { 
                    ManageGUI(3);
                }
            }
            catch (Exception ex) { 
                _mainWindow.ProgrammLog($"(DBControl / SQL_loadTableRows) {ex}", true); 
                return;
            }

            if (PrintLoadedMSG) { 
                _mainWindow.OutPutBar($"Table '{LoadedTable}' and TableInfo loaded"); 
            }
        }

        public void LoadTableInfo(string tableName) {

            try {

                string createTableSQL = string.Empty;
                using (var cmdMaster = new SQLiteCommand(SQL_tableDataString(tableName), _mainWindow.sqlConnect)) {
                    using (var readerMaster = cmdMaster.ExecuteReader()) {
                        if (readerMaster.Read()) {
                            createTableSQL = readerMaster.GetValue(0).ToString();
                        }
                    }
                }

                using (var cmd = new SQLiteCommand(SQL_getTableInfoString(tableName), _mainWindow.sqlConnect)) {
                    using (var reader = cmd.ExecuteReader()) {
                        UseRowID = false;
                        GotPrimaryKey = false;
                        PrimaryKeyName = string.Empty;
                        PrimaryKeyAt = -1;

                        _dbControl.GUI_tableInfoListView.Items.Clear();
                        TablePragmaNames.Clear(); 
                        ManageGUI(5);

                        int run = -1;
                        while (reader.Read()) {

                            string columnName = reader["name"].ToString();
                            string columnType = reader["type"].ToString();
                            string isNotNull = (reader["notnull"].ToString() == "1") ? "NOT NULL" : "NULL allowed";
                            string isPrimaryKey = (reader["pk"].ToString() == "1") ? "PRIMARY KEY" : string.Empty;
                            string isAutoIncrement = (columnType == "INTEGER" && isPrimaryKey == "PRIMARY KEY" && createTableSQL.Contains("AUTOINCREMENT"))
                                ? " | AUTOINCREMENT" : string.Empty;

                            TablePragmaNames.Add(columnName);
                            run++;
                            if (isPrimaryKey.Length > 0) {
                                _dbControl.GUI_tableInfoListView.Items.Add($"{columnName} | {columnType} | {isPrimaryKey}{isAutoIncrement}");

                                if (run == 0 && columnType == "INTEGER") {
                                    UseRowID = true;
                                }
                                else if (!GotPrimaryKey && !UseRowID) {
                                    GotPrimaryKey = true;
                                }

                                PrimaryKeyName = columnName;
                                PrimaryKeyAt = run;

                                continue;
                            }

                            _dbControl.GUI_tableInfoListView.Items.Add($"{columnName} | {columnType} | {isNotNull}");
                        }
                    }
                }
            }
            catch (Exception ex) { 
                _mainWindow.ProgrammLog($"(DBControl / SQL_loadTableInfo)  {ex}", true);
            }
        }

        void RenameTable(string tableName) {

            if (string.IsNullOrWhiteSpace(_dbControl.GUI_inputTextBox.Text) || _dbControl.GUI_inputTextBox.Text.Contains(' ')) {
                _mainWindow.OutPutBar("Enter a name to alter to / Only names without spaces");
                return;
            }

            string newName = _dbControl.GUI_inputTextBox.Text;

            try {
                UndoManager();
                using (var cmd = new SQLiteCommand(SQL_renameTableString(tableName, newName), _mainWindow.sqlConnect)) {
                    cmd.ExecuteNonQuery();
                    _mainWindow.OutPutBar($"Table '{tableName}' renamed to '{newName}'");
                }
            }
            catch (Exception ex) { 
                _mainWindow.ProgrammLog($"(DBControl / SQL_renameTable) {ex}", true); 
            }

            if (LoadedTable == tableName) { LoadedTable = newName; }
        }

        void RemoveTable(string tableName = "") {

            if (string.IsNullOrWhiteSpace(tableName) && _dbControl.GUI_inputTextBox.Text != "(all)") { 
                _mainWindow.OutPutBar("Select a table to remove"); 
                return;
            }

            UndoManager();

            if (_dbControl.GUI_inputTextBox.Text == "(all)") {
                for (int i = 0; i < _dbControl.GUI_tableListBox.Items.Count; i++) {
                    RemoveTableAction(_dbControl.GUI_tableListBox.Items[i].ToString());
                    _mainWindow.OutPutBar($"All {_dbControl.GUI_tableNumberTextBlock.Text.Replace("x", string.Empty)} tables removed");
                    LoadedTable = string.Empty;
                }
                return;
            }

            RemoveTableAction(tableName);
            _mainWindow.OutPutBar($"Table '{tableName}' removed");
            if (tableName == LoadedTable) { 
                LoadedTable = string.Empty; 
            }
        }

        void RemoveTableAction(string tableName) {

            try {
                using (var cmd = new SQLiteCommand(SQL_dropTableString(tableName), _mainWindow.sqlConnect)) {
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex) { 
                _mainWindow.ProgrammLog($"Error (DBControl / RemoveTableAction) {ex}", true); 
            }
        }

        public void CreateTable() {

            if (!CreateTableString[1].EndsWith(')')) { 
                CreateTableString[1] += ')'; 
            }

            if (CreateTableString[1] != _dbControl.GUI_stringBuilderOutPut.Text) { 
                CreateTableString[1] = _dbControl.GUI_stringBuilderOutPut.Text; 
            }

            try {
                UndoManager();
                using (var cmd = new SQLiteCommand(CreateTableString[1], _mainWindow.sqlConnect)) {
                    cmd.ExecuteNonQuery();
                    _mainWindow.OutPutBar("Table created");
                }
            }
            catch (Exception ex) { 
                _mainWindow.ProgrammLog($"(DBControl / SQL_createTable) {ex}", true); 
            }

            ResetAfterStringBuilderUse();
        }

        void ResetAfterStringBuilderUse() {

            FirstAdd = true;
            CreateTableString[0] = string.Empty;
            CreateTableString[1] = "CREATE TABLE IF NOT EXISTS ";

            EditRowString[0] = string.Empty;
            EditRowString[1] = $"UPDATE {LoadedTable} SET "; 

            AddRowString = "INSERT INTO  () VALUES ('')";
            AddRowStringParts = new List<string> { string.Empty, string.Empty, string.Empty, string.Empty };

            if(_dbControl.GUI_rowCMDComboBox.SelectedIndex == 0) {
                _dbControl.GUI_stringBuilderOutPut.Text = AddRowString;
            }
            else if(_dbControl.GUI_rowCMDComboBox.SelectedIndex == 1) {
                _dbControl.GUI_stringBuilderOutPut.Text = EditRowString[1];
            }
            else if(_dbControl.GUI_tableCMDComboBox.SelectedIndex == 1) {
                _dbControl.GUI_stringBuilderOutPut.Text = CreateTableString[1];
            }
        }

        static int tableNum;
        public void GetTables() {

            tableNum = 0;

            try {
                using (var cmd = new SQLiteCommand(SQL_getAllTablesString, _mainWindow.sqlConnect)) {
                    using (var reader = cmd.ExecuteReader()) {
                        _dbControl.GUI_tableListBox.Items.Clear();

                        while (reader.Read()) { 
                            _dbControl.GUI_tableListBox.Items.Add(reader.GetString(0));
                            tableNum++; 
                        }

                        if (_dbControl.GUI_tableListBox.Items.Count == 0) { 
                            ManageGUI(1); 
                            return; 
                        }

                        ManageGUI(2);

                        if (tableNum > 0) { _dbControl.GUI_tableNumberTextBlock.Text = $"({tableNum}x)"; }
                    }
                }
            }
            catch (Exception ex) { 
                _mainWindow.ProgrammLog($"(DBControl / SQL_loadTables) {ex}", true); 
                return; 
            }
        }

        static bool FirstAdd = true;
        public List<string> CreateTableString = new() { string.Empty, "CREATE TABLE IF NOT EXISTS " };
        public void CreateTableStringBuilder() {

            string inp = _dbControl.GUI_inputTextBox.Text;

            CreateTableString[0] = CreateTableString[1];

            if (FirstAdd && _dbControl.GUI_stringBuilderOutPut.Text != CreateTableString[1]) {
                CreateTableString[1] = _dbControl.GUI_stringBuilderOutPut.Text;
                FirstAdd = false;
            }
            else if (_dbControl.GUI_stringBuilderOutPut.Text != CreateTableString[1] + ')' || _dbControl.GUI_stringBuilderOutPut.Text != CreateTableString[1]) {
                CreateTableString[1] = _dbControl.GUI_stringBuilderOutPut.Text.Replace(")", string.Empty);
            }

            if (FirstAdd && inp.Length > 0) {
                CreateTableString[1] += $"{inp}(";
                FirstAdd = false;
                _dbControl.GUI_stringBuilderOutPut.Text = $"{CreateTableString[1]})";
                return;
            }
            else if (!FirstAdd && inp.Length > 0) { 
                CreateTableString[1] += $"{inp} "; 
            }
            else if (FirstAdd && inp.Length == 0) { 
                _mainWindow.OutPutBar("Add TableName first"); 
                return; 
            }

            if (_dbControl.GUI_constraintsListBox.SelectedIndex != -1) { 
                CreateTableString[1] += $"{_dbControl.GUI_constraintsListBox.SelectedItem} "; 
            }
            else if (_dbControl.GUI_dataTypeAndTableInfoListBox.SelectedIndex != -1) { 
                CreateTableString[1] += $"{_dbControl.GUI_dataTypeAndTableInfoListBox.SelectedItem} "; 
            }

            _dbControl.GUI_stringBuilderOutPut.Text = $"{CreateTableString[1]})";
            _dbControl.GUI_constraintsListBox.SelectedIndex = -1;
            _dbControl.GUI_dataTypeAndTableInfoListBox.SelectedIndex = -1;
        }


        //NonConnectedFunctions
        void FindListLine(ListBox listBox) {

            if (_dbControl.GUI_inputTextBox.Text.Length == 0) {
                _mainWindow.OutPutBar("Enter an index or value to find");
                return;
            }

            int LineToFind = -1;

            if (int.TryParse(_dbControl.GUI_inputTextBox.Text, out int parsedIndex)) {
                LineToFind = GetCorrectLineIndex(listBox, parsedIndex, false);
            }
            else {
                foreach (string read in listBox.Items) {
                    LineToFind++;
                    if (read == _dbControl.GUI_inputTextBox.Text) {
                        break;
                    }
                }
            }

            if (LineToFind > listBox.Items.Count || LineToFind < 0) {
                _mainWindow.OutPutBar("Enter a valid index or value to find");
                return;
            }
            else if (LineToFind < 10) {
                _mainWindow.OutPutBar("Line should be visible");
                return;
            }

            listBox.ScrollIntoView(listBox.Items[LineToFind]);
            listBox.SelectedIndex = LineToFind;
        }

        public int GetCorrectLineIndex(ListBox listBox, int indexToFind, bool findActualListIndex = true) {

            try {

                if(LoadedTableAttributes.Count == 1) {
                    return listBox.SelectedIndex + 1;
                }
                else if (LoadedTableAttributes.Count > 1 && !listBox.Items.Contains(_mainWindow._settingsVal.SpaceRowsWithASCII)) {
                    return 1;
                }

                int internIndex = 0, checkRoundIndex = -1;
                for (int i = 0; i < listBox.Items.Count; i++) {
                    if (listBox.Items[i].ToString() != _mainWindow._settingsVal.SpaceRowsWithASCII) {
                        checkRoundIndex++;
                    }

                    if (checkRoundIndex == LoadedTableAttributes.Count) {
                        checkRoundIndex = 0;
                        internIndex++;
                        if (internIndex == indexToFind - 1 && !findActualListIndex) {
                            return i;
                        }
                    }

                    if (findActualListIndex && indexToFind == i) {
                        return internIndex + 1;
                    }
                }
                return -1;
            }
            catch (Exception ex) {
                _mainWindow.ProgrammLog($"(DBControl / GetCorrectLineIndex) {ex}", true);
                return -2;
            }
        }

        public List<string> DBFilePathBackup;
        public int UndoFileLoadNum = 0;
        public void DBFilePathBackupInit() {
            DBFilePathBackup = new() {
                $"{ MainWindow.DBFilePath.Replace(".db", string.Empty) }_backup0.db",
                $"{ MainWindow.DBFilePath.Replace(".db", string.Empty) }_backup1.db",
                $"{ MainWindow.DBFilePath.Replace(".db", string.Empty) }_backup2.db"
            };
        }

        //does not work properly and i dont really care to fix it
        void UndoManager(bool SaveUndoFiles = true) {
            try {
               
                if (SaveUndoFiles && _mainWindow._settingsVal.CreateAndSaveUndoFiles) {

                    if (File.Exists(DBFilePathBackup[1])) {
                        File.Copy(DBFilePathBackup[1], DBFilePathBackup[2], true);
                    }

                    if (File.Exists(DBFilePathBackup[0])) {
                        File.Copy(DBFilePathBackup[0], DBFilePathBackup[1], true);
                    }

                    File.Copy(MainWindow.DBFilePath, DBFilePathBackup[0], true);

                    UndoFileLoadNum = 0;
                    return;
                }
                else if (SaveUndoFiles && !_mainWindow._settingsVal.CreateAndSaveUndoFiles) { 
                    return; 
                }

                if (UndoFileLoadNum == 3 || !File.Exists(DBFilePathBackup[UndoFileLoadNum])) {
                    _mainWindow.OutPutBar("Out of undos");
                    return;
                }

                _mainWindow.sqlConnect.Close();
                SQLiteConnection.ClearAllPools();

                File.Copy(DBFilePathBackup[UndoFileLoadNum], MainWindow.DBFilePath, true);
                File.Delete(DBFilePathBackup[UndoFileLoadNum]);

                if (UndoFileLoadNum < DBFilePathBackup.Count) {
                    UndoFileLoadNum++; 
                }

                _mainWindow.SQL_connectionManager();
            }
            catch (Exception ex) {
                _mainWindow.ProgrammLog($"(DBControl / UndoManager) {ex}", true);
            }
        }
    }
}