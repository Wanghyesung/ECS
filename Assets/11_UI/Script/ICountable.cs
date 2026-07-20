//카테고리(CategoryData)가 보여줄 SO의 수량/레벨을 어디서 가져올지 추상화하는 인터페이스
//FeatureManager뿐 아니라 향후 InventoryManager 등도 이를 구현해 카테고리별로 갈아끼울 수 있음
public interface ICountable
{
    int GetCount(SOFeature _refSO);
}
