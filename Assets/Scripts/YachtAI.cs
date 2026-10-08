using System.Collections.Generic;
using UnityEngine;

// 야추 AI : 시뮬레이션으로 "어떤 주사위를 고정할지", 점수 기준으로 "어떤 족보를 고를지" 결정
public static class YachtAI
{
    // 족보별 "기대 점수" (이 점수보다 높게 나오면 이득, 낮으면 손해로 계산)
    // 순서는 Category enum과 같음
    // Aces, Twos, Threes, Fours, Fives, Sixes, Choice, FourOfAKind, FullHouse, SmallStraight, LargeStraight, Yacht
    private static readonly float[] Par =
    {
        2.5f, 5f, 7.5f, 10f, 12.5f, 15f,
        22f, 12f, 17f, 10f, 15f, 8f
    };

    // ==========================================
    // 어떤 주사위를 고정할지
    // ==========================================
    // 반환: hold[i] == true 이면 i번째 주사위를 고정(다시 안 굴림)
    public static bool[] ChooseHolds(int[] dice, bool[] used, float mistakeChance = 0f, int trials = 80)
    {
        int n = dice.Length;
        bool[] result = new bool[n];

        // 일부러 실수 (난이도 조절)
        if (Random.value < mistakeChance)
        {
            for (int i = 0; i < n; i++)
            {
                result[i] = Random.value < 0.5f;
            }
            return result;
        }

        int[] temp = new int[n];
        float bestAvg = float.NegativeInfinity;
        int bestMask = 0;
        int allHeldMask = (1 << n) - 1;

        // 고정하는 모든 경우(2^5 = 32가지)를 시험
        for (int mask = 0; mask <= allHeldMask; mask++)
        {
            int runs = (mask == allHeldMask) ? 1 : trials; // 전부 고정이면 결과가 하나뿐
            float total = 0f;

            for (int t = 0; t < runs; t++)
            {
                for (int i = 0; i < n; i++)
                {
                    bool held = ((mask >> i) & 1) == 1;
                    temp[i] = held ? dice[i] : Random.Range(1, 7);
                }

                total += BestValue(temp, used);
            }

            float avg = total / runs;

            if (avg > bestAvg)
            {
                bestAvg = avg;
                bestMask = mask;
            }
        }

        for (int i = 0; i < n; i++)
        {
            result[i] = ((bestMask >> i) & 1) == 1;
        }

        return result;
    }

    // ==========================================
    // 어떤 족보를 고를지
    // ==========================================
    public static Category ChooseCategory(int[] dice, bool[] used, float mistakeChance = 0f)
    {
        List<int> open = new List<int>();

        for (int i = 0; i < used.Length; i++)
        {
            if (!used[i]) open.Add(i);
        }

        // 일부러 실수
        if (Random.value < mistakeChance)
        {
            return (Category)open[Random.Range(0, open.Count)];
        }

        float best = float.NegativeInfinity;
        int bestIndex = open[0];

        foreach (int i in open)
        {
            float v = Value(dice, i);

            if (v > best)
            {
                best = v;
                bestIndex = i;
            }
        }

        return (Category)bestIndex;
    }

    // ==========================================
    // 평가
    // ==========================================

    // 열려 있는 족보 중 가장 가치가 높은 것의 값
    private static float BestValue(int[] dice, bool[] used)
    {
        float best = float.NegativeInfinity;

        for (int i = 0; i < used.Length; i++)
        {
            if (used[i]) continue;

            float v = Value(dice, i);
            if (v > best) best = v;
        }

        return best;
    }

    // 점수 - 기대 점수. 0점으로 버리는 경우 기대 점수가 낮은 족보(예: 1의 합)부터 버리게 됨
    private static float Value(int[] dice, int categoryIndex)
    {
        int score = YachtScorer.Calculate(dice, (Category)categoryIndex);
        float value = score - Par[categoryIndex];

        // 상단(1~6) 족보에서 같은 눈 3개 이상이면 보너스(63점) 쪽에 유리하므로 가산점
        if (categoryIndex <= (int)Category.Sixes)
        {
            int face = categoryIndex + 1;
            if (score >= face * 3) value += 2f;
        }

        return value;
    }
}