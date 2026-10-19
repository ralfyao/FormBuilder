namespace FormBuilder;

/// <summary>使用者端的畫面清單：列出資料夾裡所有畫面定義檔，雙擊開啟</summary>
public class LauncherForm : Form
{
    private readonly string folder;
    private readonly ListBox lstForms = new() { Dock = DockStyle.Fill, IntegralHeight = false };

    public LauncherForm(string formsFolder)
    {
        folder = formsFolder;
        Text = "畫面清單";
        Font = new Font("Microsoft JhengHei UI", 10F);
        Size = new Size(400, 500);
        StartPosition = FormStartPosition.CenterScreen;

        var btnRefresh = new Button { Text = "重新整理", Dock = DockStyle.Bottom, Height = 34 };
        btnRefresh.Click += (s, e) => LoadList();
        lstForms.DoubleClick += (s, e) => OpenSelected();
        lstForms.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) OpenSelected(); };

        Controls.Add(lstForms);
        Controls.Add(btnRefresh);
        LoadList();
    }

    private void LoadList()
    {
        lstForms.Items.Clear();
        if (!Directory.Exists(folder))
        {
            Text = $"畫面清單（找不到資料夾：{folder}）";
            return;
        }
        foreach (string path in Directory.GetFiles(folder, "*.json").OrderBy(p => p))
            lstForms.Items.Add(Path.GetFileNameWithoutExtension(path));
        Text = $"畫面清單（{lstForms.Items.Count} 個畫面）";
    }

    private void OpenSelected()
    {
        if (lstForms.SelectedItem is not string name) return;
        try
        {
            var def = FormDefinition.Load(Path.Combine(folder, name + ".json"));
            new RuntimeForm(def).Show(this);
        }
        catch (Exception ex)
        {
            MessageBox.Show("開啟畫面失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
