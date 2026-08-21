using NUnit.Framework;

/*///////////////////////////////////////////
           ChargeShotBehaviorTests
목적 : SOChargeShotBehavior.GetChargeRatio(차징 진행률 계산 순수 함수)를 씬 없이 검증한다.
       계획서(플레이어 차지 샷 무기) §완료 기준 5가지를 그대로 테스트 케이스로 옮김.
 *///////////////////////////////////////////
public class ChargeShotBehaviorTests
{
    [Test]
    public void GetChargeRatio_경과시간이_0이면_0을_반환한다()
    {
        float fRatio = SOChargeShotBehavior.GetChargeRatio(_fElapsedTime: 0f, _fMaxChargeTime: 1.5f);

        Assert.AreEqual(0f, fRatio, 0.0001f);
    }

    [Test]
    public void GetChargeRatio_경과시간이_최대차지시간과_같으면_1을_반환한다()
    {
        float fRatio = SOChargeShotBehavior.GetChargeRatio(_fElapsedTime: 1.5f, _fMaxChargeTime: 1.5f);

        Assert.AreEqual(1f, fRatio, 0.0001f);
    }

    [Test]
    public void GetChargeRatio_경과시간이_최대차지시간을_초과해도_1로_클램프된다()
    {
        float fRatio = SOChargeShotBehavior.GetChargeRatio(_fElapsedTime: 3.0f, _fMaxChargeTime: 1.5f);

        Assert.AreEqual(1f, fRatio, 0.0001f);
    }

    [Test]
    public void GetChargeRatio_경과시간이_절반이면_0_5를_반환한다()
    {
        float fRatio = SOChargeShotBehavior.GetChargeRatio(_fElapsedTime: 0.75f, _fMaxChargeTime: 1.5f);

        Assert.AreEqual(0.5f, fRatio, 0.0001f);
    }

    [Test]
    public void GetChargeRatio_최대차지시간이_0이하이면_0으로_나누기_없이_즉시_1을_반환한다()
    {
        float fRatio = SOChargeShotBehavior.GetChargeRatio(_fElapsedTime: 0f, _fMaxChargeTime: 0f);

        Assert.AreEqual(1f, fRatio, 0.0001f);
    }
}
