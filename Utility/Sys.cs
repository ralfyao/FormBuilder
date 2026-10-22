using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace FormBuilder;

// ================================================================================================
// 系統資料庫（使用者、角色、權限、選單、已發布的畫面、稽核紀錄）。目前系統資料庫只支援 SQL Server；
// 各畫面的業務資料庫照舊可以是 SQL Server / Oracle / MySQL
// ================================================================================================

/// <summary>
/// 本機設定（%ProgramData%\ERPStudio\config.json）：系統資料庫連線（密碼用 Windows DPAPI 加密）、更新來源資料夾。
/// IT 也可以在執行檔旁放一份 config.json（可寫明碼連線字串），優先使用
/// </summary>
public class SysConfig
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static string MachinePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "ERPStudio", "config.json");
    public static string AppPath => Path.Combine(AppContext.BaseDirectory, "config.json");

    /// <summary>明碼連線字串（IT 部署用；本機設定精靈不寫這個）</summary>
    public string SystemConnection { get; set; }
    /// <summary>加密過的連線字串（DPAPI，只有這台電腦解得開）</summary>
    public string SystemConnectionProtected { get; set; }
    /// <summary>自動更新的來源資料夾（例如 \\server\ERPStudio\release），空白 = 不檢查更新</summary>
    public string UpdateSource { get; set; }
    /// <summary>上次登入的帳號（登入畫面預先帶入）</summary>
    public string LastUser { get; set; }
    /// <summary>上次從系統資料庫讀到的登入畫面企業識別（JSON），登入畫面先用它馬上顯示，不用等資料庫</summary>
    public string LoginBrandingCache { get; set; }

    public string GetConnection()
    {
        if (!string.IsNullOrWhiteSpace(SystemConnection)) return SystemConnection;
        if (string.IsNullOrWhiteSpace(SystemConnectionProtected)) return null;
        try
        {
            var bytes = ProtectedData.Unprotect(Convert.FromBase64String(SystemConnectionProtected), null, DataProtectionScope.LocalMachine);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (Exception) { return null; }       // 從別台電腦複製來的設定解不開
    }

    public void SetConnection(string connectionString)
    {
        SystemConnection = null;
        SystemConnectionProtected = Convert.ToBase64String(
            ProtectedData.Protect(Encoding.UTF8.GetBytes(connectionString), null, DataProtectionScope.LocalMachine));
    }

    public static SysConfig Load()
    {
        foreach (string path in new[] { AppPath, MachinePath })
        {
            try
            {
                if (File.Exists(path)) return JsonSerializer.Deserialize<SysConfig>(File.ReadAllText(path), JsonOptions) ?? new SysConfig();
            }
            catch (Exception) { }
        }
        return new SysConfig();
    }

    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(MachinePath));
        File.WriteAllText(MachinePath, JsonSerializer.Serialize(this, JsonOptions));
    }
}

/// <summary>
/// 登入畫面的企業識別：標題、副標題、標誌、橫幅與按鈕顏色、頁尾文字。
/// 存在系統資料庫（SYS_Setting），由 ERP Studio「系統管理 → 登入畫面（企業識別）」編輯，所有電腦共用
/// </summary>
public class LoginBranding
{
    public const string SettingKey = "LoginBranding";
    public static readonly Color DefaultBanner = Color.FromArgb(31, 58, 96);
    private static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>大標題（公司或系統名稱），空白 = 產品名稱</summary>
    public string Title { get; set; }
    public string Subtitle { get; set; }
    /// <summary>頁尾文字（例如版權宣告、客服電話）</summary>
    public string Footer { get; set; }
    /// <summary>標誌圖片（PNG，Base64）</summary>
    public string Logo { get; set; }
    /// <summary>顏色用 #RRGGBB，空白 = 預設</summary>
    public string BannerColor { get; set; }
    public string TextColor { get; set; }
    public string ButtonColor { get; set; }

    [System.Text.Json.Serialization.JsonIgnore] public Color Banner => ParseColor(BannerColor) ?? DefaultBanner;
    [System.Text.Json.Serialization.JsonIgnore] public Color Text => ParseColor(TextColor) ?? Color.White;
    [System.Text.Json.Serialization.JsonIgnore] public Color? Button => ParseColor(ButtonColor);

    public static Color? ParseColor(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        try { return ColorTranslator.FromHtml(html); }
        catch (Exception) { return null; }
    }

    public static string ToHtml(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    public Image LogoImage()
    {
        if (string.IsNullOrEmpty(Logo)) return null;
        try
        {
            using var ms = new MemoryStream(Convert.FromBase64String(Logo));
            return new Bitmap(Image.FromStream(ms));       // 複製一份，串流關掉後圖片還能用
        }
        catch (Exception) { return null; }
    }

    /// <summary>設定標誌：太大的圖縮到 256 像素以內再存成 PNG（系統資料庫和每台電腦的快取都不會太大）</summary>
    public void SetLogo(Image image)
    {
        if (image == null) { Logo = null; return; }
        double scale = Math.Min(1.0, 256.0 / Math.Max(image.Width, image.Height));
        using var bmp = new Bitmap(image, Math.Max(1, (int)(image.Width * scale)), Math.Max(1, (int)(image.Height * scale)));
        using var ms = new MemoryStream();
        bmp.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        Logo = Convert.ToBase64String(ms.ToArray());
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static LoginBranding FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new LoginBranding();
        try { return JsonSerializer.Deserialize<LoginBranding>(json, JsonOptions) ?? new LoginBranding(); }
        catch (Exception) { return new LoginBranding(); }
    }

    public static LoginBranding Load(string cs) => FromJson(SysDb.LoadSetting(cs, SettingKey));

    public void Save(string cs, string userId) => SysDb.SaveSetting(cs, SettingKey, ToJson(), userId);
}

/// <summary>
/// 一個畫面的權限（多個角色時取聯集）：開啟、新增、編修（Modify）、刪除、報表（Print：列印、列印報表）、
/// 核准（Approve：生效 / 取消生效）、輸出（Export：清單匯出 Excel / CSV）
/// </summary>
public record FormPermission(bool Open, bool Insert, bool Modify, bool Delete, bool Print, bool Approve = false, bool Export = false)
{
    public static readonly FormPermission All = new(true, true, true, true, true, true, true);
    public static readonly FormPermission None = new(false, false, false, false, false, false, false);

    public bool Any => Open || Insert || Modify || Delete || Print || Approve || Export;

    public FormPermission Union(FormPermission other) =>
        new(Open || other.Open, Insert || other.Insert, Modify || other.Modify, Delete || other.Delete, Print || other.Print,
            Approve || other.Approve, Export || other.Export);
}

/// <summary>角色對一個畫面的權限設定：權限、註記、職務代理效期（過了這天這組權限就失效；null = 不限）</summary>
public record RolePermission(FormPermission Permission, string Note, DateTime? ValidUntil);

/// <summary>登入的使用者</summary>
public record SysUser(string UserId, string UserName, bool IsAdmin);

/// <summary>
/// 目前登入的狀態：使用者、系統資料庫連線、各畫面的權限。
/// 沒有登入（開發時用 --designer 或直接開畫面檔）時 Active = false，權限全開、不寫稽核紀錄、畫面從檔案讀
/// </summary>
public static class SysContext
{
    public static SysUser User { get; private set; }
    public static string Connection { get; private set; }
    private static Dictionary<string, FormPermission> permissions = new(StringComparer.OrdinalIgnoreCase);

    public static bool Active => User != null && Connection != null;
    public static bool IsAdmin => !Active || User.IsAdmin;

    public static void SignIn(SysUser user, string connection)
    {
        User = user;
        Connection = connection;
        ReloadPermissions();
    }

    public static void SignOut()
    {
        User = null;
        Connection = null;
        permissions.Clear();
    }

    public static void ReloadPermissions() =>
        permissions = Active && !User.IsAdmin ? SysDb.LoadUserPermissions(Connection, User.UserId) : new(StringComparer.OrdinalIgnoreCase);

    /// <summary>某個畫面的權限：沒登入或管理者 = 全開；沒設定 = 全關</summary>
    public static FormPermission PermissionFor(string formKey)
    {
        if (!Active || User.IsAdmin) return FormPermission.All;
        return formKey != null && permissions.TryGetValue(formKey, out var p) ? p : FormPermission.None;
    }
}

/// <summary>稽核紀錄：誰、什麼時候、在哪個畫面、對哪個資料表做了什麼（沒登入時不寫）</summary>
public static class Audit
{
    private static bool warned;

    /// <summary>寫一筆稽核紀錄；寫入失敗不影響原本的操作，只提醒一次</summary>
    public static void Write(string formKey, string tableName, string action, string keyText, object detail = null)
    {
        if (!SysContext.Active) return;
        try
        {
            string json = detail == null ? null : detail as string ?? JsonSerializer.Serialize(detail, new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            SysDb.WriteAudit(SysContext.Connection, SysContext.User.UserId, formKey, tableName, action, keyText, json);
        }
        catch (Exception ex)
        {
            if (warned) return;
            warned = true;
            MessageBox.Show("寫入稽核紀錄失敗（資料已照常存檔）：" + ex.Message, "提醒");
        }
    }

    /// <summary>主鍵值組成文字，例如「廠商編號=AA-003」</summary>
    public static string KeyText(IEnumerable<KeyValuePair<string, object>> key) =>
        string.Join(", ", key.Select(k => $"{k.Key}={Format(k.Value)}"));

    public static string Format(object value) => value == null || value == DBNull.Value ? "NULL"
        : value is DateTime d ? d.ToString("yyyy/MM/dd HH:mm:ss") : Convert.ToString(value)?.TrimEnd();

    /// <summary>修改前後有變的欄位：{ 欄位: "舊 → 新" }</summary>
    public static Dictionary<string, string> Changes(DataRow before, IDictionary<string, object> after)
    {
        var changes = new Dictionary<string, string>();
        foreach (var (column, value) in after)
        {
            string oldText = before != null && before.Table.Columns.Contains(column) ? Format(before[column]) : "?";
            string newText = Format(value);
            if (oldText != newText) changes[column] = $"{oldText} → {newText}";
        }
        return changes;
    }
}

/// <summary>密碼雜湊：PBKDF2-SHA256，每個帳號不同的鹽，存成 PBKDF2$次數$鹽$雜湊</summary>
public static class PasswordHasher
{
    private const int Iterations = 100_000;

    public static string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(16);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"PBKDF2${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string stored)
    {
        var parts = (stored ?? "").Split('$');
        if (parts.Length != 4 || parts[0] != "PBKDF2" || !int.TryParse(parts[1], out int iterations)) return false;
        byte[] salt = Convert.FromBase64String(parts[2]);
        byte[] expected = Convert.FromBase64String(parts[3]);
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>密碼規則：至少 8 個字，要有英文字母和數字；不符合時回傳原因</summary>
    public static string CheckStrength(string password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < 8) return "密碼至少要 8 個字。";
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit)) return "密碼要同時有英文字母和數字。";
        return null;
    }
}

/// <summary>選單的一個節點：FormKey = null 是資料夾</summary>
public class MenuNode
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string Title { get; set; }
    public string FormKey { get; set; }
    public int SortOrder { get; set; }
    public bool IsFolder => string.IsNullOrEmpty(FormKey);
}

/// <summary>選單樹（存檔用）：節點與它的子節點</summary>
public class MenuTree
{
    public MenuNode Node { get; init; }
    public List<MenuTree> Children { get; } = new();
}

/// <summary>已發布的畫面或列印樣板</summary>
public record PublishedItem(string FormKey, string Kind, string Title, int Version, string PublishedBy, DateTime PublishedAt);

/// <summary>系統資料庫的資料存取</summary>
public static class SysDb
{
    public const int LockAfterFailures = 5;

    private static SqlConnection Open(string cs)
    {
        var conn = new SqlConnection(cs);
        conn.Open();
        return conn;
    }

    private static SqlCommand Cmd(SqlConnection conn, string sql, params (string Name, object Value)[] ps)
    {
        var cmd = new SqlCommand(sql, conn);
        foreach (var (name, value) in ps) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    private static DataTable Query(string cs, string sql, params (string, object)[] ps)
    {
        using var conn = Open(cs);
        using var cmd = Cmd(conn, sql, ps);
        var table = new DataTable();
        new SqlDataAdapter(cmd).Fill(table);
        return table;
    }

    private static int Execute(string cs, string sql, params (string, object)[] ps)
    {
        using var conn = Open(cs);
        using var cmd = Cmd(conn, sql, ps);
        return cmd.ExecuteNonQuery();
    }

    // ================= 建立資料表 =================

    /// <summary>建立系統資料表（已經有的不動）；可以重複執行</summary>
    public static void EnsureSchema(string cs)
    {
        const string sql = """
            IF OBJECT_ID('dbo.SYS_User') IS NULL CREATE TABLE dbo.SYS_User(
                UserId nvarchar(30) NOT NULL PRIMARY KEY, UserName nvarchar(50) NOT NULL, PasswordHash nvarchar(200) NOT NULL,
                IsAdmin bit NOT NULL DEFAULT 0, IsActive bit NOT NULL DEFAULT 1, MustChangePassword bit NOT NULL DEFAULT 1,
                FailedCount int NOT NULL DEFAULT 0, LastLogin datetime2 NULL, CreatedAt datetime2 NOT NULL DEFAULT SYSDATETIME());
            IF OBJECT_ID('dbo.SYS_Role') IS NULL CREATE TABLE dbo.SYS_Role(
                RoleId nvarchar(30) NOT NULL PRIMARY KEY, RoleName nvarchar(50) NOT NULL);
            IF OBJECT_ID('dbo.SYS_UserRole') IS NULL CREATE TABLE dbo.SYS_UserRole(
                UserId nvarchar(30) NOT NULL, RoleId nvarchar(30) NOT NULL, PRIMARY KEY(UserId, RoleId));
            IF OBJECT_ID('dbo.SYS_Form') IS NULL CREATE TABLE dbo.SYS_Form(
                FormKey nvarchar(200) NOT NULL PRIMARY KEY, Kind nvarchar(10) NOT NULL, Title nvarchar(100) NULL,
                Content nvarchar(max) NOT NULL, Version int NOT NULL DEFAULT 1, PublishedBy nvarchar(30) NULL,
                PublishedAt datetime2 NOT NULL DEFAULT SYSDATETIME());
            IF OBJECT_ID('dbo.SYS_Menu') IS NULL CREATE TABLE dbo.SYS_Menu(
                MenuId int IDENTITY NOT NULL PRIMARY KEY, ParentId int NULL, Title nvarchar(100) NOT NULL,
                FormKey nvarchar(200) NULL, SortOrder int NOT NULL DEFAULT 0);
            IF OBJECT_ID('dbo.SYS_Permission') IS NULL CREATE TABLE dbo.SYS_Permission(
                RoleId nvarchar(30) NOT NULL, FormKey nvarchar(200) NOT NULL,
                CanOpen bit NOT NULL DEFAULT 0, CanInsert bit NOT NULL DEFAULT 0, CanModify bit NOT NULL DEFAULT 0,
                CanDelete bit NOT NULL DEFAULT 0, CanPrint bit NOT NULL DEFAULT 0, PRIMARY KEY(RoleId, FormKey));
            -- 後來加的權限欄位：已經建好的系統資料庫自動補上
            IF COL_LENGTH('dbo.SYS_Permission', 'CanApprove') IS NULL ALTER TABLE dbo.SYS_Permission ADD CanApprove bit NOT NULL DEFAULT 0;
            IF COL_LENGTH('dbo.SYS_Permission', 'CanExport') IS NULL ALTER TABLE dbo.SYS_Permission ADD CanExport bit NOT NULL DEFAULT 0;
            IF COL_LENGTH('dbo.SYS_Permission', 'Note') IS NULL ALTER TABLE dbo.SYS_Permission ADD Note nvarchar(150) NULL;
            IF COL_LENGTH('dbo.SYS_Permission', 'ValidUntil') IS NULL ALTER TABLE dbo.SYS_Permission ADD ValidUntil smalldatetime NULL;
            IF OBJECT_ID('dbo.SYS_AuditLog') IS NULL
            BEGIN
                CREATE TABLE dbo.SYS_AuditLog(
                    LogId bigint IDENTITY NOT NULL PRIMARY KEY, LogTime datetime2 NOT NULL DEFAULT SYSDATETIME(),
                    UserId nvarchar(30) NULL, Machine nvarchar(64) NULL, FormKey nvarchar(200) NULL, TableName nvarchar(200) NULL,
                    Action nvarchar(20) NOT NULL, KeyText nvarchar(400) NULL, Detail nvarchar(max) NULL);
                CREATE INDEX IX_SYS_AuditLog_LogTime ON dbo.SYS_AuditLog(LogTime);
            END
            IF OBJECT_ID('dbo.SYS_Setting') IS NULL CREATE TABLE dbo.SYS_Setting(
                SettingKey nvarchar(100) NOT NULL PRIMARY KEY, Value nvarchar(max) NULL,
                UpdatedBy nvarchar(30) NULL, UpdatedAt datetime2 NOT NULL DEFAULT SYSDATETIME());
            """;
        Execute(cs, sql);
    }

    public static int UserCount(string cs) => Convert.ToInt32(Query(cs, "SELECT COUNT(*) FROM dbo.SYS_User").Rows[0][0]);

    // ================= 使用者 =================

    /// <summary>
    /// 登入：成功回傳使用者與「是否要先改密碼」；失敗回傳原因（密碼錯誤累計，錯太多次鎖定）。
    /// adminOnly = 只有系統管理者能登入（ERP Studio）：密碼正確但不是管理者也算登入失敗（不累計錯誤次數）
    /// </summary>
    public static (SysUser User, bool MustChange, string Error) Login(string cs, string userId, string password, bool adminOnly = false)
    {
        var t = Query(cs, "SELECT UserId, UserName, PasswordHash, IsAdmin, IsActive, MustChangePassword, FailedCount FROM dbo.SYS_User WHERE UserId = @u",
                      ("@u", userId));
        if (t.Rows.Count == 0)
        {
            WriteAudit(cs, userId, null, null, "登入失敗", null, "帳號不存在");
            return (null, false, "帳號或密碼錯誤。");
        }
        var r = t.Rows[0];
        if (!(bool)r["IsActive"]) return (null, false, "這個帳號已停用，請洽系統管理者。");
        if ((int)r["FailedCount"] >= LockAfterFailures) return (null, false, $"密碼錯誤超過 {LockAfterFailures} 次，帳號已鎖定，請洽系統管理者重設密碼。");
        if (!PasswordHasher.Verify(password, (string)r["PasswordHash"]))
        {
            Execute(cs, "UPDATE dbo.SYS_User SET FailedCount = FailedCount + 1 WHERE UserId = @u", ("@u", userId));
            WriteAudit(cs, userId, null, null, "登入失敗", null, "密碼錯誤");
            int left = LockAfterFailures - (int)r["FailedCount"] - 1;
            return (null, false, left > 0 ? $"帳號或密碼錯誤（再錯 {left} 次會鎖定）。" : "密碼錯誤太多次，帳號已鎖定，請洽系統管理者。");
        }
        if (adminOnly && !(bool)r["IsAdmin"])
        {
            WriteAudit(cs, userId, null, null, "登入失敗", null, "不是系統管理者，不能登入 ERP Studio");
            return (null, false, "ERP Studio 只有系統管理者可以登入。一般使用者請使用「ERP 系統」。");
        }
        Execute(cs, "UPDATE dbo.SYS_User SET FailedCount = 0, LastLogin = SYSDATETIME() WHERE UserId = @u", ("@u", userId));
        WriteAudit(cs, userId, null, null, "登入", null, null);
        return (new SysUser((string)r["UserId"], (string)r["UserName"], (bool)r["IsAdmin"]), (bool)r["MustChangePassword"], null);
    }

    public static void CreateUser(string cs, string userId, string userName, string password, bool isAdmin, bool mustChange) =>
        Execute(cs, "INSERT INTO dbo.SYS_User(UserId, UserName, PasswordHash, IsAdmin, MustChangePassword) VALUES (@u, @n, @h, @a, @m)",
                ("@u", userId), ("@n", userName), ("@h", PasswordHasher.Hash(password)), ("@a", isAdmin), ("@m", mustChange));

    /// <summary>設定新密碼（使用者自己改：mustChange = false；管理者重設：mustChange = true，下次登入要改），同時解除鎖定</summary>
    public static void SetPassword(string cs, string userId, string password, bool mustChange) =>
        Execute(cs, "UPDATE dbo.SYS_User SET PasswordHash = @h, MustChangePassword = @m, FailedCount = 0 WHERE UserId = @u",
                ("@u", userId), ("@h", PasswordHasher.Hash(password)), ("@m", mustChange));

    public static bool VerifyPassword(string cs, string userId, string password)
    {
        var t = Query(cs, "SELECT PasswordHash FROM dbo.SYS_User WHERE UserId = @u", ("@u", userId));
        return t.Rows.Count > 0 && PasswordHasher.Verify(password, (string)t.Rows[0][0]);
    }

    public static DataTable LoadUsers(string cs) =>
        Query(cs, $"""
            SELECT UserId, UserName, IsAdmin, IsActive, CAST(CASE WHEN FailedCount >= {LockAfterFailures} THEN 1 ELSE 0 END AS bit) AS IsLocked, LastLogin
            FROM dbo.SYS_User ORDER BY UserId
            """);

    public static void UpdateUser(string cs, string userId, string userName, bool isAdmin, bool isActive) =>
        Execute(cs, "UPDATE dbo.SYS_User SET UserName = @n, IsAdmin = @a, IsActive = @act WHERE UserId = @u",
                ("@u", userId), ("@n", userName), ("@a", isAdmin), ("@act", isActive));

    // ================= 角色與權限 =================

    public static List<(string RoleId, string RoleName)> LoadRoles(string cs) =>
        Query(cs, "SELECT RoleId, RoleName FROM dbo.SYS_Role ORDER BY RoleId").Rows.Cast<DataRow>()
            .Select(r => ((string)r[0], (string)r[1])).ToList();

    public static void SaveRole(string cs, string roleId, string roleName) =>
        Execute(cs, """
            IF EXISTS (SELECT 1 FROM dbo.SYS_Role WHERE RoleId = @r) UPDATE dbo.SYS_Role SET RoleName = @n WHERE RoleId = @r
            ELSE INSERT INTO dbo.SYS_Role(RoleId, RoleName) VALUES (@r, @n)
            """, ("@r", roleId), ("@n", roleName));

    public static void DeleteRole(string cs, string roleId) =>
        Execute(cs, """
            DELETE FROM dbo.SYS_Permission WHERE RoleId = @r; DELETE FROM dbo.SYS_UserRole WHERE RoleId = @r; DELETE FROM dbo.SYS_Role WHERE RoleId = @r
            """, ("@r", roleId));

    public static List<string> LoadUserRoles(string cs, string userId) =>
        Query(cs, "SELECT RoleId FROM dbo.SYS_UserRole WHERE UserId = @u", ("@u", userId)).Rows.Cast<DataRow>().Select(r => (string)r[0]).ToList();

    public static void SaveUserRoles(string cs, string userId, IEnumerable<string> roles)
    {
        using var conn = Open(cs);
        using var tran = conn.BeginTransaction();
        using (var del = Cmd(conn, "DELETE FROM dbo.SYS_UserRole WHERE UserId = @u", ("@u", userId))) { del.Transaction = tran; del.ExecuteNonQuery(); }
        foreach (string role in roles)
        {
            using var ins = Cmd(conn, "INSERT INTO dbo.SYS_UserRole(UserId, RoleId) VALUES (@u, @r)", ("@u", userId), ("@r", role));
            ins.Transaction = tran;
            ins.ExecuteNonQuery();
        }
        tran.Commit();
    }

    private const string PermissionColumns = "CanOpen, CanInsert, CanModify, CanDelete, CanPrint, CanApprove, CanExport";

    /// <summary>某個角色對每個畫面的權限（含註記、職務代理效期）</summary>
    public static Dictionary<string, RolePermission> LoadRolePermissions(string cs, string roleId) =>
        Query(cs, $"SELECT FormKey, {PermissionColumns}, Note, ValidUntil FROM dbo.SYS_Permission WHERE RoleId = @r", ("@r", roleId))
            .Rows.Cast<DataRow>().ToDictionary(r => (string)r[0], r => new RolePermission(ToPermission(r), r["Note"] as string, r["ValidUntil"] as DateTime?),
                                               StringComparer.OrdinalIgnoreCase);

    /// <summary>重存角色的權限：沒勾任何權限、也沒有註記的畫面不存</summary>
    public static void SaveRolePermissions(string cs, string roleId, IDictionary<string, RolePermission> permissions)
    {
        using var conn = Open(cs);
        using var tran = conn.BeginTransaction();
        using (var del = Cmd(conn, "DELETE FROM dbo.SYS_Permission WHERE RoleId = @r", ("@r", roleId))) { del.Transaction = tran; del.ExecuteNonQuery(); }
        foreach (var (formKey, rp) in permissions.Where(p => p.Value.Permission.Any || !string.IsNullOrWhiteSpace(p.Value.Note)))
        {
            var p = rp.Permission;
            using var ins = Cmd(conn, $"""
                INSERT INTO dbo.SYS_Permission(RoleId, FormKey, {PermissionColumns}, Note, ValidUntil) VALUES (@r, @f, @o, @i, @m, @d, @p, @a, @e, @n, @v)
                """, ("@r", roleId), ("@f", formKey), ("@o", p.Open), ("@i", p.Insert), ("@m", p.Modify), ("@d", p.Delete), ("@p", p.Print),
                ("@a", p.Approve), ("@e", p.Export), ("@n", string.IsNullOrWhiteSpace(rp.Note) ? null : rp.Note.Trim()), ("@v", rp.ValidUntil?.Date));
            ins.Transaction = tran;
            ins.ExecuteNonQuery();
        }
        tran.Commit();
    }

    /// <summary>使用者所有角色的權限聯集；過了職務代理效期的不算</summary>
    public static Dictionary<string, FormPermission> LoadUserPermissions(string cs, string userId)
    {
        var result = new Dictionary<string, FormPermission>(StringComparer.OrdinalIgnoreCase);
        var t = Query(cs, """
            SELECT p.FormKey, p.CanOpen, p.CanInsert, p.CanModify, p.CanDelete, p.CanPrint, p.CanApprove, p.CanExport
            FROM dbo.SYS_Permission p JOIN dbo.SYS_UserRole ur ON ur.RoleId = p.RoleId
            WHERE ur.UserId = @u AND (p.ValidUntil IS NULL OR p.ValidUntil >= CAST(GETDATE() AS date))
            """, ("@u", userId));
        foreach (DataRow r in t.Rows)
        {
            string key = (string)r[0];
            var p = ToPermission(r);
            result[key] = result.TryGetValue(key, out var existing) ? existing.Union(p) : p;
        }
        return result;
    }

    private static FormPermission ToPermission(DataRow r) => new((bool)r["CanOpen"], (bool)r["CanInsert"], (bool)r["CanModify"], (bool)r["CanDelete"], (bool)r["CanPrint"],
                                                                 (bool)r["CanApprove"], (bool)r["CanExport"]);

    // ================= 選單 =================

    public static List<MenuNode> LoadMenu(string cs) =>
        Query(cs, "SELECT MenuId, ParentId, Title, FormKey, SortOrder FROM dbo.SYS_Menu ORDER BY SortOrder, MenuId").Rows.Cast<DataRow>()
            .Select(r => new MenuNode
            {
                Id = (int)r["MenuId"],
                ParentId = r["ParentId"] as int?,
                Title = (string)r["Title"],
                FormKey = r["FormKey"] as string,
                SortOrder = (int)r["SortOrder"],
            }).ToList();

    /// <summary>整個選單重存（樹狀：每個節點的子節點依順序），在同一個交易裡</summary>
    public static void SaveMenu(string cs, IList<MenuTree> roots)
    {
        using var conn = Open(cs);
        using var tran = conn.BeginTransaction();
        using (var del = Cmd(conn, "DELETE FROM dbo.SYS_Menu")) { del.Transaction = tran; del.ExecuteNonQuery(); }
        void Insert(IList<MenuTree> nodes, int? parentId)
        {
            int order = 0;
            foreach (var tree in nodes)
            {
                var node = tree.Node;
                using var ins = Cmd(conn, """
                    INSERT INTO dbo.SYS_Menu(ParentId, Title, FormKey, SortOrder) VALUES (@p, @t, @f, @o); SELECT CAST(SCOPE_IDENTITY() AS int)
                    """, ("@p", parentId), ("@t", node.Title), ("@f", string.IsNullOrEmpty(node.FormKey) ? null : node.FormKey), ("@o", order++));
                ins.Transaction = tran;
                int id = (int)ins.ExecuteScalar();
                Insert(tree.Children, id);
            }
        }
        Insert(roots, null);
        tran.Commit();
    }

    // ================= 已發布的畫面、列印樣板 =================

    public static List<PublishedItem> LoadPublished(string cs, string kind = null) =>
        Query(cs, "SELECT FormKey, Kind, Title, Version, PublishedBy, PublishedAt FROM dbo.SYS_Form WHERE @k IS NULL OR Kind = @k ORDER BY FormKey", ("@k", kind))
            .Rows.Cast<DataRow>()
            .Select(r => new PublishedItem((string)r[0], (string)r[1], r[2] as string, (int)r[3], r[4] as string, (DateTime)r[5])).ToList();

    public static string LoadContent(string cs, string formKey)
    {
        var t = Query(cs, "SELECT Content FROM dbo.SYS_Form WHERE FormKey = @f", ("@f", formKey));
        return t.Rows.Count > 0 ? (string)t.Rows[0][0] : null;
    }

    /// <summary>發布（新增或更新版本）：kind = form（畫面定義）或 print（列印樣板）</summary>
    public static int Publish(string cs, string formKey, string kind, string title, string content, string userId)
    {
        using var conn = Open(cs);
        using var cmd = Cmd(conn, """
            IF EXISTS (SELECT 1 FROM dbo.SYS_Form WHERE FormKey = @f)
                UPDATE dbo.SYS_Form SET Kind = @k, Title = @t, Content = @c, Version = Version + 1, PublishedBy = @u, PublishedAt = SYSDATETIME() WHERE FormKey = @f
            ELSE
                INSERT INTO dbo.SYS_Form(FormKey, Kind, Title, Content, PublishedBy) VALUES (@f, @k, @t, @c, @u);
            SELECT Version FROM dbo.SYS_Form WHERE FormKey = @f
            """, ("@f", formKey), ("@k", kind), ("@t", title), ("@c", content), ("@u", userId));
        return (int)cmd.ExecuteScalar();
    }

    // ================= 系統設定值 =================

    /// <summary>讀一個系統設定值（例如登入畫面的企業識別）；沒有設定、或舊的系統資料庫還沒有這個資料表時回傳 null</summary>
    public static string LoadSetting(string cs, string key)
    {
        var t = Query(cs, "IF OBJECT_ID('dbo.SYS_Setting') IS NOT NULL SELECT Value FROM dbo.SYS_Setting WHERE SettingKey = @k", ("@k", key));
        return t.Rows.Count == 0 || t.Rows[0][0] == DBNull.Value ? null : (string)t.Rows[0][0];
    }

    public static void SaveSetting(string cs, string key, string value, string userId)
    {
        EnsureSchema(cs);          // 舊的系統資料庫自動補上 SYS_Setting
        Execute(cs, """
            IF EXISTS (SELECT 1 FROM dbo.SYS_Setting WHERE SettingKey = @k)
                UPDATE dbo.SYS_Setting SET Value = @v, UpdatedBy = @u, UpdatedAt = SYSDATETIME() WHERE SettingKey = @k
            ELSE
                INSERT INTO dbo.SYS_Setting(SettingKey, Value, UpdatedBy) VALUES (@k, @v, @u)
            """, ("@k", key), ("@v", value), ("@u", userId));
    }

    // ================= 稽核紀錄 =================

    public static void WriteAudit(string cs, string userId, string formKey, string tableName, string action, string keyText, string detail) =>
        Execute(cs, """
            INSERT INTO dbo.SYS_AuditLog(UserId, Machine, FormKey, TableName, Action, KeyText, Detail) VALUES (@u, @m, @f, @t, @a, @k, @d)
            """, ("@u", userId), ("@m", Environment.MachineName), ("@f", formKey), ("@t", tableName), ("@a", action),
            ("@k", keyText is { Length: > 400 } ? keyText[..400] : keyText), ("@d", detail));

    /// <summary>查稽核紀錄（條件空白 = 不限），最多 5000 筆，新的在前面</summary>
    public static DataTable QueryAudit(string cs, DateTime from, DateTime to, string userId, string formKey, string action) =>
        Query(cs, """
            SELECT TOP 5000 LogTime AS 時間, UserId AS 使用者, Machine AS 電腦, FormKey AS 畫面, TableName AS 資料表, Action AS 動作, KeyText AS 主鍵, Detail AS 內容
            FROM dbo.SYS_AuditLog
            WHERE LogTime >= @from AND LogTime < @to
              AND (@u IS NULL OR UserId LIKE '%' + @u + '%') AND (@f IS NULL OR FormKey LIKE '%' + @f + '%') AND (@a IS NULL OR Action = @a)
            ORDER BY LogId DESC
            """, ("@from", from), ("@to", to), ("@u", string.IsNullOrWhiteSpace(userId) ? null : userId.Trim()),
            ("@f", string.IsNullOrWhiteSpace(formKey) ? null : formKey.Trim()), ("@a", string.IsNullOrWhiteSpace(action) ? null : action));
}

/// <summary>
/// 畫面定義與列印樣板從哪裡讀：登入後從系統資料庫讀已發布的版本；沒登入（開發時）從檔案讀
/// </summary>
public static class FormStore
{
    /// <summary>畫面定義：name 可以是檔名或完整路徑（登入時只看檔名，到系統資料庫找）</summary>
    public static FormDefinition LoadForm(string name, string folder)
    {
        string key = Path.GetFileNameWithoutExtension(name);
        if (SysContext.Active)
        {
            string json = SysDb.LoadContent(SysContext.Connection, key)
                          ?? throw new FileNotFoundException($"系統裡沒有已發布的畫面「{key}」，請管理者先在畫面設計發布。");
            var def = FormDefinition.FromJson(json);
            def.FormKey = key;
            return def;
        }
        string path = ButtonRunner.ResolvePath(name, folder);
        if (!File.Exists(path) && !path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) path += ".json";
        return FormDefinition.Load(path);
    }

    /// <summary>列印樣板：登入時到系統資料庫找同檔名的樣板，沒登入從檔案讀</summary>
    public static PrintTemplate LoadTemplate(string name, string folder)
    {
        if (SysContext.Active)
        {
            string key = Path.GetFileName(name);
            string json = SysDb.LoadContent(SysContext.Connection, key)
                          ?? throw new FileNotFoundException($"系統裡沒有已發布的列印樣板「{key}」，請管理者在畫面設計重新發布這個畫面。");
            return PrintTemplate.FromJson(json);
        }
        string path = ButtonRunner.ResolvePath(name, folder);
        if (path == null || !File.Exists(path)) throw new FileNotFoundException("找不到列印樣板檔：" + path);
        return PrintTemplate.Load(path);
    }
}
