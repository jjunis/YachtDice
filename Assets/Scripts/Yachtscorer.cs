using UnityEngine;

public enum Category
{
    Aces,           // 1의 합
    Twos,           // 2의 합
    Threes,         // 3의 합
    Fours,          // 4의 합
    Fives,          // 5의 합
    Sixes,          // 6의 합
    Choice,         // 주사위 5개 합
    FourOfAKind,    // 같은 눈 4개 이상 -> 주사위 5개 합
    FullHouse,      // 3개 + 2개 -> 주사위 5개 합
    SmallStraight,  // 연속 4개 -> 15점
    LargeStraight,  // 연속 5개 -> 30점
    Yacht           // 5개 모두 같음 -> 50점
}

public static class YachtScorer
{
    public static readonly string[] Names =
    {
        "1의 합",
        "2의 합",
        "3의 합",
        "4의 합",
        "5의 합",
        "6의 합",
        "초이스",
        "포카드",
        "풀하우스",
        "스몰 스트레이트",
        "라지 스트레이트",
        "야추"
    };

    public static int Calculate(int[] dice, Category category)
    {
        // counts[눈] = 해당 눈이 나온 개수
        int[] counts = new int[7];
        int sum = 0;

        foreach (int v in dice)
        {
            if (v < 1 || v > 6) continue; // 아직 결과가 0인 주사위 방어
            counts[v]++;
            sum += v;
        }

        switch (category)
        {
            case Category.Aces: return counts[1] * 1;
            case Category.Twos: return counts[2] * 2;
            case Category.Threes: return counts[3] * 3;
            case Category.Fours: return counts[4] * 4;
            case Category.Fives: return counts[5] * 5;
            case Category.Sixes: return counts[6] * 6;

            case Category.Choice:
                return sum;

            case Category.FourOfAKind:
                for (int i = 1; i <= 6; i++)
                {
                    if (counts[i] >= 4) return sum;
                }
                return 0;

            case Category.FullHouse:
                {
                    bool hasThree = false;
                    bool hasTwo = false;

                    for (int i = 1; i <= 6; i++)
                    {
                        if (counts[i] == 3) hasThree = true;
                        if (counts[i] == 2) hasTwo = true;
                    }

                    return (hasThree && hasTwo) ? sum : 0;
                }

            case Category.SmallStraight:
                if (HasRun(counts, 1, 4) || HasRun(counts, 2, 5) || HasRun(counts, 3, 6))
                    return 15;
                return 0;

            case Category.LargeStraight:
                if (HasRun(counts, 1, 5) || HasRun(counts, 2, 6))
                    return 30;
                return 0;

            case Category.Yacht:
                for (int i = 1; i <= 6; i++)
                {
                    if (counts[i] == 5) return 50;
                }
                return 0;
        }

        return 0;
    }

    // from ~ to 눈이 전부 1개 이상 있는지
    private static bool HasRun(int[] counts, int from, int to)
    {
        for (int i = from; i <= to; i++)
        {
            if (counts[i] == 0) return false;
        }
        return true;
    }
}