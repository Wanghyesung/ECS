/*///////////////////////////////////////////
                ISaveLoadable
목적 : 저장 대상이 자기 JSON 파일을 읽고 쓰는 공통 규약.
      ProfileSave가 변경된 대상만 저장하거나 시작 시 목록 전체를 로드할 때 사용한다.
 *///////////////////////////////////////////

public interface ISaveLoadable
{
    string FileName { get; }
    bool Load(string _strJson);
    string Save();
}
