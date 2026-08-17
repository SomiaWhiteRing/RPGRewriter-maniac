param(
    [string]$ExePath = (Join-Path $PSScriptRoot '..\bin\Release\RPGRewriter.exe')
)

$ErrorActionPreference = 'Stop'

if ([IntPtr]::Size -ne 4) {
    $powershell32 = Join-Path $env:WINDIR 'SysWOW64\WindowsPowerShell\v1.0\powershell.exe'
    & $powershell32 -NoProfile -ExecutionPolicy Bypass -File $PSCommandPath -ExePath $ExePath
    exit $LASTEXITCODE
}

$assembly = [Reflection.Assembly]::LoadFrom((Resolve-Path $ExePath))
$type = $assembly.GetType('RPGRewriter.M', $true)
$flags = [Reflection.BindingFlags]'Static,NonPublic,Public'

$encoding = [Text.Encoding]::GetEncoding(932)
$readEncodings = [Text.Encoding[]]::new(4)
$writeEncodings = [Text.Encoding[]]::new(4)
$readEncodingIds = [int[]]::new(4)
$writeEncodingIds = [int[]]::new(4)
for ($i = 0; $i -lt 4; $i++) {
    $readEncodings[$i] = $encoding
    $writeEncodings[$i] = $encoding
    $readEncodingIds[$i] = 932
    $writeEncodingIds[$i] = 932
}
$type.GetField('readEncodings', $flags).SetValue($null, $readEncodings)
$type.GetField('writeEncodings', $flags).SetValue($null, $writeEncodings)
$type.GetField('readEncodingIDs', $flags).SetValue($null, $readEncodingIds)
$type.GetField('writeEncodingIDs', $flags).SetValue($null, $writeEncodingIds)

$type.GetMethod('loadUserSettings', $flags).Invoke($null, @($true)) | Out-Null
$arguments = [string[]]@(
    'dummy', '-export',
    '-mapnames', 'Y',
    '-mapeventnames', 'N',
    '-switchnames', 'Y',
    '-variablenames', 'N',
    '-commoneventnames', 'Y',
    '-troopnames', 'N'
)
$type.GetMethod('parseCommandLineArgs', $flags).Invoke($null, (, $arguments)) | Out-Null

$getSetting = $type.GetMethod('getExtraneousSetting', $flags)
$expected = @{
    MapNames = $true
    MapEventNames = $false
    SwitchNames = $true
    VariableNames = $false
    CommonEventNames = $true
    TroopNames = $false
}

foreach ($entry in $expected.GetEnumerator()) {
    $actual = $getSetting.Invoke($null, @($entry.Key))
    if ($actual -ne $entry.Value) {
        throw "$($entry.Key): expected $($entry.Value), got $actual"
    }
}

Write-Output 'Extraneous CLI argument self-check passed.'
