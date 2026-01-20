using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Raycast : MonoBehaviour
{
    public WaterMeshSimulator waterMeshSimulatorScript;
    //인스펙터에서 접근 가능한 변수들 (public)
    public float rainAreaSize = 100f; // 레이캐스트가 쏟아질 가로/세로 영역의 크기 (정사각형)
    public float rainSpawnHeight = 3f; // 레이캐스트가 시작될 높이 (y 좌표)
    public int raysPerFrame = 100; // 한 프레임(업데이트) 당 쏠 레이캐스트의 개수
    public float maxRayDistance = 100f; // 레이캐스트가 최대로 나아갈 거리


    public Color rayDebugColor = Color.green;
    public float rayDebugDuration = 0.1f;


    //레이 위치와 방향 조절하는 인스펙터 접근 변수
    public float directionSpeed = 3f;
    public float rotationSpeed = 20f;
    public float heightSpeed = 1f;
    public LayerMask hitLayers; 

   
    
    

    // Start is called before the first frame update
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        

        Vector3 moveDirection = new Vector3 (-horizontalInput,0, verticalInput);
        transform.Translate(moveDirection * directionSpeed * Time.deltaTime, Space.World);

        //z축 회전
        if (Input.GetKey(KeyCode.Q))
        {
            transform.Rotate(Vector3.forward, -rotationSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.E))
        {
            transform.Rotate(Vector3.forward, rotationSpeed * Time.deltaTime);
        }


        //x축 회전
        if(Input.GetKey(KeyCode.R))
        {
            transform.Rotate(Vector3.right, rotationSpeed * Time.deltaTime);
        }

        if (Input.GetKey(KeyCode.F))
        {
            transform.Rotate(Vector3.right, -rotationSpeed * Time.deltaTime);
        }

        //높이
        if (Input.GetKey(KeyCode.Z))
        {
            rainSpawnHeight += heightSpeed*Time.deltaTime;  
        }

        if (Input.GetKey(KeyCode.C))
        {
            rainSpawnHeight -= heightSpeed * Time.deltaTime;
        }


        for (int i = 0; i < raysPerFrame; i++)
        {


            // 레이캐스트 시작점 계산 (RainRaycaster 오브젝트를 기준으로 랜덤 위치)
            float randomX = Random.Range(-rainAreaSize / 2f, rainAreaSize / 2f);
            float randomZ = Random.Range(-rainAreaSize / 2f, rainAreaSize / 2f);
            // 레이캐스트의 실제 월드 포지션 (RainRaycaster 오브젝트의 월드 위치 + 랜덤 오프셋 + 지정된 높이)
            Vector3 rayOrigin = transform.position + new Vector3(randomX, rainSpawnHeight, randomZ);

            // 레이캐스트 방향 
            Vector3 rayDirection = transform.rotation*Vector3.down;

            //맞은 레이 시각화 하기 전에, 모든 레이를 시각화
           Debug.DrawRay(rayOrigin, rayDirection * maxRayDistance, rayDebugColor, rayDebugDuration);

            RaycastHit hit; // 레이캐스트 충돌 정보를 저장할 변수

            // 레이캐스트 발사
            // Physics.Raycast(시작점, 방향, 충돌정보(출력), 최대거리, 감지할레이어)
            if (Physics.Raycast(rayOrigin, rayDirection, out hit, maxRayDistance, hitLayers))
            {
                WaterMeshSimulator hitWaterMeshSimulatorScript = hit.collider.gameObject.GetComponent<WaterMeshSimulator>(); 
                if (hitWaterMeshSimulatorScript != null)
                {
                    hitWaterMeshSimulatorScript.WaterSupplyVertex(hit.point, rayDirection);
                    // 디버깅 시각화: 맞은 레이는 빨간색으로 나타냄
                  


                }
            }
        }
    }
}