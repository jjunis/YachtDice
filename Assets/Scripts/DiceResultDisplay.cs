using TMPro;
using UnityEngine;

public class DiceResultDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Dice dice;
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private Camera targetCamera;

    [Header("Position")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.2f, 0f);

    private void Start()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (resultText != null)
        {
            resultText.text = "";
        }
    }

    private void LateUpdate()
    {
        if (dice == null || resultText == null)
            return;

        // 주사위 위에 표시
        transform.position = dice.transform.position + offset;

        // 카메라를 바라보게 설정
        if (targetCamera != null)
        {
            transform.rotation = Quaternion.LookRotation(
                transform.position - targetCamera.transform.position
            );

            transform.Rotate(0f, 180f, 0f);
        }

        // 굴리는 중에는 숫자 숨김
        if (dice.IsRolling())
        {
            resultText.text = "";
            return;
        }

        // 주사위 결과 표시
        int result = dice.GetResult();

        if (result > 0)
        {
            resultText.text = result.ToString();
        }
    }
}