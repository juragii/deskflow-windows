using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DeskFlow {
 public class CalendarInfo {public string Id="",Title="",Color="#3182F6",Role="",TimeZone="";public bool Selected,Primary;public bool Writable {get{return Role=="writer"||Role=="owner";}}public override string ToString(){return Title;}}
 public class EventDraft {
  public string Id=Guid.NewGuid().ToString("N"),Title="",Description="",Location="";public bool AllDay;
  public DateTime Start,End; // End is exclusive for both timed and all-day events.
  public void Validate(){if(string.IsNullOrWhiteSpace(Title)||Title.Trim().Length>200)throw new ArgumentException("일정 제목을 1~200자로 입력해 주세요.");if(End<=Start)throw new ArgumentException("종료는 시작보다 뒤여야 합니다.");if(Description.Length>10000||Location.Length>1000)throw new ArgumentException("메모는 10,000자, 장소는 1,000자 이내로 입력해 주세요.");if(!AllDay&&(TimeZoneInfo.Local.IsInvalidTime(Start)||TimeZoneInfo.Local.IsInvalidTime(End)))throw new ArgumentException("현재 시간대에 존재하지 않는 시간입니다. 다른 시간을 선택해 주세요.");}
  public Dictionary<string,object> Body(bool patch){Validate();var start=new Dictionary<string,object>();var end=new Dictionary<string,object>();
   if(AllDay){start["date"]=Start.ToString("yyyy-MM-dd");end["date"]=End.ToString("yyyy-MM-dd");if(patch){start["dateTime"]=null;start["timeZone"]=null;end["dateTime"]=null;end["timeZone"]=null;}}
   else {start["dateTime"]=new DateTimeOffset(DateTime.SpecifyKind(Start,DateTimeKind.Unspecified)).ToString("o");end["dateTime"]=new DateTimeOffset(DateTime.SpecifyKind(End,DateTimeKind.Unspecified)).ToString("o");if(patch){start["date"]=null;end["date"]=null;}}
   var body=new Dictionary<string,object>{{"summary",Title.Trim()},{"description",Description},{"location",Location},{"start",start},{"end",end}};if(!patch)body["id"]=Id;return body;
  }
  public CalEvent LocalEvent(){var body=Body(false);return new CalEvent{Id=Id,CalendarId="__local",Calendar="이 PC",Color="#8B95A1",Title=Title.Trim(),Description=Description,Location=Location,AllDay=AllDay,Local=true,Writable=true,Start=GoogleCalendar.Str(GoogleCalendar.Obj(body["start"]),AllDay?"date":"dateTime"),End=GoogleCalendar.Str(GoogleCalendar.Obj(body["end"]),AllDay?"date":"dateTime")};}
 }
 public partial class GoogleCalendar {
  public const string RequestedScopes="https://www.googleapis.com/auth/calendar.events https://www.googleapis.com/auth/calendar.calendarlist.readonly";
  public bool CanWrite {get{return Connected&&(store.Data.GrantedScopes??"").Split(' ').Any(s=>s=="https://www.googleapis.com/auth/calendar.events"||s=="https://www.googleapis.com/auth/calendar");}}
  public async Task<List<CalendarInfo>> ListCalendars(){var result=new List<CalendarInfo>();string page="";do{
   var root=await Get("https://www.googleapis.com/calendar/v3/users/me/calendarList?maxResults=250"+(page!=""?"&pageToken="+Enc(page):""));
   if(root.ContainsKey("items"))foreach(object item in (object[])root["items"]){var c=Obj(item);bool primary=c.ContainsKey("primary")&&Convert.ToBoolean(c["primary"]);result.Add(new CalendarInfo{Id=Str(c,"id"),Title=Str(c,"summary"),Color=Str(c,"backgroundColor")==""?"#3182F6":Str(c,"backgroundColor"),Role=Str(c,"accessRole"),TimeZone=Str(c,"timeZone"),Primary=primary,Selected=primary||(c.ContainsKey("selected")&&Convert.ToBoolean(c["selected"]))});}page=Str(root,"nextPageToken");
   }while(page!="");store.Data.Calendars=result;return result;
  }
  static bool Flag(Dictionary<string,object> d,string key){return d.ContainsKey(key)&&d[key]!=null&&Convert.ToBoolean(d[key]);}
  public static CalEvent MapEvent(Dictionary<string,object> e,CalendarInfo calendar){var s=Obj(e["start"]);var t=Obj(e["end"]);bool all=s.ContainsKey("date")&&s["date"]!=null;bool attendees=e.ContainsKey("attendees")&&e["attendees"] is object[]&&((object[])e["attendees"]).Length>0;bool organiserSelf=!e.ContainsKey("organizer")||Flag(Obj(e["organizer"]),"self");string type=Str(e,"eventType");return new CalEvent{Id=Str(e,"id"),CalendarId=calendar.Id,Calendar=calendar.Title,Color=calendar.Color,Title=Str(e,"summary")==""?"(제목 없음)":Str(e,"summary"),Start=Str(s,all?"date":"dateTime"),End=Str(t,all?"date":"dateTime"),AllDay=all,Link=Str(e,"htmlLink"),Description=Str(e,"description"),Location=Str(e,"location"),ETag=Str(e,"etag"),RecurringId=Str(e,"recurringEventId"),EventType=type,HasAttendees=attendees,Writable=calendar.Writable&&!Flag(e,"locked")&&(type==""||type=="default")&&(!attendees||organiserSelf||Flag(e,"guestsCanModify"))};}
  public async Task<CalEvent> ReadEvent(string calendar,string id){var c=store.Data.Calendars.FirstOrDefault(x=>x.Id==calendar);if(c==null){await ListCalendars();c=store.Data.Calendars.FirstOrDefault(x=>x.Id==calendar);}if(c==null)throw new Exception("캘린더를 찾지 못했습니다. 동기화 후 다시 시도해 주세요.");return MapEvent(await Get("https://www.googleapis.com/calendar/v3/calendars/"+Enc(calendar)+"/events/"+Enc(id)),c);}
  public async Task DeleteEvent(CalEvent item){
   if(item==null||item.Local||!CanWrite||!item.Writable||string.IsNullOrEmpty(item.CalendarId)||string.IsNullOrEmpty(item.Id)||string.IsNullOrEmpty(item.ETag))throw new Exception("이 일정을 삭제할 권한 또는 최신 버전 정보가 없습니다. 일정을 다시 열거나 Google에 다시 로그인해 주세요.");
   using(var request=new HttpRequestMessage(HttpMethod.Delete,"https://www.googleapis.com/calendar/v3/calendars/"+Enc(item.CalendarId)+"/events/"+Enc(item.Id)+"?sendUpdates=all")){
    request.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",await Access());request.Headers.TryAddWithoutValidation("If-Match",item.ETag);
    using(var response=await http.SendAsync(request)){
     if(response.IsSuccessStatusCode||response.StatusCode==HttpStatusCode.NotFound||response.StatusCode==HttpStatusCode.Gone)return;
     if((int)response.StatusCode==412)throw new Exception("다른 곳에서 일정이 변경되었습니다. 삭제하지 않았습니다. 창을 닫고 다시 열어 확인해 주세요.");
     if(response.StatusCode==HttpStatusCode.Unauthorized){expires=DateTime.MinValue;throw new Exception("Google 인증이 만료되었습니다. 다시 로그인해 주세요.");}
     if(response.StatusCode==HttpStatusCode.Forbidden)throw new Exception("Google에서 삭제를 거부했습니다. 일정 쓰기 권한을 확인해 주세요.");
     throw new Exception("일정 삭제 실패 ("+(int)response.StatusCode+"). 연결 상태를 확인하고 다시 시도해 주세요.");
    }
   }
  }
  public async Task<CalEvent> SaveEvent(CalendarInfo calendar,EventDraft draft,CalEvent original=null){
   draft.Validate();if(!CanWrite)throw new Exception("일정 쓰기 권한이 필요합니다. 설정에서 Google 로그인을 다시 진행해 주세요.");if(!calendar.Writable)throw new Exception("이 캘린더는 읽기 전용입니다.");if(original!=null&&(!original.Writable||string.IsNullOrEmpty(original.ETag)))throw new Exception("이 일정은 편집할 수 없거나 버전 정보가 없습니다. 일정을 다시 열어 주세요.");
   string endpoint="https://www.googleapis.com/calendar/v3/calendars/"+Enc(calendar.Id)+"/events";if(original!=null)endpoint+="/"+Enc(original.Id);endpoint+="?sendUpdates=all";
   using(var req=new HttpRequestMessage(original==null?HttpMethod.Post:new HttpMethod("PATCH"),endpoint)){
    req.Headers.Authorization=new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer",await Access());if(original!=null)req.Headers.TryAddWithoutValidation("If-Match",original.ETag);
    req.Content=new StringContent(Store.Json.Serialize(draft.Body(original!=null)),Encoding.UTF8,"application/json");
    using(var response=await http.SendAsync(req)){
     if(response.StatusCode==HttpStatusCode.Conflict&&original==null){var existing=await ReadEvent(calendar.Id,draft.Id);if(Matches(existing,draft))return existing;throw new Exception("같은 식별자의 일정이 이미 있습니다. 창을 다시 열어 주세요.");}
     if((int)response.StatusCode==412){var latest=await ReadEvent(calendar.Id,original.Id);if(Matches(latest,draft))return latest;throw new Exception("다른 곳에서 이 일정이 변경되었습니다. 창을 닫고 일정을 다시 열어 주세요. 변경 내용을 덮어쓰지 않았습니다.");}
     if(response.StatusCode==HttpStatusCode.Forbidden)throw new Exception("Google에서 저장을 거부했습니다. 쓰기 권한을 승인했는지, 편집 가능한 캘린더인지 확인해 주세요.");
     if(response.StatusCode==HttpStatusCode.Unauthorized){expires=DateTime.MinValue;throw new Exception("Google 인증이 만료되었습니다. 다시 로그인해 주세요.");}
     if(!response.IsSuccessStatusCode)throw new Exception("일정 저장 실패 ("+(int)response.StatusCode+"). 입력 값과 연결 상태를 확인해 주세요.");
     return MapEvent(Obj(Store.Json.DeserializeObject(await response.Content.ReadAsStringAsync())),calendar);
    }
   }
  }
  static bool Matches(CalEvent item,EventDraft draft){return item.Title==draft.Title.Trim()&&item.Description==draft.Description&&item.Location==draft.Location&&item.AllDay==draft.AllDay&&Start(item)==draft.Start&&End(item)==draft.End;}
 }
}
