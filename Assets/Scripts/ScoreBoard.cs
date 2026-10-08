using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreBoard : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Transform rowParent;   // VerticalLayoutGroup이 붙은 패널
    [SerializeField] private Button rowPrefab;      // Button (TMP) 프리팹
    [SerializeField] private TMP_Text totalText;    // 총점 표시

    [Header("Bonus")]
    [SerializeField] private int bonusThreshold = 63; // 상단(1~6) 합계 기준
    [SerializeField] private int bonusScore = 35;

    // (선택한 족보, 획득 점수)
    public event Action<Category, int> OnCategoryChosen;

    private int categoryCount;
    private Button[] buttons;
    private TMP_Text[] labels;
    private int[] scores;
    private bool[] used;

    private int[] currentDice;   // 현재 굴림 결과 (null이면 선택 불가)
    private bool canSelect;

    private void Awake()
    {
        categoryCount = YachtScorer.Names.Length;

        buttons = new Button[categoryCount];
        labels = new TMP_Text[categoryCount];
        scores = new int[categoryCount];
        used = new bool[categoryCount];

        for (int i = 0; i < categoryCount; i++)
        {
            Button b = Instantiate(rowPrefab, rowParent);
            b.gameObject.name = "Row_" + YachtScorer.Names[i];
            b.gameObject.SetActive(true);

            int index = i; // 클로저용 복사
            b.onClick.AddListener(() => Select(index));

            buttons[i] = b;
            labels[i] = b.GetComponentInChildren<TMP_Text>();
        }

        // 프리팹 원본이 씬 오브젝트라면 숨김
        if (rowPrefab.gameObject.scene.IsValid())
        {
            rowPrefab.gameObject.SetActive(false);
        }

        RefreshAll();
        UpdateTotal();
    }

    // ==========================================
    // 외부에서 호출
    // ==========================================

    // 굴림이 끝났을 때: 족보별 예상 점수를 보여주고 선택 가능하게 함
    public void ShowPotential(int[] dice)
    {
        currentDice = dice;
        canSelect = true;
        RefreshAll();
    }

    // 굴리는 중이거나 아직 안 굴렸을 때: 선택 불가
    public void LockSelection()
    {
        currentDice = null;
        canSelect = false;
        RefreshAll();
    }

    public bool IsFull()
    {
        for (int i = 0; i < categoryCount; i++)
        {
            if (!used[i]) return false;
        }
        return true;
    }

    public int GetTotal()
    {
        int total = 0;
        for (int i = 0; i < categoryCount; i++)
        {
            if (used[i]) total += scores[i];
        }
        return total + GetBonus();
    }

    public void ResetBoard()
    {
        for (int i = 0; i < categoryCount; i++)
        {
            used[i] = false;
            scores[i] = 0;
        }

        currentDice = null;
        canSelect = false;

        RefreshAll();
        UpdateTotal();
    }

    // ==========================================
    // 내부
    // ==========================================

    private void Select(int index)
    {
        if (!canSelect || currentDice == null || used[index]) return;

        Category category = (Category)index;
        int score = YachtScorer.Calculate(currentDice, category);

        scores[index] = score;
        used[index] = true;

        currentDice = null;
        canSelect = false;

        RefreshAll();
        UpdateTotal();

        OnCategoryChosen?.Invoke(category, score);
    }

    private void RefreshAll()
    {
        for (int i = 0; i < categoryCount; i++)
        {
            string name = YachtScorer.Names[i];

            if (used[i])
            {
                labels[i].text = $"{name}   {scores[i]}";
                buttons[i].interactable = false;
            }
            else if (canSelect && currentDice != null)
            {
                int potential = YachtScorer.Calculate(currentDice, (Category)i);
                labels[i].text = $"{name}   ({potential})";
                buttons[i].interactable = true;
            }
            else
            {
                labels[i].text = $"{name}   -";
                buttons[i].interactable = false;
            }
        }
    }

    private int GetUpperSum()
    {
        int sum = 0;
        for (int i = (int)Category.Aces; i <= (int)Category.Sixes; i++)
        {
            if (used[i]) sum += scores[i];
        }
        return sum;
    }

    private int GetBonus()
    {
        return GetUpperSum() >= bonusThreshold ? bonusScore : 0;
    }

    private void UpdateTotal()
    {
        if (totalText == null) return;

        totalText.text =
            $"상단 합계 {GetUpperSum()} / {bonusThreshold}  (보너스 {GetBonus()})\n" +
            $"총점 {GetTotal()}";
    }
}