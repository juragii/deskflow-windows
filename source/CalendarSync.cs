using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Net.NetworkInformation;
using Microsoft.Win32;

namespace DeskFlow {
 sealed class SyncSchedule {
  public DateTime LastAttempt=DateTime.MinValue,LastSuccess=DateTime.MinValue,RetryAfter=DateTime.MinValue;
  public int Failures;
  public bool Due(DateTime now,bool foreground){return now>=RetryAfter&&(now-LastAttempt).TotalSeconds>=(foreground?60:180);}
  public void Completed(DateTime now,bool success,double jitter){if(success){Failures=0;LastSuccess=now;RetryAfter=DateTime.MinValue;}else{Failures=Math.Min(5,Failures+1);RetryAfter=now.AddSeconds(Math.Min(900,60*Math.Pow(2,Failures-1))*(1+Math.Max(0,Math.Min(.1,jitter))));}}
 }
 partial class Widget {
  readonly SyncSchedule syncSchedule=new SyncSchedule();readonly Random syncRandom=new Random();
  bool syncPending,syncClosed;Task activeSync;
  NetworkAvailabilityChangedEventHandler networkHandler;PowerModeChangedEventHandler powerHandler;
  void InitCalendarSync(){
   timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(5)};
   timer.Tick+=(s,e)=>{if(DateTime.Today!=lastCalendarDay){lastCalendarDay=DateTime.Today;RefreshDailyHeader(DateTime.Today);RenderCalendar();RenderTasks();}AutoSync(false);};timer.Start();
   Window.Activated+=(s,e)=>{RefreshDailyHeader(DateTime.Today);AutoSync(false);};
   Window.Loaded+=(s,e)=>AutoSync(false);
   if(suppressNative)return;
   networkHandler=(s,e)=>{if(e.IsAvailable)QueueRecovery();};powerHandler=(s,e)=>{if(e.Mode==PowerModes.Resume)QueueRecovery();};
   NetworkChange.NetworkAvailabilityChanged+=networkHandler;SystemEvents.PowerModeChanged+=powerHandler;
  }
  DateTime lastCalendarDay=DateTime.Today;
  void QueueRecovery(){if(syncClosed||Window.Dispatcher.HasShutdownStarted)return;Window.Dispatcher.BeginInvoke(new Action(()=>AutoSync(true)));}
  void AutoSync(bool recovery){if(syncClosed||suppressNative||!store.Data.AutoCalendarSync||syncing||connecting||!google.Connected)return;DateTime now=DateTime.UtcNow;if(recovery?(now-syncSchedule.LastAttempt).TotalSeconds>=15:syncSchedule.Due(now,Window.IsVisible&&Window.WindowState!=WindowState.Minimized)){var ignored=Sync();}}
  void StopCalendarSync(){syncClosed=true;if(timer!=null)timer.Stop();if(networkHandler!=null)NetworkChange.NetworkAvailabilityChanged-=networkHandler;if(powerHandler!=null)SystemEvents.PowerModeChanged-=powerHandler;}
  void ManualSync(){if(connecting){syncError="Google 로그인 완료 후 다시 동기화해 주세요.";RenderStatus();return;}if(!google.Connected){syncError="Google 캘린더를 먼저 연결해 주세요. 설정에서 로그인할 수 있습니다.";RenderStatus();return;}var ignored=Sync();}
  Task Sync(){if(syncClosed||connecting||!google.Connected)return Task.FromResult(0);if(syncing){syncPending=true;return activeSync??Task.FromResult(0);}activeSync=RunCalendarSync();return activeSync;}
  async Task RunCalendarSync(){
   syncing=true;syncError="";RenderStatus();
   try{do{syncPending=false;DateTime requested=month;long generation=eventChangeVersion;syncSchedule.LastAttempt=DateTime.UtcNow;
    try{var events=await google.Fetch(requested);if(syncClosed)return;
     bool current=generation==eventChangeVersion&&month.Year==requested.Year&&month.Month==requested.Month;
     if(current){store.Data.Events=events;store.Data.LastSync=DateTime.Now.ToString("MM.dd HH:mm:ss");SafeSave();syncSchedule.Completed(DateTime.UtcNow,true,0);}else syncPending=true;
     syncError="";
    }catch(Exception e){if(syncClosed)return;syncSchedule.Completed(DateTime.UtcNow,false,syncRandom.NextDouble()*.1);syncError="동기화 실패 · 이전 일정 유지 · "+e.Message;syncPending=false;}
   }while(syncPending&&!syncClosed&&google.Connected);
   }finally{syncing=false;if(!syncClosed)RenderCalendar();}
  }
  string CalendarSyncStatus(){return syncing?"Google 캘린더 동기화 중…":syncError!=""?syncError:google.Connected?(store.Data.LastSync==""?"연결됨 · 지금 동기화를 눌러 주세요":"마지막 동기화 "+store.Data.LastSync)+(store.Data.AutoCalendarSync?" · 자동 1분 / 백그라운드 3분":" · 자동 동기화 꺼짐"):"Google 미연결 · 할 일은 이 PC에 자동 저장됩니다.";}
  void CalendarSyncSettings(StackPanel panel){
   panel.Children.Add(Text("캘린더 동기화",17));
   var enabled=new CheckBox{Name="AutoCalendarSync",Content="Google 캘린더 자동 동기화",IsChecked=store.Data.AutoCalendarSync,Margin=new Thickness(0,12,0,8)};
   enabled.Click+=(s,e)=>{store.Data.AutoCalendarSync=enabled.IsChecked==true;SafeSave();RenderStatus();AutoSync(false);};panel.Children.Add(enabled);
   panel.Children.Add(Btn("지금 동기화",ManualSync));
   var info=Text("본 화면 사용 중 약 1분, 백그라운드에서는 약 3분 간격으로 확인합니다. 절전 복귀·인터넷 재연결 시에도 확인하며, 오류가 반복되면 최대 약 15분 간격으로 재시도합니다.\nGoogle 일정의 추가·수정은 저장 즉시 전송됩니다. ‘이 PC’ 일정과 할 일은 PC에만 저장됩니다.",11,"#8B95A1");info.Margin=new Thickness(0,8,0,20);panel.Children.Add(info);
  }
  internal async Task VerifyCalendarSync(string root){
   var original=google;var oldMonth=month;var handler=new CalendarSyncTests.Server();
   store.Token="test-only-sync";google=new GoogleCalendar(store,new System.Net.Http.HttpClient(handler));
   try{
    var first=Sync();var second=Sync();if(first!=second)throw new Exception("Sync requests did not join the active operation");
    month=month.AddMonths(1);handler.Pending.SetResult(CalendarSyncTests.Server.Response("{\"items\":[]}"));await first;
    if(handler.EventReads!=2||syncing||store.Data.LastSync==""||store.Data.Events.Count!=1||store.Data.Events[0].Title!="latest")throw new Exception("Queued sync or month replacement failed");
    string last=store.Data.LastSync;handler.Fail=true;await Sync();
    if(store.Data.Events.Count!=1||store.Data.LastSync!=last||syncSchedule.Failures!=1||syncError=="")throw new Exception("Failure must preserve cached events and last successful sync");
    handler.Fail=false;await Sync();if(syncSchedule.Failures!=0||syncError!="")throw new Exception("Manual sync must recover during backoff");
    File.AppendAllText(Path.Combine(root,"calendar-sync-result.txt")," WPF integration PASS: concurrent requests coalesce, changed month queues fresh fetch, failed fetch keeps cached events/last success, manual retry bypasses backoff and recovers.");
   }finally{store.Token="";google=original;month=oldMonth;store.Data.Events.Clear();store.Data.LastSync="";syncError="";RenderCalendar();}
  }
 }
 static class CalendarSyncTests {
  internal sealed class Server:System.Net.Http.HttpMessageHandler {
   public int EventReads;public bool Fail;public readonly TaskCompletionSource<System.Net.Http.HttpResponseMessage> Pending=new TaskCompletionSource<System.Net.Http.HttpResponseMessage>();
   internal static System.Net.Http.HttpResponseMessage Response(string json){return new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new System.Net.Http.StringContent(json)};}
   protected override Task<System.Net.Http.HttpResponseMessage> SendAsync(System.Net.Http.HttpRequestMessage request,System.Threading.CancellationToken token){
    string url=request.RequestUri.AbsoluteUri;if(url.Contains("oauth2.googleapis.com"))return Task.FromResult(Response("{\"access_token\":\"mock\",\"expires_in\":3600}"));
    if(url.Contains("calendarList"))return Task.FromResult(Response("{\"items\":[{\"id\":\"test\",\"primary\":true,\"accessRole\":\"owner\",\"summary\":\"Test\"}]}"));
    EventReads++;if(Fail)return Task.FromResult(new System.Net.Http.HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable));
    if(EventReads==1)return Pending.Task;
    return Task.FromResult(Response("{\"items\":[{\"id\":\"latest\",\"summary\":\"latest\",\"start\":{\"date\":\"2026-12-01\"},\"end\":{\"date\":\"2026-12-02\"}}]}"));
   }
  }
  static void Check(bool ok,string label){if(!ok)throw new Exception("Calendar sync: "+label);}
  public static void Run(string root){var p=new SyncSchedule();DateTime now=new DateTime(2026,11,13,0,0,0,DateTimeKind.Utc);Check(p.Due(now,true),"first sync");p.LastAttempt=now;Check(!p.Due(now.AddSeconds(59),true)&&p.Due(now.AddSeconds(60),true),"foreground interval");Check(!p.Due(now.AddSeconds(179),false)&&p.Due(now.AddSeconds(180),false),"background interval");p.Completed(now,false,0);Check(p.RetryAfter==now.AddSeconds(60)&&!p.Due(now.AddSeconds(59),true),"first backoff");for(int i=0;i<10;i++)p.Completed(now,false,0);Check(p.RetryAfter==now.AddMinutes(15),"bounded backoff");p.Completed(now,true,0);Check(p.Failures==0&&p.LastSuccess==now&&p.RetryAfter==DateTime.MinValue,"success reset");Check(Store.Json.Deserialize<State>("{}").AutoCalendarSync,"old settings default enabled");File.WriteAllText(Path.Combine(root,"calendar-sync-result.txt"),"PASS: foreground/background schedule, bounded failure backoff, successful recovery reset, existing settings default. Google fetch pagination and event-write tests run separately. Live Google account synchronization requires user credentials.");}
 }
}
