using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControl : MonoBehaviour
{
    public GameObject cameraParent;

    Vector3 defaultPosition;
    Quaternion defaultRotation;
    float defaultZoom;


    // Start is called before the first frame update
    void Start()
    {
        cameraParent = GameObject.Find("CameraParent");

        //기본 위치 저장
        defaultPosition = Camera.main.transform.position;
        defaultRotation = cameraParent.transform.rotation;
        defaultZoom = Camera.main.fieldOfView;

    }

    // Update is called once per frame
    void Update()
    {
        //카메라 이동
        if (Input.GetMouseButton(0))
        {
            Camera.main.transform.Translate(Input.GetAxisRaw("Mouse X") / 10,
                Input.GetAxisRaw("Mouse Y") / 10, 0);
        }
        //카메라 회전
        if (Input.GetMouseButton(1))
        {
            cameraParent.transform.Rotate(Input.GetAxisRaw("Mouse Y") * 10,
                Input.GetAxisRaw("Mouse X") * 10, 0);
        }

        //줌인 줌아웃
        Camera.main.fieldOfView += (-20 * Input.GetAxis("Mouse ScrollWheel"));

        if (Camera.main.fieldOfView < 10)
        {
            Camera.main.fieldOfView = 10;
        }

        if (Input.GetMouseButton(2))
        {
            Camera.main.transform.position = defaultPosition;
            cameraParent.transform.rotation = defaultRotation;
            Camera.main.fieldOfView = defaultZoom;

        }
    }
}