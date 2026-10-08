using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class DiceManager : MonoBehaviour
{
    private enum Turn { Player, AI }

    [Header("Dice")]
    [SerializeField] private Dice[] dice;
    [SerializeField] private Camera cam;
    [SerializeField] private int maxRolls = 3;

    [Header("UI")]
    [SerializeField] private TMP_Text infoText;      // DiceResultText 연결
    [FormerlySerializedAs("scoreBoard")]
    [SerializeField] private ScoreBoard playerBoard; // 내 점수판
    [SerializeField] private ScoreBoard aiBoard;     // AI 점수판

    [Header("UI Layout (오른쪽 위)")]
    [SerializeField] private Vector2 infoMargin = new Vector2(30f, 30f);
    [SerializeField] private Vector2 infoSize = new Vector2(500f, 200f);
    [SerializeField] private float infoFontSize = 28f;

    [Header("Effect (선택)")]
    //[SerializeField] private SlamEffect slamEffect;  // 없으면 연출 없이 바로 굴림

    [Header("AI")]
    [SerializeField] private float aiThinkDelay = 1.0f;   // AI 행동 사이 대기 시간
    [Range(0f, 1f)]
    [SerializeField] private float aiMistakeChance = 0.1f; // 클수록 AI가 실수를 많이 함 (쉬움)

    private Turn currentTurn = Turn.Player;
    private int rollCount = 0;
    private bool isBusy = false;     // 굴림 연출/굴러가는 중
    private bool gameOver = false;
    private string lastMessage = "";

    private void Awake()
    {
        if (cam == null) cam = Camera.main;

        SetupInfoTextLayout();
    }

    private void Start()
    {
        if (playerBoard == null || aiBoard == null)
        {
            Debug.LogError("DiceManager : Player Board / AI Board가 연결되지 않았습니다.");
            return;
        }

        playerBoard.OnCategoryChosen += HandlePlayerChosen;

        StartPlayerTurn();
    }

    private void OnDestroy()
    {
        if (playerBoard != null)
        {
            playerBoard.OnCategoryChosen -= HandlePlayerChosen;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (gameOver)
        {
            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                RestartGame();
            }
            return;
        }

        // AI 차례이거나 굴러가는 중에는 입력 무시
        if (currentTurn != Turn.Player || isBusy) return;

        if (keyboard != null && keyboard.spaceKey.wasPressedThisFrame)
        {
            TryRoll();
        }

        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            TryToggleHold();
        }
    }

    // ==========================================
    // 플레이어 턴
    // ==========================================

    private void StartPlayerTurn()
    {
        currentTurn = Turn.Player;
        rollCount = 0;
        isBusy = false;

        foreach (Dice d in dice)
        {
            d.SetHeld(false);
        }

        playerBoard.LockSelection();
        UpdatePlayerUI();
    }

    private void TryRoll()
    {
        if (rollCount >= maxRolls) return;

        StartCoroutine(RollRoutine());
    }

    private void TryToggleHold()
    {
        if (rollCount == 0 || rollCount >= maxRolls) return;

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
                UpdatePlayerUI();
            }
        }
    }

    // 플레이어가 점수판에서 족보를 골랐을 때
    private void HandlePlayerChosen(Category category, int score)
    {
        lastMessage = $"[{YachtScorer.Names[(int)category]}] {score}점 획득";

        StartCoroutine(AITurnRoutine());
    }

    // ==========================================
    // 굴리기 (플레이어/AI 공통)
    // ==========================================

    private IEnumerator RollRoutine()
    {
        isBusy = true;
        rollCount++;
        lastMessage = "";

        playerBoard.LockSelection();

        string who = currentTurn == Turn.AI ? "AI " : "";
        SetInfo($"{who}{rollCount}번째 굴리는 중...");

        DoRoll();

        // 주사위가 전부 멈출 때까지 대기
        yield return new WaitUntil(() => !AnyRolling());
        yield return new WaitForSeconds(0.2f);

        isBusy = false;

        if (currentTurn == Turn.Player)
        {
            playerBoard.ShowPotential(GetDiceValues());
            UpdatePlayerUI();
        }
    }

    private void DoRoll()
    {
        foreach (Dice d in dice)
        {
            d.RollDice(); // 잠긴 주사위는 내부에서 건너뜀
        }
    }

    // ==========================================
    // AI 턴
    // ==========================================

    private IEnumerator AITurnRoutine()
    {
        currentTurn = Turn.AI;
        rollCount = 0;

        foreach (Dice d in dice)
        {
            d.SetHeld(false);
        }

        playerBoard.LockSelection();

        SetInfo($"{lastMessage}\nAI 차례입니다...");
        yield return new WaitForSeconds(aiThinkDelay * 1.5f);

        bool[] used = aiBoard.GetUsedFlags();
        int[] values = GetDiceValues();

        while (rollCount < maxRolls)
        {
            yield return RollRoutine();

            values = GetDiceValues();

            SetInfo($"AI {rollCount}번째 굴림 결과\n{GetResultString()}");
            yield return new WaitForSeconds(aiThinkDelay);

            if (rollCount >= maxRolls) break;

            // 어떤 주사위를 고정할지 결정
            bool[] hold = YachtAI.ChooseHolds(values, used, aiMistakeChance);

            bool allHeld = true;
            for (int i = 0; i < dice.Length; i++)
            {
                dice[i].SetHeld(hold[i]);
                if (!hold[i]) allHeld = false;
            }

            // 전부 고정 = 이 결과로 만족, 더 안 굴림
            if (allHeld)
            {
                SetInfo($"AI가 이 결과로 결정했습니다\n{GetResultString()}");
                yield return new WaitForSeconds(aiThinkDelay);
                break;
            }

            SetInfo($"AI가 주사위를 고정했습니다\n{GetResultString()}");
            yield return new WaitForSeconds(aiThinkDelay);
        }

        // 족보 선택
        Category choice = YachtAI.ChooseCategory(values, used, aiMistakeChance);
        int score = aiBoard.Commit(choice, values);

        lastMessage = $"AI : [{YachtScorer.Names[(int)choice]}] {score}점 획득";
        SetInfo($"{lastMessage}\n{GetResultString()}");

        yield return new WaitForSeconds(aiThinkDelay * 1.5f);

        if (CheckGameOver()) yield break;

        StartPlayerTurn();
    }

    // ==========================================
    // 게임 종료 / 재시작
    // ==========================================

    private bool CheckGameOver()
    {
        if (!playerBoard.IsFull() || !aiBoard.IsFull()) return false;

        gameOver = true;

        int p = playerBoard.GetTotal();
        int a = aiBoard.GetTotal();
        string result = p > a ? "승리!" : (p < a ? "패배..." : "무승부");

        SetInfo(
            $"{lastMessage}\n" +
            $"게임 종료 - {result}\n" +
            $"나 {p} : AI {a}\n" +
            "R 키로 새 게임");

        return true;
    }

    private void RestartGame()
    {
        gameOver = false;
        lastMessage = "";

        playerBoard.ResetBoard();
        aiBoard.ResetBoard();

        StartPlayerTurn();
    }

    // ==========================================
    // 공통 도우미
    // ==========================================

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

    // ==========================================
    // UI
    // ==========================================

    private void SetInfo(string message)
    {
        if (infoText != null)
        {
            infoText.text = message;
        }
    }

    private void UpdatePlayerUI()
    {
        int remaining = maxRolls - rollCount;
        StringBuilder sb = new StringBuilder();

        if (!string.IsNullOrEmpty(lastMessage))
        {
            sb.AppendLine(lastMessage);
        }

        if (rollCount == 0)
        {
            sb.AppendLine("내 차례! Space를 눌러 굴리세요");
            sb.AppendLine($"남은 횟수: {remaining}");
        }
        else
        {
            sb.AppendLine($"{rollCount}번째 굴림 결과");
            sb.AppendLine(GetResultString());

            if (remaining > 0)
            {
                sb.AppendLine($"남은 횟수: {remaining}");
                sb.AppendLine("주사위를 클릭해 고정하고 Space로 다시 굴리거나,");
                sb.AppendLine("점수판에서 족보를 선택하세요");
            }
            else
            {
                sb.AppendLine("점수판에서 족보를 선택하세요");
            }
        }

        sb.Append($"점수  나 {playerBoard.GetTotal()} : AI {aiBoard.GetTotal()}");

        SetInfo(sb.ToString());
    }

    // 안내 글자를 화면 오른쪽 위에 배치
    private void SetupInfoTextLayout()
    {
        if (infoText == null) return;

        RectTransform rt = infoText.rectTransform;

        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);

        rt.anchoredPosition = new Vector2(-infoMargin.x, -infoMargin.y);
        rt.sizeDelta = infoSize;

        infoText.alignment = TextAlignmentOptions.TopRight;
        infoText.fontSize = infoFontSize;
    }
}