using System;
using System.Windows;
namespace DeskFlow {
 partial class Widget {
  void SelectDate(DateTime date){bool changed=date.Month!=month.Month||date.Year!=month.Year;selected=date;month=date;TrackDateSelection();RenderCalendar();RenderTasks();if(changed){var ignored=Sync();}}
  void UpdateDesktop(){
   if(store.Data.DesktopEnabled){
    if(desktop==null){desktop=new DesktopWidget(Window,store,Reveal,UpdateDesktopStatus,MoveMonth,SelectDate,()=>SelectDate(DateTime.Today),SafeSave,ManualSync,suppressNative,item=>{Reveal();EditEvent(item);});desktop.Show();}
    else desktop.Show();
    desktop.Render(month,selected);desktop.SyncStatus(CalendarSyncStatus(),syncing);
   }else if(desktop!=null){desktop.Hide();}
   UpdateDesktopStatus();
  }
  string DesktopInfo(){return !store.Data.DesktopEnabled?"바탕화면 위젯 꺼짐":desktop==null?"위젯 준비 중":suppressNative?"검증 모드 · 바탕화면 연결은 건너뜁니다.":desktop.Host.Attached?"독립 위젯을 표시하고 앱 뒤에 유지합니다.":desktop.Host.Error??"바탕화면 연결 대기 중";}
  void UpdateDesktopStatus(){Find<System.Windows.Controls.TextBlock>("DesktopStatus").Text=!store.Data.DesktopEnabled?"바탕화면 위젯 꺼짐":desktop!=null&&desktop.Host.Attached?"● 바탕화면 위젯 켜짐":"위젯 표시 중 · 고정 상태는 설정에서 확인";Find<System.Windows.Controls.Button>("WidgetToggle").Content=store.Data.DesktopEnabled?"위젯 켜짐":"바탕화면 위젯";}
  internal void RefreshPreview(){RenderCalendar();RenderTasks();}
 }
}
