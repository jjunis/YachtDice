using UnityEngine;

public class Dice : MonoBehaviour
{
    [Header("Rigidbody")]
    [SerializeField] private Rigidbody rb;

    [Header("Roll Settings")]
    [SerializeField] private float upwardForce = 4f;
    [SerializeField] private float forwardForce = 2f;
    [SerializeField] private float torqueForce = 8f;

    [Header("Stop Detection")]
    [SerializeField] private float linearStopThreshold = 0.15f;
    [SerializeField] private float angularStopThreshold = 0.15f;
    [SerializeField] private float requiredStopTime = 0.3f;

    [Header("Start Position")]
    [SerializeField] private Transform startPoint;

    [Header("Hold Indicator")]
    [SerializeField] private GameObject holdIndicator;

    [Header("Hold Visual (루트의 자식 오브젝트여야 함)")]
    [SerializeField] private GameObject normalDice;
    [SerializeField] private GameObject heldDice;

    private bool isRolling = false;
    private bool isHeld = false;

    private float stopTimer = 0f;
    private int currentResult = 0;

    private void Awake()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        // 시작 시 잠금 상태 비주얼 초기화
        UpdateHoldVisual();
    }

    private void Update()
    {
        if (!isRolling)
            return;

        CheckDiceStopped();
    }

    // ==========================================
    // 주사위 굴리기
    // ==========================================

    public void RollDice()
    {
        if (isHeld || isRolling)
        {
            return;
        }

        isRolling = true;
        stopTimer = 0f;
        currentResult = 0;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if (startPoint != null)
        {
            rb.position = startPoint.position;
        }

        rb.rotation = Random.rotationUniform;
        rb.WakeUp();

        // 던지는 방향은 랜덤 회전된 자신이 아니라 시작 지점 기준으로
        Vector3 throwDirection = startPoint != null ? startPoint.forward : Vector3.forward;

        Vector3 force = Vector3.up * upwardForce + throwDirection * forwardForce;
        rb.AddForce(force, ForceMode.Impulse);

        Vector3 randomTorque = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f)
        ) * torqueForce;

        rb.AddTorque(randomTorque, ForceMode.Impulse);

        Debug.Log($"{gameObject.name} 🎲 주사위 굴림");
    }

    // ==========================================
    // 정지 확인
    // ==========================================

    private void CheckDiceStopped()
    {
        float linearSpeed = rb.linearVelocity.magnitude;
        float angularSpeed = rb.angularVelocity.magnitude;

        bool almostStopped =
            linearSpeed <= linearStopThreshold &&
            angularSpeed <= angularStopThreshold;

        if (almostStopped)
        {
            stopTimer += Time.deltaTime;

            if (stopTimer >= requiredStopTime)
            {
                FinishRoll();
            }
        }
        else
        {
            stopTimer = 0f;
        }
    }

    private void FinishRoll()
    {
        isRolling = false;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        currentResult = GetTopFace();

        Debug.Log($"{gameObject.name} 🎲 결과 = {currentResult}");
    }

    // ==========================================
    // 윗면 숫자 판정
    // ==========================================

    private int GetTopFace()
    {
        Vector3[] directions =
        {
            transform.up,
            -transform.up,
            transform.forward,
            -transform.forward,
            transform.right,
            -transform.right
        };

        int[] values = { 2, 5, 1, 6, 4, 3 };

        float highestDot = -Mathf.Infinity;
        int result = 1;

        for (int i = 0; i < directions.Length; i++)
        {
            float dot = Vector3.Dot(directions[i], Vector3.up);

            if (dot > highestDot)
            {
                highestDot = dot;
                result = values[i];
            }
        }

        return result;
    }

    // ==========================================
    // 잠금
    // ==========================================

    public void ToggleHold()
    {
        if (isRolling)
        {
            return;
        }

        // 아직 한 번도 안 굴린 주사위는 잠그지 않음 (야추 규칙상 필요하면 사용)
        // if (currentResult == 0) return;

        SetHeld(!isHeld);

        Debug.Log($"{gameObject.name} : " + (isHeld ? "🔒 잠금" : "🔓 잠금 해제"));
    }

    public void SetHeld(bool value)
    {
        isHeld = value;
        UpdateHoldVisual();
    }

    public bool IsHeld()
    {
        return isHeld;
    }

    // ==========================================
    // 잠금 시각 효과 (모델 교체)
    // ==========================================

    private void UpdateHoldVisual()
    {
        if (normalDice != null)
        {
            normalDice.SetActive(!isHeld);
        }

        if (heldDice != null)
        {
            heldDice.SetActive(isHeld);
        }

        if (holdIndicator != null)
        {
            holdIndicator.SetActive(isHeld);
        }
    }

    // ==========================================
    // 결과
    // ==========================================

    public int GetResult()
    {
        return currentResult;
    }

    public bool IsRolling()
    {
        return isRolling;
    }
}