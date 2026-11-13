using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.Diagnostics;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace DeskFlow {
 public class UpdatePlan {public string InstallDir,Archive,Hash,Version,ParentExe,ResultPath;public int ParentPid;public long ParentStartedUtcTicks;public bool Restart=true,StartHidden;}
 public static class UpdateCore {
  public static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
  public static readonly string[] RuntimeFiles={"DeskFlow.exe","DeskFlow.exe.config","DeskFlow.ico","DeskFlow.Updater.exe","DeskFlow.Updater.exe.config"};
  public static Version ParseVersion(string text){Version v;if(!System.Version.TryParse((text??"").Trim().TrimStart('v','V'),out v)||v.Major<0)throw new InvalidDataException("버전 형식이 올바르지 않습니다.");return new Version(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision));}
  public static string HashFile(string path){using(var f=File.OpenRead(path))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
  public static bool HashValid(string hash){return hash!=null&&hash.Length==64&&hash.All(c=>"0123456789abcdefABCDEF".IndexOf(c)>=0);}
  public static string Full(string p){return Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar);}
  public static string[] Extract(string archive,string folder,string expectedHash,string version){
   if(!HashValid(expectedHash)||!string.Equals(HashFile(archive),expectedHash,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("업데이트 파일의 SHA-256 검증에 실패했습니다.");
   Directory.CreateDirectory(folder);var names=new List<string>();long expanded=0;
   using(var zip=ZipFile.OpenRead(archive)){
    if(zip.Entries.Count>RuntimeFiles.Length)throw new InvalidDataException("허용되지 않은 업데이트 파일 구성입니다.");
    foreach(var entry in zip.Entries){string name=entry.FullName;if(!RuntimeFiles.Contains(name,StringComparer.Ordinal)||names.Contains(name,StringComparer.OrdinalIgnoreCase))throw new InvalidDataException("허용되지 않은 파일 경로나 중복 파일이 있습니다.");expanded+=entry.Length;if(expanded>200L*1024*1024)throw new InvalidDataException("업데이트 파일의 압축 해제 크기가 너무 큽니다.");entry.ExtractToFile(Path.Combine(folder,name));names.Add(name);}
   }
   foreach(var required in new[]{"DeskFlow.exe","DeskFlow.exe.config","DeskFlow.Updater.exe","DeskFlow.Updater.exe.config"})if(!names.Contains(required))throw new InvalidDataException("필수 업데이트 파일이 없습니다.");
   var actual=ParseVersion(FileVersionInfo.GetVersionInfo(Path.Combine(folder,"DeskFlow.exe")).FileVersion);if(actual!=ParseVersion(version))throw new InvalidDataException("실행 파일과 배포 버전이 일치하지 않습니다.");return names.ToArray();
  }
  public static void Swap(string root,string prepared,string backup,string[] names,Action<int> afterWrite=null){
   root=Full(root);Directory.CreateDirectory(backup);var done=new List<string>();
   try{int count=0;foreach(string name in names){if(!RuntimeFiles.Contains(name,StringComparer.Ordinal))throw new InvalidDataException("허용되지 않은 교체 파일입니다.");string target=Path.Combine(root,name),old=Path.Combine(backup,name),temp=target+".update-"+Guid.NewGuid().ToString("N");if(File.Exists(target))File.Copy(target,old,false);
     try{File.Copy(Path.Combine(prepared,name),temp,false);if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);}finally{if(File.Exists(temp))File.Delete(temp);}done.Add(name);if(afterWrite!=null)afterWrite(++count);
    }}catch(Exception failure){try{Rollback(root,backup,done.ToArray());}catch(Exception restore){throw new AggregateException("파일 교체와 복구가 모두 실패했습니다. 업데이트 백업을 확인해 주세요.",failure,restore);}throw;}
  }
  public static void Rollback(string root,string backup,string[] names){foreach(var name in names.Reverse()){if(!RuntimeFiles.Contains(name,StringComparer.Ordinal))throw new InvalidDataException("복구 대상 파일이 올바르지 않습니다.");string target=Path.Combine(root,name),old=Path.Combine(backup,name);if(File.Exists(old)){string temp=target+".restore-"+Guid.NewGuid().ToString("N");File.Copy(old,temp);if(File.Exists(target))File.Replace(temp,target,null);else File.Move(temp,target);}else if(File.Exists(target))File.Delete(target);}}
  public static void WaitForParent(UpdatePlan plan){try{using(var process=Process.GetProcessById(plan.ParentPid)){if(process.StartTime.ToUniversalTime().Ticks!=plan.ParentStartedUtcTicks)return;if(!process.WaitForExit(120000))throw new IOException("프로그램이 아직 종료되지 않아 업데이트를 적용하지 않았습니다.");}}catch(ArgumentException){}}
  public static bool HealthPath(string path,string updateRoot){string p=Full(path),root=Full(updateRoot)+Path.DirectorySeparatorChar;return p.StartsWith(root,StringComparison.OrdinalIgnoreCase)&&p.EndsWith(".ready",StringComparison.OrdinalIgnoreCase);}
 }
}
