using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;


public class BuildOfMesh : MonoBehaviour
{
    //인스펙터 접근 불가 변수 (private)
    private MeshFilter meshFilterComponent;
    private Mesh mesh;
    private Vector3[] vertices;
    private Color[] colors;
    private float[] waterAmounts;
    //  private bool isSimulationRunning = false;

    //레이 회전 반영 관련 변수
    private Coroutine calculationCoroutine;
    private Quaternion lastRotation;
    public Transform raycastSource;
    //2단계에서 필요한 변수들
    private Vector3[] normals;
    public List<Vector3> dripPoints = new List<Vector3>();

    //3단계에서 필요한 지역 변수들(private), 클래스 인스턴스
    public LSystemGenerator lSystemGenerator;
    public IcicleGenerator icicleGenerator;
    public float maxIcicleLength = 150.0f;
    public Material blob;
    private List<LineRenderer> icicleRenderers = new List<LineRenderer>();

    // L-System 기반 궤적 계산용 내부 상태 변수
    private Stack<(Vector3 pos, Vector3 dir)> transformStack;
    private Vector3 currentSurfaceNormal;
    private Vector3 currentDir;
    private bool isFollowingSurface;

    //4단계 필요 변수
    public OriginMCBlob MetaballController;


    private List<GameObject> generatedIcicleSegments = new List<GameObject>();
    private Material lineMaterial;
    //각 버텍스 이웃 정보를 저장할 딕셔너리
    private Dictionary<int, List<int>> vertexNeighbors;
    //private float simulationUpdateInterval = 3f;
    // private float timer = 0f;



    //인스펙터 설정 public 변수들
    public float colorChangeRadius = 0.1f;
    public Color minWaterColor = Color.red;
    public Color maxWaterColor = Color.blue;
    public float maxWaterAmount = 1.0f;
    public float waterAddAmount = 1.0f;
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
    public int iterations = 20000;
    [Range(0.0f, 180.0f)]
    public float curvatureAngle = 25.0f;
    [Range(0.0f, 1.0f)]
    public float subdivisionProbability = 0.5f;
    [Range(0.0f, 360.0f)]
    public float userDispersionAngle = 137.5f;
    public float segmentLength = 2.0f;
    public LayerMask collisionLayers;

    //4단계 필요 변수들 (IcicleProfile)
    [Header("Icicle Profile parameters")]
    public float rippleFrequency = 0.4f; //fs
    public float taper = 0.05f; //t
    public float rippleAmplitude = 100.0f; //as
    public float tipRadius = 0.1f;
    public float baseRadiusScale = 0.7f;

    //noise 함수
    public float positionNoiseAmount = 0.05f;

    [Header("Base of Icicles")]
    public float baseSpreadDistance = 0.5f;
    public int baseMetaballCount = 15;
    public float baseIcicleRadius = 0.7f;

    public float minRadius = 0.01f;

    //4단계 필요 변수들 (Glaze Ice)
    [Header("Glaze Ice")]
    public int ngi = 80; //사용자가 설정할 메타볼 개수 
    public float scaling =0.02f; // s <-빙막 스케일링 값 
    public float lifeTime = 0.3f;  // lt <- 물 방울이 살아남는 생존 시간 (0~1 사이의 값) 
    public float minGI = 0.001f;

    //삼각형 메시 파악
    private float[] triangleAreas;
    private float[] cumulativeAreas;
    private float totalArea;

    //private List<GameObject> glazeSegments = new List<GameObject>(); 
    private GameObject glazeIceContainer; 
    //메타볼 표면 결정 변수들 
    public OriginMCBlob gridSize;
    public OriginMCBlob isoLevel;
    public OriginMCBlob powerScale;
    void Start()
    {
        Debug.Log("Start() 함수 시작!");
        meshFilterComponent = GetComponent<MeshFilter>();


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

        //    lSystemGenerator.Initialize(curvatureAngle, subdivisionProbability, userDispersionAngle);

        icicleGenerator = GetComponent<IcicleGenerator>();
        if (icicleGenerator == null)
        {
            icicleGenerator = gameObject.AddComponent<IcicleGenerator>();
        }

        Debug.Log("Start() 초기화 완료.", this);

        lineMaterial = new Material(Shader.Find("Sprites/Default"));

     
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

    private void CalculateMeshAreas()
    {
        Mesh tragetMesh = meshFilterComponent.sharedMesh;
        int[] triangles = mesh.triangles;
        Vector3[] verts = mesh.vertices;

        int triCount = triangles.Length / 3; 
        triangleAreas = new float[triCount];
        cumulativeAreas = new float [triCount];
        totalArea = 0f; 

        for(int i = 0; i<triCount;i++)
        {
            Vector3 v0 = verts[triangles[i * 3]];
            Vector3 v1 = verts[triangles[i *3+1]];
            Vector3 v2 = verts[triangles[i * 3 + 2]];

            float area = Vector3.Cross(v1-v0, v2 - v0).magnitude * 0.5f;
            totalArea += area;
            triangleAreas[i] = area;
            cumulativeAreas[i] = totalArea; 
        }

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
    public void DripPointsIdentification()
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

            //   Debug.Log($"물의 양에 비례하여 {dripPoints.Count}개의 고드름 생성 지점을 찾았습니다.");

            // MetaballOnDripPoint();
          //  GlazeIce(); 
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

    void MetaballOnDripPoint()
    {
        if (dripPoints == null || dripPoints.Count == 0) return;
        if (MetaballController == null) return;

        // 각 드립포인트마다 개별 메타볼 오브젝트 생성
        foreach (GameObject segment in generatedIcicleSegments)
        {
            if (segment != null) Destroy(segment);
        }
        generatedIcicleSegments.Clear();

        for (int i = 0; i < dripPoints.Count; i++)
        {
            Vector3 dripPoint = dripPoints[i];
            int vertexIndex = gizmoVertexIndices[i];
            Vector3 normal = normals[vertexIndex];

            Vector3 offsetDripPoint = dripPoint + normal * 0.1f;

            // 각 드립포인트에 메타볼 세그먼트 생성
            GameObject metaballSegment = new GameObject($"DripPoint_Metaball_{i}");

            // 월드 위치로 변환
            Vector3 worldDripPoint = transform.TransformPoint(dripPoint);
            metaballSegment.transform.position = worldDripPoint;

            // MetaballController의 자식으로 설정
            metaballSegment.transform.SetParent(MetaballController.transform, true);

            // SphereCollider 추가 (메타볼 시스템이 감지할 수 있도록)
            SphereCollider sc = metaballSegment.AddComponent<SphereCollider>();
            sc.radius = baseRadiusScale; // 기존 변수 사용
            sc.isTrigger = true;

            generatedIcicleSegments.Add(metaballSegment);

           // Debug.Log($"드립포인트 {i}에 메타볼 생성: {worldDripPoint}");
        }

        // 메타볼 시스템 업데이트
        if (generatedIcicleSegments.Count > 0)
        {
            MetaballController.RefreshBlobList();
            MetaballController.GenerateMesh();
          //  Debug.Log($"총 {generatedIcicleSegments.Count}개의 드립포인트 메타볼 생성 완료");
        }
    }

    private void GenerateIcicles()
    {
        // --- 1. 이전 고드름 오브젝트들 정리 ---
        foreach (LineRenderer lr in icicleRenderers)
        {
            if (lr != null && lr.gameObject != null)
            {
                Destroy(lr.gameObject);
            }
        }
        icicleRenderers.Clear();

        foreach (GameObject segment in generatedIcicleSegments)
        {
            if (segment != null)
            {
                Destroy(segment);
            }
        }
        generatedIcicleSegments.Clear();

        // 스케일 감지
        Vector3 currentScale = transform.lossyScale;
        float avgScale = (currentScale.x + currentScale.y + currentScale.z) / 3.0f;
        Debug.Log($"현재 스케일: {currentScale}, 평균 스케일: {avgScale}");

        // --- 2. 각 드립 포인트마다 새로운 고드름 생성 ---
        for (int i = 0; i < numberOfIcicles && i < dripPoints.Count; i++)
        {
            CreateSingleIcicle(i, avgScale);
        }

        Debug.Log($"총 {numberOfIcicles}개의 고드름 생성 완료");
    }

    private void CreateSingleIcicle(int icicleIndex, float avgScale)
    {
        int vertexIndex = gizmoVertexIndices[icicleIndex];
        Vector3 localDripPoint = vertices[vertexIndex];
        Vector3 localNormal = normals[vertexIndex];
        Transform meshTransform = meshFilterComponent.transform;

        Vector3 worldDripPoint = meshTransform.TransformPoint(localDripPoint);
        Vector3 worldNormal = meshTransform.TransformDirection(localNormal).normalized;
        float surfaceOffset = 0.01f * avgScale;
        Vector3 offsetWorldPoint = worldDripPoint + worldNormal * surfaceOffset;


        Vector3 worldGravityDirection = raycastSource.TransformDirection(Vector3.down).normalized;

        // === 1. 궤적 계산용 임시 오브젝트 ===
        GameObject trajectoryHelper = new GameObject($"TrajectoryHelper_{icicleIndex}");
        trajectoryHelper.transform.position = offsetWorldPoint;
        trajectoryHelper.transform.rotation = Quaternion.LookRotation(worldGravityDirection);
        trajectoryHelper.transform.parent = this.transform;

        // 라인 렌더러는 궤적 계산용 오브젝트에만 추가
        LineRenderer lr = trajectoryHelper.AddComponent<LineRenderer>();
        icicleRenderers.Add(lr);

        // 궤적 계산
        List<Vector3> trajectory = CalculateTrajectory(trajectoryHelper, icicleIndex, avgScale);

        // 라인 렌더러 설정
        lr.useWorldSpace = false;
        lr.positionCount = trajectory.Count;
        lr.SetPositions(trajectory.ToArray());
        lr.startWidth = 0.08f;
        lr.endWidth = 0.01f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = Color.white;
        lr.endColor = Color.white;

        // === 2. 메타볼 시스템용 별도 오브젝트 ===
        GameObject metaballIcicle = new GameObject($"Icicle_{icicleIndex}");
        metaballIcicle.transform.position = offsetWorldPoint;
        metaballIcicle.transform.rotation = Quaternion.identity;
       
        // MetaballController가 있으면 그 자식으로, 없으면 현재 오브젝트 자식으로
        if (MetaballController != null)
        {
            metaballIcicle.transform.parent = MetaballController.transform;
        }
        else
        {
            metaballIcicle.transform.parent = this.transform;
        }



        // 메타볼 컴포넌트들 추가
        OriginMCBlob metaballController = metaballIcicle.AddComponent<OriginMCBlob>();
        MeshFilter meshFilter = metaballIcicle.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = metaballIcicle.AddComponent<MeshRenderer>();


        // 메타볼 설정
        metaballController.isoLevel =1.0f;
       // metaballController.powerScale =1.0f;
        metaballController.gridSize = new Vector3(40f, 60f, 40f);

        BaseOfIcicles(metaballIcicle, vertexIndex);

        // === 3. 궤적을 따라 세그먼트 생성 ===
        CreateMetaballSegments(metaballIcicle, trajectoryHelper, trajectory, avgScale);

        
        // === 4. 메타볼 메시 생성 ===
        try
        {
            // RefreshBlobList 호출 시도
            var refreshMethod = metaballController.GetType().GetMethod("RefreshBlobList");
            if (refreshMethod != null)
            {
                refreshMethod.Invoke(metaballController, null);
            }

            metaballController.GenerateMesh();
            Debug.Log($"고드름 {icicleIndex} 생성 완료");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"고드름 {icicleIndex} 메타볼 생성 중 오류: {e.Message}");
         
        }
    }

    private List<Vector3> CalculateTrajectory(GameObject trajectoryHelper, int icicleIndex, float avgScale)
    {
        
        // --- 1. L-System 문자열 생성 ---
        float waterValue = waterAmounts[gizmoVertexIndices[icicleIndex]];
        int maxIterations = (segmentLength > 0) ? Mathf.FloorToInt(maxIcicleLength / segmentLength) : 0;
        int calculatedIterations = Mathf.RoundToInt(this.iterations * waterValue);
        int dynamicIterations = Mathf.Max(1, Mathf.Min(calculatedIterations, maxIterations));

        // L-System 파라미터 조정 (물 양에 따라 분기 확률 조절)
        float dynamicSubdivisionProb = Mathf.Clamp01(subdivisionProbability * Mathf.Clamp01(waterValue));
        lSystemGenerator.subdivisionProbability = dynamicSubdivisionProb;
        lSystemGenerator.curvatureAngle = this.curvatureAngle;
        lSystemGenerator.userDispersionAngle = this.userDispersionAngle;

        string lSystemString = lSystemGenerator.Generate(dynamicIterations);
      //  Debug.Log($"[Icicle #{icicleIndex}] L-System 문자열 생성 완료: 길이 {lSystemString.Length}");

        // --- 2. 초기 상태 변수들 ---
        Vector3 worldGravityDir = raycastSource.TransformDirection(Vector3.down).normalized;
        Vector3 currentDir = worldGravityDir;
        Vector3 currentLocalPos = Vector3.zero;
        Vector3 currentSurfaceNormal = Vector3.up;
        bool isFollowingSurface = false;

        Stack<(Vector3 pos, Vector3 dir)> transformStack = new Stack<(Vector3, Vector3)>();
        List<Vector3> trajectory = new List<Vector3>();
        trajectory.Add(currentLocalPos);

        // --- 3. L-System 문자열 해석 ---
        foreach (char command in lSystemString)
        {
            switch (command)
            {
              case 'F': // 앞으로 전진
                case 'G':
                    {
                        Vector3 moveDir;
                        if (isFollowingSurface)
                        {
                            moveDir = Vector3.ProjectOnPlane(worldGravityDir, currentSurfaceNormal).normalized;
                            if (moveDir.sqrMagnitude < 1e-6f)
                                moveDir = worldGravityDir;
                        }
                        else
                        {
                            moveDir = Vector3.Slerp(currentDir, worldGravityDir, 0.3f).normalized;
                        }

                        Vector3 currentWorldPos = trajectoryHelper.transform.TransformPoint(currentLocalPos);
                        Vector3 rayStart = isFollowingSurface
                            ? currentWorldPos + currentSurfaceNormal * 0.03f
                            : currentWorldPos;

                        RaycastHit hit;
                        if (Physics.Raycast(rayStart, moveDir, out hit, segmentLength, collisionLayers))
                        {
                            // 표면을 따라 이동
                            isFollowingSurface = true;
                            currentSurfaceNormal = hit.normal;
                            Vector3 offsetHitPoint = hit.point + hit.normal * 0.03f;
                            currentLocalPos = trajectoryHelper.transform.InverseTransformPoint(offsetHitPoint);
                            currentDir = Vector3.ProjectOnPlane(worldGravityDir, hit.normal).normalized;
                        }
                        else
                        {
                            // 공중 이동
                            isFollowingSurface = false;
                            Vector3 nextWorldPos = currentWorldPos + moveDir * segmentLength;
                            currentLocalPos = trajectoryHelper.transform.InverseTransformPoint(nextWorldPos);
                            currentDir = Vector3.Slerp(currentDir, worldGravityDir, 0.1f).normalized;
                        }

                        trajectory.Add(currentLocalPos);
                    }
                    break;

                case '+': // 좌우 회전 (곡률)
                    currentDir = Quaternion.AngleAxis(curvatureAngle, Vector3.forward) * currentDir;
                    break;

                case '-': // 반대 회전
                    currentDir = Quaternion.AngleAxis(-curvatureAngle, Vector3.forward) * currentDir;
                    break;

                    //case '[': // 분기 시작
                    //    transformStack.Push((currentLocalPos, currentDir));
                    //    break;

                    //case ']': // 분기 끝
                    //    if (transformStack.Count > 0)
                    //    {
                    //        var (savedPos, savedDir) = transformStack.Pop();
                    //        currentLocalPos = savedPos;
                    //        currentDir = savedDir;
                    //        trajectory.Add(currentLocalPos);
                    //    }
                    //    break;

                    //default:
                    //    break;
            }
        }

        // --- 4. 결과 반환 ---
        return trajectory;
    

}


    private void CreateMetaballSegments(GameObject metaballIcicle, GameObject trajectoryHelper, List<Vector3> trajectory, float avgScale)
    {
        if (trajectory.Count <= 1) return;

        // 1) 전체 길이 & 구간 길이들
        float totalLength = 0f;
        var segLen = new List<float>(trajectory.Count - 1);
        for (int i = 0; i < trajectory.Count - 1; i++)
        {
            float d = Vector3.Distance(trajectory[i], trajectory[i + 1]);
            segLen.Add(d);
            totalLength += d;
        }
        if (totalLength <= 1e-6f) return;

        // 2) 원하는 간격(촘촘도) - 필요 시 인스펙터로 노출
        float spacing = 0.05f;             // 0.1~0.3 추천
        float acc = 0f;
        int segIdx = 0;
        float segAcc = 0f;

        // 3) 시작점 하나 생성
        Place(pointAt(0, 0f), 0f);

        // 4) 전체 길이를 spacing으로 채우기
        while (acc + spacing <= totalLength)
        {
            float target = acc + spacing;

            // target이 위치한 세그먼트를 찾고 보간
            while (segIdx < segLen.Count && segAcc + segLen[segIdx] < target)
            {
                segAcc += segLen[segIdx];
                segIdx++;
            }
            if (segIdx >= segLen.Count) break;

            float t = (target - segAcc) / Mathf.Max(1e-6f, segLen[segIdx]);
            Vector3 p = Vector3.Lerp(trajectory[segIdx], trajectory[segIdx + 1], t);

            Place(p, target);
            acc = target;
        }

        // ---- 내부 로컬 함수들 ----
        Vector3 pointAt(int i, float _dummy) => trajectory[i];

        void Place(Vector3 localPosInHelper, float curX)
        {
            // helper(local) -> world
            Vector3 pointInWorld = trajectoryHelper.transform.TransformPoint(localPosInHelper);

            // (선택) 약간의 노이즈
            if (positionNoiseAmount > 0)
            {
                Vector2 rnd = Random.insideUnitCircle * positionNoiseAmount;
                Vector3 off = trajectoryHelper.transform.right * rnd.x + trajectoryHelper.transform.up * rnd.y;
                pointInWorld += off;
            }

            // 반지름 계산 (진행도 기반)
            float radius = IcicleProfile(curX, totalLength);

            // === 여기서 좌표 방식 '하나'만 사용 (옵션 A 또는 B) ===
            GameObject g = new GameObject($"icicleSeg_{generatedIcicleSegments.Count}");
            g.transform.SetParent(metaballIcicle.transform, /*worldPositionStays*/ true);
            g.transform.position = pointInWorld;                    // 월드 방식만
            g.transform.rotation = Quaternion.identity;
            g.transform.localScale = Vector3.one;

            SphereCollider sc = g.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = radius;

            generatedIcicleSegments.Add(g);
        }
    }

    //프로파일 함수 구현 4.4.1
    //private float IcicleProfile(float currentPositionX, float totalLengthL)
    //{
    //    // 진행도 (0 = 시작, 1 = 끝)
    //    float t = (totalLengthL > 1e-6f) ? Mathf.Clamp01(currentPositionX / totalLengthL) : 0f;

    //    // 선형 테이퍼: 시작쪽 두껍고 끝쪽 얇게
    //    float cone = Mathf.Lerp(baseRadiusScale, tipRadius, t);

    //    // Ripple은 작은 비율로만 반영
    //    float ripple = 1f + rippleAmplitude * Mathf.Sin(t * rippleFrequency * Mathf.PI * 2f);

    //    float radius = cone * ripple;

    //    // 최소 반지름 보정
    //    return Mathf.Max(minRadius, radius);
    //}

    // paper-style profile
    private float IcicleProfile(float s, float L)
    {
        s = Mathf.Clamp(s, 0f, L);

        float tip = tipRadius;
        float slope = taper; 

        float linear = tip + (L - s) * slope;

        float ripple = 1f + rippleAmplitude * Mathf.Sin(s * rippleFrequency);

        float radius = linear * ripple;
        return Mathf.Max(minRadius, radius);
    }




    // 4.4.3 Base of Icicle (논문식 구현)

    private void BaseOfIcicles(GameObject parentIcicle, int mainDripPointIndex)
    {
        int skipCenterMetaballs =1;
        float eb = baseSpreadDistance;    // influence radius (e_b)
        int nmb = baseMetaballCount;      // number of base metaballs (n_mb)
        if (parentIcicle == null || eb <= 0f || nmb <= 0) return;

        // --- Normalize water coefficients ---
        float denom = 1f;
        if (waterAmounts != null && waterAmounts.Length > 0)
        {
            float[] tmp = (float[])waterAmounts.Clone();
            System.Array.Sort(tmp);
            int p = Mathf.Clamp(Mathf.FloorToInt(tmp.Length * 0.95f), 0, tmp.Length - 1);
            denom = Mathf.Max(1e-6f, tmp[p]);
        }

        // --- Drip point local/world info ---
        Vector3 pLocal = vertices[mainDripPointIndex];
        Vector3 nLocal = normals[mainDripPointIndex].normalized;
        Vector3 centerWorld = transform.TransformPoint(pLocal + nLocal * 0.003f);

        // --- (1) Collect vertices within surface distance e_b ---
        var nearby = new List<int>();
        var geod = new Dictionary<int, float>(256);
        var q = new Queue<int>(256);
        int start = mainDripPointIndex;

        geod[start] = 0f;
        q.Enqueue(start);

        while (q.Count > 0)
        {
            int v = q.Dequeue();
            float dv = geod[v];
            if (dv > eb) continue;

            if (v != start)
                nearby.Add(v);

            if (!vertexNeighbors.TryGetValue(v, out var nbs)) continue;
            foreach (int u in nbs)
            {
                float w = Vector3.Distance(vertices[v], vertices[u]);
                float nd = dv + w;
                if (nd <= eb && (!geod.ContainsKey(u) || nd < geod[u]))
                {
                    geod[u] = nd;
                    q.Enqueue(u);
                }
            }
        }

        if (nearby.Count == 0) nearby.Add(start);


        nearby.Sort((a, b) => geod[a].CompareTo(geod[b]));
        int take = Mathf.Min(nmb, nearby.Count);

        // wc: use drip point’s water coefficient as scale (논문 정의)
        float wcDrip = Mathf.Clamp01(waterAmounts[start] / denom);

        //드립포인트 주변으로 메타볼 생성
        for (int k = skipCenterMetaballs; k < take; k++)
        {
            int idx = nearby[k];
            Vector3 vLocal = vertices[idx];
            Vector3 vNorm = normals[idx].normalized;


            float dLocal = geod.TryGetValue(idx, out var dd) ? dd : eb;
            float numer = Mathf.Max(0f, eb - dLocal);
            float falloff = (numer * numer) / (eb * eb);
            float rb = falloff * wcDrip * baseIcicleRadius;
            //float  rb = falloff * wcDrip;


            Vector3 worldPos = transform.TransformPoint(vLocal);
          

            // Create metaball object
            GameObject seg = new GameObject($"IcicleBase_{idx}");
            seg.transform.SetParent(parentIcicle.transform, true);
            seg.transform.position = worldPos;
            seg.transform.rotation = Quaternion.identity;
            seg.transform.localScale = Vector3.one;

            SphereCollider sc = seg.AddComponent<SphereCollider>();
            sc.isTrigger = true;
            sc.radius = rb;

            generatedIcicleSegments.Add(seg);
        }
    }

    //private void GlazeIce()
    //{
    //    // 0) 이전 빙막 삭제
    //    foreach (var g in glazeSegments)
    //        if (g != null) Destroy(g);
    //    glazeSegments.Clear();

    //    if (vertices == null || waterAmounts == null) return;
    //    if (vertices.Length != waterAmounts.Length) return;

    //    Transform tr = meshFilterComponent.transform;

    //    // 레이 방향(월드) → 메쉬 로컬 중력 방향
    //    Vector3 worldGravityDir = raycastSource.TransformDirection(Vector3.down).normalized;
    //    Vector3 localGravityDir = transform.InverseTransformDirection(worldGravityDir).normalized;

    //    float minH = float.PositiveInfinity;
    //    float maxH = float.NegativeInfinity;

    //    // --- 1. wc>0인 버텍스들의 로컬 높이 범위(minH, maxH) 계산 ---
    //    for (int i = 0; i < vertices.Length; i++)
    //    {
    //        if (waterAmounts[i] <= 0f) continue;

    //        Vector3 vLocal = vertices[i];                     // 로컬 버텍스
    //        float h = Vector3.Dot(vLocal, -localGravityDir);  // 로컬 기준 높이

    //        if (h < minH) minH = h;
    //        if (h > maxH) maxH = h;
    //    }

    //    if (!float.IsFinite(minH) || !float.IsFinite(maxH) || Mathf.Approximately(maxH, minH))
    //        return;

    //    float heightRange = Mathf.Max(1e-6f, maxH - minH);

    //    int created = 0;
    //    int safeCount = vertices.Length * 3;
    //    int targetCount = Mathf.RoundToInt(ngi);
    //    float lt = Mathf.Clamp01(lifeTime);

    //    // --- 2. wc>0인 버텍스 중 일부를 뽑아서 빙막 세그먼트 생성 ---
    //    while (created < targetCount && (safeCount-- > 0))
    //    {
    //        int idx = Random.Range(0, vertices.Length);
    //        if (waterAmounts[idx] <= 0f) continue;

    //        Vector3 vLocal = vertices[idx];
    //        float h = Vector3.Dot(vLocal, -localGravityDir);

    //        // 0~1 정규화된 높이
    //        float t = (h - minH) / heightRange;   // 0 = 가장 아래, 1 = 가장 위
    //        float dUpNorm = t;
    //        float dDownNorm = 1f - t;

    //        // rGI = minGI + scaling * [ dUpNorm*lt + dDownNorm*(1-lt) ]
    //        float rGI = minGI + scaling * (dUpNorm * lt + dDownNorm * (1f - lt));

    //        if (rGI <= 0f) continue;

    //        // 로컬 → 월드로 변환해서 표면에 붙이기
    //        Vector3 wPos = tr.TransformPoint(vLocal);

    //        GameObject seg = new GameObject($"GlazeIce_{created}");
    //        seg.transform.SetParent(transform, true);
    //        seg.transform.position = wPos;
    //        seg.transform.rotation = Quaternion.identity;
    //        seg.transform.localScale = Vector3.one;

    //        SphereCollider sc = seg.AddComponent<SphereCollider>();
    //        sc.isTrigger = true;
    //        sc.radius = rGI;

    //        glazeSegments.Add(seg);
    //        created++;
    //    }
    //}


    //private void GlazeIce()
    //{
    //    // 이전 빙막 삭제
    //    //foreach (var g in glazeSegments)
    //    //    if (g != null) Destroy(g);
    //    //glazeSegments.Clear();

    //    if(glazeIceContainer != null)
    //    {
    //        Destroy(glazeIceContainer);
    //    }

    //    if (vertices == null || waterAmounts == null) return;
    //    if (vertices.Length != waterAmounts.Length) return;

    //    Transform tr = meshFilterComponent.transform;
    //    Vector3 worldGravityDir = raycastSource.TransformDirection(Vector3.down).normalized;
    //    Vector3 localGravityDir = transform.InverseTransformDirection(worldGravityDir).normalized;

    //    float minH = float.PositiveInfinity;
    //    float maxH = float.NegativeInfinity;

    //    for (int i = 0; i < vertices.Length; i++)
    //    {
    //        if (waterAmounts[i] <= 0f) continue;
    //        Vector3 vLocal = vertices[i];
    //        float h = Vector3.Dot(vLocal, -localGravityDir);
    //        if (h < minH) minH = h;
    //        if (h > maxH) maxH = h;
    //    }

    //    if (!float.IsFinite(minH) || !float.IsFinite(maxH) || Mathf.Approximately(maxH, minH))
    //        return;

    //    float heightRange = Mathf.Max(1e-6f, maxH - minH);

    //    int created = 0;
    //    int safeCount = vertices.Length * 3;
    //    int targetCount = Mathf.RoundToInt(ngi);
    //    float lt = Mathf.Clamp01(lifeTime);

    //    Transform metaballParent = MetaballController ? MetaballController.transform : transform;
    //    glazeIceContainer = new GameObject("GlazeIce");

    //    glazeIceContainer.transform.SetParent(metaballParent, true);
    //    glazeIceContainer.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
    //    glazeIceContainer.transform.localScale = Vector3.one; 

    //    while (created < targetCount && (safeCount-- > 0))
    //    {
    //        int idx = Random.Range(0, vertices.Length);
    //        if (waterAmounts[idx] <= 0f) continue;

    //        Vector3 vLocal = vertices[idx];
    //        float h = Vector3.Dot(vLocal, -localGravityDir);

    //        float t = (h - minH) / heightRange;
    //        float dUpNorm = t;
    //        float dDownNorm = 1f - t;

    //        float rGI = minGI + scaling * (dUpNorm * lt + dDownNorm * (1f - lt));
    //        if (rGI <= 0f) continue;

    //        Vector3 wPos = tr.TransformPoint(vLocal);

    //        GameObject seg = new GameObject($"GlazeIce_{created}");
    //        //자식으로 넣는다. 
    //        seg.transform.SetParent(glazeIceContainer.transform, true);

    //      // seg.transform.SetParent(metaballParent, true);
    //        seg.transform.position = wPos;
    //        seg.transform.rotation = Quaternion.identity;
    //        seg.transform.localScale = Vector3.one;

    //        SphereCollider sc = seg.AddComponent<SphereCollider>();
    //        sc.isTrigger = true;
    //        sc.radius = rGI;

    //       // glazeSegments.Add(seg);
    //        created++;
    //    }
    //}



}

