# End-to-end check of dist\BirthdayBuddy.exe on the real desktop: popups, focus, click-to-acknowledge,
# retract, heads-up toast, single instance. Run in Windows PowerShell 5.1 after ./publish.ps1:
#     powershell -ExecutionPolicy Bypass -File tools/e2e.ps1
# It seeds a temporary %AppData%\BirthdayBuddy and deletes it afterwards, so it refuses to run when that
# folder already exists (your real birthday list) or when Birthday Buddy is running.
# It moves the mouse and types, so don't touch the PC for about 25 s. Results and screenshots go to $Out.
param(
    [string]$Exe = (Join-Path $PSScriptRoot '..\dist\BirthdayBuddy.exe'),
    [string]$Out = (Join-Path $PSScriptRoot '..\TestResults\e2e'))
$ErrorActionPreference = 'Stop'
if (-not (Test-Path $Exe)) { throw "Missing $Exe. Run ./publish.ps1 first." }
New-Item -ItemType Directory -Force $Out | Out-Null
Add-Type -AssemblyName System.Windows.Forms, System.Drawing, UIAutomationClient, UIAutomationTypes
Add-Type -Namespace E2E -Name Win -MemberDefinition @'
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern void mouse_event(uint f, uint dx, uint dy, uint d, IntPtr e);
public delegate bool EnumProc(IntPtr h, IntPtr l);
[DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc p, IntPtr l);
[DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
[DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
[StructLayout(LayoutKind.Sequential)] public struct RECT { public int L, T, R, B; }
[DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
[DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
[DllImport("user32.dll")] public static extern IntPtr SetThreadDpiAwarenessContext(IntPtr c);
'@

# Physical pixels everywhere (the app is per-monitor DPI aware; Windows PowerShell is not).
[void][E2E.Win]::SetThreadDpiAwarenessContext([IntPtr]-4)
$results = [System.Collections.Generic.List[string]]::new()
function Check([string]$name, [bool]$ok, [string]$detail = '') {
    $results.Add(('{0}  {1}{2}' -f ($(if ($ok) { 'PASS' } else { 'FAIL' })), $name, $(if ($detail) { " - $detail" } else { '' })))
}
function Pump([int]$ms) { $end = (Get-Date).AddMilliseconds($ms); while ((Get-Date) -lt $end) { [System.Windows.Forms.Application]::DoEvents(); Start-Sleep -Milliseconds 20 } }
function AppWindows([int]$procId) {
    $list = [System.Collections.Generic.List[object]]::new()
    $cb = [E2E.Win+EnumProc] {
        param($h, $l)
        $p = 0; [void][E2E.Win]::GetWindowThreadProcessId($h, [ref]$p)
        if ($p -eq $procId -and [E2E.Win]::IsWindowVisible($h)) {
            $sb = New-Object System.Text.StringBuilder 256; [void][E2E.Win]::GetWindowText($h, $sb, 256)
            $r = New-Object E2E.Win+RECT; [void][E2E.Win]::GetWindowRect($h, [ref]$r)
            $list.Add([pscustomobject]@{ Handle = $h; Title = $sb.ToString(); L = $r.L; T = $r.T; R = $r.R; B = $r.B; ExStyle = [E2E.Win]::GetWindowLong($h, -20) })
        }
        $true
    }
    [void][E2E.Win]::EnumWindows($cb, [IntPtr]::Zero)
    $list
}
function Shot([string]$name) {
    $area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    $w = [Math]::Min(900, $area.Width); $h = [Math]::Min(800, $area.Height)
    $bmp = New-Object System.Drawing.Bitmap $w, $h
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen($area.Right - $w, $area.Bottom - $h, 0, 0, (New-Object System.Drawing.Size $w, $h))
    $bmp.Save((Join-Path $Out "$name.png")); $g.Dispose(); $bmp.Dispose()
}
function Click([int]$x, [int]$y) { [void][E2E.Win]::SetCursorPos($x, $y); Start-Sleep -Milliseconds 150; [E2E.Win]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 60; [E2E.Win]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero) }
function Data { Get-Content $json -Raw | ConvertFrom-Json }

$dataDir = Join-Path $env:APPDATA 'BirthdayBuddy'
$json = Join-Path $dataDir 'birthdays.json'
if (Test-Path $dataDir) { throw "Refusing to run: $dataDir already exists." }
if (Get-Process BirthdayBuddy -ErrorAction SilentlyContinue) { throw 'Refusing to run: Birthday Buddy is already running.' }
$runKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$runBefore = (Get-ItemProperty $runKey -ErrorAction SilentlyContinue).BirthdayBuddy

$today = Get-Date; $soon = $today.AddDays(2); $todayIso = $today.ToString('yyyy-MM-dd')
New-Item -ItemType Directory $dataDir | Out-Null
$seed = @{
    version = 1
    settings = @{ headsUpDays = 3; startWithWindows = $false; idleMinutes = 5; fanfareVolume = 0.2 }
    people = @(
        @{ id = [guid]::NewGuid().ToString(); name = 'Sam'; day = $today.Day; month = $today.Month; year = 1992 },
        @{ id = [guid]::NewGuid().ToString(); name = 'Alex'; day = $today.Day; month = $today.Month; year = 1986 },
        @{ id = [guid]::NewGuid().ToString(); name = 'Kim'; day = $soon.Day; month = $soon.Month }
    )
}
$seed | ConvertTo-Json -Depth 5 | Set-Content $json -Encoding UTF8

$cursorBefore = [System.Windows.Forms.Cursor]::Position
$app = $null
try {
    # A typing target that holds the foreground while the popup appears.
    $form = New-Object System.Windows.Forms.Form -Property @{ Text = 'E2E typing target'; Width = 500; Height = 160; StartPosition = 'CenterScreen'; TopMost = $false }
    $box = New-Object System.Windows.Forms.TextBox -Property @{ Dock = 'Fill'; Multiline = $true }
    $form.Controls.Add($box); $form.Show(); Pump 300
    $form.Activate(); [void][E2E.Win]::SetForegroundWindow($form.Handle); $box.Focus() | Out-Null; Pump 300
    $targetIsForeground = [E2E.Win]::GetForegroundWindow() -eq $form.Handle
    $results.Add("INFO  typing target is foreground before launch: $targetIsForeground")

    $app = Start-Process $Exe -ArgumentList '--tray' -PassThru
    $t0 = Get-Date
    Pump 700
    Check 'no popup before 1 s' (@(AppWindows $app.Id | Where-Object Title -like "It's*").Count -eq 0)
    # Wait (up to 6 s; the single-file exe needs a moment to start) until both popups have finished sliding in.
    $area = [System.Windows.Forms.Screen]::PrimaryScreen.WorkingArea
    $deadline = (Get-Date).AddSeconds(6)
    do {
        Pump 100
        $popups = @(AppWindows $app.Id | Where-Object Title -like "It's*" | Sort-Object B -Descending)
    } until (($popups.Count -eq 2 -and @($popups | Where-Object { $_.R -gt $area.Right }).Count -eq 0) -or (Get-Date) -gt $deadline)
    $results.Add(('INFO  popups settled {0:N1} s after launch (includes app start-up)' -f ((Get-Date) - $t0).TotalSeconds))
    Pump 400
    Check 'two popups after 1 s' ($popups.Count -eq 2) (($popups | ForEach-Object { "$($_.Title) [$($_.L),$($_.T),$($_.R),$($_.B)]" }) -join '; ')
    Check 'bottom popup is Alex (name order)' ($popups.Count -ge 1 -and $popups[0].Title -eq "It's Alex's birthday!")
    Check 'stack sits bottom-right above the taskbar' ($popups.Count -ge 1 -and $popups[0].R -le $area.Right -and $popups[0].B -le $area.Bottom -and ($area.Right - $popups[0].R) -lt 60 -and ($area.Bottom - $popups[0].B) -lt 60) "working area $area"
    Check 'popups are no-activate tool windows' (@($popups | Where-Object { ($_.ExStyle -band 0x08000088) -ne 0x08000088 }).Count -eq 0)
    if ($targetIsForeground) {
        Check 'foreground unchanged after popups appeared' ([E2E.Win]::GetForegroundWindow() -eq $form.Handle)
        [System.Windows.Forms.SendKeys]::SendWait('typing while the popup is up'); Pump 300
        Check 'typing still reaches the active window' ($box.Text -eq 'typing while the popup is up') "box: '$($box.Text)'"
    }
    Shot '1-popups'

    # Hover to hold the stack, then click Alex's button with real mouse input.
    $alex = [System.Windows.Automation.AutomationElement]::FromHandle($popups[0].Handle)
    $cond = New-Object System.Windows.Automation.PropertyCondition ([System.Windows.Automation.AutomationElement]::NameProperty), 'Okay I got it!'
    $button = $alex.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $cond)
    Check 'Alex popup has the Okay I got it! button' ($null -ne $button)
    $r = $button.Current.BoundingRectangle
    [void][E2E.Win]::SetCursorPos([int]($popups[0].L + 20), [int]($popups[0].T + 20)); Pump 500
    Click ([int]($r.X + $r.Width / 2)) ([int]($r.Y + $r.Height / 2)); Pump 900
    $alexAck = (Data).people | Where-Object name -eq 'Alex' | Select-Object -ExpandProperty lastBirthdayAck -ErrorAction SilentlyContinue
    Check 'click saved Alex''s acknowledgement' ($alexAck -eq $todayIso) "lastBirthdayAck=$alexAck"
    $samAck = (Data).people | Where-Object name -eq 'Sam' | Select-Object -ExpandProperty lastBirthdayAck -ErrorAction SilentlyContinue
    Check 'Sam is not acknowledged' (-not $samAck)
    if ($targetIsForeground) { Check 'clicking the button did not take focus' ([E2E.Win]::GetForegroundWindow() -eq $form.Handle) }
    $left = @(AppWindows $app.Id | Where-Object Title -like "It's*")
    Check 'only Sam''s popup remains' ($left.Count -eq 1 -and $left[0].Title -eq "It's Sam's birthday!") (($left | ForEach-Object Title) -join '; ')
    Check 'Sam moved down into the bottom slot' ($left.Count -eq 1 -and [Math]::Abs($left[0].B - $popups[0].B) -le 2) "Sam bottom $($left[0].B), slot bottom $($popups[0].B)"
    Shot '2-after-ack'

    # Move away: the stack retracts 4 s later.
    [void][E2E.Win]::SetCursorPos($cursorBefore.X, $cursorBefore.Y); Pump 5200
    Check 'stack retracted 4 s after the pointer left' (@(AppWindows $app.Id | Where-Object Title -like "It's*").Count -eq 0)

    # Heads-up toast for Kim at 15 s after a --tray launch.
    while (((Get-Date) - $t0).TotalSeconds -lt 17) { Pump 250 }
    $kimHeadsUp = (Data).people | Where-Object name -eq 'Kim' | Select-Object -ExpandProperty lastHeadsUp -ErrorAction SilentlyContinue
    Check 'heads-up for Kim sent after the 15 s login delay' ($kimHeadsUp -eq $soon.ToString('yyyy-MM-dd')) "lastHeadsUp=$kimHeadsUp"
    $notice = (Data).people | Where-Object { $_.lastBirthdayNotice }
    Check 'no day-of toast marker written' (@($notice).Count -eq 0)
    Shot '3-heads-up'

    # Second launch: single instance shows the main window of the running app.
    $second = Start-Process $Exe -PassThru; Pump 2500
    Check 'second launch exits' ($second.HasExited)
    $main = @(AppWindows $app.Id | Where-Object Title -eq 'Birthday Buddy')
    Check 'running app shows its main window' ($main.Count -eq 1)
    Shot '4-main-window'
}
finally {
    [void][E2E.Win]::SetCursorPos($cursorBefore.X, $cursorBefore.Y)
    if ($app -and -not $app.HasExited) { Stop-Process -Id $app.Id -Force; Start-Sleep -Milliseconds 500 }
    if ($form) { $form.Close() }
    $runAfter = (Get-ItemProperty $runKey -ErrorAction SilentlyContinue).BirthdayBuddy
    Check 'startup registry entry unchanged' ($runAfter -eq $runBefore) "before='$runBefore' after='$runAfter'"
    Copy-Item $json (Join-Path $Out 'birthdays-after.json') -ErrorAction SilentlyContinue
    Copy-Item (Join-Path $dataDir 'log.txt') (Join-Path $Out 'log.txt') -ErrorAction SilentlyContinue
    Remove-Item $dataDir -Recurse -Force -ErrorAction SilentlyContinue
    Check 'test data removed' (-not (Test-Path $dataDir))
    $results | Set-Content (Join-Path $Out 'e2e-results.txt')
    $results
}
