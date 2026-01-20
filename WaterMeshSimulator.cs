using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;


public class WaterMeshSimulator : MonoBehaviour
{
    //인스펙터 접근 불가 변수 (private)
    private MeshFilter meshFilterComponent;
    private Mesh mesh;
    private Vector3[] vertices;
    private Color[] colors;
    private float[] waterAmounts;
    // private bool isSimulationRunning = false;
    //레이 회전 반영 관련 변수
    private Coroutine calculationCoroutine;
    private Quaternion lastRotation;
    public Transform raycastSource;
    //2단계에서 필요한 변수들
    private Vector3[] normals;
    public List<Vector3> dripPoints = new List<Vector3>();

    //3단계에서 필요한 지역 변수들(private), 클래스 인스턴스
    public LSystemGenerator lSystemGenerator;

    public float maxIcicleLength = 30.0f;

    private List<LineRenderer> icicleRenderers = new List<LineRenderer>();
    private Transform icicleContainer;

    //4단계 필요 변수
    //public MCBlob MetaballController;
    public float metaballRadius = 0.03f;
    private List<GameObject> generatedIcicleSegments = new List<GameObject>();


    //각 버텍스 이웃 정보를 저장할 딕셔너리
    private Dictionary<int, List<int>> vertexNeighbors;
    //private float simulationUpdateInterval = 3f;
    // private float timer = 0f;



    //인스펙터 설정 public 변수들
    public float colorChangeRadius = 0.1f;
    public Color minWaterColor = Color.red;
    public Color maxWaterColor = Color.blue;
    public float maxWaterAmount = 1.0f;
    public float waterAddAmount = 0.1f;
    public float waterSupplyThreshold = 0.001f;
    public int verticesPerFrame = 1000;

    public Vector3 currentGravityDirection = Vector3.down;


    //2단계에서 필요한 변수들
    public float dripLimitAngle = 100.0f;
    public int numberOfIcicles = 10;
    public List<int> gizmoVertexIndices;
    public float gizmoSize = 0.5f;
    public int[] vertexIndex;

    //3단계에서 필요한 변수들 (전역변수)
    [Header("Icicle L-System Parameters")]
    public int iterations = 1000;
    [Range(0.0f, 180.0f)]
    public float curvatureAngle = 25.0f;
    [Range(0.0f, 1.0f)]
    public float subdivisionProbability = 0.5f;
    [Range(0.0f, 360.0f)]
    public float userDispersionAngle = 137.5f;
    public float segmentLength = 5f;
    public LayerMask collisionLayers;

    public struct TransformState
    {
        public Vector3 position;
        public Vector3 direction;

        public TransformState(Vector3 pos, Vector3 dir)
        {
            position = pos;
            direction = dir;
        }
    }

    // Start is called before the first frame update

    void Start()
    {
        Debug.Log("Start() 함수 시작!");
        meshFilterComponent = GetComponentInChildren<MeshFilter>();
        icicleContainer = new GameObject("[icicles]").transform;

        //1.메시 필터가 없는 경우
        if (meshFilterComponent == null)
        {
            Debug.LogWarning("MeshFilter가 오브젝트에 없습니다.", this);
            enabled = false;
            return;
        }
        Debug.Log("Start(): Step 1 - MeshFilter 컴포넌트 찾기 성공.", this);

        mesh = meshFilterComponent.mesh;


        //2. 메시 데이터가 없는 경우
        if (mesh == null)
        {
            Debug.LogWarning("메시 데이터가 없습니다.", this);
            enabled = false;
            return;
        }
        Debug.Log("Start(): Step 2 - Mesh 객체 가져오기 성공.", this);
        //버텍스 데이터 초기화
        vertices = mesh.vertices;

        Debug.Log("Start(): Step 3 - BuildVertexNeighbors() 호출 직전.", this);

        //color 배열 초기화 / 기존 컬러 없으면 흰색으로 초기화 

        if (mesh.colors.Length == vertices.Length)
        {
            colors = mesh.colors;
        }

        else
        {
            colors = new Color[vertices.Length];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.white;
            }
        }

        //waterAmounts 배열 초기화 (초기 물 공급 가능성은 0) 
        waterAmounts = new float[vertices.Length];
        for (int i = 0; i < waterAmounts.Length; i++)
        {
            waterAmounts[i] = 0f;
        }


        //이웃 버텍스 정보를 빌드하는 함수 호출
        BuildVertexNeighbors();
        Debug.Log("Start(): Step 4 - BuildVertexNeighbors() 호출 완료.", this);

        vertices = mesh.vertices;
        normals = mesh.normals;

        mesh.colors = colors;


        //isSimulationRunning = false;
        //calculationCoroutine = StartCoroutine(WaterCoefficientCalculateCoroutine());

        lSystemGenerator = GetComponent<LSystemGenerator>();
        if (lSystemGenerator == null)
        {
            lSystemGenerator = gameObject.AddComponent<LSystemGenerator>();
        }

        //  lSystemGenerator.Initialize(curvatureAngle, subdivisionProbability, userDispersionAngle);
        Debug.Log("Start() 초기화 완료.", this);
    }



    // Update is called once per frame
    void Update()
    {
        if (raycastSource == null || raycastSource.rotation == lastRotation)
        {
            return;
        }

        Debug.Log("Rain Controller의 Transform이 변경되었습니다. 물 계수 계산을 다시 시작합니다.");

        if (calculationCoroutine != null)
        {
            StopCoroutine(calculationCoroutine);
        }
        calculationCoroutine = StartCoroutine(WaterCoefficientCalculateCoroutine());
        // 현재 회전 값을 저장하여 다음 프레임과 비교합니다.
        lastRotation = raycastSource.rotation;
    }


    //처음 물 공급원 저장 함수
    public void WaterSupplyVertex(Vector3 worldHitPoint, Vector3 rayDirection)
    {
        if (mesh == null) return;

        this.currentGravityDirection = rayDirection;

        Vector3 localHitPoint = transform.InverseTransformPoint(worldHitPoint);

        for (int i = 0; i < vertices.Length; i++)
        {
            float distance = Vector3.Distance(vertices[i], localHitPoint);
            if (distance < colorChangeRadius)
            {
                waterAmounts[i] = Mathf.Max(waterAmounts[i], waterAddAmount);
            }
        }
    }




    //이웃 정점 찾는 함수(start에서 호출)
    private void BuildVertexNeighbors()
    {
        vertexNeighbors = new Dictionary<int, List<int>>();

        int[] triangles = mesh.triangles;

        for (int i = 0; i < vertices.Length; i++)
        {
            vertexNeighbors.Add(i, new List<int>());
        }

        //삼각형은 3개의 버텍스 인덱스로 구성, i를 3씩 증가시키며 모든 삼각형을 처리
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int v1 = triangles[i];
            int v2 = triangles[i + 1];
            int v3 = triangles[i + 2];

            //각 버텍스에 대해 나머지 두 버텍스를 이웃으로 추가 
            AddUniqueNeighbor(v1, v2);
            AddUniqueNeighbor(v1, v3);
            AddUniqueNeighbor(v2, v1);
            AddUniqueNeighbor(v2, v3);
            AddUniqueNeighbor(v3, v1);
            AddUniqueNeighbor(v3, v2);
        }

        Debug.Log("이웃 버텍스 정보 빌드 완료. 총 버텍스 수 " + vertices.Length);

    }


    //이웃 리스트에 중복 없이 버텍스를 추가하는 함수
    private void AddUniqueNeighbor(int vertexIndex, int neighborIndex)
    {
        if (!vertexNeighbors.ContainsKey(vertexIndex))
        {
            vertexNeighbors.Add(vertexIndex, new List<int>());
        }
        if (!vertexNeighbors[vertexIndex].Contains(neighborIndex))
        {
            vertexNeighbors[vertexIndex].Add(neighborIndex);
        }
    }




    //물 계수 함수 알고리즘
    private IEnumerator WaterCoefficientCalculateCoroutine()
    {


        Vector3 g = Quaternion.Inverse(raycastSource.rotation) * -Vector3.up;
        //Vector3 worldG = (raycastSource != null) ? raycastSource.TransformDirection(Vector3.down) : Vector3.down;
        // Vector3 g = transform.InverseTransformDirection(worldG).normalized;

        float[] newWaterCoefficients = new float[vertices.Length];
        System.Array.Copy(waterAmounts, newWaterCoefficients, waterAmounts.Length);

        //무한 루프 방지
        int maxSearchIterations = vertices.Length * 2;
        int processedVertices = 0;


        for (int v_index = 0; v_index < vertices.Length; v_index++)
        {

            int c_current_index = v_index;
            Vector3 c_position = vertices[c_current_index];
            // 물 계수 값 초기화
            float wc = 0;

            HashSet<int> visitedVertices = new HashSet<int>();
            visitedVertices.Add(c_current_index);

            int currentIterations = 0;

            while (true)
            {
                currentIterations++;
                if (currentIterations > maxSearchIterations)
                {
                    Debug.LogWarning($"WaterCoefficientsComputation: 버텍스 {v_index} 상향식 탐색 무한 루프 가능성, 강제 종료됨.");
                    break;
                }

                float min_p_value = float.MaxValue;
                int n_min_index = -1;


                if (vertexNeighbors.ContainsKey(c_current_index))
                {
                    foreach (int n_neighbor_index in vertexNeighbors[c_current_index])
                    {
                        if (visitedVertices.Contains(n_neighbor_index))
                        {
                            continue;
                        }

                        Vector3 n_position = vertices[n_neighbor_index];
                        Vector3 cn_vector = (n_position - c_position).normalized;

                        float p_value = Vector3.Dot(cn_vector, g);

                        if (p_value < min_p_value)
                        {
                            min_p_value = p_value;
                            n_min_index = n_neighbor_index;
                        }
                    }
                }
                if (n_min_index == -1)
                {
                    break;
                }
                bool c_is_water_supply = (waterAmounts[c_current_index] > waterSupplyThreshold);
                bool nmin_is_water_supply = (waterAmounts[n_min_index] > waterSupplyThreshold);

                float d = Vector3.Distance(c_position, vertices[n_min_index]);
                float result = d * (-min_p_value);

                if (c_is_water_supply != nmin_is_water_supply)
                {
                    result /= 2.0f;
                }
                wc = wc + result;

                c_current_index = n_min_index;
                c_position = vertices[c_current_index];
                visitedVertices.Add(c_current_index);
            }
            newWaterCoefficients[v_index] = wc;
            processedVertices++;

            if (processedVertices % verticesPerFrame == 0)
            {
                System.Array.Copy(newWaterCoefficients, waterAmounts, waterAmounts.Length);
                for (int i = 0; i < vertices.Length; i++)
                {
                    float normalizedWaterAmount = Mathf.Clamp01(waterAmounts[i] / maxWaterAmount);
                    colors[i] = Color.Lerp(minWaterColor, maxWaterColor, normalizedWaterAmount);
                }
                mesh.colors = colors;

                yield return null;
            }
        }
        System.Array.Copy(newWaterCoefficients, waterAmounts, waterAmounts.Length);

        for (int i = 0; i < vertices.Length; i++)
        {
            float normalizedWaterAmount = Mathf.Clamp01(waterAmounts[i] / maxWaterAmount);
            colors[i] = Color.Lerp(minWaterColor, maxWaterColor, normalizedWaterAmount);
        }
        mesh.colors = colors;

        Debug.Log("물 계수 계산이 완료되었습니다.", this);

        DripPointsIdentification();
    }

    //2단계 고드름 드립 포인트 찾기
    private void DripPointsIdentification()
    {
        dripPoints.Clear();
        gizmoVertexIndices.Clear();

        Vector3 g = Quaternion.Inverse(raycastSource.rotation) * -Vector3.up;

        // 1. 잠재적인 모든 드립 포인트를 리스트에 담습니다.
        List<int> dripRegionVertices = new List<int>();
        for (int i = 0; i < vertices.Length; i++)
        {
            if (waterAmounts[i] > 0 && Vector3.Angle(normals[i], g) > dripLimitAngle)
            {
                dripRegionVertices.Add(i);
            }
        }

        if (dripRegionVertices.Count > 0)
        {
            // 2. 각 후보지의 물의 양(가중치)을 별도 리스트로 만듭니다.
            List<float> weights = dripRegionVertices.Select(index => waterAmounts[index]).ToList();

            // 생성할 고드름 개수를 정합니다.
            int iciclesToGenerate = Mathf.Min(numberOfIcicles, dripRegionVertices.Count);

            // 3. 정해진 개수만큼 추첨을 반복
            for (int i = 0; i < iciclesToGenerate; i++)
            {
                // 3-1. 전체 총합을 계산합니다.
                float totalWeight = weights.Sum();
                if (totalWeight <= 0) break; // 더 이상 뽑을 후보가 없으면 중단

                // 3-2. 0부터 복권 총합 사이의 랜덤한 숫자를 뽑습니다.
                float randomValue = Random.Range(0, totalWeight);

                // 3-3. 후보지를 순회하며 랜덤 숫자에 해당하는 당첨자를 찾습니다.
                int chosenIndex = -1;
                for (int j = 0; j < dripRegionVertices.Count; j++)
                {
                    // 이미 뽑힌 위치는 건너뜁니다.
                    if (weights[j] <= 0) continue;

                    randomValue -= weights[j];
                    if (randomValue <= 0)
                    {
                        chosenIndex = j;
                        break;
                    }
                }

                // 3-4. 당첨자를 최종 리스트에 추가하고, 다음 추첨에서 제외시킵니다.
                if (chosenIndex != -1)
                {
                    int vertexIndex = dripRegionVertices[chosenIndex];
                    dripPoints.Add(vertices[vertexIndex]);
                    gizmoVertexIndices.Add(vertexIndex);

                    // 다음 추첨에 중복 당첨되지 않도록 가중치를 0으로 만듭니다.
                    weights[chosenIndex] = 0;
                }
            }

            Debug.Log($"물의 양에 비례하여 {dripPoints.Count}개의 고드름 생성 지점을 찾았습니다.");
            GenerateIcicles();
        }
        else
        {
            Debug.Log("고드름이 생성될 지점을 찾지 못했습니다.");
        }
    }

    void OnDrawGizmos()
    {
        // 필요한 데이터(물 양, 정점, 시각화할 인덱스)가 모두 있는지 확인
        if (waterAmounts != null && vertices != null && gizmoVertexIndices != null)
        {

            // 시각화할 정점 인덱스 리스트를 순회
            foreach (int i in gizmoVertexIndices)
            {
                // 배열 인덱스 범위를 벗어나지 않도록 검사
                if (i >= 0 && i < vertices.Length && i < waterAmounts.Length)
                {
                    if (waterAmounts[i] / maxWaterAmount >= 0.5f)
                    {
                        Gizmos.color = maxWaterColor; // 물이 많을 때는 파란색 등으로
                    }
                    else
                    {
                        Gizmos.color = minWaterColor; // 물이 적을 때는 빨간색 등으로
                    }

                    // 현재 정점의 로컬 위치를 월드 위치로 변환
                    Vector3 worldPosition = transform.TransformPoint(vertices[i]);

                    // 해당 위치에 색상이 지정된 구체 기즈모를 그림
                    Gizmos.DrawSphere(worldPosition, gizmoSize);
                }
            }
        }
    }
    //3단계: L-system
    private void GenerateIcicles()
    {

        foreach (LineRenderer lr in icicleRenderers)
        {
            if (lr != null && lr.gameObject != null)
            {
                DestroyImmediate(lr.gameObject);
            }
        }
        icicleRenderers.Clear();

        for (int i = 0; i < numberOfIcicles && i < dripPoints.Count; i++)
        {
            int vertexIndex = gizmoVertexIndices[i];
            Vector3 localDripPoint = vertices[vertexIndex];
            Vector3 localNormal = normals[vertexIndex];
            Transform meshTransform = meshFilterComponent.transform;

            Vector3 worldDripPoint = meshTransform.TransformPoint(localDripPoint);
            Vector3 worldNormal = meshTransform.TransformDirection(localNormal).normalized;
            float surfaceOffset = 0.01f;
            Vector3 offsetWorldPoint = worldDripPoint + worldNormal * surfaceOffset;
            Vector3 worldGravityDirection = Vector3.down;

            GameObject icicleGo = new GameObject($"Icicle_{i}");
            icicleGo.transform.position = offsetWorldPoint;
            icicleGo.transform.rotation = Quaternion.identity;
            icicleGo.transform.parent = icicleContainer;

            LineRenderer lr = icicleGo.AddComponent<LineRenderer>();
            icicleRenderers.Add(lr);





        }
    }
}







