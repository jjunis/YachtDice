using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;

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
    [SerializeField] private SlamEffect slamEffect;  // 주먹으로 내려치는 연출. 없으면 바로 굴림
    [SerializeField] private TableFlipEffect tableFlip; // YEET 성공 시 판 엎기 연출. 없으면 바로 재시작
    [SerializeField] private CameraZoom cameraZoom;     // 주사위가 멈춘 뒤 확대해서 눈을 확인. 없으면 확대 안 함

    [Header("AI")]
    [SerializeField] private float aiThinkDelay = 1.0f;   // AI 행동 사이 대기 시간
    [Range(0f, 1f)]
    [SerializeField] private float aiMistakeChance = 0.1f; // (난이도 선택 패널을 쓰면 아래 값으로 자동 덮어씀)

    [Header("AI 난이도 선택")]
    [SerializeField] private GameObject difficultyPanel;   // 난이도 선택 패널. 비우면 난이도 선택 없이 위 aiMistakeChance로 진행
    [SerializeField] private Button easyButton;
    [SerializeField] private Button normalButton;
    [SerializeField] private Button hardButton;
    [Range(0f, 1f)] [SerializeField] private float easyMistake = 0.35f;   // 쉬움: 실수 확률
    [SerializeField] private int easyTrials = 15;                         // 쉬움: AI 시뮬레이션 횟수(적을수록 멍청)
    [Range(0f, 1f)] [SerializeField] private float normalMistake = 0.1f;
    [SerializeField] private int normalTrials = 60;
    [Range(0f, 1f)] [SerializeField] private float hardMistake = 0f;      // 어려움: 실수 없음
    [SerializeField] private int hardTrials = 150;
    [SerializeField] private TMP_Text difficultyText;      // 우측 상단에 "현재 난이도 : 쉬움" 표시 (비워도 됨)
    private int aiTrials = 60;
    private bool difficultyChosen;

    [Header("YEET")]
    [SerializeField] private Button[] yeetButtons;               // Y, E, E, T 순서로 4개
    [SerializeField] private TMP_Text yeetTimerText;             // 남은 시간 표시 (선택)
    [SerializeField] private int yeetScoreGap = 30;              // 이 점수 이상 차이 나면 표시
    [SerializeField] private float yeetDuration = 6f;            // 버튼이 떠 있는 시간(초)
    [SerializeField] private bool yeetInOrder = true;            // 체크하면 Y→E→E→T 순서대로 눌러야 함

    [Header("규칙 설명 (게임 시작 전)")]
    [SerializeField] private GameObject rulesPanel;       // 규칙 이미지가 들어 있는 패널. 비우면 규칙 설명 없이 바로 시작
    [SerializeField] private TMP_Text rulesTimerText;     // 남은 시간 표시 (선택)
    [SerializeField] private float rulesDuration = 10f;   // 규칙을 보여주는 시간(초)
    [SerializeField] private bool rulesAllowSkip = true;  // 체크하면 Space / Enter / 마우스 클릭으로 건너뛸 수 있음
    [SerializeField] private Button rulesButton;          // 좌측 하단의 규칙 보기 버튼 (선택). 누르면 규칙 이미지를 다시 보여줌

    [Header("YEET 사운드 (선택)")]
    [SerializeField] private AudioSource yeetAudioSource;   // 비워두면 자동으로 만들어 사용
    [SerializeField] private AudioClip yeetShowClip;        // YEET 버튼이 나타날 때
    [SerializeField] private AudioClip yeetLetterClip;      // 글자(Y/E/E/T) 하나를 올바르게 눌렀을 때
    [SerializeField] private AudioClip yeetSuccessClip;     // 4글자를 모두 눌러 성공했을 때 (음성)
    [Range(0f, 1f)]
    [SerializeField] private float yeetVolume = 1f;

    [Header("YEET 랜덤 위치")]
    [SerializeField] private bool yeetRandomPosition = true;     // 켜면 버튼이 나타날 때마다 랜덤 위치
    [SerializeField] private RectTransform yeetArea;             // 랜덤 위치 범위 (비우면 화면 Canvas 전체)
    [SerializeField] private float yeetEdgeMargin = 60f;         // 영역 가장자리에서 떨어질 거리
    [SerializeField] private float yeetGap = 20f;                // 버튼끼리 최소 간격

    private Turn currentTurn = Turn.Player;
    private int rollCount = 0;
    private bool isBusy = false;     // 굴러가는 중
    private bool gameOver = false;
    private string lastMessage = "";

    private bool rulesViewing = false;   // 규칙 버튼으로 규칙을 보고 있는 중
    private int rulesOpenFrame = -1;

    private Coroutine yeetRoutine;
    private bool[] yeetPressed;
    private int yeetNext;            // 순서 모드에서 다음에 눌러야 할 번호
    private const string YeetWord = "YEET";

    private void Awake()
    {
        if (cam == null) cam = Camera.main;

        // YEET 사운드를 재생할 AudioSource (없으면 자동 생성)
        if (yeetAudioSource == null)
        {
            yeetAudioSource = GetComponent<AudioSource>();
            if (yeetAudioSource == null) yeetAudioSource = gameObject.AddComponent<AudioSource>();
            yeetAudioSource.playOnAwake = false;
        }

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

        // YEET 버튼 연결 (글자는 Y, E, E, T로 자동 설정)
        if (yeetButtons != null)
        {
            string word = YeetWord;

            for (int i = 0; i < yeetButtons.Length; i++)
            {
                if (yeetButtons[i] == null) continue;

                int index = i; // 클로저용 복사
                yeetButtons[i].onClick.AddListener(() => OnYeetLetterClicked(index));

                TMP_Text label = yeetButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null && i < word.Length)
                {
                    label.text = word[i].ToString();
                }
            }
        }

        SetYeetVisible(false);

        if (rulesButton != null)
        {
            rulesButton.onClick.AddListener(OpenRules);
        }

        if (difficultyPanel != null)
        {
            if (easyButton != null) easyButton.onClick.AddListener(() => ChooseDifficulty(easyMistake, easyTrials, "쉬움"));
            if (normalButton != null) normalButton.onClick.AddListener(() => ChooseDifficulty(normalMistake, normalTrials, "보통"));
            if (hardButton != null) hardButton.onClick.AddListener(() => ChooseDifficulty(hardMistake, hardTrials, "어려움"));
        }
        else
        {
            difficultyChosen = true;
        }

        // 난이도 선택 → 규칙 이미지 → 첫 턴
        StartCoroutine(StartWithRules());
    }

    // 규칙 이미지를 rulesDuration초 동안 보여준 뒤 첫 턴을 시작
    private void ChooseDifficulty(float mistake, int trials, string label)
    {
        if (difficultyText != null)
        {
            difficultyText.gameObject.SetActive(true);
            difficultyText.text = $"현재 난이도 : {label}";
        }

        aiMistakeChance = mistake;
        aiTrials = trials;
        difficultyChosen = true;

        if (difficultyPanel != null) difficultyPanel.SetActive(false);
    }

    private IEnumerator StartWithRules(bool showRules = true)
    {
        if (!difficultyChosen)
        {
            isBusy = true;   // 선택하는 동안 입력 막기
            difficultyPanel.SetActive(true);
            if (difficultyText != null) difficultyText.gameObject.SetActive(false);

            while (!difficultyChosen) yield return null;
        }

        if (showRules && rulesPanel != null && rulesDuration > 0f)
        {
            isBusy = true;   // 규칙을 보는 동안 Space / 주사위 클릭 입력 막기
            rulesPanel.SetActive(true);

            float remain = rulesDuration;

            while (remain > 0f)
            {
                if (rulesTimerText != null)
                {
                    string skipHint = rulesAllowSkip ? "  (Space / 클릭으로 건너뛰기)" : "";
                    rulesTimerText.text = $"{Mathf.CeilToInt(remain)}초 후 게임이 시작됩니다{skipHint}";
                }

                if (rulesAllowSkip && SkipPressed())
                {
                    break;
                }

                remain -= Time.deltaTime;
                yield return null;
            }

            rulesPanel.SetActive(false);
        }

        StartPlayerTurn();   // isBusy도 여기서 false로 돌아감
    }

    // 좌측 하단 규칙 버튼을 눌렀을 때: 규칙 이미지를 다시 보여줌
    private void OpenRules()
    {
        if (rulesPanel == null || rulesViewing) return;

        rulesViewing = true;
        rulesOpenFrame = Time.frameCount;
        rulesPanel.SetActive(true);

        if (rulesTimerText != null)
        {
            rulesTimerText.text = "Space / Esc / 클릭으로 닫기";
        }
    }

    private void CloseRules()
    {
        rulesViewing = false;

        if (rulesPanel != null)
        {
            rulesPanel.SetActive(false);
        }
    }

    private bool SkipPressed()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        bool key = keyboard != null &&
                   (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
        bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;

        return key || click;
    }

    private void OnDestroy()
    {
        if (rulesButton != null)
        {
            rulesButton.onClick.RemoveListener(OpenRules);
        }

        if (playerBoard != null)
        {
            playerBoard.OnCategoryChosen -= HandlePlayerChosen;
        }

        if (yeetButtons != null)
        {
            foreach (Button b in yeetButtons)
            {
                if (b != null) b.onClick.RemoveAllListeners();
            }
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        // 카메라가 줌 중일 때는 난이도 글자를 숨기고, 줌이 풀리면 다시 표시
        if (difficultyText != null && difficultyChosen)
        {
            bool show = cameraZoom == null || !cameraZoom.IsZoomed;
            if (difficultyText.gameObject.activeSelf != show) difficultyText.gameObject.SetActive(show);
        }

        // 규칙을 보는 동안에는 게임 입력을 막고, Space / Esc / Enter / 클릭으로 닫음
        if (rulesViewing)
        {
            bool closeKey = keyboard != null &&
                (keyboard.spaceKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame);
            bool closeClick = mouse != null && mouse.leftButton.wasPressedThisFrame;

            if ((closeKey || closeClick) && Time.frameCount > rulesOpenFrame)
            {
                CloseRules();
            }

            return;
        }

        if (gameOver)
        {
            if (keyboard != null)
            {
                if (keyboard.spaceKey.wasPressedThisFrame) RestartGame(false);       // 같은 난이도로 다시 시작
                else if (keyboard.rKey.wasPressedThisFrame) RestartGame(true);       // 난이도 선택 화면으로
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

        CheckYeet();
    }

    private void TryRoll()
    {
        if (rollCount >= maxRolls) return;

        HideYeet();
        StartCoroutine(RollRoutine());
    }

    private void TryToggleHold()
    {
        if (rollCount == 0 || rollCount >= maxRolls) return;

        // UI(점수판/YEET 버튼)를 클릭한 경우에는 주사위 잠금 처리 안 함
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
        HideYeet();
        if (cameraZoom != null) cameraZoom.ZoomOutNow();

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

        // 확대돼 있던 카메라를 먼저 원래대로 (주먹 연출 / 카메라 흔들림과 겹치지 않게)
        if (cameraZoom != null)
        {
            yield return cameraZoom.ZoomOut();
        }

        if (slamEffect != null)
        {
            // 내려치는 순간에 DoRoll이 호출됨
            yield return slamEffect.Play(DoRoll);
        }
        else
        {
            DoRoll();
        }

        // 주사위가 전부 멈출 때까지 대기
        yield return new WaitUntil(() => !AnyRolling());
        yield return new WaitForSeconds(0.2f);

        // 주사위가 멈췄으니 확대해서 눈을 보여줌 (기다리지 않고 바로 다음으로 진행)
        if (cameraZoom != null)
        {
            cameraZoom.ZoomIn(dice);
        }

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
            bool[] hold = YachtAI.ChooseHolds(values, used, aiMistakeChance, aiTrials);

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
    // YEET (점수 차이가 크면 글자 버튼 4개로 재시작 기회)
    // ==========================================

    private void CheckYeet()
    {
        if (yeetButtons == null || yeetButtons.Length == 0) return;

        int diff = aiBoard.GetTotal() - playerBoard.GetTotal(); // +면 AI가 앞섬

        // AI가 플레이어보다 yeetScoreGap점 이상 앞설 때만 표시 (내가 이기고 있으면 표시 안 함)
        bool trigger = diff >= yeetScoreGap;

        if (trigger)
        {
            ShowYeet();
        }
        else
        {
            HideYeet();
        }
    }

    private void PlayYeetSound(AudioClip clip)
    {
        if (clip == null || yeetAudioSource == null) return;
        yeetAudioSource.PlayOneShot(clip, yeetVolume);
    }

    private void ShowYeet()
    {
        HideYeet();
        PlayYeetSound(yeetShowClip);
        yeetRoutine = StartCoroutine(YeetRoutine());
    }

    private void HideYeet()
    {
        if (yeetRoutine != null)
        {
            StopCoroutine(yeetRoutine);
            yeetRoutine = null;
        }

        SetYeetVisible(false);
    }

    private void SetYeetVisible(bool visible)
    {
        if (yeetButtons != null)
        {
            foreach (Button b in yeetButtons)
            {
                if (b != null) b.gameObject.SetActive(visible);
            }
        }

        if (yeetTimerText != null)
        {
            yeetTimerText.gameObject.SetActive(visible);
        }
    }

    // 버튼 4개를 영역 안의 랜덤 위치에 서로 겹치지 않게 배치
    private void RandomizeYeetPositions()
    {
        if (!yeetRandomPosition || yeetButtons == null || yeetButtons.Length == 0) return;

        // 영역 : 직접 지정하지 않으면 버튼이 속한 화면 Canvas 전체
        RectTransform area = yeetArea;
        if (area == null && yeetButtons[0] != null)
        {
            Canvas canvas = yeetButtons[0].GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                area = canvas.rootCanvas.transform as RectTransform;
            }
        }
        if (area == null) return;

        Rect bounds = area.rect;

        // 위치 기준을 영역 중앙으로 통일
        List<RectTransform> rts = new List<RectTransform>();

        foreach (Button b in yeetButtons)
        {
            if (b == null) continue;

            RectTransform rt = (RectTransform)b.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            // 부모에 Layout Group이 있어도 위치를 덮어쓰지 않도록 설정
            LayoutElement layoutElement = b.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = b.gameObject.AddComponent<LayoutElement>();
            }
            layoutElement.ignoreLayout = true;

            rts.Add(rt);
        }

        if (rts.Count == 0) return;

        Vector2[] result = new Vector2[rts.Count];

        // 1) 랜덤 배치를 먼저 시도하고, 2) 실패하면 격자 칸에 나눠서 반드시 겹치지 않게 배치
        if (!TryPlaceRandomly(rts, bounds, result))
        {
            PlaceOnGrid(rts, bounds, result);
        }

        for (int i = 0; i < rts.Count; i++)
        {
            // 영역 중앙 기준 좌표를 월드 좌표로 바꿔 배치 (부모가 어디에 있든 영역 안에 놓임)
            rts[i].position = area.TransformPoint(area.rect.center + result[i]);
        }
    }

    // 랜덤 위치를 뽑아서 겹치지 않으면 성공. 전체 배치를 최대 30번까지 다시 시도
    private bool TryPlaceRandomly(List<RectTransform> rts, Rect bounds, Vector2[] result)
    {
        for (int layout = 0; layout < 30; layout++)
        {
            List<Rect> placed = new List<Rect>();
            bool allPlaced = true;

            for (int i = 0; i < rts.Count; i++)
            {
                Vector2 size = rts[i].rect.size;

                // 영역 중앙을 (0, 0)으로 봤을 때 버튼 중심이 놓일 수 있는 범위
                float maxX = Mathf.Max(0f, bounds.width * 0.5f - yeetEdgeMargin - size.x * 0.5f);
                float maxY = Mathf.Max(0f, bounds.height * 0.5f - yeetEdgeMargin - size.y * 0.5f);

                bool found = false;

                for (int attempt = 0; attempt < 40; attempt++)
                {
                    Vector2 pos = new Vector2(Random.Range(-maxX, maxX), Random.Range(-maxY, maxY));
                    Rect rect = MakeYeetRect(pos, size);

                    if (OverlapsAny(placed, rect)) continue;

                    placed.Add(rect);
                    result[i] = pos;
                    found = true;
                    break;
                }

                if (!found)
                {
                    allPlaced = false;
                    break;
                }
            }

            if (allPlaced) return true;
        }

        return false;
    }

    // 영역을 칸으로 나누고, 섞은 칸에 버튼을 하나씩 넣음 (칸이 겹치지 않으니 버튼도 겹치지 않음)
    private void PlaceOnGrid(List<RectTransform> rts, Rect bounds, Vector2[] result)
    {
        // 가장 큰 버튼 기준으로 칸 크기 결정
        Vector2 cell = Vector2.zero;
        foreach (RectTransform rt in rts)
        {
            cell = Vector2.Max(cell, rt.rect.size);
        }
        cell += Vector2.one * yeetGap;

        float usableW = Mathf.Max(0f, bounds.width - yeetEdgeMargin * 2f);
        float usableH = Mathf.Max(0f, bounds.height - yeetEdgeMargin * 2f);

        int cols = Mathf.Max(1, Mathf.FloorToInt((usableW + yeetGap) / cell.x));
        int rows = Mathf.Max(1, Mathf.FloorToInt((usableH + yeetGap) / cell.y));

        if (cols * rows < rts.Count)
        {
            Debug.LogWarning("DiceManager : YEET 영역이 너무 작아 일부 버튼이 영역 밖에 놓일 수 있습니다. YeetGroup을 화면 크기로 늘려 주세요.");
            rows = Mathf.CeilToInt((float)rts.Count / cols);
        }

        // 칸 번호를 섞기
        List<int> slots = new List<int>();
        for (int i = 0; i < cols * rows; i++)
        {
            slots.Add(i);
        }

        for (int i = slots.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int temp = slots[i];
            slots[i] = slots[j];
            slots[j] = temp;
        }

        float gridW = cols * cell.x - yeetGap;
        float gridH = rows * cell.y - yeetGap;
        float jitter = yeetGap * 0.4f; // 칸 안에서 살짝 흔들어 격자처럼 안 보이게

        for (int i = 0; i < rts.Count; i++)
        {
            int col = slots[i] % cols;
            int row = slots[i] / cols;

            float x = -gridW * 0.5f + col * cell.x + (cell.x - yeetGap) * 0.5f;
            float y = gridH * 0.5f - row * cell.y - (cell.y - yeetGap) * 0.5f;

            x += Random.Range(-jitter, jitter);
            y += Random.Range(-jitter, jitter);

            result[i] = new Vector2(x, y);
        }
    }

    // 버튼 주위에 간격(yeetGap)의 절반만큼 여유를 더한 영역
    private Rect MakeYeetRect(Vector2 pos, Vector2 size)
    {
        return new Rect(
            pos - size * 0.5f - Vector2.one * (yeetGap * 0.5f),
            size + Vector2.one * yeetGap);
    }

    private static bool OverlapsAny(List<Rect> placed, Rect rect)
    {
        foreach (Rect r in placed)
        {
            if (r.Overlaps(rect)) return true;
        }
        return false;
    }

    private void ResetYeetProgress()
    {
        yeetNext = 0;
        yeetPressed = new bool[yeetButtons.Length];

        foreach (Button b in yeetButtons)
        {
            if (b != null) b.interactable = true;
        }
    }

    private IEnumerator YeetRoutine()
    {
        SetYeetVisible(true);
        RandomizeYeetPositions();
        ResetYeetProgress();

        float remain = yeetDuration;

        while (remain > 0f)
        {
            if (yeetTimerText != null)
            {
                yeetTimerText.text = Mathf.CeilToInt(remain).ToString();
            }

            remain -= Time.deltaTime;
            yield return null;
        }

        HideYeet();
    }

    // 버튼 번호에 해당하는 글자 (Y, E, E, T)
    private char GetYeetLetter(int index)
    {
        return (index >= 0 && index < YeetWord.Length) ? YeetWord[index] : '?';
    }

    private void OnYeetLetterClicked(int index)
    {
        if (yeetPressed == null || index >= yeetPressed.Length || yeetPressed[index]) return;

        // 순서가 틀리면 YEET 버튼이 사라지고 기회가 끝남
        // 번호가 아니라 글자로 비교하므로 두 E 버튼은 어느 쪽을 먼저 눌러도 됨
        if (yeetInOrder && GetYeetLetter(index) != GetYeetLetter(yeetNext))
        {
            HideYeet();
            return;
        }

        yeetPressed[index] = true;
        yeetButtons[index].interactable = false; // 눌린 글자는 회색
        PlayYeetSound(yeetLetterClip);
        yeetNext++;

        // 아직 안 눌린 버튼이 있으면 계속
        foreach (bool pressed in yeetPressed)
        {
            if (!pressed) return;
        }

        // 4개 모두 눌렀으면 판을 엎고 재시작
        HideYeet();
        StartCoroutine(YeetSuccessRoutine());
    }

    private IEnumerator YeetSuccessRoutine()
    {
        PlayYeetSound(yeetSuccessClip); // YEET 성공 음성
        if (cameraZoom != null) yield return cameraZoom.ZoomOut();
        isBusy = true;                 // 연출 중 Space / 주사위 클릭 막기
        playerBoard.LockSelection();   // 연출 중 점수판 클릭 막기
        SetInfo("YEET!!!");

        if (tableFlip != null)
        {
            yield return tableFlip.Play();
        }

        // 바로 재시작하지 않고 R 키를 기다림 (R → 난이도 선택 화면)
        HideYeet();
        gameOver = true;
        isBusy = false;
        SetInfo("AI 게임 더럽게 하네 \nSpace : 같은 난이도로 다시 시작\nR : 난이도 선택");
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
            "Space : 같은 난이도로 다시 시작\nR : 난이도 선택");

        return true;
    }

    private void RestartGame(bool chooseDifficulty = true)
    {
        HideYeet();

        gameOver = false;
        lastMessage = "";

        playerBoard.ResetBoard();
        aiBoard.ResetBoard();

        if (chooseDifficulty && difficultyPanel != null)
        {
            // 난이도 선택 화면으로 돌아감 (규칙 화면은 다시 보여주지 않음)
            difficultyChosen = false;
            isBusy = true;
            SetInfo("");
            StartCoroutine(StartWithRules(false));
        }
        else
        {
            StartPlayerTurn();
        }
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