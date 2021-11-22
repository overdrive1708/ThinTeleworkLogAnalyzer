using Microsoft.WindowsAPICodePack.Dialogs;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows;
using ThinTeleworkLogAnalyzer.Models;

namespace ThinTeleworkLogAnalyzer.ViewModels
{
    /// <summary>
    /// テレワーク状況データクラス
    /// </summary>
    public class TeleworkStatus
    {
        public string PCName { get; set; }          // PC名
        public DateTime Date { get; set; }          // テレワーク日
        public DateTime StartTime { get; set; }     // 開始時刻
        public DateTime EndTime { get; set; }       // 終了時刻
        public bool IsNowTeleworking { get; set; }  // テレワーク中フラグ
        public string ConnectTime { get; set; }     // 接続時間
        public string Remarks { get; set; }         // 備考
    }

    public class MainWindowViewModel : BindableBase
    {
        #region バインディングデータ
        /// <summary>
        /// バインディングデータ：タイトル
        /// </summary>
        private string _title = "Thin Telework Log Analyzer";
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        /// <summary>
        /// バインディングデータ：ステータス
        /// </summary>
        private string _system_status = "ログファイル読み込み未完了｡ログファイルをドラッグ&ドロップしてください｡";
        public string SystemStatus
        {
            get { return _system_status; }
            set { SetProperty(ref _system_status, value); }
        }

        /// <summary>
        /// バインディングデータ：インストール済みPC
        /// </summary>
        private ObservableCollection<InstalledPCInfo> _installed_pc_list = new ObservableCollection<InstalledPCInfo>();
        public ObservableCollection<InstalledPCInfo> InstalledPCList
        {
            get { return _installed_pc_list; }
            set { SetProperty(ref _installed_pc_list, value); }
        }

        /// <summary>
        /// バインディングデータ：詳細ログ出力PC
        /// </summary>
        private ObservableCollection<VervoseLogPCInfo> _vervose_log_pc_list = new ObservableCollection<VervoseLogPCInfo>();
        public ObservableCollection<VervoseLogPCInfo> VervoseLogPCList
        {
            get { return _vervose_log_pc_list; }
            set { SetProperty(ref _vervose_log_pc_list, value); }
        }

        /// <summary>
        /// バインディングデータ：プロセスログ出力PC
        /// </summary>
        private ObservableCollection<ProcessLogPCInfo> _process_log_pc_list = new ObservableCollection<ProcessLogPCInfo>();
        public ObservableCollection<ProcessLogPCInfo> ProcessLogPCList
        {
            get { return _process_log_pc_list; }
            set { SetProperty(ref _process_log_pc_list, value); }
        }

        /// <summary>
        /// バインディングデータ：テレワーク状況
        /// </summary>
        private ObservableCollection<TeleworkStatus> _telework_status_data = new ObservableCollection<TeleworkStatus>();
        public ObservableCollection<TeleworkStatus> TeleworkStatusData
        {
            get { return _telework_status_data; }
            set { SetProperty(ref _telework_status_data, value); }
        }

        /// <summary>
        /// バインディングデータ：CSV出力ボタン有効/無効
        /// </summary>
        private bool _is_enable_export = false;
        public bool IsEnableExport
        {
            get { return _is_enable_export; }
            set { SetProperty(ref _is_enable_export, value); }
        }
        #endregion

        #region バインディングコマンド
        /// <summary>
        /// バインディングコマンド：ログファイルドラッグ
        /// </summary>
        private DelegateCommand<DragEventArgs> _commandPreviewDrag;
        public DelegateCommand<DragEventArgs> Command_PreviewDragOver =>
            _commandPreviewDrag ?? (_commandPreviewDrag = new DelegateCommand<DragEventArgs>(ExecuteCommandPreviewDragOver));

        /// <summary>
        /// バインディングコマンド：ログファイルドロップ
        /// </summary>
        private DelegateCommand<DragEventArgs> _commandDrop;
        public DelegateCommand<DragEventArgs> Command_Drop =>
            _commandDrop ?? (_commandDrop = new DelegateCommand<DragEventArgs>(ExecuteCommandDrop));

        /// <summary>
        /// バインディングコマンド：エクスポート
        /// </summary>
        private DelegateCommand _commandExport;
        public DelegateCommand Command_Export =>
            _commandExport ?? (_commandExport = new DelegateCommand(ExecuteCommandExport));
        #endregion

        #region 内部データ
        /// <summary>
        /// ログファイルパス
        /// </summary>
        private string _logfile_path = string.Empty;
        public string LogFilePath
        {
            get { return _logfile_path; }
            set { SetProperty(ref _logfile_path, value); }
        }

        /// <summary>
        /// ログ開始日時
        /// </summary>
        private DateTime _log_start_Date = new DateTime(DateTime.MaxValue.Ticks);
        public DateTime LogStartDate
        {
            get { return _log_start_Date; }
            set { SetProperty(ref _log_start_Date, value); }
        }

        /// <summary>
        /// ログ終了日時
        /// </summary>
        private DateTime _log_end_Date = new DateTime(DateTime.MinValue.Ticks);
        public DateTime LogEndDate
        {
            get { return _log_end_Date; }
            set { SetProperty(ref _log_end_Date, value); }
        }
        #endregion

        #region 処理
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindowViewModel()
        {
            // バージョン情報をタイトルに表示する｡
            Assembly assm = Assembly.GetExecutingAssembly();
            Title = Title + " | Version:" +  assm.GetCustomAttribute<AssemblyFileVersionAttribute>()?.Version.ToString() ;
        }

        /// <summary>
        /// ログファイルドラッグコマンド実行処理
        /// </summary>
        /// <param name="e">イベントデータ</param>
        private void ExecuteCommandPreviewDragOver(DragEventArgs e)
        {
            // ドラッグしてきたデータがファイルの場合､ドロップを許可する｡
            e.Effects = DragDropEffects.Copy;
            e.Handled = e.Data.GetDataPresent(DataFormats.FileDrop);
        }

        /// <summary>
        /// ログファイルドロップコマンド実行処理
        /// </summary>
        /// <param name="e">イベントデータ</param>
        private void ExecuteCommandDrop(DragEventArgs e)
        {
            // ドロップされたデータの1つ目をファイル名として採用する｡
            if (e.Data.GetData(DataFormats.FileDrop) is string[] dropitems)
            {
                LogFilePath = dropitems[0];
            }

            // インストール済みPCの抽出を行う｡
            InstalledPCList = InstalledPC.GetInstalledPCList(LogFilePath);

            // 詳細ログ出力PCの抽出を行う｡
            VervoseLogPCList = VervoseLogPC.GetVervoseLogPCList(LogFilePath);

            // プロセスログ出力PCの抽出を行う｡
            ProcessLogPCList = ProcessLogPC.GetProcessLogPCList(LogFilePath);

            // テレワーク状況の集約を行う｡
            CreateTeleworkStatus();

            // ログの解析期間を算出する｡
            CreateAnalyzePeriod();

            // 解析が完了したため､CSV出力機能有効化する｡
            SystemStatus = "ログファイル読み込み&解析完了｡集計期間：" + LogStartDate.ToString() + "～" + LogEndDate.ToString();
            IsEnableExport = true;
        }

        /// <summary>
        /// エクスポートコマンド実行処理
        /// </summary>
        private void ExecuteCommandExport()
        {
            // 出力先を指定させる｡
            CommonOpenFileDialog dialog = new CommonOpenFileDialog()
            {
                Title = "フォルダを選択してください",
                InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                IsFolderPicker = true,
            };
            if (dialog.ShowDialog() != CommonFileDialogResult.Ok)
            {
                return;
            }

            // インストール済みPCリストをCSV出力する｡
            StreamWriter swInstalledPCList = new StreamWriter(Path.Combine(dialog.FileName, "インストール済みPCリスト.csv"), false, System.Text.Encoding.UTF8);
            swInstalledPCList.WriteLine("#集計期間:" + LogStartDate.ToString() + "～" + LogEndDate.ToString());
            swInstalledPCList.WriteLine("\"PC名\",\"Version\",\"Build\"");
            foreach(InstalledPCInfo installedPCinfo in InstalledPCList)
            {
                swInstalledPCList.WriteLine(string.Format("\"{0}\",\"{1}\",\"{2}\"", installedPCinfo.PCName, installedPCinfo.Version, installedPCinfo.Build));
            }
            swInstalledPCList.Close();

            // テレワーク状況をCSV出力する｡
            StreamWriter swTeleworkStatusList = new StreamWriter(Path.Combine(dialog.FileName, "テレワーク状況リスト.csv"), false, System.Text.Encoding.UTF8);
            swTeleworkStatusList.WriteLine("#集計期間:" + LogStartDate.ToString() + "～" + LogEndDate.ToString());
            swTeleworkStatusList.WriteLine("\"PC名\",\"テレワーク日\",\"開始時刻\",\"終了時刻\",\"接続時間\",\"備考\"");
            foreach (TeleworkStatus data in TeleworkStatusData)
            {
                swTeleworkStatusList.WriteLine(string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",\"{5}\"", data.PCName, data.Date.ToString("yyyy/MM/dd"), data.StartTime.ToString("HH:mm:ss"), data.EndTime.ToString("HH:mm:ss"), data.ConnectTime, data.Remarks));
            }
            swTeleworkStatusList.Close();

            MessageBox.Show("CSV出力が完了しました｡", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// テレワーク状況集約処理
        /// </summary>
        private void CreateTeleworkStatus()
        {
            // 前回分のデータのクリア
            TeleworkStatusData.Clear();

            // ログファイルを開き､ログファイルを検索する｡
            StreamReader sr = new StreamReader(LogFilePath);
            while (sr.EndOfStream == false)
            {
                string readstring = sr.ReadLine();

                // テレワーク開始のログが見つかった場合､PC名と日時を抽出し､テレワーク開始のデータを保存する｡
                if (Regex.IsMatch(readstring, Config.ExtractPatternStartTelework))
                {
                    Regex r = new Regex(Config.ExtractPatternPCNameDateInfo);
                    Match m = r.Match(readstring);
                    if (m.Success)
                    {
                        string pcname = m.Result("${pcname}");
                        DateTime time = new DateTime(   int.Parse(m.Result("${year}")),
                                                        int.Parse(m.Result("${month}")),
                                                        int.Parse(m.Result("${day}")),
                                                        int.Parse(m.Result("${hour}")),
                                                        int.Parse(m.Result("${minutes}")),
                                                        int.Parse(m.Result("${second}")));
                        InsertTeleworkStatusStartTime(pcname, time);
                    }
                }

                // テレワーク終了のログが見つかった場合､PC名と日時を抽出し､テレワーク終了のデータを保存する｡
                if (Regex.IsMatch(readstring, Config.ExtractPatternEndTelework))
                {
                    Regex r = new Regex(Config.ExtractPatternPCNameDateInfo);
                    Match m = r.Match(readstring);
                    if (m.Success)
                    {
                        string pcname = m.Result("${pcname}");
                        DateTime time = new DateTime(   int.Parse(m.Result("${year}")),
                                                        int.Parse(m.Result("${month}")),
                                                        int.Parse(m.Result("${day}")),
                                                        int.Parse(m.Result("${hour}")),
                                                        int.Parse(m.Result("${minutes}")),
                                                        int.Parse(m.Result("${second}")));
                        InsertTeleworkStatusEndTime(pcname, time);
                    }
                }

                // テレワーク中にテレワーク終了のログが欠測し､シャットダウンのログが見つかった場合､テレワーク終了のデータとして扱う｡
                if (readstring.Contains(Config.ExtractKeywordShutdown))
                {
                    Regex r = new Regex(Config.ExtractPatternPCNameDateInfo);
                    Match m = r.Match(readstring);
                    if (m.Success)
                    {
                        string pcname = m.Result("${pcname}");
                        DateTime time = new DateTime(int.Parse(m.Result("${year}")),
                                                        int.Parse(m.Result("${month}")),
                                                        int.Parse(m.Result("${day}")),
                                                        int.Parse(m.Result("${hour}")),
                                                        int.Parse(m.Result("${minutes}")),
                                                        int.Parse(m.Result("${second}")));
                        UpdateTeleworkStatusEndTime(pcname, time);
                    }
                }
            }
            sr.Close();

            // 欠測のチェックと接続時間の計算
            for (int cnt = 0; cnt < TeleworkStatusData.Count(); cnt++)
            {
                TeleworkStatus work = TeleworkStatusData[cnt];
                if(work.StartTime == DateTime.MinValue)
                {
                    work.ConnectTime = "--:--:--";
                    work.Remarks = "開始時刻:欠測";
                }
                else if(work.EndTime == DateTime.MinValue)
                {
                    work.ConnectTime = "--:--:--";
                    work.Remarks = "終了時刻:欠測";
                }
                else
                {
                    TimeSpan elapsedTime = work.EndTime - work.StartTime;
                    work.ConnectTime = elapsedTime.ToString("c");
                    work.Remarks = "－";
                }
                TeleworkStatusData[cnt] = work;
            }
        }

        /// <summary>
        /// テレワーク開始データ保存処理
        /// </summary>
        /// <param name="pcname">PC名</param>
        /// <param name="time">テレワーク開始日時</param>
        private void InsertTeleworkStatusStartTime(string pcname, DateTime time)
        {
            // テレワーク中の情報が記録されている場合､終了時間を欠測したと判断する｡
            TeleworkStatus found = TeleworkStatusData.FirstOrDefault(item => item.PCName == pcname && item.IsNowTeleworking);
            int index = TeleworkStatusData.IndexOf(found);
            if (index >= 0)
            {
                TeleworkStatusData[index].EndTime = DateTime.MinValue;
                TeleworkStatusData[index].IsNowTeleworking = false;
            }

            // テレワーク開始で登録する｡
            TeleworkStatus startData = new TeleworkStatus
            {
                PCName = pcname,
                Date = time,
                StartTime = time,
                EndTime = DateTime.MinValue,
                IsNowTeleworking = true,
                ConnectTime = string.Empty,
                Remarks = string.Empty
            };
            TeleworkStatusData.Add(startData);
        }

        /// <summary>
        /// テレワーク終了データ保存処理
        /// </summary>
        /// <param name="pcname">PC名</param>
        /// <param name="time">テレワーク終了日時</param>
        private void InsertTeleworkStatusEndTime(string pcname, DateTime time)
        {
            // テレワーク中の情報に対して､終了時刻を書き込む｡
            // テレワーク中の情報が見つからない場合､開始時間を欠測したと判断する｡
            TeleworkStatus found = TeleworkStatusData.FirstOrDefault(item => item.PCName == pcname && item.IsNowTeleworking);
            int index = TeleworkStatusData.IndexOf(found);
            if (index >= 0)
            {
                TeleworkStatusData[index].EndTime = time;
                TeleworkStatusData[index].IsNowTeleworking = false;
            }
            else
            {
                TeleworkStatus startData = new TeleworkStatus
                {
                    PCName = pcname,
                    Date = time,
                    StartTime = DateTime.MinValue,
                    EndTime = time,
                    IsNowTeleworking = false,
                    ConnectTime = string.Empty,
                    Remarks = string.Empty
                };
                TeleworkStatusData.Add(startData);
            }
        }

        /// <summary>
        /// テレワーク終了データ更新処理
        /// </summary>
        /// <param name="pcname">PC名</param>
        /// <param name="time">テレワーク終了日時</param>
        private void UpdateTeleworkStatusEndTime(string pcname, DateTime time)
        {
            // テレワーク中の情報が見つかった場合は､切断ログを取りこぼしたものとして終了時間を更新する｡
            // テレワークをしない日でもシャットダウンのログが残るため､更新のみを行う｡
            TeleworkStatus found = TeleworkStatusData.FirstOrDefault(item => item.PCName == pcname && item.IsNowTeleworking);
            int index = TeleworkStatusData.IndexOf(found);
            if (index >= 0)
            {
                TeleworkStatusData[index].EndTime = time;
                TeleworkStatusData[index].IsNowTeleworking = false;
            }
        }

        /// <summary>
        /// ログ解析期間算出処理
        /// </summary>
        private void CreateAnalyzePeriod()
        {
            // ログファイルを開き､ログファイルを解析期間を算出する｡
            StreamReader sr = new StreamReader(LogFilePath);
            while (sr.EndOfStream == false)
            {
                string readstring = sr.ReadLine();

                Regex r = new Regex(Config.ExtractPatternPCNameDateInfo);
                Match m = r.Match(readstring);
                if (m.Success)
                {
                    DateTime time = new DateTime(int.Parse(m.Result("${year}")),
                                                    int.Parse(m.Result("${month}")),
                                                    int.Parse(m.Result("${day}")),
                                                    int.Parse(m.Result("${hour}")),
                                                    int.Parse(m.Result("${minutes}")),
                                                    int.Parse(m.Result("${second}")));
                    if(time < LogStartDate)
                    {
                        LogStartDate = time;
                    }
                    if(LogEndDate < time)
                    {
                        LogEndDate = time;
                    }
                }
            }
            sr.Close();
        }
        #endregion
    }
}
