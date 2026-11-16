using System;
using System.Linq;
using System.IO;
using System.Windows.Controls;
namespace DeskFlow {
 static class DailyMessage {
  // Original encouragement written for Nanal; these are not attributed quotations.
  internal static readonly string[] Messages={
   "작은 한 걸음도, 분명 앞으로 가는 중입니다.",
   "오늘의 가능성은 아직 다 펼쳐지지 않았습니다.",
   "서두르지 않아도, 나만의 계절은 찾아옵니다.",
   "지금의 정성이 내일의 나를 든든하게 합니다.",
   "완벽한 하루보다, 나다운 하루면 충분합니다.",
   "잠깐 쉬어 가도 꿈은 사라지지 않습니다.",
   "어제보다 조금 가벼운 마음으로 시작해 보세요.",
   "해낸 일 하나가 오늘의 빛이 됩니다.",
   "오늘도 다시 시작할 자리는 남아 있습니다.",
   "아직 모르는 좋은 날들이 기다리고 있습니다.",
   "작게 시작해도, 시작한 마음은 작지 않습니다.",
   "나를 믿는 연습도 조금씩 쌓여 갑니다.",
   "길이 보이지 않을 때도 발걸음은 남습니다.",
   "한 번의 결과가 나의 전부는 아닙니다.",
   "오늘 할 수 있는 만큼이면 괜찮습니다.",
   "마음에 여백을 두면 새로운 것이 들어옵니다.",
   "조용히 쌓인 시간이 나의 힘이 됩니다.",
   "잘하고 싶은 마음만큼, 나를 다정하게 대해 주세요.",
   "조금 느린 날에도 소중한 하루는 흐릅니다.",
   "지나온 시간 속에 이미 많은 용기가 있습니다.",
   "하나를 마쳤다면, 스스로에게 잘했다고 말해 주세요.",
   "지금의 작은 선택이 새로운 길을 열 수 있습니다.",
   "오늘 만날 작은 기쁨을 놓치지 마세요.",
   "마음을 다한 시간은 나에게 남습니다.",
   "내 속도로 가는 길도 충분히 아름답습니다.",
   "막막한 하루에도 할 수 있는 한 가지는 있습니다.",
   "어제의 나에게 고맙고, 오늘의 나를 응원합니다.",
   "다시 해 보는 마음이 다음 장을 엽니다.",
   "조금의 용기로도 하루는 달라질 수 있습니다.",
   "나를 위한 작은 약속 하나를 지켜 보세요.",
   "모든 답을 알아야 출발할 수 있는 것은 아닙니다.",
   "평범한 오늘도 나의 이야기를 이어 갑니다.",
   "숨을 고르고, 눈앞의 한 걸음부터 시작해 보세요.",
   "비교를 잠시 내려놓으면 나의 길이 보입니다.",
   "쉬어 가는 시간도 나를 돌보는 시간입니다.",
   "애쓴 마음을 알아주는 첫 사람이 되어 주세요.",
   "작은 변화를 반기는 하루가 되기를 바랍니다.",
   "아직 서툴러도 배워 가는 나는 소중합니다.",
   "오늘의 시도가 내일의 선택지를 넓힙니다.",
   "좋은 날은 때로 작은 순간의 모습으로 찾아옵니다.",
   "내 마음에 다정한 말 한마디를 건네 보세요.",
   "할 수 있는 일부터 차근차근 풀어 가면 됩니다.",
   "긴 하루에도 나를 위한 쉼표를 남겨 두세요.",
   "걸어온 길을 돌아보면 생각보다 멀리 왔습니다.",
   "기다리는 시간에도 나의 하루는 자라고 있습니다.",
   "실수한 하루에도 배운 것은 남아 있습니다.",
   "작은 성취를 크게 기뻐해도 괜찮습니다.",
   "오늘의 나에게 필요한 속도를 찾아보세요.",
   "다음 한 걸음은 언제든 새롭게 고를 수 있습니다.",
   "누군가의 기준보다 나의 마음을 살펴보세요.",
   "꾸준함은 매번 다시 돌아오는 마음이기도 합니다.",
   "잘 쉬고 다시 걷는 나를 응원합니다.",
   "눈에 띄지 않는 노력도 나를 이루고 있습니다.",
   "오늘 하루를 다 채우지 않아도 괜찮습니다.",
   "불안한 마음과 함께라도 한 걸음 걸을 수 있습니다.",
   "미뤄 둔 작은 시작을 오늘 만나 보세요.",
   "마음이 향하는 곳에 잠시 귀를 기울여 보세요.",
   "스스로를 믿어 준 순간들이 나를 지켜 줍니다.",
   "내일의 나에게 건넬 작은 선물을 남겨 보세요.",
   "처음의 설렘을 떠올리며 천천히 이어 가세요.",
   "지금의 나를 인정하는 데서 새 힘이 생깁니다.",
   "한 가지를 덜어 내는 것도 좋은 선택입니다.",
   "끝내지 못한 일보다 이어 갈 가능성을 보세요.",
   "기분 좋은 순간 하나를 마음에 담아 두세요.",
   "계획이 달라져도 나의 가치는 달라지지 않습니다.",
   "나를 돌보는 일도 오늘의 중요한 일정입니다.",
   "작은 친절 하나가 하루를 따뜻하게 만듭니다.",
   "오늘의 용기는 어제와 다른 모습이어도 됩니다.",
   "무거운 마음은 잠시 내려놓고 숨을 쉬어 보세요.",
   "한 걸음씩 쌓아 온 나를 잊지 마세요.",
   "새로운 아침에는 새로운 여지가 있습니다.",
   "오늘도 나의 하루를 나답게 이어 갑니다."
  };
  internal static string ForDate(DateTime day){return Messages[(int)((day.Date.Ticks/TimeSpan.TicksPerDay)%Messages.Length)];}
  internal static void Verify(string root){
   DateTime date=new DateTime(2026,12,31);if(ForDate(date)!=ForDate(date.AddHours(23))||ForDate(date)==ForDate(date.AddDays(1))||Messages.Distinct().Count()!=Messages.Length)throw new Exception("Daily message stability/rotation failed");
   if(ForDate(new DateTime(2028,2,29))==ForDate(new DateTime(2028,3,1)))throw new Exception("Daily message leap-day rotation failed");
   File.WriteAllText(Path.Combine(root,"daily-message-result.txt"),"PASS: same local date stays stable across time/restart, adjacent date/year/leap-day changes, unique original messages. No network dependency.");
  }
 }
 partial class Widget {
  internal void RefreshDailyHeader(DateTime date){Find<TextBlock>("DailyMessage").Text=DeskFlow.DailyMessage.ForDate(date);Find<TextBlock>("TodayLabel").Text=date.ToString("yyyy년 M월 d일 dddd",System.Globalization.CultureInfo.GetCultureInfo("ko-KR"))+" · 필요한 것만, 한눈에";}
 }
}
