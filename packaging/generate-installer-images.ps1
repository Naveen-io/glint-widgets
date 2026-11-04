Add-Type -AssemblyName System.Drawing

$root = Split-Path $PSScriptRoot -Parent
$icoPath = Join-Path $root "assets\app.ico"
$outDir = Join-Path $root "packaging\assets"
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

# Extract PNG frame from ICO (all frames are PNG-compressed, ToBitmap() is broken for these)
function Get-IconPng([string]$icoFile, [int]$frameIndex) {
    $bytes = [System.IO.File]::ReadAllBytes($icoFile)
    $offset = 6 + $frameIndex * 16
    $size = [BitConverter]::ToUInt32($bytes, $offset + 8)
    $dataOffset = [BitConverter]::ToUInt32($bytes, $offset + 12)
    $ms = New-Object System.IO.MemoryStream($bytes, $dataOffset, $size)
    $img = [System.Drawing.Image]::FromStream($ms)
    return $img
}

# Use the 256x256 frame (index 7) for best quality
$appIcon = Get-IconPng $icoPath 7
Write-Host "  Extracted 256x256 PNG from app.ico"

# --- Wizard sidebar image (410x797, 24-bit BMP) ---
$w = 410; $h = 797
$bmp = New-Object System.Drawing.Bitmap($w, $h, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

# Light background (#F5F5F7)
$g.Clear([System.Drawing.Color]::FromArgb(245, 245, 247))

# App icon centered, 96x96
$iconSize = 96
$iconX = [int](($w - $iconSize) / 2)
$iconY = 270
$g.DrawImage($appIcon, $iconX, $iconY, $iconSize, $iconSize)

# "Desk Widgets" title
$titleFont = New-Object System.Drawing.Font("Segoe UI Variable Display", 22, [System.Drawing.FontStyle]::Bold)
$titleBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(29, 29, 31))
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$g.DrawString("Desk Widgets", $titleFont, $titleBrush, [float]($w / 2), 390.0, $sf)
$titleFont.Dispose()
$titleBrush.Dispose()

# Version subtitle
$subFont = New-Object System.Drawing.Font("Segoe UI Variable Text", 12, [System.Drawing.FontStyle]::Regular)
$subBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(110, 110, 115))
$g.DrawString("Version 1.7.0", $subFont, $subBrush, [float]($w / 2), 428.0, $sf)
$subFont.Dispose()
$subBrush.Dispose()

# Tagline
$tagFont = New-Object System.Drawing.Font("Segoe UI Variable Text", 11, [System.Drawing.FontStyle]::Regular)
$tagBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(134, 134, 139))

$g.DrawString("Lightweight desktop widgets", $tagFont, $tagBrush, [float]($w / 2), 456.0, $sf)
$tagFont.Dispose()
$tagBrush.Dispose()
$sf.Dispose()

# Subtle horizontal line
$linePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(210, 210, 215), 1)
$g.DrawLine($linePen, 60, 500, $w - 60, 500)
$linePen.Dispose()

$g.Dispose()
$wizardPath = Join-Path $outDir "WizardImage.bmp"
$bmp.Save($wizardPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$bmp.Dispose()
Write-Host "  WizardImage.bmp (${w}x${h})"

# --- Small header image (55x58, 24-bit BMP) ---
$sw = 55; $sh = 58
$small = New-Object System.Drawing.Bitmap($sw, $sh, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$g = [System.Drawing.Graphics]::FromImage($small)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.Clear([System.Drawing.Color]::FromArgb(245, 245, 247))

$padX = [int](($sw - 48) / 2)
$padY = [int](($sh - 48) / 2)
$g.DrawImage($appIcon, $padX, $padY, 48, 48)
$g.Dispose()
$smallPath = Join-Path $outDir "WizardSmallImage.bmp"
$small.Save($smallPath, [System.Drawing.Imaging.ImageFormat]::Bmp)
$small.Dispose()
Write-Host "  WizardSmallImage.bmp (${sw}x${sh})"

# --- Also regenerate MSIX logos ---
function Export-IconPng([string]$outPath, [int]$width, [int]$height) {
    $out = New-Object System.Drawing.Bitmap($width, $height)
    $gx = [System.Drawing.Graphics]::FromImage($out)
    $gx.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gx.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $gx.Clear([System.Drawing.Color]::Transparent)
    $gx.DrawImage($appIcon, 0, 0, $width, $height)
    $gx.Dispose()
    $out.Save($outPath, [System.Drawing.Imaging.ImageFormat]::Png)
    $out.Dispose()
}

Export-IconPng (Join-Path $outDir "Square44x44Logo.png")   44  44
Export-IconPng (Join-Path $outDir "Square150x150Logo.png") 150 150
Export-IconPng (Join-Path $outDir "StoreLogo.png")         50  50

$wide = New-Object System.Drawing.Bitmap(310, 150)
$g = [System.Drawing.Graphics]::FromImage($wide)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.Clear([System.Drawing.Color]::Transparent)
$g.DrawImage($appIcon, 91, 11, 128, 128)
$g.Dispose()
$wide.Save((Join-Path $outDir "Wide310x150Logo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$wide.Dispose()
Write-Host "  MSIX logos regenerated"

$appIcon.Dispose()
Write-Host "Done." -ForegroundColor Green
