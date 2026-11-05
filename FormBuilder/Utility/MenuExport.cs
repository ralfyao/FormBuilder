using System.Text.Json;

namespace FormBuilder;

/// <summary>選單架構檔裡的一個節點：資料夾（FormKey 空白）或畫面</summary>
public class MenuExportNode
{
    public string Title { get; set; }
    public string FormKey { get; set; }
    public List<MenuExportNode> Children { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsFolder => string.IsNullOrEmpty(FormKey);
}

/// <summary>
/// 選單架構檔（匯出 JSON 時跟畫面放在同一個資料夾）：記下匯出的畫面在選單、「未放進選單的畫面」裡的資料夾位置，
/// 匯入時照它把畫面放回同樣的資料夾
/// </summary>
public class MenuExportFile
{
    public const string FileName = "選單架構.menu.json";

    public List<MenuExportNode> Menu { get; set; } = new();
    public List<MenuExportNode> Unlisted { get; set; } = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,   // 中文直接寫，不轉成 \uXXXX
    };

    public string ToJson() => JsonSerializer.Serialize(this, Options);

    public static MenuExportFile FromJson(string json) => JsonSerializer.Deserialize<MenuExportFile>(json, Options) ?? new MenuExportFile();

    /// <summary>平的選單節點轉成樹，只留 include 的畫面，以及裡面有這些畫面的資料夾</summary>
    public static List<MenuExportNode> Build(List<MenuNode> nodes, Func<string, bool> include)
    {
        List<MenuExportNode> Children(int? parentId) =>
            nodes.Where(n => n.ParentId == parentId).OrderBy(n => n.SortOrder).ThenBy(n => n.Id)
                .Select(n => n.IsFolder
                    ? new MenuExportNode { Title = n.Title, Children = Children(n.Id) }
                    : include(n.FormKey) ? new MenuExportNode { Title = n.Title, FormKey = n.FormKey } : null)
                .Where(n => n != null && (!n.IsFolder || n.Children.Count > 0))
                .ToList();
        return Children(null);
    }

    /// <summary>平的選單節點轉成存檔用的樹（保留原本的順序）</summary>
    public static List<MenuTree> ToTrees(List<MenuNode> nodes)
    {
        List<MenuTree> Children(int? parentId) =>
            nodes.Where(n => n.ParentId == parentId).OrderBy(n => n.SortOrder).ThenBy(n => n.Id)
                .Select(n =>
                {
                    var tree = new MenuTree { Node = n };
                    tree.Children.AddRange(Children(n.Id));
                    return tree;
                }).ToList();
        return Children(null);
    }

    /// <summary>「未放進選單的畫面」匯出時放的資料夾名稱</summary>
    public const string UnlistedFolder = "未放進選單的畫面";

    /// <summary>資料夾名稱裡不能用的符號換成底線（選單資料夾的標題當成磁碟上的資料夾名稱）</summary>
    public static string SafeFolderName(string title)
    {
        var bad = Path.GetInvalidFileNameChars();
        string name = new string((title ?? "").Select(c => bad.Contains(c) ? '_' : c).ToArray()).Trim().TrimEnd('.');
        return name == "" ? "_" : name;
    }

    /// <summary>
    /// 每個畫面在磁碟上要放的資料夾（相對路徑，照選單的資料夾層次；「未放進選單的畫面」放在同名資料夾底下）。
    /// 同一個畫面出現在好幾個地方時，用選單上第一個位置；都沒有的不列（放在最上層）
    /// </summary>
    public Dictionary<string, string> FolderOfForms()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        void Walk(IEnumerable<MenuExportNode> nodes, string path)
        {
            foreach (var n in nodes)
            {
                if (n.IsFolder) Walk(n.Children, Path.Combine(path, SafeFolderName(n.Title)));
                else result.TryAdd(n.FormKey, path);
            }
        }
        Walk(Menu, "");
        Walk(Unlisted, UnlistedFolder);
        return result;
    }

    /// <summary>
    /// 沒有選單架構檔時，照磁碟上的資料夾結構建選單：子資料夾 = 選單資料夾，畫面放在它所在的資料夾；
    /// 「未放進選單的畫面」資料夾底下的放回「未放進選單的畫面」。titleOf 提供畫面的名稱
    /// </summary>
    public static MenuExportFile FromFolders(string root, IEnumerable<string> formFiles, Func<string, string> titleOf)
    {
        var file = new MenuExportFile();
        foreach (var path in formFiles)
        {
            string relative = Path.GetRelativePath(root, Path.GetDirectoryName(path) ?? root);
            var parts = relative == "." ? new List<string>() : relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).ToList();
            var list = file.Menu;
            if (parts.Count > 0 && parts[0] == UnlistedFolder)
            {
                list = file.Unlisted;
                parts.RemoveAt(0);
            }
            foreach (var part in parts)
            {
                var folder = list.FirstOrDefault(n => n.IsFolder && n.Title == part);
                if (folder == null) list.Add(folder = new MenuExportNode { Title = part });
                list = folder.Children;
            }
            string key = Path.GetFileNameWithoutExtension(path);
            list.Add(new MenuExportNode { Title = titleOf(key), FormKey = key });
        }
        return file;
    }

    /// <summary>樹裡所有的畫面代號</summary>
    public static IEnumerable<string> Keys(IEnumerable<MenuTree> trees) =>
        trees.SelectMany(t => Keys(t.Children).Prepend(t.Node.FormKey)).Where(k => !string.IsNullOrEmpty(k));

    /// <summary>
    /// 把 source 的架構合併進 target：同名的資料夾沿用，沒有就加在最後面；placed 裡已經有位置的畫面不重複放
    /// （canPlace = false 的畫面不放，例如沒匯入成功的）。回傳放進去的畫面數
    /// </summary>
    public static int Merge(List<MenuTree> target, List<MenuExportNode> source, HashSet<string> placed, Func<string, bool> canPlace)
    {
        int count = 0;
        foreach (var s in source)
        {
            if (s.IsFolder)
            {
                var folder = target.FirstOrDefault(t => t.Node.IsFolder && t.Node.Title == s.Title);
                bool created = folder == null;
                folder ??= new MenuTree { Node = new MenuNode { Title = s.Title } };
                int added = Merge(folder.Children, s.Children, placed, canPlace);
                if (created && added > 0) target.Add(folder);            // 新資料夾裡有畫面才加
                count += added;
            }
            else if (canPlace(s.FormKey) && placed.Add(s.FormKey))
            {
                target.Add(new MenuTree { Node = new MenuNode { Title = s.Title, FormKey = s.FormKey } });
                count++;
            }
        }
        return count;
    }

    /// <summary>從樹裡拿掉 keys 裡的畫面（例如已經放進選單的，不要再留在「未放進選單的畫面」）</summary>
    public static void Remove(List<MenuTree> trees, ISet<string> keys)
    {
        trees.RemoveAll(t => !t.Node.IsFolder && keys.Contains(t.Node.FormKey));
        foreach (var t in trees) Remove(t.Children, keys);
    }
}
