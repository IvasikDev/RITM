param([string]$AppSource = 'App.cs', [string]$Executable = 'RhythmTyping.exe')

$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$iconPath = Join-Path $PSScriptRoot 'Rhythm.ico'
$brandPath = Join-Path $PSScriptRoot 'RhythmBrand.png'

Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Bitmap]::new((Join-Path $PSScriptRoot 'RhythmIconSource.png'))
try {
    $area = [System.Drawing.Rectangle]::new(0, 0, $source.Width, $source.Height)
    $pixels = $source.LockBits($area, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $left = $source.Width
    $top = $source.Height
    $right = -1
    $bottom = -1
    try {
        $row = [byte[]]::new($source.Width * 4)
        for ($y = 0; $y -lt $source.Height; $y += 2) {
            [System.Runtime.InteropServices.Marshal]::Copy([IntPtr]::Add($pixels.Scan0, $y * $pixels.Stride), $row, 0, $row.Length)
            for ($x = 0; $x -lt $source.Width; $x += 2) {
                if ($row[4 * $x + 3] -lt 64) { continue }
                if ($x -lt $left) { $left = $x }
                if ($x -gt $right) { $right = $x }
                if ($y -lt $top) { $top = $y }
                if ($y -gt $bottom) { $bottom = $y }
            }
        }
    } finally { $source.UnlockBits($pixels) }
    if ($right -lt 0) { throw 'The icon has no visible pixels.' }
    $side = [Math]::Ceiling([Math]::Max($right - $left, $bottom - $top) * 1.075)
    $crop = [System.Drawing.RectangleF]::new(($left + $right - $side) / 2, ($top + $bottom - $side) / 2, $side, $side)

    $sizes = @(16, 20, 24, 32, 40, 48, 64, 96, 128, 256)
    $frames = [System.Collections.Generic.List[byte[]]]::new()
    foreach ($size in $sizes) {
        $frame = [System.Drawing.Bitmap]::new($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($frame)
        $png = [System.IO.MemoryStream]::new()
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $destination = [System.Drawing.RectangleF]::new(0, 0, $size, $size)
            $graphics.DrawImage($source, $destination, $crop, [System.Drawing.GraphicsUnit]::Pixel)
            $frame.Save($png, [System.Drawing.Imaging.ImageFormat]::Png)
            $bytes = $png.ToArray()
            $frames.Add($bytes)
            if ($size -eq 128) { [System.IO.File]::WriteAllBytes($brandPath, $bytes) }
        } finally {
            $png.Dispose()
            $graphics.Dispose()
            $frame.Dispose()
        }
    }
} finally { $source.Dispose() }

$icon = [System.IO.BinaryWriter]::new([System.IO.File]::Create($iconPath))
try {
    $icon.Write([uint16]0)
    $icon.Write([uint16]1)
    $icon.Write([uint16]$sizes.Length)
    $offset = 6 + 16 * $sizes.Length
    for ($index = 0; $index -lt $sizes.Length; $index++) {
        $length = $frames[$index].Length
        $dimension = [byte]($sizes[$index] -band 255)
        $icon.Write($dimension)
        $icon.Write($dimension)
        $icon.Write([uint16]0)
        $icon.Write([uint16]1)
        $icon.Write([uint16]32)
        $icon.Write([uint32]$length)
        $icon.Write([uint32]$offset)
        $offset += $length
    }
    foreach ($frame in $frames) { $icon.Write($frame) }
} finally { $icon.Dispose() }

& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:anycpu /optimize+ /codepage:65001 `
    "/out:$(Join-Path $PSScriptRoot $Executable)" "/win32icon:$iconPath" `
    "/resource:$(Join-Path $PSScriptRoot 'MainWindow.xaml'),RhythmTyping.MainWindow.xaml" `
    "/resource:$brandPath,RhythmTyping.Brand.png" `
    "/resource:$(Join-Path $PSScriptRoot 'HazardWarning.wav'),RhythmTyping.HazardWarning.wav" `
    "/reference:$wpf/PresentationFramework.dll" "/reference:$wpf/PresentationCore.dll" "/reference:$wpf/WindowsBase.dll" `
    /reference:System.Xaml.dll /reference:System.Xml.Linq.dll "/reference:$wpf/System.Speech.dll" `
    (Join-Path $PSScriptRoot $AppSource) (Join-Path $PSScriptRoot 'Trainer.cs') (Join-Path $PSScriptRoot 'Catalog.cs') `
    (Join-Path $PSScriptRoot 'HandGuide.cs') (Join-Path $PSScriptRoot 'SessionReport.cs') `
    (Join-Path $PSScriptRoot 'PracticeCorpus.cs') (Join-Path $PSScriptRoot 'CorpusText.cs') `
    (Join-Path $PSScriptRoot 'Poetry.cs') (Join-Path $PSScriptRoot 'WordSpeech.cs')
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed.' }
