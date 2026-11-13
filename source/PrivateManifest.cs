using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace DeskFlow {
 public sealed partial class UpdateClient {
  internal static UpdateRelease ParsePrivateManifest(string json,string repository,Version current){
   repository=Repository(repository);try{if(json.Length>16384)throw new InvalidDataException();var m=(Dictionary<string,object>)UpdateCore.Json.DeserializeObject(json);if(!m.ContainsKey("schema")||Convert.ToInt32(m["schema"])!=1||!Str(m,"repository").Equals(repository,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException();var version=UpdateCore.ParseVersion(Str(m,"version"));if(version<=current)return null;string blob=Str(m,"blob_sha"),hash=Str(m,"sha256");if(!Regex.IsMatch(blob,@"\A(?:[0-9a-fA-F]{40}|[0-9a-fA-F]{64})\z")||!UpdateCore.HashValid(hash))throw new InvalidDataException();return new UpdateRelease{Version=version.ToString(),Repository=repository,ApiUrl="https://api.github.com/repos/"+repository+"/git/blobs/"+blob,Url="https://github.com/"+repository,Hash=hash,Notes=Str(m,"notes")};}catch{throw new IOException("비공개 업데이트 목록의 형식이나 검증 정보가 올바르지 않습니다.");}
  }
  async Task<UpdateRelease> TryManifest(string repository,Version current,string token){var file=await PrivateJson("https://api.github.com/repos/"+repository+"/contents/updates/latest.json",repository,token,true);if(file==null)return null;if(Str(file,"encoding")!="base64")throw new IOException("비공개 업데이트 목록의 파일 인코딩을 확인하지 못했습니다.");string json;try{json=Encoding.UTF8.GetString(Convert.FromBase64String(Str(file,"content")));}catch{throw new IOException("비공개 업데이트 목록을 읽지 못했습니다.");}return ParsePrivateManifest(json,repository,current);}
 }
}
