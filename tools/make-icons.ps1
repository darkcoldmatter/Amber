# Renders the Amber icon (shape and colors from Figma "app_icon") into .ico files.
# Run from Windows PowerShell:  powershell -File tools\make-icons.ps1

Add-Type -AssemblyName PresentationCore, WindowsBase

$shape = 'M20 7C31.0457 7 40 15.8957 40 26.8691C40 28.7983 39.723 30.6632 39.2067 32.4268C38.8601 33.6109 37.5921 34.215 36.3844 33.9303C31.0863 32.6814 25.5591 32.0204 19.8765 32.0204C14.2739 32.0204 8.82235 32.6631 3.59244 33.8782C2.38386 34.1589 1.11788 33.5508 0.775177 32.3656C0.270408 30.62 0 28.7759 0 26.8691C0 15.8957 8.9543 7 20 7Z'
$sizes = 16, 20, 24, 32, 40, 48, 64, 256
$outDir = Join-Path $PSScriptRoot '..\src\Amber\Assets'

function Render-Png([int]$size, [string]$top, [string]$bottom) {
    $brush = New-Object Windows.Media.LinearGradientBrush(
        [Windows.Media.ColorConverter]::ConvertFromString($top),
        [Windows.Media.ColorConverter]::ConvertFromString($bottom), 90)
    $visual = New-Object Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    $dc.PushTransform((New-Object Windows.Media.ScaleTransform($size / 40), ($size / 40)))
    $dc.PushTransform((New-Object Windows.Media.TranslateTransform 0, -0.5)) # center the 27px-tall shape vertically
    $dc.DrawGeometry($brush, $null, [Windows.Media.Geometry]::Parse($shape))
    $dc.Close()

    $bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $encoder = New-Object Windows.Media.Imaging.PngBitmapEncoder
    $encoder.Frames.Add([Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = New-Object IO.MemoryStream
    $encoder.Save($stream)
    return , $stream.ToArray()
}

function Write-Ico([string]$fileName, [string]$top, [string]$bottom) {
    $images = foreach ($size in $sizes) { , (Render-Png $size $top $bottom) }
    $writer = New-Object IO.BinaryWriter([IO.File]::Create((Join-Path $outDir $fileName)))
    $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]$sizes.Count)
    $offset = 6 + 16 * $sizes.Count
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $dimension = if ($sizes[$i] -ge 256) { 0 } else { $sizes[$i] }
        $writer.Write([byte]$dimension); $writer.Write([byte]$dimension); $writer.Write([byte]0); $writer.Write([byte]0)
        $writer.Write([uint16]1); $writer.Write([uint16]32)
        $writer.Write([uint32]$images[$i].Length); $writer.Write([uint32]$offset)
        $offset += $images[$i].Length
    }
    foreach ($image in $images) { $writer.Write($image) }
    $writer.Close()
}

New-Item -ItemType Directory -Force $outDir | Out-Null
Write-Ico 'amber.ico' '#DFE2EB' '#FEDA79'      # on (also the app icon)
Write-Ico 'amber-off.ico' '#DFE2EB' '#9AA0AD'  # off: grey
