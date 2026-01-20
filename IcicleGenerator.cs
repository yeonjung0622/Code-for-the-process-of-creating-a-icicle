using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// L-System을 사용하여 고드름과 같은 구조를 생성합니다.
/// 이 컴포넌트는 L-System 문자열을 해석하여 궤적을 만들고,
/// 그 경로를 따라 메쉬를 생성합니다.
/// </summary>
public class IcicleGenerator : MonoBehaviour
{
    [Header("L-System 파라미터")]
    [Tooltip("고드름 구조를 생성할 L-System 문자열입니다.")]
    public string lSystemString = "F[+F][-F]F[+F][-F]";

    [Tooltip("고드름의 각 세그먼트(마디) 길이입니다.")]
    public float segmentLength = 0.5f;

    [Tooltip("회전 연산(+, -)에 사용할 각도입니다.")]
    public float turnAngle = 10.0f;

    // 생성된 모든 고드름 게임 오브젝트를 추적하기 위한 private 리스트입니다.
    private readonly List<GameObject> generatedIcicles = new List<GameObject>();

    /// <summary>
    /// L-System의 분기 처리를 위해 상태(위치 및 회전)를 저장하는 private 보조 클래스입니다.
    /// </summary>
    private class IcicleState
    {
        public Vector3 position;
        public Quaternion rotation;

        public IcicleState(Vector3 pos, Quaternion rot)
        {
            position = pos;
            rotation = rot;
        }
    }

    /// <summary>
    /// 이전에 생성된 고드름을 모두 지우고 새 고드름을 만듭니다.
    /// </summary>
    public void Build()
    {
        // 중복 생성을 방지하기 위해 새 고드름을 만들기 전에 이전 항목을 지웁니다.
        Clear();

        // 고드름의 궤적 포인트를 생성합니다.
        List<Vector3> trajectoryPoints = BuildTrajectory(lSystemString, segmentLength);

        // 궤적 포인트로부터 눈에 보이는 고드름 메쉬를 생성합니다.
        // 궤적이 유효하지 않을 경우 빈 오브젝트가 생성되는 것을 방지합니다.
        if (trajectoryPoints.Count > 1)
        {
            CreateIcicleMesh(trajectoryPoints);
        }
    }

    /// <summary>
    /// 이 스크립트에 의해 생성된 모든 고드름 게임 오브젝트를 파괴합니다.
    /// 에디터 모드와 플레이 모드에서의 파괴를 모두 처리합니다.
    /// </summary>
    public void Clear()
    {
        // 리스트를 안전하게 수정하기 위해 복사본을 순회합니다.
        foreach (var icicle in generatedIcicles)
        {
            if (Application.isPlaying)
            {
                // 플레이 모드에서는 Destroy를 사용합니다.
                Destroy(icicle);
            }
            else
            {
                // 에디터에서는 즉시 파괴를 위해 DestroyImmediate를 사용합니다.
                DestroyImmediate(icicle);
            }
        }
        // 참조 리스트를 비웁니다.
        generatedIcicles.Clear();
    }

    /// <summary>
    /// L-System 문자열을 해석하여 포인트 리스트(궤적)를 생성합니다.
    /// </summary>
    /// <param name="systemString">구조를 정의하는 L-System 문자열입니다.</param>
    /// <param name="segLength">전진 세그먼트('F')의 길이입니다.</param>
    /// <returns>고드름의 경로를 나타내는 Vector3 포인트 리스트를 반환합니다.</returns>
  public  List<Vector3> BuildTrajectory(string systemString, float segLength)
    {
        var points = new List<Vector3>();
        var stateStack = new Stack<IcicleState>();

        Vector3 currentPosition = Vector3.zero;
        Quaternion currentRotation = Quaternion.identity;

        // 궤적의 시작점을 추가합니다.
        points.Add(currentPosition);

        foreach (char c in systemString)
        {
            switch (c)
            {
                case 'F': // 전진하며 포인트 추가
                    currentPosition += currentRotation * Vector3.forward * segLength;
                    points.Add(currentPosition);
                    break;

                case '+': // 왼쪽으로 회전 (Yaw)
                    currentRotation *= Quaternion.Euler(0, turnAngle, 0);
                    break;

                case '-': // 오른쪽으로 회전 (Yaw)
                    currentRotation *= Quaternion.Euler(0, -turnAngle, 0);
                    break;

                // 참고: L-System에 필요하다면 Pitch, Roll 같은 다른 축 회전도 추가할 수 있습니다.
                // 예: '&'는 아래로 숙이기, '^'는 위로 들기 등

                case '[': // 현재 상태를 스택에 저장 (가지 시작)
                    stateStack.Push(new IcicleState(currentPosition, currentRotation));
                    break;

                case ']': // 스택에서 상태를 복원 (이전 분기점으로 돌아가기)
                    if (stateStack.Count > 0)
                    {
                        IcicleState prevState = stateStack.Pop();
                        currentPosition = prevState.position;
                        currentRotation = prevState.rotation;
                        // 복원된 위치를 새로운 선분의 시작점으로 추가하면 유용할 때가 많습니다.
                        points.Add(currentPosition);
                    }
                    break;
            }
        }
        return points;
    }


    private void CreateIcicleMesh(List<Vector3> trajectoryPoints)
    {
        // 고드름 시각화를 담을 새 게임 오브젝트를 생성합니다.
        var icicleObject = new GameObject("GeneratedIcicle");
        // 이 오브젝트의 자식으로 설정합니다.
        icicleObject.transform.SetParent(this.transform, false);

        // 궤적을 그리기 위해 LineRenderer 컴포넌트를 추가합니다.
        var lineRenderer = icicleObject.AddComponent<LineRenderer>();
        lineRenderer.positionCount = trajectoryPoints.Count;
        lineRenderer.SetPositions(trajectoryPoints.ToArray());

        // 라인의 기본 스타일을 설정합니다.
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.02f;
        lineRenderer.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        lineRenderer.startColor = Color.white;
        lineRenderer.endColor = new Color(1, 1, 1, 0.5f);

        // 나중에 정리할 수 있도록 생성된 오브젝트를 추적 리스트에 추가합니다.
        generatedIcicles.Add(icicleObject);
    }
}