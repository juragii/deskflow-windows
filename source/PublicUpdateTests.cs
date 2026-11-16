using System;
using System.IO;
using System.Text;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
namespace DeskFlow {
 static class PublicUpdateTests {
  sealed class Server:HttpMessageHandler {
   public readonly byte[] Bytes=Encoding.UTF8.GetBytes("public-update-payload");public string Hash,Blob=new string('c',40);public int Reads;
   public Server(){using(var s=SHA256.Create())Hash=BitConverter.ToString(s.ComputeHash(Bytes)).Replace("-","").ToLowerInvariant();}
   protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
    if(request.Headers.Authorization!=null)throw new Exception("Public update must not transmit stored credentials");Reads++;
    var response=new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=request};string path=request.RequestUri.AbsolutePath;
    if(path.EndsWith("/git/blobs/"+Blob))response.Content=new ByteArrayContent(Bytes);
    else if(path.EndsWith("/contents/updates/latest.json")){var manifest=new{schema=1,repository="example/nanal",version="2.3.10.0",blob_sha=Blob,sha256=Hash};response.Content=new StringContent(UpdateCore.Json.Serialize(new{encoding="base64",content=Convert.ToBase64String(Encoding.UTF8.GetBytes(UpdateCore.Json.Serialize(manifest)))}));}
    else response.Content=new StringContent("{\"private\":false,\"full_name\":\"example/nanal\"}");return Task.FromResult(response);
   }
  }
  public static async Task Run(string root){var server=new Server();var client=new UpdateClient(new HttpClient(server));var release=await client.CheckRepository("example/nanal",new Version(2,3,9,0),"stale-token-must-not-be-sent");if(release==null||!release.Anonymous||release.Version!="2.3.10.0")throw new Exception("Public manifest selection failed");string file=await client.DownloadPrivate(release,Path.Combine(root,"public-update-tests"),"");if(File.ReadAllText(file)!="public-update-payload")throw new Exception("Public blob/hash validation failed");if(await client.CheckRepository("example/nanal",new Version(2,3,10,0))!=null)throw new Exception("Current version must not update");await client.CheckRepositoryConnection("example/nanal");File.WriteAllText(Path.Combine(root,"public-update-result.txt"),"PASS: anonymous public metadata/manifest/immutable blob download, stale stored PAT never sent, SHA-256 check, current-version no-op, connection check. Private updater safeguards tested separately.");}
 }
}
