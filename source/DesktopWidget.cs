using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace DeskFlow {
 public sealed class DesktopWidget {
  public Window Window {get;private set;} public readonly DesktopHost Host=new DesktopHost();
  Store store;Action save,hidden;bool suppressNative,dragging,closed;DesktopHost.Point dragPoint;DesktopHost.Rect dragRect;DispatcherTimer monitor;
  T Get<T>(string n) where T:class{return Window.FindName(n) as T;}
  static Brush B(string s){return (Brush)new BrushConverter().ConvertFromString(s);}
  string Ink {get{return store.Data.WidgetDark?"#F2F4F6":"#333D4B";}}
  public DesktopWidget(Window main,Store state,Action open,Action onHidden,Action<int> month,Action<DateTime> select,Action today,Action onSave,Action sync,bool test=false){
   store=state;save=onSave;hidden=onHidden;suppressNative=test;
   using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("DesktopWidget.xaml"))Window=(Window)XamlReader.Load(s);
   Window.Resources=main.Resources;Window.Icon=main.Icon;
   Window.Width=Math.Min(Window.Width,Math.Max(320,SystemParameters.WorkArea.Width-32));Window.Height=Math.Min(Window.Height,Math.Max(400,SystemParameters.WorkArea.Height-32));
   Window.Left=Math.Max(SystemParameters.WorkArea.Left,SystemParameters.WorkArea.Right-Window.Width-24);Window.Top=SystemParameters.WorkArea.Top+32;
   Get<Button>("WidgetOpen").Click+=(s,e)=>open();Get<Button>("WidgetHide").Click+=(s,e)=>{Hide();store.Data.DesktopEnabled=false;save();hidden();};
   Get<Button>("WidgetLock").Click+=(s,e)=>{store.Data.WidgetLocked=!store.Data.WidgetLocked;Appearance();save();};
   Get<Button>("WidgetPrev").Click+=(s,e)=>month(-1);Get<Button>("WidgetNext").Click+=(s,e)=>month(1);Get<Button>("WidgetToday").Click+=(s,e)=>today();
   var header=Get<Grid>("WidgetDrag");header.MouseLeftButtonDown+=(s,e)=>{if(store.Data.WidgetLocked||FindButton(e.OriginalSource as DependencyObject))return;DesktopHost.GetCursorPos(out dragPoint);dragRect=Host.Bounds();if(!Host.Attached){var h=new WindowInteropHelper(Window).Handle;DesktopHost.GetWindowRect(h,out dragRect);}dragging=true;header.CaptureMouse();e.Handled=true;};
   header.MouseMove+=(s,e)=>{if(!dragging||e.LeftButton!=MouseButtonState.Pressed)return;DesktopHost.Point p;DesktopHost.GetCursorPos(out p);Host.MoveScreen(dragRect.Left+p.X-dragPoint.X,dragRect.Top+p.Y-dragPoint.Y);};
   header.MouseLeftButtonUp+=(s,e)=>{if(!dragging)return;dragging=false;header.ReleaseMouseCapture();SavePosition();};header.LostMouseCapture+=(s,e)=>{dragging=false;};
   Window.Loaded+=(s,e)=>{if(!suppressNative){Attach();RestorePosition();}Appearance();};
   Window.Closed+=(s,e)=>{Host.Detach();closed=true;if(monitor!=null)monitor.Stop();};
   monitor=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};monitor.Tick+=(s,e)=>{if(!closed&&!suppressNative&&store.Data.DesktopEnabled){if(!Host.Attached)Attach();else Host.Maintain();}};monitor.Start();
   Get<Button>("WidgetRefresh").Click+=(s,e)=>sync();Select=select;Appearance();
  }
  Action<DateTime> Select;
  bool FindButton(DependencyObject d){while(d!=null){if(d is Button)return true;if(d is FrameworkContentElement)d=((FrameworkContentElement)d).Parent;else d=VisualTreeHelper.GetParent(d);}return false;}
  public void Attach(){if(suppressNative)return;Host.Attach(Window,store.Data.WidgetAlwaysOnTop);Appearance();}
  public void ResetPosition(){Window.Show();{Host.Attach(Window,store.Data.WidgetAlwaysOnTop);var source=PresentationSource.FromVisual(Window);double scale=source==null?1:source.CompositionTarget.TransformToDevice.M11;int x=(int)((SystemParameters.WorkArea.Right-Window.Width-24)*scale),y=(int)((SystemParameters.WorkArea.Top+32)*scale);Host.MoveScreen(x,y);SavePosition();Host.Maintain(true);}}
  void RestorePosition(){if(store.Data.WidgetX==int.MinValue)return;int x=store.Data.WidgetX,y=store.Data.WidgetY;var bounds=Host.Bounds();int width=Math.Max(1,bounds.Right-bounds.Left),height=Math.Max(1,bounds.Bottom-bounds.Top);var rect=new System.Drawing.Rectangle(x,y,width,height);if(!System.Windows.Forms.Screen.AllScreens.Any(screen=>screen.WorkingArea.IntersectsWith(rect)))return;var area=System.Windows.Forms.Screen.FromRectangle(rect).WorkingArea;x=Math.Max(area.Left,Math.Min(x,area.Right-width));y=Math.Max(area.Top,Math.Min(y,area.Bottom-height));Host.MoveScreen(x,y);}
  void SavePosition(){var r=Host.Bounds();store.Data.WidgetX=r.Left;store.Data.WidgetY=r.Top;save();}
  public void Appearance(){
   byte alpha=(byte)Math.Round(Math.Max(.15,Math.Min(1,store.Data.WidgetOpacity))*255);byte value=store.Data.WidgetDark?(byte)24:(byte)255;
   Get<Border>("WidgetSurface").Background=new SolidColorBrush(Color.FromArgb(alpha,value,value,value));Window.Foreground=B(Ink);
   var b=Get<Button>("WidgetLock");b.Content=store.Data.WidgetLocked?"잠금":"이동 중";b.Background=B(store.Data.WidgetLocked?"#193182F6":"#303182F6");b.Foreground=B("#3182F6");
   Get<Button>("WidgetOpen").Foreground=B(store.Data.WidgetDark?"#D1D6DB":"#6B7684");Get<Button>("WidgetHide").Foreground=Get<Button>("WidgetOpen").Foreground;
   Get<TextBlock>("WidgetState").Text=suppressNative?"위젯 화면 미리보기":Host.Attached?"바탕화면 고정 · "+(store.Data.WidgetLocked?"이동 잠금":"상단을 끌어 이동"):"바탕화면 연결 대기 · 설정에서 상태 확인";
   Get<StackPanel>("WidgetTaskSection").Visibility=store.Data.WidgetShowTasks?Visibility.Visible:Visibility.Collapsed;
  }
  TextBlock Txt(string text,int size,string color=null){return new TextBlock{Text=text,FontSize=size,Foreground=B(color??Ink),TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};}
  public void Render(DateTime month,DateTime selected){
   if(closed)return;Appearance();Get<TextBlock>("WidgetMonth").Text=month.ToString("yyyy년 M월");Get<TextBlock>("WidgetDay").Text=selected.ToString("M월 d일 dddd",CultureInfo.GetCultureInfo("ko-KR"));
   var days=Get<UniformGrid>("WidgetDays");days.Children.Clear();DateTime first=new DateTime(month.Year,month.Month,1);DateTime start=first.AddDays(-(int)first.DayOfWeek);
   for(int i=0;i<42;i++){
    DateTime d=start.AddDays(i);bool active=d.Date==selected.Date;var entries=store.CalendarEvents.Where(x=>GoogleCalendar.OnDay(x,d)).OrderBy(x=>!x.AllDay).ThenBy(GoogleCalendar.Start).ToList();
    var panel=new StackPanel{Margin=new Thickness(3,4,3,3)};var label=Txt(d.Day.ToString(),11,active?"#FFFFFF":d.Month!=month.Month?(store.Data.WidgetDark?"#6B7684":"#B0B8C1"):d.DayOfWeek==DayOfWeek.Sunday?"#F04452":d.DayOfWeek==DayOfWeek.Saturday?"#3182F6":Ink);label.HorizontalAlignment=HorizontalAlignment.Center;panel.Children.Add(label);
    foreach(var item in entries.Take(2)){var title=Txt(item.Title,10,active?"#FFFFFF":store.Data.WidgetDark?"#B6D5FF":"#1B64DA");title.MaxHeight=27;title.TextTrimming=TextTrimming.CharacterEllipsis;panel.Children.Add(new Border{Background=B(active?"#20FFFFFF":store.Data.WidgetDark?"#303182F6":"#183182F6"),CornerRadius=new CornerRadius(3),Margin=new Thickness(0,3,0,0),Padding=new Thickness(2,1,2,1),Child=title});}
    if(entries.Count>2)panel.Children.Add(Txt("+"+(entries.Count-2)+"개",9,active?"#FFFFFF":"#8B95A1"));
    string tooltip=d.ToString("M월 d일")+(entries.Count==0?" · 일정 없음":"\n"+string.Join("\n",entries.Select(x=>(x.AllDay?"종일":GoogleCalendar.Start(x).ToString("HH:mm"))+" · "+x.Title)));
    var button=new Button{MinHeight=98,Padding=new Thickness(0),Margin=new Thickness(2),HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Top,Content=panel,Background=active?B("#3182F6"):d.Date==DateTime.Today?B("#203182F6"):Brushes.Transparent,ToolTip=tooltip};button.Click+=(s,e)=>Select(d);days.Children.Add(button);
   }
   var agenda=Get<StackPanel>("WidgetAgenda");agenda.Children.Clear();var events=store.CalendarEvents.Where(e=>GoogleCalendar.OnDay(e,selected)).OrderBy(e=>!e.AllDay).ThenBy(GoogleCalendar.Start).ToList();
   foreach(var e in events.Take(3)){var row=new Grid{Margin=new Thickness(0,0,0,10)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(52)});row.ColumnDefinitions.Add(new ColumnDefinition());row.Children.Add(Txt(e.AllDay?"종일":GoogleCalendar.Start(e).ToString("HH:mm"),11,"#3182F6"));var title=Txt(e.Title,12);Grid.SetColumn(title,1);row.Children.Add(title);agenda.Children.Add(row);}if(events.Count==0)agenda.Children.Add(Txt("등록된 일정이 없습니다.",11,"#8B95A1"));if(events.Count>3)agenda.Children.Add(Txt("+ "+(events.Count-3)+"개 일정 · 본 화면에서 보기",10,"#8B95A1"));
   var tasks=Get<StackPanel>("WidgetTasks");tasks.Children.Clear();foreach(var t in store.Data.Tasks.Where(x=>!x.Done).OrderBy(x=>x.Due==""?"9999":x.Due).Take(3)){var check=new CheckBox{Content=Txt(t.Title,11),Margin=new Thickness(0,3,0,3)};check.Click+=(s,e)=>{t.Done=check.IsChecked==true;save();Select(selected);};tasks.Children.Add(check);}if(tasks.Children.Count==0)tasks.Children.Add(Txt("남은 할 일이 없습니다.",11,"#8B95A1"));
   Get<TextBlock>("WidgetSync").Text=store.Data.LastSync==""?"Google 연결은 본 화면의 설정에서 진행하세요.":"업데이트 "+store.Data.LastSync+" · Google 캘린더";
  }
  public void SyncStatus(string text,bool busy){Get<TextBlock>("WidgetSync").Text=text;Get<TextBlock>("WidgetSync").ToolTip=text;Get<Button>("WidgetRefresh").IsEnabled=!busy;}
  public void Hide(){Host.SetDisplayEnabled(false);Window.Hide();}
  public void Show(){if(!closed){Host.SetDisplayEnabled(true);if(Window.WindowState==WindowState.Minimized)Window.WindowState=WindowState.Normal;Window.Show();if(!suppressNative){Attach();RestorePosition();}}}
  public void Close(){if(closed)return;if(!suppressNative&&Host.Attached)SavePosition();Host.Detach();Window.Close();}
 }
}
