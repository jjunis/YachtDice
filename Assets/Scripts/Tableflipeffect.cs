using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// YEET 성공 시 게임 판을 엎어버리는 연출.
// 판(Tabl, Plane, 점수판 캔버스 등)이 한쪽 모서리를 축으로 뒤집히고, 주사위는 공중으로 튕겨 나감.
// 연출이 끝나면 모든 것이 원래 자리로 돌아옴 (그 뒤에 DiceManager가 게임을 재시작)
public class TableFlipEffect : MonoBehaviour
{
    [Header("엎을 대상 (판에 속한 것들)")]
    [SerializeField] private Transform[] boardObjects;   // Tabl, Plane, AIPlane, ScoreCanvas, AIScoreCanvas ...
    [SerializeField] private Dice[] dice;                // 튕겨 나갈 주사위 5개

    [Header("손 모델 (선택)")]
    [SerializeField] private GameObject handModel;       // 판을 엎는 손 모델 (Collider / Rigidbody 없어야 함)
    [SerializeField] private Transform handStartPoint;   // 손이 처음 나타나는 위치 (화면 밖 아래쪽 등)
    [SerializeField] private Transform handGrabPoint;    // 손이 판을 잡는 지점 (판의 가까운 쪽 모서리). Board Objects에도 넣을 것!
    [SerializeField] private Vector3 handEulerOffset = Vector3.zero;  // 손 방향 보정
    [SerializeField] private float handAppearDuration = 0.4f;         // 손이 날아와 판을 잡는 시간

    [Header("엎은 뒤 등장하는 모델 (선택)")]
    [SerializeField] private GameObject afterModel;      // 판을 엎은 뒤에 나오는 모델 (엿 날리는 손 등)
    [Header("  카메라 정면에 띄우기 (엿 날리기용)")]
    [SerializeField] private bool afterInFrontOfCamera = true;       // 체크하면 모델이 카메라 정면에 나타나 카메라를 향함 (아래 Point 칸은 무시)
    [SerializeField] private Vector2 afterViewportPos = new Vector2(0.5f, 0.5f); // 화면 위치 (0~1, 0.5/0.5 = 화면 정중앙)
    [SerializeField] private bool afterUseWorldRotation = true;      // 체크하면 아래 월드 회전값 그대로 사용 (카메라가 고정이면 이쪽이 정확)
    [SerializeField] private Vector3 afterWorldEuler = new Vector3(0f, 0f, 90f);  // 엿 날리는 모델의 회전 (가운데 손가락이 위, 카메라를 향함)
    [SerializeField] private float afterDistance = 6f;               // 카메라에서 떨어진 거리 (클수록 작게 보임)
    [SerializeField] private float afterRiseFrom = 3f;               // 화면 아래에서 이만큼 아래서 튀어오름

    [Header("  직접 위치 지정 (위 체크를 끈 경우)")]
    [SerializeField] private Transform afterStartPoint;  // 등장 시작 위치 (화면 밖/아래)
    [SerializeField] private Transform afterEndPoint;    // 최종 위치 (카메라에 잘 보이는 곳)
    [SerializeField] private Vector3 afterEulerOffset = Vector3.zero;  // 방향 보정
    [SerializeField] private float afterAppearDuration = 0.35f;        // 튀어나오는 시간
    [SerializeField] private float afterStay = 0.8f;                   // 등장 후 머무는 시간

    [Header("회전 축")]
    [SerializeField] private Transform pivot;            // 판의 '먼 쪽 모서리'에 둔 빈 오브젝트 (이 점을 축으로 뒤집힘)
    [SerializeField] private Vector3 flipAxis = Vector3.right;  // 회전 축 (월드 기준)
    [SerializeField] private float flipAngle = 180f;     // 엎는 각도 (180 = 완전히 뒤집힘. 반대로 넘어가면 -180)

    [Header("위로 던지기")]
    [SerializeField] private Vector3 tossOffset = new Vector3(0f, 2f, 6f); // 판이 날아가는 거리 (월드 기준 X, Y, Z). Z가 플러스 = 카메라에서 먼 쪽(화면 위쪽)

    [Header("날아가기 (엎은 뒤 판이 날아감)")]
    [SerializeField] private Vector3 flyVelocity = new Vector3(0f, 4f, 16f);  // 날아가는 속도 (월드 기준). Z가 플러스 = 카메라 반대쪽(화면 위쪽)
    [SerializeField] private Vector3 flyGravity = new Vector3(0f, -6f, 0f);   // 중력 (아래로 휘어지는 정도). 0이면 직선으로 날아감
    [SerializeField] private Vector3 flyAcceleration = new Vector3(0f, 0f, 25f); // 점점 빨라지는 정도. 화면 밖으로 확실히 나가게 해 줌
    [SerializeField] private float flyDuration = 0.8f;                         // 최소로 날아가는 시간(초)
    [SerializeField] private bool flyUntilOffScreen = true;                    // 체크하면 판이 화면 밖으로 완전히 나갈 때까지 계속 날아감
    [SerializeField] private float flyMaxDuration = 3f;                        // 화면 밖으로 안 나가도 이 시간이 지나면 끝냄(안전장치)
    [SerializeField] private float flyExtraSpin = 90f;                         // 날아가며 더 기울어지는 각도 (0이면 엎은 각도 그대로)
    [SerializeField] private bool smoothReturn = false;                        // 체크하면 날아간 판이 천천히 되돌아옴. 끄면 화면 밖에서 바로 제자리로

    [Header("Timing")]
    [SerializeField] private float windUpAngle = -8f;    // 엎기 전에 살짝 반대로 기울이는 각도
    [SerializeField] private float windUpDuration = 0.25f;
    [SerializeField] private float flipDuration = 0.35f; // 뒤집히는 시간 (짧을수록 격하게)
    [SerializeField] private float returnDuration = 0.6f;// 원래 자리로 돌아오는 시간

    [Header("주사위 튕김")]
    [SerializeField] private Vector3 diceLaunchVelocity = new Vector3(0f, 7f, 4f); // 위쪽 + 먼 쪽으로
    [SerializeField] private float diceRandomPower = 2.5f;   // 방향 무작위 정도
    [SerializeField] private float diceSpinPower = 20f;      // 회전 세기

    [Header("Camera Shake")]
    [SerializeField] private Camera cam;
    [SerializeField] private float shakePower = 0.3f;
    [SerializeField] private float shakeDuration = 0.5f;

    [Header("Sound (선택)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip flipClip;

    private struct Pose
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    private bool handFollowing = false;
    private readonly List<Renderer> boardRenderers = new List<Renderer>();
    private Vector3 flyOffset = Vector3.zero;   // 날아가는 동안의 추가 이동량
    private float lift = 0f;   // 던지기 진행도 (0 = 제자리, 1 = 가장 멀리). 실제 이동량 = tossOffset * lift

    private void Awake()
    {
        if (cam == null) cam = Camera.main;

        if (handModel != null) handModel.SetActive(false);
        if (afterModel != null) afterModel.SetActive(false);

        // 판이 화면 밖으로 나갔는지 검사하기 위해 판에 속한 렌더러를 모아 둠
        if (boardObjects != null)
        {
            foreach (Transform bt in boardObjects)
            {
                if (bt == null) continue;
                boardRenderers.AddRange(bt.GetComponentsInChildren<Renderer>(true));
            }
        }

        // 부모가 먼저 처리되도록 계층 깊이순으로 정렬 (부모/자식을 같이 넣어도 꼬이지 않게)
        if (boardObjects != null)
        {
            Array.Sort(boardObjects, (a, b) => Depth(a).CompareTo(Depth(b)));
        }
    }

    private static int Depth(Transform t)
    {
        int depth = 0;
        while (t != null && t.parent != null)
        {
            depth++;
            t = t.parent;
        }
        return depth;
    }

    // onFlipped : 완전히 뒤집힌 순간에 호출하고 싶을 때 (선택)
    public IEnumerator Play(Action onFlipped = null)
    {
        if (boardObjects == null || boardObjects.Length == 0 || pivot == null)
        {
            Debug.LogWarning("TableFlipEffect : Board Objects 또는 Pivot이 연결되지 않았습니다.");
            onFlipped?.Invoke();
            yield break;
        }

        flyOffset = Vector3.zero;
        lift = 0f;

        Vector3 axis = flipAxis.sqrMagnitude < 0.0001f ? Vector3.right : flipAxis.normalized;
        Vector3 pivotPos = pivot.position;

        // 1. 원래 상태 저장
        Pose[] boardStart = new Pose[boardObjects.Length];
        for (int i = 0; i < boardObjects.Length; i++)
        {
            boardStart[i].position = boardObjects[i].position;
            boardStart[i].rotation = boardObjects[i].rotation;
        }

        int diceCount = dice != null ? dice.Length : 0;
        Pose[] diceStart = new Pose[diceCount];
        Rigidbody[] diceBody = new Rigidbody[diceCount];
        bool[] diceKinematic = new bool[diceCount];
        bool[] diceGravity = new bool[diceCount];

        for (int i = 0; i < diceCount; i++)
        {
            if (dice[i] == null) continue;

            diceStart[i].position = dice[i].transform.position;
            diceStart[i].rotation = dice[i].transform.rotation;

            diceBody[i] = dice[i].GetComponent<Rigidbody>();
            if (diceBody[i] == null) diceBody[i] = dice[i].GetComponentInChildren<Rigidbody>();

            if (diceBody[i] != null)
            {
                diceKinematic[i] = diceBody[i].isKinematic;
                diceGravity[i] = diceBody[i].useGravity;
            }
        }

        // 1-2. 손 모델이 날아와 판 모서리를 잡음
        if (handModel != null && handStartPoint != null && handGrabPoint != null)
        {
            handModel.SetActive(true);
            yield return MoveModel(handModel.transform, handStartPoint, handGrabPoint, handEulerOffset, handAppearDuration);
            handFollowing = true;
        }

        // 2. 엎기 전 살짝 기울이기 (반대 방향으로 힘을 모음)
        yield return Tilt(boardStart, pivotPos, axis, 0f, windUpAngle, 0f, 0f, windUpDuration, EaseOut);

        // 3. 확 뒤집기
        if (audioSource != null && flipClip != null) audioSource.PlayOneShot(flipClip);
        StartCoroutine(Shake());

        // 뒤집히기 시작하는 순간 주사위를 튕겨냄
        LaunchDice(diceBody);

        // 한쪽 모서리를 들어 올리며 위로 던짐 (회전 + 위로 솟구침)
        yield return Tilt(boardStart, pivotPos, axis, windUpAngle, flipAngle, 0f, 1f, flipDuration, EaseOut);

        onFlipped?.Invoke();

        // 손은 판을 놓고 사라짐
        handFollowing = false;
        if (handModel != null) handModel.SetActive(false);

        // 4. 엎어진 판이 날아가는 동안 + 다른 모델 등장
        Coroutine fly = StartCoroutine(FlyRoutine(boardStart, pivotPos, axis));

        if (afterModel != null && afterInFrontOfCamera && cam != null)
        {
            // 카메라 정면: 화면 위치/거리로 끝 지점을 정하고, 카메라를 향하도록 회전
            Transform camT = cam.transform;
            Vector3 endPos = cam.ViewportToWorldPoint(new Vector3(afterViewportPos.x, afterViewportPos.y, afterDistance));
            Vector3 startPos = endPos - camT.up * afterRiseFrom;
            Quaternion faceRot = afterUseWorldRotation
                ? Quaternion.Euler(afterWorldEuler)
                : camT.rotation * Quaternion.Euler(afterEulerOffset);

            afterModel.SetActive(true);
            yield return MovePose(afterModel.transform, startPos, faceRot, endPos, faceRot, afterAppearDuration);
            yield return new WaitForSeconds(afterStay);
            afterModel.SetActive(false);
        }
        else if (afterModel != null && afterStartPoint != null && afterEndPoint != null)
        {
            afterModel.SetActive(true);
            yield return MoveModel(afterModel.transform, afterStartPoint, afterEndPoint, afterEulerOffset, afterAppearDuration);
            yield return new WaitForSeconds(afterStay);
            afterModel.SetActive(false);
        }

        yield return fly; // 날아가는 시간이 끝날 때까지 기다림

        // 5. 원래 자리로 돌아옴
        if (smoothReturn)
        {
            yield return ReturnRoutine(boardStart, pivotPos, axis);
        }
        else
        {
            // 판이 화면 밖으로 날아갔으므로 잠깐 기다렸다가 바로 제자리로
            yield return new WaitForSeconds(0.3f);
        }

        // 6. 주사위 원위치 + 물리 초기화
        for (int i = 0; i < diceCount; i++)
        {
            if (dice[i] == null) continue;

            Rigidbody rb = diceBody[i];
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = diceKinematic[i];
                rb.useGravity = diceGravity[i];
            }

            dice[i].transform.SetPositionAndRotation(diceStart[i].position, diceStart[i].rotation);
        }

        flyOffset = Vector3.zero;
        lift = 0f;

        // 연출용 모델 정리
        handFollowing = false;
        if (handModel != null) handModel.SetActive(false);
        if (afterModel != null) afterModel.SetActive(false);

        // 판도 오차 없이 원래 값으로 고정
        for (int i = 0; i < boardObjects.Length; i++)
        {
            boardObjects[i].SetPositionAndRotation(boardStart[i].position, boardStart[i].rotation);
        }
    }

    // 고정된 위치/회전으로 튀어나오듯 이동 (살짝 오버슈트)
    private IEnumerator MovePose(Transform model, Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot, float duration)
    {
        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float p = EaseOutBack(Mathf.Clamp01(time / duration));

            model.SetPositionAndRotation(
                Vector3.LerpUnclamped(fromPos, toPos, p),
                Quaternion.SlerpUnclamped(fromRot, toRot, p));

            yield return null;
        }

        model.SetPositionAndRotation(toPos, toRot);
    }

    // 엎어진 판이 포물선을 그리며 날아감
    private IEnumerator FlyRoutine(Pose[] start, Vector3 pivotPos, Vector3 axis)
    {
        float sign = flipAngle >= 0f ? 1f : -1f;
        float time = 0f;
        float limit = flyUntilOffScreen ? Mathf.Max(flyDuration, flyMaxDuration) : flyDuration;

        while (time < limit)
        {
            time += Time.deltaTime;

            flyOffset = flyVelocity * time + 0.5f * (flyGravity + flyAcceleration) * time * time;
            float spin = flyExtraSpin * sign * Mathf.Clamp01(time / flyDuration);

            ApplyAngle(start, pivotPos, axis, flipAngle + spin);

            // 최소 시간이 지난 뒤, 판이 화면 밖으로 완전히 나가면 끝
            if (flyUntilOffScreen && time >= flyDuration && !IsBoardOnScreen())
            {
                break;
            }

            yield return null;
        }
    }

    // 판의 렌더러 중 하나라도 카메라 화면 안에 있으면 true
    private bool IsBoardOnScreen()
    {
        if (cam == null) return false;

        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(cam);

        foreach (Renderer r in boardRenderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;

            if (GeometryUtility.TestPlanesAABB(planes, r.bounds))
            {
                return true;
            }
        }

        return false;
    }

    // 날아간 판이 천천히 제자리로 돌아옴 (Smooth Return을 켠 경우)
    private IEnumerator ReturnRoutine(Pose[] start, Vector3 pivotPos, Vector3 axis)
    {
        float sign = flipAngle >= 0f ? 1f : -1f;
        float fromAngle = flipAngle + flyExtraSpin * sign;
        Vector3 fromOffset = flyOffset;
        float fromLift = lift;
        float time = 0f;

        while (time < returnDuration)
        {
            time += Time.deltaTime;
            float p = EaseInOut(Mathf.Clamp01(time / returnDuration));

            flyOffset = Vector3.Lerp(fromOffset, Vector3.zero, p);
            lift = Mathf.Lerp(fromLift, 0f, p);
            ApplyAngle(start, pivotPos, axis, Mathf.Lerp(fromAngle, 0f, p));
            yield return null;
        }
    }

    // 모델을 from 위치에서 to 위치로 튀어나오듯 이동 (살짝 오버슈트)
    private IEnumerator MoveModel(Transform model, Transform from, Transform to, Vector3 eulerOffset, float duration)
    {
        Vector3 fromPos = from.position;
        Quaternion fromRot = from.rotation * Quaternion.Euler(eulerOffset);

        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float p = EaseOutBack(Mathf.Clamp01(time / duration));

            // 이동 중에도 목표가 움직일 수 있으므로(판 위 지점) 매 프레임 목표 위치를 다시 읽음
            Vector3 toPos = to.position;
            Quaternion toRot = to.rotation * Quaternion.Euler(eulerOffset);

            model.SetPositionAndRotation(
                Vector3.LerpUnclamped(fromPos, toPos, p),
                Quaternion.SlerpUnclamped(fromRot, toRot, p));

            yield return null;
        }

        model.SetPositionAndRotation(to.position, to.rotation * Quaternion.Euler(eulerOffset));
    }

    // 판 전체를 pivot 기준으로 fromAngle -> toAngle 만큼 회전시킴
    private IEnumerator Tilt(Pose[] start, Vector3 pivotPos, Vector3 axis, float fromAngle, float toAngle, float fromLift, float toLift, float duration, Func<float, float> ease)
    {
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float p = ease(Mathf.Clamp01(time / duration));
            lift = Mathf.LerpUnclamped(fromLift, toLift, p);
            ApplyAngle(start, pivotPos, axis, Mathf.LerpUnclamped(fromAngle, toAngle, p));
            yield return null;
        }

        lift = toLift;
        ApplyAngle(start, pivotPos, axis, toAngle);
    }

    private void ApplyAngle(Pose[] start, Vector3 pivotPos, Vector3 axis, float angle)
    {
        Quaternion rot = Quaternion.AngleAxis(angle, axis);

        for (int i = 0; i < boardObjects.Length; i++)
        {
            if (boardObjects[i] == null) continue;

            Vector3 newPos = pivotPos + rot * (start[i].position - pivotPos) + tossOffset * lift + flyOffset;
            Quaternion newRot = rot * start[i].rotation;
            boardObjects[i].SetPositionAndRotation(newPos, newRot);
        }

        // 손이 잡은 모서리를 따라 같이 움직임
        if (handFollowing && handModel != null && handGrabPoint != null)
        {
            handModel.transform.SetPositionAndRotation(
                handGrabPoint.position,
                handGrabPoint.rotation * Quaternion.Euler(handEulerOffset));
        }
    }

    private void LaunchDice(Rigidbody[] bodies)
    {
        foreach (Rigidbody rb in bodies)
        {
            if (rb == null) continue;

            rb.isKinematic = false;
            rb.useGravity = true;

            Vector3 randomDir = new Vector3(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(0f, 1f),
                UnityEngine.Random.Range(-1f, 1f)) * diceRandomPower;

            rb.linearVelocity = diceLaunchVelocity + randomDir;
            rb.angularVelocity = UnityEngine.Random.insideUnitSphere * diceSpinPower;
        }
    }

    private IEnumerator Shake()
    {
        if (cam == null) yield break;

        Transform camT = cam.transform;
        Vector3 original = camT.localPosition;
        float time = 0f;

        while (time < shakeDuration)
        {
            time += Time.deltaTime;

            float fade = 1f - (time / shakeDuration);
            camT.localPosition = original + UnityEngine.Random.insideUnitSphere * shakePower * fade;

            yield return null;
        }

        camT.localPosition = original;
    }

    private static float Linear(float t) { return t; }
    private static float EaseIn(float t) { return t * t * t; }
    private static float EaseOut(float t) { return 1f - (1f - t) * (1f - t); }
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
    private static float EaseInOut(float t) { return t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f; }
}