using System.Data;

namespace FormBuilder;

/// <summary>
/// 使用者、角色與權限（管理者用）：
/// 「使用者」頁籤新增帳號、重設密碼（同時解鎖）、改姓名 / 管理者 / 啟用，右邊勾選所屬角色；
/// 「角色與權限」頁籤管理角色，勾選每個角色對各個已發布畫面的開啟、新增、修改、刪除、列印權限
/// </summary>
public partial class UserAdminForm : Form
{
    private static string Cs => SysContext.Connection;
    private DataTable users;
    private readonly DataTable perms = new();
    private bool loadingRoles;

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
        foreach (string c in new[] { "Open", "Insert", "Modify", "Delete", "Print" }) perms.Columns.Add(c, typeof(bool));
        dgvPerms.DataSource = perms;
        dgvPerms.DataBindingComplete += (s, e) => SetupPermColumns();
        lstRoles.SelectedIndexChanged += (s, e) => LoadPerms();
        btnNewRole.Click += (s, e) => NewRole();
        btnRenameRole.Click += (s, e) => RenameRole();
        btnDeleteRole.Click += (s, e) => DeleteRole();
        btnAllPerms.Click += (s, e) => SetAllPerms(true);
        btnNoPerms.Click += (s, e) => SetAllPerms(false);
        btnSavePerms.Click += (s, e) => SavePerms();
        // 勾「新增 / 修改 / 刪除 / 列印」時自動勾「開啟」（沒有開啟權限其他都用不到）
        dgvPerms.CellValueChanged += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 3) return;
            if (perms.Rows[e.RowIndex][e.ColumnIndex] is true) perms.Rows[e.RowIndex]["Open"] = true;
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
            var p = current.GetValueOrDefault(f.FormKey, FormPermission.None);
            perms.Rows.Add(f.FormKey, f.Title ?? f.FormKey, p.Open, p.Insert, p.Modify, p.Delete, p.Print);
        }
        perms.AcceptChanges();
        lblPermHint.Text = $"角色「{role.Name}」對每個已發布畫面的權限（使用者有多個角色時取聯集）";
    }

    private void SetupPermColumns()
    {
        var headers = new Dictionary<string, string>
        {
            ["FormKey"] = "畫面代號", ["Title"] = "畫面名稱", ["Open"] = "開啟", ["Insert"] = "新增", ["Modify"] = "修改", ["Delete"] = "刪除", ["Print"] = "列印",
        };
        foreach (DataGridViewColumn col in dgvPerms.Columns)
        {
            col.HeaderText = headers.GetValueOrDefault(col.DataPropertyName, col.HeaderText);
            col.ReadOnly = col.DataPropertyName is "FormKey" or "Title";
            col.Width = col.DataPropertyName switch { "FormKey" => 180, "Title" => 200, _ => 56 };
        }
    }

    private void SetAllPerms(bool value)
    {
        foreach (DataRow r in perms.Rows)
            foreach (string c in new[] { "Open", "Insert", "Modify", "Delete", "Print" }) r[c] = value;
    }

    private void SavePerms()
    {
        var role = SelectedRole;
        if (role == null) return;
        dgvPerms.EndEdit();
        var map = perms.Rows.Cast<DataRow>().ToDictionary(r => (string)r["FormKey"],
            r => new FormPermission((bool)r["Open"], (bool)r["Insert"], (bool)r["Modify"], (bool)r["Delete"], (bool)r["Print"]));
        SysDb.SaveRolePermissions(Cs, role.Id, map);
        Audit.Write(null, "SYS_Permission", "權限", "RoleId=" + role.Id, map.Where(p => p.Value.Open).ToDictionary(p => p.Key, p => p.Value.ToString()));
        perms.AcceptChanges();
        MessageBox.Show($"角色「{role.Name}」的權限已儲存（使用者重新整理選單或重新登入後生效）。", Text);
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
