using System;
using System.Collections;
using UnityEngine;

// 주먹(또는 손, 컵 등) 모델이 위에서 나타나 책상을 내려치는 연출.
// 내려치는 순간(onImpact)에 주사위를 굴리도록 DiceManager가 연결해서 사용
public class SlamEffect : MonoBehaviour
{
    [Header("Model")]
    [SerializeField] private GameObject slamModel;   // 주먹 모델 (Collider / Rigidbody 없어야 함)
    [SerializeField] private Transform hitPoint;     // 책상 위 내려치는 지점

    [Header("Pose (주먹 방향)")]
    [SerializeField] private Vector3 hitEuler = Vector3.zero;                         // 책상에 닿는 순간의 회전
    [SerializeField] private Vector3 windUpEulerOffset = new Vector3(-35f, 0f, 0f);   // 힘 모을 때 더 젖히는 각도

    [Header("Timing")]
    [SerializeField] private float startHeight = 4f;       // 처음 나타나는 높이
    [SerializeField] private float windUpHeight = 0.6f;    // 내려치기 전에 더 올라가는 높이
    [SerializeField] private float appearDuration = 0.25f; // 나타나서 자리 잡는 시간
    [SerializeField] private float windUpDuration = 0.2f;  // 힘 모으는 시간
    [SerializeField] private float slamDuration = 0.1f;    // 내려치는 시간 (짧을수록 강한 느낌)
    [SerializeField] private float holdDuration = 0.15f;   // 내려친 뒤 멈춰 있는 시간
    [SerializeField] private float leaveDuration = 0.3f;   // 위로 사라지는 시간

    [Header("Camera Shake")]
    [SerializeField] private Camera cam;
    [SerializeField] private float shakePower = 0.15f;
    [SerializeField] private float shakeDuration = 0.25f;

    [Header("Sound (선택)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip slamClip;

    private void Awake()
    {
        if (cam == null) cam = Camera.main;

        if (slamModel != null)
        {
            slamModel.SetActive(false);
        }
    }

    // onImpact : 내려치는 순간에 호출 (여기서 주사위를 굴림)
    public IEnumerator Play(Action onImpact)
    {
        if (slamModel == null || hitPoint == null)
        {
            onImpact?.Invoke();
            yield break;
        }

        Vector3 hitPos = hitPoint.position;
        Vector3 highPos = hitPos + Vector3.up * startHeight;
        Vector3 windUpPos = highPos + Vector3.up * windUpHeight;

        Quaternion hitRot = Quaternion.Euler(hitEuler);
        Quaternion windUpRot = Quaternion.Euler(hitEuler + windUpEulerOffset);

        Transform t = slamModel.transform;

        // 1. 등장 (위에서 내려와 자리 잡음)
        t.SetPositionAndRotation(highPos + Vector3.up * 2f, windUpRot);
        slamModel.SetActive(true);

        yield return MoveTo(t, highPos, windUpRot, appearDuration, EaseOut);

        // 2. 힘 모으기 (위로 올라가며 주먹을 뒤로 젖힘)
        yield return MoveTo(t, windUpPos, windUpRot, windUpDuration, EaseOut);

        // 3. 내려치기 (가속하며 주먹이 앞으로 꺾임)
        yield return MoveTo(t, hitPos, hitRot, slamDuration, EaseIn);

        // 4. 충격
        onImpact?.Invoke();
        StartCoroutine(Shake());

        if (audioSource != null && slamClip != null)
        {
            audioSource.PlayOneShot(slamClip);
        }

        yield return new WaitForSeconds(holdDuration);

        // 5. 사라지기
        yield return MoveTo(t, highPos + Vector3.up * 2f, windUpRot, leaveDuration, EaseOut);

        slamModel.SetActive(false);
    }

    private IEnumerator MoveTo(Transform target, Vector3 toPos, Quaternion toRot, float duration, Func<float, float> ease)
    {
        Vector3 fromPos = target.position;
        Quaternion fromRot = target.rotation;
        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;
            float p = ease(Mathf.Clamp01(time / duration));

            target.position = Vector3.LerpUnclamped(fromPos, toPos, p);
            target.rotation = Quaternion.SlerpUnclamped(fromRot, toRot, p);

            yield return null;
        }

        target.SetPositionAndRotation(toPos, toRot);
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

            float fade = 1f - (time / shakeDuration); // 점점 약해짐
            camT.localPosition = original + UnityEngine.Random.insideUnitSphere * shakePower * fade;

            yield return null;
        }

        camT.localPosition = original;
    }

    private static float EaseIn(float t) { return t * t; }
    private static float EaseOut(float t) { return 1f - (1f - t) * (1f - t); }
}