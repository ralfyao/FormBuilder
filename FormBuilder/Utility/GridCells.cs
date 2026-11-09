using System.ComponentModel;
using System.Data;
using System.Globalization;

namespace FormBuilder;

/// <summary>
/// Grid 的欄位：依「輸入方式」建立對應的 DataGridView 欄位（文字框、下拉選單、勾選框、日期選擇）。
/// 執行畫面的單身 / 單頭清單與編輯視窗的明細清單共用
/// </summary>
public static class GridCells
{
    /// <summary>「自動」實際會用哪一種：bit = 勾選框、date / datetime = 日期選擇，其他 = 文字框</summary>
    public static GridCellKind Resolve(GridColumnDefinition c) => c.CellType != GridCellKind.自動 ? c.CellType
        : TypeHelper.IsBool(c.DataType) ? GridCellKind.勾選框
        : TypeHelper.IsDate(c.DataType) ? GridCellKind.日期選擇
        : GridCellKind.文字框;

    /// <summary>執行時 Grid 上要出現的欄位：勾了顯示的資料表欄位與自訂欄位（其他畫面欄位只放在編輯視窗）</summary>
    public static bool ShownInGrid(GridColumnDefinition c) => c.Visible && (!c.ScreenOnly || c.IsCustom);

    /// <param name="connectionString">下拉選單的選項 SQL 用</param>
    public static DataGridViewColumn CreateColumn(GridColumnDefinition c, string connectionString)
    {
        DataGridViewColumn col = Resolve(c) switch
        {
            GridCellKind.下拉選單 => CreateCombo(c, connectionString),
            GridCellKind.勾選框 => CreateCheckBox(c),
            GridCellKind.日期選擇 => new CalendarColumn(c),
            GridCellKind.按鈕 => new DataGridViewButtonColumn { Text = ButtonText(c), UseColumnTextForButtonValue = true, FlatStyle = FlatStyle.System },
            _ => CreateTextBox(c),
        };
        if (Resolve(c) != GridCellKind.按鈕) col.DataPropertyName = c.Column;     // 按鈕只顯示按鈕文字，不對應資料
        col.HeaderText = c.Header;
        col.Width = c.Width;
        col.Tag = c;
        if (c.IsCustom) col.ReadOnly = true;                    // 自訂欄位由 HookCustomColumns 處理點擊，不進入格子編輯
        if (c.HasFormula)
        {
            col.ReadOnly = true;                                // 有公式的欄位由同一列的值算出來（HookFormulas）
            col.DefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
        }
        if (TypeHelper.IsNumber(c.DataType) && col is DataGridViewTextBoxColumn)
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        return col;
    }

    /// <summary>
    /// 有公式的欄位：同一列用到的欄位改了（Grid 輸入、編輯視窗寫回）就重算那一列，結果寫進 DataTable（資料表欄位會跟著存檔）。
    /// 讀進來的資料不重算，顯示資料庫裡存的值（空的由 FillEmptyFormulas 補算）。每個新的 DataTable 呼叫一次
    /// </summary>
    /// <param name="master">公式用到同一列沒有的欄位時取值（單頭畫面、編輯視窗上的欄位）；畫面欄位改了要呼叫 RecalcRows</param>
    public static void HookFormulas(DataTable table, IEnumerable<GridColumnDefinition> columns, Func<string, object> master = null)
    {
        var formulas = columns.Where(c => c.HasFormula && !Formula.HasAggregate(c.Formula) && table.Columns.Contains(c.Column)).ToList();
        if (formulas.Count == 0) return;
        var used = formulas.SelectMany(c => SafeNames(c.Formula)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool recalculating = false;
        table.ColumnChanged += (s, e) =>
        {
            if (recalculating || !used.Contains(e.Column.ColumnName)) return;
            recalculating = true;
            try
            {
                RecalcRow(e.Row, formulas, master);
            }
            finally
            {
                recalculating = false;
            }
        };
    }

    /// <summary>公式有用到同一列以外的欄位（畫面上的欄位）</summary>
    public static bool UsesOutside(DataTable table, IEnumerable<GridColumnDefinition> columns) =>
        table != null && columns.Where(c => c.HasFormula && !Formula.HasAggregate(c.Formula) && table.Columns.Contains(c.Column))
                                .SelectMany(c => SafeNames(c.Formula)).Any(n => !table.Columns.Contains(n));

    /// <summary>
    /// 畫面上的欄位改了：公式用到畫面欄位的 Grid，每一列重算（值沒變的不動，不會變成有異動）
    /// </summary>
    public static void RecalcRows(DataTable table, IEnumerable<GridColumnDefinition> columns, Func<string, object> master)
    {
        if (!UsesOutside(table, columns)) return;
        var formulas = columns.Where(c => c.HasFormula && !Formula.HasAggregate(c.Formula) && table.Columns.Contains(c.Column)).ToList();
        foreach (DataRow row in table.Rows)
            if (row.RowState != DataRowState.Deleted) RecalcRow(row, formulas, master);
    }

    /// <summary>
    /// 讀進來時公式欄位是空的（資料庫沒存、只顯示在畫面的欄位）：照公式算出來顯示。
    /// 有值的不動（顯示資料庫裡存的值）；算出來的只是顯示，不算異動，不會因此存檔
    /// </summary>
    public static void FillEmptyFormulas(DataTable table, IEnumerable<GridColumnDefinition> columns, Func<string, object> master = null)
    {
        var formulas = columns.Where(c => c.HasFormula && !Formula.HasAggregate(c.Formula) && table.Columns.Contains(c.Column)).ToList();
        if (formulas.Count == 0) return;
        foreach (var c in formulas) table.Columns[c.Column].ReadOnly = false;
        foreach (DataRow row in table.Rows)
        {
            if (row.RowState != DataRowState.Unchanged || formulas.All(c => row[c.Column] != DBNull.Value)) continue;
            var stored = formulas.ToDictionary(c => c, c => row[c.Column]);
            RecalcRow(row, formulas, master);
            foreach (var (c, value) in stored)
                if (value != DBNull.Value) row[c.Column] = value;   // 資料庫有存值的欄位維持原值
            row.AcceptChanges();
        }
    }

    private static List<string> SafeNames(string formula)
    {
        try { return Formula.Names(formula); }
        catch (FormatException) { return new List<string>(); }
    }

    /// <summary>重算一列的公式欄位；用到其他公式欄位時先算它（循環參照 = 算不出來，清空）</summary>
    private static void RecalcRow(DataRow row, List<GridColumnDefinition> formulas, Func<string, object> master = null)
    {
        // 欄位先找同一列，沒有再找畫面上的欄位
        object Outside(string name) =>
            master != null ? master(name) ?? DBNull.Value : throw new FormatException($"公式裡的欄位「{name}」不在這一列");
        // 值一樣就不寫（不然整列會變成有異動）
        static void Set(DataRow r, string column, object value)
        {
            if (!Equals(r[column], value)) r[column] = value;
        }
        var cache = new Dictionary<GridColumnDefinition, decimal?>();
        decimal? Compute(GridColumnDefinition c, HashSet<GridColumnDefinition> visiting)
        {
            if (cache.TryGetValue(c, out var done)) return done;
            if (!visiting.Add(c)) throw new FormatException("公式循環參照");
            decimal? result = Formula.Evaluate(c.Formula, name =>
            {
                if (!row.Table.Columns.Contains(name)) return Outside(name);
                var source = formulas.FirstOrDefault(x => x.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
                return source != null ? Compute(source, visiting) ?? throw new FormatException("算不出來") : row[name];
            });
            cache[c] = result;
            return result;
        }
        // 數字公式先算，文字公式（& 串接）再用算好的值接起來
        foreach (var c in formulas.OrderBy(c => Formula.IsText(c.Formula)))
        {
            var column = row.Table.Columns[c.Column];
            if (Formula.IsText(c.Formula))
            {
                string text = Formula.EvaluateText(c.Formula, name =>
                    row.Table.Columns.Contains(name) ? row[name] : Outside(name), c.Decimals);
                try
                {
                    Set(row, c.Column, text == null ? DBNull.Value
                        : column.DataType == typeof(string) ? text : Convert.ChangeType(text, column.DataType, CultureInfo.CurrentCulture));
                }
                catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
                {
                    Set(row, c.Column, DBNull.Value);           // 文字放不進數字、日期欄位
                }
                continue;
            }
            decimal? value = Compute(c, new HashSet<GridColumnDefinition>());
            bool integer = column.DataType == typeof(int) || column.DataType == typeof(long) || column.DataType == typeof(short) || column.DataType == typeof(byte)
                           || c.DataType is "int" or "bigint" or "smallint" or "tinyint";
            int decimals = integer ? 0 : c.Decimals;
            try
            {
                Set(row, c.Column, value == null ? DBNull.Value
                    : column.DataType == typeof(string) ? Formula.Format(value.Value, decimals)
                    : Convert.ChangeType(decimals >= 0 ? Math.Round(value.Value, decimals, MidpointRounding.AwayFromZero) : value.Value, column.DataType, CultureInfo.InvariantCulture));
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
            {
                Set(row, c.Column, DBNull.Value);           // 結果放不進這個欄位（例如超出範圍）
            }
        }
    }

    /// <summary>欄寬自動符合資料時，一欄最寬幾點（備註之類的長文字不要把其他欄擠出畫面）</summary>
    public const int MaxAutoFitWidth = 400;

    /// <summary>
    /// 欄寬符合資料：每次換一批資料（查詢、重新載入）就依標題與看得到的資料調整欄寬，最寬 MaxAutoFitWidth；
    /// 只量畫面上看得到的列，資料多也不會慢。Grid 還沒顯示時，等第一次顯示再調整
    /// </summary>
    public static void HookAutoFit(DataGridView grid)
    {
        bool pending = false;
        void Fit()
        {
            if (!grid.IsHandleCreated || !grid.Visible) { pending = true; return; }
            pending = false;
            grid.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);
            foreach (DataGridViewColumn c in grid.Columns)
                if (c.Width > MaxAutoFitWidth) c.Width = MaxAutoFitWidth;
        }
        grid.DataBindingComplete += (s, e) =>
        {
            if (e.ListChangedType != ListChangedType.Reset) return;
            if (grid.IsHandleCreated) grid.BeginInvoke(Fit);        // 等列都建好、排好版再量
            else pending = true;
        };
        grid.VisibleChanged += (s, e) => { if (pending && grid.Visible && grid.IsHandleCreated) grid.BeginInvoke(Fit); };
        grid.HandleCreated += (s, e) => { if (pending) grid.BeginInvoke(Fit); };
    }

    /// <summary>Grid 的資料有變動（換一批資料、改一格、新增或刪除一列）就呼叫 changed</summary>
    public static void HookDataChanged(DataGridView grid, Action changed)
    {
        grid.DataSourceChanged += (s, e) =>
        {
            if (grid.DataSource is DataTable table)
            {
                table.ColumnChanged += (s2, e2) => changed();
                table.RowChanged += (s2, e2) => { if (e2.Action is DataRowAction.Add or DataRowAction.Delete or DataRowAction.Rollback) changed(); };
                table.RowDeleted += (s2, e2) => changed();
            }
            changed();
        };
    }

    /// <summary>Grid 目前的資料有這個欄位（彙總函數找欄位用）</summary>
    public static bool HasColumn(DataGridView grid, string column) => grid.DataSource is DataTable t && t.Columns.Contains(column);

    /// <summary>彙總 Grid 的一欄（刪掉的列不算；沒有資料 = 沒有任何值）</summary>
    public static decimal Aggregate(DataGridView grid, string function, string column)
    {
        var rows = grid.DataSource is DataTable t && t.Columns.Contains(column)
            ? t.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Deleted).Select(r => r[column])
            : Enumerable.Empty<object>();
        return Formula.Aggregate(function, rows);
    }

    /// <summary>
    /// 檢查 Grid / 明細清單的「執行後動作」：一行一組「畫面欄位=公式」。回傳每個問題的說明（沒問題 = 空的）：
    /// 左邊的欄位不在畫面上（targets）、SUM(…) 括號裡的欄位明細沒有（detailColumns）、公式裡其他名稱都找不到（otherNames；null = 不檢查）
    /// </summary>
    public static List<string> CheckAfterChange(string text, IEnumerable<string> targets, IEnumerable<string> detailColumns, IEnumerable<string> otherNames = null)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return problems;
        var targetSet = new HashSet<string>(targets, StringComparer.OrdinalIgnoreCase);
        var detailSet = new HashSet<string>(detailColumns, StringComparer.OrdinalIgnoreCase);
        var otherSet = otherNames == null ? null : new HashSet<string>(otherNames.Concat(targetSet).Concat(detailSet), StringComparer.OrdinalIgnoreCase);
        foreach (var raw in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string line = raw.Trim();
            if (line == "") continue;
            int eq = line.IndexOf('=');
            if (eq <= 0)
            {
                problems.Add($"「{line}」少了「畫面欄位=」");
                continue;
            }
            string target = line[..eq].Trim().TrimStart('@');
            string formula = line[(eq + 1)..].Trim();
            if (!targetSet.Contains(target)) problems.Add($"「{line}」：畫面上沒有「{target}」這個欄位");
            try
            {
                foreach (var column in Formula.AggregateColumns(formula).Where(c => !detailSet.Contains(c)))
                    problems.Add($"「{line}」：明細沒有「{column}」這個欄位");
                if (otherSet != null)
                    foreach (var name in Formula.Names(formula).Where(n => !otherSet.Contains(n)))
                        problems.Add($"「{line}」：找不到「{name}」這個欄位");
            }
            catch (FormatException ex)
            {
                problems.Add($"「{line}」：{ex.Message}");
            }
        }
        return problems;
    }

    /// <summary>
    /// Grid 的「執行後動作」：一行一組「畫面欄位=公式」，公式可以用 SUM(欄位) 等彙總函數（aggregateOf）和畫面上的欄位（valueOf）。
    /// 文字標籤、只當查詢條件的欄位一直顯示計算結果；資料表的欄位只在 fillData（新增、修改）時填，瀏覽時顯示資料庫裡存的值
    /// </summary>
    public static void RunAfterChange(string text, IDictionary<FieldDefinition, Control> inputs, bool fillData,
                                      Func<string, object> valueOf, Func<string, string, decimal> aggregateOf)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        foreach (var line in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string target = line[..eq].Trim().TrimStart('@');
            string formula = line[(eq + 1)..].Trim();
            var f = inputs.Keys.FirstOrDefault(x => x.Column.Equals(target, StringComparison.OrdinalIgnoreCase));
            if (f == null || formula == "") continue;
            bool display = f.IsTextLabel || f.QueryOnly;
            if (!display && !fillData) continue;
            bool integer = f.DataType is "int" or "bigint" or "smallint" or "tinyint";
            if (Formula.IsText(formula))
            {
                string joined = Formula.EvaluateText(formula, valueOf, integer ? 0 : f.Decimals, aggregateOf);
                InputHelper.SetValue(inputs[f], joined ?? (display ? "" : null));
                continue;
            }
            decimal? value = Formula.Evaluate(formula, valueOf, aggregateOf);
            InputHelper.SetValue(inputs[f], value == null ? (display ? "" : null)
                                          : Formula.Display(value.Value, integer ? 0 : f.Decimals));
        }
    }

    /// <summary>
    /// 單身欄位的「執行後動作」（GridColumnDefinition.EditAfterSelectSql，跟編輯視窗同一個設定）：使用者改了這一格（下拉選單選了、輸入完離開）
    /// 就執行 SQL，@名稱 先取同一列的欄位，沒有再用 fallback（單頭）；SQL 查出資料時第一筆依回傳對應帶進同一列的欄位（空白 = 同名的欄位）
    /// </summary>
    public static void HookAfterSelect(DataGridView grid, string connectionString, Func<string, object> fallback)
    {
        bool running = false;
        // 下拉選單、勾選框一選就送出，不用等離開格子
        grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (grid.IsCurrentCellDirty && grid.CurrentCell?.OwningColumn is DataGridViewComboBoxColumn or DataGridViewCheckBoxColumn
                && grid.CurrentCell.OwningColumn.Tag is GridColumnDefinition { EditAfterSelectSql: { Length: > 0 } })
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += (s, e) => Run(e.RowIndex, e.ColumnIndex);
        // 開窗選取帶回值是直接寫進資料列，不會觸發 CellValueChanged：由 HookPickers 帶回後呼叫
        afterSelectRunners.AddOrUpdate(grid, Run);

        void Run(int rowIndex, int columnIndex)
        {
            if (running || rowIndex < 0 || columnIndex < 0 || rowIndex >= grid.Rows.Count || grid.ReadOnly) return;
            if (grid.Columns[columnIndex].Tag is not GridColumnDefinition c || string.IsNullOrWhiteSpace(c.EditAfterSelectSql)) return;
            if (grid.Rows[rowIndex].DataBoundItem is not DataRowView view) return;
            var table = view.Row.Table;
            object ValueOf(string name) => table.Columns.Contains(name) ? view[name] : fallback(name);
            if (ValueOf(c.Column) is null or DBNull || ValueOf(c.Column) is string text && text.Trim() == "") return;   // 清空時不查
            running = true;
            try
            {
                ButtonRunner.ExecuteSql(c.EditAfterSelectSql, c.EditAfterSelectParams, connectionString, new Func<string, object>[] { ValueOf }, out var result);
                if (result == null) return;
                var notFound = new List<string>();
                foreach (var (name, value) in ButtonRunner.MapResult(result, c.EditAfterSelectReturn))
                {
                    if (!table.Columns.Contains(name) || name.Equals(c.Column, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(c.EditAfterSelectReturn))
                    {
                        if (!string.IsNullOrWhiteSpace(c.EditAfterSelectReturn)) notFound.Add(name);
                        continue;                               // 回傳對應空白時，單身沒有的欄位略過（自己這一格也不覆蓋）
                    }
                    try
                    {
                        var column = table.Columns[name];
                        column.ReadOnly = false;                // 關聯資料表帶出來的欄位（例如項目名稱）載入時是唯讀
                        view[name] = value is null or DBNull ? DBNull.Value
                                   : column.DataType == typeof(string) ? Convert.ToString(value, CultureInfo.CurrentCulture)
                                   : Convert.ChangeType(value, column.DataType, CultureInfo.CurrentCulture);
                    }
                    catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
                    {
                        notFound.Add($"{name}（型別不符）");
                    }
                }
                view.EndEdit();
                if (notFound.Count > 0) MessageBox.Show("執行後動作查出來的值帶不進這些單身欄位：" + string.Join("、", notFound), "提醒");
                grid.InvalidateRow(rowIndex);
            }
            catch (FormatException ex)
            {
                MessageBox.Show(ex.Message, "提醒");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"單身欄位「{c.Header ?? c.Column}」的執行後動作失敗：{ex.Message}", "錯誤", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                running = false;
            }
        }
    }

    /// <summary>各 Grid 的執行後動作（HookAfterSelect 登記，開窗選取帶回值後依欄位執行）</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DataGridView, Action<int, int>> afterSelectRunners = new();

    /// <summary>開窗帶回值後：帶回的欄位有設定執行後動作就執行（跟手動輸入完離開格子一樣）</summary>
    private static void RunAfterSelect(DataGridView grid, int rowIndex, IEnumerable<string> columns)
    {
        if (!afterSelectRunners.TryGetValue(grid, out var run)) return;
        foreach (string name in columns.Distinct(StringComparer.OrdinalIgnoreCase).ToList())
            foreach (DataGridViewColumn col in grid.Columns)
                if (col.Tag is GridColumnDefinition { EditAfterSelectSql: { Length: > 0 } } c && c.Column.Equals(name, StringComparison.OrdinalIgnoreCase))
                    run(rowIndex, col.Index);
    }

    /// <summary>
    /// 金額欄位（decimal、numeric、money、float，主鍵除外）顯示千分號；輸入時打不打千分號都可以。每個 Grid 呼叫一次
    /// </summary>
    public static void HookThousands(DataGridView grid)
    {
        static bool Uses(DataGridViewColumn col) =>
            col?.Tag is GridColumnDefinition c && TypeHelper.UsesThousands(c.DataType, c.IsKey) && col is DataGridViewTextBoxColumn;
        grid.CellFormatting += (s, e) =>
        {
            if (e.ColumnIndex < 0 || e.Value is not (decimal or double or float) || !Uses(grid.Columns[e.ColumnIndex])) return;
            e.Value = TypeHelper.Thousands(e.Value);
            e.FormattingApplied = true;
        };
        grid.CellParsing += (s, e) =>
        {
            if (e.ColumnIndex < 0 || e.Value is not string text || !Uses(grid.Columns[e.ColumnIndex])) return;
            if (text.Trim() == "")
            {
                e.Value = DBNull.Value;
                e.ParsingApplied = true;
                return;
            }
            if (!decimal.TryParse(text.Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var d)) return;     // 打錯的交給 DataError 提醒
            var type = Nullable.GetUnderlyingType(e.DesiredType) ?? e.DesiredType;
            try
            {
                e.Value = type == typeof(decimal) ? d : Convert.ChangeType(d, type, CultureInfo.CurrentCulture);
                e.ParsingApplied = true;
            }
            catch (Exception ex) when (ex is InvalidCastException or OverflowException or FormatException) { }
        };
    }

    /// <summary>
    /// Grid 上自訂勾選框欄位打勾的列（有好幾個勾選框欄位時，任一個打勾就算）；沒有勾選框欄位或都沒打勾 = 空的。
    /// 「執行SQL」按鈕有打勾的列時只對這些列執行
    /// </summary>
    public static List<DataRow> CheckedRows(DataGridView grid)
    {
        if (grid?.DataSource is not DataTable table) return new List<DataRow>();
        var checks = grid.Columns.Cast<DataGridViewColumn>()
            .Select(col => col.Tag as GridColumnDefinition)
            .Where(c => c is { IsCustom: true } && Resolve(c) == GridCellKind.勾選框 && table.Columns.Contains(c.Column))
            .Select(c => c.Column).ToList();
        if (checks.Count == 0) return new List<DataRow>();
        return table.Rows.Cast<DataRow>().Where(r => r.RowState != DataRowState.Deleted && checks.Any(c => r[c] is true)).ToList();
    }

    // ================= 開窗選取 =================

    /// <summary>格子右邊「…」按鈕的寬度</summary>
    private const int PickerButtonWidth = 18;

    /// <summary>Grid 欄位的開窗設定轉成選取視窗按鈕（同編輯視窗欄位的開窗選取）；沒設定開窗畫面回傳 null</summary>
    public static ButtonDefinition PickerButton(GridColumnDefinition c) => ButtonRunner.PickerButton(new FieldDefinition
    {
        Column = c.Column,
        Label = c.Header ?? c.Column,
        PickerForm = c.EditPickerForm,
        PickerParams = c.EditPickerParams,
        PickerReturn = c.EditPickerReturn,
    });

    /// <summary>
    /// 輸入方式 = 開窗選取的欄位：格子右邊畫「…」按鈕，點它或按 F4 就開選取視窗（格子可以輸入時才開），
    /// 選一筆後依回傳對應把值帶回同一列（空白 = 只帶回這個欄位）。
    /// 傳入參數的 @名稱 先取同一列的欄位，沒有再用 fallback（單頭、編輯視窗上的欄位）
    /// </summary>
    public static void HookPickers(DataGridView grid, Func<string, object> fallback, string sourcePath, IWin32Window owner, string connectionString)
    {
        static bool IsPicker(DataGridViewColumn col) => col?.Tag is GridColumnDefinition c && Resolve(c) == GridCellKind.開窗選取;
        if (!grid.Columns.Cast<DataGridViewColumn>().Any(IsPicker)) return;
        bool busy = false;

        bool CanPick(int rowIndex, int columnIndex) =>
            rowIndex >= 0 && columnIndex >= 0 && IsPicker(grid.Columns[columnIndex]) && !grid.ReadOnly && !grid[columnIndex, rowIndex].ReadOnly;

        void Open(int rowIndex, int columnIndex)
        {
            if (busy || !CanPick(rowIndex, columnIndex)) return;
            var c = (GridColumnDefinition)grid.Columns[columnIndex].Tag;
            var b = PickerButton(c);
            if (b == null)
            {
                MessageBox.Show($"「{c.Header ?? c.Column}」還沒有設定開窗畫面（到單身欄位設定，雙擊這個欄位的「選項 / 格式」設定）。", "提醒");
                return;
            }
            busy = true;
            try
            {
                grid.CurrentCell = grid[columnIndex, rowIndex];
                if (grid.Rows[rowIndex].IsNewRow)
                {
                    // 還沒輸入的新增列：先讓它變成真的一列，帶回來的值才放得進去
                    grid.BeginEdit(false);
                    grid.NotifyCurrentCellDirty(true);
                    grid.EndEdit();
                }
                else if (grid.IsCurrentCellInEditMode) grid.EndEdit();
                if (grid.Rows[rowIndex].DataBoundItem is not DataRowView view) return;
                var table = view.Row.Table;
                object ValueOf(string name) => table.Columns.Contains(name) ? view[name] : fallback?.Invoke(name);
                ButtonRunner.OpenForm(b, sourcePath, owner, ValueOf, values =>
                {
                    var notFound = new List<string>();
                    foreach (var (name, value) in values)
                    {
                        if (!table.Columns.Contains(name) || table.Columns[name].ReadOnly && !table.Columns[name].AllowDBNull && value is null or DBNull)
                        {
                            notFound.Add(name);
                            continue;
                        }
                        try
                        {
                            var column = table.Columns[name];
                            column.ReadOnly = false;
                            view[name] = value is null or DBNull ? DBNull.Value
                                       : column.DataType == typeof(string) ? Convert.ToString(value, CultureInfo.CurrentCulture)
                                       : Convert.ChangeType(value, column.DataType, CultureInfo.CurrentCulture);
                        }
                        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
                        {
                            notFound.Add($"{name}（型別不符）");
                        }
                    }
                    view.EndEdit();
                    if (notFound.Count > 0) MessageBox.Show("開窗帶回來的值放不進這些欄位：" + string.Join("、", notFound), "提醒");
                    if (rowIndex < grid.Rows.Count) grid.InvalidateRow(rowIndex);
                    // 帶回的欄位有設定執行後動作（例如選了項目編號帶出項目名稱）就接著執行
                    RunAfterSelect(grid, rowIndex, values.Keys.Where(table.Columns.Contains));
                }, connectionString: connectionString);
            }
            finally
            {
                busy = false;
            }
        }

        // 格子右邊畫「…」按鈕（可以輸入時才畫）
        grid.CellPainting += (s, e) =>
        {
            if (!CanPick(e.RowIndex, e.ColumnIndex) || e.Graphics == null) return;
            e.Paint(e.ClipBounds, DataGridViewPaintParts.All);
            var r = ButtonRect(e.CellBounds);
            ButtonRenderer.DrawButton(e.Graphics, r, System.Windows.Forms.VisualStyles.PushButtonState.Normal);
            TextRenderer.DrawText(e.Graphics, "…", grid.Font, r, SystemColors.ControlText,
                                  TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            e.Handled = true;
        };
        grid.CellMouseClick += (s, e) =>
        {
            if (e.Button != MouseButtons.Left || !CanPick(e.RowIndex, e.ColumnIndex)) return;
            var cell = grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            if (e.X >= cell.Width - PickerButtonWidth - 2) grid.BeginInvoke(() => Open(e.RowIndex, e.ColumnIndex));
        };
        // F4：格子上（還沒進入編輯）或正在輸入的文字框裡都可以
        grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.F4 || grid.CurrentCell == null) return;
            e.Handled = true;
            Open(grid.CurrentCell.RowIndex, grid.CurrentCell.ColumnIndex);
        };
        grid.EditingControlShowing += (s, e) =>
        {
            if (e.Control is not TextBox box || box.Tag as string == "picker-hooked") return;
            box.Tag = "picker-hooked";
            box.KeyDown += (s2, e2) =>
            {
                if (e2.KeyCode != Keys.F4 || grid.CurrentCell == null) return;
                e2.Handled = true;
                int row = grid.CurrentCell.RowIndex, column = grid.CurrentCell.ColumnIndex;
                grid.BeginInvoke(() => Open(row, column));
            };
        };
    }

    private static Rectangle ButtonRect(Rectangle cell) =>
        new(cell.Right - PickerButtonWidth - 2, cell.Top + 2, PickerButtonWidth, Math.Max(cell.Height - 5, 8));

    /// <summary>
    /// 欄寬填滿：每一欄依設計時的欄寬比例伸縮，剛好填滿整個 Grid（AutoSizeColumnsMode = Fill），Grid 大小改變時跟著調整
    /// </summary>
    public static void ApplyFill(DataGridView grid)
    {
        var columns = grid.Columns.Cast<DataGridViewColumn>().ToList();
        float total = Math.Max(1, columns.Sum(c => c.Width));
        foreach (var c in columns)
        {
            c.FillWeight = Math.Max(1f, c.Width * 1000f / total);     // FillWeight 合計不能超過 65535，換算成比例
            c.MinimumWidth = Math.Min(c.Width, 30);
        }
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    /// <summary>生效欄位顯示用的勾選框欄位名稱（HookEffectiveColumn 靠它找到要轉換的欄位）</summary>
    public const string EffectiveColumnName = "__effective";

    /// <summary>
    /// 生效欄位：顯示成唯讀勾選框（只看不改，生效 / 取消生效要用按鈕）。
    /// 欄位是文字或數字時，值是 1、Y、True 就打勾（跟生效按鈕的判斷一樣），要配合 HookEffectiveColumn
    /// </summary>
    public static DataGridViewColumn CreateEffectiveColumn(GridColumnDefinition c) => new DataGridViewCheckBoxColumn
    {
        Name = EffectiveColumnName,
        DataPropertyName = c.Column,
        HeaderText = c.Header,
        Width = Math.Min(c.Width, 70),
        ReadOnly = true,
        Tag = c,
    };

    /// <summary>把生效欄位的值（文字、數字）轉成打勾 / 不打勾顯示</summary>
    public static void HookEffectiveColumn(DataGridView grid)
    {
        if (grid.Columns[EffectiveColumnName] == null) return;
        grid.CellFormatting += (s, e) =>
        {
            if (e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != EffectiveColumnName) return;
            e.Value = GridDefinition.IsEffectiveValue(e.Value);
            e.FormattingApplied = true;
        };
    }

    /// <summary>按鈕欄位上的文字：「選項 / 格式」填的文字，空白用欄位標題</summary>
    public static string ButtonText(GridColumnDefinition c) =>
        string.IsNullOrWhiteSpace(c.CellOptions) ? c.Header ?? c.Column : c.CellOptions.Trim();

    /// <summary>
    /// 自訂欄位的操作（Grid 唯讀時也能用）：點勾選框切換打勾、點日期格子跳出月曆、Delete 清除，按鈕交給 onButton（那一列的資料）。
    /// 值寫在 Grid 資料來源（DataTable）多加的同名欄位裡，按鈕的 SQL、執行前檢查才取得到；資料表沒有這個欄位，存檔時不寫。
    /// 文字框 / 數字框只顯示資料：值由公式或顯示 SQL（connectionString 的資料庫）帶出來（見 HookCustomValues）
    /// </summary>
    public static void HookCustomColumns(DataGridView grid, Action<GridColumnDefinition, DataRow> onButton, string connectionString = null)
    {
        static GridColumnDefinition CustomOf(DataGridViewColumn col) => col.Tag is GridColumnDefinition { IsCustom: true } c ? c : null;
        var custom = grid.Columns.Cast<DataGridViewColumn>().Select(CustomOf).Where(c => c != null).ToList();
        if (custom.Count == 0) return;
        // 按鈕有「執行前檢查」：不符合的列畫成灰色、按了沒反應。符合的列每個按鈕算一次記起來，資料有變動再重算
        // 有「詢問是否繼續」的條件時不畫灰：要讓使用者按下去選擇要不要繼續
        var checks = custom.Where(c => Resolve(c) == GridCellKind.按鈕 && !string.IsNullOrWhiteSpace(c.Button?.PreCheck)
                                       && !(c.Button.PreCheckBlocks ?? new()).Any(r => r.Action == PreCheckAction.詢問是否繼續)).ToHashSet();
        var passed = new Dictionary<GridColumnDefinition, HashSet<DataRow>>();
        DataTable checkedTable = null;
        void ClearChecks(object s, EventArgs e) => passed.Clear();
        // 換資料來源（重新查詢）時，資料表補上自訂欄位；Grid 會跟著重新對應欄位
        grid.DataSourceChanged += (s, e) =>
        {
            var table = grid.DataSource as DataTable ?? (grid.DataSource as DataView)?.Table;
            passed.Clear();
            if (checks.Count > 0 && table != checkedTable)
            {
                if (checkedTable != null)
                {
                    checkedTable.RowChanged -= ClearChecks;
                    checkedTable.RowDeleted -= ClearChecks;
                    checkedTable.TableCleared -= ClearChecks;
                }
                checkedTable = table;
                if (table != null)
                {
                    table.RowChanged += ClearChecks;           // 改值（含自訂勾選框）、新增列都會觸發
                    table.RowDeleted += ClearChecks;
                    table.TableCleared += ClearChecks;
                }
            }
            if (table == null) return;
            foreach (var c in custom.Where(c => Resolve(c) != GridCellKind.按鈕 && !table.Columns.Contains(c.Column)))
                table.Columns.Add(new DataColumn(c.Column, Resolve(c) switch
                {
                    GridCellKind.勾選框 => typeof(bool),
                    GridCellKind.日期選擇 => typeof(DateTime),
                    _ => TypeHelper.IsNumber(c.DataType) && !(c.HasFormula && Formula.IsText(c.Formula)) ? typeof(decimal) : typeof(string),   // 文字公式（& 串接）的結果是文字
                }) { DefaultValue = Resolve(c) == GridCellKind.勾選框 ? false : DBNull.Value });
            HookCustomValues(grid, table, custom.Where(c => Resolve(c) == GridCellKind.文字框 && (c.HasFormula || !string.IsNullOrWhiteSpace(c.ValueSql))).ToList(),
                             connectionString);
        };
        DataRow RowAt(int index) => index >= 0 && index < grid.Rows.Count && !grid.Rows[index].IsNewRow
                                     && grid.Rows[index].DataBoundItem is DataRowView v ? v.Row : null;
        grid.CellClick += (s, e) =>
        {
            if (e.ColumnIndex < 0 || CustomOf(grid.Columns[e.ColumnIndex]) is not { } c || RowAt(e.RowIndex) is not { } row) return;
            if (!row.Table.Columns.Contains(c.Column)) return;
            if (Resolve(c) == GridCellKind.勾選框)
                SetCustomValue(row, c.Column, !(row[c.Column] is true));
            else if (Resolve(c) == GridCellKind.日期選擇)
                PickDate(grid, grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false), row[c.Column] as DateTime?,
                         date => SetCustomValue(row, c.Column, date));
            else if (Resolve(c) == GridCellKind.下拉選單 && grid.Columns[e.ColumnIndex] is DataGridViewComboBoxColumn { DataSource: List<Option> options })
                PickOption(grid, grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false), options, row[c.Column],
                           value => SetCustomValue(row, c.Column, value));
        };
        // 這一列的按鈕是不是灰的：不符合執行前檢查；檢查寫錯就照樣可以按（按下去會提醒寫法有誤）
        bool Disabled(GridColumnDefinition c, DataRow row)
        {
            if (!checks.Contains(c)) return false;
            if (!passed.TryGetValue(c, out var set))
            {
                try
                {
                    set = row.Table.Select(c.Button.PreCheck).ToHashSet();
                }
                catch (Exception ex) when (ex is EvaluateException or SyntaxErrorException)
                {
                    set = null;
                }
                passed[c] = set;
            }
            return set != null && !set.Contains(row);
        }
        grid.CellPainting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || CustomOf(grid.Columns[e.ColumnIndex]) is not { } c || !checks.Contains(c)) return;
            if (RowAt(e.RowIndex) is not { } row || !Disabled(c, row)) return;
            e.PaintBackground(e.ClipBounds, (e.State & DataGridViewElementStates.Selected) != 0);
            var rect = e.CellBounds;
            rect.Inflate(-2, -2);
            ButtonRenderer.DrawButton(e.Graphics, rect, System.Windows.Forms.VisualStyles.PushButtonState.Disabled);
            TextRenderer.DrawText(e.Graphics, ButtonText(c), e.CellStyle.Font, rect, SystemColors.GrayText,
                                  TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            e.Handled = true;
        };
        grid.CellContentClick += (s, e) =>
        {
            if (e.ColumnIndex < 0 || CustomOf(grid.Columns[e.ColumnIndex]) is not { } c || Resolve(c) != GridCellKind.按鈕) return;
            if (RowAt(e.RowIndex) is { } row && !Disabled(c, row)) onButton?.Invoke(c, row);
        };
        grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Delete || grid.CurrentCell == null || CustomOf(grid.CurrentCell.OwningColumn) is not { } c) return;
            if (Resolve(c) == GridCellKind.按鈕 || RowAt(grid.CurrentCell.RowIndex) is not { } row || !row.Table.Columns.Contains(c.Column)) return;
            SetCustomValue(row, c.Column, Resolve(c) == GridCellKind.勾選框 ? false : DBNull.Value);
            e.Handled = true;                                  // 不要變成刪除這一列
        };
    }

    /// <summary>已經算過自訂文字框 / 數字框的資料表（重新設定同一個資料來源時不重複掛事件）</summary>
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<DataTable, object> customValueTables = new();

    /// <summary>
    /// 自訂文字框 / 數字框的值：有公式用同一列的欄位計算，否則執行顯示 SQL（@欄位 取同一列），取第一筆第一個欄位。
    /// 現在的每一列先算一次，之後同一列用到的欄位改了（或新增列）就重算那一列；SQL 錯誤只提醒一次，之後不再查
    /// </summary>
    private static void HookCustomValues(DataGridView grid, DataTable table, List<GridColumnDefinition> columns, string connectionString)
    {
        if (columns.Count == 0 || customValueTables.TryGetValue(table, out _)) return;
        customValueTables.Add(table, null);
        var failed = new HashSet<GridColumnDefinition>();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in columns)
            if (c.HasFormula) used.UnionWith(SafeNames(c.Formula));
            else foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(c.ValueSql, @"(?<!@)@([\p{L}\p{N}_]+)"))
                used.Add(m.Groups[1].Value);
        used.ExceptWith(columns.Select(c => c.Column));
        bool busy = false;
        void Fill(IEnumerable<DataRow> rows)
        {
            var list = rows.Where(r => r.RowState != DataRowState.Deleted && r.RowState != DataRowState.Detached).ToList();
            if (list.Count == 0) return;
            busy = true;
            DbDialect d = null;
            System.Data.Common.DbConnection conn = null;
            try
            {
                foreach (var c in columns)
                {
                    if (failed.Contains(c)) continue;
                    try
                    {
                        if (c.HasFormula)
                        {
                            foreach (var row in list)
                            {
                                object ValueOf(string name) => row.Table.Columns.Contains(name) ? row[name] : throw new FormatException($"公式裡的欄位「{name}」不在這一列");
                                SetCustomValue(row, c.Column, Formula.IsText(c.Formula)
                                    ? (object)Formula.EvaluateText(c.Formula, ValueOf, c.Decimals) ?? DBNull.Value
                                    : ToColumnValue(row.Table.Columns[c.Column], c, Formula.Evaluate(c.Formula, ValueOf)));
                            }
                            continue;
                        }
                        if (string.IsNullOrWhiteSpace(connectionString)) throw new InvalidOperationException("這個畫面沒有資料庫連線");
                        d ??= DbDialect.Of(connectionString);
                        conn ??= d.Open(connectionString);
                        var (sql, parameters) = d.PrepareUserSql(c.ValueSql);
                        foreach (var row in list)
                        {
                            using var cmd = d.CreateCommand(conn, sql);
                            foreach (var (name, parameter) in parameters)
                                d.AddParam(cmd, parameter, row.Table.Columns.Contains(name) ? row[name] : DBNull.Value);
                            object value = cmd.ExecuteScalar();
                            SetCustomValue(row, c.Column, value == null || value == DBNull.Value ? DBNull.Value
                                : row.Table.Columns[c.Column].DataType == typeof(string) ? Convert.ToString(value, CultureInfo.CurrentCulture)
                                : Convert.ChangeType(value, row.Table.Columns[c.Column].DataType, CultureInfo.InvariantCulture));
                        }
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException)
                    {
                        failed.Add(c);
                        MessageBox.Show(grid.FindForm(), $"自訂欄位「{c.Header ?? c.Column}」的{(c.HasFormula ? "公式" : " SQL ")}執行失敗，這個欄位先不顯示資料：\n{ex.Message}",
                                        "提醒", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            finally
            {
                conn?.Dispose();
                busy = false;
            }
        }
        Fill(table.Rows.Cast<DataRow>());
        table.ColumnChanged += (s, e) =>
        {
            if (!busy && used.Contains(e.Column.ColumnName)) Fill(new[] { e.Row });
        };
        table.RowChanged += (s, e) =>
        {
            if (!busy && e.Action == DataRowAction.Add) Fill(new[] { e.Row });
        };
    }

    /// <summary>公式結果轉成欄位的型別（小數位數照設定）；放不進去 = 空白</summary>
    private static object ToColumnValue(DataColumn column, GridColumnDefinition c, decimal? value)
    {
        if (value == null) return DBNull.Value;
        if (column.DataType == typeof(string)) return Formula.Display(value.Value, c.Decimals);     // 自訂欄位只顯示，加千分號
        return c.Decimals >= 0 ? Math.Round(value.Value, c.Decimals, MidpointRounding.AwayFromZero) : value.Value;
    }

    /// <summary>改自訂欄位的值：原本沒改過的列改完還是「沒改過」，存檔時才不會把這一列當成修改過</summary>
    private static void SetCustomValue(DataRow row, string column, object value)
    {
        bool unchanged = row.RowState == DataRowState.Unchanged && !row.HasVersion(DataRowVersion.Proposed);
        row[column] = value;
        if (unchanged) row.AcceptChanges();
    }

    /// <summary>在格子下方跳出月曆，點選日期就帶回</summary>
    /// <summary>自訂下拉選單：在格子下面跳出選項清單，點一個就帶入</summary>
    private static void PickOption(DataGridView grid, Rectangle cell, List<Option> options, object current, Action<object> onPick)
    {
        var list = new ListBox
        {
            IntegralHeight = false,
            BorderStyle = BorderStyle.None,
            Font = grid.Font,
            Width = Math.Max(cell.Width, 120),
            DisplayMember = nameof(Option.Text),
        };
        foreach (var o in options) list.Items.Add(o);
        list.Height = Math.Min(options.Count, 12) * list.ItemHeight + 4;
        list.SelectedItem = options.FirstOrDefault(o => Equals(o.Value, current) || o.Value is string s && current is string t && s.Trim() == t.Trim());
        var popup = new ToolStripDropDown { Padding = Padding.Empty };
        popup.Items.Add(new ToolStripControlHost(list) { Margin = Padding.Empty, Padding = Padding.Empty, AutoSize = false, Size = list.Size });
        void Choose()
        {
            if (list.SelectedItem is not Option o) return;
            onPick(o.Value ?? DBNull.Value);
            try { popup.Close(); }
            catch (ObjectDisposedException) { }            // 已經自己關掉（點到外面）了
        }
        list.MouseClick += (s, e) => Choose();
        list.KeyDown += (s, e) => { if (e.KeyCode == Keys.Enter) Choose(); };
        popup.Closed += (s, e) => popup.Dispose();
        popup.Show(grid, new Point(cell.Left, cell.Bottom));
        list.Focus();
    }

    private static void PickDate(DataGridView grid, Rectangle cell, DateTime? value, Action<DateTime> onPick)
    {
        var calendar = new MonthCalendar { MaxSelectionCount = 1 };
        if (value is { } d) calendar.SetDate(d);
        var popup = new ToolStripDropDown { Padding = Padding.Empty };
        popup.Items.Add(new ToolStripControlHost(calendar) { Margin = Padding.Empty, Padding = Padding.Empty });
        calendar.DateSelected += (s, e) =>
        {
            popup.Close();
            onPick(e.Start.Date);
        };
        popup.Closed += (s, e) => popup.Dispose();
        popup.Show(grid, new Point(cell.Left, cell.Bottom));
    }

    private static DataGridViewTextBoxColumn CreateTextBox(GridColumnDefinition c)
    {
        var col = new DataGridViewTextBoxColumn();
        if (TypeHelper.IsDate(c.DataType)) col.DefaultCellStyle.Format = "yyyy/MM/dd";       // 日期欄位一律只顯示日期，不顯示時分秒
        if (TypeHelper.IsString(c.DataType) && c.MaxLength > 0) col.MaxInputLength = c.MaxLength;
        return col;
    }

    /// <summary>下拉選單的一個選項（Value 已轉成欄位的型別，才能跟資料表裡的值比對）</summary>
    public sealed class Option
    {
        public object Value { get; init; }
        public string Text { get; init; }
    }

    /// <summary>下拉選單：選項 = 「選項 / 格式」填的固定選項（值=顯示，逗號分隔），或 SELECT 開頭的選項 SQL</summary>
    private static DataGridViewComboBoxColumn CreateCombo(GridColumnDefinition c, string connectionString)
    {
        string options = c.CellOptions?.Trim() ?? "";
        bool isSql = options.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) || options.StartsWith("WITH", StringComparison.OrdinalIgnoreCase);
        var source = new FieldDefinition { Items = isSql ? null : options, ItemsSql = isSql ? options : null };
        var items = new List<Option>();
        if (c.IsNullable) items.Add(new Option { Value = DBNull.Value, Text = "" });
        foreach (var item in InputHelper.BuildItems(source, connectionString, withBlank: false))
        {
            object value = ToColumnValue(item.Value, c);
            if (value != null) items.Add(new Option { Value = value, Text = item.Text });
        }
        return new DataGridViewComboBoxColumn
        {
            DataSource = items,
            ValueMember = nameof(Option.Value),
            DisplayMember = nameof(Option.Text),
            DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,   // 平常像文字，進入編輯才出現下拉箭頭
            FlatStyle = FlatStyle.Flat,
        };
    }

    /// <summary>勾選框：bit 欄位直接用 True / False；其他欄位用「選項 / 格式」填的「勾選值/取消值」（例如 Y/N，預設 1/0）</summary>
    private static DataGridViewCheckBoxColumn CreateCheckBox(GridColumnDefinition c)
    {
        var col = new DataGridViewCheckBoxColumn();
        if (TypeHelper.IsBool(c.DataType)) return col;
        string[] parts = (c.CellOptions ?? "").Split('/', StringSplitOptions.TrimEntries);
        string on = parts.Length > 0 && parts[0] != "" ? parts[0] : "1";
        string off = parts.Length > 1 && parts[1] != "" ? parts[1] : "0";
        col.TrueValue = ToColumnValue(on, c) ?? on;
        col.FalseValue = ToColumnValue(off, c) ?? off;
        col.IndeterminateValue = DBNull.Value;
        return col;
    }

    /// <summary>
    /// 把選項的文字值轉成欄位的型別：數字欄位轉數字；char / nchar 補空白到欄位長度（資料庫讀出來的值有補空白）。
    /// 轉不過去回傳 null（這個選項不用）
    /// </summary>
    private static object ToColumnValue(string text, GridColumnDefinition c)
    {
        if (TypeHelper.IsString(c.DataType))
            return c.DataType is "char" or "nchar" && c.MaxLength > 0 ? text.PadRight(c.MaxLength) : text;
        if (TypeHelper.IsBool(c.DataType))
            return text.Trim() is "1" or "Y" or "y" || text.Trim().Equals("true", StringComparison.OrdinalIgnoreCase);
        try
        {
            return TypeHelper.Parse(text, c.DataType);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// 停用下拉選單、勾選框顯示時的格式錯誤訊息（資料表裡有選項以外的值時，格子顯示空白，不要一直跳訊息）；
    /// 回傳 true = 已處理
    /// </summary>
    public static bool IgnoreDisplayError(DataGridView grid, DataGridViewDataErrorEventArgs e)
    {
        bool display = e.Context.HasFlag(DataGridViewDataErrorContexts.Formatting) || e.Context.HasFlag(DataGridViewDataErrorContexts.Display)
                    || e.Context.HasFlag(DataGridViewDataErrorContexts.PreferredSize);
        if (!display || e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex] is not (DataGridViewComboBoxColumn or DataGridViewCheckBoxColumn)) return false;
        e.ThrowException = false;
        return true;
    }

    private static readonly Color GridLineColor = Color.FromArgb(218, 218, 218);

    /// <summary>
    /// 設計畫面上的 Grid 示意（單身 / 單頭清單 / 編輯視窗的明細清單共用）：圓角外框、欄位標題、空白資料列，
    /// 跟執行時一樣有橫線和直線
    /// </summary>
    public static void PaintPreview(Graphics g, Control box, IEnumerable<GridColumnDefinition> columns, int headerHeight, int rowHeight, bool selected)
    {
        g.Clear(RoundedStyle.BackgroundOf(box.Parent));            // 圓角外面露出畫面的底色
        int w = box.Width, h = box.Height;
        var old = g.SmoothingMode;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        using var path = RoundedStyle.RoundRect(new RectangleF(0.5f, 0.5f, w - 1f, h - 1f), RoundedStyle.Radius);
        g.FillPath(SystemBrushes.Window, path);
        g.SmoothingMode = old;

        var clip = g.Clip;
        g.SetClip(path);
        g.FillRectangle(SystemBrushes.Control, 0, 0, w, headerHeight);
        using var linePen = new Pen(GridLineColor);
        // 空白資料列的橫線
        for (int y = headerHeight + rowHeight; y < h; y += rowHeight)
            g.DrawLine(linePen, 0, y, w, y);
        // 欄位標題 + 每個欄位右邊的直線（從標題一路到底）
        int x = 0;
        foreach (var c in columns.Where(c => c.Visible))
        {
            var cell = new Rectangle(x, 0, c.Width, headerHeight);
            TextRenderer.DrawText(g, c.Header, box.Font, Rectangle.Inflate(cell, -4, 0), SystemColors.ControlText,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            x += c.Width;
            g.DrawLine(SystemPens.ControlDark, x - 1, 0, x - 1, headerHeight);
            g.DrawLine(linePen, x - 1, headerHeight, x - 1, h);
            if (x > w) break;
        }
        g.DrawLine(SystemPens.ControlDark, 0, headerHeight, w, headerHeight);
        g.Clip = clip;

        // 外框：選取時用較粗的藍框
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if (selected)
        {
            using var pen = new Pen(Color.DodgerBlue, 3);
            using var inner = RoundedStyle.RoundRect(new RectangleF(1.5f, 1.5f, w - 3f, h - 3f), RoundedStyle.Radius - 1);
            g.DrawPath(pen, inner);
        }
        else
        {
            using var pen = new Pen(RoundedStyle.BorderColor);
            g.DrawPath(pen, path);
        }
        g.SmoothingMode = old;
    }

    /// <summary>Grid 一律有橫線和直線（格線）</summary>
    public static void ApplyGridLines(DataGridView grid)
    {
        grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        grid.GridColor = GridLineColor;
    }
}

/// <summary>Grid 儲存格的輸入方式</summary>
public enum GridCellKind
{
    /// <summary>依資料型別：bit = 勾選框、日期 = 日期選擇，其他 = 文字框</summary>
    自動,
    文字框,
    下拉選單,
    勾選框,
    日期選擇,
    /// <summary>按鈕：只有自訂欄位能用，按下去執行欄位設定的按鈕動作</summary>
    按鈕,
    /// <summary>開窗選取：點格子右邊的「…」、雙擊或按 F4 開選取視窗，選一筆帶回同一列（設定同編輯視窗的開窗畫面 / 傳入參數 / 回傳對應）</summary>
    開窗選取,
}

// ================= 日期選擇欄位（DataGridView 沒有內建，照 MSDN 的 CalendarColumn 做法） =================

/// <summary>
/// 日期選擇欄位：date / datetime 欄位直接存日期；文字、數字欄位依「選項 / 格式」填的日期格式（預設 yyyyMMdd）存成文字
/// </summary>
public class CalendarColumn : DataGridViewColumn
{
    /// <summary>文字、數字欄位存進資料庫的日期格式；null = date / datetime 欄位</summary>
    public string StorageFormat { get; set; }
    /// <summary>畫面上顯示的格式</summary>
    public string DisplayFormat { get; set; } = "yyyy/MM/dd";

    public CalendarColumn() : base(new CalendarCell()) { }

    public CalendarColumn(GridColumnDefinition c) : this()
    {
        bool nativeDate = TypeHelper.IsDate(c.DataType);
        StorageFormat = nativeDate ? null : string.IsNullOrWhiteSpace(c.CellOptions) ? "yyyyMMdd" : c.CellOptions.Trim();
        // 一律只顯示日期（DisplayFormat 預設值），不顯示時分秒
    }

    public override DataGridViewCell CellTemplate
    {
        get => base.CellTemplate;
        set
        {
            if (value != null && value is not CalendarCell) throw new InvalidCastException("必須是 CalendarCell");
            base.CellTemplate = value;
        }
    }

    public override object Clone()
    {
        var clone = (CalendarColumn)base.Clone();
        clone.StorageFormat = StorageFormat;
        clone.DisplayFormat = DisplayFormat;
        return clone;
    }
}

public class CalendarCell : DataGridViewTextBoxCell
{
    private CalendarColumn Column => OwningColumn as CalendarColumn;
    private string DisplayFormat => Column?.DisplayFormat ?? "yyyy/MM/dd";

    public override Type EditType => typeof(CalendarEditingControl);
    public override Type FormattedValueType => typeof(string);

    /// <summary>格子裡的值 → 日期（文字欄位依存檔格式解析；解析不了 = 沒有日期）</summary>
    public DateTime? ToDate(object value)
    {
        if (value == null || value == DBNull.Value) return null;
        if (value is DateTime d) return d;
        string format = Column?.StorageFormat;
        string text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (format != null && DateTime.TryParseExact(text, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact)) return exact;
        return DateTime.TryParse(text, out var parsed) ? parsed : null;
    }

    protected override object GetFormattedValue(object value, int rowIndex, ref DataGridViewCellStyle cellStyle,
                                                TypeConverter valueTypeConverter, TypeConverter formattedValueTypeConverter,
                                                DataGridViewDataErrorContexts context) =>
        ToDate(value)?.ToString(DisplayFormat, CultureInfo.InvariantCulture) ?? "";

    /// <summary>畫面上的文字 → 存進資料表的值（date 欄位 = 日期；文字、數字欄位 = 依存檔格式的文字 / 數字）</summary>
    public override object ParseFormattedValue(object formattedValue, DataGridViewCellStyle cellStyle,
                                               TypeConverter formattedValueTypeConverter, TypeConverter valueTypeConverter)
    {
        string text = Convert.ToString(formattedValue)?.Trim();
        if (string.IsNullOrEmpty(text)) return DBNull.Value;
        if (!DateTime.TryParseExact(text, DisplayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && !DateTime.TryParse(text, out date))
            throw new FormatException("日期格式不正確：" + text);
        // 畫面只顯示日期：原本有時間的話保留原本的時間，改日期不會把時間變成 00:00
        if (RowIndex >= 0 && ToDate(GetValue(RowIndex)) is { } old) date = date.Date + old.TimeOfDay;
        string format = Column?.StorageFormat;
        if (format == null) return date;
        string stored = date.ToString(format, CultureInfo.InvariantCulture);
        // 數字欄位（例如 int 存 20261021）轉回數字型別
        return ValueType != null && ValueType != typeof(string) ? Convert.ChangeType(stored, ValueType, CultureInfo.InvariantCulture) : stored;
    }

    public override void InitializeEditingControl(int rowIndex, object initialFormattedValue, DataGridViewCellStyle dataGridViewCellStyle)
    {
        base.InitializeEditingControl(rowIndex, initialFormattedValue, dataGridViewCellStyle);
        if (DataGridView.EditingControl is CalendarEditingControl picker)
        {
            picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = DisplayFormat;
            picker.Value = ToDate(GetValue(rowIndex)) ?? DateTime.Today;
        }
    }
}

/// <summary>日期欄位編輯時出現的日期選擇器</summary>
public class CalendarEditingControl : DateTimePicker, IDataGridViewEditingControl
{
    private bool valueChanged;

    public DataGridView EditingControlDataGridView { get; set; }
    public int EditingControlRowIndex { get; set; }
    public bool EditingControlValueChanged { get => valueChanged; set => valueChanged = value; }
    public Cursor EditingPanelCursor => base.Cursor;
    public bool RepositionEditingControlOnValueChange => false;

    public object EditingControlFormattedValue
    {
        get => Value.ToString(CustomFormat, CultureInfo.InvariantCulture);
        set
        {
            if (value is string s && DateTime.TryParseExact(s, CustomFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) Value = d;
        }
    }

    public object GetEditingControlFormattedValue(DataGridViewDataErrorContexts context) => EditingControlFormattedValue;

    public void ApplyCellStyleToEditingControl(DataGridViewCellStyle style)
    {
        Font = style.Font;
        CalendarForeColor = style.ForeColor;
        CalendarMonthBackground = style.BackColor;
    }

    /// <summary>方向鍵、數字鍵等留給日期選擇器自己處理（換日、換月）</summary>
    public bool EditingControlWantsInputKey(Keys key, bool dataGridViewWantsInputKey) => (key & Keys.KeyCode) switch
    {
        Keys.Left or Keys.Up or Keys.Down or Keys.Right or Keys.Home or Keys.End or Keys.PageDown or Keys.PageUp => true,
        _ => !dataGridViewWantsInputKey,
    };

    public void PrepareEditingControlForEdit(bool selectAll) { }

    protected override void OnValueChanged(EventArgs eventargs)
    {
        valueChanged = true;
        EditingControlDataGridView?.NotifyCurrentCellDirty(true);
        base.OnValueChanged(eventargs);
    }
}

// ================= 圓角 Grid =================

/// <summary>
/// 圓角 Grid 的外框：DataGridView 本身不能畫圓角，所以放進這個面板，
/// 面板畫圓角框線，Grid 縮在框內並用 Region 裁成圓角（捲軸也一起裁掉角落）
/// </summary>
public class RoundedGridHost : Panel
{
    public DataGridView Grid { get; }

    public RoundedGridHost(DataGridView grid)
    {
        Grid = grid;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        // 位置、大小、停靠方式交給外框，Grid 填滿框內
        Bounds = grid.Bounds;
        Dock = grid.Dock;
        Anchor = grid.Anchor;
        Padding = new Padding(1);
        grid.BorderStyle = BorderStyle.None;
        grid.Dock = DockStyle.Fill;
        Controls.Add(grid);
        grid.SizeChanged += (s, e) => ClipGrid();
        grid.Enter += (s, e) => Invalidate();
        grid.Leave += (s, e) => Invalidate();
        grid.EnabledChanged += (s, e) => Invalidate();
        grid.BackgroundColorChanged += (s, e) => Invalidate();   // 單身輸入中 / 瀏覽時底色不同
        grid.VisibleChanged += (s, e) => Visible = grid.Visible;
        ClipGrid();
    }

    private void ClipGrid()
    {
        if (Grid.Width <= 0 || Grid.Height <= 0) return;
        using var path = RoundedStyle.RoundRect(new RectangleF(0, 0, Grid.Width, Grid.Height), RoundedStyle.Radius - 1);
        Grid.Region = new Region(path);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(RoundedStyle.BackgroundOf(Parent));     // 圓角外面露出父控制項的底色
        RoundedStyle.DrawBox(e.Graphics, ClientRectangle, Grid.BackgroundColor,
                             Grid.ContainsFocus ? RoundedStyle.FocusColor : RoundedStyle.BorderColor);
    }

    /// <summary>把 Grid 包進圓角外框，回傳要加到畫面上的外框</summary>
    public static RoundedGridHost Wrap(DataGridView grid) => new(grid);
}
