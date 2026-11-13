using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
namespace DeskFlow {
 public static class ShowDesktopTests {
  [DllImport("user32.dll")]static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int width,int height,uint flags);
  static void Check(bool value,string text){if(!value)throw new Exception("Show Desktop guard: "+text);}
  public static void Run(DesktopWidget widget,string root){
   var w=widget.Window;var h=new WindowInteropHelper(w).Handle;Check(widget.Host.Attach(w),"attach");
   DesktopHost.SendMessage(h,0x112,new IntPtr(0xF020),IntPtr.Zero);Check(!DesktopHost.IsIconic(h),"system minimize command intercepted");
   SetWindowPos(h,IntPtr.Zero,0,0,0,0,0x0001|0x0002|0x0004|0x0010|0x0080);Check(DesktopHost.IsWindowVisible(h),"external hide intercepted");
   widget.Hide();Check(!DesktopHost.IsWindowVisible(h),"intentional hide still allowed");widget.Host.Maintain();Check(!DesktopHost.IsWindowVisible(h),"maintenance respects intentional hide");
   widget.Show();widget.Host.Attach(w);widget.Host.Maintain();Check(DesktopHost.IsWindowVisible(h),"explicit show re-enables protection");
   bool peek=widget.Host.PeekExclusionApplied;widget.Host.Detach();w.Hide();Check(!DesktopHost.IsWindowVisible(h),"detached window can hide normally");w.Show();
   File.WriteAllText(Path.Combine(root,"show-desktop-result.txt"),"PASS: native system-minimize command blocked, external hide flag blocked, explicit widget hide preserved, maintenance does not undo user hide, explicit show restores guard, hooks detach correctly. DWM Peek exclusion accepted: "+peek+". Actual Explorer Show Desktop button click was not automated in this isolated environment.");
  }
 }
}
