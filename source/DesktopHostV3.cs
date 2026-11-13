using System;
using System.Text;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using System.Threading;

namespace DeskFlow {
 // Keep the layered WPF window top-level. Parenting it to wallpaper WorkerW
 // can put its surface behind the wallpaper or desktop icon host.
 public sealed class DesktopHost {
  public bool Attached {get;private set;}public bool AlwaysOnTop {get;private set;}public string Error {get;private set;}
  public IntPtr Host {get{return hwnd;}}IntPtr hwnd;long originalEx;bool initialized,displayEnabled,placing;
  HwndSource source;Dispatcher dispatcher;WinEventProc callback;GCHandle callbackRoot;IntPtr foregroundHook,minimizeHook,objectHook;int recoveryQueued;
  public bool DesktopPresentationActive {get;private set;}public bool PeekExclusionApplied {get;private set;}
  [StructLayout(LayoutKind.Sequential)]struct WindowPos {public IntPtr Hwnd,After;public int X,Y,Width,Height;public uint Flags;}
  delegate void WinEventProc(IntPtr hook,uint kind,IntPtr window,int objectId,int child,uint thread,uint time);
  [DllImport("user32.dll")]static extern IntPtr SetWinEventHook(uint first,uint last,IntPtr module,WinEventProc proc,uint process,uint thread,uint flags);
  [DllImport("user32.dll")]static extern bool UnhookWinEvent(IntPtr hook);
  [DllImport("user32.dll")]static extern IntPtr GetAncestor(IntPtr window,uint flags);
  [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr h,int attr,ref int value,int size);
  [DllImport("user32.dll")]internal static extern IntPtr SendMessage(IntPtr h,uint message,IntPtr w,IntPtr l);
  [DllImport("user32.dll")]internal static extern bool ShowWindowAsync(IntPtr h,int command);
  public struct Rect {public int Left,Top,Right,Bottom;}
  [StructLayout(LayoutKind.Sequential)]public struct Point {public int X,Y;}
  delegate bool EnumProc(IntPtr h,IntPtr p);
  [DllImport("user32.dll")]public static extern bool IsWindow(IntPtr h);
  [DllImport("user32.dll")]public static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")]public static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")]public static extern IntPtr GetParent(IntPtr h);
  [DllImport("user32.dll")]public static extern bool GetWindowRect(IntPtr h,out Rect r);
  [DllImport("user32.dll")]public static extern bool GetCursorPos(out Point p);
  [DllImport("user32.dll")]static extern bool EnumWindows(EnumProc p,IntPtr v);
  [DllImport("user32.dll")]static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetWindowTextLength(IntPtr h);
  [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetClassName(IntPtr h,StringBuilder b,int n);
  [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int height,uint flags);
  [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr h,int command);
  [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")]static extern IntPtr Get64(IntPtr h,int n);
  [DllImport("user32.dll",EntryPoint="GetWindowLongW")]static extern int Get32(IntPtr h,int n);
  [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")]static extern IntPtr Set64(IntPtr h,int n,IntPtr v);
  [DllImport("user32.dll",EntryPoint="SetWindowLongW")]static extern int Set32(IntPtr h,int n,int v);
  [DllImport("dwmapi.dll")]static extern int DwmGetWindowAttribute(IntPtr h,int attr,out int value,int size);
  static long Get(IntPtr h,int n){return IntPtr.Size==8?Get64(h,n).ToInt64():Get32(h,n);}
  static void Set(IntPtr h,int n,long v){if(IntPtr.Size==8)Set64(h,n,new IntPtr(v));else Set32(h,n,unchecked((int)v));}
  static string Class(IntPtr h){var b=new StringBuilder(256);GetClassName(h,b,256);return b.ToString();}
  static bool Shell(IntPtr h){string c=Class(h);return c=="Progman"||c=="WorkerW"||c=="SHELLDLL_DefView"||c=="Shell_TrayWnd"||c=="Shell_SecondaryTrayWnd";}
  static bool Desktop(IntPtr h){string c=Class(h);return c=="Progman"||c=="WorkerW"||c=="SHELLDLL_DefView";}
  static bool App(IntPtr h,IntPtr foreground){long ex=Get(h,-20);return h==foreground||(ex&0x40000)!=0||((ex&0x80)==0&&GetWindowTextLength(h)>0);}
  bool DesktopRaised(){IntPtr foreground=GetForegroundWindow();if(foreground!=IntPtr.Zero&&Desktop(GetAncestor(foreground,2)))return true;bool raised=false;EnumWindows((h,v)=>{if(h==hwnd||!IsWindowVisible(h)||IsIconic(h)||Cloaked(h))return true;if(Desktop(h)){raised=true;return false;}if(!Shell(h)&&App(h,foreground))return false;return true;},IntPtr.Zero);return raised;}
  static bool Cloaked(IntPtr h){try{int value;return DwmGetWindowAttribute(h,14,out value,4)==0&&value!=0;}catch{return false;}}
  public bool Attach(Window window,bool onTop=false){
   hwnd=new WindowInteropHelper(window).Handle;Error="";AlwaysOnTop=onTop;
   if(hwnd==IntPtr.Zero||!IsWindow(hwnd)){Error="위젯 창이 아직 준비되지 않았습니다.";Attached=false;return false;}
   if(!initialized){originalEx=Get(hwnd,-20);initialized=true;}
   Set(hwnd,-20,(originalEx|0x80L|0x08000000L)&~0x40000L); // TOOLWINDOW, NOACTIVATE
   Attached=GetParent(hwnd)==IntPtr.Zero;
   if(!Attached){Error="위젯이 독립 창으로 생성되지 않았습니다. 프로그램을 다시 실행해 주세요.";return false;}
   displayEnabled=true;InstallProtection(window);Maintain(true);return true;
  }
  public void SetDisplayEnabled(bool value){displayEnabled=value;}
  void InstallProtection(Window window){
   dispatcher=window.Dispatcher;if(source==null){source=HwndSource.FromHwnd(hwnd);if(source!=null)source.AddHook(WindowMessage);}
   if(callback==null){callback=ExternalEvent;callbackRoot=GCHandle.Alloc(callback);foregroundHook=SetWinEventHook(3,3,IntPtr.Zero,callback,0,0,0);minimizeHook=SetWinEventHook(0x16,0x17,IntPtr.Zero,callback,0,0,0);objectHook=SetWinEventHook(0x8002,0x800B,IntPtr.Zero,callback,0,0,0);}
   try{int enabled=1;PeekExclusionApplied=DwmSetWindowAttribute(hwnd,12,ref enabled,4)==0;}catch{PeekExclusionApplied=false;}
  }
  void QueueRecovery(){if(!displayEnabled||!Attached||dispatcher==null||dispatcher.HasShutdownStarted||Interlocked.Exchange(ref recoveryQueued,1)!=0)return;try{dispatcher.BeginInvoke(DispatcherPriority.Send,new Action(()=>{Interlocked.Exchange(ref recoveryQueued,0);if(displayEnabled&&Attached)Maintain();}));}catch{Interlocked.Exchange(ref recoveryQueued,0);}}
  void ExternalEvent(IntPtr hook,uint kind,IntPtr window,int objectId,int child,uint thread,uint time){if(!displayEnabled||!Attached||window==IntPtr.Zero||objectId!=0)return;if(kind==3||(window==hwnd&&(kind==0x16||kind==0x17||kind==0x8002||kind==0x8003))||(window!=hwnd&&Desktop(window)&&(kind==0x8002||kind==0x8003||kind==0x800B)))QueueRecovery();}
  IntPtr WindowMessage(IntPtr window,int message,IntPtr w,IntPtr l,ref bool handled){
   if(!displayEnabled||!Attached)return IntPtr.Zero;
   if(message==0x112&&(w.ToInt64()&0xFFF0)==0xF020){handled=true;return IntPtr.Zero;}
   if(message==0x46&&l!=IntPtr.Zero&&!placing){var pos=(WindowPos)Marshal.PtrToStructure(l,typeof(WindowPos));if((pos.Flags&0x80)!=0){pos.Flags=(pos.Flags&~0x80u)|0x40u;Marshal.StructureToPtr(pos,l,false);QueueRecovery();}}
   if(message==0x5&&w.ToInt64()==1)QueueRecovery();return IntPtr.Zero;
  }
  public void Maintain(bool force=false){
   if(!Attached||!displayEnabled||!IsWindow(hwnd)||placing)return;placing=true;try{
   // Explorer's Show Desktop may minimize top-level windows. Restore without
   // taking focus; hiding the widget explicitly disables the owning timer.
   if(IsIconic(hwnd)||!IsWindowVisible(hwnd))ShowWindow(hwnd,4);
   DesktopPresentationActive=DesktopRaised();if(AlwaysOnTop||DesktopPresentationActive){SetWindowPos(hwnd,new IntPtr(-1),0,0,0,0,0x0001|0x0002|0x0010|0x0040);return;}
   if((Get(hwnd,-20)&8)!=0)SetWindowPos(hwnd,new IntPtr(-2),0,0,0,0,0x0001|0x0002|0x0010); // leave topmost group
   IntPtr lowest=IntPtr.Zero,foreground=GetForegroundWindow();
   EnumWindows((h,v)=>{long ex=Get(h,-20);if(h!=hwnd&&App(h,foreground)&&IsWindowVisible(h)&&!IsIconic(h)&&!Shell(h)&&!Cloaked(h)&&((ex&8)==0)){Rect r;if(GetWindowRect(h,out r)&&r.Right-r.Left>60&&r.Bottom-r.Top>40)lowest=h;}return true;},IntPtr.Zero);
   // Insert below all normal app windows, but above Explorer's desktop. HWND_BOTTOM
   // would also put the widget below the wallpaper, so deliberately avoid it.
   SetWindowPos(hwnd,lowest,0,0,0,0,0x0001|0x0002|0x0010|0x0040);
   }finally{placing=false;}
  }
  public void MoveScreen(int x,int y){if(hwnd!=IntPtr.Zero)SetWindowPos(hwnd,IntPtr.Zero,x,y,0,0,0x0001|0x0004|0x0010);}
  public Rect Bounds(){Rect r;GetWindowRect(hwnd,out r);return r;}
  public void Detach(){displayEnabled=false;Attached=false;if(foregroundHook!=IntPtr.Zero)UnhookWinEvent(foregroundHook);if(minimizeHook!=IntPtr.Zero)UnhookWinEvent(minimizeHook);if(objectHook!=IntPtr.Zero)UnhookWinEvent(objectHook);foregroundHook=minimizeHook=objectHook=IntPtr.Zero;if(source!=null){try{source.RemoveHook(WindowMessage);}catch(ObjectDisposedException){}source=null;}if(callbackRoot.IsAllocated)callbackRoot.Free();callback=null;if(initialized&&IsWindow(hwnd)){try{int off=0;DwmSetWindowAttribute(hwnd,12,ref off,4);}catch{}Set(hwnd,-20,originalEx);SetWindowPos(hwnd,new IntPtr(-2),0,0,0,0,0x0001|0x0002|0x0010);}}
 }
}
