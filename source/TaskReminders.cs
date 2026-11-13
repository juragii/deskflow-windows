using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace DeskFlow {
 public static class TodoDeadline {
  public static DateTime? Due(Todo t){DateTime day;TimeSpan time;if(!DateTime.TryParseExact(t.Due,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out day)||!TimeSpan.TryParseExact(t.DueTime??"","hh\\:mm",CultureInfo.InvariantCulture,out time))return null;return day.Add(time);}
  public static string Key(Todo t){return t.Due+"|"+t.DueTime+"|"+t.ReminderMinutes;}
  public static bool Ready(Todo t,DateTime now){var due=Due(t);return !t.Done&&t.Reminder&&due.HasValue&&now>=due.Value.AddMinutes(-Math.Max(0,t.ReminderMinutes));}
  public static string Message(Todo t){var due=Due(t);return t.Title+"\n마감: "+(due.HasValue?due.Value.ToString("M월 d일 HH:mm"):t.Due);}
 }
 sealed class ReminderEngine {
  readonly Store store;readonly Func<List<Todo>,Task> pc;readonly Func<Todo,Task> phone;readonly Func<bool> phoneEnabled;
  readonly Dictionary<string,DateTime> retry=new Dictionary<string,DateTime>();readonly Dictionary<string,int> failures=new Dictionary<string,int>();public bool Busy,Stopped;public string LastError="";
  public ReminderEngine(Store s,Func<List<Todo>,Task> desktop,Func<Todo,Task> mobile,Func<bool> enabled){store=s;pc=desktop;phone=mobile;phoneEnabled=enabled;}
  public async Task Check(DateTime now){if(Busy||Stopped)return;Busy=true;try{
   var due=store.Data.Tasks.Where(t=>TodoDeadline.Ready(t,now)).ToList();
   var desktop=due.Where(t=>store.Data.PcTaskAlerts&&t.PcReminderKey!=TodoDeadline.Key(t)).ToList();
   if(desktop.Count>0){await pc(desktop);foreach(var t in desktop)t.PcReminderKey=TodoDeadline.Key(t);store.Save();}
   if(Stopped||!phoneEnabled())return;
   foreach(var t in due){if(Stopped)return;string key=TodoDeadline.Key(t),retryKey=t.Id+"|"+key;if(t.PhoneReminderKey==key||t.Done||!store.Data.Tasks.Contains(t)||retry.ContainsKey(retryKey)&&now<retry[retryKey])continue;
    try{await phone(t);if(TodoDeadline.Key(t)==key)t.PhoneReminderKey=key;store.Save();retry.Remove(retryKey);failures.Remove(retryKey);LastError="";}
    catch{int count;failures.TryGetValue(retryKey,out count);count=Math.Min(5,count+1);failures[retryKey]=count;retry[retryKey]=now.AddMinutes(Math.Min(15,Math.Pow(2,count-1)));LastError="휴대폰 알림 전송 실패 · 연결 및 인증 정보를 확인해 주세요. 자동으로 재시도합니다.";}
   }
  }catch{LastError="알림 표시 또는 저장에 실패했습니다. 알림 설정과 저장 공간을 확인해 주세요.";}finally{Busy=false;}}
 }
 partial class Widget {
  TextBlock reminderStatus;TimePicker taskTime;DispatcherTimer reminderTimer;ReminderEngine reminders;
  void InitTaskReminders(){
   taskTime=new TimePicker{Text="18:00",Width=140};Find<ContentControl>("TaskTimeHost").Content=taskTime;
   Action enabled=()=>{bool on=Find<CheckBox>("TaskTimedDue").IsChecked==true;taskTime.IsEnabled=on;Find<ComboBox>("TaskReminderOffset").IsEnabled=on;Find<CheckBox>("TaskReminder").IsEnabled=on;};
   Find<CheckBox>("TaskTimedDue").Click+=(s,e)=>enabled();enabled();
   reminders=new ReminderEngine(store,items=>{if(suppressNative)return Task.FromResult(0);tray.BalloonTipTitle=items.Count==1?"나날 · 할 일 마감 알림":"나날 · "+items.Count+"개 할 일 마감";string message=string.Join("\n\n",items.Take(3).Select(TodoDeadline.Message))+(items.Count>3?"\n외 "+(items.Count-3)+"개":"");tray.BalloonTipText=message.Length>240?message.Substring(0,237)+"…":message;tray.ShowBalloonTip(10000);return Task.FromResult(0);},async item=>{var account=PhoneCredentials.Load(store.Dir);await new TelegramAlerts().Send(account,TodoDeadline.Message(item));},()=>store.Data.TelegramAlerts);
   reminderTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(15)};reminderTimer.Tick+=async(s,e)=>{if(!suppressNative){await reminders.Check(DateTime.Now);if(reminderStatus!=null)reminderStatus.Text=reminders.LastError;}};reminderTimer.Start();
   Window.Loaded+=async(s,e)=>{if(!suppressNative)await reminders.Check(DateTime.Now);};
  }
  void StopTaskReminders(){if(reminders!=null)reminders.Stopped=true;if(reminderTimer!=null)reminderTimer.Stop();}
  static ComboBox ReminderOffset(){var b=new ComboBox{MinWidth=125};foreach(var label in new[]{"마감 시각","5분 전","10분 전","30분 전","1시간 전","1일 전","2일 전","3일 전"})b.Items.Add(label);b.SelectedIndex=0;return b;}
  static int OffsetValue(int index){return new[]{0,5,10,30,60,1440,2880,4320}[Math.Max(0,Math.Min(7,index))];}
  void TaskAlertSettings(StackPanel p){
   p.Children.Add(Text("할 일 알림",17));var pc=new CheckBox{Content="컴퓨터에 마감 알림 표시",IsChecked=store.Data.PcTaskAlerts,Margin=new Thickness(0,10,0,8)};pc.Click+=(s,e)=>{store.Data.PcTaskAlerts=pc.IsChecked==true;SafeSave();};p.Children.Add(pc);
   p.Children.Add(Btn("PC 테스트 알림",()=>{tray.BalloonTipTitle="나날 · 테스트 알림";tray.BalloonTipText="이곳에 할 일의 마감 알림이 표시됩니다.";if(!suppressNative)tray.ShowBalloonTip(10000);}));
   p.Children.Add(Text("알림은 나날이 실행 중이고 컴퓨터가 깨어 있을 때 표시됩니다. 놓친 마감은 다음 실행·절전 복귀 후 한 번 알립니다. Windows 알림/방해 금지 설정에 따라 표시가 제한될 수 있습니다.",11,"#8B95A1"));
   reminderStatus=Text(reminders==null?"":reminders.LastError,11,"#F04452");p.Children.Add(reminderStatus);PhoneSettings(p);
  }
  internal void EditTask(Todo task){var p=new StackPanel{Margin=new Thickness(24)};p.Children.Add(Text("할 일 수정",20));var input=new TextBox{Name="TodoTitle",Text=task.Title,Margin=new Thickness(0,18,0,12)};p.Children.Add(input);p.Children.Add(Text("마감일 (선택)",12));var due=new DatePicker{Name="TodoDate",SelectedDate=task.Due==""?(DateTime?)null:DateTime.ParseExact(task.Due,"yyyy-MM-dd",CultureInfo.InvariantCulture),Margin=new Thickness(0,8,0,12)};p.Children.Add(due);
   var timed=new CheckBox{Name="TodoTimed",Content="마감 시간 지정",IsChecked=!string.IsNullOrEmpty(task.DueTime)};p.Children.Add(timed);var time=new TimePicker{Name="TodoTime",Text=string.IsNullOrEmpty(task.DueTime)?"18:00":task.DueTime,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,16)};p.Children.Add(time);
   var remind=new CheckBox{Name="TodoRemind",Content="PC · 연결한 휴대폰으로 알림",IsChecked=task.Reminder};p.Children.Add(remind);var offset=ReminderOffset();offset.Name="TodoOffset";offset.SelectedIndex=Array.IndexOf(new[]{0,5,10,30,60,1440,2880,4320},task.ReminderMinutes);offset.Margin=new Thickness(0,8,0,16);p.Children.Add(offset);Action enabled=()=>{time.IsEnabled=remind.IsEnabled=offset.IsEnabled=timed.IsChecked==true;};timed.Click+=(s,e)=>enabled();enabled();
   var error=Text("",11,"#F04452");p.Children.Add(error);var w=Dialog("할 일 수정",p,570);var save=Btn("저장",()=>{TimeSpan value=TimeSpan.Zero;if(input.Text.Trim()==""||input.Text.Trim().Length>500){error.Text="할 일을 1~500자로 입력해 주세요.";return;}bool hasTime=timed.IsChecked==true;if(hasTime&&(!due.SelectedDate.HasValue||!time.TryTime(out value))){error.Text="마감일과 올바른 시·분을 지정해 주세요.";return;}task.Title=input.Text.Trim();task.Due=due.SelectedDate.HasValue?due.SelectedDate.Value.ToString("yyyy-MM-dd"):"";task.DueTime=hasTime?value.ToString("hh\\:mm"):"";task.Reminder=remind.IsChecked==true;task.ReminderMinutes=OffsetValue(offset.SelectedIndex);SafeSave();RenderTasks();w.Close();});save.Name="TodoSave";p.Children.Add(save);w.ShowDialog();
  }
 }
}
