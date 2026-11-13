using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
namespace DeskFlow {
 partial class Widget {
  sealed class RecurrenceServer:HttpMessageHandler {
   public string Deleted="",Tag="";
   protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancel){
    string path=request.RequestUri.AbsolutePath,json="{}";
    if(path.Contains("/token"))json="{\"access_token\":\"mock\",\"expires_in\":3600}";
    else if(request.Method==HttpMethod.Delete){Deleted=path.Split('/').Last();Tag=request.Headers.GetValues("If-Match").First();return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent));}
    else if(path.Contains("calendarList"))json="{\"items\":[{\"id\":\"test\",\"primary\":true,\"accessRole\":\"owner\",\"summary\":\"Test\"}]}";
    else {bool parent=path.EndsWith("/series");json="{\"id\":\""+(parent?"series":"instance")+"\",\"summary\":\"매주 프로젝트 회의\",\"etag\":\""+(parent?"parent-version":"instance-version")+"\","+(parent?"":"\"recurringEventId\":\"series\",")+"\"start\":{\"dateTime\":\"2026-11-13T14:00:00+09:00\"},\"end\":{\"dateTime\":\"2026-11-13T15:00:00+09:00\"}}";}
    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)});
   }
  }
  internal void VerifyRecurringDeletion(string root){
   var savedGoogle=google;var oldScopes=store.Data.GrantedScopes;var oldCalendars=store.Data.Calendars;
   store.Token="test-only-recurrence";store.Data.GrantedScopes=GoogleCalendar.RequestedScopes;store.Data.Calendars=new System.Collections.Generic.List<CalendarInfo>{new CalendarInfo{Id="test",Title="Test",Role="owner"}};
   try{foreach(bool whole in new[]{false,true}){
    var server=new RecurrenceServer();google=new GoogleCalendar(store,new HttpClient(server));
    var instance=new CalEvent{Id="instance",CalendarId="test",Title="매주 프로젝트 회의",Writable=true,ETag="instance-version",RecurringId="series",Start="2026-11-13T14:00:00+09:00",End="2026-11-13T15:00:00+09:00"};Exception failure=null;
    Window.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(async()=>{
     var dialog=Application.Current.Windows.Cast<Window>().FirstOrDefault(x=>x.Owner==Window);
     Func<string,object> named=n=>LogicalTreeHelper.FindLogicalNode(dialog,n);
     try{
      var delete=(Button)named("EventDelete");for(int wait=0;!delete.IsEnabled&&wait<100;wait++)await Task.Delay(10);delete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
      var single=(RadioButton)named("EventDeleteSingle");var series=(RadioButton)named("EventDeleteSeries");
      if(single.IsChecked!=true||((StackPanel)named("EventDeleteScope")).Visibility!=Visibility.Visible||server.Deleted!="")throw new Exception("Recurring deletion must ask scope before making a request: single="+single.IsChecked+" scope="+((StackPanel)named("EventDeleteScope")).Visibility+" enabled="+delete.IsEnabled+" deleted="+server.Deleted);
      if(whole){series.IsChecked=true;for(int wait=0;!((Button)named("EventDeleteConfirm")).IsEnabled&&wait<100;wait++)await Task.Delay(10);}
      var confirm=(Button)named("EventDeleteConfirm");if(!confirm.IsEnabled)throw new Exception("Valid deletion scope did not enable confirmation");
      if(whole)Tests.SaveRender(dialog,Path.Combine(root,"recurrence-delete-preview.png"));
      confirm.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
      if(server.Deleted!=(whole?"series":"instance")||server.Tag!=(whole?"parent-version":"instance-version"))throw new Exception("Wrong deletion target or version for selected scope");
     }catch(Exception e){failure=e;}finally{if(dialog!=null&&dialog.IsVisible)dialog.Close();}
    }));
    EditEvent(instance);if(failure!=null)throw failure;
   }
   var parent=new CalEvent{Id="series",CalendarId="test"};
   if(!GoogleCalendar.DeletedBy(new CalEvent{Id="instance",RecurringId="series",CalendarId="test"},parent,true)||GoogleCalendar.DeletedBy(new CalEvent{Id="instance",RecurringId="series",CalendarId="other"},parent,true)||GoogleCalendar.DeletedBy(new CalEvent{Id="unrelated",CalendarId="test"},parent,true))throw new Exception("Series cache removal affected unrelated events");
   File.WriteAllText(Path.Combine(root,"recurrence-result.txt"),"PASS: recurring deletion offers instance/whole-series choices, defaults to instance, no request before confirmation, whole-series fetches parent version, selected target and If-Match verified through mock HTTP, unrelated calendars/events preserved by cache filter. No live Google deletion performed.");
   }finally{store.Token="";google=savedGoogle;store.Data.GrantedScopes=oldScopes;store.Data.Calendars=oldCalendars;store.Data.Events.Clear();store.Data.LastSync="";RenderCalendar();}
  }
 }
}
