Add-Type -AssemblyName System.Drawing
$maps=Get-Content -LiteralPath 'Backups/WorldTopology/map-preview.json' -Raw|ConvertFrom-Json
$bmp=[Drawing.Bitmap]::new(1500,1100)
$g=[Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode=[Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.Clear([Drawing.ColorTranslator]::FromHtml('#101722'))
$title=[Drawing.Font]::new('Segoe UI',24,[Drawing.FontStyle]::Bold)
$font=[Drawing.Font]::new('Segoe UI',17)
$small=[Drawing.Font]::new('Segoe UI',11)
$white=[Drawing.Brushes]::White;$gray=[Drawing.Brushes]::LightSteelBlue
$g.DrawString('JustMe | Recorridos generados',$title,$white,30,15)
$g.DrawString('Rutas reales del generador. Escala independiente por panel; no son un HUD del juego.',$small,$gray,30,62)
$labels=@{Horizontal='Horizontal';Ascent='Ascenso';Descent='Descenso';U='U';Switchback='S con cambios de sentido';Cross='Cruce con brazos de ida y vuelta'}
$colors=@('#adc1d7','#adc1d7','#ffb45e','#ffb45e','#bf8cff','#55baff','#adc1d7')
for($i=0;$i -lt $maps.Count;$i++){
 $m=$maps[$i];$x=20+($i%3)*495;$y=105+[Math]::Floor($i/3)*470;$w=480;$h=450
 $bg=[Drawing.SolidBrush]::new([Drawing.ColorTranslator]::FromHtml('#192535'));$g.FillRectangle($bg,[float]$x,[float]$y,[float]$w,[float]$h);$bg.Dispose()
 $g.DrawString($labels[$m.name],$font,$white,[float]($x+16),[float]($y+12))
 $xs=@($m.nodes|ForEach-Object {$_.FloorX*.5});$ys=@($m.nodes|ForEach-Object {$_.FloorY*.5})
 $minX=($xs|Measure-Object -Minimum).Minimum;$maxX=($xs|Measure-Object -Maximum).Maximum
 $minY=($ys|Measure-Object -Minimum).Minimum;$maxY=($ys|Measure-Object -Maximum).Maximum
 $spanX=$maxX-$minX;$spanY=$maxY-$minY
 $scale=[Math]::Min(($w-65)/[Math]::Max($spanX,1),($h-105)/[Math]::Max($spanY,1))
 $point={param($index) $n=$m.nodes[$index];[Drawing.PointF]::new([float]($x+32+($n.FloorX*.5-$minX)*$scale),[float]($y+$h-45-($n.FloorY*.5-$minY)*$scale))}
 foreach($link in $m.links){$a=&$point $link.FromNode;$b=&$point $link.ToNode;$pen=[Drawing.Pen]::new([Drawing.ColorTranslator]::FromHtml($colors[[int]$link.Action]),4);$g.DrawLine($pen,$a,$b);$pen.Dispose()}
 foreach($idx in $m.pages){$p=&$point $idx;$g.FillEllipse([Drawing.Brushes]::Gold,$p.X-5,$p.Y-5,10,10)}
 $p=&$point 0;$g.FillEllipse([Drawing.Brushes]::LightGreen,$p.X-6,$p.Y-6,12,12)
 $p=&$point $m.links[-1].ToNode;$g.FillEllipse([Drawing.Brushes]::Salmon,$p.X-6,$p.Y-6,12,12)
 $g.DrawString(('Extension de ruta: {0:0} x {1:0} unidades' -f $spanX,$spanY),$small,$gray,[float]($x+16),[float]($y+$h-29))
}
$g.DrawString('Azul: escalada | Naranja: salto | Morado: arrastre | Amarillo: trozos | Verde: inicio | Rojo: Libro',$small,$gray,30,1065)
$bmp.Save((Join-Path $PWD 'Backups/WorldTopology/layout-preview.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose();$title.Dispose();$font.Dispose();$small.Dispose()
