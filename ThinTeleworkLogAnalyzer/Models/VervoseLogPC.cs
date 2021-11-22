using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ThinTeleworkLogAnalyzer.Models
{
    /// <summary>
    /// 詳細ログ出力PC情報データ構造
    /// </summary>
    public class VervoseLogPCInfo
    {
        public string PCName { get; set; }          // PC名
        public string DetectDate { get; set; }      // 検知日
    }
    public class VervoseLogPC
    {
        /// <summary>
        /// 詳細ログ出力PCリスト取得処理
        /// </summary>
        /// <param name="logFilePath">ログファイルのパス</param>
        /// <returns>詳細ログ出力PCリスト</returns>
        public static ObservableCollection<VervoseLogPCInfo> GetVervoseLogPCList(string logFilePath)
        {
            ObservableCollection<VervoseLogPCInfo> vervoseLogPCList = new ObservableCollection<VervoseLogPCInfo>();

            vervoseLogPCList.Clear();

            // ログファイルを開き､詳細デバッグログを検索する｡
            // 見つかった場合は､PC名と日付を抽出し､データを保存する｡
            StreamReader sr = new StreamReader(logFilePath);
            while (sr.EndOfStream == false)
            {
                string readstring = sr.ReadLine();

                if (readstring.Contains(Config.ExtractKeywordEnableVerboseLog))
                {
                    Regex r = new Regex(Config.ExtractPatternPCNameDateInfo);
                    Match m = r.Match(readstring);
                    if (m.Success)
                    {
                        // PC名が同一日でリストに存在しない場合のみ､リストに追加する｡
                        // そうでない場合は､リストに追加する｡
                        string detectDate = m.Result("${year}") + "/" + m.Result("${month}") + "/" + m.Result("${day}");
                        VervoseLogPCInfo found = vervoseLogPCList.FirstOrDefault(item => 
                                                                                    item.PCName == m.Result("${pcname}") && 
                                                                                    item.DetectDate == detectDate);
                        int index = vervoseLogPCList.IndexOf(found);
                        if (index < 0)
                        {
                            VervoseLogPCInfo addinfo = new VervoseLogPCInfo
                            {
                                PCName = m.Result("${pcname}"),
                                DetectDate = detectDate
                            };
                            vervoseLogPCList.Add(addinfo);
                        }
                    }
                }
            }
            sr.Close();

            return vervoseLogPCList;
        }
    }
}
