using R3;
using UnityEngine;

/*///////////////////////////////////////////
                PlayerSlider
목적 : 배틀씬 HUD의 HP/EXP 슬라이더를 DDOL로 살아있는 Player(HP)와
       이 씬의 BattleManager(EXP)에 바인딩하는 View. 씬마다 새로 생기고
       씬 언로드 시 AddTo(this)로 구독이 정리된다 (AimView와 같은 패턴)
 *///////////////////////////////////////////
public sealed class PlayerSlider : MonoBehaviour
{
    [SerializeField] private SliderImage m_refHPSliderImage = null;
    [SerializeField] private SliderImage m_refExSliderImage = null;

    // Start: 같은 씬 BattleManager.Awake 이후 보장. Player.Start는 이보다 늦게(SetActive 후) 돌지만
    // ReactiveProperty라 값이 들어오는 순간 콜백이 오므로 상관없음
    private void Start()
    {
        Player refPlayer = Player.CurrentPlayer;
        if (refPlayer == null)
            return;

        ObjectInfo refObjInfo = refPlayer.ObjectInfo;
        m_refHPSliderImage.SetRange(refObjInfo.MaxHP, refObjInfo.CurrentHP.Value);
        refObjInfo.CurrentHP.Subscribe(_lHp => m_refHPSliderImage.UpdateSlider(_lHp, refObjInfo.MaxHP)).AddTo(this);

        // EXP는 BattleManager 소유 지표 — 구독으로만 UI 갱신
        BattleManager refBattle = BattleManager.m_Instance;
        m_refExSliderImage.SetRange(refBattle.MaxExp, refBattle.Exp.CurrentValue);
        refBattle.Exp.Subscribe(_iExp => m_refExSliderImage.UpdateSlider(_iExp, refBattle.MaxExp)).AddTo(this);

        // ExSlider가 실제로 Max까지 다 찬 시점에 0부터 다시 채우는 연출 후 레벨업(카드 UI)을 확정
        m_refExSliderImage.OnFillMaxReached.Subscribe(_ =>
        {
            m_refExSliderImage.SetRange(refBattle.MaxExp, 0.0f);
            refBattle.LevelUp();
        }).AddTo(this);
    }
}
