using System.Data;

namespace FormBuilder;

/// <summary>
/// 使用者、角色與權限（管理者用）：
/// 「使用者」頁籤新增帳號、重設密碼（同時解鎖）、改姓名 / 管理者 / 啟用，右邊勾選所屬角色；
/// 「角色與權限」頁籤管理角色，勾選每個角色對各個已發布畫面的開啟、編修（新增 / 修改 / 刪除）、核准、報表、輸出權限
/// </summary>
public partial class UserAdminForm : Form
{
    private static string Cs => SysContext.Connection;
    private DataTable users;
    private readonly DataTable perms = new();
    private bool loadingRoles;

    /// <summary>權限勾選欄：資料欄名稱、顯示名稱（編修 = 新增、修改、刪除；報表 = 列印）</summary>
    private static readonly (string Name, string Header, string Tip)[] PermFlags =
    {
        ("Open", "開啟", "可以在選單看到並開啟這個畫面"),
        ("Modify", "編修", "新增、修改、刪除資料（含批次刪除）、單身明細、執行SQL 按鈕"),
        ("Approve", "核准", "生效 / 取消生效（含批次）；系統管理者不受限"),
        ("Print", "報表", "列印、列印報表"),
        ("Export", "輸出", "清單匯出 Excel / CSV"),
    };

    private sealed record RoleItem(string Id, string Name)
    {
        public override string ToString() => $"{Name}（{Id}）";
    }

    public UserAdminForm()
    {
        InitializeComponent();
        // ---- 使用者 ----
        dgvUsers.SelectionChanged += (s, e) => ShowUserRoles();
        clbRoles.ItemCheck += (s, e) => { if (!loadingRoles) BeginInvoke(SaveUserRoles); };     // 勾選狀態變更後才存
        btnNewUser.Click += (s, e) => NewUser();
        btnResetPwd.Click += (s, e) => ResetPassword();
        btnSaveUsers.Click += (s, e) => SaveUsers();

        // ---- 角色與權限 ----
        perms.Columns.Add("FormKey", typeof(string));
        perms.Columns.Add("Title", typeof(string));
        foreach (var f in PermFlags) perms.Columns.Add(f.Name, typeof(bool));
        perms.Columns.Add("Note", typeof(string));
        perms.Columns.Add("ValidUntil", typeof(DateTime));
        SetupPermColumns();
        dgvPerms.DataSource = perms;
        dgvPerms.DataError += (s, e) => e.ThrowException = false;
        // 職務代理效期：選日期；按 Delete 清掉（= 不限）
        dgvPerms.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Delete && dgvPerms.CurrentCell?.OwningColumn.DataPropertyName is "ValidUntil" or "Note"
                && dgvPerms.CurrentRow?.DataBoundItem is DataRowView v)
            {
                v[dgvPerms.CurrentCell.OwningColumn.DataPropertyName] = DBNull.Value;
                e.Handled = true;
            }
        };
        lstRoles.SelectedIndexChanged += (s, e) => LoadPerms();
        btnNewRole.Click += (s, e) => NewRole();
        btnRenameRole.Click += (s, e) => RenameRole();
        btnDeleteRole.Click += (s, e) => DeleteRole();
        btnAllPerms.Click += (s, e) => SetAllPerms(true);
        btnNoPerms.Click += (s, e) => SetAllPerms(false);
        btnSavePerms.Click += (s, e) => SavePerms();
        // 勾「編修 / 核准 / 報表 / 輸出」時自動勾「開啟」（沒有開啟權限其他都用不到）
        dgvPerms.CellValueChanged += (s, e) =>
        {
            if (e.RowIndex < 0 || dgvPerms.Columns[e.ColumnIndex].DataPropertyName is not ("Modify" or "Approve" or "Print" or "Export")) return;
            if (dgvPerms.Rows[e.RowIndex].DataBoundItem is DataRowView v && v[dgvPerms.Columns[e.ColumnIndex].DataPropertyName] is true) v["Open"] = true;
        };
        dgvPerms.CurrentCellDirtyStateChanged += (s, e) => { if (dgvPerms.IsCurrentCellDirty) dgvPerms.CommitEdit(DataGridViewDataErrorContexts.Commit); };

        LoadUsers();
        LoadRoles();
    }

    // ================= 使用者 =================

    private void LoadUsers()
    {
        users = SysDb.LoadUsers(Cs);
        dgvUsers.DataSource = users;
        var headers = new Dictionary<string, string>
        {
            ["UserId"] = "帳號", ["UserName"] = "姓名", ["IsAdmin"] = "管理者", ["IsActive"] = "啟用", ["IsLocked"] = "已鎖定", ["LastLogin"] = "最後登入",
        };
        foreach (DataGridViewColumn col in dgvUsers.Columns)
        {
            col.HeaderText = headers.GetValueOrDefault(col.DataPropertyName, col.HeaderText);
            col.ReadOnly = col.DataPropertyName is "UserId" or "IsLocked" or "LastLogin";
        }
        if (dgvUsers.Columns["LastLogin"] is { } last) last.DefaultCellStyle.Format = "yyyy/MM/dd HH:mm";
    }

    private string SelectedUser => dgvUsers.CurrentRow?.DataBoundItem is DataRowView v ? (string)v["UserId"] : null;

    private void ShowUserRoles()
    {
        loadingRoles = true;
        try
        {
            string userId = SelectedUser;
            var mine = userId == null ? new List<string>() : SysDb.LoadUserRoles(Cs, userId);
            for (int i = 0; i < clbRoles.Items.Count; i++)
                clbRoles.SetItemChecked(i, clbRoles.Items[i] is RoleItem r && mine.Contains(r.Id, StringComparer.OrdinalIgnoreCase));
            clbRoles.Enabled = userId != null;
        }
        finally
        {
            loadingRoles = false;
        }
    }

    private void SaveUserRoles()
    {
        string userId = SelectedUser;
        if (userId == null) return;
        var roles = clbRoles.CheckedItems.OfType<RoleItem>().Select(r => r.Id).ToList();
        SysDb.SaveUserRoles(Cs, userId, roles);
        Audit.Write(null, "SYS_UserRole", "權限", "UserId=" + userId, "角色：" + string.Join("、", roles));
    }

    private void NewUser()
    {
        var v = PromptDialog.Ask(this, "新增使用者", values =>
        {
            if (values[0] == "" || values[1] == "") return "請輸入帳號與姓名。";
            if (users.Rows.Cast<DataRow>().Any(r => string.Equals((string)r["UserId"], values[0], StringComparison.OrdinalIgnoreCase))) return "這個帳號已經有了。";
            return PasswordHasher.CheckStrength(values[2]);
        },
            new PromptDialog.Field("帳號"), new PromptDialog.Field("姓名"), new PromptDialog.Field("初始密碼", Password: true));
        if (v == null) return;
        SysDb.CreateUser(Cs, v[0], v[1], v[2], isAdmin: false, mustChange: true);
        Audit.Write(null, "SYS_User", "新增帳號", "UserId=" + v[0], "姓名：" + v[1]);
        LoadUsers();
        MessageBox.Show($"已新增「{v[1]}」。第一次登入時會要求改密碼；記得在右邊勾選角色。", Text);
    }

    private void ResetPassword()
    {
        string userId = SelectedUser;
        if (userId == null) return;
        var v = PromptDialog.Ask(this, $"重設「{userId}」的密碼", values => PasswordHasher.CheckStrength(values[0]),
                                 new PromptDialog.Field("新密碼", Password: true));
        if (v == null) return;
        SysDb.SetPassword(Cs, userId, v[0], mustChange: true);
        Audit.Write(null, "SYS_User", "重設密碼", "UserId=" + userId, "同時解除鎖定");
        LoadUsers();
        MessageBox.Show("已重設密碼並解除鎖定，使用者下次登入時要改密碼。", Text);
    }

    private void SaveUsers()
    {
        dgvUsers.EndEdit();
        int count = 0;
        foreach (DataRow r in users.Rows)
        {
            if (r.RowState != DataRowState.Modified) continue;
            string userId = (string)r["UserId"];
            // 不能把自己的管理者或啟用拿掉（避免沒有人能管理系統）
            if (userId.Equals(SysContext.User.UserId, StringComparison.OrdinalIgnoreCase) && (!(bool)r["IsAdmin"] || !(bool)r["IsActive"]))
            {
                MessageBox.Show("不能取消自己的管理者或啟用。", Text);
                return;
            }
            SysDb.UpdateUser(Cs, userId, Convert.ToString(r["UserName"]).Trim(), (bool)r["IsAdmin"], (bool)r["IsActive"]);
            Audit.Write(null, "SYS_User", "修改帳號", "UserId=" + userId, $"姓名={r["UserName"]}, 管理者={r["IsAdmin"]}, 啟用={r["IsActive"]}");
            count++;
        }
        LoadUsers();
        MessageBox.Show(count == 0 ? "沒有修改。" : $"已儲存 {count} 個使用者。", Text);
    }

    // ================= 角色與權限 =================

    private void LoadRoles(string select = null)
    {
        var roles = SysDb.LoadRoles(Cs).Select(r => new RoleItem(r.RoleId, r.RoleName)).ToList();
        lstRoles.Items.Clear();
        lstRoles.Items.AddRange(roles.ToArray());
        clbRoles.Items.Clear();
        clbRoles.Items.AddRange(roles.ToArray());
        ShowUserRoles();
        int index = roles.FindIndex(r => r.Id == select);
        if (lstRoles.Items.Count > 0) lstRoles.SelectedIndex = Math.Max(0, index);
        else LoadPerms();
    }

    private RoleItem SelectedRole => lstRoles.SelectedItem as RoleItem;

    private void LoadPerms()
    {
        perms.Rows.Clear();
        var role = SelectedRole;
        dgvPerms.Enabled = btnSavePerms.Enabled = btnAllPerms.Enabled = btnNoPerms.Enabled = role != null;
        if (role == null) return;
        var current = SysDb.LoadRolePermissions(Cs, role.Id);
        foreach (var f in SysDb.LoadPublished(Cs, "form"))
        {
            var rp = current.GetValueOrDefault(f.FormKey) ?? new RolePermission(FormPermission.None, null, null);
            var p = rp.Permission;
            perms.Rows.Add(f.FormKey, f.Title ?? f.FormKey, p.Open, p.Modify, p.Approve, p.Print, p.Export,
                           (object)rp.Note ?? DBNull.Value, rp.ValidUntil.HasValue ? rp.ValidUntil.Value : DBNull.Value);
        }
        perms.AcceptChanges();
        lblPermHint.Text = $"角色「{role.Name}」對每個已發布畫面的權限（使用者有多個角色時取聯集）";
    }

    /// <summary>權限表的欄位：畫面代號、名稱、七個權限勾選、註記、職務代理效期（日期選擇）</summary>
    private void SetupPermColumns()
    {
        dgvPerms.AutoGenerateColumns = false;
        dgvPerms.Columns.Clear();
        dgvPerms.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "FormKey", HeaderText = "畫面代號", Width = 150, ReadOnly = true, Frozen = true });
        dgvPerms.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Title", HeaderText = "畫面名稱", Width = 150, ReadOnly = true, Frozen = true });
        foreach (var (name, header, tip) in PermFlags)
            dgvPerms.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = name, HeaderText = header, Width = 48, ToolTipText = tip });
        dgvPerms.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Note", HeaderText = "註記", Width = 200, MaxInputLength = 150 });
        dgvPerms.Columns.Add(new CalendarColumn { DataPropertyName = "ValidUntil", HeaderText = "職務代理效期", Width = 110,
                                                  ToolTipText = "過了這天這組權限就失效（例如職務代理到期）；空白 = 不限，按 Delete 清除" });
        GridCells.ApplyGridLines(dgvPerms);
    }

    private void SetAllPerms(bool value)
    {
        foreach (DataRow r in perms.Rows)
            foreach (var f in PermFlags) r[f.Name] = value;
    }

    private void SavePerms()
    {
        var role = SelectedRole;
        if (role == null) return;
        dgvPerms.EndEdit();
        var map = perms.Rows.Cast<DataRow>().ToDictionary(r => (string)r["FormKey"],
            // 新增、刪除跟著「編修」（資料庫的新增、刪除欄位存一樣的值）
            r => new RolePermission(new FormPermission((bool)r["Open"], (bool)r["Modify"], (bool)r["Modify"], (bool)r["Modify"], (bool)r["Print"],
                                                       (bool)r["Approve"], (bool)r["Export"]),
                                    r["Note"] as string, r["ValidUntil"] as DateTime?));
        SysDb.SaveRolePermissions(Cs, role.Id, map);
        Audit.Write(null, "SYS_Permission", "權限", "RoleId=" + role.Id,
                    map.Where(p => p.Value.Permission.Any).ToDictionary(p => p.Key, p => DescribePermission(p.Value)));
        perms.AcceptChanges();
        MessageBox.Show($"角色「{role.Name}」的權限已儲存（使用者重新整理選單或重新登入後生效）。", Text);
    }

    /// <summary>稽核紀錄用：「開啟、新增、核准（到 2026/12/31）」</summary>
    private static string DescribePermission(RolePermission rp)
    {
        var p = rp.Permission;
        var names = new (bool On, string Name)[] { (p.Open, "開啟"), (p.Modify, "編修"), (p.Approve, "核准"), (p.Print, "報表"), (p.Export, "輸出") }
            .Where(x => x.On).Select(x => x.Name);
        return string.Join("、", names) + (rp.ValidUntil.HasValue ? $"（到 {rp.ValidUntil:yyyy/MM/dd}）" : "");
    }

    private void NewRole()
    {
        var v = PromptDialog.Ask(this, "新增角色", values =>
            values[0] == "" || values[1] == "" ? "請輸入角色代號與名稱。"
            : lstRoles.Items.OfType<RoleItem>().Any(r => r.Id.Equals(values[0], StringComparison.OrdinalIgnoreCase)) ? "這個角色代號已經有了。" : null,
            new PromptDialog.Field("角色代號"), new PromptDialog.Field("角色名稱"));
        if (v == null) return;
        SysDb.SaveRole(Cs, v[0], v[1]);
        Audit.Write(null, "SYS_Role", "新增角色", "RoleId=" + v[0], v[1]);
        LoadRoles(v[0]);
    }

    private void RenameRole()
    {
        var role = SelectedRole;
        if (role == null) return;
        var v = PromptDialog.Ask(this, "角色改名", values => values[0] == "" ? "請輸入角色名稱。" : null, new PromptDialog.Field("角色名稱", role.Name));
        if (v == null) return;
        SysDb.SaveRole(Cs, role.Id, v[0]);
        LoadRoles(role.Id);
    }

    private void DeleteRole()
    {
        var role = SelectedRole;
        if (role == null) return;
        if (MessageBox.Show($"確定要刪除角色「{role.Name}」嗎？屬於這個角色的使用者會失去它的權限。", Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        SysDb.DeleteRole(Cs, role.Id);
        Audit.Write(null, "SYS_Role", "刪除角色", "RoleId=" + role.Id, role.Name);
        LoadRoles();
    }
}
