using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace DeskFlow {
 public static class PrivateUpdateTests {
  public const string DummyToken="github_pat_unit_test_not_a_real_token_1234567890";
  static void Check(bool ok,string label){if(!ok)throw new Exception("Private updater: "+label);}
  static async Task Reject(Func<Task> action,string label){bool rejected=false;try{await action();}catch(Exception e){Check(!e.ToString().Contains(DummyToken),"secret omitted from errors");rejected=true;}Check(rejected,label);}
  class Server:HttpMessageHandler {
   public bool Private=true,Unauthorized,Direct,UseManifest;public string Redirect="https://release-assets.githubusercontent.com/test/update.zip?signature=fake",AssetUrl="https://api.github.com/repos/example/deskflow/releases/assets/123";public byte[] Bytes=Encoding.UTF8.GetBytes("private-payload");public int ApiReads,CdnReads;public string Blob=new string('b',40);
   string Digest(){using(var s=SHA256.Create())return BitConverter.ToString(s.ComputeHash(Bytes)).Replace("-","").ToLowerInvariant();}
   protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken c){Check(r.Method==HttpMethod.Get,"read-only requests");var response=new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=r};if(r.RequestUri.Host=="api.github.com"){
     ApiReads++;Check(r.Headers.Authorization!=null&&r.Headers.Authorization.Parameter==DummyToken,"API bearer authentication");
     if(Unauthorized){response.StatusCode=HttpStatusCode.Unauthorized;response.Content=new StringContent(DummyToken);return Task.FromResult(response);}
     if(r.RequestUri.AbsolutePath.EndsWith("/contents/updates/latest.json")){if(!UseManifest){response.StatusCode=HttpStatusCode.NotFound;response.Content=new StringContent("{}");}else{var manifest=new{schema=1,repository="example/deskflow",version="2.3.1",blob_sha=Blob,sha256=Digest()};response.Content=new StringContent(UpdateCore.Json.Serialize(new{encoding="base64",content=Convert.ToBase64String(Encoding.UTF8.GetBytes(UpdateCore.Json.Serialize(manifest)))}));}return Task.FromResult(response);}
     if(r.RequestUri.AbsolutePath.EndsWith("/git/blobs/"+Blob)){Check(r.Headers.Accept.Any(x=>x.MediaType=="application/vnd.github.raw+json"),"raw immutable blob download");response.Content=new ByteArrayContent(Bytes);return Task.FromResult(response);}
     if(r.RequestUri.AbsolutePath.EndsWith("/releases/assets/123")){Check(r.Headers.Accept.Any(x=>x.MediaType=="application/octet-stream"),"binary API accept header");if(Direct)response.Content=new ByteArrayContent(Bytes);else{response.StatusCode=HttpStatusCode.Found;response.Headers.Location=new Uri(Redirect);response.Content=new StringContent("");}return Task.FromResult(response);}
     object data=r.RequestUri.AbsolutePath.EndsWith("/releases/latest")?(object)new{tag_name="v2.3.1",draft=false,prerelease=false,body="update",assets=new[]{new{id=123,url=AssetUrl,name="DeskFlow-update.zip",browser_download_url="https://github.com/example/deskflow/releases/download/v2.3.1/DeskFlow-update.zip",digest="sha256:"+Digest()}}}:new{full_name="example/deskflow",@private=Private};response.Content=new StringContent(UpdateCore.Json.Serialize(data));
    }else {CdnReads++;Check(r.RequestUri.Host=="release-assets.githubusercontent.com","trusted CDN only");Check(r.Headers.Authorization==null,"PAT never sent to CDN");response.Content=new ByteArrayContent(Bytes);}return Task.FromResult(response);
   }
  }
  public static async Task Run(string reportRoot){string root=Path.Combine(reportRoot,"private-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var credentials=new PrivateUpdateCredentials(root);
   credentials.Save("example/deskflow",DummyToken);Check(credentials.Load("example/deskflow")==DummyToken,"DPAPI credential roundtrip");Check(credentials.Load("other/repo")=="","credentials bound to one repository");Check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root,"github-update.bin"))).Contains(DummyToken),"token encrypted on disk");var store=new Store(root);store.Save();Check(!File.ReadAllText(Path.Combine(root,"data.json")).Contains(DummyToken),"token absent from settings JSON");
   File.WriteAllText(Path.Combine(root,"account.bin"),"preserve-google-auth");credentials.Clear();Check(credentials.Load("example/deskflow")==""&&File.ReadAllText(Path.Combine(root,"account.bin"))=="preserve-google-auth","clear only updater credentials");
   var server=new Server();var client=new UpdateClient(new HttpClient(server));string result=await client.CheckPrivateConnection("example/deskflow",DummyToken);Check(result.Length>0,"private metadata connection");var release=await client.CheckPrivate("example/deskflow",new Version(2,3,0,0),DummyToken);Check(release.ApiUrl==server.AssetUrl,"private asset API binding");string archive=await client.DownloadPrivate(release,root,DummyToken);Check(File.ReadAllBytes(archive).SequenceEqual(server.Bytes)&&server.CdnReads==1,"authenticated asset redirect download");
   server.Direct=true;await client.DownloadPrivate(release,root,DummyToken);server.Direct=false;
   int cdn=server.CdnReads;server.Redirect="https://evil.example/update.zip";await Reject(async()=>{await client.DownloadPrivate(release,root,DummyToken);},"untrusted redirect blocked");Check(server.CdnReads==cdn,"no request to untrusted host");
   server.Redirect="https://api.github.com/repos/other/repo/releases/assets/123";await Reject(async()=>{await client.DownloadPrivate(release,root,DummyToken);},"cross-repository credential redirect blocked");
   server.Redirect="http://release-assets.githubusercontent.com/test/update.zip";await Reject(async()=>{await client.DownloadPrivate(release,root,DummyToken);},"HTTP downgrade blocked");
   server.AssetUrl="https://api.github.com/repos/other/repo/releases/assets/123";await Reject(async()=>{await client.CheckPrivate("example/deskflow",new Version(2,3,0,0),DummyToken);},"foreign asset metadata rejected");
   server.UseManifest=true;var manifestRelease=await client.CheckPrivate("example/deskflow",new Version(2,3,0,0),DummyToken);Check(manifestRelease.ApiUrl.EndsWith("/git/blobs/"+server.Blob),"repository manifest selects immutable blob");var manifestArchive=await client.DownloadPrivate(manifestRelease,root,DummyToken);Check(File.ReadAllBytes(manifestArchive).SequenceEqual(server.Bytes),"private Git blob package download");
   server.Private=false;await Reject(async()=>{await client.CheckPrivateConnection("example/deskflow",DummyToken);},"public repository rejected in private mode");server.Private=true;server.Unauthorized=true;await Reject(async()=>{await client.CheckPrivateConnection("example/deskflow",DummyToken);},"expired token handled without disclosure");
   File.WriteAllText(Path.Combine(reportRoot,"private-update-result.txt"),"PASS: repository-bound DPAPI storage, plaintext/settings exclusion, token clear isolation, private repository validation, GET-only API requests, authenticated release/asset requests, private repository manifest and immutable Git blob download, direct and redirected binary downloads, credential stripping at CDN, cross-origin and cross-repository redirect rejection, HTTPS downgrade rejection, private asset metadata binding, public repository rejection, sanitized authentication errors. All GitHub responses were mocked; no real PAT was used.");
  }
 }
}
