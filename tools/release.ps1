<#
.SYNOPSIS
  Dong goi QuestLog thanh bo cai Velopack va (tuy chon) phat hanh len repo public.

.DESCRIPTION
  Bo cai gom 3 exe trong mot thu muc:
    QuestLog.exe              vo WebView2 (MasterPlay.Shell) - exe chinh
    host\MasterPlay.Host.exe  API + giao dien da build (host\wwwroot)
    tray\MasterPlay.TrayApp.exe

  Repo phat hanh CHI chua file cai dat, ma nguon nam o repo private. Truoc khi
  dong goi, script chan neu thu muc publish co .pdb/.map hoac bat ky gia tri bi
  mat nao lay tu auth.local.json / config.json tren may nay.

.EXAMPLE
  .\tools\release.ps1 -Version 0.1.0            # chi dong goi vao .artifacts\release\releases
  .\tools\release.ps1 -Version 0.1.0 -Upload    # dong goi + phat hanh len GitHub
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$')]
    [string]$Version,

    [switch]$Upload
)

$ErrorActionPreference = 'Stop'

$PackId = 'QuestLogDesktop'
$ReleasesRepo = 'https://github.com/danhtran172/questlog-releases'
$Runtimes = 'net8-x64-desktop,net8-x64-aspnetcore'

$Root = Split-Path $PSScriptRoot -Parent
$Work = Join-Path $Root '.artifacts\release'
$Publish = Join-Path $Work 'publish'
$Releases = Join-Path $Work 'releases'

function Invoke-Native([string]$What, [scriptblock]$Command, [switch]$AllowFailure) {
    Write-Host "==> $What" -ForegroundColor Cyan
    # PowerShell 5.1 bien moi dong stderr (npm/dotnet canh bao) thanh loi; voi
    # 'Stop' la dung ca script. Chi ma thoat moi quyet dinh thanh/bai.
    $previous = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $Command | Out-Host }
    finally { $ErrorActionPreference = $previous }
    $ok = $LASTEXITCODE -eq 0
    if (-not $ok -and -not $AllowFailure) { throw "$What that bai (exit $LASTEXITCODE)." }
    if ($AllowFailure) { return $ok }
}

if (-not (Get-Command vpk -ErrorAction SilentlyContinue)) {
    throw 'Chua co vpk. Cai bang: dotnet tool install -g vpk'
}

if (Test-Path $Publish) { Remove-Item $Publish -Recurse -Force }
# Phat hanh that: chi dung ban truoc tai tu GitHub, khong bao gio day nham goi
# thu nghiem con nam trong thu muc releases local.
if ($Upload -and (Test-Path $Releases)) { Remove-Item $Releases -Recurse -Force }
New-Item -ItemType Directory -Force $Publish, $Releases | Out-Null

# ---------- 1. Giao dien ----------
Push-Location (Join-Path $Root 'MasterPlay.Web')
try {
    if (-not (Test-Path 'node_modules')) { Invoke-Native 'npm ci' { npm ci --no-audit --no-fund } }
    Invoke-Native 'Build giao dien' { npm run build }
}
finally { Pop-Location }

# ---------- 2. Ba tien trinh .NET ----------
# Framework-dependent: Setup.exe cua Velopack tu cai .NET Desktop + ASP.NET Core
# runtime neu may chua co, nen bo cai va moi ban cap nhat deu nho.
$publishArgs = @(
    '-c', 'Release', '-r', 'win-x64', '--self-contained', 'false', '--nologo', '-clp:ErrorsOnly',
    "-p:Version=$Version", '-p:DebugType=none', '-p:DebugSymbols=false',
    # Khong gan ma commit cua repo private vao version cua exe.
    '-p:IncludeSourceRevisionInInformationalVersion=false'
)
Invoke-Native 'Publish Shell' { dotnet publish (Join-Path $Root 'MasterPlay.Shell\MasterPlay.Shell.csproj') @publishArgs -o $Publish }
Invoke-Native 'Publish Host' { dotnet publish (Join-Path $Root 'MasterPlay.Host\MasterPlay.Host.csproj') @publishArgs -o (Join-Path $Publish 'host') }
Invoke-Native 'Publish Tray' { dotnet publish (Join-Path $Root 'MasterPlay.TrayApp\MasterPlay.TrayApp.csproj') @publishArgs -o (Join-Path $Publish 'tray') }

$wwwroot = Join-Path $Publish 'host\wwwroot'
New-Item -ItemType Directory -Force $wwwroot | Out-Null
Copy-Item (Join-Path $Root 'MasterPlay.Web\dist\*') $wwwroot -Recurse -Force

# ---------- 3. Chan ro ri ----------
Write-Host '==> Kiem tra ro ri ma nguon / bi mat' -ForegroundColor Cyan
$files = Get-ChildItem $Publish -Recurse -File

$debugFiles = $files | Where-Object { $_.Extension -in '.pdb', '.map' }
if ($debugFiles) { throw "Con file debug trong ban cai: $($debugFiles.FullName -join ', ')" }

$configFiles = $files | Where-Object { $_.Name -match '^(auth\.local.*|config)\.json$' }
if ($configFiles) { throw "File cau hinh bi mat lot vao ban cai: $($configFiles.FullName -join ', ')" }

# Gom moi gia tri bi mat dang co tren may (ten khoa chua key/secret/password/token).
$secretSources = @(
    (Join-Path $env:APPDATA 'QuestLog\auth.local.json'),
    (Join-Path $env:APPDATA 'QuestLog\config.json'),
    (Join-Path $env:APPDATA 'MasterPlay\auth.local.json'),
    (Join-Path $env:APPDATA 'MasterPlay\config.json'),
    (Join-Path $Root 'auth.local.json'),
    (Join-Path $Root 'auth.local.example.json')
) | Where-Object { Test-Path $_ }

$secrets = New-Object System.Collections.Generic.HashSet[string]
function Add-Secrets($node, [string]$name) {
    if ($null -eq $node) { return }
    if ($node -is [string]) {
        if ($name -match '(?i)key|secret|password|token' -and $node.Length -ge 12) { [void]$secrets.Add($node) }
        return
    }
    if ($node -is [System.Management.Automation.PSCustomObject]) {
        foreach ($p in $node.PSObject.Properties) { Add-Secrets $p.Value $p.Name }
    }
    elseif ($node -is [System.Collections.IEnumerable]) {
        foreach ($item in $node) { Add-Secrets $item $name }
    }
}
foreach ($source in $secretSources) {
    try { Add-Secrets (Get-Content $source -Raw -Encoding UTF8 | ConvertFrom-Json) '' }
    catch { throw "Khong doc duoc $source de kiem tra bi mat: $($_.Exception.Message)" }
}

$latin1 = [Text.Encoding]::GetEncoding(28591)
$jwt = [regex]'eyJ[A-Za-z0-9_-]{10,}\.(eyJ[A-Za-z0-9_-]{10,})\.[A-Za-z0-9_-]{10,}'
$leaks = @()
foreach ($file in $files) {
    $bytes = [IO.File]::ReadAllBytes($file.FullName)
    $texts = @($latin1.GetString($bytes), [Text.Encoding]::Unicode.GetString($bytes))
    foreach ($text in $texts) {
        foreach ($secret in $secrets) {
            if ($text.Contains($secret)) { $leaks += "$($file.FullName): gia tri bi mat tu file cau hinh" }
        }
        # Khoa service_role cua Supabase o bat ky dang nao.
        foreach ($match in $jwt.Matches($text)) {
            $payload = $match.Groups[1].Value.Replace('-', '+').Replace('_', '/')
            $payload = $payload.PadRight($payload.Length + (4 - $payload.Length % 4) % 4, '=')
            try {
                $claims = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload))
                if ($claims -match '"role"\s*:\s*"service_role"') { $leaks += "$($file.FullName): Supabase service_role key" }
            }
            catch { }
        }
    }
}
if ($leaks) { throw "DUNG: ban cai chua bi mat:`n$(($leaks | Select-Object -Unique) -join "`n")" }
Write-Host "    $($files.Count) file sach (doi chieu $($secrets.Count) gia tri bi mat)."

# ---------- 4. Dong goi ----------
$token = $null
if ($Upload) {
    $token = (gh auth token).Trim()
    if (-not $token) { throw 'Chua dang nhap gh (gh auth login).' }

    # Lay ban truoc ve de vpk tao goi delta (cap nhat chi tai phan khac biet).
    $downloaded = Invoke-Native 'Tai ban truoc de tao delta' -AllowFailure {
        vpk download github --repoUrl $ReleasesRepo --token $token -o $Releases
    }
    if (-not $downloaded) { Write-Warning 'Chua co ban truoc (lan phat hanh dau?) - bo qua delta.' }
}

Invoke-Native "Dong goi $PackId $Version" {
    vpk pack `
        --packId $PackId `
        --packVersion $Version `
        --packTitle 'QuestLog' `
        --packAuthors 'QuestLog' `
        --packDir $Publish `
        --runtime 'win-x64' `
        --mainExe 'QuestLog.exe' `
        --icon (Join-Path $Root 'MasterPlay.Shell\app.ico') `
        --framework $Runtimes `
        --outputDir $Releases
}

# ---------- 5. Bo cai chon thu muc ----------
# Setup.exe cua Velopack cai thang vao %LOCALAPPDATA%\QuestLogDesktop, khong hoi. QuestLog-Setup.exe
# (.NET Framework 4.8, co san tren Windows) nhung no vao, hoi thu muc roi goi --installto.
$installerOut = Join-Path $Work 'installer'
if (Test-Path $installerOut) { Remove-Item $installerOut -Recurse -Force }
$velopackSetup = Join-Path $Releases "$PackId-win-Setup.exe"
Invoke-Native 'Build QuestLog-Setup.exe' {
    dotnet build (Join-Path $Root 'MasterPlay.Installer\MasterPlay.Installer.csproj') `
        -c Release --nologo -clp:ErrorsOnly `
        "-p:Version=$Version" '-p:DebugType=none' '-p:DebugSymbols=false' `
        "-p:VelopackSetup=$velopackSetup" `
        -o $installerOut
}
Copy-Item (Join-Path $installerOut 'QuestLog-Setup.exe') $Releases -Force

if ($Upload) {
    Invoke-Native "Phat hanh v$Version len $ReleasesRepo" {
        vpk upload github `
            --repoUrl $ReleasesRepo `
            --token $token `
            --outputDir $Releases `
            --publish `
            --releaseName "QuestLog $Version" `
            --tag "v$Version"
    }
    # vpk upload chi day cac file no tao ra.
    Invoke-Native "Them QuestLog-Setup.exe vao v$Version" {
        gh release upload "v$Version" (Join-Path $Releases 'QuestLog-Setup.exe') --repo ($ReleasesRepo -replace '^https://github.com/', '') --clobber
    }
    Write-Host "Xong. Ban cai: $ReleasesRepo/releases/latest" -ForegroundColor Green
}
else {
    Write-Host "Xong. Bo cai o $Releases\QuestLog-Setup.exe (chua phat hanh; them -Upload de day len GitHub)." -ForegroundColor Green
}
