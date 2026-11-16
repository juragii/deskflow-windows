using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
namespace DeskFlow {
 public sealed partial class UpdateClient {
  public async Task<UpdateRelease> CheckRepository(string repository,Version current,string token=""){
   repository=Repository(repository);
   var meta=await PrivateJson("https://api.github.com/repos/"+repository,repository,"",true);
   if(meta==null||meta.ContainsKey("private")&&Convert.ToBoolean(meta["private"]))return await CheckPrivate(repository,current,token);
   if(!meta.ContainsKey("private")||!Str(meta,"full_name").Equals(repository,StringComparison.OrdinalIgnoreCase))throw new IOException("업데이트 저장소 정보가 주소와 일치하지 않습니다.");
   var file=await PrivateJson("https://api.github.com/repos/"+repository+"/contents/updates/latest.json",repository,"");
   if(Str(file,"encoding")!="base64")throw new IOException("업데이트 목록의 인코딩이 올바르지 않습니다.");
   string json;try{json=Encoding.UTF8.GetString(Convert.FromBase64String(Str(file,"content")));}catch{throw new IOException("업데이트 목록을 읽지 못했습니다.");}
   var release=ParsePrivateManifest(json,repository,current);if(release!=null)release.Anonymous=true;return release;
  }
  public async Task<string> CheckRepositoryConnection(string repository,string token=""){
   repository=Repository(repository);var meta=await PrivateJson("https://api.github.com/repos/"+repository,repository,"",true);
   if(meta!=null&&meta.ContainsKey("private")&&!Convert.ToBoolean(meta["private"])&&Str(meta,"full_name").Equals(repository,StringComparison.OrdinalIgnoreCase))return "공개 저장소 연결 완료 · 토큰 없이 자동 업데이트를 사용할 수 있습니다.";
   return await CheckPrivateConnection(repository,token);
  }
 }
}
