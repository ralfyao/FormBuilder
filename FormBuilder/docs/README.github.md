# FormBuilder（ERP Studio + ERP 系統）

用 WinForms（.NET 8）寫的類 4GL 畫面設計器與執行器。管理者連上資料庫，把資料表欄位拖到畫面上，就能做出查詢、新增、修改、刪除的維護畫面，不用寫程式。做好的畫面發布後，使用者登入 ERP 系統，依權限從選單開啟使用。

![畫面設計](FormBuilder/docs/screenshots/04-1-designer.png)

## 這個儲存庫有什麼

| 資料夾 | 程式 | 誰用 | 內容 |
|---|---|---|---|
| [`FormBuilder/`](FormBuilder) | ERP Studio | 系統管理者 | 畫面設計、列印樣板、使用者與權限、選單管理、稽核紀錄、企業識別、系統設定 |
| [`ERPClient/`](ERPClient) | ERP 系統 | 一般使用者 | 登入後依權限顯示功能選單，用分頁開啟發布好的畫面 |

`ERPClient` 用 `..\FormBuilder\` 的相對路徑直接編譯 FormBuilder 的原始檔（執行畫面、登入、權限等共用同一份程式碼），所以兩個資料夾要放在同一層。

## 主要功能

- **拖拉設計畫面**：欄位、單身 Grid、單頭清單、SQL 清單、JOIN、編輯視窗版面、按鈕、列印樣板，存成 JSON 畫面定義。
- **4GL 式操作**：查詢 → 首筆 / 上筆 / 下筆 / 末筆；新增、修改、刪除；單身明細在同一個交易裡存檔。
- **欄位規則**：必填、預設值（`TODAY()`、`USER()`…）、編號規則（`[BF][YYYY][MM][DD][XXX]`）、計算欄位公式。
- **按鈕動作**：內建動作，以及執行 SQL、自訂查詢、開啟其他畫面、開啟查詢條件畫面、列印報表。
- **多種資料庫**：畫面可以連 SQL Server、Oracle（12c 以上）或 MySQL；所有 SQL 都用參數傳值。
- **系統管理**：帳號密碼登入、角色權限（開啟 / 編修 / 核准 / 報表 / 輸出）、樹狀選單、稽核紀錄、企業識別。
- **發布與更新**：自含式發布、Inno Setup 安裝程式，啟動時從共用資料夾自動更新。

| 執行畫面 | ERP 系統 |
|---|---|
| ![執行畫面](FormBuilder/docs/screenshots/06-1-runtime.png) | ![ERP 系統](FormBuilder/docs/screenshots/03-4-erp-shell.png) |

## 快速開始

需要 Windows 10 / 11 和 .NET 8 SDK。系統資料庫（帳號、權限、選單、已發布畫面）目前只支援 SQL Server。

```bash
git clone https://github.com/ralfyao/FormBuilder.git
```

建置 ERP Studio（後台）：

```bash
dotnet build FormBuilder/FormBuilder.csproj
```

建置 ERP 系統（使用者端）：

```bash
dotnet build ERPClient/ERPClient.csproj
```

不登入、直接開畫面設計視窗（開發用）：

```bash
dotnet run --project FormBuilder/FormBuilder.csproj -- --designer
```

第一次正式啟動 ERP Studio 時，系統設定精靈會引導你選系統資料庫、建立資料表、建立第一個管理者帳號。

## 詳細說明

功能說明、啟動參數、第一次使用、發布新版本、專案結構、各資料庫的差異，請看 [FormBuilder/README.md](FormBuilder/README.md)。

## 關於這個儲存庫

這裡的內容是從公司內部的大儲存庫同步過來的，只包含 `FormBuilder/` 和 `ERPClient/` 兩個資料夾，並保留它們的提交歷史。同步方式見 [FormBuilder/README.md 的「同步到 GitHub」](FormBuilder/README.md#同步到-github)。
