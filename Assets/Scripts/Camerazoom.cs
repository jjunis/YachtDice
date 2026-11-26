using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// 주사위가 멈춘 뒤 카메라를 주사위 쪽으로 확대해서 눈이 잘 보이게 하고,
// 다음 행동(스페이스바 / 족보 선택) 때 원래 위치로 되돌림
public class CameraZoom : MonoBehaviour
{
    [SerializeField] private Camera cam;

    [Header("위에서 똑바로 보기")]
    [SerializeField] private bool topDownView = true;       // 체크하면 주사위를 바로 위에서 내려다봄 (눈이 정면으로 보임)
    [SerializeField] private float fitPadding = 1.0f;       // 주사위 바깥 여백 (월드 단위, 클수록 더 멀리서 넓게 보임)
    [SerializeField] private float minHeight = 2.0f;        // 카메라가 주사위 위로 올라갈 최소 높이 (너무 가까워지는 것 방지)

    [Header("주사위 판에 맞추기")]
    [SerializeField] private Transform[] trayTargets;       // 주사위 판 오브젝트들 (바닥 + 노란 테두리). 비우면 주사위 위치로 맞춤
    [SerializeField] private float trayPadding = 0.1f;      // 판 바깥 여백 (월드 단위). 0에 가까울수록 판이 화면에 꽉 참
    [SerializeField] private bool cropOutsideTray = true;   // 체크하면 판 바깥은 검게 가려서 판까지만 보이게 함

    [Header("Zoom (위에서 똑바로 보기를 끈 경우)")]
    [Range(0.15f, 1f)]
    [SerializeField] private float zoomRatio = 0.45f;       // 주사위까지의 거리를 이 비율로 줄임 (작을수록 더 확대)
    [SerializeField] private Vector3 focusOffset = Vector3.zero; // 바라보는 지점 보정 (주사위 중심 기준)
    [SerializeField] private float zoomInDuration = 0.35f;
    [SerializeField] private float zoomOutDuration = 0.25f;

    [Header("유지")]
    [SerializeField] private bool stayUntilNextAction = false; // 체크하면 다음 스페이스바/족보 선택 때까지 계속 확대 상태
    [SerializeField] private float holdTime = 2f;              // 체크 안 했을 때 확대 상태를 유지하는 시간(초)

    private Coroutine zoomRoutine;
    private bool hasTray;           // 판 범위를 구했는지
    private Bounds trayBounds;
    private GameObject maskRoot;    // 판 바깥을 가리는 검은 막대 4개
    private RectTransform[] bars;
    private bool hasBase;           // 원래 카메라 위치를 저장했는지
    private Vector3 basePos;
    private Quaternion baseRot;

    // 확대 중(확대하는 중 ~ 원래대로 돌아오기 전까지)이면 true
    public bool IsZoomed => hasBase;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    // 주사위들 쪽으로 확대 (기다리지 않고 바로 반환)
    public void ZoomIn(Component[] targets)
    {
        if (cam == null || targets == null || targets.Length == 0) return;

        StopRunning();
        zoomRoutine = StartCoroutine(ZoomInRoutine(targets));
    }

    // 원래 위치로 되돌림 (기다리지 않음)
    public void ZoomOutNow()
    {
        if (!hasBase) return;

        StopRunning();
        zoomRoutine = StartCoroutine(ZoomOut());
    }

    // 원래 위치로 되돌림 (끝날 때까지 기다리고 싶을 때: yield return cameraZoom.ZoomOut())
    public IEnumerator ZoomOut()
    {
        if (!hasBase || cam == null) yield break;

        StopRunning();
        yield return MoveTo(basePos, baseRot, zoomOutDuration);
        hasBase = false;
        zoomRoutine = null;
    }

    private IEnumerator ZoomInRoutine(Component[] targets)
    {
        Transform camT = cam.transform;

        if (!hasBase)
        {
            basePos = camT.position;
            baseRot = camT.rotation;
            hasBase = true;
        }

        // 주사위들의 중심
        Vector3 center = Vector3.zero;
        int count = 0;
        foreach (Component c in targets)
        {
            if (c == null) continue;
            center += c.transform.position;
            count++;
        }
        if (count == 0) yield break;

        center = center / count + focusOffset;

        Vector3 targetPos;
        Quaternion targetRot;

        if (topDownView)
        {
            // 화면의 '위쪽'이 원래 카메라가 보던 방향이 되도록 함 (방향이 돌아가지 않게)
            Vector3 screenUp = Vector3.ProjectOnPlane(baseRot * Vector3.forward, Vector3.up);
            if (screenUp.sqrMagnitude < 0.0001f) screenUp = Vector3.forward;
            screenUp.Normalize();
            Vector3 screenRight = Vector3.Cross(Vector3.up, screenUp);

            float halfW = 0f, halfH = 0f;
            hasTray = TryGetTrayBounds(out trayBounds);

            if (hasTray)
            {
                // 주사위 판이 화면에 꽉 차도록: 판 중심을 보고, 판 모서리 8개가 들어오는 높이를 계산
                center = trayBounds.center + focusOffset;

                Vector3 mn = trayBounds.min, mx = trayBounds.max;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = new Vector3(
                        (i & 1) == 0 ? mn.x : mx.x,
                        (i & 2) == 0 ? mn.y : mx.y,
                        (i & 4) == 0 ? mn.z : mx.z);

                    Vector3 d = corner - center;
                    halfW = Mathf.Max(halfW, Mathf.Abs(Vector3.Dot(d, screenRight)));
                    halfH = Mathf.Max(halfH, Mathf.Abs(Vector3.Dot(d, screenUp)));
                }

                halfW += trayPadding;
                halfH += trayPadding;
            }
            else
            {
                // 주사위들이 화면에 다 들어오도록 필요한 높이 계산
                foreach (Component c in targets)
                {
                    if (c == null) continue;
                    Vector3 d = c.transform.position - center;
                    halfW = Mathf.Max(halfW, Mathf.Abs(Vector3.Dot(d, screenRight)));
                    halfH = Mathf.Max(halfH, Mathf.Abs(Vector3.Dot(d, screenUp)));
                }
                halfW += fitPadding;
                halfH += fitPadding;
            }

            float tanHalfFov = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float needH = halfH / tanHalfFov;
            float needW = halfW / (tanHalfFov * Mathf.Max(0.1f, cam.aspect));
            float height = Mathf.Max(minHeight, Mathf.Max(needH, needW));

            targetPos = center + Vector3.up * height;
            targetRot = Quaternion.LookRotation(Vector3.down, screenUp);
        }
        else
        {
            // 카메라를 주사위 중심 쪽으로 당기고, 중심을 바라보게 함
            targetPos = center + (basePos - center) * zoomRatio;
            targetRot = Quaternion.LookRotation(center - targetPos, Vector3.up);
        }

        yield return MoveTo(targetPos, targetRot, zoomInDuration);

        // 판 바깥을 검게 가려서 판까지만 보이게 함
        if (topDownView && hasTray && cropOutsideTray)
        {
            ShowMask();
        }

        if (stayUntilNextAction)
        {
            zoomRoutine = null;
            yield break;
        }

        yield return new WaitForSeconds(holdTime);
        HideMask();
        yield return MoveTo(basePos, baseRot, zoomOutDuration);

        hasBase = false;
        zoomRoutine = null;
    }

    private IEnumerator MoveTo(Vector3 toPos, Quaternion toRot, float duration)
    {
        Transform camT = cam.transform;
        Vector3 fromPos = camT.position;
        Quaternion fromRot = camT.rotation;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float p = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(time / duration));

            camT.SetPositionAndRotation(
                Vector3.Lerp(fromPos, toPos, p),
                Quaternion.Slerp(fromRot, toRot, p));

            yield return null;
        }

        camT.SetPositionAndRotation(toPos, toRot);
    }

    // trayTargets의 렌더러들을 모두 감싸는 범위를 구함
    private bool TryGetTrayBounds(out Bounds result)
    {
        result = new Bounds();
        bool has = false;

        if (trayTargets == null) return false;

        foreach (Transform t in trayTargets)
        {
            if (t == null) continue;

            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled) continue;

                if (!has) { result = r.bounds; has = true; }
                else result.Encapsulate(r.bounds);
            }
        }

        return has;
    }

    // 판의 화면상 사각형 바깥을 검은 막대 4개로 가림
    private void ShowMask()
    {
        EnsureMask();

        float w = Screen.width, h = Screen.height;
        float minX = w, maxX = 0f, minY = h, maxY = 0f;

        Vector3 mn = trayBounds.min, mx = trayBounds.max;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3(
                (i & 1) == 0 ? mn.x : mx.x,
                (i & 2) == 0 ? mn.y : mx.y,
                (i & 4) == 0 ? mn.z : mx.z);

            Vector3 sp = cam.WorldToScreenPoint(corner);
            minX = Mathf.Min(minX, sp.x); maxX = Mathf.Max(maxX, sp.x);
            minY = Mathf.Min(minY, sp.y); maxY = Mathf.Max(maxY, sp.y);
        }

        minX = Mathf.Clamp(minX, 0f, w); maxX = Mathf.Clamp(maxX, 0f, w);
        minY = Mathf.Clamp(minY, 0f, h); maxY = Mathf.Clamp(maxY, 0f, h);

        SetBar(bars[0], 0f, 0f, minX, h);              // 왼쪽
        SetBar(bars[1], maxX, 0f, w - maxX, h);        // 오른쪽
        SetBar(bars[2], minX, 0f, maxX - minX, minY);  // 아래
        SetBar(bars[3], minX, maxY, maxX - minX, h - maxY); // 위

        maskRoot.SetActive(true);
    }

    private void HideMask()
    {
        if (maskRoot != null) maskRoot.SetActive(false);
    }

    private static void SetBar(RectTransform bar, float x, float y, float width, float height)
    {
        bar.anchorMin = Vector2.zero;
        bar.anchorMax = Vector2.zero;
        bar.pivot = Vector2.zero;
        bar.anchoredPosition = new Vector2(x, y);
        bar.sizeDelta = new Vector2(Mathf.Max(0f, width), Mathf.Max(0f, height));
    }

    private void EnsureMask()
    {
        if (maskRoot != null) return;

        maskRoot = new GameObject("ZoomCropMask", typeof(Canvas));
        Canvas canvas = maskRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10;   // 기존 UI(점수판, 결과 글자)보다 아래에 그려서 글자는 그대로 보이게 함

        bars = new RectTransform[4];
        for (int i = 0; i < 4; i++)
        {
            GameObject bar = new GameObject("Bar" + i, typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(maskRoot.transform, false);
            bar.GetComponent<Image>().color = Color.black;
            bar.GetComponent<Image>().raycastTarget = false;
            bars[i] = bar.GetComponent<RectTransform>();
        }

        maskRoot.SetActive(false);
    }

    private void StopRunning()
    {
        HideMask();

        if (zoomRoutine != null)
        {
            StopCoroutine(zoomRoutine);
            zoomRoutine = null;
        }
    }
}