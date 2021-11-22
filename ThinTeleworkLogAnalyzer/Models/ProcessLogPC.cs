using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ThinTeleworkLogAnalyzer.Models
{
    /// <summary>
    /// プロセスログ出力PC情報データ構造
    /// </summary>
    public class ProcessLogPCInfo
    {
        public string PCName { get; set; }          // PC名
        public string DetectDate { get; set; }      // 検知日
    }
    public class ProcessLogPC
    {
        /// <summary>
        /// プロセスログ出力PCリスト取得処理
        /// </summary>
        /// <param name="logFilePath">ログファイルのパス</param>
        /// <returns>プロセスログ出力PCリスト</returns>
        public static ObservableCollection<ProcessLogPCInfo> GetProcessLogPCList(string logFilePath)
        {
            ObservableCollection<ProcessLogPCInfo> processLogPCList = new ObservableCollection<ProcessLogPCInfo>();

            processLogPCList.Clear();

            // ログファイルを開き､プロセスデバッグログを検索する｡
            // 見つかった場合は､PC名と日付を抽出し､データを保存する｡
            StreamReader sr = new StreamReader(logFilePath);
            while (sr.EndOfStream == false)
            {
                string readstring = sr.ReadLine();

                if (readstring.Contains(Config.ExtractKeywordEnableProcessLog))
                {
                    Regex r = new Regex(Config.ExtractPatternPCNameDateInfo);
                    Match m = r.Match(readstring);
                    if (m.Success)
                    {
                        // PC名が同一日でリストに存在しない場合のみ､リストに追加する｡
                        // そうでない場合は､リストに追加する｡
                        string detectDate = m.Result("${year}") + "/" + m.Result("${month}") + "/" + m.Result("${day}");
                        ProcessLogPCInfo found = processLogPCList.FirstOrDefault(item =>
                                                                                    item.PCName == m.Result("${pcname}") &&
                                                                                    item.DetectDate == detectDate);
                        int index = processLogPCList.IndexOf(found);
                        if (index < 0)
                        {
                            ProcessLogPCInfo addinfo = new ProcessLogPCInfo
                            {
                                PCName = m.Result("${pcname}"),
                                DetectDate = detectDate
                            };
                            processLogPCList.Add(addinfo);
                        }
                    }
                }
            }
            sr.Close();

            return processLogPCList;
        }
    }
}
