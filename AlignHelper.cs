namespace FormBuilder;

/// <summary>設計畫面上的對齊工具：拖曳吸附、靠左 / 靠上對齊、同寬、等距排列（主設計畫面與編輯視窗版面共用）</summary>
public static class AlignHelper
{
    /// <summary>拖曳時離其他元件的左邊 / 上邊幾個像素內就自動貼齊</summary>
    public const int Magnet = 8;

    /// <summary>
    /// 拖曳吸附：被拖的元件（lead）新位置靠近其他元件的 X 或 Y 時，調整位移讓它貼齊。
    /// 按住 Alt 拖曳可以暫時不吸附
    /// </summary>
    public static (int Dx, int Dy) Magnetize(Point leadStart, int dx, int dy, IEnumerable<DesignBox> others)
    {
        if (Control.ModifierKeys.HasFlag(Keys.Alt)) return (dx, dy);
        int x = leadStart.X + dx, y = leadStart.Y + dy;
        int bestDx = Magnet + 1, bestDy = Magnet + 1;
        int? snapX = null, snapY = null;
        foreach (var o in others)
        {
            var p = o.ModelLocation;
            if (Math.Abs(p.X - x) < bestDx) { bestDx = Math.Abs(p.X - x); snapX = p.X; }
            if (Math.Abs(p.Y - y) < bestDy) { bestDy = Math.Abs(p.Y - y); snapY = p.Y; }
        }
        return (snapX.HasValue ? snapX.Value - leadStart.X : dx, snapY.HasValue ? snapY.Value - leadStart.Y : dy);
    }

    /// <summary>靠左對齊：全部對齊到最左邊那個元件的 X</summary>
    public static void AlignLeft(IList<DesignBox> boxes)
    {
        if (boxes.Count < 2) return;
        int x = boxes.Min(b => b.ModelLocation.X);
        foreach (var b in boxes) b.SetModelLocation(x, b.ModelLocation.Y);
    }

    /// <summary>靠上對齊（排成同一行）：全部對齊到最上面那個元件的 Y</summary>
    public static void AlignTop(IList<DesignBox> boxes)
    {
        if (boxes.Count < 2) return;
        int y = boxes.Min(b => b.ModelLocation.Y);
        foreach (var b in boxes) b.SetModelLocation(b.ModelLocation.X, y);
    }

    /// <summary>同寬：欄位的輸入框寬度都改成第一個選取欄位的寬度</summary>
    public static void SameWidth(IList<DesignBox> boxes)
    {
        var fields = boxes.OfType<FieldBox>().ToList();
        if (fields.Count < 2) return;
        int width = fields[0].Field.Width;
        foreach (var f in fields)
        {
            f.Field.Width = width;
            f.RefreshFromModel();
        }
    }

    /// <summary>水平等距：依左右順序，讓左邊緣間隔一樣（最左、最右不動）</summary>
    public static void DistributeHorizontally(IList<DesignBox> boxes)
    {
        if (boxes.Count < 3) return;
        var sorted = boxes.OrderBy(b => b.ModelLocation.X).ToList();
        int first = sorted[0].ModelLocation.X, last = sorted[^1].ModelLocation.X;
        double step = (last - first) / (double)(sorted.Count - 1);
        for (int i = 1; i < sorted.Count - 1; i++)
            sorted[i].SetModelLocation(first + (int)Math.Round(step * i), sorted[i].ModelLocation.Y);
    }

    /// <summary>垂直等距：依上下順序，讓上邊緣間隔一樣（最上、最下不動）</summary>
    public static void DistributeVertically(IList<DesignBox> boxes)
    {
        if (boxes.Count < 3) return;
        var sorted = boxes.OrderBy(b => b.ModelLocation.Y).ToList();
        int first = sorted[0].ModelLocation.Y, last = sorted[^1].ModelLocation.Y;
        double step = (last - first) / (double)(sorted.Count - 1);
        for (int i = 1; i < sorted.Count - 1; i++)
            sorted[i].SetModelLocation(sorted[i].ModelLocation.X, first + (int)Math.Round(step * i));
    }

    /// <summary>建立「對齊」按鈕們，加到工具列；getSelection 取目前選取的元件，after 在調整後呼叫（重算畫面大小等）</summary>
    public static void AddAlignButtons(ToolStrip toolStrip, Func<IList<DesignBox>> getSelection, Action after)
    {
        void Add(string text, Action<IList<DesignBox>> action, int min, string tip)
        {
            var b = new ToolStripButton(text) { ToolTipText = tip };
            b.Click += (s, e) =>
            {
                var boxes = getSelection();
                if (boxes.Count < min)
                {
                    MessageBox.Show($"請先選取至少 {min} 個欄位（圈選或按住 Ctrl 點選）。", "提醒");
                    return;
                }
                action(boxes);
                after();
            };
            toolStrip.Items.Add(b);
        }
        toolStrip.Items.Add(new ToolStripLabel("對齊："));
        Add("靠左", AlignLeft, 2, "選取的欄位對齊到最左邊那個");
        Add("靠上", AlignTop, 2, "選取的欄位排成同一行（對齊最上面那個）");
        Add("同寬", SameWidth, 2, "輸入框寬度都改成第一個選取欄位的寬度");
        Add("水平等距", DistributeHorizontally, 3, "依左右順序平均分配間距");
        Add("垂直等距", DistributeVertically, 3, "依上下順序平均分配間距");
        toolStrip.Items.Add(new ToolStripLabel("　拖曳時靠近其他欄位會自動貼齊（按住 Alt 不貼齊）") { ForeColor = SystemColors.GrayText });
    }
}
