using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Oracle.ManagedDataAccess.Client;

namespace FormBuilder;

/// <summary>
/// 資料庫連線設定：選資料庫類型（SQL Server / Oracle / MySQL），輸入伺服器、資料庫、驗證方式、帳號、密碼，組成連線字串。
/// 會記住上次的類型、伺服器、資料庫、帳號（不記密碼）
/// </summary>
public partial class ConnectionDialog : Form
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FormBuilder", "last-connection.json");

    /// <summary>按「確定」後組好的連線字串</summary>
    public string ConnectionString { get; private set; }

    /// <summary>按「確定」後選的資料庫類型</summary>
    public DbKind DatabaseType => Kind;

    private DbKind Kind => ((DbDialect)cboKind.SelectedItem)?.Kind ?? DbKind.SqlServer;

    public ConnectionDialog(string currentConnectionString, DbKind currentKind = DbKind.SqlServer)
    {
        InitializeComponent();
        cboKind.Items.AddRange(new object[] { DbDialect.SqlServer, DbDialect.Oracle, DbDialect.MySql });
        cboKind.SelectedIndexChanged += (s, e) => ApplyKind();
        btnListDb.Click += (s, e) => ListDatabases();
        rbSql.CheckedChanged += (s, e) => txtUser.Enabled = txtPassword.Enabled = rbSql.Checked;
        btnTest.Click += (s, e) => TestConnection(true);
        btnOk.Click += (s, e) =>
        {
            if (!TestConnection(false)) return;
            ConnectionString = Build();
            DbDialect.Register(ConnectionString, Kind);     // 之後各處只拿到連線字串，靠登記查出是哪種資料庫
            SaveLastSettings();
            DialogResult = DialogResult.OK;
        };
        LoadInitialValues(currentConnectionString, currentKind);
    }

    /// <summary>依資料庫類型切換欄位名稱與可用的選項</summary>
    private void ApplyKind()
    {
        var kind = Kind;
        lblDatabase.Text = kind == DbKind.Oracle ? "服務名稱" : "資料庫";
        btnListDb.Enabled = kind != DbKind.Oracle;               // Oracle 的服務名稱沒辦法列出來
        rbWindows.Enabled = kind == DbKind.SqlServer;            // Windows 驗證只有 SQL Server 有
        if (kind != DbKind.SqlServer) rbSql.Checked = true;
        txtServer.PlaceholderText = kind switch
        {
            DbKind.Oracle => "主機:連接埠（預設 1521）",
            DbKind.MySql => "主機:連接埠（預設 3306）",
            _ => @"伺服器\執行個體",
        };
        cboDatabase.Items.Clear();
    }

    /// <summary>有目前的連線字串就用它（含密碼）；沒有的話用上次記住的設定</summary>
    private void LoadInitialValues(string current, DbKind kind)
    {
        string server = "", database = "", user = "", password = "";
        bool windowsAuth = false;
        try
        {
            if (!string.IsNullOrWhiteSpace(current))
            {
                switch (kind)
                {
                    case DbKind.Oracle:
                    {
                        var b = new OracleConnectionStringBuilder(current);
                        // 資料來源是「主機:連接埠/服務名稱」（EZConnect）；沒有斜線的當 TNS 名稱放在伺服器
                        int slash = b.DataSource.LastIndexOf('/');
                        server = slash >= 0 ? b.DataSource[..slash] : b.DataSource;
                        database = slash >= 0 ? b.DataSource[(slash + 1)..] : "";
                        user = b.UserID;
                        password = b.Password;
                        break;
                    }
                    case DbKind.MySql:
                    {
                        var b = new MySqlConnectionStringBuilder(current);
                        server = b.Port is 0 or 3306 ? b.Server : $"{b.Server}:{b.Port}";
                        database = b.Database;
                        user = b.UserID;
                        password = b.Password;
                        break;
                    }
                    default:
                    {
                        var b = new SqlConnectionStringBuilder(current);
                        server = b.DataSource;
                        database = b.InitialCatalog;
                        windowsAuth = b.IntegratedSecurity;
                        user = b.UserID;
                        password = b.Password;
                        break;
                    }
                }
            }
            else if (File.Exists(SettingsPath))
            {
                var last = JsonSerializer.Deserialize<LastSettings>(File.ReadAllText(SettingsPath));
                kind = last.Kind;
                server = last.Server;
                database = last.Database;
                windowsAuth = last.WindowsAuth;
                user = last.User;
            }
        }
        catch
        {
            // 格式不對就從空白開始
        }
        cboKind.SelectedItem = DbDialect.For(kind);
        txtServer.Text = server;
        cboDatabase.Text = database;
        rbWindows.Checked = windowsAuth && kind == DbKind.SqlServer;
        rbSql.Checked = !rbWindows.Checked;
        txtUser.Text = user;
        txtPassword.Text = password;
    }

    /// <summary>依類型組出連線字串；database = null 時用畫面上輸入的資料庫（列出資料庫時另外指定）</summary>
    private string Build(string database = null)
    {
        string server = txtServer.Text.Trim();
        database ??= cboDatabase.Text.Trim();
        switch (Kind)
        {
            case DbKind.Oracle:
                return new OracleConnectionStringBuilder
                {
                    // EZConnect：主機[:連接埠]/服務名稱；沒填服務名稱時，伺服器當成 TNS 名稱
                    DataSource = database == "" ? server : $"{server}/{database}",
                    UserID = txtUser.Text.Trim(),
                    Password = txtPassword.Text,
                    ConnectionTimeout = 10,
                }.ConnectionString;

            case DbKind.MySql:
            {
                var (host, port) = SplitPort(server);
                var b = new MySqlConnectionStringBuilder
                {
                    Server = host,
                    UserID = txtUser.Text.Trim(),
                    Password = txtPassword.Text,
                    ConnectionTimeout = 10,
                };
                if (port.HasValue) b.Port = port.Value;
                if (database != "") b.Database = database;
                return b.ConnectionString;
            }

            default:
            {
                var b = new SqlConnectionStringBuilder
                {
                    DataSource = server,
                    InitialCatalog = database,
                    TrustServerCertificate = true,     // 內部伺服器通常沒有正式憑證
                    ConnectTimeout = 10,
                };
                if (rbWindows.Checked)
                {
                    b.IntegratedSecurity = true;
                }
                else
                {
                    b.UserID = txtUser.Text.Trim();
                    b.Password = txtPassword.Text;
                }
                return b.ConnectionString;
            }
        }
    }

    /// <summary>「主機:連接埠」或「主機,連接埠」拆開；沒有連接埠時回傳 null（用預設）</summary>
    public static (string Host, uint? Port) SplitPort(string server)
    {
        int sep = server.LastIndexOfAny(new[] { ':', ',' });
        if (sep > 0 && uint.TryParse(server[(sep + 1)..].Trim(), out uint port))
            return (server[..sep].Trim(), port);
        return (server, null);
    }

    private bool TestConnection(bool showSuccess)
    {
        // Oracle 可以只填 TNS 名稱（伺服器欄），服務名稱留空
        if (txtServer.Text.Trim() == "" || (Kind != DbKind.Oracle && cboDatabase.Text.Trim() == ""))
        {
            MessageBox.Show(Kind == DbKind.Oracle ? "請輸入伺服器與服務名稱。" : "請輸入伺服器與資料庫。", "提醒");
            return false;
        }
        try
        {
            Cursor = Cursors.WaitCursor;
            using var conn = DbDialect.For(Kind).Open(Build());
            if (showSuccess) MessageBox.Show("連線成功。", "測試連線");
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("連線失敗：" + ex.Message, "測試連線", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    /// <summary>列出伺服器上的資料庫（SQL Server 連到 master；MySQL 不指定資料庫）</summary>
    private void ListDatabases()
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            var d = DbDialect.For(Kind);
            using var conn = d.Open(Build(Kind == DbKind.SqlServer ? "master" : ""));
            using var cmd = d.CreateCommand(conn, Kind == DbKind.MySql
                ? "SELECT SCHEMA_NAME FROM information_schema.SCHEMATA ORDER BY SCHEMA_NAME"
                : "SELECT name FROM sys.databases WHERE HAS_DBACCESS(name) = 1 ORDER BY name");
            using var reader = cmd.ExecuteReader();
            string current = cboDatabase.Text;
            cboDatabase.Items.Clear();
            while (reader.Read()) cboDatabase.Items.Add(Convert.ToString(reader.GetValue(0)));
            cboDatabase.Text = current;
            cboDatabase.DroppedDown = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("列出資料庫失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    /// <summary>記住類型、伺服器、資料庫、帳號，方便下次使用；密碼不記</summary>
    private void SaveLastSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new LastSettings
            {
                Kind = Kind,
                Server = txtServer.Text.Trim(),
                Database = cboDatabase.Text.Trim(),
                WindowsAuth = rbWindows.Checked,
                User = txtUser.Text.Trim(),
            }));
        }
        catch
        {
            // 記不住只是下次要重打，不影響連線
        }
    }

    /// <summary>
    /// 畫面上顯示連線狀態用：只說有沒有設定、是哪種資料庫，不顯示伺服器、資料庫名稱、帳號（資料庫路徑不外露）
    /// </summary>
    public static string Describe(string connectionString) =>
        string.IsNullOrWhiteSpace(connectionString) ? "（尚未設定連線）" : $"已連線（{DbDialect.Of(connectionString).DisplayName}）";

    /// <summary>連線字串裡的密碼換成 ****（三種資料庫共用）；畫面上請用 Describe，不要顯示完整的連線字串</summary>
    public static string Mask(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return "（尚未設定連線）";
        try
        {
            var d = DbDialect.Of(connectionString);
            if (d.Kind == DbKind.SqlServer)
            {
                var sb = new SqlConnectionStringBuilder(connectionString);
                if (!string.IsNullOrEmpty(sb.Password)) sb.Password = "****";
                return sb.ConnectionString;
            }
            var b = new DbConnectionStringBuilder { ConnectionString = connectionString };
            foreach (string key in b.Keys.Cast<string>().ToList())
                if (key.Equals("password", StringComparison.OrdinalIgnoreCase) || key.Equals("pwd", StringComparison.OrdinalIgnoreCase))
                    b[key] = "****";
            return $"[{d.DisplayName}] {b.ConnectionString}";
        }
        catch
        {
            return "（連線字串格式不正確）";
        }
    }

    private class LastSettings
    {
        public DbKind Kind { get; set; }           // 舊的設定檔沒有 = SqlServer
        public string Server { get; set; }
        public string Database { get; set; }
        public bool WindowsAuth { get; set; }
        public string User { get; set; }
    }
}
