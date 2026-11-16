using System;
using System.Linq;
using System.Collections.Generic;
namespace DeskFlow {
 static class QuoteCatalog {
  static void Add(List<DailyQuote> list,string author,string work,string url,string edition,string rows){foreach(string raw in rows.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){var p=raw.Split('|');if(p.Length!=2)throw new Exception("Invalid quotation catalog row");list.Add(new DailyQuote(p[1],author,work,p[0],url,edition));}}
  static void Analects(List<DailyQuote> list,string slug,string chapter,string rows,string author="공자"){Add(list,author,"『논어』 「"+chapter+"」","https://ctext.org/analects/"+slug,"한문 본문 · 발췌",rows);}
  static void Marcus(List<DailyQuote> list,string book,string slug,string rows){Add(list,"마르쿠스 아우렐리우스","『명상록』 "+book+"권","https://classics.mit.edu/Antoninus/meditations."+slug+".html","참고 본문: George Long의 영어 번역 (1862) · 발췌",rows);}
  internal static DailyQuote[] Expand(DailyQuote[] previous){var list=new List<DailyQuote>();
   Analects(list,"xue-er","학이",@"吾日三省吾身。|나는 날마다 세 가지로 나 자신을 돌아본다.","증자");
   Analects(list,"xue-er","학이",@"不患人之不己知，患不知人也。|남이 나를 알아주지 않음을 걱정하지 말고, 내가 남을 알지 못함을 걱정하라.
過則勿憚改。|잘못이 있으면 고치기를 두려워하지 마라.
行有餘力，則以學文。|행할 일을 다하고 힘이 남거든 글을 배워라.");
   Analects(list,"wei-zheng","위정",@"溫故而知新，可以為師矣。|옛것을 익혀 새것을 알면 스승이 될 수 있다.
學而不思則罔，思而不學則殆。|배우기만 하고 생각하지 않으면 어둡고, 생각만 하고 배우지 않으면 위태롭다.
知之為知之，不知為不知，是知也。|아는 것을 안다고 하고 모르는 것을 모른다고 하는 것이 앎이다.
先行其言，而後從之。|말하려는 것을 먼저 실행하고, 말은 그 뒤를 따르게 하라.");
   Analects(list,"li-ren","이인",@"德不孤，必有鄰。|덕은 외롭지 않다. 반드시 이웃이 있다.
君子欲訥於言而敏於行。|군자는 말에는 신중하고 행동에는 민첩하려 한다.
見賢思齊焉，見不賢而內自省也。|훌륭한 사람을 보면 본받고, 그렇지 못한 사람을 보면 자신을 돌아보아라.");
   Analects(list,"gong-ye-chang","공야장",@"聽其言而觀其行。|그 말을 듣고 그 행동을 살펴라.
敏而好學，不恥下問。|배우기를 좋아하고, 아랫사람에게 묻는 것을 부끄러워하지 마라.");
   Analects(list,"yong-ye","옹야",@"文質彬彬，然後君子。|꾸밈과 바탕이 조화를 이룬 뒤에야 군자라 할 수 있다.
知之者不如好之者，好之者不如樂之者。|아는 사람은 좋아하는 사람만 못하고, 좋아하는 사람은 즐기는 사람만 못하다.
知者樂水，仁者樂山。|지혜로운 이는 물을 좋아하고, 어진 이는 산을 좋아한다.
己欲立而立人，己欲達而達人。|자신이 서고자 하면 남도 서게 하고, 자신이 이루고자 하면 남도 이루게 하라.");
   Analects(list,"shu-er","술이",@"默而識之，學而不厭，誨人不倦。|묵묵히 새겨 두고, 배우기를 싫어하지 않으며, 가르치기를 게을리하지 않는다.
三人行，必有我師焉。|세 사람이 함께 가면 반드시 나의 스승이 있다.
發憤忘食，樂以忘憂。|배움에 힘쓰느라 먹는 것을 잊고, 즐거워서 근심을 잊는다.");
   Analects(list,"tai-bo","태백",@"士不可以不弘毅，任重而道遠。|뜻을 품은 이는 넓고 굳세야 한다. 맡은 일은 무겁고 갈 길은 멀기 때문이다.","증자");
   Analects(list,"tai-bo","태백",@"興於詩，立於禮，成於樂。|시에서 마음을 일으키고, 예로써 서며, 음악으로 완성한다.");
   Analects(list,"zi-han","자한",@"逝者如斯夫！不舍晝夜。|흘러가는 것이 이와 같구나. 밤낮을 쉬지 않는구나.
歲寒，然後知松柏之後彫也。|추운 계절이 온 뒤에야 소나무와 잣나무가 늦게 시듦을 안다.
知者不惑，仁者不憂，勇者不懼。|지혜로운 이는 미혹되지 않고, 어진 이는 근심하지 않으며, 용기 있는 이는 두려워하지 않는다.
後生可畏，焉知來者之不如今也？|젊은 후배는 두려워할 만하다. 앞으로 올 이들이 지금의 우리만 못하리라 어찌 알겠는가.");
   Analects(list,"yan-yuan","안연",@"己所不欲，勿施於人。|자신이 원하지 않는 것을 남에게 행하지 마라.
君子成人之美，不成人之惡。|군자는 남의 아름다움을 이루어 주고, 남의 악함을 이루어 주지 않는다.
內省不疚，夫何憂何懼？|스스로 돌아보아 거리낌이 없다면, 무엇을 근심하고 두려워하겠는가.");
   Analects(list,"zi-lu","자로",@"欲速則不達，見小利則大事不成。|빨리 이루려 하면 도달하지 못하고, 작은 이익만 보면 큰일을 이루지 못한다.
君子和而不同。|군자는 조화를 이루되 무작정 같아지지는 않는다.
其身正，不令而行；其身不正，雖令不從。|자신이 바르면 명령하지 않아도 따르고, 자신이 바르지 못하면 명령해도 따르지 않는다.");
   Analects(list,"wei-ling-gong","위령공",@"人無遠慮，必有近憂。|멀리 내다보는 생각이 없으면 반드시 가까운 근심이 생긴다.
君子不以言舉人，不以人廢言。|군자는 말만으로 사람을 높이지 않고, 사람 때문에 옳은 말을 버리지 않는다.
過而不改，是謂過矣。|잘못하고도 고치지 않는 것, 그것이 잘못이다.
有教無類。|가르침에는 차별이 없어야 한다.");
   string mencius="https://sangle.web.wesleyan.edu/etext/pre-qin/mengzi.html";
   Add(list,"맹자","『맹자』 「진심상」",mencius,"한문 본문 · 발췌",@"窮則獨善其身，達則兼善天下。|뜻을 얻지 못하면 자신을 닦고, 뜻을 얻으면 천하를 함께 이롭게 하라.");
   Add(list,"맹자","『맹자』 「이루하」",mencius,"한문 본문 · 발췌",@"愛人者，人恆愛之；敬人者，人恆敬之。|남을 사랑하는 이는 남도 늘 사랑하고, 남을 공경하는 이는 남도 늘 공경한다.
人有不為也，而後可以有為。|옳지 않은 일을 하지 않아야, 비로소 옳은 일을 할 수 있다.");
   Add(list,"맹자","『맹자』 「고자상」",mencius,"한문 본문 · 발췌",@"學問之道無他，求其放心而已矣。|학문의 길은 다른 데 있지 않다. 잃어버린 마음을 되찾는 것일 뿐이다.");
   string tao=@"8|上善若水。水善利萬物而不爭。|가장 높은 선은 물과 같다. 물은 만물을 이롭게 하면서도 다투지 않는다.
9|功遂身退，天之道。|공을 이룬 뒤 물러나는 것이 하늘의 도다.
11|有之以為利，無之以為用。|있는 것이 이로움을 만들고, 없는 것이 쓰임을 만든다.
16|致虛極，守靜篤。|마음을 지극히 비우고, 고요함을 굳게 지켜라.
22|曲則全，枉則直。|굽히면 온전해지고, 휘어지면 곧아진다.
24|企者不立，跨者不行。|발끝으로 선 이는 바로 서지 못하고, 무리하게 보폭을 벌린 이는 나아가지 못한다.
26|重為輕根，靜為躁君。|무거움은 가벼움의 뿌리요, 고요함은 조급함의 주인이다.
27|善人者，不善人之師；不善人者，善人之資。|선한 사람은 선하지 않은 사람의 스승이며, 선하지 않은 사람은 선한 사람의 배움의 바탕이다.
29|去甚，去奢，去泰。|지나침과 사치와 교만을 버려라.
33|知人者智，自知者明。|남을 아는 이는 지혜롭고, 자신을 아는 이는 밝다.
33|勝人者有力，自勝者強。|남을 이기는 이는 힘이 있고, 자신을 이기는 이는 강하다.
33|知足者富。|만족할 줄 아는 이가 부유하다.
37|道常無為而無不為。|도는 늘 억지로 행하지 않으면서도 이루지 않는 것이 없다.
40|反者道之動；弱者道之用。|되돌아감은 도의 움직임이고, 부드러움은 도의 쓰임이다.
44|知足不辱，知止不殆，可以長久。|만족할 줄 알면 욕되지 않고, 멈출 줄 알면 위태롭지 않아 오래갈 수 있다.
46|知足之足，常足矣。|만족할 줄 아는 만족은 언제나 넉넉하다.
48|為學日益，為道日損。|배움을 위해서는 날마다 더하고, 도를 위해서는 날마다 덜어 낸다.
49|聖人無常心，以百姓心為心。|성인은 고집하는 마음이 없고, 사람들의 마음을 자신의 마음으로 삼는다.
56|知者不言，言者不知。|아는 이는 함부로 말하지 않고, 말만 하는 이는 알지 못한다.
63|圖難於其易，為大於其細。|어려운 일은 쉬울 때 도모하고, 큰일은 작은 데서 이루어라.
64|慎終如始，則無敗事。|끝을 처음처럼 신중히 하면 일을 그르치지 않는다.
66|江海所以能為百谷王者，以其善下之。|강과 바다가 온갖 골짜기의 으뜸인 것은, 그보다 낮은 곳에 있기 때문이다.
67|慈故能勇。|자애롭기에 용감할 수 있다.
81|信言不美，美言不信。|참된 말은 꾸밈이 없고, 꾸민 말은 참되지 않다.";
   foreach(string raw in tao.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){var p=raw.Split('|');list.Add(new DailyQuote(p[2],"노자","『도덕경』 "+p[0]+"장",p[1],"https://ctext.org/text.pl?if=en&node="+(11591+int.Parse(p[0]))+"&show=parallel","한문 통행본 · 노자 전통적 귀속 · 발췌"));}
   Marcus(list,"2","2.two",@"For we are made for co-operation|우리는 서로 협력하도록 태어났다.
Give thyself time to learn something new and good|새롭고 좋은 것을 배울 시간을 자신에게 주어라.
do what thou hast in hand with perfect and simple dignity|지금 손에 든 일을 온전하고 소박한 품위로 해내라.
if thou doest every act of thy life as if it were the last|삶의 모든 행동을 마치 마지막 행동인 것처럼 하라.");
   Marcus(list,"4","4.four",@"Let no act be done without a purpose|어떤 행동도 목적 없이 하지 마라.
tranquility is nothing else than the good ordering of the mind|평온은 마음을 잘 정돈하는 것일 뿐이다.
The universe is transformation: life is opinion.|우주는 변화이며, 삶은 판단이다.
look at them as they are in truth|사물을 실제 모습 그대로 보아라.");
   Marcus(list,"5","5.five",@"it is necessary to take rest also|휴식 또한 필요하다.
if a thing is good to be done or said, do not consider it unworthy of thee|좋은 말이나 행동이라면 자신에게 어울리지 않는다고 여기지 마라.
Show those qualities then which are altogether in thy power|자신이 발휘할 수 있는 좋은 자질을 드러내라.
Be not disgusted, nor discouraged, nor dissatisfied, if thou dost not succeed in doing everything according to right principles|모든 일을 올바른 원칙대로 해내지 못했다고 낙담하거나 불만을 품지 마라.");
   Marcus(list,"6","6.six",@"Look within.|자신의 내면을 살펴라.
The best way of avenging thyself is not to become like the wrong doer.|가장 좋은 복수는 잘못을 저지른 사람과 같아지지 않는 것이다.
quickly return to thyself and do not continue out of tune longer than the compulsion lasts|곧 자신에게 돌아오고, 상황이 강요한 시간 이상으로 흔들리지 마라.
That which is not good for the swarm, neither is it good for the bee.|벌떼에게 이롭지 않은 것은 한 마리 벌에게도 이롭지 않다.");
   Add(list,"벤저민 프랭클린","『가난한 리처드의 연감』","https://www.gutenberg.org/files/36151/36151-h/36151-h.htm","영어 원문 · 격언 발췌",@"Read much, but not too many books.|많이 읽되, 너무 많은 책을 읽지는 마라.
Search others for their virtues, thyself for thy vices.|남에게서는 장점을 찾고, 자신에게서는 고칠 점을 찾아라.
Forewarned, forearmed.|미리 알면 미리 대비할 수 있다.
None preaches better than the ant, and she says nothing.|개미보다 잘 가르치는 이는 없지만, 개미는 아무 말도 하지 않는다.
He that can compose himself is wiser than he that composes books.|자신의 마음을 다스리는 이는 책을 쓰는 이보다 현명하다.
The noblest question in the world is, What good may I do in it?|세상에서 가장 고귀한 질문은 내가 어떤 좋은 일을 할 수 있는가이다.
After crosses and losses men grow humbler and wiser.|시련과 상실을 겪은 뒤 사람은 더 겸손하고 지혜로워진다.
He that can take rest is greater than he that can take cities.|쉴 줄 아는 이는 도시를 정복하는 이보다 위대하다.");
   if(list.Count!=88)throw new Exception("Expected 88 new sourced quotations, got "+list.Count);
   var random=new Random(20261117);for(int i=list.Count-1;i>0;i--){int j=random.Next(i+1);var tmp=list[i];list[i]=list[j];list[j]=tmp;}
   // Old 12 come last, so an upgrading user sees the newly added material first.
   list.AddRange(previous);return list.ToArray();
  }
 }
}
