using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ThinTeleworkLogAnalyzer.Models
{
    /// <summary>
    /// インストール済みPC情報データ構造
    /// </summary>
    public class InstalledPCInfo
    {
        public string PCName { get; set; }          // PC名
        public string Version { get; set; }         // Version
        public string Build { get; set; }           // Build
    }

    /// <summary>
    /// インストール済みPC情報
    /// </summary>
    public class InstalledPC
    {
        /// <summary>
        /// インストール済みPCリスト取得処理
        /// </summary>
        /// <param name="logFilePath">ログファイルのパス</param>
        /// <returns>インストール済みPCリスト</returns>
        public static ObservableCollection<InstalledPCInfo> GetInstalledPCList(string logFilePath)
        {
            ObservableCollection<InstalledPCInfo> installedPCList = new ObservableCollection<InstalledPCInfo>();

            installedPCList.Clear();

            StreamReader sr = new StreamReader(logFilePath);
            while (sr.EndOfStream == false)
            {
                string readstring = sr.ReadLine();

                // システムの起動ログを検索する｡
                // 見つかった場合は､PC名･Version･Buildを抽出し､リストを作成する｡
                Regex r = new Regex(Config.ExtractPatternInstallInfo);
                Match m = r.Match(readstring);
                if (m.Success)
                {
                    // PC名がすでにリストに存在する場合､リストを更新する｡
                    // そうでない場合は､リストに追加する｡
                    InstalledPCInfo found = installedPCList.FirstOrDefault(item => item.PCName == m.Result("${pcname}"));
                    int index = installedPCList.IndexOf(found);
                    if (index >= 0)
                    {
                        installedPCList[index].Version = m.Result("${version}");
                        installedPCList[index].Build = m.Result("${build}");
                    }
                    else
                    {
                        InstalledPCInfo addinfo = new InstalledPCInfo
                        {
                            PCName = m.Result("${pcname}"),
                            Version = m.Result("${version}"),
                            Build = m.Result("${build}")
                        };
                        installedPCList.Add(addinfo);
                    }
                }
            }
            sr.Close();

            return installedPCList;
        }
    }
}
