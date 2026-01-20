using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class BunnyMesh : MonoBehaviour
{
    //인스펙터 접근 불가 변수 (private)
    private MeshFilter meshFilterComponent;
    private Mesh mesh;
    private Vector3[] vertices;
    private Color[] colors;
    private float[] waterAmounts;

    

    //각 버텍스 이웃 정보를 저장할 딕셔너리
    private Dictionary<int, List<int>> vertexNeighbors;
    private float simulationUpdateInterval = 0.5f;
    private float timer = 0f;
  

    
    //인스펙터 설정 public 변수들
    public float colorChangeRadius = 0.1f;
    public Color minWaterColor = Color.red;
    public Color maxWaterColor = Color.blue;
    public float maxWaterAmount = 1.0f;
    public float waterAddAmount = 0.1f;
    public float waterSupplyThreshold = 0.001f;




    // Start is called before the first frame update
    void Start()
    {
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


        mesh.colors = colors;
        Debug.Log("Start() 초기화 완료.", this);
    }



    // Update is called once per frame
    void Update()
    {
        
        timer += Time.deltaTime;
        if(timer >=simulationUpdateInterval)
        {
            WaterCoefficientCalculate();
            timer = 0f;
        }
 

        for (int i = 0; i < vertices.Length; i++)
        {
            float normalizedWaterAmount = Mathf.Clamp01(waterAmounts[i]/maxWaterAmount);
            colors[i] = Color.Lerp(minWaterColor, maxWaterColor, normalizedWaterAmount);
        }
        mesh.colors = colors;

    }

    //처음 물 공급원 저장 함수
    public void WaterSupplyVertex(Vector3 worldHitPoint)
    {
        if (mesh == null) return;

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
    private void WaterCoefficientCalculate()
    {
        Vector3 g = Vector3.down;

        float[] newWaterCoefficients = new float[vertices.Length];
        System.Array.Copy(waterAmounts, newWaterCoefficients, waterAmounts.Length);

        //무한 루프 방지
        int maxSearchIterations = vertices.Length * 2;

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
        }
        System.Array.Copy(newWaterCoefficients, waterAmounts, waterAmounts.Length);
    }
}
