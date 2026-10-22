$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$out = "ZenShrink.exe"

$src = @(
    "Source\AssemblyInfo.cs",
    "Source\ShrinkEngine.cs",
    "Source\MainWindow.cs",
    "Source\App.cs"
)

$refs = @(
    "System.dll",
    "System.Core.dll",
    "System.Drawing.dll",
    "System.Windows.Forms.dll",
    "System.Xaml.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\WindowsBase.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationCore.dll",
    "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\WPF\PresentationFramework.dll"
)

$refArgs = $refs | ForEach-Object { "/r:`"$_`"" }
$srcArgs = $src | ForEach-Object { "`"$_`"" }

$params = @(
    "/target:winexe",
    "/out:$out",
    "/platform:anycpu",
    "/optimize+",
    "/win32icon:app.ico"
) + $refArgs + $srcArgs

Write-Host "Derleniyor: ZenShrink.exe..." -ForegroundColor Cyan
& $csc $params

if ($LASTEXITCODE -eq 0) {
    Write-Host "Başarıyla derlendi! $out" -ForegroundColor Green
    Get-Item $out | Select-Object Name, Length, LastWriteTime
} else {
    Write-Host "Derleme hatası oluştu!" -ForegroundColor Red
}
