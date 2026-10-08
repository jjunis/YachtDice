using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;

public class DiceManager : MonoBehaviour
{
    [Header("Dice")]
    [SerializeField] private Dice[] dice;
    [SerializeField] private Camera cam;
    [SerializeField] private int maxRolls = 3;

    [Header("UI")]
    [SerializeField] private TMP_Text infoText;      // DiceResultText 연결
    [SerializeField] private ScoreBoard scoreBoard;  // 점수판 연결

    private int rollCount = 0;
    private bool waitingForResult = false;
    private bool gameOver = false;
    private string lastMessage = "";

    private void Awake()
    {
        if (cam == null) cam = Camera.main;
    }

    private void Start()
    {
        if (scoreBoard != null)
        {
            scoreBoard.OnCategoryChosen += HandleCategoryChosen;
        }

        UpdateUI();
    }

    private void OnDestroy()
    {
        if (scoreBoard != null)
        {
            scoreBoard.OnCategoryChosen -= HandleCategoryChosen;
        }
    }

    private void Update()
    {
        if (gameOver)
        {
            // R 키로 새 게임
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                RestartGame();
            }
            return;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            RollAll();
        }

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryToggleHold();
        }

        // 굴림이 끝나는 순간 감지
        if (waitingForResult && !AnyRolling())
        {
            waitingForResult = false;

            if (scoreBoard != null)
            {
                scoreBoard.ShowPotential(GetDiceValues());
            }

            UpdateUI();
        }
    }

    // ==========================================
    // 굴리기 / 잠금
    // ==========================================

    private void RollAll()
    {
        if (rollCount >= maxRolls) return;
        if (AnyRolling()) return;

        // 점수를 고르지 않으면 새 턴으로 못 넘어감 (rollCount가 리셋되지 않으므로 위에서 막힘)

        foreach (Dice d in dice)
        {
            d.RollDice(); // 잠긴 주사위는 내부에서 자동으로 건너뜀
        }

        rollCount++;
        waitingForResult = true;
        lastMessage = "";

        if (scoreBoard != null)
        {
            scoreBoard.LockSelection();
        }

        UpdateUI();
    }

    private void TryToggleHold()
    {
        if (rollCount == 0 || AnyRolling()) return;
        if (rollCount >= maxRolls) return;

        // UI(점수판 버튼)를 클릭한 경우에는 주사위 잠금 처리 안 함
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Ray ray = cam.ScreenPointToRay(Mouse.current.position.ReadValue());

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Dice d = hit.collider.GetComponentInParent<Dice>();
            if (d != null)
            {
                d.ToggleHold();
                UpdateUI();
            }
        }
    }

    private bool AnyRolling()
    {
        foreach (Dice d in dice)
        {
            if (d.IsRolling()) return true;
        }
        return false;
    }

    private int[] GetDiceValues()
    {
        int[] values = new int[dice.Length];

        for (int i = 0; i < dice.Length; i++)
        {
            values[i] = dice[i].GetResult();
        }

        return values;
    }

    // ==========================================
    // 점수 선택 후 처리
    // ==========================================

    private void HandleCategoryChosen(Category category, int score)
    {
        lastMessage = $"[{YachtScorer.Names[(int)category]}] {score}점 획득";

        if (scoreBoard != null && scoreBoard.IsFull())
        {
            gameOver = true;
            infoText.text =
                $"{lastMessage}\n게임 종료!\n최종 점수: {scoreBoard.GetTotal()}\nR 키로 새 게임";
            return;
        }

        ResetTurn();
    }

    // 새 턴 시작
    public void ResetTurn()
    {
        rollCount = 0;
        waitingForResult = false;

        foreach (Dice d in dice)
        {
            d.SetHeld(false);
        }

        if (scoreBoard != null)
        {
            scoreBoard.LockSelection();
        }

        UpdateUI();
    }

    private void RestartGame()
    {
        gameOver = false;
        lastMessage = "";

        if (scoreBoard != null)
        {
            scoreBoard.ResetBoard();
        }

        ResetTurn();
    }

    // ==========================================
    // UI
    // ==========================================

    private void UpdateUI()
    {
        if (infoText == null) return;

        int remaining = maxRolls - rollCount;

        if (rollCount == 0)
        {
            string head = string.IsNullOrEmpty(lastMessage) ? "" : lastMessage + "\n";
            infoText.text = head + $"Space를 눌러 주사위를 굴리세요\n남은 횟수: {remaining}";
            return;
        }

        if (AnyRolling())
        {
            infoText.text = $"{rollCount}번째 굴리는 중...";
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine($"{rollCount}번째 굴림 결과");
        sb.AppendLine(GetResultString());

        if (remaining > 0)
        {
            sb.AppendLine($"남은 횟수: {remaining}");
            sb.Append("주사위를 클릭해 고정하고 Space로 다시 굴리거나,\n오른쪽 점수판에서 족보를 선택하세요");
        }
        else
        {
            sb.Append("점수판에서 족보를 선택하세요");
        }

        infoText.text = sb.ToString();
    }

    private string GetResultString()
    {
        StringBuilder sb = new StringBuilder();

        foreach (Dice d in dice)
        {
            sb.Append(d.GetResult());
            if (d.IsHeld()) sb.Append("(고정)");
            sb.Append("  ");
        }

        return sb.ToString();
    }
}