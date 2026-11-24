Add-Type -AssemblyName System.Drawing
$before=Get-Content -LiteralPath 'Backups/SpatialVariety/before-tiles.json' -Raw|ConvertFrom-Json
$after=Get-Content -LiteralPath 'Backups/SpatialVariety/after-tiles.json' -Raw|ConvertFrom-Json
$bmp=[Drawing.Bitmap]::new(1840,800);$g=[Drawing.Graphics]::FromImage($bmp);$g.Clear([Drawing.Color]::FromArgb(17,23,32))
$title=[Drawing.Font]::new('Segoe UI',22,[Drawing.FontStyle]::Bold);$font=[Drawing.Font]::new('Segoe UI',13);$small=[Drawing.Font]::new('Segoe UI',11)
$g.DrawString('JustMe - comparacion de tiles reales',$title,[Drawing.Brushes]::White,24,12)
$g.DrawString('Misma semilla y escala por celda. Oscuro: terreno solido. Claro: espacio transitable. Sin esquema de rutas.',$small,[Drawing.Brushes]::LightSteelBlue,24,57)
$solid=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(48,42,59));$air=[Drawing.SolidBrush]::new([Drawing.Color]::FromArgb(220,215,200))
$scale=3.8
for($row=0;$row -lt 2;$row++){
 for($col=0;$col -lt 2;$col++){
  $m=if($col -eq 0){$before[$row]}else{$after[$row]};$x=24+$col*915;$y=105+$row*335
  $label=if($col -eq 0){'ANTES - receta y curva de salto repetidas'}else{'AHORA - camaras y frecuencia independientes'}
  $g.DrawString(($label+' | semilla '+$m.seed),$font,[Drawing.Brushes]::White,[float]$x,[float]$y)
  for($yy=0;$yy -lt $m.height;$yy++){
   $line=$m.rows[$yy]
   for($xx=0;$xx -lt $m.width;$xx++){$brush=if($line[$xx] -eq '1'){$solid}else{$air};$g.FillRectangle($brush,[float]($x+$xx*$scale),[float]($y+35+$yy*$scale),[float]$scale,[float]$scale)}
  }
  $g.DrawString(('{0} x {1} celdas' -f $m.width,$m.height),$small,[Drawing.Brushes]::LightSteelBlue,[float]$x,[float]($y+35+[Math]::Max($before[$row].height,$after[$row].height)*$scale+12))
 }
}
$g.DrawString('Comparacion del generador puro; la comprobacion fisica y la vista real de Unity se registran por separado.',$small,[Drawing.Brushes]::LightSteelBlue,24,770)
$bmp.Save((Join-Path $PWD 'Backups/SpatialVariety/tile-comparison.png'),[Drawing.Imaging.ImageFormat]::Png)
$g.Dispose();$bmp.Dispose();$solid.Dispose();$air.Dispose();$title.Dispose();$font.Dispose();$small.Dispose()
