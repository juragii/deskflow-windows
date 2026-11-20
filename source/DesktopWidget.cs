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
 public sealed partial class DesktopWidget {
  public Window Window {get;private set;} public readonly DesktopHost Host=new DesktopHost();
  Store store;Action save,hidden;bool suppressNative,dragging,closed,layoutQueued;DesktopHost.Point dragPoint;DesktopHost.Rect dragRect;DispatcherTimer monitor;
  T Get<T>(string n) where T:class{return Window.FindName(n) as T;}
  static Brush B(string s){return (Brush)new BrushConverter().ConvertFromString(s);}
  string Ink {get{return store.Data.WidgetDark?"#F2F4F6":"#333D4B";}}
  public DesktopWidget(Window main,Store state,Action open,Action onHidden,Action<int> month,Action<DateTime> select,Action today,Action onSave,Action sync,bool test=false){
   store=state;save=onSave;hidden=onHidden;suppressNative=test;Host.RecoveryRequested+=()=>RecoverPresentation(true);
   using(var s=Assembly.GetExecutingAssembly().GetManifestResourceStream("DesktopWidget.xaml"))Window=(Window)XamlReader.Load(s);
   Window.Resources=main.Resources;Window.Icon=main.Icon;
   Window.Width=Math.Min(Math.Max(Window.MinWidth,store.Data.WidgetWidth),Math.Max(320,SystemParameters.WorkArea.Width-32));Window.Height=Math.Min(Math.Max(Window.MinHeight,store.Data.WidgetHeight),Math.Max(400,SystemParameters.WorkArea.Height-32));
   Window.Left=Math.Max(SystemParameters.WorkArea.Left,SystemParameters.WorkArea.Right-Window.Width-24);Window.Top=SystemParameters.WorkArea.Top+32;
   Get<Button>("WidgetFit").Click+=(s,e)=>FitToContent();InitLayout();
   var grip=Get<Thumb>("WidgetResize");grip.DragDelta+=(s,e)=>{store.Data.WidgetAutoFit=false;ResizeBy(e.HorizontalChange,e.VerticalChange);};grip.DragCompleted+=(s,e)=>SaveSize();
   Get<Button>("WidgetOpen").Click+=(s,e)=>open();Get<Button>("WidgetHide").Click+=(s,e)=>{Hide();store.Data.DesktopEnabled=false;save();hidden();};
   Get<Button>("WidgetLock").Click+=(s,e)=>{store.Data.WidgetLocked=!store.Data.WidgetLocked;Appearance();save();};
   Get<Button>("WidgetPrev").Click+=(s,e)=>month(-1);Get<Button>("WidgetNext").Click+=(s,e)=>month(1);Get<Button>("WidgetToday").Click+=(s,e)=>today();
   var header=Get<Grid>("WidgetDrag");header.MouseLeftButtonDown+=(s,e)=>{if(store.Data.WidgetLocked||FindButton(e.OriginalSource as DependencyObject))return;DesktopHost.GetCursorPos(out dragPoint);dragRect=Host.Bounds();if(!Host.Attached){var h=new WindowInteropHelper(Window).Handle;DesktopHost.GetWindowRect(h,out dragRect);}dragging=true;header.CaptureMouse();e.Handled=true;};
   header.MouseMove+=(s,e)=>{if(!dragging||e.LeftButton!=MouseButtonState.Pressed)return;DesktopHost.Point p;DesktopHost.GetCursorPos(out p);Host.MoveScreen(dragRect.Left+p.X-dragPoint.X,dragRect.Top+p.Y-dragPoint.Y);};
   header.MouseLeftButtonUp+=(s,e)=>{if(!dragging)return;dragging=false;header.ReleaseMouseCapture();SavePosition();};header.LostMouseCapture+=(s,e)=>{dragging=false;};
   Window.Loaded+=(s,e)=>{if(!suppressNative){Attach();RestorePosition();}Appearance();};
   Window.SizeChanged+=(s,e)=>{if(layoutQueued||closed)return;layoutQueued=true;Window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{layoutQueued=false;if(!closed)ResponsiveLayout();}));};
   Window.Closed+=(s,e)=>{Host.Detach();closed=true;if(monitor!=null)monitor.Stop();};
   monitor=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};monitor.Tick+=(s,e)=>{if(!closed&&!suppressNative&&store.Data.DesktopEnabled){if(!Host.Attached)Attach();else Host.Maintain();}};monitor.Start();
   Get<Button>("WidgetRefresh").Click+=(s,e)=>sync();Select=select;Appearance();
  }
  public void ResizeBy(double dx,double dy){var area=SystemParameters.WorkArea;Window.Width=Math.Max(Window.MinWidth,Math.Min(area.Width-16,Window.Width+dx));Window.Height=Math.Max(Window.MinHeight,Math.Min(area.Height-16,Window.Height+dy));}
  void ClampOnScreen(){if(!Host.Attached)return;var r=Host.Bounds();var area=System.Windows.Forms.Screen.FromRectangle(new System.Drawing.Rectangle(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top)).WorkingArea;int x=Math.Max(area.Left,Math.Min(r.Left,area.Right-(r.Right-r.Left))),y=Math.Max(area.Top,Math.Min(r.Top,area.Bottom-(r.Bottom-r.Top)));Host.MoveScreen(x,y);store.Data.WidgetX=x;store.Data.WidgetY=y;}
  public void SaveSize(){ClampOnScreen();store.Data.WidgetWidth=Window.Width;store.Data.WidgetHeight=Window.Height;save();}
  public void FitToContent(){store.Data.WidgetAutoFit=true;ApplyFit();SaveSize();if(Host.Attached)RestorePosition();}
  void ApplyFit(){if(!store.Data.WidgetAutoFit)return;Window.Width=Math.Max(Window.MinWidth,Math.Min(900,SystemParameters.WorkArea.Width-48));Window.Height=Math.Max(Window.MinHeight,Math.Min(Get<Grid>("WidgetContent").Height+80,SystemParameters.WorkArea.Height-48));ClampOnScreen();}
  void ResponsiveLayout(){var view=Get<Viewbox>("WidgetFitView");var content=Get<Grid>("WidgetContent");content.Width=Math.Max(860,view.ActualWidth);if(!store.Data.WidgetAutoFit)content.Height=Math.Max(Layout.CalendarHeight,Math.Max(view.ActualHeight,Get<StackPanel>("WidgetAgenda").DesiredSize.Height+(store.Data.WidgetShowTasks?Get<StackPanel>("WidgetTaskSection").DesiredSize.Height:0)+60));}
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
  double textScale=1;
  TextBlock Txt(string text,int size,string color=null){return new TextBlock{Text=text,FontSize=size*textScale,Foreground=B(color??Ink),TextWrapping=TextWrapping.Wrap,VerticalAlignment=VerticalAlignment.Center};}
  public void Render(DateTime month,DateTime selected){
   if(closed)return;lastMonth=month;lastSelected=selected;ApplyLayout();Appearance();textScale=Layout.CalendarText;Get<TextBlock>("WidgetMonth").Text=month.ToString("yyyy년 M월");Get<TextBlock>("WidgetDay").Text=selected.ToString("M월 d일 dddd",CultureInfo.GetCultureInfo("ko-KR"));
   var days=Get<UniformGrid>("WidgetDays");days.Children.Clear();DateTime first=new DateTime(month.Year,month.Month,1);DateTime start=first.AddDays(-(int)first.DayOfWeek);
   for(int i=0;i<42;i++){
    DateTime d=start.AddDays(i);bool active=d.Date==selected.Date;var entries=store.CalendarEvents.Where(x=>GoogleCalendar.OnDay(x,d)).OrderBy(x=>!x.AllDay).ThenBy(GoogleCalendar.Start).ToList();
    var panel=new StackPanel{Margin=new Thickness(3,4,3,3)};var label=Txt(d.Day.ToString(),11,active?"#FFFFFF":d.Month!=month.Month?(store.Data.WidgetDark?"#6B7684":"#B0B8C1"):d.DayOfWeek==DayOfWeek.Sunday?"#F04452":d.DayOfWeek==DayOfWeek.Saturday?"#3182F6":Ink);label.HorizontalAlignment=HorizontalAlignment.Center;panel.Children.Add(label);
    foreach(var item in entries.Take(Layout.EventsPerDay)){var title=Txt(item.Title,10,active?"#FFFFFF":store.Data.WidgetDark?"#B6D5FF":"#1B64DA");title.FontSize=10*Layout.EventText;title.MaxHeight=Layout.EventLines*title.FontSize*1.5;title.TextTrimming=TextTrimming.CharacterEllipsis;panel.Children.Add(new Border{Background=B(active?"#20FFFFFF":store.Data.WidgetDark?"#303182F6":"#183182F6"),CornerRadius=new CornerRadius(3),Margin=new Thickness(0,3*Layout.Spacing,0,0),Padding=new Thickness(2,Layout.Spacing,2,Layout.Spacing),Child=title});}
    if(entries.Count>Layout.EventsPerDay)panel.Children.Add(Txt("+"+(entries.Count-Layout.EventsPerDay)+"개",9,active?"#FFFFFF":"#8B95A1"));
    string tooltip=d.ToString("M월 d일")+(entries.Count==0?" · 일정 없음":"\n"+string.Join("\n",entries.Select(x=>(x.AllDay?"종일":GoogleCalendar.Start(x).ToString("HH:mm"))+" · "+x.Title)));
    var button=new Button{ClipToBounds=true,Padding=new Thickness(0),Margin=new Thickness(2),HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalContentAlignment=VerticalAlignment.Top,Content=panel,Background=active?B("#3182F6"):d.Date==DateTime.Today?B("#203182F6"):Brushes.Transparent,ToolTip=tooltip};button.Click+=(s,e)=>Select(d);days.Children.Add(button);
   }
   textScale=Layout.AgendaText;var agenda=Get<StackPanel>("WidgetAgenda");agenda.Children.Clear();var events=store.CalendarEvents.Where(e=>GoogleCalendar.OnDay(e,selected)).OrderBy(e=>!e.AllDay).ThenBy(GoogleCalendar.Start).ToList();
   foreach(var e in events){var row=new Grid{Margin=new Thickness(0,0,0,10)};row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(52)});row.ColumnDefinitions.Add(new ColumnDefinition());row.Children.Add(Txt(e.AllDay?"종일":GoogleCalendar.Start(e).ToString("HH:mm"),11,"#3182F6"));var title=Txt(e.Title,12);Grid.SetColumn(title,1);row.Children.Add(title);agenda.Children.Add(row);}if(events.Count==0)agenda.Children.Add(Txt("등록된 일정이 없습니다.",11,"#8B95A1"));
   textScale=Layout.TaskText;var tasks=Get<StackPanel>("WidgetTasks");tasks.Children.Clear();var pending=store.Data.Tasks.Where(x=>!x.Done).OrderBy(x=>x.Due==""?"9999":x.Due).ThenBy(x=>x.DueTime).ToList();Get<TextBlock>("WidgetTaskHeading").Text="남은 할 일 · "+pending.Count;
   foreach(var t in pending){var content=new StackPanel();content.Children.Add(Txt(t.Title,12));if(t.Due!="")content.Children.Add(Txt("마감 "+t.Due.Substring(5).Replace("-","/")+(t.DueTime!=""?" "+t.DueTime:""),10,"#8B95A1"));var check=new CheckBox{Content=content,Margin=new Thickness(0,4,0,8),HorizontalContentAlignment=HorizontalAlignment.Stretch};check.Click+=(s,e)=>{t.Done=check.IsChecked==true;save();Select(selected);};tasks.Children.Add(check);}if(tasks.Children.Count==0)tasks.Children.Add(Txt("남은 할 일이 없습니다.",11,"#8B95A1"));
   ScaleItems();ResponsiveLayout();Get<Grid>("WidgetContent").Height=Layout.CalendarHeight;Window.UpdateLayout();double contentHeight=Get<StackPanel>("WidgetAgenda").DesiredSize.Height+(store.Data.WidgetShowTasks?Get<StackPanel>("WidgetTaskSection").DesiredSize.Height:0)+60;Get<Grid>("WidgetContent").Height=Math.Max(Layout.CalendarHeight,contentHeight);ApplyFit();
   Get<TextBlock>("WidgetSync").Text=store.Data.LastSync==""?"Google 연결은 본 화면의 설정에서 진행하세요.":"업데이트 "+store.Data.LastSync+" · Google 캘린더";
  }
  public void SyncStatus(string text,bool busy){Get<TextBlock>("WidgetSync").Text=text;Get<TextBlock>("WidgetSync").ToolTip=text;Get<Button>("WidgetRefresh").IsEnabled=!busy;}
  public void Hide(){Host.SetDisplayEnabled(false);Window.Hide();}
  public void Show(){if(!closed){Host.SetDisplayEnabled(true);if(Window.WindowState==WindowState.Minimized)Window.WindowState=WindowState.Normal;Window.Show();if(!suppressNative){Attach();RestorePosition();}}}
  public void Close(){if(closed)return;if(!suppressNative&&Host.Attached)SavePosition();Host.Detach();Window.Close();}
 }
}
