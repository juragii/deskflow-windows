using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
namespace DeskFlow {
 static class WidgetLayoutTests {
  static void Check(bool ok,string message){if(!ok)throw new Exception("Widget customization: "+message);}
  static void Image(Window window,string path){window.UpdateLayout();var image=new RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32);image.Render(window);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var file=File.Create(path))encoder.Save(file);}
  public static void Run(DesktopWidget widget,Store store,string root){
   var w=widget.Window;widget.SetCalendarShare(.62);w.UpdateLayout();var content=(Grid)w.FindName("WidgetContent");double left=content.ColumnDefinitions[0].ActualWidth;
   widget.SetCalendarShare(.75);w.UpdateLayout();Check(content.ColumnDefinitions[0].ActualWidth>left,"calendar width changes");
   content.ColumnDefinitions[0].Width=new GridLength(.6,GridUnitType.Star);content.ColumnDefinitions[2].Width=new GridLength(.4,GridUnitType.Star);w.UpdateLayout();((GridSplitter)w.FindName("WidgetColumnSplitter")).RaiseEvent(new DragCompletedEventArgs(0,0,false){RoutedEvent=Thumb.DragCompletedEvent});Check(Math.Abs(new Store(store.Dir).Data.WidgetLayout.CalendarShare-.6)<.01,"splitter completion saves ratio");
   store.Data.WidgetLayout.TaskText=1.3;store.Data.WidgetLayout.MonthText=30;widget.Render(DateTime.Today,DateTime.Today);Check(((TextBlock)w.FindName("WidgetMonth")).FontSize==30,"independent title font applied");
   Exception failure=null;w.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>{var editor=Application.Current.Windows.Cast<Window>().FirstOrDefault(x=>x.Title=="위젯 꾸미기");try{Check(editor!=null,"customizer opens");var panel=(StackPanel)((ScrollViewer)((DockPanel)editor.Content).Children[2]).Content;var sliders=panel.Children.OfType<Slider>().ToList();Check(sliders.Count==22,"all 22 size controls present");sliders[3].Value=65;sliders[5].Value=31;Check(Math.Abs(store.Data.WidgetLayout.CalendarShare-.65)<.001&&((TextBlock)w.FindName("WidgetMonth")).FontSize==31,"sliders update widget immediately");Image(editor,Path.Combine(root,"widget-customize-preview.png"));}catch(Exception e){failure=e;}finally{if(editor!=null)editor.Close();}}));
   ((Button)w.FindName("WidgetCustomize")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(failure!=null)throw failure;
   var loaded=new Store(store.Dir).Data.WidgetLayout;Check(loaded.MonthText==31&&Math.Abs(loaded.CalendarShare-.65)<.001,"customization persists across reload");
   widget.Hide();Check(!w.IsVisible,"hidden fixture");store.Data.DesktopEnabled=true;widget.RecoverPresentation(true);Check(w.IsVisible&&w.WindowState==WindowState.Normal,"enabled widget recovers without app restart");
   widget.Hide();store.Data.DesktopEnabled=false;widget.RecoverPresentation(true);Check(!w.IsVisible,"recovery respects user-disabled widget");store.Data.DesktopEnabled=true;widget.Show();
   widget.Host.Attach(w);widget.Host.Detach();Check(widget.Host.Attach(w),"native display protection reattaches");widget.Host.Repaint();Check(DesktopHost.IsWindowVisible(widget.Host.Host),"native recovered surface visible");
   int resumeSignals=0;Action resumed=()=>resumeSignals++;widget.Host.RecoveryRequested+=resumed;DesktopHost.SendMessage(widget.Host.Host,0x218,new IntPtr(18),IntPtr.Zero);var frame=new DispatcherFrame();w.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(()=>frame.Continue=false));Dispatcher.PushFrame(frame);widget.Host.RecoveryRequested-=resumed;Check(resumeSignals==1,"native automatic-resume message queues recovery");widget.Host.Detach();
   Image(w,Path.Combine(root,"widget-custom-layout-preview.png"));
   store.Data.WidgetLayout=new WidgetLayout();widget.Render(DateTime.Today,DateTime.Today);store.Save();
   File.WriteAllText(Path.Combine(root,"widget-layout-result.txt"),"PASS: resized calendar columns, splitter save handler, 22 controls, live font/ratio changes, persisted settings, hidden-window recovery without app restart, user-disabled widget remains hidden, native detach/reattach and repaint. Actual laptop restart/sleep and Explorer restart were not performed.");
  }
 }
}
