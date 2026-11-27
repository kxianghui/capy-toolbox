param(
    [string]$Type = 'release',
    [string]$Ver = '0.0.1',
    [string]$Num = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$src = Join-Path $root 'src'
$dist = Join-Path $root 'dist'
$build = Join-Path $root '_build'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

Write-Host '[1/4] Preparing Python environment...'
$venv = Join-Path $build 'venv'
$py = Join-Path $venv 'Scripts\python.exe'
if (!(Test-Path $py)) { python -m venv $venv }
& $py -m pip install -q -r (Join-Path $src 'requirements-engine.txt')
if ($LASTEXITCODE -ne 0) { throw 'pip install failed' }

Write-Host '[2/4] Building pdf2docx engine with PyInstaller...'
$engineDir = Join-Path $build 'engine'
& $py -m PyInstaller --noconfirm --clean (Join-Path $src 'pdf2docx_engine.spec') --distpath $engineDir --workpath (Join-Path $build 'work')
if ($LASTEXITCODE -ne 0) { throw 'Engine build failed' }
$engine = Join-Path $engineDir 'pdf2docx_engine.exe'
if (!(Test-Path $engine)) { throw 'Engine exe not found' }

Write-Host '[3/4] Compiling single-file GUI with embedded engine...'
$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$icons = Join-Path $root 'icon'
# Version naming: release has no type suffix; alpha/beta/etc. append -<type><num>.
$type = ($Type + '').Trim().ToLowerInvariant()
$version = ($Ver + '').Trim()
$number = ($Num + '').Trim()
if ($version -eq '') { throw '-Ver is required' }
foreach ($token in @($type, $version, $number)) {
    if ($token -notmatch '^[0-9A-Za-z._+\-]*$') { throw "Invalid version token: $token" }
}
if ($type -eq '' -or $type -eq 'release') {
    $exeName = "Capy-v$version.exe"
    $aboutVersion = "v$version"
    $windowTitle = "Capy $aboutVersion"
} else {
    $typeTitle = $type.Substring(0, 1).ToUpperInvariant() + $type.Substring(1)
    if ($number -ne '') {
        $suffix = "$type$number"
        $captionVersion = "v$version-$number"
    } else {
        $suffix = $type
        $captionVersion = "v$version"
    }
    $exeName = "Capy-v$version-$suffix.exe"
    $aboutVersion = "v$version-$suffix"
    $windowTitle = "Capy $typeTitle $captionVersion"
}

# Emit version constants as C# so the About page reads them at compile time.
$genDir = Join-Path $build 'gen'
New-Item -ItemType Directory -Force -Path $genDir | Out-Null
$genFile = Join-Path $genDir 'BuildInfo.g.cs'
@"
namespace PdfWordTool
{
    internal static class BuildInfo
    {
        public const string Type = "$type";
        public const string Version = "$version";
        public const string Number = "$number";
        public const string About = "$aboutVersion";
        public const string Title = "$windowTitle";
    }
}
"@ | Set-Content -Path $genFile -Encoding UTF8

$out = Join-Path $dist $exeName
Push-Location $src
try {
    & $csc -nologo -target:winexe -optimize+ -platform:anycpu `
        "-out:$out" `
        "-win32icon:$icons\App.ico" `
        -r:System.dll -r:System.Core.dll -r:System.Drawing.dll -r:System.Windows.Forms.dll -r:Microsoft.CSharp.dll `
        "-resource:$engine,PdfWordTool.pdf2docx_engine.exe" `
        "-resource:$icons\Capy.ico,PdfWordTool.Capy.ico" `
        "-resource:$icons\CapyLogo.ico,PdfWordTool.CapyLogo.ico" `
        "-resource:$icons\CapyTray.ico,PdfWordTool.CapyTray.ico" `
        Program.cs Engine.cs AppSettings.cs MainForm.cs UiControls.cs ToolPage.cs ImageOps.cs ImageWriters.cs `
        PdfToWordPage.cs ImageCompressPage.cs ImageConvertPage.cs "$genFile"
    if ($LASTEXITCODE -ne 0) { throw 'C# compile failed' }
} finally {
    Pop-Location
}

Write-Host '[4/4] Embedding file version info...'
& python (Join-Path $root 'embed_version.py') `
    --exe $out --fileversion $version --productversion $aboutVersion --original $exeName
if ($LASTEXITCODE -ne 0) { throw 'Embedding file version info failed' }

Write-Host "Done: $out"
