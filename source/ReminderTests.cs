using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
namespace DeskFlow {
 static class ReminderTests {
  const string Token="123456789:TEST_ONLY_BOT_TOKEN_1234567890";
  static void Check(bool ok,string label){if(!ok)throw new Exception("Reminders: "+label);}
  sealed class Server:HttpMessageHandler {
   public string Body="",Method="";public bool Fail;
   protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel){
    Check(request.RequestUri.Host=="api.telegram.org","Telegram origin");Method=request.RequestUri.AbsolutePath.Split('/').Last();Body=await request.Content.ReadAsStringAsync();
    if(Fail)throw new HttpRequestException("Secret URL "+request.RequestUri);
    string json=Method=="getUpdates"?"{\"ok\":true,\"result\":[{\"message\":{\"chat\":{\"id\":123,\"type\":\"private\",\"first_name\":\"본인\"}}},{\"message\":{\"chat\":{\"id\":-456,\"type\":\"group\"}}}]}":"{\"ok\":true,\"result\":{}}";
    return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)};
   }
  }
  public static async Task Run(string root){
   var dir=Path.Combine(root,"reminders-"+Guid.NewGuid().ToString("N"));var store=new Store(dir);DateTime now=new DateTime(2026,11,13,18,0,0);
   var todo=new Todo{Title="검증 할 일",Due="2026-11-13",DueTime="18:10",ReminderMinutes=10};store.Data.Tasks.Add(todo);store.Data.Tasks.Add(new Todo{Title="날짜만",Due="2020-01-01"});
   Check(!TodoDeadline.Ready(todo,now.AddSeconds(-1))&&TodoDeadline.Ready(todo,now),"exact pre-deadline trigger");Check(!TodoDeadline.Ready(store.Data.Tasks[1],now),"legacy date-only does not alert");
   foreach(int days in new[]{1,2,3}){var early=new Todo{Title="일 단위 경계",Due="2026-12-01",DueTime="18:37",ReminderMinutes=days*1440};var trigger=new DateTime(2026,12,1,18,37,0).AddDays(-days);Check(!TodoDeadline.Ready(early,trigger.AddSeconds(-1))&&TodoDeadline.Ready(early,trigger),"Day offset crosses month correctly: "+days);store.Data.Tasks.Add(early);store.Save();Check(new Store(dir).Data.Tasks.Last().ReminderMinutes==days*1440,"Day offset roundtrip");store.Data.Tasks.Remove(early);}
   int pc=0,mobile=0;bool failure=true;var engine=new ReminderEngine(store,items=>{pc+=items.Count;return Task.FromResult(0);},item=>{mobile++;if(failure)throw new IOException("offline");return Task.FromResult(0);},()=>true);
   await engine.Check(now);Check(pc==1&&mobile==1&&todo.PcReminderKey!=""&&todo.PhoneReminderKey=="","independent delivery and phone failure");await engine.Check(now.AddSeconds(30));Check(pc==1&&mobile==1,"duplicate prevention and retry delay");failure=false;await engine.Check(now.AddMinutes(1));Check(mobile==2&&todo.PhoneReminderKey==TodoDeadline.Key(todo),"phone retry succeeds");
   var loaded=new Store(dir);int restarted=0;var restartedEngine=new ReminderEngine(loaded,items=>{restarted++;return Task.FromResult(0);},item=>{restarted++;return Task.FromResult(0);},()=>true);await restartedEngine.Check(now.AddMinutes(2));Check(restarted==0,"delivery dedup persists across restart");
   todo.Done=true;todo.DueTime="18:11";await engine.Check(now.AddMinutes(2));Check(pc==1&&mobile==2,"completed tasks suppressed");todo.Done=false;await engine.Check(now.AddMinutes(2));Check(pc==2&&mobile==3,"rescheduled deadline alerts once");
   todo.Reminder=false;todo.DueTime="18:12";await engine.Check(now.AddMinutes(3));Check(pc==2,"per-task alert off");
   var account=new PhoneAccount{Token=Token,ChatId="123"};PhoneCredentials.Save(dir,account);Check(PhoneCredentials.Load(dir).Token==Token&&!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(dir,"phone-alerts.bin"))).Contains(Token),"DPAPI encryption");
   var server=new Server();var client=new TelegramAlerts(new HttpClient(server));var chats=await client.Chats(Token);Check(chats.Count==1&&chats[0].Id=="123","private candidates only, explicit selection required");await client.Send(account,"마감 18:10");Check(server.Method=="sendMessage"&&server.Body.Contains("chat_id=123"),"Telegram request routing");server.Fail=true;bool sanitized=false;try{await client.Send(account,"test");}catch(Exception ex){sanitized=!ex.ToString().Contains(Token);}Check(sanitized,"no bot token in errors");PhoneCredentials.Clear(dir);Check(PhoneCredentials.Load(dir).Token=="","credentials clear");
   File.WriteAllText(Path.Combine(root,"reminder-result.txt"),"PASS: minute-precision deadline/pre-alert, legacy date-only tasks, completed/off suppression, reschedule, PC/mobile independent success, retry backoff, restart dedup, DPAPI, Telegram personal-chat filtering/request target, sanitized network errors. No live PC notification or Telegram message was sent.");
  }
 }
}
