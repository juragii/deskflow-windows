using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Security.Cryptography;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Globalization;

namespace DeskFlow {
 public class Todo { public string Id=Guid.NewGuid().ToString(); public string Title=""; public string Due="",DueTime="",PcReminderKey="",PhoneReminderKey=""; public int ReminderMinutes; public bool Reminder=true; public bool Done; }
 public class CalEvent { public string Title,Start,End,Link,Calendar,Color; public bool AllDay; public string Id="",CalendarId="",Description="",Location="",ETag="",RecurringId="",EventType="default"; public bool Writable,Local,HasAttendees; }
 public class State {
  public WidgetLayout WidgetLayout=new WidgetLayout();
  public string QuoteCycleStart="";
  public List<Todo> Tasks=new List<Todo>(); public List<CalEvent> Events=new List<CalEvent>(); public List<CalEvent> LocalEvents=new List<CalEvent>(); public List<CalendarInfo> Calendars=new List<CalendarInfo>();
  public double Left=100,Top=80,Width=1100,Height=800,Opacity=1; public bool Pinned,ShowDone;
  public string UpdateRepository="juragii/deskflow-windows"; public bool AutoUpdates=true; public bool AutoCalendarSync=true; public bool PcTaskAlerts=true,TelegramAlerts=false;
  public string GrantedScopes=""; public int WidgetEngineVersion=0; public bool WidgetAlwaysOnTop; public int LayoutVersion=0; public bool DesktopEnabled=true,WidgetLocked=true,WidgetDark=false,WidgetShowTasks=true;
  public double WidgetWidth=900,WidgetHeight=760; public bool WidgetAutoFit=true; public double WidgetOpacity=.9; public int WidgetX=int.MinValue,WidgetY=int.MinValue;
  public string ClientId="",ClientSecret="",LastSync="";
 }
 public class Store {
  public readonly string Dir; public State Data; public static JavaScriptSerializer Json=new JavaScriptSerializer { MaxJsonLength=16000000 };
  public Store(string dir) { Dir=dir; Directory.CreateDirectory(dir); Data=Load(); }
  State Load() {
   string p=Path.Combine(Dir,"data.json");
   if(!File.Exists(p))return new State();
   try {return Json.Deserialize<State>(File.ReadAllText(p,Encoding.UTF8)) ?? new State();}
   catch { if(File.Exists(p+".bak")) {try{return Json.Deserialize<State>(File.ReadAllText(p+".bak",Encoding.UTF8));}catch{}}
    throw new InvalidDataException("저장 파일을 읽지 못했습니다. data.json 및 .bak 파일을 확인해 주세요. 기존 데이터는 덮어쓰지 않았습니다."); }
  }
  public IEnumerable<CalEvent> CalendarEvents {get{return Data.Events.Concat(Data.LocalEvents);}}
  public void Save(){string p=Path.Combine(Dir,"data.json"),temp=p+".tmp";File.WriteAllText(temp,Json.Serialize(Data),Encoding.UTF8);if(File.Exists(p))File.Replace(temp,p,p+".bak");else File.Move(temp,p);}
  public string Token {get{string p=Path.Combine(Dir,"account.bin");return File.Exists(p)?Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(p),null,DataProtectionScope.CurrentUser)):"";}
   set {string p=Path.Combine(Dir,"account.bin");if(string.IsNullOrEmpty(value)){if(File.Exists(p))File.Delete(p);}else File.WriteAllBytes(p,ProtectedData.Protect(Encoding.UTF8.GetBytes(value),null,DataProtectionScope.CurrentUser));}}
 }
 public partial class GoogleCalendar {
  Store store; HttpClient http; string access=""; DateTime expires=DateTime.MinValue;
  public GoogleCalendar(Store s,HttpClient client=null){store=s;http=client??new HttpClient { Timeout=TimeSpan.FromSeconds(30) };ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;}
  public static string Enc(string s){return Uri.EscapeDataString(s);}
  public static Dictionary<string,object> Obj(object o){return (Dictionary<string,object>)o;}
  public static string Str(Dictionary<string,object> d,string k){return d.ContainsKey(k)&&d[k]!=null?Convert.ToString(d[k],CultureInfo.InvariantCulture):"";}
  static string Random(){byte[] b=new byte[32];using(var r=RandomNumberGenerator.Create())r.GetBytes(b);return B64(b);}
  static string B64(byte[] b){return Convert.ToBase64String(b).TrimEnd('=').Replace('+','-').Replace('/','_');}
  public bool Connected {get{return !string.IsNullOrEmpty(store.Token);}}
  public void Import(string path){var root=Obj(Store.Json.DeserializeObject(File.ReadAllText(path)));if(!root.ContainsKey("installed"))throw new Exception("‘데스크톱 앱’ 유형의 OAuth JSON 파일을 선택해 주세요.");var d=Obj(root["installed"]);var id=Str(d,"client_id");if(!id.EndsWith(".apps.googleusercontent.com"))throw new Exception("올바른 Google OAuth 클라이언트가 아닙니다.");if(store.Data.ClientId!=id){Disconnect();}store.Data.ClientId=id;store.Data.ClientSecret=Str(d,"client_secret");store.Save();}
  OAuthLoopback receiver;
  public bool SubmitCallback(string url){var active=receiver;return active!=null&&active.SubmitUrl(url);}
  void Log(string stage){try{File.AppendAllText(Path.Combine(store.Dir,"auth-log.txt"),DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" "+stage+Environment.NewLine,Encoding.UTF8);}catch{}}
  public async Task Connect(CancellationToken cancel,Action<string> progress=null) {
   if(string.IsNullOrEmpty(store.Data.ClientId))throw new Exception("먼저 데스크톱 앱 인증 JSON 파일을 불러와 주세요.");
   Action<string> report=stage=>{Log(stage);if(progress!=null)progress(stage);};
   string state=Random(),verifier=Random(),challenge;using(var hash=SHA256.Create())challenge=B64(hash.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
   using(var local=new OAuthLoopback(state,cancel,report)) {
    receiver=local;
    try {
     string redirect=local.Redirect;
     string url="https://accounts.google.com/o/oauth2/v2/auth?client_id="+Enc(store.Data.ClientId)+"&redirect_uri="+Enc(redirect)+"&response_type=code&scope="+Enc(RequestedScopes)+"&access_type=offline&prompt=consent&state="+state+"&code_challenge="+challenge+"&code_challenge_method=S256";
     Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
     string code=await local.Completion;
     cancel.ThrowIfCancellationRequested();report("Google 인증 토큰 교환 중…");
     var fields=Fields("code",code,"client_id",store.Data.ClientId,"client_secret",store.Data.ClientSecret,"redirect_uri",redirect,"grant_type","authorization_code","code_verifier",verifier);
     var tokens=await Post("https://oauth2.googleapis.com/token",fields);
     cancel.ThrowIfCancellationRequested();
     if(string.IsNullOrEmpty(Str(tokens,"refresh_token")))throw new Exception("자동 연결 토큰을 받지 못했습니다. Google 연결을 다시 시도해 주세요.");
     store.Token=Str(tokens,"refresh_token");store.Data.GrantedScopes=Str(tokens,"scope");if(store.Data.GrantedScopes=="")store.Data.GrantedScopes=RequestedScopes;store.Save();Apply(tokens);report("Google 연결 완료 · 일정 조회 준비 중");
    }catch(OperationCanceledException){Log("로그인 취소");throw;}
    catch(Exception e){Log("로그인 실패: "+e.GetType().Name);throw;}
    finally{receiver=null;}
   }
  }
  static Dictionary<string,string> Fields(params string[] p){var d=new Dictionary<string,string>();for(int i=0;i<p.Length;i+=2)d.Add(p[i],p[i+1]);return d;}
  async Task<Dictionary<string,object>> Post(string url,Dictionary<string,string> fields){using(var response=await http.PostAsync(url,new FormUrlEncodedContent(fields))){string body=await response.Content.ReadAsStringAsync();var d=Obj(Store.Json.DeserializeObject(body));if(!response.IsSuccessStatusCode)throw new Exception("Google 인증 오류 ("+Str(d,"error")+"). API 활성화, 테스트 사용자 및 인증 파일을 확인해 주세요.");return d;}}
  void Apply(Dictionary<string,object> d){access=Str(d,"access_token");expires=DateTime.UtcNow.AddSeconds(Convert.ToDouble(d["expires_in"])-60);}
  async Task<string> Access(){if(DateTime.UtcNow<expires&&!string.IsNullOrEmpty(access))return access;string refresh=store.Token;if(string.IsNullOrEmpty(refresh))throw new Exception("Google 캘린더를 먼저 연결해 주세요.");Apply(await Post("https://oauth2.googleapis.com/token",Fields("refresh_token",refresh,"client_id",store.Data.ClientId,"client_secret",store.Data.ClientSecret,"grant_type","refresh_token")));return access;}
  async Task<Dictionary<string,object>> Get(string url){string token=await Access();using(var req=new HttpRequestMessage(HttpMethod.Get,url)){req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",token);using(var res=await http.SendAsync(req)){if(res.StatusCode==HttpStatusCode.Unauthorized){expires=DateTime.MinValue;throw new Exception("Google 인증이 만료되었습니다. 설정에서 다시 연결해 주세요.");}if(!res.IsSuccessStatusCode)throw new Exception("Google 일정 조회 실패 ("+(int)res.StatusCode+"). 인터넷 연결과 Calendar API 활성화 여부를 확인해 주세요.");return Obj(Store.Json.DeserializeObject(await res.Content.ReadAsStringAsync()));}}}
  public async Task<List<CalEvent>> Fetch(DateTime month){
   var calendars=await ListCalendars();string page="";
   var result=new List<CalEvent>();DateTime start=new DateTime(month.Year,month.Month,1).AddDays(-7),end=new DateTime(month.Year,month.Month,1).AddMonths(1).AddDays(14);
   foreach(var cal in calendars.Where(c=>c.Selected)) {page="";do{
    string url="https://www.googleapis.com/calendar/v3/calendars/"+Enc(cal.Id)+"/events?singleEvents=true&orderBy=startTime&maxResults=2500&timeMin="+Enc(new DateTimeOffset(start).ToString("o"))+"&timeMax="+Enc(new DateTimeOffset(end).ToString("o"))+(page!=""?"&pageToken="+Enc(page):"");
    var root=await Get(url);if(root.ContainsKey("items"))foreach(object item in (object[])root["items"]){var e=Obj(item);if(Str(e,"status")=="cancelled"||!e.ContainsKey("start")||!e.ContainsKey("end"))continue;result.Add(MapEvent(e,cal));}page=Str(root,"nextPageToken");
   }while(page!="");}return result;
  }
  public void Disconnect(){store.Token="";access="";expires=DateTime.MinValue;store.Data.Events.Clear();store.Data.Calendars.Clear();store.Data.GrantedScopes="";store.Data.LastSync="";store.Save();}
  public static DateTime Start(CalEvent e){return e.AllDay?DateTime.ParseExact(e.Start,"yyyy-MM-dd",CultureInfo.InvariantCulture):DateTimeOffset.Parse(e.Start,CultureInfo.InvariantCulture).LocalDateTime;}
  public static DateTime End(CalEvent e){return e.AllDay?DateTime.ParseExact(e.End,"yyyy-MM-dd",CultureInfo.InvariantCulture):DateTimeOffset.Parse(e.End,CultureInfo.InvariantCulture).LocalDateTime;}
  public static bool OnDay(CalEvent e,DateTime day){return Start(e)<day.Date.AddDays(1)&&End(e)>day.Date;}
 }
}
