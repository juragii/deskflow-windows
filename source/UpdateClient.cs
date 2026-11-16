using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.Http;
using System.Diagnostics;
using System.Reflection;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DeskFlow {
 public class UpdateRelease {public bool Anonymous;public string Version,Url,Hash,Notes,ApiUrl,Repository;}
 public sealed partial class UpdateClient {
  HttpClient http;public UpdateClient(HttpClient client=null){http=client??new HttpClient(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(5)};if(http.DefaultRequestHeaders.Authorization!=null)throw new ArgumentException("업데이트 통신에는 기본 인증 헤더를 설정할 수 없습니다.");ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;http.DefaultRequestHeaders.UserAgent.ParseAdd("DeskFlow/2.3");}
  public static string Repository(string text){string s=(text??"").Trim();if(s.StartsWith("https://github.com/",StringComparison.OrdinalIgnoreCase))s=s.Substring(19).TrimEnd('/');if(s.EndsWith(".git"))s=s.Substring(0,s.Length-4);if(!Regex.IsMatch(s,@"\A[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9_.-]{1,100}\z")||s.Split('/')[1]=="."||s.Split('/')[1]=="..")throw new ArgumentException("GitHub 저장소를 계정/저장소 형식으로 입력해 주세요.");return s;}
  static string Str(Dictionary<string,object> d,string k){return d.ContainsKey(k)&&d[k]!=null?Convert.ToString(d[k]):"";}
  public static UpdateRelease ParseRelease(string json,string repository,Version current){
   repository=Repository(repository);var d=(Dictionary<string,object>)UpdateCore.Json.DeserializeObject(json);if((d.ContainsKey("draft")&&Convert.ToBoolean(d["draft"]))||(d.ContainsKey("prerelease")&&Convert.ToBoolean(d["prerelease"])))return null;
   Version version=UpdateCore.ParseVersion(Str(d,"tag_name"));if(version<=current)return null;
   var assets=d.ContainsKey("assets")?(object[])d["assets"]:new object[0];foreach(var raw in assets){var a=(Dictionary<string,object>)raw;if(Str(a,"name")!="DeskFlow-update.zip")continue;string url=Str(a,"browser_download_url"),digest=Str(a,"digest");Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https"||!uri.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)||uri.Port!=443||uri.UserInfo!=""||!uri.AbsolutePath.StartsWith("/"+repository+"/releases/download/",StringComparison.OrdinalIgnoreCase)||uri.Query!=""||uri.Fragment!="")throw new InvalidDataException("업데이트 다운로드 주소가 저장소와 일치하지 않습니다.");if(!digest.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase)||!UpdateCore.HashValid(digest.Substring(7)))throw new InvalidDataException("GitHub에서 SHA-256 검증 정보를 받지 못했습니다.");return new UpdateRelease{Version=version.ToString(),Url=url,Hash=digest.Substring(7),Notes=Str(d,"body")};}throw new InvalidDataException("새 버전에 DeskFlow-update.zip 배포 파일이 없습니다.");
  }
  public async Task<UpdateRelease> Check(string repository,Version current){repository=Repository(repository);using(var r=await http.GetAsync("https://api.github.com/repos/"+repository+"/releases/latest")){if(r.StatusCode==HttpStatusCode.NotFound)throw new IOException("저장소가 비공개이거나 아직 공개 릴리스가 없습니다.");if(!r.IsSuccessStatusCode)throw new IOException("업데이트 확인 실패 ("+(int)r.StatusCode+"). 잠시 후 다시 시도합니다.");return ParseRelease(await r.Content.ReadAsStringAsync(),repository,current);}}
  public async Task<string> Download(UpdateRelease release,string updateRoot){
   string folder=Path.Combine(updateRoot,release.Version+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string archive=Path.Combine(folder,"update.zip");
   using(var response=await http.GetAsync(release.Url,HttpCompletionOption.ResponseHeadersRead)){response.EnsureSuccessStatusCode();if(response.RequestMessage.RequestUri.Scheme!="https")throw new IOException("보안 연결이 아닌 다운로드를 거부했습니다.");if(response.Content.Headers.ContentLength>100L*1024*1024)throw new IOException("업데이트 파일이 너무 큽니다.");using(var input=await response.Content.ReadAsStreamAsync())using(var output=File.Create(archive)){byte[] b=new byte[65536];long count=0;int n;while((n=await input.ReadAsync(b,0,b.Length))>0){count+=n;if(count>100L*1024*1024)throw new IOException("업데이트 크기 제한을 초과했습니다.");await output.WriteAsync(b,0,n);}}}
   if(!string.Equals(UpdateCore.HashFile(archive),release.Hash,StringComparison.OrdinalIgnoreCase))throw new IOException("업데이트 파일 검증에 실패하여 설치하지 않았습니다.");return archive;
  }
  public static string PreparePlan(UpdateRelease release,string archive,string installDir,bool hidden){
   string stage=Path.GetDirectoryName(archive);string extracted=Path.Combine(stage,"verified-"+Guid.NewGuid().ToString("N"));UpdateCore.Extract(archive,extracted,release.Hash,release.Version);
   string helper=Path.Combine(installDir,"DeskFlow.Updater.exe");if(!File.Exists(helper))throw new IOException("업데이트 도우미가 없습니다. 최초 설치 파일을 다시 풀어 주세요.");File.Copy(helper,Path.Combine(stage,"apply.exe"));string config=helper+".config";if(File.Exists(config))File.Copy(config,Path.Combine(stage,"apply.exe.config"));
   using(var process=Process.GetCurrentProcess()){var plan=new UpdatePlan{InstallDir=UpdateCore.Full(installDir),Archive=Path.GetFullPath(archive),Hash=release.Hash,Version=release.Version,ParentExe=Assembly.GetExecutingAssembly().Location,ParentPid=process.Id,ParentStartedUtcTicks=process.StartTime.ToUniversalTime().Ticks,StartHidden=hidden,ResultPath=Path.Combine(stage,"result.json")};string path=Path.Combine(stage,"plan.json");File.WriteAllText(path,UpdateCore.Json.Serialize(plan));return path;}
  }
  public static void StartApply(string planPath){string stage=Path.GetDirectoryName(planPath);Process.Start(new ProcessStartInfo(Path.Combine(stage,"apply.exe"),"--apply \""+planPath+"\""){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=stage});}
 }
}
