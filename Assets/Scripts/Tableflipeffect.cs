using System;
using System.Collections;
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
    [SerializeField] private Transform afterStartPoint;  // 등장 시작 위치 (화면 밖/아래)
    [SerializeField] private Transform afterEndPoint;    // 최종 위치 (카메라에 잘 보이는 곳)
    [SerializeField] private Vector3 afterEulerOffset = Vector3.zero;  // 방향 보정
    [SerializeField] private float afterAppearDuration = 0.35f;        // 튀어나오는 시간
    [SerializeField] private float afterStay = 0.8f;                   // 등장 후 머무는 시간

    [Header("회전 축")]
    [SerializeField] private Transform pivot;            // 판의 '먼 쪽 모서리'에 둔 빈 오브젝트 (이 점을 축으로 뒤집힘)
    [SerializeField] private Vector3 flipAxis = Vector3.right;  // 회전 축 (월드 기준)
    [SerializeField] private float flipAngle = 115f;     // 뒤집히는 각도 (반대로 뒤집히면 -115)

    [Header("Timing")]
    [SerializeField] private float windUpAngle = -8f;    // 엎기 전에 살짝 반대로 기울이는 각도
    [SerializeField] private float windUpDuration = 0.25f;
    [SerializeField] private float flipDuration = 0.35f; // 뒤집히는 시간 (짧을수록 격하게)
    [SerializeField] private float holdDuration = 1.0f;  // 뒤집힌 채로 주사위가 날아다니는 시간
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

    private void Awake()
    {
        if (cam == null) cam = Camera.main;

        if (handModel != null) handModel.SetActive(false);
        if (afterModel != null) afterModel.SetActive(false);

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
        yield return Tilt(boardStart, pivotPos, axis, 0f, windUpAngle, windUpDuration, EaseOut);

        // 3. 확 뒤집기
        if (audioSource != null && flipClip != null) audioSource.PlayOneShot(flipClip);
        StartCoroutine(Shake());

        // 뒤집히기 시작하는 순간 주사위를 튕겨냄
        LaunchDice(diceBody);

        yield return Tilt(boardStart, pivotPos, axis, windUpAngle, flipAngle, flipDuration, EaseIn);

        onFlipped?.Invoke();

        // 손은 판을 놓고 사라짐
        handFollowing = false;
        if (handModel != null) handModel.SetActive(false);

        // 4. 뒤집힌 채로 주사위가 흩날리는 시간 + 다른 모델 등장
        if (afterModel != null && afterStartPoint != null && afterEndPoint != null)
        {
            afterModel.SetActive(true);
            yield return MoveModel(afterModel.transform, afterStartPoint, afterEndPoint, afterEulerOffset, afterAppearDuration);
            yield return new WaitForSeconds(afterStay);
            afterModel.SetActive(false);
        }

        yield return new WaitForSeconds(holdDuration);

        // 5. 판이 원래 자리로 돌아옴
        yield return Tilt(boardStart, pivotPos, axis, flipAngle, 0f, returnDuration, EaseInOut);

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
    private IEnumerator Tilt(Pose[] start, Vector3 pivotPos, Vector3 axis, float fromAngle, float toAngle, float duration, Func<float, float> ease)
    {
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float p = ease(Mathf.Clamp01(time / duration));
            ApplyAngle(start, pivotPos, axis, Mathf.LerpUnclamped(fromAngle, toAngle, p));
            yield return null;
        }

        ApplyAngle(start, pivotPos, axis, toAngle);
    }

    private void ApplyAngle(Pose[] start, Vector3 pivotPos, Vector3 axis, float angle)
    {
        Quaternion rot = Quaternion.AngleAxis(angle, axis);

        for (int i = 0; i < boardObjects.Length; i++)
        {
            if (boardObjects[i] == null) continue;

            Vector3 newPos = pivotPos + rot * (start[i].position - pivotPos);
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