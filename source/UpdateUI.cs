using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Threading.Tasks;

namespace DeskFlow {
 partial class Widget {
  UpdateClient updater;DispatcherTimer updateTimer;bool updateBusy,updateHandoff;string readyUpdatePlan="",updateStatus="배포 저장소를 연결하면 자동 업데이트를 시작합니다.";long updateAuthGeneration;DateTime nextUpdate=DateTime.MinValue;
  void InitUpdates(){
   if(suppressNative||Environment.GetCommandLineArgs().Contains("--update-verify-exit"))return;updater=new UpdateClient();updateTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(15)};updateTimer.Tick+=async(s,e)=>{if(!string.IsNullOrEmpty(readyUpdatePlan)){TryApplyUpdate();return;}if(store.Data.AutoUpdates&&DateTime.UtcNow>=nextUpdate&&!string.IsNullOrWhiteSpace(store.Data.UpdateRepository))await CheckUpdates(false);};updateTimer.Start();
   Window.IsVisibleChanged+=(s,e)=>{if(!Window.IsVisible)TryApplyUpdate();};Window.Loaded+=async(s,e)=>{if(store.Data.AutoUpdates&&!string.IsNullOrWhiteSpace(store.Data.UpdateRepository))await CheckUpdates(false);};
  }
  async Task CheckUpdates(bool manual){
   if(updateBusy)return;if(!string.IsNullOrEmpty(readyUpdatePlan)){updateStatus="업데이트 준비 완료 · 화면을 숨기거나 아래 적용 버튼을 누르세요.";return;}if(updater==null)updater=new UpdateClient();if(string.IsNullOrWhiteSpace(store.Data.UpdateRepository)){updateStatus="GitHub 공개 배포 저장소 주소를 먼저 저장해 주세요.";return;}
   updateBusy=true;nextUpdate=DateTime.UtcNow.AddHours(6);updateStatus="새 버전을 확인하고 있습니다…";
   string repository=store.Data.UpdateRepository;long authGeneration=updateAuthGeneration;
   try{string credential=new PrivateUpdateCredentials(store.Dir).Load(repository);if(string.IsNullOrEmpty(credential)){updateStatus="비공개 업데이트 인증 정보를 프로그램 설정에 등록해 주세요.";return;}var release=await updater.CheckPrivate(repository,Assembly.GetExecutingAssembly().GetName().Version,credential);if(release==null){updateStatus="현재 최신 버전입니다. ("+Assembly.GetExecutingAssembly().GetName().Version.ToString(3)+")";return;}string blocked=Path.Combine(store.Dir,"Updates","blocked.json");if(!manual&&File.Exists(blocked)){var failure=UpdateCore.Json.Deserialize<UpdatePlan>(File.ReadAllText(blocked));if(failure.Version==release.Version){updateStatus="이 버전은 이전 적용에 실패하여 자동 재시도를 중단했습니다. 수동 확인으로 다시 시도할 수 있습니다.";return;}}updateStatus=release.Version+" 버전을 내려받고 검증하고 있습니다…";
    string archive=await updater.DownloadPrivate(release,Path.Combine(store.Dir,"Updates"),credential);if(repository!=store.Data.UpdateRepository||authGeneration!=updateAuthGeneration)throw new IOException("배포 주소가 변경되어 이전 주소의 업데이트를 취소했습니다.");readyUpdatePlan=UpdateClient.PreparePlan(release,archive,Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),true);updateStatus="업데이트 준비 완료 · 편집을 마치고 본 화면을 숨기면 자동 적용합니다.";
    if(tray!=null)tray.ShowBalloonTip(5000,"DeskFlow 업데이트","새 버전을 준비했습니다. 본 화면을 숨기면 위젯이 자동으로 다시 시작됩니다.",System.Windows.Forms.ToolTipIcon.Info);TryApplyUpdate();
   }catch(Exception ex){updateStatus="업데이트 보류 · "+ex.Message;nextUpdate=DateTime.UtcNow.AddMinutes(30);}finally{updateBusy=false;}
  }
  bool CanApplyUpdate(){return !Window.IsVisible&&!connecting&&!syncing&&(reminders==null||!reminders.Busy)&&!Window.OwnedWindows.Cast<Window>().Any(w=>w.IsVisible)&&string.IsNullOrWhiteSpace(Find<TextBox>("TaskInput").Text);}
  void TryApplyUpdate(bool manual=false){if(updateHandoff||string.IsNullOrEmpty(readyUpdatePlan)||(!manual&&!store.Data.AutoUpdates)||!CanApplyUpdate())return;try{store.Save();UpdateClient.StartApply(readyUpdatePlan);updateHandoff=true;Window.Close();}catch(Exception ex){updateStatus="업데이트 적용을 미뤘습니다. "+ex.Message;}}
  void ScheduleUpdateOnExit(){if(updateHandoff||string.IsNullOrEmpty(readyUpdatePlan)||!store.Data.AutoUpdates||connecting||Window.OwnedWindows.Cast<Window>().Any(w=>w.IsVisible)||!string.IsNullOrWhiteSpace(Find<TextBox>("TaskInput").Text))return;try{var plan=UpdateCore.Json.Deserialize<UpdatePlan>(File.ReadAllText(readyUpdatePlan));plan.Restart=false;File.WriteAllText(readyUpdatePlan,UpdateCore.Json.Serialize(plan));UpdateClient.StartApply(readyUpdatePlan);updateHandoff=true;}catch(Exception ex){updateStatus="종료 후 업데이트를 적용하지 못했습니다. "+ex.Message;}}
  void StopUpdates(){if(updateTimer!=null)updateTimer.Stop();}
  void UpdateSettings(StackPanel p,Window settings){
   p.Children.Add(Text("비공개 자동 업데이트",18,"#191F28"));var version=Text("현재 버전 "+Assembly.GetExecutingAssembly().GetName().Version.ToString(3),11,"#8B95A1");version.Margin=new Thickness(0,8,0,12);p.Children.Add(version);
   var enabled=new CheckBox{Content="새 버전 자동 다운로드 및 적용",IsChecked=store.Data.AutoUpdates,Margin=new Thickness(0,4,0,12)};enabled.Click+=(s,e)=>{store.Data.AutoUpdates=enabled.IsChecked==true;SafeSave();};p.Children.Add(enabled);
   bool configured=false;try{configured=new PrivateUpdateCredentials(store.Dir).Load(store.Data.UpdateRepository)!="";}catch{}
   var setup=new StackPanel();var setupExpander=new Expander{Header="처음 한 번 연결 설정",IsExpanded=!configured,Content=setup,Margin=new Thickness(0,4,0,12)};p.Children.Add(setupExpander);
   setup.Children.Add(Text("내 비공개 GitHub 저장소",12));var repo=new TextBox{Name="PrivateUpdateRepo",Text=store.Data.UpdateRepository,Height=42,Margin=new Thickness(0,8,0,8),ToolTip="계정/저장소 형식"};setup.Children.Add(repo);
   var label=Text(updateStatus,11,"#8B95A1");label.Margin=new Thickness(0,8,0,8);string observed=updateStatus;var statusTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(1)};statusTimer.Tick+=(s,e)=>{if(observed!=updateStatus){observed=updateStatus;label.Text=updateStatus;}};statusTimer.Start();settings.Closed+=(s,e)=>statusTimer.Stop();
   setup.Children.Add(Btn("저장소 주소 저장",()=>{try{string normalized=UpdateClient.Repository(repo.Text);if(normalized!=store.Data.UpdateRepository){readyUpdatePlan="";updateAuthGeneration++;}store.Data.UpdateRepository=normalized;repo.Text=normalized;SafeSave();nextUpdate=DateTime.MinValue;label.Text="주소를 저장했습니다. 이 저장소 전용 읽기 토큰을 아래에 등록해 주세요.";}catch(Exception ex){label.Text=ex.Message;}}));
   setup.Children.Add(Btn("GitHub에서 비공개 저장소 만들기 ↗",()=>Open("https://github.com/new")));
   setup.Children.Add(Text("업데이트 읽기 전용 인증",14));var help=Text("GitHub에서 fine-grained 토큰을 만들고 Only select repositories에서 이 저장소만 선택합니다. Repository permissions의 Contents를 Read-only로 설정하세요. 토큰은 이 프로그램에만 입력하며 채팅으로 보내지 않습니다.",11,"#8B95A1");help.Margin=new Thickness(0,8,0,8);setup.Children.Add(help);
   setup.Children.Add(Btn("읽기 전용 토큰 만들기 ↗",()=>{try{string owner=UpdateClient.Repository(store.Data.UpdateRepository).Split('/')[0];Open("https://github.com/settings/personal-access-tokens/new?name=DeskFlow%20Update&description=Private%20DeskFlow%20updates&target_name="+Uri.EscapeDataString(owner)+"&contents=read&expires_in=90");}catch(Exception ex){label.Text=ex.Message;}}));
   var password=new PasswordBox{Name="PrivateUpdateToken",MaxLength=4096,Padding=new Thickness(12,9,12,9),MinHeight=42,Margin=new Thickness(0,8,0,8),ToolTip="github_pat_로 시작하는 저장소 전용 읽기 토큰",Background=Brush("#F2F4F6"),BorderThickness=new Thickness(0)};setup.Children.Add(password);
   var credentialState=Text("",11,"#8B95A1");Action refresh=()=>{try{credentialState.Text=new PrivateUpdateCredentials(store.Dir).Load(store.Data.UpdateRepository)==""?"이 저장소의 인증 정보가 없습니다.":"인증 정보가 이 Windows 계정으로 암호화되어 저장되어 있습니다.";}catch(Exception ex){credentialState.Text=ex.Message;}};refresh();setup.Children.Add(credentialState);
   setup.Children.Add(Btn("인증 정보 암호화 저장",()=>{try{if(updateBusy){label.Text="진행 중인 확인이 끝난 후 다시 시도해 주세요.";return;}string normalized=UpdateClient.Repository(repo.Text);new PrivateUpdateCredentials(store.Dir).Save(normalized,password.Password);password.Clear();store.Data.UpdateRepository=normalized;repo.Text=normalized;readyUpdatePlan="";updateAuthGeneration++;SafeSave();nextUpdate=DateTime.MinValue;refresh();label.Text="암호화해 저장했습니다. 비공개 연결 확인을 눌러 주세요.";}catch(Exception ex){label.Text=ex.Message;}}));
   setup.Children.Add(Btn("이 PC의 업데이트 인증 지우기",()=>{new PrivateUpdateCredentials(store.Dir).Clear();password.Clear();readyUpdatePlan="";updateAuthGeneration++;refresh();updateStatus="GitHub 업데이트 인증을 지웠습니다. Google 로그인과 일정은 유지됩니다.";label.Text=updateStatus;}));
   var connection=Btn("비공개 연결 확인",()=>{});connection.Click+=async(s,e)=>{connection.IsEnabled=false;try{string token=new PrivateUpdateCredentials(store.Dir).Load(store.Data.UpdateRepository);if(updater==null)updater=new UpdateClient();label.Text=await updater.CheckPrivateConnection(store.Data.UpdateRepository,token);setupExpander.IsExpanded=false;}catch(Exception ex){label.Text=ex.Message;}finally{connection.IsEnabled=true;}};setup.Children.Add(connection);
   var check=Btn("지금 업데이트 확인",()=>{});check.Click+=async(s,e)=>{check.IsEnabled=false;await CheckUpdates(true);label.Text=updateStatus;check.IsEnabled=true;};p.Children.Add(check);p.Children.Add(label);
   p.Children.Add(Btn("준비된 업데이트 적용",()=>{if(string.IsNullOrEmpty(readyUpdatePlan)){label.Text="먼저 업데이트를 확인해 주세요.";return;}if(connecting||syncing||!string.IsNullOrWhiteSpace(Find<TextBox>("TaskInput").Text)||Window.OwnedWindows.Cast<Window>().Any(x=>x!=settings&&x.IsVisible)){label.Text="진행 중인 작업과 입력 중인 할 일을 먼저 마쳐 주세요.";return;}settings.Close();Window.Hide();TryApplyUpdate(true);}));
   var note=Text("ChatGPT의 GitHub 연결과 이 프로그램의 읽기 인증은 별도입니다. 토큰은 암호화 파일에만 저장하며, 업데이트 다운로드 이동 시 외부 서버에 전달하지 않습니다. 편집 중인 내용이 있으면 적용을 미룹니다.",11,"#8B95A1");note.Margin=new Thickness(0,0,0,24);p.Children.Add(note);
  }
  public static void MarkUpdateHealthy(string[] args,string dataRoot){int i=Array.IndexOf(args,"--update-health");if(i<0||i+1>=args.Length)return;string file=args[i+1];if(UpdateCore.HealthPath(file,Path.Combine(dataRoot,"Updates"))&&Directory.Exists(Path.GetDirectoryName(file)))File.WriteAllText(file,"ready");}
 }
}
