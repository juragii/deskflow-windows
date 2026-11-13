using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Collections.Generic;
using System.Diagnostics;

namespace DeskFlow {
 public static class UpdateTests {
  static void Check(bool value,string name){if(!value)throw new Exception("Updater: "+name);}
  static void Reject(Action action,string name){bool failed=false;try{action();}catch{failed=true;}Check(failed,name);}
  static string Release(string url,string hash,string tag="v2.2.1",bool preview=false){return UpdateCore.Json.Serialize(new{tag_name=tag,draft=false,prerelease=preview,body="changes",assets=new[]{new{name="DeskFlow-update.zip",browser_download_url=url,digest="sha256:"+hash}}});}
  sealed class Mock:HttpMessageHandler {public string Json;public byte[] Package;protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){var response=new HttpResponseMessage(HttpStatusCode.OK){RequestMessage=request};response.Content=request.RequestUri.Host=="api.github.com"?(HttpContent)new StringContent(Json):new ByteArrayContent(Package);return Task.FromResult(response);}}
  public static async Task Run(string reportRoot){
   string bin=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),root=Path.Combine(reportRoot,"updates-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
   Check(UpdateClient.Repository("https://github.com/example/deskflow.git")=="example/deskflow","repository normalization");Reject(()=>UpdateClient.Repository("https://evil.test/repo"),"foreign origin rejected");
   var current=new Version(2,2,0,0);string url="https://github.com/example/deskflow/releases/download/v2.2.1/DeskFlow-update.zip";string hash=new string('a',64);
   Check(UpdateClient.ParseRelease(Release(url,hash),"example/deskflow",current).Version=="2.2.1.0","upgrade parsing");Check(UpdateClient.ParseRelease(Release(url,hash,"v2.1.0"),"example/deskflow",current)==null,"downgrade ignored");Check(UpdateClient.ParseRelease(Release(url,hash,"v2.2.1",true),"example/deskflow",current)==null,"prerelease ignored");Reject(()=>UpdateClient.ParseRelease(Release("https://evil.test/app.zip",hash),"example/deskflow",current),"asset origin binding");Reject(()=>UpdateClient.ParseRelease(Release(url,"bad"),"example/deskflow",current),"missing digest rejected");
   string runtime=Path.Combine(root,"runtime");Directory.CreateDirectory(runtime);File.Copy(Path.Combine(bin,"verification","payload","DeskFlow.exe"),Path.Combine(runtime,"DeskFlow.exe"));foreach(var name in UpdateCore.RuntimeFiles.Where(n=>n!="DeskFlow.exe"))File.Copy(Path.Combine(bin,name),Path.Combine(runtime,name));
   string zip=Path.Combine(root,"release.zip");ZipFile.CreateFromDirectory(runtime,zip);hash=UpdateCore.HashFile(zip);
   var mock=new Mock{Json=Release(url,hash),Package=File.ReadAllBytes(zip)};var client=new UpdateClient(new HttpClient(mock));var release=await client.Check("example/deskflow",current);string archive=await client.Download(release,root);
   var files=UpdateCore.Extract(archive,Path.Combine(root,"unpacked"),hash,"2.2.1");Check(files.Length==5,"complete runtime extracted");Reject(()=>UpdateCore.Extract(archive,Path.Combine(root,"bad-hash"),new string('0',64),"2.2.1"),"hash mismatch rejected");Reject(()=>UpdateCore.Extract(archive,Path.Combine(root,"wrong-version"),hash,"2.2.2"),"payload version mismatch rejected");
   string bad=Path.Combine(root,"traversal.zip");using(var z=ZipFile.Open(bad,ZipArchiveMode.Create)){using(var writer=new StreamWriter(z.CreateEntry("../data.json").Open()))writer.Write("bad");}Reject(()=>UpdateCore.Extract(bad,Path.Combine(root,"bad-path"),UpdateCore.HashFile(bad),"2.2.1"),"archive traversal rejected");
   string duplicate=Path.Combine(root,"duplicate.zip");using(var z=ZipFile.Open(duplicate,ZipArchiveMode.Create)){z.CreateEntry("DeskFlow.exe");z.CreateEntry("DeskFlow.exe");}Reject(()=>UpdateCore.Extract(duplicate,Path.Combine(root,"bad-duplicate"),UpdateCore.HashFile(duplicate),"2.2.1"),"duplicate entries rejected");
   string install=Path.Combine(root,"install");Directory.CreateDirectory(install);foreach(var name in UpdateCore.RuntimeFiles)File.Copy(name=="DeskFlow.exe"?Path.Combine(bin,"verification","payload","DeskFlow.old.exe"):Path.Combine(bin,name),Path.Combine(install,name));string before=UpdateCore.HashFile(Path.Combine(install,"DeskFlow.exe"));
   Reject(()=>UpdateCore.Swap(install,Path.Combine(root,"unpacked"),Path.Combine(root,"rollback"),files,i=>{if(i==2)throw new IOException("injected write failure");}),"injected transaction failure");Check(UpdateCore.HashFile(Path.Combine(install,"DeskFlow.exe"))==before,"partial swap rolled back");
   string stage=Path.GetDirectoryName(archive);File.Copy(Path.Combine(bin,"DeskFlow.Updater.exe"),Path.Combine(stage,"apply.exe"));File.Copy(Path.Combine(bin,"DeskFlow.Updater.exe.config"),Path.Combine(stage,"apply.exe.config"));
   var plan=new UpdatePlan{InstallDir=install,ParentExe=Path.Combine(install,"DeskFlow.exe"),ParentPid=int.MaxValue,ParentStartedUtcTicks=0,Archive=archive,Hash=hash,Version="2.2.1.0",ResultPath=Path.Combine(stage,"result.json"),Restart=true,StartHidden=true};string planPath=Path.Combine(stage,"plan.json");File.WriteAllText(planPath,UpdateCore.Json.Serialize(plan));
   using(var helper=Process.Start(new ProcessStartInfo(Path.Combine(stage,"apply.exe"),"--apply \""+planPath+"\""){UseShellExecute=false,CreateNoWindow=true})){bool exited=await Task.Run(()=>helper.WaitForExit(45000));Check(exited,"helper completes");Check(helper.ExitCode==0,"external helper installs and health-checks test payload");}
   var result=(Dictionary<string,object>)UpdateCore.Json.DeserializeObject(File.ReadAllText(plan.ResultPath));Check(Convert.ToString(result["status"])=="installed","helper result");Check(UpdateCore.ParseVersion(FileVersionInfo.GetVersionInfo(Path.Combine(install,"DeskFlow.exe")).FileVersion)==new Version(2,2,1,0),"new executable installed");
   string installedHash=UpdateCore.HashFile(Path.Combine(install,"DeskFlow.exe"));File.Copy(Path.Combine(bin,"verification","payload","DeskFlow.bad.exe"),Path.Combine(runtime,"DeskFlow.exe"),true);string badStage=Path.Combine(root,"bad-startup");Directory.CreateDirectory(badStage);string badZip=Path.Combine(badStage,"update.zip");ZipFile.CreateFromDirectory(runtime,badZip);File.Copy(Path.Combine(bin,"DeskFlow.Updater.exe"),Path.Combine(badStage,"apply.exe"));File.Copy(Path.Combine(bin,"DeskFlow.Updater.exe.config"),Path.Combine(badStage,"apply.exe.config"));plan.Archive=badZip;plan.Hash=UpdateCore.HashFile(badZip);plan.Version="2.2.2.0";plan.ResultPath=Path.Combine(badStage,"result.json");string badPlan=Path.Combine(badStage,"plan.json");File.WriteAllText(badPlan,UpdateCore.Json.Serialize(plan));
   using(var helper=Process.Start(new ProcessStartInfo(Path.Combine(badStage,"apply.exe"),"--apply \""+badPlan+"\""){UseShellExecute=false,CreateNoWindow=true})){Check(await Task.Run(()=>helper.WaitForExit(45000)),"failed startup helper completes");Check(helper.ExitCode==1,"startup failure reported");}var failed=(Dictionary<string,object>)UpdateCore.Json.DeserializeObject(File.ReadAllText(plan.ResultPath));Check(Convert.ToString(failed["status"])=="rolled-back"&&UpdateCore.HashFile(Path.Combine(install,"DeskFlow.exe"))==installedHash,"failed startup restores previous executable");
   Check(UpdateCore.Json.Deserialize<UpdatePlan>(File.ReadAllText(Path.Combine(root,"blocked.json"))).Version=="2.2.2.0","failed version recorded for automatic retry suppression");
   Check(UpdateCore.HealthPath(Path.Combine(root,"test.ready"),root)&&!UpdateCore.HealthPath(Path.Combine(Path.GetDirectoryName(root),"escape.ready"),root),"health marker path binding");
   File.WriteAllText(Path.Combine(reportRoot,"update-result.txt"),"PASS: GitHub release parsing, downgrade/prerelease rejection, repository and asset origin binding, digest check, streamed download with mock HTTP, archive traversal/duplicate rejection, runtime file/version validation, transactional replacement, injected failure rollback, external updater process, replacement and new-version health handshake, failed-startup rollback. Live GitHub repository publishing and downloading were not performed.");
  }
 }
}
