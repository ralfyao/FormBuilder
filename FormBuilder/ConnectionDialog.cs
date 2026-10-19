using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace FormBuilder;

/// <summary>
/// 資料庫連線設定：輸入伺服器、資料庫、驗證方式、帳號、密碼，組成連線字串。
/// 會記住上次的伺服器、資料庫、帳號（不記密碼）
/// </summary>
public partial class ConnectionDialog : Form
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FormBuilder", "last-connection.json");

    /// <summary>按「確定」後組好的連線字串</summary>
    public string ConnectionString { get; private set; }

    public ConnectionDialog(string currentConnectionString)
    {
        InitializeComponent();
        btnListDb.Click += (s, e) => ListDatabases();
        rbSql.CheckedChanged += (s, e) => txtUser.Enabled = txtPassword.Enabled = rbSql.Checked;
        btnTest.Click += (s, e) => TestConnection(true);
        btnOk.Click += (s, e) =>
        {
            if (!TestConnection(false)) return;
            ConnectionString = Build().ConnectionString;
            SaveLastSettings();
            DialogResult = DialogResult.OK;
        };
        LoadInitialValues(currentConnectionString);
    }

    /// <summary>有目前的連線字串就用它（含密碼）；沒有的話用上次記住的設定</summary>
    private void LoadInitialValues(string current)
    {
        var b = new SqlConnectionStringBuilder();
        try
        {
            if (!string.IsNullOrWhiteSpace(current)) b.ConnectionString = current;
            else if (File.Exists(SettingsPath))
            {
                var last = JsonSerializer.Deserialize<LastSettings>(File.ReadAllText(SettingsPath));
                b.DataSource = last.Server;
                b.InitialCatalog = last.Database;
                b.IntegratedSecurity = last.WindowsAuth;
                b.UserID = last.User;
            }
        }
        catch
        {
            // 格式不對就從空白開始
        }
        txtServer.Text = b.DataSource;
        cboDatabase.Text = b.InitialCatalog;
        rbWindows.Checked = b.IntegratedSecurity;
        rbSql.Checked = !b.IntegratedSecurity;
        txtUser.Text = b.UserID;
        txtPassword.Text = b.Password;
    }

    private SqlConnectionStringBuilder Build(string database = null)
    {
        var b = new SqlConnectionStringBuilder
        {
            DataSource = txtServer.Text.Trim(),
            InitialCatalog = database ?? cboDatabase.Text.Trim(),
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
        return b;
    }

    private bool TestConnection(bool showSuccess)
    {
        if (txtServer.Text.Trim() == "" || cboDatabase.Text.Trim() == "")
        {
            MessageBox.Show("請輸入伺服器與資料庫。", "提醒");
            return false;
        }
        try
        {
            Cursor = Cursors.WaitCursor;
            using var conn = new SqlConnection(Build().ConnectionString);
            conn.Open();
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

    /// <summary>連到 master 列出伺服器上的資料庫</summary>
    private void ListDatabases()
    {
        try
        {
            Cursor = Cursors.WaitCursor;
            using var conn = new SqlConnection(Build("master").ConnectionString);
            conn.Open();
            using var cmd = new SqlCommand("SELECT name FROM sys.databases WHERE HAS_DBACCESS(name) = 1 ORDER BY name", conn);
            using var reader = cmd.ExecuteReader();
            string current = cboDatabase.Text;
            cboDatabase.Items.Clear();
            while (reader.Read()) cboDatabase.Items.Add(reader.GetString(0));
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

    /// <summary>記住伺服器、資料庫、帳號，方便下次使用；密碼不記</summary>
    private void SaveLastSettings()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new LastSettings
            {
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

    /// <summary>連線字串裡的密碼換成 ****，用來顯示在畫面上</summary>
    public static string Mask(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return "（尚未設定連線）";
        try
        {
            var b = new SqlConnectionStringBuilder(connectionString);
            if (!string.IsNullOrEmpty(b.Password)) b.Password = "****";
            return b.ConnectionString;
        }
        catch
        {
            return "（連線字串格式不正確）";
        }
    }

    private class LastSettings
    {
        public string Server { get; set; }
        public string Database { get; set; }
        public bool WindowsAuth { get; set; }
        public string User { get; set; }
    }
}
