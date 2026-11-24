Add-Type -AssemblyName System.Drawing
$maps=Get-Content Backups/SpatialVariety/preferred-tiles.json -Raw|ConvertFrom-Json
$bmp=[Drawing.Bitmap]::new(1800,1460);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::FromArgb(17,23,32))
$title=[Drawing.Font]::new('Segoe UI',21,[Drawing.FontStyle]::Bold);$font=[Drawing.Font]::new('Segoe UI',13);$small=[Drawing.Font]::new('Segoe UI',10)
$g.DrawString('JustMe - terreno generado: camaras y ramales intermedios',$title,[Drawing.Brushes]::White,22,12)
$g.DrawString('Tiles reales del generador. Claro: aire. Oscuro: terreno. Verde: inicio. Rojo: salida. Escala ajustada por panel.',$small,[Drawing.Brushes]::LightSteelBlue,22,52)
$solid=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(48,42,59));$air=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(220,215,200))
for($i=0;$i -lt $maps.Count;$i++){
 $m=$maps[$i];$col=$i%2;$row=[Math]::Floor($i/2);$ox=22+$col*890;$oy=82+$row*450
 $g.DrawString(('{0} / {1}, semilla {2} - {3} x {4} celdas' -f $m.layout,$m.profile,$m.seed,$m.width,$m.height),$font,[Drawing.Brushes]::White,[float]$ox,[float]$oy)
 $scale=[Math]::Min(845.0/$m.width,398.0/$m.height);$yy0=$oy+32
 for($yy=0;$yy -lt $m.height;$yy++){for($xx=0;$xx -lt $m.width;$xx++){$brush=if($m.rows[$yy][$xx] -eq '1'){$solid}else{$air};$g.FillRectangle($brush,[float]($ox+$xx*$scale),[float]($yy0+$yy*$scale),[float]($scale+.1),[float]($scale+.1))}}
 $g.FillEllipse([Drawing.Brushes]::Lime,[float]($ox+$m.spawnX*$scale-3),[float]($yy0+($m.height-1-$m.spawnY)*$scale-3),6,6)
 $g.FillEllipse([Drawing.Brushes]::OrangeRed,[float]($ox+$m.goalX*$scale-3),[float]($yy0+($m.height-1-$m.goalY)*$scale-3),6,6)
}
$bmp.Save((Join-Path $PWD 'Backups/SpatialVariety/preferred-terrain.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose();$solid.Dispose();$air.Dispose();$title.Dispose();$font.Dispose();$small.Dispose()

