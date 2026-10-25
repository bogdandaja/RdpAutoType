# Compileaza TestTyper.cs in doua variante: 32-bit si 64-bit.
# Foloseste csc.exe din .NET Framework (nu are nevoie de .NET SDK).

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) {
    $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe"
}
Write-Host "csc: $csc"

$src = Join-Path $PSScriptRoot "TestTyper.cs"

# 32-bit (platform:x86)
& $csc /nologo /platform:x86 /out:"$PSScriptRoot\TestTyper32.exe" "$src"
# 64-bit (platform:x64)
& $csc /nologo /platform:x64 /out:"$PSScriptRoot\TestTyper64.exe" "$src"

Write-Host ""
Write-Host "Gata:"
Get-ChildItem "$PSScriptRoot\TestTyper*.exe" | Select Name, Length
