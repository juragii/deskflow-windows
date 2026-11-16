using System;
using System.Linq;
using System.IO;
using System.Windows.Controls;
namespace DeskFlow {
 sealed class DailyQuote {
  public readonly string Text,Author,Work,Original,Source,Edition;
  public DailyQuote(string text,string author,string work,string original,string source,string edition="영어 원문"){Text=text;Author=author;Work=work;Original=original;Source=source;Edition=edition;}
  public string Credit {get{return "— "+Author+" · "+Work;}}
 }
 static class DailyMessage {
  // Public-domain works/speeches; Korean translations rendered for Nanal.
  internal static readonly DailyQuote[] Quotes={
   new DailyQuote("낙관은 성취로 이끄는 믿음이다. 희망 없이는 아무것도 이룰 수 없다.","헬렌 켈러","『낙관주의』 (1903)","Optimism is the faith that leads to achievement; nothing can be done without hope.","https://www.afb.org/about-afb/history/helen-keller/books-essays-speeches/optimism-1903"),
   new DailyQuote("천 리 길도 첫걸음에서 시작된다.","노자","『도덕경』 64장","千里之行，始於足下。","https://ctext.org/text.pl?if=en&node=11655&show=parallel","한문 본문"),
   new DailyQuote("우리가 두려워해야 할 유일한 것은 두려움 그 자체다.","프랭클린 D. 루스벨트","제1차 취임 연설 (1933)","the only thing we have to fear is fear itself","https://www.fdrlibrary.org/first-inaugural-curriculum-hub"),
   new DailyQuote("말을 잘하는 것보다 행동을 잘하는 것이 낫다.","벤저민 프랭클린","『가난한 리처드의 연감』","Well done is better than well said.","https://www.gutenberg.org/files/36151/36151-h/36151-h.htm"),
   new DailyQuote("좋은 사람이 어떠해야 하는지 더는 말하지 말고, 그런 사람이 되어라.","마르쿠스 아우렐리우스","『명상록』 10권 16절","No longer talk at all about the kind of man that a good man ought to be, but be such.","https://classics.mit.edu/Antoninus/meditations.10.ten.html","참고 본문: George Long의 영어 번역 (1862)"),
   new DailyQuote("자신에게 평화를 가져다줄 수 있는 것은 자신뿐이다.","랠프 월도 에머슨","『자기 신뢰』 (1841)","Nothing can bring you peace but yourself.","https://anthology.lib.virginia.edu/work/Emerson/emerson-self-reliance"),
   new DailyQuote("배우고 때때로 익히면 또한 기쁘지 않은가.","공자","『논어』 학이편 1장","學而時習之，不亦說乎？","https://ctext.org/analects/xue-er","한문 본문"),
   new DailyQuote("하루의 질을 바꾸는 것, 그것이 가장 높은 예술이다.","헨리 데이비드 소로","『월든』 「나는 어디서, 무엇을 위해 살았는가」","To affect the quality of the day, that is the highest of arts.","https://www.gutenberg.org/cache/epub/205/pg205-images.html"),
   new DailyQuote("우리에게 달린 일이 있고, 우리에게 달리지 않은 일도 있다.","에픽테토스","『엥케이리디온』 1장","Some things are in our control and others not.","https://classics.mit.edu/Epictetus/epicench.html","참고 본문: Elizabeth Carter의 영어 번역 (1758)"),
   new DailyQuote("인내할 수 있는 사람은 원하는 것을 얻을 수 있다.","벤저민 프랭클린","『가난한 리처드의 연감』","He that can have patience, can have what he will.","https://www.gutenberg.org/files/36151/36151-h/36151-h.htm"),
   new DailyQuote("자신을 믿어라.","랠프 월도 에머슨","『자기 신뢰』 (1841)","Trust thyself","https://anthology.lib.virginia.edu/work/Emerson/emerson-self-reliance"),
   new DailyQuote("우리가 깨어 있는 그날만이 밝아온다.","헨리 데이비드 소로","『월든』 「맺음말」","Only that day dawns to which we are awake.","https://www.gutenberg.org/cache/epub/205/pg205-images.html")
  };
  internal static DailyQuote EntryForDate(DateTime day){return Quotes[(int)((day.Date.Ticks/TimeSpan.TicksPerDay)%Quotes.Length)];}
  internal static string ForDate(DateTime day){return EntryForDate(day).Text;}
  internal static void Verify(string root){
   DateTime date=new DateTime(2026,12,31);if(ForDate(date)!=ForDate(date.AddHours(23))||ForDate(date)==ForDate(date.AddDays(1))||Quotes.Select(q=>q.Text).Distinct().Count()!=Quotes.Length)throw new Exception("Daily quote stability/rotation failed");
   foreach(var quote in Quotes){Uri uri;if(string.IsNullOrWhiteSpace(quote.Author)||string.IsNullOrWhiteSpace(quote.Work)||string.IsNullOrWhiteSpace(quote.Original)||!Uri.TryCreate(quote.Source,UriKind.Absolute,out uri)||uri.Scheme!="https")throw new Exception("Daily quote attribution/source missing");}
   if(ForDate(new DateTime(2028,2,29))==ForDate(new DateTime(2028,3,1)))throw new Exception("Daily quote leap-day rotation failed");
   File.WriteAllText(Path.Combine(root,"daily-message-result.txt"),"PASS: local-date stability, date/year/leap-day rotation, unique translated quotes, author/work/excerpt/HTTPS source required for all entries. Source texts checked separately during curation; displayed quotes work offline.");
  }
 }
 partial class Widget {
  DailyQuote displayedQuote;
  internal void RefreshDailyHeader(DateTime date){displayedQuote=DeskFlow.DailyMessage.EntryForDate(date);Find<TextBlock>("DailyMessage").Text=displayedQuote.Text;Find<TextBlock>("DailyQuoteAuthor").Text=displayedQuote.Credit;Find<TextBlock>("DailyMessage").ToolTip=displayedQuote.Credit+"\n한국어 번역 · 원문·출처에서 인용 문장을 확인하세요.";Find<TextBlock>("TodayLabel").Text=date.ToString("yyyy년 M월 d일 dddd",System.Globalization.CultureInfo.GetCultureInfo("ko-KR"))+" · 필요한 것만, 한눈에";}
  internal void ShowDailyQuoteSource(){var quote=displayedQuote;if(quote==null)return;var p=new StackPanel{Margin=new System.Windows.Thickness(24)};p.Children.Add(Text(quote.Text,21,"#191F28"));var credit=Text(quote.Credit,13,"#4E5968");credit.Margin=new System.Windows.Thickness(0,12,0,20);p.Children.Add(credit);p.Children.Add(Text(quote.Edition+" · 발췌",11,"#8B95A1"));var original=Text(quote.Original,15);original.Margin=new System.Windows.Thickness(0,8,0,20);p.Children.Add(original);p.Children.Add(Text("한국어 문장은 해당 인용 문장을 바탕으로 나날에서 번역했습니다.",11,"#8B95A1"));p.Children.Add(Btn("출처 문서 열기 ↗",()=>Open(quote.Source)));var w=Dialog("오늘의 명언 · 원문과 출처",p,440);w.Width=600;p.Children.Add(Btn("닫기",()=>w.Close()));w.ShowDialog();}
 }
}
