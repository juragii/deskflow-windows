using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
namespace DeskFlow {
 public sealed class PhoneAccount {public string Token="",ChatId="";}
 public static class PhoneCredentials {
  public static PhoneAccount Load(string dir){var path=Path.Combine(dir,"phone-alerts.bin");if(!File.Exists(path))return new PhoneAccount();try{return Store.Json.Deserialize<PhoneAccount>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),null,DataProtectionScope.CurrentUser)));}catch{throw new IOException("휴대폰 알림 인증 정보를 읽지 못했습니다. 다시 연결해 주세요.");}}
  public static void Save(string dir,PhoneAccount account){var path=Path.Combine(dir,"phone-alerts.bin");byte[] bytes=ProtectedData.Protect(Encoding.UTF8.GetBytes(Store.Json.Serialize(account)),null,DataProtectionScope.CurrentUser);File.WriteAllBytes(path+".tmp",bytes);if(File.Exists(path))File.Replace(path+".tmp",path,null);else File.Move(path+".tmp",path);}
  public static void Clear(string dir){var path=Path.Combine(dir,"phone-alerts.bin");if(File.Exists(path))File.Delete(path);}
 }
 public sealed class PhoneChat {public string Id,Label;public override string ToString(){return Label+" · "+Id;}}
 public sealed class TelegramAlerts {
  static readonly HttpClient Shared=new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromSeconds(15)};readonly HttpClient http;
  public TelegramAlerts(HttpClient client=null){http=client??Shared;}
  static void Validate(string token){if(!Regex.IsMatch(token??"","^[0-9]{5,}:[A-Za-z0-9_-]{20,}$"))throw new IOException("BotFather가 발급한 올바른 봇 토큰을 입력해 주세요.");}
  async Task<Dictionary<string,object>> Call(string token,string method,Dictionary<string,string> fields){Validate(token);try{using(var response=await http.PostAsync("https://api.telegram.org/bot"+token+"/"+method,new FormUrlEncodedContent(fields))){if(!response.IsSuccessStatusCode)throw new IOException();var body=GoogleCalendar.Obj(Store.Json.DeserializeObject(await response.Content.ReadAsStringAsync()));if(!body.ContainsKey("ok")||!Convert.ToBoolean(body["ok"]))throw new IOException();return body;}}catch{throw new IOException("텔레그램 연결에 실패했습니다. 토큰·봇 대화 시작·인터넷 연결을 확인해 주세요.");}}
  public async Task Send(PhoneAccount account,string message){if(account==null||!Regex.IsMatch(account.ChatId??"","^[1-9][0-9]*$"))throw new IOException("개인 대화를 먼저 선택하고 저장해 주세요.");await Call(account.Token,"sendMessage",new Dictionary<string,string>{{"chat_id",account.ChatId},{"text",message}});}
  public async Task<List<PhoneChat>> Chats(string token){var body=await Call(token,"getUpdates",new Dictionary<string,string>{{"timeout","0"},{"limit","100"}});var chats=new Dictionary<string,PhoneChat>();if(body.ContainsKey("result"))foreach(object item in (object[])body["result"]){var update=GoogleCalendar.Obj(item);if(!update.ContainsKey("message"))continue;var message=GoogleCalendar.Obj(update["message"]);if(!message.ContainsKey("chat"))continue;var chat=GoogleCalendar.Obj(message["chat"]);string id=GoogleCalendar.Str(chat,"id");if(GoogleCalendar.Str(chat,"type")!="private"||!Regex.IsMatch(id,"^[1-9][0-9]*$"))continue;chats[id]=new PhoneChat{Id=id,Label=(GoogleCalendar.Str(chat,"first_name")+" "+GoogleCalendar.Str(chat,"last_name")+" @"+GoogleCalendar.Str(chat,"username")).Trim()};}return chats.Values.ToList();}
 }
 partial class Widget {
  void PhoneSettings(StackPanel p){
   var heading=Text("휴대폰 알림 · 텔레그램",16);heading.Margin=new Thickness(0,18,0,8);p.Children.Add(heading);
   p.Children.Add(Text("① Telegram의 @BotFather에서 /newbot으로 봇을 만듭니다.\n② 휴대폰에서 만든 봇과 대화를 열고 /start를 보냅니다.\n③ 아래에 토큰을 입력하고 ‘내 대화 찾기’에서 본인 대화를 선택합니다. 토큰은 채팅에 올리지 마세요.",11));
   p.Children.Add(Btn("BotFather 열기 ↗",()=>Open("https://t.me/BotFather")));
   var token=new PasswordBox{Name="PhoneBotToken",Margin=new Thickness(0,8,0,8),Padding=new Thickness(10),ToolTip="봇 토큰 · 암호화 저장 후 입력창은 비워집니다"};p.Children.Add(token);
   int tokenGeneration=0;string candidateToken="";var chats=new ComboBox{Name="PhoneChat",MinHeight=36,Margin=new Thickness(0,8,0,8),ToolTip="알림을 받을 본인 개인 대화를 직접 선택"};p.Children.Add(chats);token.PasswordChanged+=(s,e)=>{tokenGeneration++;candidateToken="";chats.Items.Clear();};
   var label=Text(store.Data.TelegramAlerts?"휴대폰 알림 켜짐":"휴대폰 알림 꺼짐 · 연결 설정이 필요합니다.",11);p.Children.Add(label);
   var find=Btn("내 대화 찾기",()=>{});find.Click+=async(s,e)=>{find.IsEnabled=false;int generation=tokenGeneration;try{string key=token.Password.Trim();if(key=="")key=PhoneCredentials.Load(store.Dir).Token;var found=await new TelegramAlerts().Chats(key);if(generation!=tokenGeneration){label.Text="토큰이 변경되었습니다. 다시 대화를 찾아 주세요.";return;}candidateToken=key;chats.Items.Clear();foreach(var chat in found)chats.Items.Add(chat);label.Text=found.Count==0?"휴대폰에서 봇에 /start를 보낸 뒤 다시 찾아 주세요.":"목록에서 본인 대화를 선택하고 연결을 저장해 주세요.";}catch(Exception ex){label.Text=ex.Message;}finally{find.IsEnabled=true;}};p.Children.Add(find);
   p.Children.Add(Btn("선택한 대화에 연결 · 알림 켜기",()=>{try{var chosen=chats.SelectedItem as PhoneChat;if(chosen==null){label.Text="대화를 찾아 본인 대화를 선택해 주세요.";return;}string key=candidateToken;if(key=="")throw new IOException("토큰을 확인하고 대화를 다시 찾아 주세요.");PhoneCredentials.Save(store.Dir,new PhoneAccount{Token=key,ChatId=chosen.Id});store.Data.TelegramAlerts=true;SafeSave();token.Clear();label.Text="연결 저장 완료 · 미완료 할 일의 알림을 이 대화로 보냅니다.";}catch(Exception ex){label.Text=ex.Message;}}));
   var test=Btn("휴대폰 테스트 알림 보내기",()=>{});test.Click+=async(s,e)=>{test.IsEnabled=false;try{await new TelegramAlerts().Send(PhoneCredentials.Load(store.Dir),"나날 테스트 알림입니다. 이 대화로 할 일 마감 알림을 받습니다.");label.Text="텔레그램이 메시지를 수락했습니다. 휴대폰 알림 권한과 대화 알림 설정을 확인해 주세요.";}catch(Exception ex){label.Text=ex.Message;}finally{test.IsEnabled=true;}};p.Children.Add(test);
   p.Children.Add(Btn("휴대폰 알림 끄기 · 인증 삭제",()=>{try{PhoneCredentials.Clear(store.Dir);tokenGeneration++;candidateToken="";chats.Items.Clear();store.Data.TelegramAlerts=false;SafeSave();token.Clear();label.Text="휴대폰 알림을 끄고 저장한 인증을 삭제했습니다.";}catch(Exception ex){label.Text=ex.Message;}}));
   p.Children.Add(Text("PC가 꺼져 있거나 절전 중이면 휴대폰 전송도 지연됩니다. 토큰과 대화 ID는 이 Windows 계정으로 암호화 저장합니다. 할 일 제목과 마감 시각은 Telegram으로 전송됩니다.",11,"#8B95A1"));
  }
 }
}
