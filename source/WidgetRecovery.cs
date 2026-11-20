using System;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
namespace DeskFlow {
 partial class Widget {
  DispatcherTimer desktopRecoveryTimer;PowerModeChangedEventHandler desktopPower;SessionSwitchEventHandler desktopSession;EventHandler desktopDisplay;bool desktopRecoveryClosed;int desktopRecoveryAttempts=6;
  void InitDesktopRecovery(){
   desktopRecoveryTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(5)};desktopRecoveryTimer.Tick+=(s,e)=>{
    if(desktopRecoveryClosed||!store.Data.DesktopEnabled)return;
    if(desktop==null)UpdateDesktop();
    if(desktop!=null){desktop.RecoverPresentation(desktopRecoveryAttempts>0);if(desktopRecoveryAttempts>0)desktopRecoveryAttempts--;UpdateDesktopStatus();}
   };desktopRecoveryTimer.Start();
   if(suppressNative)return;
   desktopPower=(s,e)=>{if(e.Mode==PowerModes.Resume)QueueDesktopRecovery();};
   desktopSession=(s,e)=>{if(e.Reason==SessionSwitchReason.SessionUnlock||e.Reason==SessionSwitchReason.SessionLogon||e.Reason==SessionSwitchReason.ConsoleConnect||e.Reason==SessionSwitchReason.RemoteConnect)QueueDesktopRecovery();};
   desktopDisplay=(s,e)=>QueueDesktopRecovery();SystemEvents.PowerModeChanged+=desktopPower;SystemEvents.SessionSwitch+=desktopSession;SystemEvents.DisplaySettingsChanged+=desktopDisplay;
  }
  void QueueDesktopRecovery(){if(desktopRecoveryClosed||Window.Dispatcher.HasShutdownStarted)return;Window.Dispatcher.BeginInvoke(new Action(()=>{if(desktopRecoveryClosed)return;desktopRecoveryAttempts=6;if(store.Data.DesktopEnabled){if(desktop==null)UpdateDesktop();if(desktop!=null)desktop.RecoverPresentation(true);}}));}
  void StopDesktopRecovery(){desktopRecoveryClosed=true;if(desktopRecoveryTimer!=null)desktopRecoveryTimer.Stop();if(desktopPower!=null)SystemEvents.PowerModeChanged-=desktopPower;if(desktopSession!=null)SystemEvents.SessionSwitch-=desktopSession;if(desktopDisplay!=null)SystemEvents.DisplaySettingsChanged-=desktopDisplay;}
 }
 public sealed partial class DesktopWidget {
  public void RecoverPresentation(bool force=false){
   if(closed||!store.Data.DesktopEnabled)return;
   if(Window.WindowState==WindowState.Minimized)Window.WindowState=WindowState.Normal;
   if(!Window.IsVisible)Window.Show();
   if(!suppressNative){if(force||!Host.Attached||!DesktopHost.IsWindow(Host.Host)){if(force)Host.Detach();Host.SetDisplayEnabled(true);Attach();RestorePosition();ClampOnScreen();}Host.Maintain(force);}
   if(force){Window.InvalidateVisual();Get<System.Windows.Controls.Grid>("WidgetContent").InvalidateVisual();Window.UpdateLayout();if(!suppressNative)Host.Repaint();}
  }
 }
}
