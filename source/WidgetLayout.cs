using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
namespace DeskFlow {
 public sealed class WidgetLayout {
  public double CalendarShare=.8,CalendarHeight=640,CalendarText=1,EventText=1,AgendaText=1,TaskText=1;
  public double MonthText=23,HeadingText=13,WeekText=10,ToolbarText=10,FooterText=9;
  public double Padding=16,Spacing=1,MonthHeight=50,WeekHeight=26,TaskGap=20,CheckSize=1;
  public int EventsPerDay=2,EventLines=2;
  static double C(double v,double min,double max,double fallback){return double.IsNaN(v)||double.IsInfinity(v)?fallback:Math.Max(min,Math.Min(max,v));}
  public void Normalize(){CalendarShare=C(CalendarShare,.35,.85,.8);CalendarHeight=C(CalendarHeight,360,1400,640);CalendarText=C(CalendarText,.7,2,1);EventText=C(EventText,.7,2,1);AgendaText=C(AgendaText,.7,2,1);TaskText=C(TaskText,.7,2,1);MonthText=C(MonthText,14,40,23);HeadingText=C(HeadingText,10,28,13);WeekText=C(WeekText,8,22,10);ToolbarText=C(ToolbarText,8,18,10);FooterText=C(FooterText,8,18,9);Padding=C(Padding,4,40,16);Spacing=C(Spacing,.5,2.5,1);MonthHeight=C(MonthHeight,40,100,50);WeekHeight=C(WeekHeight,18,55,26);TaskGap=C(TaskGap,0,120,20);CheckSize=C(CheckSize,.75,1.75,1);EventsPerDay=Math.Max(1,Math.Min(10,EventsPerDay));EventLines=Math.Max(1,Math.Min(6,EventLines));}
 }
 public sealed partial class DesktopWidget {
  DateTime lastMonth=DateTime.Today,lastSelected=DateTime.Today;bool applyingLayout;DispatcherTimer layoutSave;
  WidgetLayout Layout {get{if(store.Data.WidgetLayout==null)store.Data.WidgetLayout=new WidgetLayout();return store.Data.WidgetLayout;}}
  void InitLayout(){
   Layout.Normalize();Get<Button>("WidgetCustomize").Click+=(s,e)=>Customize();
   var splitter=Get<GridSplitter>("WidgetColumnSplitter");splitter.DragCompleted+=(s,e)=>SaveSplit();splitter.KeyUp+=(s,e)=>SaveSplit();
   layoutSave=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};layoutSave.Tick+=(s,e)=>{layoutSave.Stop();save();};
   Window.Closed+=(s,e)=>{if(layoutSave.IsEnabled){layoutSave.Stop();save();}};ApplyLayout();
  }
  void SaveSplit(){if(applyingLayout)return;var left=Get<ColumnDefinition>("WidgetCalendarColumn");var right=Get<ColumnDefinition>("WidgetSideColumn");double sum=left.ActualWidth+right.ActualWidth;if(sum<=0)return;Layout.CalendarShare=Math.Max(.35,Math.Min(.85,left.ActualWidth/sum));ApplyLayout();Render(lastMonth,lastSelected);save();}
  public void SetCalendarShare(double share){Layout.CalendarShare=share;Layout.Normalize();Render(lastMonth,lastSelected);save();}
  public void ApplyLayout(){
   if(closed)return;applyingLayout=true;try{Layout.Normalize();var content=Get<Grid>("WidgetContent");content.ColumnDefinitions[0].Width=new GridLength(Layout.CalendarShare,GridUnitType.Star);content.ColumnDefinitions[2].Width=new GridLength(1-Layout.CalendarShare,GridUnitType.Star);
   Get<Border>("WidgetSurface").Padding=new Thickness(Layout.Padding,Layout.Padding*.75,Layout.Padding,Layout.Padding*.75);
   Get<Border>("WidgetSidebar").Padding=new Thickness(10*Layout.Spacing,4,0,0);
   Get<TextBlock>("WidgetMonth").FontSize=Layout.MonthText;Get<TextBlock>("WidgetDay").FontSize=Layout.HeadingText;Get<TextBlock>("WidgetTaskHeading").FontSize=Layout.HeadingText;
   Get<RowDefinition>("WidgetMonthRow").Height=new GridLength(Math.Max(Layout.MonthHeight,Layout.MonthText*1.6+10));Get<RowDefinition>("WidgetWeekRow").Height=new GridLength(Math.Max(Layout.WeekHeight,Layout.WeekText*1.6));
   foreach(TextBlock text in Get<UniformGrid>("WidgetWeekdays").Children)text.FontSize=Layout.WeekText;
   foreach(string name in new[]{"WidgetCustomize","WidgetFit","WidgetLock","WidgetOpen","WidgetHide","WidgetToday","WidgetRefresh","WidgetPrev","WidgetNext"}){var button=Get<Button>(name);button.FontSize=Layout.ToolbarText;button.Padding=new Thickness(6*Layout.Spacing,3*Layout.Spacing,6*Layout.Spacing,3*Layout.Spacing);}
   Get<Grid>("WidgetDrag").Margin=new Thickness(0,0,0,6*Layout.Spacing);Get<TextBlock>("WidgetSync").FontSize=Layout.FooterText;
   Get<StackPanel>("WidgetTaskSection").Margin=new Thickness(0,Layout.TaskGap,0,0);
   }finally{applyingLayout=false;}
  }
  void ScaleItems(){
   foreach(FrameworkElement item in Get<StackPanel>("WidgetAgenda").Children)item.Margin=new Thickness(0,0,0,10*Layout.Spacing);
   foreach(var item in Get<StackPanel>("WidgetTasks").Children){var check=item as CheckBox;if(check==null)continue;check.Margin=new Thickness(0,4*Layout.Spacing,0,8*Layout.Spacing);check.LayoutTransform=new ScaleTransform(Layout.CheckSize,Layout.CheckSize);}
  }
  public void Customize(){
   var panel=new StackPanel{Margin=new Thickness(24)};var editor=new Window{Title="위젯 꾸미기",Owner=Window,Width=460,Height=700,MinWidth=380,MinHeight=400,WindowStartupLocation=WindowStartupLocation.CenterScreen,Background=B(store.Data.WidgetDark?"#18202B":"#F4F6F9"),Foreground=B(Ink),FontFamily=Window.FontFamily,ShowInTaskbar=false,Resources=Window.Resources,};
   var preview=new Grid{Height=176,Margin=new Thickness(24,12,24,0),ClipToBounds=true};
   var surface=Get<Border>("WidgetSurface");
   string renderedBounds=null;
   preview.Children.Add(new Border{Background=new VisualBrush(surface){Stretch=Stretch.Fill},BorderBrush=B("#D5DEE8"),BorderThickness=new Thickness(1)});
   preview.Children.Add(new System.Windows.Shapes.Path{Fill=B("#183182F6"),Stroke=B("#3182F6"),StrokeThickness=2,IsHitTestVisible=false});
   var selection=new TextBlock{Text="조절할 항목에 마우스를 올려 보세요",Margin=new Thickness(24,8,24,6),Foreground=B("#3182F6")};
   var shell=new DockPanel();DockPanel.SetDock(preview,Dock.Top);shell.Children.Add(preview);DockPanel.SetDock(selection,Dock.Top);shell.Children.Add(selection);shell.Children.Add(new ScrollViewer{Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});editor.Content=shell;
   string activeTarget=null;
   Action redraw=()=>{
    var group=new GeometryGroup();
    if(activeTarget!=null&&surface.ActualWidth>0&&surface.ActualHeight>0){
     foreach(string name in activeTarget.Split(',')){
      var element=Get<FrameworkElement>(name);
      if(element==null||!element.IsVisible||element.ActualWidth<=0||element.ActualHeight<=0)continue;
      var bounds=new Rect(new Point(),element.RenderSize);
      if(element!=surface)bounds=element.TransformToAncestor(surface).TransformBounds(bounds);
      double sx=preview.ActualWidth/surface.ActualWidth,sy=preview.ActualHeight/surface.ActualHeight;
      group.Children.Add(new RectangleGeometry(new Rect(bounds.X*sx,bounds.Y*sy,bounds.Width*sx,bounds.Height*sy)));
     }
    }
    string boundsKey=string.Join(";",System.Linq.Enumerable.Select(group.Children,g=>g.Bounds.ToString(CultureInfo.InvariantCulture)));
    if(boundsKey==renderedBounds)return;
    renderedBounds=boundsKey;
    ((System.Windows.Shapes.Path)preview.Children[1]).Data=group;
   };
   preview.SizeChanged+=(s,e)=>redraw();
   Action<string,string> pointAt=(target,caption)=>{activeTarget=target;selection.Text=caption;redraw();preview.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(redraw));};
   panel.Children.Add(new TextBlock{Text="내 책상에 맞게",FontSize=24,FontWeight=FontWeights.Bold,Margin=new Thickness(0,0,0,8)});
   panel.Children.Add(new TextBlock{Text="구분선을 좌우로 끌어 영역 너비를 조절하세요. 아래 값은 위젯에 바로 반영되고 자동 저장됩니다. 전체 크기는 위젯 오른쪽 아래 모서리로 조절합니다.",TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,18)});
   AddSetting(panel,pointAt,"WidgetSurface","위젯 전체 너비",Window.MinWidth,Math.Max(Window.MinWidth,SystemParameters.WorkArea.Width-16),Window.Width,v=>{store.Data.WidgetAutoFit=false;Window.Width=v;store.Data.WidgetWidth=v;});
   AddSetting(panel,pointAt,"WidgetSurface","위젯 전체 높이",Window.MinHeight,Math.Max(Window.MinHeight,SystemParameters.WorkArea.Height-16),Window.Height,v=>{store.Data.WidgetAutoFit=false;Window.Height=v;store.Data.WidgetHeight=v;});
   AddSetting(panel,pointAt,"WidgetSurface","배경 불투명도 (%)",15,100,store.Data.WidgetOpacity*100,v=>store.Data.WidgetOpacity=v/100);
   AddSetting(panel,pointAt,"WidgetCalendar,WidgetSidebar","달력 너비 비율 (%)",35,85,Layout.CalendarShare*100,v=>Layout.CalendarShare=v/100);
   AddSetting(panel,pointAt,"WidgetCalendar","달력 세로 크기",360,1400,Layout.CalendarHeight,v=>Layout.CalendarHeight=v);
   AddSetting(panel,pointAt,"WidgetMonth","월 제목 글자",14,40,Layout.MonthText,v=>Layout.MonthText=v);
   AddSetting(panel,pointAt,"WidgetWeekdays","요일 글자",8,22,Layout.WeekText,v=>Layout.WeekText=v);
   AddSetting(panel,pointAt,"WidgetDays","날짜 숫자 크기 (%)",70,200,Layout.CalendarText*100,v=>Layout.CalendarText=v/100);
   AddSetting(panel,pointAt,"WidgetDays","달력 일정 글자 (%)",70,200,Layout.EventText*100,v=>Layout.EventText=v/100);
   AddSetting(panel,pointAt,"WidgetDays","날짜당 표시할 일정 수",1,10,Layout.EventsPerDay,v=>Layout.EventsPerDay=(int)v);
   AddSetting(panel,pointAt,"WidgetDays","일정당 표시할 줄 수",1,6,Layout.EventLines,v=>Layout.EventLines=(int)v);
   AddSetting(panel,pointAt,"WidgetDay,WidgetTaskHeading","선택한 날 · 할 일 제목 글자",10,28,Layout.HeadingText,v=>Layout.HeadingText=v);
   AddSetting(panel,pointAt,"WidgetAgenda","선택한 날 일정 글자 (%)",70,200,Layout.AgendaText*100,v=>Layout.AgendaText=v/100);
   AddSetting(panel,pointAt,"WidgetTasks","할 일 · 마감일 글자 (%)",70,200,Layout.TaskText*100,v=>Layout.TaskText=v/100);
   AddSetting(panel,pointAt,"WidgetTasks","체크박스와 할 일 크기 (%)",75,175,Layout.CheckSize*100,v=>Layout.CheckSize=v/100);
   AddSetting(panel,pointAt,"WidgetDrag","상단 버튼 글자",8,18,Layout.ToolbarText,v=>Layout.ToolbarText=v);
   AddSetting(panel,pointAt,"WidgetSync","하단 상태 글자",8,18,Layout.FooterText,v=>Layout.FooterText=v);
   AddSetting(panel,pointAt,"WidgetSurface","바깥 여백",4,40,Layout.Padding,v=>Layout.Padding=v);
   AddSetting(panel,pointAt,"WidgetSurface","항목 · 버튼 간격 (%)",50,250,Layout.Spacing*100,v=>Layout.Spacing=v/100);
   AddSetting(panel,pointAt,"WidgetMonth","월 제목 영역 높이",40,100,Layout.MonthHeight,v=>Layout.MonthHeight=v);
   AddSetting(panel,pointAt,"WidgetWeekdays","요일 영역 높이",18,55,Layout.WeekHeight,v=>Layout.WeekHeight=v);
   AddSetting(panel,pointAt,"WidgetTaskSection","일정과 할 일 사이 간격",0,120,Layout.TaskGap,v=>Layout.TaskGap=v);
   var reset=new Button{Content="기본 크기로 되돌리기",Margin=new Thickness(0,12,0,8)};reset.Click+=(s,e)=>{store.Data.WidgetLayout=new WidgetLayout();Render(lastMonth,lastSelected);save();editor.Close();Customize();};panel.Children.Add(reset);
   var done=new Button{Content="완료"};done.Click+=(s,e)=>editor.Close();panel.Children.Add(done);editor.Closed+=(s,e)=>save();editor.ShowDialog();
  }
  void AddSetting(StackPanel panel,Action<string,string> pointAt,string target,string caption,double min,double max,double value,Action<double> change){
   var row=new Grid{Margin=new Thickness(0,0,0,4)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(64)});row.Children.Add(new TextBlock{Text=caption,VerticalAlignment=VerticalAlignment.Center});var number=new TextBox{Text=Math.Round(value).ToString(CultureInfo.InvariantCulture),Padding=new Thickness(4),HorizontalContentAlignment=HorizontalAlignment.Right};Grid.SetColumn(number,1);row.Children.Add(number);panel.Children.Add(row);
   var slider=new Slider{Minimum=min,Maximum=max,Value=value,TickFrequency=1,IsSnapToTickEnabled=true,Margin=new Thickness(0,4,0,18)};panel.Children.Add(slider);
   row.MouseEnter+=(s,e)=>pointAt(target,caption);slider.MouseEnter+=(s,e)=>pointAt(target,caption);number.GotKeyboardFocus+=(s,e)=>pointAt(target,caption);slider.GotKeyboardFocus+=(s,e)=>pointAt(target,caption);
   slider.ValueChanged+=(s,e)=>{number.Text=Math.Round(slider.Value).ToString(CultureInfo.InvariantCulture);change(slider.Value);Render(lastMonth,lastSelected);pointAt(target,caption);layoutSave.Stop();layoutSave.Start();};
   Action commit=()=>{double parsed;if(double.TryParse(number.Text,NumberStyles.Float,CultureInfo.InvariantCulture,out parsed)&&!double.IsNaN(parsed)&&!double.IsInfinity(parsed))slider.Value=Math.Max(min,Math.Min(max,parsed));number.Text=Math.Round(slider.Value).ToString(CultureInfo.InvariantCulture);};number.LostKeyboardFocus+=(s,e)=>commit();number.KeyDown+=(s,e)=>{if(e.Key==System.Windows.Input.Key.Enter){commit();e.Handled=true;}};
  }
 }
}
