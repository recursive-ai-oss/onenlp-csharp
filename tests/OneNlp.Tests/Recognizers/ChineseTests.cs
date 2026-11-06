using RecursiveAI.OneNlp.Recognizers;

namespace RecursiveAI.OneNlp.Tests;

/// <summary>Chinese: numerals without spaces, measures, money, the sequence set around Chinese text, and dates, times and holidays (reference date Wednesday 18 March 2026).</summary>
public class ChineseTests
{
    private static readonly DateTimeOffset Wed = new(2026, 3, 18, 10, 30, 0, TimeSpan.Zero);

    private static string Run(string text, bool withReference = true) =>
        string.Join(" ; ", Recognizer.For(Language.Chinese).Recognize(text, withReference ? new RecognitionContext(Wed) : default).Select(r => r switch
        {
            Quantity q => $"{q.Text}|{q.Kind}|{q.Value}|{q.Unit ?? q.Currency}",
            TemporalMatch t => $"{t.Text}|{t.Kind}|{t.Value}",
            _ => $"{r.Text}|{r.Label}",
        }));

    [Theory]
    [InlineData("三百二十五本书", "三百二十五|Cardinal|325|")]
    [InlineData("一千零五名选手", "一千零五|Cardinal|1005|")]
    [InlineData("三万五千人", "三万五千|Cardinal|35000|")]
    [InlineData("三万五", "三万五|Cardinal|35000|")]
    [InlineData("两百把椅子", "两百|Cardinal|200|")]
    [InlineData("1.5亿", "1.5亿|Cardinal|150000000|")]
    [InlineData("12,345人", "12,345|Cardinal|12345|")]
    [InlineData("三点一四", "三点一四|Cardinal|3.14|")]
    [InlineData("四分之三", "四分之三|Cardinal|0.75|")]
    [InlineData("伍佰贰拾", "伍佰贰拾|Cardinal|520|")]
    [InlineData("她养了两只猫", "两|Cardinal|2|")]
    [InlineData("负三", "负三|Cardinal|-3|")]
    public void Numbers(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("他统一了意见")]
    [InlineData("万一下雨怎么办")]
    [InlineData("一些人认为不行")]
    [InlineData("十分感谢")]
    [InlineData("三角形的面积")]
    [InlineData("三心二意")]
    public void WordsThatContainNumeralsAreNoNumbers(string text) => Assert.Equal(string.Empty, Run(text));

    [Theory]
    [InlineData("第十二名", "第十二|Ordinal|12|")]
    [InlineData("第3页", "第3|Ordinal|3|")]
    [InlineData("百分之五点五", "百分之五点五|Percent|5.5|percent")]
    [InlineData("百分之百", "百分之百|Percent|100|percent")]
    [InlineData("65％", "65％|Percent|65|percent")]
    public void OrdinalsAndPercentages(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("全长十五公里", "十五公里|Measure|15|kilometer")]
    [InlineData("重两千克", "两千克|Measure|2|kilogram")]
    [InlineData("限速100公里/小时", "100公里/小时|Measure|100|kilometer per hour")]
    [InlineData("文件5MB", "5MB|Measure|5|megabyte")]
    [InlineData("零下5度", "零下5度|Measure|-5|degree")]
    [InlineData("摄氏180度", "摄氏180度|Measure|180|celsius")]
    [InlineData("零下8摄氏度", "零下8摄氏度|Measure|-8|celsius")]
    [InlineData("华氏72度", "华氏72度|Measure|72|fahrenheit")]
    [InlineData("他五岁了", "五岁|Measure|5|year")]
    [InlineData("宝宝六个月大", "六个月大|Measure|6|month")]
    public void Measures(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("¥100", "¥100|Money|100|CNY")]
    [InlineData("25元", "25元|Money|25|CNY")]
    [InlineData("五块钱", "五块钱|Money|5|CNY")]
    [InlineData("5元3角", "5元3角|Money|5.3|CNY")]
    [InlineData("人民币100万元", "人民币100万元|Money|1000000|CNY")]
    [InlineData("1.5亿美元", "1.5亿美元|Money|150000000|USD")]
    [InlineData("RMB 500", "RMB 500|Money|500|CNY")]
    public void Money(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("邮箱a.b@example.com联系", "a.b@example.com|EMAIL")]
    [InlineData("请访问https://www.example.com/a?b=1查看", "https://www.example.com/a?b=1|URL")]
    [InlineData("手机13800138000", "13800138000|PHONE_LIKE")]
    [InlineData("电话010-12345678", "010-12345678|PHONE_LIKE")]
    [InlineData("电话138 0013 8000", "138 0013 8000|PHONE_LIKE")]
    [InlineData("大家都在发#春节快乐#这个话题", "#春节快乐#|HASHTAG")]
    [InlineData("转给 @小明 看看", "@小明|MENTION")]
    public void SequencesAroundChineseText(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("2026年3月18日", "2026年3月18日|Date|2026-03-18")]
    [InlineData("二〇二五年十月八日", "二〇二五年十月八日|Date|2025-10-08")]
    [InlineData("明天下午3点", "明天下午3点|DateTime|2026-03-19T15:00")]
    [InlineData("下周五", "下周五|Date|2026-03-27")]
    [InlineData("周一", "周一|Date|2026-03-23")]
    [InlineData("三天后", "三天后|Date|2026-03-21")]
    [InlineData("今晚8点", "今晚8点|DateTime|2026-03-18T20:00")]
    [InlineData("下午三点半", "下午三点半|Time|T15:30")]
    [InlineData("上午9点到11点", "上午9点到11点|TimeRange|T09:00/T11:00")]
    [InlineData("明年春天", "明年春天|DateRange|2027-03-01/2027-06-01")]
    [InlineData("2026年第二季度", "2026年第二季度|DateRange|2026-04-01/2026-07-01")]
    [InlineData("4月6日到4月10日", "4月6日到4月10日|DateRange|2026-04-06/2026-04-11")]
    [InlineData("2026年3月底", "2026年3月底|DateRange|2026-03-21/2026-04-01")]
    [InlineData("春节", "春节|Date|2027-02-06")]
    [InlineData("2025年春节", "2025年春节|Date|2025-01-29")]
    [InlineData("中秋节", "中秋节|Date|2026-09-25")]
    [InlineData("复活节", "复活节|Date|2026-04-05")]
    [InlineData("母亲节", "母亲节|Date|2026-05-10")]
    [InlineData("1990年代", "1990年代|DateRange|1990-01-01/2000-01-01")]
    public void Dates(string text, string expected) => Assert.Equal(expected, Run(text));

    [Theory]
    [InlineData("等了三天", "三天|Duration|259200")]
    [InlineData("半小时", "半小时|Duration|1800")]
    [InlineData("一个半小时", "一个半小时|Duration|5400")]
    [InlineData("一年半", "一年半|Duration|P1Y6M")]
    [InlineData("两年零三个月", "两年零三个月|Duration|P2Y3M")]
    [InlineData("10个工作日", "10个工作日|Duration|P10BD")]
    [InlineData("每周五晚上8点", "每周五晚上8点|Set|XXXX-WXX-5T20:00")]
    [InlineData("每隔两天", "每隔两天|Set|P2D")]
    [InlineData("每月15号", "每月15号|Set|XXXX-XX-15")]
    public void DurationsAndRecurrences(string text, string expected) => Assert.Equal(expected, Run(text));

    [Fact]
    public void RelativeExpressionsNeedAReferenceDate()
    {
        Assert.Equal(string.Empty, Run("明天", withReference: false));
        Assert.Equal("2026年3月18日|Date|2026-03-18", Run("2026年3月18日", withReference: false));
        Assert.Equal("三个小时|Duration|10800", Run("三个小时", withReference: false));
    }

    [Fact]
    public void AYearIsNotAnAmountOfTime() => Assert.Equal("2026年|DateRange|2026-01-01/2027-01-01", Run("2026年"));

    [Fact]
    public void RulesAreLoadedForChineseOnly()
    {
        Assert.Equal(Language.Chinese, Recognizer.Chinese.Language);
        Assert.Equal(string.Empty, string.Join(";", Recognizer.English.Recognize("三百二十五", new RecognitionContext(Wed)).Select(r => r.Text)));
    }
}
