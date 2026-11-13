using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Diagnostics;
using System.Reflection;

namespace DeskFlow {
 class UpdaterProgram {
  [STAThread] static int Main(string[] args){
   if(args.Length!=2||args[0]!="--apply")return 2;UpdatePlan plan=null;string stage=Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),backup=Path.Combine(stage,"backup");string[] names=null;bool swapped=false,validated=false;
   try{
    if(UpdateCore.Full(Path.GetDirectoryName(args[1]))!=UpdateCore.Full(stage))throw new InvalidDataException("업데이트 계획 경로가 올바르지 않습니다.");plan=UpdateCore.Json.Deserialize<UpdatePlan>(File.ReadAllText(args[1]));
    if(UpdateCore.Full(Path.GetDirectoryName(plan.Archive))!=UpdateCore.Full(stage)||UpdateCore.Full(plan.ResultPath)!=UpdateCore.Full(Path.Combine(stage,"result.json")))throw new InvalidDataException("업데이트 작업 폴더가 일치하지 않습니다.");
    string main=Path.Combine(plan.InstallDir,"DeskFlow.exe");if(!File.Exists(main)||UpdateCore.Full(main)!=UpdateCore.Full(plan.ParentExe))throw new InvalidDataException("설치 경로에 기존 프로그램이 없습니다.");validated=true;
    UpdateCore.WaitForParent(plan);
    using(var mutex=new Mutex(false,"Local\\DeskFlow-Widget")){bool locked=false;try{try{locked=mutex.WaitOne(TimeSpan.FromSeconds(30));}catch(AbandonedMutexException){locked=true;}if(!locked)throw new IOException("다른 DeskFlow가 실행 중이어서 교체를 중단했습니다.");
      if(UpdateCore.ParseVersion(FileVersionInfo.GetVersionInfo(main).FileVersion)>=UpdateCore.ParseVersion(plan.Version)){Result(plan,"already-current","");return 0;}
      names=UpdateCore.Extract(plan.Archive,Path.Combine(stage,"unpacked"),plan.Hash,plan.Version);UpdateCore.Swap(plan.InstallDir,Path.Combine(stage,"unpacked"),backup,names);swapped=true;
     }finally{if(locked)mutex.ReleaseMutex();}}
    {string ready=Path.Combine(stage,Guid.NewGuid().ToString("N")+".ready");var info=new ProcessStartInfo(main,"--update-health \""+ready+"\""+((plan.StartHidden||!plan.Restart)?" --start-hidden":"")+(!plan.Restart?" --update-verify-exit":"")){UseShellExecute=false,WorkingDirectory=plan.InstallDir};using(var child=Process.Start(info)){var watch=Stopwatch.StartNew();while(!File.Exists(ready)&&!child.HasExited&&watch.Elapsed<TimeSpan.FromSeconds(30))Thread.Sleep(150);if(!File.Exists(ready)){if(!child.HasExited){child.Kill();child.WaitForExit(5000);}throw new IOException("새 버전이 정상적으로 시작되지 않아 이전 버전으로 복구했습니다.");}if(!plan.Restart&&!child.WaitForExit(10000)){child.Kill();child.WaitForExit(5000);}}}
    try{Result(plan,"installed","");}catch{}return 0;
   }catch(Exception error){string status=error is AggregateException?"rollback-failed":"failed";if(swapped&&plan!=null&&names!=null){try{UpdateCore.Rollback(plan.InstallDir,backup,names);status="rolled-back";}catch(Exception restore){error=new IOException(error.Message+" 복구 실패: "+restore.Message);status="rollback-failed";}}
    if(validated){try{Result(plan,status,error.Message);}catch{}if(plan.Restart&&status!="rollback-failed"){try{Process.Start(new ProcessStartInfo(Path.Combine(plan.InstallDir,"DeskFlow.exe"),plan.StartHidden?"--start-hidden":""){UseShellExecute=false,WorkingDirectory=plan.InstallDir});}catch{}}}return 1;
   }
  }
  static void Result(UpdatePlan p,string status,string message){var data=new{status=status,version=p.Version,message=message,at=DateTime.UtcNow.ToString("o")};string json=UpdateCore.Json.Serialize(data);File.WriteAllText(p.ResultPath,json);string root=Path.GetDirectoryName(Path.GetDirectoryName(p.ResultPath));File.WriteAllText(Path.Combine(root,"last-result.json"),json);if(status=="failed"||status=="rolled-back"||status=="rollback-failed")File.WriteAllText(Path.Combine(root,"blocked.json"),json);}
 }
}
