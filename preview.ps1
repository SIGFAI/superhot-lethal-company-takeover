# Plays the demo like the machine does (mod + demo flag) and keeps the game running for screenshots.
$k = 'C:/mod/work/b34_1156/kits/superhot'
$b = & "$k/build.ps1" -Dir "C:/mod/work/b34_1156"; if ($LASTEXITCODE -ne 0) { $b; exit 1 }
& "$k/play.ps1" -Mod 'C:/mod/work/b34_1156/release' -Demo
$log = 'C:/mod/work/superhot/game/BepInEx/LogOutput.log'
for ($i = 0; $i -lt 120; $i++) { Start-Sleep 1; if ((Test-Path $log) -and (Select-String -Path $log -Pattern 'SIGF_READY' -Quiet)) { break } }
Start-Sleep 3
Set-Content 'C:/mod/work/superhot-rec.txt' '1' -NoNewline -Encoding ascii
Write-Output 'demo started'
