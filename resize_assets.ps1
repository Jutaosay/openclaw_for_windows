Add-Type -AssemblyName System.Drawing

$sourcePath = "C:\Users\Zen\.gemini\antigravity\brain\f0bb5d54-09e0-4164-8bc6-0176c19dc5f6\openclaw_icon_v3_1774239639441.png"
$outDir = "c:\Users\Zen\Repo\Codings\Claw_winui3\src\OpenClaw\Assets"

if (-not (Test-Path $sourcePath)) {
    Write-Host "Source image not found: $sourcePath"
    exit 1
}

function Resize-Image {
    param($width, $height, $outFile, $fitMode = "Uniform")
    
    $sourceImg = [System.Drawing.Image]::FromFile($sourcePath)
    $destBmp = New-Object System.Drawing.Bitmap($width, $height)
    
    $g = [System.Drawing.Graphics]::FromImage($destBmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    
    $g.Clear([System.Drawing.Color]::Transparent)
    
    # Calculate uniform scale
    $ratioX = $width / $sourceImg.Width
    $ratioY = $height / $sourceImg.Height
    $ratio = if ($ratioX -lt $ratioY) { $ratioX } else { $ratioY }
    
    # Add a small margin depending on size (15% padding)
    if ($width -gt 50) {
        $ratio = $ratio * 0.85
    }
    
    $newW = [int]($sourceImg.Width * $ratio)
    $newH = [int]($sourceImg.Height * $ratio)
    
    $posX = [int](($width - $newW) / 2)
    $posY = [int](($height - $newH) / 2)
    
    $g.DrawImage($sourceImg, $posX, $posY, $newW, $newH)
    $g.Dispose()
    
    $destBmp.Save($outFile, [System.Drawing.Imaging.ImageFormat]::Png)
    $destBmp.Dispose()
    $sourceImg.Dispose()
    Write-Host "Created $outFile"
}

function Create-Ico {
    param($width, $height, $outFile)
    
    $sourceImg = [System.Drawing.Image]::FromFile($sourcePath)
    $destBmp = New-Object System.Drawing.Bitmap($width, $height)
    
    $g = [System.Drawing.Graphics]::FromImage($destBmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    
    $g.Clear([System.Drawing.Color]::Transparent)
    
    $ratioX = $width / $sourceImg.Width
    $ratioY = $height / $sourceImg.Height
    $ratio = if ($ratioX -lt $ratioY) { $ratioX } else { $ratioY }
    
    $ratio = $ratio * 0.9 # slight margin for ico
    
    $newW = [int]($sourceImg.Width * $ratio)
    $newH = [int]($sourceImg.Height * $ratio)
    $posX = [int](($width - $newW) / 2)
    $posY = [int](($height - $newH) / 2)
    
    $g.DrawImage($sourceImg, $posX, $posY, $newW, $newH)
    $g.Dispose()
    
    # Save as ICO (PNG embedding method)
    $fs = New-Object System.IO.FileStream($outFile, [System.IO.FileMode]::Create)
    $bw = New-Object System.IO.BinaryWriter($fs)
    $bw.Write([int16]0)   # reserved
    $bw.Write([int16]1)   # type (1=ico)
    $bw.Write([int16]1)   # count
    
    $w = $width
    $h = $height
    if ($w -eq 256) { $w = 0 }
    if ($h -eq 256) { $h = 0 }
    
    $bw.Write([byte]$w)
    $bw.Write([byte]$h)
    $bw.Write([byte]0)    # color count
    $bw.Write([byte]0)    # reserved
    $bw.Write([int16]1)   # planes
    $bw.Write([int16]32)  # bpp
    
    $ms = New-Object System.IO.MemoryStream
    $destBmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngBytes = $ms.ToArray()
    $ms.Dispose()
    
    $bw.Write([int]$pngBytes.Length)
    $bw.Write([int]22)    # offset
    
    $bw.Write($pngBytes)
    $bw.Close()
    $fs.Dispose()
    $destBmp.Dispose()
    $sourceImg.Dispose()
    Write-Host "Created $outFile"
}

Resize-Image 24 24 "$outDir\LockScreenLogo.png"
Resize-Image 620 300 "$outDir\SplashScreen.png"
Resize-Image 150 150 "$outDir\Square150x150Logo.png"
Resize-Image 44 44 "$outDir\Square44x44Logo.png"
Resize-Image 24 24 "$outDir\Square44x44Logo.targetsize-24_altform-unplated.png"
Resize-Image 50 50 "$outDir\StoreLogo.png"
Resize-Image 310 150 "$outDir\Wide310x150Logo.png"

Create-Ico 256 256 "$outDir\WindowIcon.ico"

Write-Host "All assets generated."
