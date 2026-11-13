using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
namespace DeskFlow {
 public static class LoopbackTests {
  class ScriptStream:Stream {
   byte[] input;int pos;bool idle;public MemoryStream Output=new MemoryStream();
   public ScriptStream(string request,bool blank=false){input=Encoding.ASCII.GetBytes(request);idle=blank;}
   public override Task<int> ReadAsync(byte[] b,int off,int count,CancellationToken ct){if(idle)return new TaskCompletionSource<int>().Task;int n=Math.Min(7,Math.Min(count,input.Length-pos));Array.Copy(input,pos,b,off,n);pos+=n;return Task.FromResult(n);}
   public override Task WriteAsync(byte[] b,int off,int count,CancellationToken ct){Output.Write(b,off,count);return Task.FromResult(0);}
   public override bool CanRead{get{return true;}}public override bool CanWrite{get{return true;}}public override bool CanSeek{get{return false;}}public override long Length{get{throw new NotSupportedException();}}public override long Position{get{return pos;}set{throw new NotSupportedException();}}
   public override void Flush(){}public override int Read(byte[] b,int o,int c){throw new NotSupportedException();}public override void Write(byte[] b,int o,int c){Output.Write(b,o,c);}public override long Seek(long o,SeekOrigin s){throw new NotSupportedException();}public override void SetLength(long v){throw new NotSupportedException();}
  }
  static string Packet(string path){return "GET "+path+" HTTP/1.1\r\nHost: 127.0.0.1\r\nConnection: close\r\n\r\n";}
  static async Task TestStreams(){using(var r=new OAuthLoopback("stream-state",CancellationToken.None)){
   var idle=new ScriptStream("",true);var waiting=r.Serve(idle);
   var favicon=new ScriptStream(Packet("/favicon.ico"));await r.Serve(favicon);Check(Encoding.UTF8.GetString(favicon.Output.ToArray()).Contains("400 Bad Request"),"favicon ignored");Check(!r.Completion.IsCompleted,"favicon cannot end login");
   var fragment=new ScriptStream(Packet("/?state=stream-state&code=stream-code"));var response=r.Serve(fragment);Check(await Task.WhenAny(response,Task.Delay(2000))==response,"idle preconnect cannot block fragmented request");await response;Check(await r.Completion=="stream-code","fragmented callback code");Check(Encoding.UTF8.GetString(fragment.Output.ToArray()).Contains("200 OK"),"fragmented response");Check(!waiting.IsCompleted,"idle test socket is still waiting");
  }}
  static void Check(bool ok,string message){if(!ok)throw new Exception("Loopback: "+message);}
  static async Task<string> Request(string redirect,string target,bool fragmented=false){
   var u=new Uri(redirect);using(var client=new TcpClient()){await client.ConnectAsync(u.Host,u.Port);using(var stream=client.GetStream()){
    string request="GET "+target+" HTTP/1.1\r\nHost: "+u.Authority+"\r\nConnection: close\r\n\r\n";byte[] bytes=Encoding.ASCII.GetBytes(request);
    if(fragmented){await stream.WriteAsync(bytes,0,7);await Task.Delay(40);await stream.WriteAsync(bytes,7,bytes.Length-7);}else await stream.WriteAsync(bytes,0,bytes.Length);
    byte[] response=new byte[4096];int count=await stream.ReadAsync(response,0,response.Length);return Encoding.UTF8.GetString(response,0,count);
   }}
  }
  public static async Task Run(){
   await TestStreams();
   await TestValidation();
   try{await TestSockets();}catch(SocketException e){if(e.NativeErrorCode!=10013)throw;File.WriteAllText(Path.Combine(Path.GetDirectoryName(typeof(LoopbackTests).Assembly.Location),"verification","loopback-network.txt"),"SKIPPED real TCP connections: execution sandbox denies loopback sockets (10013). Stream-level idle/fragmented HTTP tests and callback validation passed. Test on a normal Windows desktop remains necessary.");}
  }
  static async Task TestSockets(){
   using(var stop=new CancellationTokenSource())using(var receiver=new OAuthLoopback("secure-state",stop.Token)){
    var u=new Uri(receiver.Redirect);using(var idle=new TcpClient()){
     await idle.ConnectAsync(u.Host,u.Port);
     var icon=Request(receiver.Redirect,"/favicon.ico");Check(await Task.WhenAny(icon,Task.Delay(2000))==icon,"idle preconnect must not block another socket");Check((await icon).Contains("400 Bad Request"),"favicon ignored");
     Check(!receiver.SubmitUrl(receiver.Redirect+"?code=stolen&state=wrong"),"wrong state rejected");
     Check(!receiver.SubmitUrl("https://example.com/?code=stolen&state=secure-state"),"wrong origin rejected");
     Check(!receiver.SubmitUrl(receiver.Redirect+"other?code=stolen&state=secure-state"),"wrong path rejected");
     Check(!receiver.Completion.IsCompleted,"invalid requests do not end login");
     var real=Request(receiver.Redirect,"/?state=secure-state&code=valid-code",true);Check(await Task.WhenAny(real,Task.Delay(2000))==real,"fragmented callback accepted while idle socket open");Check((await real).Contains("200 OK"),"callback response");Check(await receiver.Completion=="valid-code","callback code");
    }
   }
  }
  static async Task TestValidation(){
   using(var c=new CancellationTokenSource())using(var r=new OAuthLoopback("manual",c.Token)){Check(!r.SubmitUrl(r.Redirect+"?state=wrong&code=attack"),"invalid state");Check(!r.SubmitUrl("https://example.com/?state=manual&code=attack"),"invalid origin");Check(!r.SubmitUrl(r.Redirect+"other?state=manual&code=attack"),"invalid path");Check(!r.Completion.IsCompleted,"invalid callback ignored");Check(r.SubmitUrl(r.Redirect+"?state=manual&code=manual-code"),"manual fallback");Check(await r.Completion=="manual-code","manual code");}
   using(var c=new CancellationTokenSource())using(var r=new OAuthLoopback("cancel",c.Token)){c.Cancel();bool cancelled=false;try{await r.Completion;}catch(OperationCanceledException){cancelled=true;}Check(cancelled,"cancellation");}
   using(var r=new OAuthLoopback("timeout",CancellationToken.None,null,TimeSpan.FromMilliseconds(30))){bool timed=false;try{await r.Completion;}catch(TimeoutException){timed=true;}Check(timed,"timeout");}
   using(var r=new OAuthLoopback("denied",CancellationToken.None)){r.SubmitUrl(r.Redirect+"?state=denied&error=access_denied");bool denied=false;try{await r.Completion;}catch(InvalidOperationException){denied=true;}Check(denied,"denial");}
  }
 }
}
