$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$references = @('System.dll','System.Core.dll','System.Net.Http.dll','System.Web.dll','System.Web.Extensions.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll','System.Security.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$arguments = @('/nologo','/target:winexe','/platform:anycpu','/optimize+','/utf8output',('/out:' + (Join-Path $PSScriptRoot 'DeskFlow.exe')),('/resource:' + (Join-Path $PSScriptRoot 'MainV2.xaml') + ',Main.xaml'))
foreach ($reference in $references) { $arguments += '/reference:' + (Join-Path $framework $reference) }
$arguments += Join-Path $PSScriptRoot 'AssemblyInfo.cs'
$arguments += '/win32manifest:' + (Join-Path $PSScriptRoot 'app.manifest')
$arguments += '/win32icon:' + (Join-Path $PSScriptRoot 'DeskFlow.ico')
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'Controls.xaml') + ',Controls.xaml'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'NanalLogo.png') + ',NanalLogo.png'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'DeskFlow.ico') + ',DeskFlow.ico'
$arguments += '/resource:' + (Join-Path $PSScriptRoot 'DesktopWidget.xaml') + ',DesktopWidget.xaml'
$arguments += Join-Path $PSScriptRoot 'WidgetIntegration.cs'
$arguments += Join-Path $PSScriptRoot 'ShowDesktopTests.cs'
$arguments += Join-Path $PSScriptRoot 'DesktopHostV3.cs'
$arguments += Join-Path $PSScriptRoot 'DesktopWidget.cs'
$arguments += Join-Path $PSScriptRoot 'TimePicker.cs'
$arguments += Join-Path $PSScriptRoot 'TaskReminders.cs'
$arguments += Join-Path $PSScriptRoot 'ReminderTests.cs'
$arguments += Join-Path $PSScriptRoot 'TelegramAlerts.cs'
$arguments += Join-Path $PSScriptRoot 'CalendarSync.cs'
$arguments += Join-Path $PSScriptRoot 'CalendarEditing.cs'
$arguments += Join-Path $PSScriptRoot 'EventEditor.cs'
$arguments += Join-Path $PSScriptRoot 'EditingTests.cs'
$arguments += Join-Path $PSScriptRoot 'RecurrenceTests.cs'
$arguments += Join-Path $PSScriptRoot 'PrivateUpdates.cs'
$arguments += Join-Path $PSScriptRoot 'PrivateManifest.cs'
$arguments += Join-Path $PSScriptRoot 'PrivateUpdateTests.cs'
$arguments += Join-Path $PSScriptRoot 'UpdateCore.cs'
$arguments += Join-Path $PSScriptRoot 'UpdateClient.cs'
$arguments += Join-Path $PSScriptRoot 'UpdateUI.cs'
$arguments += Join-Path $PSScriptRoot 'UpdateTests.cs'
$arguments += Join-Path $PSScriptRoot 'Core.cs'
$arguments += Join-Path $PSScriptRoot 'App.cs'
$arguments += Join-Path $PSScriptRoot 'OAuthLoopback.cs'
$arguments += Join-Path $PSScriptRoot 'LoopbackTests.cs'
& (Join-Path $framework 'csc.exe') @arguments
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }


$helperArgs=@('/nologo','/target:winexe','/platform:anycpu','/optimize+',('/out:'+(Join-Path $PSScriptRoot 'DeskFlow.Updater.exe')),('/win32icon:'+(Join-Path $PSScriptRoot 'DeskFlow.ico')))
foreach($r in @('System.dll','System.Core.dll','System.Web.Extensions.dll','System.IO.Compression.dll','System.IO.Compression.FileSystem.dll')){$helperArgs+='/reference:'+(Join-Path $framework $r)}
$helperArgs+=Join-Path $PSScriptRoot 'UpdateCore.cs'
$helperArgs+=Join-Path $PSScriptRoot 'UpdaterProgram.cs'
& (Join-Path $framework 'csc.exe') @helperArgs
if($LASTEXITCODE -ne 0){throw 'Updater build failed'}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'DeskFlow.exe.config') -Destination (Join-Path $PSScriptRoot 'DeskFlow.Updater.exe.config') -Force
New-Item -ItemType Directory -Force -Path (Join-Path $PSScriptRoot 'verification\payload') | Out-Null
& (Join-Path $framework 'csc.exe') /nologo /target:winexe ('/out:'+(Join-Path $PSScriptRoot 'verification\payload\DeskFlow.exe')) (Join-Path $PSScriptRoot 'UpdateTestPayload.cs')
if($LASTEXITCODE -ne 0){throw 'Test payload build failed'}

& (Join-Path $framework 'csc.exe') /nologo /target:winexe /define:OLD_PAYLOAD ('/out:'+(Join-Path $PSScriptRoot 'verification\payload\DeskFlow.old.exe')) (Join-Path $PSScriptRoot 'UpdateTestPayload.cs')
if($LASTEXITCODE -ne 0){throw 'Old test payload build failed'}
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /define:BAD_PAYLOAD ('/out:'+(Join-Path $PSScriptRoot 'verification\payload\DeskFlow.bad.exe')) (Join-Path $PSScriptRoot 'UpdateTestPayload.cs')
if($LASTEXITCODE -ne 0){throw 'Bad test payload build failed'}
