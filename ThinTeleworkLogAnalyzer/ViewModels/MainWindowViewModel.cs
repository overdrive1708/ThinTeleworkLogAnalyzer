﻿using Microsoft.WindowsAPICodePack.Dialogs;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;

namespace ThinTeleworkLogAnalyzer.ViewModels
{
    /// <summary>
    /// テレワーク状況データクラス
    /// </summary>
    public class TeleworkStatus
    {
        public string PCName { get; set; }          // PC名
        public DateTime StartTime { get; set; }     // 開始時刻
        public DateTime EndTime { get; set; }       // 終了時刻
    }

    public class MainWindowViewModel : BindableBase
    {
        #region コンフィギュレーション
        // NTT 東日本 - IPA 「シン・テレワークシステム」側でフォーマットが変更になった場合や､
        // syslogサーバ側でフォーマットが変更になった場合は､以下設定の変更が必要｡

        /// <summary>
        /// ログからシステムの起動を判断するためのキーワード
        /// このキーワードが含まれる場合､インストールされていると判断する｡
        /// </summary>
        private static readonly string _installed_pcname_extract_keyword = "NTT 東日本 - IPA シン・テレワークシステム サーバー エンジンを起動しました。";

        /// <summary>
        /// ログから｢詳細デバッグログを有効｣を設定しているPCを判断するためのキーワード
        /// </summary>
        private static readonly string _verbose_log_pcname_extract_keyword = "[DEBUG]";

        /// <summary>
        /// ログから｢プロセスの起動･終了を記録｣を設定しているPCを判断するためのキーワード
        /// </summary>
        private static readonly string _process_log_pcname_extract_keyword = "プロセスが起動されました。";

        /// <summary>
        /// ログからインストール済みPC名を抽出するための正規表現パターン
        /// </summary>
        private static readonly string _pcname_extract_pattern = @"\[(?<pcname>.*)/Thin Telework System\]";

        /// <summary>
        /// ログからテレワークの開始を判断するためのキーワード(正規表現パターン)
        /// </summary>
        private static readonly string _telework_start_pattern = @"このコンピュータとの間で新しい仮想通信チャネル ID: \d+ を確立しました。これにより、現在このコンピュータとの間で確立済みの仮想通信チャネルの総数は 1 本となりました。";

        /// <summary>
        /// ログからテレワークの終了を判断するためのキーワード(正規表現パターン)
        /// </summary>
        private static readonly string _telework_end_pattern = @"このコンピュータとの間で確立されていた仮想通信チャネル ID: \d+ を切断しました。.*これにより、現在このコンピュータとの間で確立済みの仮想通信チャネルの総数は 0 本となりました。";

        /// <summary>
        /// ログからテレワークの開始･終了時のPC名･日時を抽出するための正規表現パターン
        /// </summary>
        private static readonly string _telework_info_extract_pattern = @"\[(?<pcname>.*)/Thin Telework System\].*(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2}).*(?<hour>\d{2}):(?<minutes>\d{2}):(?<second>\d{2})\..*\)";

        #endregion

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
        private ObservableCollection<string> _installed_pc_list = new ObservableCollection<string>();
        public ObservableCollection<string> InstalledPCList
        {
            get { return _installed_pc_list; }
            set { SetProperty(ref _installed_pc_list, value); }
        }

        /// <summary>
        /// バインディングデータ：詳細ログ出力PC
        /// </summary>
        private ObservableCollection<string> _vervose_log_pc_list = new ObservableCollection<string>();
        public ObservableCollection<string> VervoseLogPCList
        {
            get { return _vervose_log_pc_list; }
            set { SetProperty(ref _vervose_log_pc_list, value); }
        }

        /// <summary>
        /// バインディングデータ：プロセスログ出力PC
        /// </summary>
        private ObservableCollection<string> _process_log_pc_list = new ObservableCollection<string>();
        public ObservableCollection<string> ProcessLogPCList
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
        private string _logfile_path;
        public string LogFilePath
        {
            get { return _logfile_path; }
            set { SetProperty(ref _logfile_path, value); }
        }
        #endregion

        #region 処理
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindowViewModel()
        {
            // 無処理
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
            CreateInstalledPCList();

            // 詳細ログ出力PCの抽出を行う｡
            CreateVervoseLogPCList();

            //プロセスログ出力PCの抽出を行う｡
            CreateProcessLogPCList();

            // テレワーク状況の集約を行う｡
            CreateTeleworkStatus();

            // 解析が完了したため､CSV出力機能有効化する｡
            SystemStatus = "ログファイル読み込み&解析完了｡";
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
            swInstalledPCList.WriteLine("PC名");
            foreach(string str in InstalledPCList)
            {
                swInstalledPCList.WriteLine(string.Format("{0}", str));
            }
            swInstalledPCList.Close();

            // テレワーク状況をCSV出力する｡
            StreamWriter swTeleworkStatusList = new StreamWriter(Path.Combine(dialog.FileName, "テレワーク状況リスト.csv"), false, System.Text.Encoding.UTF8);
            swTeleworkStatusList.WriteLine("PC名,テレワーク日,開始時刻,終了時刻");
            foreach (TeleworkStatus data in TeleworkStatusData)
            {
                swTeleworkStatusList.WriteLine(string.Format("{0},{1},{2},{3}", data.PCName, data.StartTime.ToShortDateString(), data.StartTime.ToLongTimeString(), data.EndTime.ToLongTimeString()));
            }
            swTeleworkStatusList.Close();

            MessageBox.Show("CSV出力が完了しました｡", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// インストール済みPCリスト生成処理
        /// </summary>
        private void CreateInstalledPCList()
        {
            // 前回分のデータのクリア
            InstalledPCList.Clear();

            // ログファイルを開き､システムの起動ログを検索する｡
            // 見つかった場合は､PC名を抽出し､データを保存する｡
            StreamReader sr = new StreamReader(LogFilePath);
            while (sr.EndOfStream == false)
            {
                string readstring = sr.ReadLine();

                if(readstring.Contains(_installed_pcname_extract_keyword))
                {
                    Regex r = new Regex(_pcname_extract_pattern);
                    Match m = r.Match(readstring);
                    if (m.Success)
                    {
                        if (!InstalledPCList.Contains(m.Result("${pcname}")))
                        {
                            InstalledPCList.Add(m.Result("${pcname}"));
                        }
                    }
                }
            }
            sr.Close();
        }

        /// <summary>
        /// 詳細ログ出力PCリスト生成処理
        /// </summary>
        private void CreateVervoseLogPCList()
        {
            // 前回分のデータのクリア
            VervoseLogPCList.Clear();

            // ログファイルを開き､詳細デバッグログを検索する｡
            // 見つかった場合は､PC名を抽出し､データを保存する｡
            StreamReader sr = new StreamReader(LogFilePath);
            while (sr.EndOfStream == false)
            {
                string readstring = sr.ReadLine();

                if (readstring.Contains(_verbose_log_pcname_extract_keyword))
                {
                    Regex r = new Regex(_pcname_extract_pattern);
                    Match m = r.Match(readstring);
                    if (m.Success)
                    {
                        if (!VervoseLogPCList.Contains(m.Result("${pcname}")))
                        {
                            VervoseLogPCList.Add(m.Result("${pcname}"));
                        }
                    }
                }
            }
            sr.Close();
        }

        /// <summary>
        /// プロセスログ出力PCリスト生成処理
        /// </summary>
        private void CreateProcessLogPCList()
        {
            // 前回分のデータのクリア
            ProcessLogPCList.Clear();

            // ログファイルを開き､プロセスの開始･終了ログを検索する｡
            // 見つかった場合は､PC名を抽出し､データを保存する｡
            StreamReader sr = new StreamReader(LogFilePath);
            while (sr.EndOfStream == false)
            {
                string readstring = sr.ReadLine();

                if (readstring.Contains(_process_log_pcname_extract_keyword))
                {
                    Regex r = new Regex(_pcname_extract_pattern);
                    Match m = r.Match(readstring);
                    if (m.Success)
                    {
                        if (!ProcessLogPCList.Contains(m.Result("${pcname}")))
                        {
                            ProcessLogPCList.Add(m.Result("${pcname}"));
                        }
                    }
                }
            }
            sr.Close();
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
                if (Regex.IsMatch(readstring, _telework_start_pattern))
                {
                    Regex r = new Regex(_telework_info_extract_pattern);
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
                if (Regex.IsMatch(readstring, _telework_end_pattern))
                {
                    Regex r = new Regex(_telework_info_extract_pattern);
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
            }
            sr.Close();
        }

        /// <summary>
        /// テレワーク開始データ保存処理
        /// </summary>
        /// <param name="pcname">PC名</param>
        /// <param name="time">テレワーク開始日時</param>
        private void InsertTeleworkStatusStartTime(string pcname, DateTime time)
        {
            // PC名･日付の組み合わせで新規登録のみ行う｡
            // (ネットワークが切断された場合は､一日の最初と一日の最後を記録する｡)
            IEnumerable<int> foundItems = TeleworkStatusData.Select((item, index) => new { Index = index, Value = item })
                .Where(item => item.Value.PCName == pcname && item.Value.StartTime.Date == time.Date)
                .Select(item => item.Index);
            if(foundItems.Count() == 0)
            {
                TeleworkStatus startData = new TeleworkStatus
                {
                    PCName = pcname,
                    StartTime = time,
                    EndTime = new DateTime()
                };
                TeleworkStatusData.Add(startData);
            }
        }

        /// <summary>
        /// テレワーク終了データ保存処理
        /// </summary>
        /// <param name="pcname">PC名</param>
        /// <param name="time">テレワーク終了日時</param>
        private void InsertTeleworkStatusEndTime(string pcname, DateTime time)
        {
            // PC名･日付の組み合わせで開始時刻登録済みのデータについて､終了時刻を更新する｡
            TeleworkStatus found = TeleworkStatusData.FirstOrDefault(item => item.PCName == pcname && item.StartTime.Date == time.Date);
            int index = TeleworkStatusData.IndexOf(found);
            if (index >= 0)
            {
                TeleworkStatusData[index].EndTime = time;
            }
        }
        #endregion
    }
}
