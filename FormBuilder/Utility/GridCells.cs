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
    public static void HookFormulas(DataTable table, IEnumerable<GridColumnDefinition> columns)
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
                RecalcRow(e.Row, formulas);
            }
            finally
            {
                recalculating = false;
            }
        };
    }

    /// <summary>
    /// 讀進來時公式欄位是空的（資料庫沒存、只顯示在畫面的欄位）：照公式算出來顯示。
    /// 有值的不動（顯示資料庫裡存的值）；算出來的只是顯示，不算異動，不會因此存檔
    /// </summary>
    public static void FillEmptyFormulas(DataTable table, IEnumerable<GridColumnDefinition> columns)
    {
        var formulas = columns.Where(c => c.HasFormula && !Formula.HasAggregate(c.Formula) && table.Columns.Contains(c.Column)).ToList();
        if (formulas.Count == 0) return;
        foreach (var c in formulas) table.Columns[c.Column].ReadOnly = false;
        foreach (DataRow row in table.Rows)
        {
            if (row.RowState != DataRowState.Unchanged || formulas.All(c => row[c.Column] != DBNull.Value)) continue;
            var stored = formulas.ToDictionary(c => c, c => row[c.Column]);
            RecalcRow(row, formulas);
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
    private static void RecalcRow(DataRow row, List<GridColumnDefinition> formulas)
    {
        var cache = new Dictionary<GridColumnDefinition, decimal?>();
        decimal? Compute(GridColumnDefinition c, HashSet<GridColumnDefinition> visiting)
        {
            if (cache.TryGetValue(c, out var done)) return done;
            if (!visiting.Add(c)) throw new FormatException("公式循環參照");
            decimal? result = Formula.Evaluate(c.Formula, name =>
            {
                if (!row.Table.Columns.Contains(name)) throw new FormatException($"公式裡的欄位「{name}」不在這一列");
                var source = formulas.FirstOrDefault(x => x.Column.Equals(name, StringComparison.OrdinalIgnoreCase));
                return source != null ? Compute(source, visiting) ?? throw new FormatException("算不出來") : row[name];
            });
            cache[c] = result;
            return result;
        }
        foreach (var c in formulas)
        {
            decimal? value = Compute(c, new HashSet<GridColumnDefinition>());
            var column = row.Table.Columns[c.Column];
            bool integer = column.DataType == typeof(int) || column.DataType == typeof(long) || column.DataType == typeof(short) || column.DataType == typeof(byte)
                           || c.DataType is "int" or "bigint" or "smallint" or "tinyint";
            int decimals = integer ? 0 : c.Decimals;
            try
            {
                row[c.Column] = value == null ? DBNull.Value
                    : column.DataType == typeof(string) ? Formula.Format(value.Value, decimals)
                    : Convert.ChangeType(decimals >= 0 ? Math.Round(value.Value, decimals, MidpointRounding.AwayFromZero) : value.Value, column.DataType, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
            {
                row[c.Column] = DBNull.Value;               // 結果放不進這個欄位（例如超出範圍）
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
            decimal? value = Formula.Evaluate(formula, valueOf, aggregateOf);
            bool integer = f.DataType is "int" or "bigint" or "smallint" or "tinyint";
            InputHelper.SetValue(inputs[f], value == null ? (display ? "" : null)
                                          : Formula.Format(value.Value, integer ? 0 : f.Decimals));
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
        grid.CellValueChanged += (s, e) =>
        {
            if (running || e.RowIndex < 0 || e.ColumnIndex < 0 || grid.ReadOnly) return;
            if (grid.Columns[e.ColumnIndex].Tag is not GridColumnDefinition c || string.IsNullOrWhiteSpace(c.EditAfterSelectSql)) return;
            if (grid.Rows[e.RowIndex].DataBoundItem is not DataRowView view) return;
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
                        view[name] = value is null or DBNull ? DBNull.Value
                                   : column.DataType == typeof(string) ? Convert.ToString(value, CultureInfo.CurrentCulture)
                                   : Convert.ChangeType(value, column.DataType, CultureInfo.CurrentCulture);
                    }
                    catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException or ArgumentException)
                    {
                        notFound.Add($"{name}（型別不符）");
                    }
                }
                if (notFound.Count > 0) MessageBox.Show("執行後動作查出來的值帶不進這些單身欄位：" + string.Join("、", notFound), "提醒");
                grid.InvalidateRow(e.RowIndex);
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
        };
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
        var checks = custom.Where(c => Resolve(c) == GridCellKind.按鈕 && !string.IsNullOrWhiteSpace(c.Button?.PreCheck)).ToHashSet();
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
                    _ => TypeHelper.IsNumber(c.DataType) ? typeof(decimal) : typeof(string),
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
                                SetCustomValue(row, c.Column, ToColumnValue(row.Table.Columns[c.Column], c, Formula.Evaluate(c.Formula, name =>
                                    row.Table.Columns.Contains(name) ? row[name] : throw new FormatException($"公式裡的欄位「{name}」不在這一列"))));
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
        if (column.DataType == typeof(string)) return Formula.Format(value.Value, c.Decimals);
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
