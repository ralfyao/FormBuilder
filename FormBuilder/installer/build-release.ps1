<#
.SYNOPSIS
  發布 ERP Studio 新版本：自含式發布 → 產生自動更新用的資料夾 → 製作 MSI 安裝程式（WiX，第一次建置時自動從 NuGet 下載）。

.EXAMPLE
  .\build-release.ps1 -Version 1.0.1 -Notes "修正列印邊界" -UpdateShare "\\server\ERPStudio\release"
  .\build-release.ps1 -Project ERPClient -Version 1.0.1 -UpdateShare "\\server\ERPStudio\release"   （使用者端；輸出在 ..\release-client）
  兩個程式用同一個 -UpdateShare：檔案會放在 \\server\ERPStudio\release\FormBuilder、...\ERPClient 子資料夾，
  使用者電腦的「更新來源」都設成 \\server\ERPStudio\release

  產生：
    ..\release\files\                  新版的全部檔案（自含 .NET，不用另外安裝）
    ..\release\version.json            版本與更新說明
    ..\release\ERPStudio-Setup-x.y.z.msi   MSI 安裝程式（使用者端是 ..\release-client\ERPClient-Setup-x.y.z.msi）
  有給 -UpdateShare 時，把 files 和 version.json 複製到共用資料夾；
  使用者電腦的「更新來源」設成這個資料夾，下次啟動就會自動更新。
#>
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$Notes = "",
    [string]$UpdateShare = "",
    # 輸出資料夾（預設是專案旁的 release）
    [string]$OutputRoot = "",
    # FormBuilder = ERP Studio（後台：設計器 + 系統管理）；ERPClient = ERP 系統（使用者端）
    [ValidateSet("FormBuilder", "ERPClient")][string]$Project = "FormBuilder"
)
$ErrorActionPreference = "Stop"
if ($Project -eq "ERPClient") {
    $csproj = Resolve-Path (Join-Path $PSScriptRoot "..\..\ERPClient\ERPClient.csproj")
    $defaultRelease = Join-Path $PSScriptRoot "..\release-client"
} else {
    $csproj = Resolve-Path (Join-Path $PSScriptRoot "..\FormBuilder.csproj")
    $defaultRelease = Join-Path $PSScriptRoot "..\release"
}
$wixproj = Join-Path $PSScriptRoot "msi\ERPSetup.wixproj"
$release = [IO.Path]::GetFullPath($(if ($OutputRoot) { $OutputRoot } else { $defaultRelease }))
$files = Join-Path $release "files"

Write-Host "1/4 發布 $Version（自含式 win-x64）…"
if (Test-Path $files) { Remove-Item $files -Recurse -Force }
dotnet publish $csproj -c Release -r win-x64 --self-contained true -p:Version=$Version -p:PublishSingleFile=false -o $files
if ($LASTEXITCODE -ne 0) { throw "dotnet publish 失敗" }

Write-Host "2/4 寫 version.json…"
$info = [ordered]@{ App = $Project; Version = $Version; Notes = $Notes; BuiltAt = (Get-Date).ToString("yyyy/MM/dd HH:mm") }
[IO.File]::WriteAllText((Join-Path $release "version.json"), ($info | ConvertTo-Json), (New-Object Text.UTF8Encoding $false))

Write-Host "3/4 製作 MSI 安裝程式…"
$msiBin = Join-Path $PSScriptRoot "msi\bin\$Project"
if (Test-Path $msiBin) { Remove-Item $msiBin -Recurse -Force }
dotnet build $wixproj -c Release "-p:App=$Project" "-p:AppVersion=$Version" "-p:SourceDir=$files"
if ($LASTEXITCODE -ne 0) { throw "MSI 安裝程式建置失敗" }
Get-ChildItem $msiBin -Recurse -Filter *.msi | Copy-Item -Destination $release -Force

if ($UpdateShare) {
    # 兩個程式共用同一個更新來源，各放在自己名稱的子資料夾（使用者電腦的「更新來源」設成 $UpdateShare 本身）
    $target = Join-Path $UpdateShare $Project
    Write-Host "4/4 複製到更新來源 $target …"
    New-Item -ItemType Directory -Force $target | Out-Null
    robocopy $files (Join-Path $target "files") /MIR /R:3 /W:2 /NFL /NDL /NJH /NJS | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "複製到更新來源失敗（robocopy $LASTEXITCODE）" }
    # version.json 最後才複製：檔案都到齊了，使用者才會看到新版本
    Copy-Item (Join-Path $release "version.json") (Join-Path $target "version.json") -Force
} else {
    Write-Host "4/4 沒有指定 -UpdateShare，略過複製到更新來源"
}
Write-Host "完成：$release"
