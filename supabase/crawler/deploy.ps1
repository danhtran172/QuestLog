#Requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidatePattern('^[a-z]{20}$')][string]$ProjectRef,
    [switch]$PreflightOnly,
    [switch]$EnableCron
)
$ErrorActionPreference = 'Stop'
$taskRoot = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
function Read-TaskEnv([string]$Name) {
    $value = [Environment]::GetEnvironmentVariable($Name, 'Process')
    if ([string]::IsNullOrWhiteSpace($value)) { $value = [Environment]::GetEnvironmentVariable($Name, 'User') }
    if ([string]::IsNullOrWhiteSpace($value)) { throw "Missing environment variable: $Name" }
    return $value.Trim()
}
$taskToken = Read-TaskEnv 'SUPABASE_ACCESS_TOKEN'
$taskHeaders = @{ Authorization = "Bearer $taskToken" }
$taskApi = "https://api.supabase.com/v1/projects/$ProjectRef"
function Invoke-TaskApi([string]$Path, [object]$Body) {
    try {
        Invoke-RestMethod -Method Post -Uri "$taskApi/$Path" -Headers $taskHeaders -ContentType 'application/json' `
            -Body (ConvertTo-Json -InputObject $Body -Depth 20 -Compress) -TimeoutSec 120
    } catch { throw "Supabase request failed ($Path). Inspect Dashboard logs; response hidden to protect secrets." }
}
function Invoke-TaskSql([string]$Sql, [object[]]$Parameters = @()) {
    $body = @{ query = $Sql }
    if ($Parameters.Count) { $body.parameters = $Parameters }
    Invoke-TaskApi 'database/query' $body
}

$preflight = @(Invoke-TaskSql @'
select to_regclass('public.metadata_games') is not null as catalog,
       to_regclass('public.metadata_audit_events') is not null as audit,
       to_regclass('public.metadata_publish_rules') is not null as publishing,
       to_regclass('crawler.state') is not null as installed,
       to_regclass('integrations.integration_configs') is not null as integrations,
       pg_database_size(current_database()) as database_bytes,
       exists(select 1 from information_schema.columns where table_schema='public'
         and table_name='metadata_games' and column_name='poster_url') as image_roles;
'@)[0]
$preflight | Format-List
if (-not ($preflight.catalog -and $preflight.audit -and $preflight.publishing -and $preflight.integrations -and $preflight.image_roles)) {
    throw 'Required metadata/integration migrations are incomplete. Apply the documented migrations before deploying the crawler.'
}
if ($PreflightOnly) { return }
if ([long]$preflight.database_bytes -ge 424673280) { throw 'Database already exceeds the default discovery headroom. Review project quota before deployment.' }
$taskSecret = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
$taskUrl = "https://$ProjectRef.supabase.co/functions/v1/igdb-crawler"
$oldToken = $env:SUPABASE_ACCESS_TOKEN
$env:SUPABASE_ACCESS_TOKEN = $taskToken
try {
    if (-not $preflight.installed) {
        $migration = Get-Content -Raw -LiteralPath (Join-Path $taskRoot 'supabase/migrations/20261001175945_igdb_catalog_crawler.sql')
        Invoke-TaskSql ("begin;`n" + $migration + "`ncommit;") | Out-Null
    }
    # Stop scheduling before rotating this worker's secret. No data removed.
    Invoke-TaskSql (Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'pause.sql')) | Out-Null
    Invoke-TaskApi 'secrets' @(
        @{ name = 'CRAWLER_SECRET'; value = $taskSecret }
    ) | Out-Null
    & npx --yes supabase@2.119.0 functions deploy igdb-crawler --project-ref $ProjectRef --use-api --no-verify-jwt --workdir $taskRoot
    if ($LASTEXITCODE -ne 0) { throw 'Function deployment failed.' }
    foreach ($entry in @(@{ name = 'crawler_url'; value = $taskUrl }, @{ name = 'crawler_secret'; value = $taskSecret })) {
        $existing = @(Invoke-TaskSql 'select id from vault.secrets where name=$1' @($entry.name))
        if ($existing.Count) {
            Invoke-TaskSql 'select vault.update_secret($1::uuid,$2)' @($existing[0].id, $entry.value) | Out-Null
        } else {
            Invoke-TaskSql 'select vault.create_secret($1,$2)' @($entry.value, $entry.name) | Out-Null
        }
    }
    $invokeHeaders = @{ 'x-crawler-secret' = $taskSecret }
    $dry = Invoke-RestMethod -Method Post -Uri $taskUrl -Headers $invokeHeaders -ContentType 'application/json' -Body '{"dryRun":true}' -TimeoutSec 120
    if ($dry.dryRun -ne $true) { throw 'Dry run did not succeed.' }
    $dry | ConvertTo-Json -Compress
    if ($EnableCron) {
        Invoke-TaskSql "update crawler.state set enabled=true,next_run_at='-infinity' where singleton" | Out-Null
        try {
            $first = Invoke-RestMethod -Method Post -Uri $taskUrl -Headers $invokeHeaders -ContentType 'application/json' -Body '{}' -TimeoutSec 120
            if ($first.status -ne 'completed' -or $first.errors -ne 0) { throw 'First run incomplete.' }
            $first | Select-Object id,status,inserted,refreshed,errors | Format-List
            Invoke-TaskSql (Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'schedule.sql')) | Out-Null
        } catch {
            Invoke-TaskSql (Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'pause.sql')) | Out-Null
            throw 'First run or scheduling failed; crawler paused. Inspect crawler.runs and Edge logs.'
        }
        Write-Host 'Crawler deployed and scheduled every six hours.'
    } else { Write-Host 'Deployed and dry-run verified; crawler remains paused. Use -EnableCron for activation.' }
} finally {
    $env:SUPABASE_ACCESS_TOKEN = $oldToken
    $taskToken = $taskSecret = $null
}
