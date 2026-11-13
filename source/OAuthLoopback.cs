using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace DeskFlow {
 // Browsers may open an idle preconnect before sending the actual callback.
 // Process each socket independently; never block Accept on a single client.
 public sealed class OAuthLoopback:IDisposable {
  readonly TcpListener listener;readonly string state;readonly Action<string> log;
  readonly TaskCompletionSource<string> completion=new TaskCompletionSource<string>();
  readonly CancellationTokenSource stopped=new CancellationTokenSource();
  readonly CancellationTokenRegistration cancellation;readonly Timer timer;
  public readonly string Redirect; public Task<string> Completion {get{return completion.Task;}}
  public OAuthLoopback(string expectedState,CancellationToken cancel,Action<string> logger=null,TimeSpan? timeout=null){
   state=expectedState;log=logger??delegate{};listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
   Redirect="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port+"/";
   cancellation=cancel.Register(()=>completion.TrySetCanceled());
   timer=new Timer(o=>completion.TrySetException(new TimeoutException("로그인 대기 시간이 지났습니다. Google 로그인을 다시 시작해 주세요.")),null,timeout??TimeSpan.FromMinutes(5),Timeout.InfiniteTimeSpan);
   log("브라우저 로그인 대기 중");var ignored=AcceptLoop();
  }
  async Task AcceptLoop(){
   try{while(!stopped.IsCancellationRequested){var client=await listener.AcceptTcpClientAsync().ConfigureAwait(false);var ignored=Handle(client);}}
   catch(ObjectDisposedException){}catch(SocketException){if(!stopped.IsCancellationRequested)completion.TrySetException(new IOException("로그인 수신 연결이 끊겼습니다. 다시 로그인해 주세요."));}
  }
  public bool SubmitUrl(string text){
   Uri uri,expected=new Uri(Redirect);if(!Uri.TryCreate(text.Trim(),UriKind.Absolute,out uri)||uri.Scheme!=expected.Scheme||uri.Host!=expected.Host||uri.Port!=expected.Port||uri.AbsolutePath!=expected.AbsolutePath)return false;
   return ProcessQuery(uri);
  }
  bool ProcessQuery(Uri uri){
   var q=HttpUtility.ParseQueryString(uri.Query);if(q["state"]!=state)return false;
   if(!string.IsNullOrEmpty(q["error"])) {completion.TrySetException(new InvalidOperationException("Google에서 접근을 승인하지 않았습니다. 로그인을 다시 시작해 주세요."));return true;}
   if(string.IsNullOrEmpty(q["code"]))return false;
   if(completion.TrySetResult(q["code"]))log("Google 인증 응답 수신 완료");return true;
  }
  async Task Handle(TcpClient client){
   using(client)try{using(var stream=client.GetStream()){await Serve(stream).ConfigureAwait(false);}}
   catch(IOException){}catch(ObjectDisposedException){}catch(SocketException){}catch(OperationCanceledException){}
  }
  internal async Task Serve(Stream stream){
    var bytes=new MemoryStream();byte[] buffer=new byte[2048];string headers="";var deadline=Task.Delay(TimeSpan.FromSeconds(8),stopped.Token);
    while(headers.IndexOf("\r\n\r\n",StringComparison.Ordinal)<0){
     var read=stream.ReadAsync(buffer,0,buffer.Length);if(await Task.WhenAny(read,deadline).ConfigureAwait(false)!=read)return;
     int n=await read.ConfigureAwait(false);if(n==0)return;bytes.Write(buffer,0,n);if(bytes.Length>32768)return;headers=Encoding.ASCII.GetString(bytes.ToArray());
    }
    string[] parts=headers.Split('\n')[0].TrimEnd('\r').Split(' ');Uri uri;bool valid=false;
    if(parts.Length==3&&parts[0]=="GET"&&parts[1].StartsWith("/",StringComparison.Ordinal)&&!parts[1].StartsWith("//",StringComparison.Ordinal)&&Uri.TryCreate(Redirect.TrimEnd('/')+parts[1],UriKind.Absolute,out uri))valid=SubmitUrl(uri.AbsoluteUri);
    string body="<!doctype html><meta charset='utf-8'><meta name='referrer' content='no-referrer'><title>DeskFlow</title><body style='font:18px sans-serif;padding:48px'><h2>"+(valid?"인증 응답을 받았습니다":"DeskFlow 로그인 대기 중")+"</h2><p>"+(valid?"DeskFlow로 돌아가 연결 결과를 확인해 주세요. 이 창은 닫으셔도 됩니다.":"프로그램에서 Google 로그인을 다시 시작해 주세요.")+"</p></body>";
    byte[] content=Encoding.UTF8.GetBytes(body),head=Encoding.ASCII.GetBytes("HTTP/1.1 "+(valid?"200 OK":"400 Bad Request")+"\r\nContent-Type: text/html; charset=utf-8\r\nCache-Control: no-store\r\nReferrer-Policy: no-referrer\r\nContent-Length: "+content.Length+"\r\nConnection: close\r\n\r\n");
    await stream.WriteAsync(head,0,head.Length).ConfigureAwait(false);await stream.WriteAsync(content,0,content.Length).ConfigureAwait(false);
  }
  public void Dispose(){stopped.Cancel();listener.Stop();timer.Dispose();cancellation.Dispose();stopped.Dispose();}
 }
}
