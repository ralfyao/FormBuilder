using System.Globalization;

namespace FormBuilder;

/// <summary>
/// 多欄清單的下拉選單（FieldDefinition.ItemsGrid）：拉下來時不用原生的單欄清單，改成表格列出「選項 SQL」的每個欄位（欄位名稱當標題），
/// 點一列（或用方向鍵選、按 Enter）就選這個選項，跟使用者在原生清單選一樣觸發 SelectionChangeCommitted（執行後動作、選取回傳對應都照用）。
/// 值、顯示照舊放在 Items（ComboItem），取值、設值不用改
/// </summary>
public sealed class GridComboBox : ComboBox
{
    private ToolStripDropDown popup;
    private ListView list;
    /// <summary>剛關掉表格：點下拉箭頭關表格時不要馬上又打開</summary>
    private DateTime closedAt;

    /// <summary>表格裡最多一次看到幾列，超過就捲動</summary>
    private const int VisibleRows = 15;

    public GridComboBox()
    {
        // 原生清單一打開就關掉，換成表格（開窗選取也是同樣的作法）
        DropDown += (s, e) => BeginInvoke(() =>
        {
            DroppedDown = false;
            if ((DateTime.Now - closedAt).TotalMilliseconds > 250) ShowGrid();
        });
    }

    /// <summary>表格裡的值怎麼顯示：數字加千分號（小數位數照資料庫），日期 yyyy/MM/dd</summary>
    public static string Format(object value) => value switch
    {
        null or DBNull => "",
        decimal d => d.ToString("N" + ((decimal.GetBits(d)[3] >> 16) & 0xFF), CultureInfo.CurrentCulture),
        double or float => Convert.ToDouble(value).ToString("#,0.##########", CultureInfo.CurrentCulture),
        DateTime t => t.TimeOfDay == TimeSpan.Zero ? t.ToString("yyyy/MM/dd", CultureInfo.InvariantCulture) : t.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture),
        bool b => b ? "是" : "否",
        _ => Convert.ToString(value, CultureInfo.CurrentCulture),
    };

    private static bool IsNumber(object value) => value is decimal or double or float or int or long or short or byte;

    private void ShowGrid()
    {
        if (!Enabled || Items.Count == 0) return;
        var columns = Items.OfType<ComboItem>().FirstOrDefault(i => i.Columns != null)?.Columns ?? new[] { "" };
        var firstRow = Items.OfType<ComboItem>().FirstOrDefault(i => i.Cells != null)?.Cells;

        list?.Dispose();
        popup?.Dispose();
        list = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            MultiSelect = false,
            HideSelection = false,
            HeaderStyle = ColumnHeaderStyle.Nonclickable,
            BorderStyle = BorderStyle.FixedSingle,
            Font = Font,
        };
        for (int c = 0; c < columns.Count; c++)
            list.Columns.Add(columns[c], -2,
                             firstRow != null && c < firstRow.Length && IsNumber(firstRow[c]) ? HorizontalAlignment.Right : HorizontalAlignment.Left);
        list.BeginUpdate();
        foreach (var obj in Items)
        {
            var item = obj as ComboItem;
            string[] texts = item?.Cells != null
                ? Enumerable.Range(0, columns.Count).Select(c => c < item.Cells.Length ? Format(item.Cells[c]) : "").ToArray()
                : new[] { item?.Text ?? Convert.ToString(obj) }.Concat(Enumerable.Repeat("", Math.Max(0, columns.Count - 1))).ToArray();
            list.Items.Add(new ListViewItem(texts) { Tag = obj });
        }
        list.EndUpdate();

        // 欄寬：內容與標題取寬的
        _ = list.Handle;
        var widths = new int[list.Columns.Count];
        list.AutoResizeColumns(ColumnHeaderAutoResizeStyle.ColumnContent);
        for (int c = 0; c < widths.Length; c++) widths[c] = list.Columns[c].Width;
        list.AutoResizeColumns(ColumnHeaderAutoResizeStyle.HeaderSize);
        for (int c = 0; c < widths.Length; c++) list.Columns[c].Width = Math.Max(widths[c], list.Columns[c].Width) + 8;

        int rowHeight = list.Items.Count > 0 ? list.GetItemRect(0).Height : Font.Height + 4;
        int headerHeight = list.Items.Count > 0 ? list.GetItemRect(0).Top : Font.Height + 8;
        int rows = Math.Min(Math.Max(list.Items.Count, 1), VisibleRows);
        bool scroll = list.Items.Count > VisibleRows;
        int width = list.Columns.Cast<ColumnHeader>().Sum(c => c.Width) + (scroll ? SystemInformation.VerticalScrollBarWidth : 0) + 4;
        list.Size = new Size(Math.Max(width, Width), headerHeight + rows * rowHeight + 4);

        // 標出目前選的那一列
        if (SelectedIndex >= 0 && SelectedIndex < list.Items.Count)
        {
            list.Items[SelectedIndex].Selected = list.Items[SelectedIndex].Focused = true;
            list.EnsureVisible(SelectedIndex);
        }

        list.MouseClick += (s, e) => { if (list.GetItemAt(e.X, e.Y) is { } hit) Commit(hit.Index); };
        list.KeyDown += (s, e) =>
        {
            if (e.KeyCode == Keys.Enter && list.SelectedIndices.Count > 0) { Commit(list.SelectedIndices[0]); e.Handled = true; }
            else if (e.KeyCode == Keys.Escape) { popup.Close(); e.Handled = true; }
        };

        popup = new ToolStripDropDown { Padding = Padding.Empty, AutoClose = true };
        popup.Items.Add(new ToolStripControlHost(list) { Padding = Padding.Empty, Margin = Padding.Empty, AutoSize = false, Size = list.Size });
        popup.Closed += (s, e) => { closedAt = DateTime.Now; if (!IsDisposed) Focus(); };
        popup.Show(this, new Point(0, Height));
        list.Focus();
    }

    /// <summary>選了表格的第 index 列：跟原生清單一樣設定 SelectedIndex 並觸發 SelectionChangeCommitted</summary>
    private void Commit(int index)
    {
        popup?.Close();
        if (index < 0 || index >= Items.Count) return;
        SelectedIndex = index;
        OnSelectionChangeCommitted(EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            list?.Dispose();
            popup?.Dispose();
        }
        base.Dispose(disposing);
    }
}
