# Renders the resize cursor (shape and colors from Figma "Cursor & hover") into a .cur file.
# Run from Windows PowerShell:  powershell -File tools\make-cursor.ps1

Add-Type -AssemblyName PresentationCore, WindowsBase

$shape = 'M11.5001 1.31224C11.5004 0.650539 12.1691 0.26754 12.6476 0.655017L17.2003 4.34154C17.6002 4.66541 17.6002 5.33407 17.2003 5.65795L12.6476 9.34447C12.1691 9.73195 11.5004 9.34895 11.5001 8.68724V6.49974H6.50013V8.68724C6.4999 9.34895 5.83111 9.73195 5.35267 9.34447L0.799933 5.65795C0.400022 5.33407 0.400022 4.66541 0.799933 4.34154L5.35267 0.655017C5.83111 0.26754 6.4999 0.650539 6.50013 1.31224V3.49974H11.5001V1.31224Z'
$sizes = 32, 48, 64  # for 100 %, 150 % and 200 % display scale; the shape (18 x 10 with its outline) is design size at 32
$outFile = Join-Path $PSScriptRoot '..\src\Amber\Assets\resize.cur'

# One frame in the format Windows' own cursors use: header, 32-bit colors, 1-bit mask, rows bottom-up.
function Render-Frame([int]$size) {
    $scale = $size / 32
    $left = [Math]::Floor(($size - 18 * $scale) / 2)  # centred, on whole pixels so the dark edges stay sharp
    $top = [Math]::Floor(($size - 10 * $scale) / 2)
    $visual = New-Object Windows.Media.DrawingVisual
    $dc = $visual.RenderOpen()
    $dc.PushTransform((New-Object Windows.Media.MatrixTransform($scale, 0, 0, $scale, $left, $top)))
    $fill = New-Object Windows.Media.SolidColorBrush([Windows.Media.ColorConverter]::ConvertFromString('#271F1F'))
    $dc.DrawGeometry($fill, (New-Object Windows.Media.Pen([Windows.Media.Brushes]::White, 1)), [Windows.Media.Geometry]::Parse($shape))
    $dc.Close()

    $bitmap = New-Object Windows.Media.Imaging.RenderTargetBitmap($size, $size, 96, 96, [Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $straight = New-Object Windows.Media.Imaging.FormatConvertedBitmap($bitmap, [Windows.Media.PixelFormats]::Bgra32, $null, 0)
    $pixels = New-Object byte[] ($size * $size * 4)
    $straight.CopyPixels($pixels, $size * 4, 0)

    $maskStride = [int][Math]::Ceiling($size / 32) * 4
    $stream = New-Object IO.MemoryStream
    $writer = New-Object IO.BinaryWriter($stream)
    $writer.Write([uint32]40); $writer.Write([int32]$size); $writer.Write([int32](2 * $size))  # height counts colors + mask
    $writer.Write([uint16]1); $writer.Write([uint16]32); $writer.Write([uint32]0)
    $writer.Write([uint32]($size * $size * 4 + $size * $maskStride))
    $writer.Write([int32]0); $writer.Write([int32]0); $writer.Write([uint32]0); $writer.Write([uint32]0)
    for ($y = $size - 1; $y -ge 0; $y--) { $writer.Write($pixels, $y * $size * 4, $size * 4) }
    for ($y = $size - 1; $y -ge 0; $y--) {
        $row = New-Object byte[] $maskStride  # bit set = fully transparent pixel
        for ($x = 0; $x -lt $size; $x++) {
            if ($pixels[($y * $size + $x) * 4 + 3] -eq 0) { $row[$x -shr 3] = $row[$x -shr 3] -bor (0x80 -shr ($x % 8)) }
        }
        $writer.Write($row)
    }
    $writer.Flush()
    return , $stream.ToArray()
}

$frames = foreach ($size in $sizes) { , (Render-Frame $size) }
$writer = New-Object IO.BinaryWriter([IO.File]::Create($outFile))
$writer.Write([uint16]0); $writer.Write([uint16]2); $writer.Write([uint16]$sizes.Count)  # type 2 = cursor
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $writer.Write([byte]$sizes[$i]); $writer.Write([byte]$sizes[$i]); $writer.Write([byte]0); $writer.Write([byte]0)
    $writer.Write([uint16]($sizes[$i] / 2)); $writer.Write([uint16]($sizes[$i] / 2))  # hotspot: centre of the frame
    $writer.Write([uint32]$frames[$i].Length); $writer.Write([uint32]$offset)
    $offset += $frames[$i].Length
}
foreach ($frame in $frames) { $writer.Write($frame) }
$writer.Close()
