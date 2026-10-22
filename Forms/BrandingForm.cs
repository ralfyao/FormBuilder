namespace FormBuilder;

/// <summary>
/// 登入畫面的企業識別：標題、副標題、標誌、橫幅 / 文字 / 登入按鈕的顏色、頁尾文字。
/// 存進系統資料庫，ERP 系統和 ERP Studio 的登入畫面下次開啟時就會套用（每台電腦會快取一份，開啟時馬上顯示）
/// </summary>
public partial class BrandingForm : Form
{
    private readonly LoginBranding branding;

    public BrandingForm()
    {
        InitializeComponent();
        try
        {
            branding = LoginBranding.Load(SysContext.Connection);
        }
        catch (Exception ex)
        {
            MessageBox.Show("讀取企業識別失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            branding = new LoginBranding();
        }
        txtTitle.Text = branding.Title ?? "";
        txtSubtitle.Text = branding.Subtitle ?? "";
        txtFooter.Text = branding.Footer ?? "";
        ShowLogo();
        ShowColors();

        btnLogo.Click += (s, e) => PickLogo();
        btnClearLogo.Click += (s, e) => { branding.SetLogo(null); ShowLogo(); };
        btnBanner.Click += (s, e) => PickColor(c => branding.BannerColor = c, branding.Banner);
        btnText.Click += (s, e) => PickColor(c => branding.TextColor = c, branding.Text);
        btnButton.Click += (s, e) => PickColor(c => branding.ButtonColor = c, branding.Button ?? SystemColors.Control);
        btnDefaultColors.Click += (s, e) =>
        {
            branding.BannerColor = branding.TextColor = branding.ButtonColor = null;
            ShowColors();
        };
        btnPreview.Click += (s, e) => LoginForm.Preview(this, Collect(), "ERP 系統");
        btnSave.Click += (s, e) => Save();
    }

    private LoginBranding Collect()
    {
        branding.Title = Blank(txtTitle.Text);
        branding.Subtitle = Blank(txtSubtitle.Text);
        branding.Footer = Blank(txtFooter.Text);
        return branding;
    }

    private static string Blank(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private void ShowLogo()
    {
        var old = picLogo.Image;
        picLogo.Image = branding.LogoImage();
        old?.Dispose();
        btnClearLogo.Enabled = picLogo.Image != null;
    }

    private void ShowColors()
    {
        btnBanner.BackColor = branding.Banner;
        btnText.BackColor = branding.Text;
        btnButton.BackColor = branding.Button ?? SystemColors.Control;
        btnButton.Text = branding.Button == null ? "預設" : "";
    }

    private void PickColor(Action<string> set, Color current)
    {
        using var dlg = new ColorDialog { Color = current, FullOpen = true };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        set(LoginBranding.ToHtml(dlg.Color));
        ShowColors();
    }

    private void PickLogo()
    {
        using var dlg = new OpenFileDialog { Title = "選擇標誌圖片", Filter = "圖片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.ico|所有檔案|*.*" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try
        {
            using var image = Image.FromFile(dlg.FileName);
            branding.SetLogo(image);
            ShowLogo();
        }
        catch (Exception ex)
        {
            MessageBox.Show("讀取圖片失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Save()
    {
        try
        {
            Collect().Save(SysContext.Connection, SysContext.User.UserId);
            Audit.Write(null, "SYS_Setting", "修改登入畫面", LoginBranding.SettingKey);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            MessageBox.Show("儲存失敗：" + ex.Message, "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
