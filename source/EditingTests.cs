using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DeskFlow {
 public static class EditingTests {
  static void Check(bool value,string name){if(!value)throw new Exception("Event editing: "+name);}
  sealed class Server:HttpMessageHandler {
   public int Writes;public string Method,IfMatch,Uri;public Dictionary<string,object> Body,Existing;public HttpStatusCode WriteStatus=HttpStatusCode.OK;
   protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel){
    string url=request.RequestUri.AbsoluteUri;if(url.Contains("oauth2.googleapis.com"))return Json(new Dictionary<string,object>{{"access_token","mock"},{"expires_in",3600}},HttpStatusCode.OK);
    Check(request.Headers.Authorization.Parameter=="mock","Bearer");
    if(request.Method==HttpMethod.Get)return Json(Existing,HttpStatusCode.OK);
    Writes++;Method=request.Method.Method;Uri=url;IEnumerable<string> tags;if(request.Headers.TryGetValues("If-Match",out tags))IfMatch=tags.First();Body=GoogleCalendar.Obj(Store.Json.DeserializeObject(await request.Content.ReadAsStringAsync()));
    if(WriteStatus!=HttpStatusCode.OK)return Json(new Dictionary<string,object>(),WriteStatus);
    var response=new Dictionary<string,object>(Body);response["id"]=Body.ContainsKey("id")?Body["id"]:"existing";response["etag"]="\"next-version\"";return Json(response,HttpStatusCode.OK);
   }
   static HttpResponseMessage Json(object body,HttpStatusCode status){return new HttpResponseMessage(status){Content=new StringContent(Store.Json.Serialize(body),Encoding.UTF8,"application/json")};}
  }
  public static async Task Run(string root){
   var store=new Store(Path.Combine(root,"editing-"+Guid.NewGuid().ToString("N")));store.Token="mock-refresh";store.Data.GrantedScopes=GoogleCalendar.RequestedScopes;
   var calendar=new CalendarInfo{Id="owner@example.com",Title="내 일정",Role="owner",Color="#3182F6",Selected=true};store.Data.Calendars.Add(calendar);var server=new Server();var google=new GoogleCalendar(store,new HttpClient(server));
   var draft=new EventDraft{Title="테스트 일정",Start=new DateTime(2026,11,13,9,0,0),End=new DateTime(2026,11,13,10,0,0),Description="메모",Location="회의실"};
   var created=await google.SaveEvent(calendar,draft);Check(server.Method=="POST"&&created.Id==draft.Id,"Create preserves client-generated ID");Check(server.Uri.Contains("owner%40example.com")&&server.Uri.Contains("sendUpdates=all"),"calendar target and updates");Check(created.Writable&&created.Title==draft.Title,"server mapping");
   var original=new CalEvent{Id="existing",CalendarId=calendar.Id,Writable=true,ETag="\"original-version\""};draft.AllDay=true;draft.Start=new DateTime(2026,11,13);draft.End=new DateTime(2026,11,15);var edited=await google.SaveEvent(calendar,draft,original);
   Check(server.Method=="PATCH"&&server.IfMatch=="\"original-version\"","Conditional partial update");Check(!server.Body.ContainsKey("attendees")&&!server.Body.ContainsKey("recurrence")&&!server.Body.ContainsKey("id"),"untouched event fields omitted");Check(GoogleCalendar.Obj(server.Body["start"])["dateTime"]==null,"timed-to-all-day removes dateTime");Check(edited.AllDay&&GoogleCalendar.OnDay(edited,new DateTime(2026,11,14))&&!GoogleCalendar.OnDay(edited,new DateTime(2026,11,15)),"exclusive all-day end");
   draft.AllDay=false;draft.Start=draft.Start.AddHours(9);draft.End=draft.Start.AddHours(1);await google.SaveEvent(calendar,draft,original);Check(GoogleCalendar.Obj(server.Body["start"])["date"]==null,"all-day-to-timed removes date");
   server.WriteStatus=(HttpStatusCode)412;server.Existing=new Dictionary<string,object>(draft.Body(false));server.Existing["id"]="existing";server.Existing["summary"]="他の変更";server.Existing["etag"]="\"newer\"";bool conflict=false;int before=server.Writes;try{await google.SaveEvent(calendar,draft,original);}catch(Exception e){conflict=e.Message.Contains("변경");}Check(conflict&&server.Writes==before+1,"conflict does not overwrite or retry");
   server.WriteStatus=HttpStatusCode.Conflict;server.Existing=new Dictionary<string,object>(draft.Body(false));server.Existing["etag"]="\"saved\"";var duplicate=await google.SaveEvent(calendar,draft);Check(duplicate.Id==draft.Id,"retry reconciles existing insert id");
   store.Data.GrantedScopes="https://www.googleapis.com/auth/calendar.readonly";bool denied=false;before=server.Writes;try{await google.SaveEvent(calendar,draft);}catch(Exception){denied=true;}Check(denied&&server.Writes==before,"old scope cannot write");
   var local=draft.LocalEvent();store.Data.LocalEvents.Add(local);store.Data.Events.Clear();store.Save();var loaded=new Store(store.Dir);Check(loaded.Data.LocalEvents.Count==1&&loaded.CalendarEvents.Count()==1,"local events survive cache refresh and persistence");
   File.WriteAllText(Path.Combine(root,"editing-result.txt"),"PASS: Google POST/PATCH requests via mock handler, calendar target, stable insert ID and duplicate reconciliation, If-Match conflict protection, event field preservation, all-day/timed conversion, exclusive end, read-only scope guard, local event persistence. Live Google writes were not performed.");
  }
 }
}
