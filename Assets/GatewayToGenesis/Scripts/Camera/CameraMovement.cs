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

    [SerializeField] private float minZoom = 3f, maxZoom = 10f, currentZoom = 7f;

    // Bounds percentage adjustment
    [SerializeField, Range(0.5f, 1f)] private float minBoundsScale = 0.9f; // Minimum scale at max zoom-in
    [SerializeField, Range(0.5f, 1f)] private float maxBoundsScale = 1f;   // Maximum scale at max zoom-out

    private Vector3 screenSize;

    private float edgeX, edgeY;

    private Vector3 minBounds, maxBounds;
    private float cameraWidth, cameraHeight;

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

        UpdateCameraBounds();
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
            float newZoom = Mathf.Clamp(currentZoom - scrollInput * zoomSpeed, minZoom, maxZoom);

            currentZoom = newZoom;
            virtualCamera.m_Lens.OrthographicSize = currentZoom;

            // Update move speed and smoothing dynamically
            moveSpeed = Mathf.Lerp(5f, 10f, (currentZoom - minZoom) / (maxZoom - minZoom));
            smoothingTime = Mathf.Lerp(0.5f, 0.2f, (currentZoom - minZoom) / (maxZoom - minZoom));

            // Update bounds for the new zoom level
            UpdateCameraBounds();
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

    private void UpdateCameraBounds()
    {
        // Calculate the zoom factor (e.g., 1 at max zoom-out, less at higher zoom-in levels)
        float zoomFactor = (currentZoom - minZoom) / (maxZoom - minZoom);

        // Lerp the bounds scale between minBoundsScale and maxBoundsScale based on zoomFactor
        float boundsScale = Mathf.Lerp(minBoundsScale, maxBoundsScale, zoomFactor);

        // Adjust bounds based on scaled size
        float adjustedWidth = worldBoundPolygon.bounds.size.x * boundsScale;
        float adjustedHeight = worldBoundPolygon.bounds.size.y * boundsScale;

        Vector2 adjustedMin = worldBoundPolygon.bounds.center - new Vector3(adjustedWidth / 2, adjustedHeight / 2, 0);
        Vector2 adjustedMax = worldBoundPolygon.bounds.center + new Vector3(adjustedWidth / 2, adjustedHeight / 2, 0);

        minBounds = new Vector3(adjustedMin.x, adjustedMin.y, 0);
        maxBounds = new Vector3(adjustedMax.x, adjustedMax.y, 0);

        // Update the camera size dynamically
        cameraWidth = currentZoom * 2 * mainCamera.aspect;
        cameraHeight = currentZoom * 2;
    }

    private Vector3 ClampPositionWithinBounds(Vector3 position)
    {
        position.x = Mathf.Clamp(position.x, minBounds.x + cameraWidth / 2, maxBounds.x - cameraWidth / 2);
        position.y = Mathf.Clamp(position.y, minBounds.y + cameraHeight / 2, maxBounds.y - cameraHeight / 2);

        return position;
    }
}
