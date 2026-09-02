using System.Runtime.CompilerServices;

/*///////////////////////////////////////////
                AssemblyInfo
목적 : 게임 코드 전체가 들어있는 기본 어셈블리(Assembly-CSharp)의 internal 멤버를
       에디터 어셈블리(Assembly-CSharp-Editor)에 공개한다.
       EditMode 테스트는 Assets/Tests/Editor 에 두어 Assembly-CSharp-Editor로 컴파일되는데,
       테스트 대상 멤버가 internal로 선언된 경우 이 특성이 없으면 테스트 코드에서 접근할 수 없다.

       [왜 별도 테스트 asmdef를 만들지 않았는가]
       Unity 2022.3 매뉴얼(assembly-definition-files)에 명시: "Classes in assemblies created
       with an Assembly Definition cannot use types defined in the predefined assemblies."
       즉 asmdef로 만든 테스트 어셈블리는 이 프로젝트의 모든 게임 코드가 들어있는
       Assembly-CSharp을 참조할 수 없다(참조 목록에 이름을 적어도 해석되지 않음 —
       CompilationPipeline.GetAssemblyDefinitionFilePathFromAssemblyName("Assembly-CSharp")가
       null을 반환. Assembly-CSharp은 asmdef로 정의된 어셈블리가 아니기 때문).
       반면 Assembly-CSharp-Editor는 Assembly-CSharp을 이미 참조하고 nunit.framework 및
       TestRunner 어셈블리 참조와 UNITY_INCLUDE_TESTS 심볼도 이미 갖고 있어,
       asmdef 없이 그대로 EditMode 테스트를 담을 수 있다.
 *///////////////////////////////////////////

[assembly: InternalsVisibleTo("Assembly-CSharp-Editor")]
