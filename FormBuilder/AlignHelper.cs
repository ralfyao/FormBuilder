namespace FormBuilder;

/// <summary>
/// 設計畫面上的對齊工具（主設計畫面與編輯視窗版面共用）：
/// 拖曳吸附、水平（靠左 / 置中 / 靠右）與垂直（靠上 / 置中 / 靠下）對齊、同寬、同標籤寬、等距排列。
/// 欄位的「左」以輸入框左緣為準（AlignX），標籤寬度不同時輸入框才會排整齊
/// </summary>
public static class AlignHelper
{
    /// <summary>拖曳時離其他元件的對齊線幾個像素內就自動貼齊</summary>
    public const int Magnet = 8;

    /// <summary>
    /// 拖曳吸附：被拖的元件（lead）的左邊對齊線（欄位是輸入框左緣）或上緣，
    /// 靠近其他元件的對齊線時調整位移讓它貼齊。按住 Alt 拖曳可以暫時不吸附
    /// </summary>
    public static (int Dx, int Dy) Magnetize(DesignBox lead, Point leadStart, int dx, int dy, IEnumerable<DesignBox> others)
    {
        if (Control.ModifierKeys.HasFlag(Keys.Alt)) return (dx, dy);
        int offset = lead.AlignX - lead.ModelLocation.X;          // 欄位：標籤寬度 + 間隔；其他元件：0
        int x = leadStart.X + dx + offset, y = leadStart.Y + dy;
        int bestDx = Magnet + 1, bestDy = Magnet + 1;
        int? snapX = null, snapY = null;
        foreach (var o in others)
        {
            if (Math.Abs(o.AlignX - x) < bestDx) { bestDx = Math.Abs(o.AlignX - x); snapX = o.AlignX; }
            int oy = o.ModelLocation.Y;
            if (Math.Abs(oy - y) < bestDy) { bestDy = Math.Abs(oy - y); snapY = oy; }
        }
        return (snapX.HasValue ? snapX.Value - offset - leadStart.X : dx, snapY.HasValue ? snapY.Value - leadStart.Y : dy);
    }

    // ================= 水平 =================

    /// <summary>靠左：對齊到最左邊那條對齊線（欄位用輸入框左緣）；有欄位會被推出畫面左邊時，整排往右移</summary>
    public static void AlignLeft(IList<DesignBox> boxes)
    {
        if (boxes.Count < 2) return;
        int target = boxes.Min(b => b.AlignX);
        int minX = boxes.Min(b => b.ModelLocation.X + (target - b.AlignX));
        if (minX < 0) target -= minX;
        foreach (var b in boxes) b.SetModelLocation(b.ModelLocation.X + (target - b.AlignX), b.ModelLocation.Y);
    }

    /// <summary>靠右：右緣對齊到最右邊那個元件</summary>
    public static void AlignRight(IList<DesignBox> boxes)
    {
        if (boxes.Count < 2) return;
        int target = boxes.Max(b => b.ModelLocation.X + b.Width);
        foreach (var b in boxes) b.SetModelLocation(Math.Max(0, target - b.Width), b.ModelLocation.Y);
    }

    /// <summary>水平置中：中心線對齊到第一個選取的元件</summary>
    public static void AlignCenter(IList<DesignBox> boxes)
    {
        if (boxes.Count < 2) return;
        int target = boxes[0].ModelLocation.X + boxes[0].Width / 2;
        foreach (var b in boxes) b.SetModelLocation(Math.Max(0, target - b.Width / 2), b.ModelLocation.Y);
    }

    // ================= 垂直 =================

    /// <summary>靠上（排成同一行）：上緣對齊到最上面那個元件</summary>
    public static void AlignTop(IList<DesignBox> boxes)
    {
        if (boxes.Count < 2) return;
        int y = boxes.Min(b => b.ModelLocation.Y);
        foreach (var b in boxes) b.SetModelLocation(b.ModelLocation.X, y);
    }

    /// <summary>靠下：下緣對齊到最下面那個元件</summary>
    public static void AlignBottom(IList<DesignBox> boxes)
    {
        if (boxes.Count < 2) return;
        int target = boxes.Max(b => b.ModelLocation.Y + b.Height);
        foreach (var b in boxes) b.SetModelLocation(b.ModelLocation.X, Math.Max(0, target - b.Height));
    }

    /// <summary>垂直置中：中心線對齊到第一個選取的元件（按鈕和欄位高度不同時排在同一條線）</summary>
    public static void AlignMiddle(IList<DesignBox> boxes)
    {
        if (boxes.Count < 2) return;
        int target = boxes[0].ModelLocation.Y + boxes[0].Height / 2;
        foreach (var b in boxes) b.SetModelLocation(b.ModelLocation.X, Math.Max(0, target - b.Height / 2));
    }

    // ================= 大小 =================

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

    /// <summary>同標籤寬：標籤寬度都改成第一個選取欄位的寬度（輸入框位置不動）</summary>
    public static void SameLabelWidth(IList<DesignBox> boxes)
    {
        var fields = boxes.OfType<FieldBox>().ToList();
        if (fields.Count < 2) return;
        int labelWidth = fields[0].Field.LabelWidth;
        foreach (var f in fields)
        {
            int inputLeft = f.AlignX;
            f.Field.LabelWidth = labelWidth;
            f.Field.X = Math.Max(0, inputLeft - f.InputOffset);
            f.RefreshFromModel();
        }
    }

    // ================= 等距 =================

    /// <summary>水平等距：依左右順序，讓對齊線間隔一樣（最左、最右不動）</summary>
    public static void DistributeHorizontally(IList<DesignBox> boxes)
    {
        if (boxes.Count < 3) return;
        var sorted = boxes.OrderBy(b => b.AlignX).ToList();
        int first = sorted[0].AlignX, last = sorted[^1].AlignX;
        double step = (last - first) / (double)(sorted.Count - 1);
        for (int i = 1; i < sorted.Count - 1; i++)
        {
            var b = sorted[i];
            b.SetModelLocation(b.ModelLocation.X + (first + (int)Math.Round(step * i) - b.AlignX), b.ModelLocation.Y);
        }
    }

    /// <summary>垂直等距：依上下順序，讓上緣間隔一樣（最上、最下不動）</summary>
    public static void DistributeVertically(IList<DesignBox> boxes)
    {
        if (boxes.Count < 3) return;
        var sorted = boxes.OrderBy(b => b.ModelLocation.Y).ToList();
        int first = sorted[0].ModelLocation.Y, last = sorted[^1].ModelLocation.Y;
        double step = (last - first) / (double)(sorted.Count - 1);
        for (int i = 1; i < sorted.Count - 1; i++)
            sorted[i].SetModelLocation(sorted[i].ModelLocation.X, first + (int)Math.Round(step * i));
    }

    /// <summary>建立對齊按鈕，加到工具列；getSelection 取目前選取的元件，after 在調整後呼叫（重算畫面大小等）</summary>
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
                    MessageBox.Show($"請先選取至少 {min} 個元件（圈選或按住 Ctrl 點選）。", "提醒");
                    return;
                }
                action(boxes);
                after();
            };
            toolStrip.Items.Add(b);
        }
        toolStrip.Items.Add(new ToolStripLabel("水平："));
        Add("靠左", AlignLeft, 2, "對齊到最左邊（欄位以輸入框左緣為準）");
        Add("置中", AlignCenter, 2, "中心線對齊到第一個選取的元件");
        Add("靠右", AlignRight, 2, "右緣對齊到最右邊的元件");
        toolStrip.Items.Add(new ToolStripSeparator());
        toolStrip.Items.Add(new ToolStripLabel("垂直："));
        Add("靠上", AlignTop, 2, "上緣對齊（排成同一行）");
        Add("置中", AlignMiddle, 2, "中心線對齊到第一個選取的元件（按鈕和欄位高度不同時用）");
        Add("靠下", AlignBottom, 2, "下緣對齊到最下面的元件");
        toolStrip.Items.Add(new ToolStripSeparator());
        Add("同寬", SameWidth, 2, "輸入框寬度都改成第一個選取欄位的寬度");
        Add("同標籤寬", SameLabelWidth, 2, "標籤寬度都改成第一個選取欄位的寬度（輸入框位置不動）");
        Add("水平等距", DistributeHorizontally, 3, "依左右順序平均分配間距");
        Add("垂直等距", DistributeVertically, 3, "依上下順序平均分配間距");
        toolStrip.Items.Add(new ToolStripLabel("　拖曳時靠近其他欄位會自動貼齊（按住 Alt 不貼齊）") { ForeColor = SystemColors.GrayText });
    }
}
