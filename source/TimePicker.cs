using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
namespace DeskFlow {
 public sealed class TimePicker:Grid {
  public readonly ComboBox Hour=new ComboBox(),Minute=new ComboBox();bool changing;
  public event EventHandler TimeChanged;
  public TimePicker(){
   Width=156;MinHeight=34;ColumnDefinitions.Add(new ColumnDefinition());ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});ColumnDefinitions.Add(new ColumnDefinition());
   foreach(var box in new[]{Hour,Minute}){box.IsEditable=true;box.MinHeight=34;box.FontSize=12;box.AddHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler((s,e)=>Changed()));box.PreviewKeyDown+=(s,e)=>{if(e.Key!=Key.Up&&e.Key!=Key.Down)return;int n;if(!int.TryParse(box.Text,out n))n=0;int max=box==Hour?24:60;box.Text=((n+(e.Key==Key.Up?1:max-1))%max).ToString("00");e.Handled=true;};}
   Hour.ToolTip="시 · 0~23 직접 입력 또는 선택";Minute.ToolTip="분 · 0~59 직접 입력 또는 선택";
   for(int i=0;i<24;i++)Hour.Items.Add(i.ToString("00"));for(int i=0;i<60;i++)Minute.Items.Add(i.ToString("00"));
   Children.Add(Hour);var colon=new TextBlock{Text=":",Margin=new Thickness(5,0,5,0),VerticalAlignment=VerticalAlignment.Center};SetColumn(colon,1);Children.Add(colon);SetColumn(Minute,2);Children.Add(Minute);Text="09:00";
  }
  void Changed(){if(!changing&&TimeChanged!=null)TimeChanged(this,EventArgs.Empty);}
  public string Text{get{return Hour.Text+":"+Minute.Text;}set{changing=true;try{var parts=(value??"").Split(':');Hour.Text=parts.Length>0?parts[0]:"";Minute.Text=parts.Length>1?parts[1]:"";}finally{changing=false;}Changed();}}
  public bool TryTime(out TimeSpan time){int h,m;time=TimeSpan.Zero;if(!int.TryParse(Hour.Text,NumberStyles.None,CultureInfo.InvariantCulture,out h)||!int.TryParse(Minute.Text,NumberStyles.None,CultureInfo.InvariantCulture,out m)||h<0||h>23||m<0||m>59)return false;time=new TimeSpan(h,m,0);return true;}
 }
}
