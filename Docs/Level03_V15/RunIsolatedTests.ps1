param(
    [Parameter(Mandatory=$true)][string]$Platform,
    [Parameter(Mandatory=$true)][string]$ResultPath,
    [Parameter(Mandatory=$true)][string]$LogPath,
    [string]$Filter = '',
    [int]$RandomOrderSeed = 0,
    [int]$Repeat = 0,
    [switch]$Graphics
)
$ErrorActionPreference = 'Stop'
if (Get-Process -Name Unity -ErrorAction SilentlyContinue) { throw 'A Unity Editor is already running; do not touch shared PlayerPrefs.' }
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$editor = 'C:\Program Files\Unity\Hub\Editor\2023.1.22f1\Editor\Unity.exe'
$subkeys = @('Software\Unity\UnityEditor\DefaultCompany\My project','Software\DefaultCompany\My project')
$backupDir = Join-Path $env:TEMP ('SDG_V15_Prefs_' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $backupDir | Out-Null
$snapshots = @()
foreach ($subkey in $subkeys) {
    $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($subkey, $false)
    if ($null -eq $key) { throw "PlayerPrefs key not found: $subkey" }
    $values = @{}
    foreach ($name in $key.GetValueNames()) {
        $values[$name] = @{ Value = $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames); Kind = $key.GetValueKind($name) }
    }
    $key.Dispose()
    $snapshots += @{ Path = $subkey; Values = $values }
    & reg.exe export ("HKCU\" + $subkey) (Join-Path $backupDir ("prefs$($snapshots.Count).reg")) /y | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'Failed to export PlayerPrefs backup.' }
}
$arguments = @('-batchmode')
if (-not $Graphics) { $arguments += '-nographics' }
$arguments += @('-projectPath', ('"' + $root + '"'), '-runTests', '-testPlatform', $Platform)
if ($Filter) { $arguments += @('-testFilter', $Filter) }
if ($RandomOrderSeed -ne 0) { $arguments += @('-randomOrderSeed', $RandomOrderSeed.ToString()) }
if ($Repeat -gt 0) { $arguments += @('-repeat', $Repeat.ToString()) }
$arguments += @('-testResults', ('"' + $ResultPath + '"'), '-logFile', ('"' + $LogPath + '"'))
try {
    $process = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $process.WaitForExit()
    $process.Refresh()
    $exitCode = $process.ExitCode
} finally {
    foreach ($snapshot in $snapshots) {
        $key = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey($snapshot.Path, $true)
        if ($null -eq $key) { throw "Cannot restore PlayerPrefs: $($snapshot.Path); backups: $backupDir" }
        foreach ($name in $key.GetValueNames()) {
            if (-not $snapshot.Values.ContainsKey($name)) { $key.DeleteValue($name) }
        }
        foreach ($name in $snapshot.Values.Keys) {
            $entry = $snapshot.Values[$name]
            $key.SetValue($name, $entry.Value, $entry.Kind)
        }
        $key.Flush()
        foreach ($name in $snapshot.Values.Keys) {
            $entry = $snapshot.Values[$name]
            $actual = $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
            if ($key.GetValueKind($name) -ne $entry.Kind -or
                (Compare-Object @($actual) @($entry.Value))) { throw "PlayerPrefs verification failed; backups: $backupDir" }
        }
        $key.Dispose()
    }
    Write-Output "PlayerPrefs restored and verified. Backups: $backupDir"
}
exit $exitCode
