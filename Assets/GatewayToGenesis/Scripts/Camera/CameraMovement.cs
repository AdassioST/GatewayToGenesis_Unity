using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Cinemachine;
using System.Collections.Generic;

public class CameraMovement : MonoBehaviour
{
    [SerializeField] CinemachineVirtualCamera virtualCamera;
    [SerializeField] Camera mainCamera;                    

    [SerializeField] float moveSpeed = 5f, zoomSpeed = 1f, screenEdgePercentage = 0.15f, smoothingTime = 0.2f;

    [SerializeField] PolygonCollider2D worldBoundPolygon;
    [SerializeField] Transform cameraFollowPoint;

    [SerializeField] CinemachineConfiner2D confiner2D;

    private Vector3 targetPosition;

    private CinemachineTransposer transposer;
    private Vector3 velocity = Vector3.zero;

    [SerializeField] private float minZoom = 4f, maxZoom = 9f, currentZoom = 7f;

    private Vector3 screenSize;

    private float edgeX, edgeY;

    private void Awake()
    {
        if (virtualCamera != null)
        {
            transposer = virtualCamera.GetCinemachineComponent<CinemachineTransposer>();

            currentZoom = virtualCamera.m_Lens.OrthographicSize;
        }

        screenSize = new Vector3(Screen.width, Screen.height, 0);

        edgeX = screenSize.x * screenEdgePercentage;
        edgeY = screenSize.y * screenEdgePercentage;

        if (confiner2D != null && worldBoundPolygon != null)
        {
            confiner2D.m_BoundingShape2D = worldBoundPolygon;
        }
    }
    private void Update()
    {
        if (IsPointerOverUI()) return;

        HandleZoom();

        HandleMovement();
    }


    private void HandleZoom()
    {
        float scrollInput = Input.mouseScrollDelta.y;

        if (scrollInput != 0)
        {
            currentZoom -= scrollInput * zoomSpeed;
            currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

            virtualCamera.m_Lens.OrthographicSize = currentZoom;

            moveSpeed = Mathf.Lerp(5f, 10f, (currentZoom - minZoom) / (maxZoom - minZoom));
            smoothingTime = Mathf.Lerp(0.5f, 0.2f, (currentZoom - minZoom) / (maxZoom - minZoom));
        }
    }

    private void HandleMovement()
    {
        Vector3 mousePos = Input.mousePosition;

        float moveX = CalculateEdgeMovement(mousePos.x, screenSize.x, edgeX);
        float moveY = CalculateEdgeMovement(mousePos.y, screenSize.y, edgeY);

        targetPosition = cameraFollowPoint.position + new Vector3(moveX, moveY, 0) * moveSpeed * Time.deltaTime;

        targetPosition = ClampPositionWithinBounds(targetPosition);

        SmoothCameraMovement();

        cameraFollowPoint.position = targetPosition;
    }

    private float CalculateEdgeMovement(float mousePos, float screenSize, float edge)
    {
        if (mousePos < edge) return -1f;
        else if (mousePos > screenSize - edge) return 1f;
        return 0f;
    }

    private bool IsPointerOverUI()
    {
        if (EventSystem.current.IsPointerOverGameObject())
        {
            PointerEventData pointerData = new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

            List<RaycastResult> results = new List<RaycastResult>();

            EventSystem.current.RaycastAll(pointerData, results);

            foreach (var result in results)
            {

                Image image = result.gameObject.GetComponent<Image>();

                if (image != null && image.alphaHitTestMinimumThreshold > 0f)
                {
                    return false;
                }
            }
            return true;
        }
        return false;
    }

    private void SmoothCameraMovement()
    {
        mainCamera.transform.position = Vector3.SmoothDamp(mainCamera.transform.position, targetPosition, ref velocity, smoothingTime);
    }

    private Vector3 ClampPositionWithinBounds(Vector3 position)
    {
        float width = mainCamera.orthographicSize * 2 * mainCamera.aspect;
        float height = mainCamera.orthographicSize * 2;

        Vector3 minBounds = worldBoundPolygon.bounds.min;
        Vector3 maxBounds = worldBoundPolygon.bounds.max;

        position.x = Mathf.Clamp(position.x, minBounds.x + width / 2, maxBounds.x - width / 2);
        position.y = Mathf.Clamp(position.y, minBounds.y + height / 2, maxBounds.y - height / 2);

        return position;
    }
}
