namespace FormBuilder;

/// <summary>
/// 畫面設計、編輯視窗版面的複製 / 貼上（Ctrl+C / Ctrl+V）：複製時存成 JSON，每次貼上都還原成新的一份，
/// 位置往右下錯開；兩個設計器之間也可以互貼按鈕、文字標籤
/// </summary>
public static class DesignClipboard
{
    /// <summary>放編輯視窗 SQL 清單的 Grid（只是裝 EditLists 用）</summary>
    private const string ListHolder = "__DesignClipboard_Lists__";
    private static string json;
    private static int pasteCount;

    public static bool IsEmpty => json == null;

    public static void Copy(IEnumerable<FieldDefinition> fields, IEnumerable<ButtonDefinition> buttons,
                            IEnumerable<GridDefinition> grids, IEnumerable<EditListDefinition> lists)
    {
        var holder = new FormDefinition { Fields = fields.ToList(), Buttons = buttons.ToList(), Grids = grids.ToList() };
        holder.Grids.Add(new GridDefinition { Title = ListHolder, EditLists = lists.ToList() });
        json = holder.ToJson();
        pasteCount = 0;
    }

    /// <summary>取出複製的元件（新的一份），位置已經往右下錯開（連續貼上越貼越往下）</summary>
    public static (List<FieldDefinition> Fields, List<ButtonDefinition> Buttons, List<GridDefinition> Grids, List<EditListDefinition> Lists) Paste()
    {
        if (json == null) return (new(), new(), new(), new());
        var holder = FormDefinition.FromJson(json);
        var lists = holder.Grids.Where(g => g.Title == ListHolder).SelectMany(g => g.EditLists).ToList();
        var grids = holder.Grids.Where(g => g.Title != ListHolder).ToList();
        int offset = 20 * ++pasteCount;
        foreach (var f in holder.Fields) { f.X += offset; f.Y += offset; }
        foreach (var b in holder.Buttons) { b.X += offset; b.Y += offset; }
        foreach (var g in grids) { g.X += offset; g.Y += offset; }
        foreach (var l in lists) { l.X += offset; l.Y += offset; }
        return (holder.Fields, holder.Buttons, grids, lists);
    }

    /// <summary>不跟 used 重複的名稱：prefix1、prefix2…（找到的名稱會加進 used）</summary>
    public static string UniqueName(string prefix, ISet<string> used)
    {
        int n = 1;
        while (used.Contains($"{prefix}{n}")) n++;
        used.Add($"{prefix}{n}");
        return $"{prefix}{n}";
    }
}
