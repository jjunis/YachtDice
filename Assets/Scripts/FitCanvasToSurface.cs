using UnityEngine;

// World Space Canvas를 Plane 표면 위에 눕혀서, Plane 크기에 맞게 자동으로 배치
[RequireComponent(typeof(Canvas))]
public class FitCanvasToSurface : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Renderer surface;           // Plane의 Mesh Renderer

    [Header("Fit")]
    [SerializeField] private float heightOffset = 0.02f; // 표면에서 띄우는 높이 (겹쳐서 깜빡이면 올리기)
    [Range(0.5f, 1f)]
    [SerializeField] private float fill = 0.92f;         // Plane을 얼마나 채울지 (1 = 가득)
    [SerializeField] private bool rotate180 = false;     // 글자가 거꾸로 보이면 체크

    private void Start()
    {
        Fit();
    }

    // 인스펙터에서 ? > Fit 을 누르면 에디터에서도 바로 확인 가능
    [ContextMenu("Fit")]
    public void Fit()
    {
        if (surface == null)
        {
            Debug.LogWarning("FitCanvasToSurface : Surface가 연결되지 않았습니다.");
            return;
        }

        Canvas canvas = GetComponent<Canvas>();
        RectTransform rt = (RectTransform)transform;

        canvas.renderMode = RenderMode.WorldSpace;

        // 클릭 처리에 필요한 카메라
        if (canvas.worldCamera == null)
        {
            canvas.worldCamera = Camera.main;
        }

        Bounds b = surface.bounds;

        // 위치 : Plane 중앙, 표면 바로 위
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.position = new Vector3(b.center.x, b.max.y + heightOffset, b.center.z);

        // 방향 : 위를 보도록 눕힘
        rt.rotation = Quaternion.Euler(90f, rotate180 ? 180f : 0f, 0f);

        // 크기 : Plane 안에 들어오도록 (Canvas의 Width/Height 비율 유지)
        float scale = Mathf.Min(b.size.x / rt.rect.width, b.size.z / rt.rect.height) * fill;
        rt.localScale = Vector3.one * scale;
    }
}