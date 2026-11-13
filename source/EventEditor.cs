using System;
using System.Linq;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DeskFlow {
 partial class Widget {
  internal void EditEvent(CalEvent original){
   var p=new StackPanel{Margin=new Thickness(24)};var w=Dialog(original==null?"새 일정":"일정 보기 / 편집",p,760);w.Width=520;
   p.Children.Add(Text(original==null?"어떤 일정이 있나요?":"일정 상세",22,"#191F28"));
   var status=Text("",11,"#8B95A1");status.Margin=new Thickness(0,8,0,16);p.Children.Add(status);
   p.Children.Add(Text("저장할 캘린더",12));var calendars=new ComboBox{Name="EventCalendar",Margin=new Thickness(0,8,0,16),Padding=new Thickness(10),MinHeight=38};
   var local=new CalendarInfo{Id="__local",Title="이 PC에만 저장",Color="#8B95A1",Role="owner",Selected=true};calendars.Items.Add(local);calendars.SelectedItem=local;p.Children.Add(calendars);
   p.Children.Add(Text("제목",12));var title=new TextBox{Name="EventTitle",Height=42,MaxLength=200,Margin=new Thickness(0,8,0,16),MinHeight=42};p.Children.Add(title);
   var allDay=new CheckBox{Name="EventAllDay",Content="하루 종일",Margin=new Thickness(0,0,0,12)};p.Children.Add(allDay);
   var startDate=new DatePicker{Name="EventStartDate",SelectedDate=selected.Date,MinWidth=180};var endDate=new DatePicker{Name="EventEndDate",SelectedDate=selected.Date,MinWidth=180};
   var startTime=new TimePicker{Name="EventStartTime",Text="09:00",Width=156,MinHeight=32,Margin=new Thickness(10,0,0,0)};var endTime=new TimePicker{Name="EventEndTime",Text="10:00",Width=156,MinHeight=32,Margin=new Thickness(10,0,0,0)};
   p.Children.Add(Text("시작",12));var from=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,12)};from.Children.Add(startDate);from.Children.Add(startTime);p.Children.Add(from);
   p.Children.Add(Text("종료 · 종일 일정은 마지막 날짜 포함",12));var until=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,8)};until.Children.Add(endDate);until.Children.Add(endTime);p.Children.Add(until);
   var zone=Text("시간대: "+TimeZoneInfo.Local.DisplayName,10,"#8B95A1");zone.Margin=new Thickness(0,0,0,16);p.Children.Add(zone);
   p.Children.Add(Text("장소",12));var location=new TextBox{Name="EventLocation",Height=42,MaxLength=1000,MinHeight=42,Margin=new Thickness(0,8,0,16)};p.Children.Add(location);
   p.Children.Add(Text("메모",12));var description=new TextBox{Name="EventDescription",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MaxLength=10000,MinHeight=90,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,8,0,16)};p.Children.Add(description);
   var footer=new StackPanel{Margin=new Thickness(24,8,24,18)};var error=Text("",12,"#F04452");error.Margin=new Thickness(0,0,0,10);footer.Children.Add(error);
   var save=Btn("일정 저장",()=>{});save.Name="EventSave";save.Background=Brush("#3182F6");save.Foreground=Brushes.White;save.Padding=new Thickness(12);footer.Children.Add(save);
   var delete=Btn("일정 삭제",()=>{});delete.Name="EventDelete";delete.Foreground=Brush("#F04452");delete.Visibility=original==null?Visibility.Collapsed:Visibility.Visible;footer.Children.Add(delete);
   var confirmation=new StackPanel{Visibility=Visibility.Collapsed,Margin=new Thickness(0,8,0,8)};
   var deleteNote=Text("",11,"#F04452");confirmation.Children.Add(deleteNote);
   var scope=new StackPanel{Name="EventDeleteScope",Visibility=Visibility.Collapsed,Margin=new Thickness(0,8,0,8)};
   var onlyThis=new RadioButton{Name="EventDeleteSingle",GroupName="DeleteScope",Content="이 일정만 삭제",IsChecked=true,Margin=new Thickness(0,4,0,4)};
   var wholeSeries=new RadioButton{Name="EventDeleteSeries",GroupName="DeleteScope",Content="반복 일정 전체 삭제 · 과거와 앞으로의 모든 회차",Margin=new Thickness(0,4,0,4)};scope.Children.Add(onlyThis);scope.Children.Add(wholeSeries);confirmation.Children.Add(scope);
   var confirmDelete=Btn("삭제 확인",()=>{});confirmDelete.Name="EventDeleteConfirm";confirmDelete.Background=Brush("#F04452");confirmDelete.Foreground=Brushes.White;confirmation.Children.Add(confirmDelete);
   var cancelDelete=Btn("취소",()=>confirmation.Visibility=Visibility.Collapsed);confirmation.Children.Add(cancelDelete);footer.Children.Add(confirmation);footer.Children.Add(Btn("닫기",()=>w.Close()));
   var scrolling=(ScrollViewer)w.Content;w.Content=null;var layout=new DockPanel();DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);layout.Children.Add(scrolling);w.Content=layout;
   DateTime? lastAutoStart=null;CalEvent current=original,seriesTarget=null;bool loading=false,saving=false,closed=false,filling=false,loadFailed=false;string draftId=original!=null?original.Id:Guid.NewGuid().ToString("N");
   Action update=()=>{bool editable=current==null||current.Local||(google.CanWrite&&current.Writable&&!string.IsNullOrEmpty(current.Id));save.IsEnabled=editable&&!loading&&!saving&&!loadFailed;delete.IsEnabled=confirmDelete.IsEnabled=current!=null&&editable&&!loading&&!saving&&!loadFailed;confirmDelete.IsEnabled=confirmDelete.IsEnabled&&(wholeSeries.IsChecked!=true||seriesTarget!=null);onlyThis.IsEnabled=wholeSeries.IsEnabled=!loading&&!saving;cancelDelete.IsEnabled=!saving;startTime.IsEnabled=endTime.IsEnabled=allDay.IsChecked!=true&&editable&&!loading&&!saving;title.IsReadOnly=location.IsReadOnly=description.IsReadOnly=!editable||loading||saving;startDate.IsEnabled=endDate.IsEnabled=allDay.IsEnabled=editable&&!loading&&!saving;calendars.IsEnabled=current==null&&!loading&&!saving;if(!loading){var cal=calendars.SelectedItem as CalendarInfo;status.Text=cal!=null&&cal.Id=="__local"?"이 PC에만 저장되며 Google에는 동기화되지 않습니다.":!google.CanWrite?"Google 일정 쓰기 권한이 필요합니다. 설정에서 Google에 다시 로그인해 주세요.":!editable?"읽기 전용 일정입니다.":"저장하면 Google 캘린더와 위젯에 반영됩니다.";if(current!=null&&current.RecurringId!="")status.Text+=" 반복 일정은 선택한 회차만 변경합니다.";if(current!=null&&current.HasAttendees)status.Text+=" Google이 기존 참석자에게 변경 알림을 보낼 수 있습니다.";}};
   Action<CalEvent> fill=e=>{filling=true;title.Text=e.Title;description.Text=e.Description??"";location.Text=e.Location??"";allDay.IsChecked=e.AllDay;DateTime a=GoogleCalendar.Start(e),b=GoogleCalendar.End(e);startDate.SelectedDate=a.Date;endDate.SelectedDate=e.AllDay?b.AddDays(-1).Date:b.Date;startTime.Text=a.ToString("HH:mm");endTime.Text=b.ToString("HH:mm");lastAutoStart=a;filling=false;};
   if(original!=null){fill(original);if(!original.Local){calendars.Items.Clear();var c=store.Data.Calendars.FirstOrDefault(x=>x.Id==original.CalendarId)??new CalendarInfo{Id=original.CalendarId,Title=original.Calendar,Role=original.Writable?"writer":"reader",Color=original.Color};calendars.Items.Add(c);calendars.SelectedItem=c;if(!string.IsNullOrEmpty(original.Link))p.Children.Add(Btn("Google 캘린더에서 열기 ↗",()=>Open(original.Link)));}}
   Action defaultEnd=()=>{if(filling||loading||saving||allDay.IsChecked==true||!startDate.SelectedDate.HasValue)return;DateTime time;if(!DateTime.TryParseExact(startTime.Text.Trim(),new[]{"H:mm","HH:mm"},CultureInfo.InvariantCulture,DateTimeStyles.None,out time))return;DateTime begin=startDate.SelectedDate.Value.Date.Add(time.TimeOfDay);if(lastAutoStart==begin)return;lastAutoStart=begin;DateTime finish=begin.AddHours(1);endDate.SelectedDate=finish.Date;endTime.Text=finish.ToString("HH:mm");};
   startTime.TimeChanged+=(s,e)=>defaultEnd();startDate.SelectedDateChanged+=(s,e)=>defaultEnd();
   allDay.Click+=(s,e)=>{lastAutoStart=null;defaultEnd();update();};calendars.SelectionChanged+=(s,e)=>update();
   w.Closing+=(s,e)=>{if(saving){e.Cancel=true;error.Text="저장 결과를 확인 중입니다. 잠시만 기다려 주세요.";}};w.Closed+=(s,e)=>closed=true;
   w.Loaded+=async(s,e)=>{if(current!=null&&!current.Local&&google.Connected&&!string.IsNullOrEmpty(current.Id)){loading=true;status.Text="최신 일정을 불러오는 중입니다…";update();try{var fresh=await google.ReadEvent(current.CalendarId,current.Id);if(closed)return;current=fresh;fill(current);}catch(Exception ex){if(!closed){error.Text="최신 일정을 불러오지 못했습니다. "+ex.Message;loadFailed=true;save.IsEnabled=delete.IsEnabled=confirmDelete.IsEnabled=false;}return;}finally{loading=false;}if(!closed)update();}
    else if(current==null&&google.CanWrite){loading=true;update();status.Text="Google 캘린더 목록을 불러오는 중입니다…";try{var list=await google.ListCalendars();if(closed)return;foreach(var c in list.Where(x=>x.Writable&&x.Selected))calendars.Items.Add(c);var primary=list.FirstOrDefault(x=>x.Writable&&x.Primary);if(primary!=null)calendars.SelectedItem=primary;}catch(Exception ex){if(!closed)error.Text="Google 목록을 불러오지 못했습니다. 이 PC 저장은 가능합니다. "+ex.Message;}finally{loading=false;}if(!closed)update();}};
   save.Click+=async(s,e)=>{if(loading||saving||loadFailed)return;error.Text="";try{
    if(!startDate.SelectedDate.HasValue||!endDate.SelectedDate.HasValue)throw new Exception("시작 날짜와 종료 날짜를 선택해 주세요.");DateTime a=startDate.SelectedDate.Value.Date,b=endDate.SelectedDate.Value.Date;bool full=allDay.IsChecked==true;
    if(full)b=b.AddDays(1);else {DateTime at,bt;if(!DateTime.TryParseExact(startTime.Text.Trim(),new[]{"H:mm","HH:mm"},CultureInfo.InvariantCulture,DateTimeStyles.None,out at)||!DateTime.TryParseExact(endTime.Text.Trim(),new[]{"H:mm","HH:mm"},CultureInfo.InvariantCulture,DateTimeStyles.None,out bt))throw new Exception("시간을 09:00처럼 입력해 주세요.");a=a.Add(at.TimeOfDay);b=b.Add(bt.TimeOfDay);}
    var draft=new EventDraft{Id=draftId,Title=title.Text,Description=description.Text,Location=location.Text,AllDay=full,Start=a,End=b};draft.Validate();var cal=calendars.SelectedItem as CalendarInfo;if(cal==null)throw new Exception("캘린더를 선택해 주세요.");
    saving=true;update();status.Text="일정을 저장하고 있습니다…";CalEvent saved;
    if(cal.Id=="__local"){saved=draft.LocalEvent();var previous=store.Data.LocalEvents.ToList();store.Data.LocalEvents.RemoveAll(x=>x.Id==saved.Id);store.Data.LocalEvents.Add(saved);try{store.Save();}catch{store.Data.LocalEvents=previous;throw;}}
    else {saved=await google.SaveEvent(cal,draft,current);eventChangeVersion++;store.Data.Events.RemoveAll(x=>x.Id==saved.Id&&x.CalendarId==saved.CalendarId);store.Data.Events.Add(saved);store.Save();}
    selected=GoogleCalendar.Start(saved).Date;month=selected;RenderCalendar();saving=false;w.Close();if(!saved.Local)await Sync();
   }catch(Exception ex){saving=false;update();error.Text=ex is TaskCanceledException?"저장 결과를 확인하지 못했습니다. 같은 창에서 다시 시도해 주세요.":ex.Message;}};
   Action updateDeleteNote=()=>{if(current==null)return;bool whole=wholeSeries.IsChecked==true;deleteNote.Text="‘"+current.Title+"’ "+(whole?"반복 일정 전체를 삭제합니다. 과거와 앞으로의 모든 회차가 Google 캘린더에서도 삭제됩니다.":"일정을 삭제하시겠습니까?"+(current.Local?" 이 PC에서 삭제됩니다.":" Google 캘린더에서도 삭제됩니다.")+(string.IsNullOrEmpty(current.RecurringId)?"":" 선택한 회차만 삭제합니다."))+(current.HasAttendees||seriesTarget!=null&&seriesTarget.HasAttendees?" 참석자에게 취소 알림이 전송될 수 있습니다.":"");confirmDelete.Content=whole?"반복 일정 전체 삭제 확인":"삭제 확인";};
   onlyThis.Checked+=(s,e)=>{updateDeleteNote();update();};
   wholeSeries.Checked+=async(s,e)=>{if(current==null||string.IsNullOrEmpty(current.RecurringId))return;seriesTarget=null;loading=true;error.Text="";updateDeleteNote();update();status.Text="반복 일정 전체의 최신 정보를 확인하고 있습니다…";
    try{var parent=await google.ReadSeries(current);if(closed)return;seriesTarget=parent;}
    catch(Exception ex){if(!closed)error.Text="반복 일정 전체를 확인하지 못했습니다. 삭제하지 않았습니다. "+ex.Message;}
    finally{loading=false;if(!closed){updateDeleteNote();update();}}
   };
   delete.Click+=(s,e)=>{if(current==null||loading||saving||loadFailed)return;seriesTarget=null;onlyThis.IsChecked=true;scope.Visibility=!current.Local&&!string.IsNullOrEmpty(current.RecurringId)?Visibility.Visible:Visibility.Collapsed;updateDeleteNote();confirmation.Visibility=Visibility.Visible;update();};
   confirmDelete.Click+=async(s,e)=>{if(current==null||loading||saving||loadFailed||confirmation.Visibility!=Visibility.Visible)return;error.Text="";saving=true;update();status.Text="일정을 삭제하고 있습니다…";
    try{bool whole=wholeSeries.IsChecked==true&&scope.Visibility==Visibility.Visible;if(whole&&seriesTarget==null)throw new Exception("반복 일정 전체의 최신 정보를 먼저 확인해 주세요.");if(current.Local){var previous=store.Data.LocalEvents.ToList();store.Data.LocalEvents.RemoveAll(x=>x.Id==current.Id);try{store.Save();}catch{store.Data.LocalEvents=previous;throw;}}
     else{var target=whole?seriesTarget:current;await google.DeleteEvent(target);eventChangeVersion++;store.Data.Events.RemoveAll(x=>GoogleCalendar.DeletedBy(x,target,whole));store.Save();}
     RenderCalendar();saving=false;w.Close();if(!current.Local)await Sync();
    }catch(Exception ex){saving=false;update();error.Text=ex is TaskCanceledException?"삭제 결과를 확인하지 못했습니다. 다시 시도하거나 동기화하여 확인해 주세요.":ex.Message;}
   };
   update();w.ShowDialog();
  }
 }
}
