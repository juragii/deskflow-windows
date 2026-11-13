using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
namespace DeskFlow {
 public sealed class TimePicker:Grid {
  public readonly TextBox Hour=new TextBox(),Minute=new TextBox();bool changing;
  public event EventHandler TimeChanged;
  public TimePicker(){
   Width=156;MinHeight=38;ColumnDefinitions.Add(new ColumnDefinition());ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});ColumnDefinitions.Add(new ColumnDefinition());
   AddSegment(Hour,0,24,"시 · 직접 입력 또는 위아래 버튼/방향키로 조절");AddSegment(Minute,2,60,"분 · 1분 단위로 자유롭게 입력");
   var colon=new TextBlock{Text=":",Margin=new Thickness(5,0,5,0),Foreground=Brushes.SlateGray,VerticalAlignment=VerticalAlignment.Center};SetColumn(colon,1);Children.Add(colon);Text="09:00";
  }
  void AddSegment(TextBox box,int column,int range,string tip){
   var grid=new Grid();grid.ColumnDefinitions.Add(new ColumnDefinition());grid.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(20)});
   var surface=new Border{Background=new SolidColorBrush(Color.FromRgb(242,244,246)),CornerRadius=new CornerRadius(9),Child=grid};SetColumn(surface,column);Children.Add(surface);
   box.Style=null;box.BorderThickness=new Thickness(0);box.Background=Brushes.Transparent;box.FontSize=14;box.MaxLength=2;box.TextAlignment=TextAlignment.Center;box.VerticalContentAlignment=VerticalAlignment.Center;box.Padding=new Thickness(2);box.ToolTip=tip;grid.Children.Add(box);
   Action<int> step=delta=>{int n;if(!int.TryParse(box.Text,out n))n=0;box.Text=((n+delta+range)%range).ToString("00");};
   var arrows=new StackPanel{VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(arrows,1);grid.Children.Add(arrows);
   foreach(int delta in new[]{1,-1}){var button=new Button{Content=delta==1?"▴":"▾",FontSize=10,Padding=new Thickness(2,0,2,0),Height=17,Focusable=false,ToolTip=delta==1?"1 증가":"1 감소"};int change=delta;button.Click+=(s,e)=>step(change);arrows.Children.Add(button);}
   box.TextChanged+=(s,e)=>Changed();box.PreviewTextInput+=(s,e)=>e.Handled=!e.Text.All(char.IsDigit);
   DataObject.AddPastingHandler(box,(s,e)=>{string text=e.DataObject.GetData(typeof(string)) as string;if(text==null||text.Length>2||!text.All(char.IsDigit))e.CancelCommand();});
   box.GotKeyboardFocus+=(s,e)=>box.Dispatcher.BeginInvoke(new Action(()=>box.SelectAll()));
   box.LostKeyboardFocus+=(s,e)=>{int value;if(int.TryParse(box.Text,out value)&&value>=0&&value<range)box.Text=value.ToString("00");};
   box.PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Up||e.Key==Key.Down){step(e.Key==Key.Up?1:-1);e.Handled=true;}};
  }
  void Changed(){if(!changing&&TimeChanged!=null)TimeChanged(this,EventArgs.Empty);}
  public string Text{get{return Hour.Text+":"+Minute.Text;}set{changing=true;try{var parts=(value??"").Split(':');Hour.Text=parts.Length>0?parts[0]:"";Minute.Text=parts.Length>1?parts[1]:"";}finally{changing=false;}Changed();}}
  public bool TryTime(out TimeSpan time){int h,m;time=TimeSpan.Zero;if(!int.TryParse(Hour.Text,NumberStyles.None,CultureInfo.InvariantCulture,out h)||!int.TryParse(Minute.Text,NumberStyles.None,CultureInfo.InvariantCulture,out m)||h<0||h>23||m<0||m>59)return false;time=new TimeSpan(h,m,0);return true;}
 }
}
