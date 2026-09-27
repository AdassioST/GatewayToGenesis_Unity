param([string]$OutDir, [string]$PreviewDir, [int]$Seed = 7)
# Tooltip sprites in the style of Visual/UI/Tabs/DecisionTab.png: half decaying (weathered stone, the Storage Tab's
# worn plate texture), half elegant (thin copper lines with bright end ticks). Regenerate with:
#   powershell -File make_tooltip_sprites.ps1 -OutDir ..  [-PreviewDir <dir>]
# Outputs (all point-filtered pixel art at 16 px/unit; import settings live in the .meta files):
#   TooltipBackground.png  158x298 seamless tile mirrored from the Storage Tab texture (tiled behind the text)
#   TooltipFrame.png       24x24, border 8, stone rim + copper inset line, transparent centre (tiled edges)
#   TooltipDivider.png     24x9, border 6 left/right, stone bar with copper end ticks
#   TooltipRing.png        12x12 white ring (tinted in Unity), TooltipRingSocket.png 12x12 stone disc
#   TooltipShadow.png      16x16 soft shadow, border 6
Add-Type -AssemblyName System.Drawing
$rng = New-Object System.Random $Seed

function C([string]$hex, [int]$a = 255) {
    $h = $hex.TrimStart('#')
    [System.Drawing.Color]::FromArgb($a, [Convert]::ToInt32($h.Substring(0,2),16), [Convert]::ToInt32($h.Substring(2,2),16), [Convert]::ToInt32($h.Substring(4,2),16))
}
function Mix($a, $b, [double]$t) {
    $t = [Math]::Max(0, [Math]::Min(1, $t))
    [System.Drawing.Color]::FromArgb([int]($a.A + ($b.A - $a.A) * $t), [int]($a.R + ($b.R - $a.R) * $t), [int]($a.G + ($b.G - $a.G) * $t), [int]($a.B + ($b.B - $a.B) * $t))
}
$clear = [System.Drawing.Color]::FromArgb(0,0,0,0)
function Blank($w, $h) { $b = New-Object System.Drawing.Bitmap $w, $h; for ($y = 0; $y -lt $h; $y++) { for ($x = 0; $x -lt $w; $x++) { $b.SetPixel($x, $y, $clear) } }; $b }
function Save($bmp, $name) { $bmp.Save((Join-Path $OutDir $name), [System.Drawing.Imaging.ImageFormat]::Png) }

# Tileable value noise on an n x n lattice over a size x size torus.
function Noise($size, $cells) {
  $grid = New-Object 'double[,]' $cells, $cells
  for ($j = 0; $j -lt $cells; $j++) { for ($i = 0; $i -lt $cells; $i++) { $grid[$i, $j] = $rng.NextDouble() } }
  $out = New-Object 'double[,]' $size, $size
  $step = $size / $cells
  for ($y = 0; $y -lt $size; $y++) { for ($x = 0; $x -lt $size; $x++) {
    $gx = $x / $step; $gy = $y / $step
    $x0 = [int][Math]::Floor($gx) % $cells; $y0 = [int][Math]::Floor($gy) % $cells
    $x1 = ($x0 + 1) % $cells; $y1 = ($y0 + 1) % $cells
    $fx = $gx - [Math]::Floor($gx); $fy = $gy - [Math]::Floor($gy)
    $fx = $fx * $fx * (3 - 2 * $fx); $fy = $fy * $fy * (3 - 2 * $fy)
    $a = $grid[$x0, $y0] + ($grid[$x1, $y0] - $grid[$x0, $y0]) * $fx
    $b = $grid[$x0, $y1] + ($grid[$x1, $y1] - $grid[$x0, $y1]) * $fx
    $out[$x, $y] = $a + ($b - $a) * $fy
  } }
  ,$out
}

# ---- Background tile: the Storage Tab's own texture (Visual/UI/Tabs/StorageTab.png), so tooltips match the tabs.
# Its interior (inside the tan border line) is mirrored both ways into a seamless tile that can repeat.
$storage = [System.Drawing.Bitmap]::FromFile((Join-Path $PSScriptRoot '..\..\Tabs\StorageTab.png'))
$cropX = 5; $cropY = 5; $cropW = 79; $cropH = 149   # x 5..83, y 5..153: the border line and its shade are outside
$bgTile = New-Object System.Drawing.Bitmap (2 * $cropW), (2 * $cropH)
for ($y = 0; $y -lt $cropH; $y++) { for ($x = 0; $x -lt $cropW; $x++) {
  $c = $storage.GetPixel($cropX + $x, $cropY + $y)
  $bgTile.SetPixel($x, $y, $c)
  $bgTile.SetPixel(2 * $cropW - 1 - $x, $y, $c)
  $bgTile.SetPixel($x, 2 * $cropH - 1 - $y, $c)
  $bgTile.SetPixel(2 * $cropW - 1 - $x, 2 * $cropH - 1 - $y, $c)
} }
$storage.Dispose()
Save $bgTile 'TooltipBackground.png'

# ---- Frame: 24x24, border 8. Outline, 3 px weathered stone, gap, copper line (chamfered corners), shade ----
$N = 24; $B = 8
$outline = C '161110'; $stoneHi = C '8C7A67'; $stone = C '74645A'; $stoneMid = C '6A5B4F'; $stoneLo = C '574A40'; $crack = C '3E332C'
$gap = C '1F1917'; $copperHi = C 'FED4AA'; $copper = C 'E6BC93'; $copperShade = C '8A6D52'; $copperDeep = C '5E4938'
$stoneNoise = Noise $N 6; $stoneGrain = Noise $N 12
$frame = Blank $N $N
for ($y = 0; $y -lt $N; $y++) { for ($x = 0; $x -lt $N; $x++) {
  $dx = [Math]::Min($x, $N - 1 - $x); $dy = [Math]::Min($y, $N - 1 - $y); $d = [Math]::Min($dx, $dy)
  $top = $y -lt $N / 2; $left = $x -lt $N / 2
  $c = $clear
  if ($d -eq 0) { if (-not ($dx -le 1 -and $dy -le 1)) { $c = $outline } }
  elseif ($d -le 3) {
    if ($dx -eq 1 -and $dy -eq 1) { $c = $outline }
    else {
      $v = $stoneNoise[$x, $y] * 0.7 + $stoneGrain[$x, $y] * 0.3
      $c = if ($v -gt 0.66) { $stoneHi } elseif ($v -gt 0.48) { $stone } elseif ($v -gt 0.3) { $stoneMid } else { $stoneLo }
      if ($d -eq 1 -and $top -and $dy -eq 1) { $c = Mix $c $stoneHi 0.6 }       # light from above on the rim's top
      if ($d -eq 3) { $c = Mix $c $crack 0.45 }                                    # rim's inner edge in shadow
      if ($stoneGrain[$x, $y] -lt 0.14) { $c = $crack }                             # cracks and chips
    }
  }
  elseif ($d -eq 4) { $c = $gap }
  elseif ($d -eq 5) {
    # Copper line, chamfered at the corners: the corner pixel steps inward.
    if ($dx -eq 5 -and $dy -eq 5) { $c = $gap }
    else { $c = if ($top -and $dy -eq 5) { $copperHi } elseif ($left -and $dx -eq 5) { $copperHi } else { $copper } }
  }
  elseif ($d -eq 6) {
    if ($dx -eq 6 -and $dy -eq 6) { $c = $copper }                                   # chamfer: the corner joins one pixel in
    else { $c = $copperShade }
  }
  elseif ($d -eq 7) { $c = Mix $copperDeep $clear 0.35 }
  $frame.SetPixel($x, $y, $c)
} }
Save $frame 'TooltipFrame.png'

# ---- Divider: 24x9, sliced 6 left/right. Bevelled stone bar with tall copper ticks at both ends ----
$W = 24; $H = 9
$div = Blank $W $H
$barRows = @{ 2 = (C '3B2F29'); 3 = (C '7D6C5C'); 4 = (C '796959'); 5 = (C '6E5F52'); 6 = (C '2A2422') }
$barNoise = Noise $W 6
for ($x = 4; $x -le ($W - 5); $x++) {
  foreach ($row in $barRows.Keys) {
    $c = $barRows[$row]
    $wear = 0.5 - $barNoise[$x, 0]
    if ($row -ge 3 -and $row -le 5 -and $wear -gt 0) { $c = Mix $c (C '574A40') ($wear * 0.8) }
    $div.SetPixel($x, $row, $c)
  }
}
foreach ($cx in @(2, ($W - 3))) {
  for ($y = 0; $y -lt $H; $y++) {
    $div.SetPixel($cx, $y, $(if ($y -eq 0 -or $y -eq $H - 1) { C 'B8997A' } else { C 'FED4AA' }))
    $div.SetPixel($cx + $(if ($cx -lt $W / 2) { 1 } else { -1 }), $y, $(if ($y -eq 0 -or $y -eq $H - 1) { C '5E4938' } else { C '8A6D52' }))
  }
}
Save $div 'TooltipDivider.png'

# ---- Lock ring: 12x12 white ring (tinted in Unity) ----
$ring = Blank 12 12
for ($y = 0; $y -lt 12; $y++) { for ($x = 0; $x -lt 12; $x++) {
  $d = [Math]::Sqrt([Math]::Pow($x - 5.5, 2) + [Math]::Pow($y - 5.5, 2))
  if ($d -ge 3.4 -and $d -le 5.6) { $ring.SetPixel($x, $y, (C 'FFFFFF')) }
} }
Save $ring 'TooltipRing.png'

# ---- Ring socket: 12x12 stone disc with an outline ----
$socket = Blank 12 12
for ($y = 0; $y -lt 12; $y++) { for ($x = 0; $x -lt 12; $x++) {
  $d = [Math]::Sqrt([Math]::Pow($x - 5.5, 2) + [Math]::Pow($y - 5.5, 2))
  if ($d -le 5.9) { $socket.SetPixel($x, $y, $outline) }
  if ($d -le 5.2) { $socket.SetPixel($x, $y, $(if ($y -lt 6) { C '6A5B4F' } else { C '4F443B' })) }
  if ($d -le 3.0) { $socket.SetPixel($x, $y, (C '2A2320')) }
} }
Save $socket 'TooltipRingSocket.png'

# ---- Shadow: 16x16 soft black, sliced 6 ----
$shadow = Blank 16 16
for ($y = 0; $y -lt 16; $y++) { for ($x = 0; $x -lt 16; $x++) {
  $d = [Math]::Min([Math]::Min($x, 15 - $x), [Math]::Min($y, 15 - $y))
  $shadow.SetPixel($x, $y, (C '000000' ([int]([Math]::Min(1.0, ($d + 1) / 6.0) * 140))))
} }
Save $shadow 'TooltipShadow.png'

if ($PreviewDir) {
  foreach ($pair in @(@($bgTile,'background'), @($frame,'frame'), @($div,'divider'), @($socket,'socket'))) {
    $src = $pair[0]; $s = 12
    $big = New-Object System.Drawing.Bitmap ($src.Width * $s), ($src.Height * $s)
    $g = [System.Drawing.Graphics]::FromImage($big)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
    $g.Clear((C '3A3A46'))
    $g.DrawImage($src, 0, 0, $src.Width * $s, $src.Height * $s)
    $big.Save((Join-Path $PreviewDir "preview_$($pair[1]).png"), [System.Drawing.Imaging.ImageFormat]::Png)
    $g.Dispose(); $big.Dispose()
  }
}
foreach ($b in @($bgTile, $frame, $div, $ring, $socket, $shadow)) { $b.Dispose() }
