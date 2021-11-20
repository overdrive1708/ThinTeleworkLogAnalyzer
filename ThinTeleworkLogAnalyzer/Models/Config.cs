namespace ThinTeleworkLogAnalyzer.Models
{
    /// <summary>
    /// コンフィギュレーション
    /// </summary>
    public class Config
    {
        // NTT 東日本 - IPA 「シン・テレワークシステム」側でフォーマットが変更になった場合や､
        // syslogサーバ側でフォーマットが変更になった場合は､以下設定の変更が必要｡

        /// <summary>
        /// ログからインストール済みPCのPC名･Version情報･Build情報を抽出するための正規表現パターン
        /// </summary>
        public static readonly string ExtractPatternInstallInfo = @"\[(?<pcname>.*)/Thin Telework System\].*NTT 東日本 - IPA シン・テレワークシステム サーバー.*Version (?<version>.*) Build (?<build>.*)";

        /// <summary>
        /// ログから｢詳細デバッグログを有効｣を設定しているPCを判断するためのキーワード
        /// </summary>
        public static readonly string ExtractKeywordEnableVerboseLog = "[DEBUG]";

        /// <summary>
        /// ログから｢プロセスの起動･終了を記録｣を設定しているPCを判断するためのキーワード
        /// </summary>
        public static readonly string ExtractKeywordEnableProcessLog = "プロセスが起動されました。";

        /// <summary>
        /// ログからテレワークの開始を判断するための正規表現パターン
        /// </summary>
        public static readonly string ExtractPatternStartTelework = @"このコンピュータとの間で新しい仮想通信チャネル ID: \d+ を確立しました。これにより、現在このコンピュータとの間で確立済みの仮想通信チャネルの総数は 1 本となりました。";

        /// <summary>
        /// ログからテレワークの終了を判断するための正規表現パターン
        /// </summary>
        public static readonly string ExtractPatternEndTelework = @"このコンピュータとの間で確立されていた仮想通信チャネル ID: \d+ を切断しました。.*これにより、現在このコンピュータとの間で確立済みの仮想通信チャネルの総数は 0 本となりました。";

        /// <summary>
        /// ログからPC名･日時を抽出するための正規表現パターン
        /// </summary>
        public static readonly string ExtractPatternPCNameDateInfo = @"\[(?<pcname>.*)/Thin Telework System\].*(?<year>\d{4})-(?<month>\d{2})-(?<day>\d{2}).*(?<hour>\d{2}):(?<minutes>\d{2}):(?<second>\d{2})\..*\)";
    }
}
