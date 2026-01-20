using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class LSystemGenerator : MonoBehaviour
{
    [Range(0f, 180f)] public float curvatureAngle = 20f;
    [Range(0f, 1f)] public float subdivisionProbability = 0.4f;
    [Range(0f, 360f)] public float userDispersionAngle = 137.5f; // 해석기에서 랜덤 회전 등에 활용할 수 있음

    // 해석기가 이해하는 알파벳만 사용
    private const string AXIOM = "X";
    private Dictionary<char, string[]> rules;

    void Awake()
    {
        // 전형적인 브래킷 L-시스템
        // X -> F[+X][-X]FX (확률 분기)
        rules = new Dictionary<char, string[]>
        {
            { 'X', new[]{
                "F[+X]F[-X]FX",     // 가지 양쪽
                "F[+X]FX",          // 한쪽만
                "F[-X]FX",          // 반대쪽만
                "FF"                // 직진 위주
            }},
            { 'F', new[]{
                "FF",               // 앞으로 성장
                "F"                 // 그대로 (성장 완만)
            }}
        };
    }

    public string Generate(int iterations)
    {
        string current = AXIOM;
        var sb = new StringBuilder();

        for (int i = 0; i < iterations; i++)
        {
            sb.Clear();
            foreach (char ch in current)
            {
                if (rules.TryGetValue(ch, out var alts))
                {
                    // 간단한 확률 분기
                    string picked =
                        (ch == 'X' && Random.value < subdivisionProbability)
                        ? alts[Random.Range(0, alts.Length - 1)] // 더 가지치기 되는 규칙들 쪽을 우대
                        : alts[alts.Length - 1];                  // 마지막(보수적 성장)
                    sb.Append(picked);
                }
                else
                {
                    sb.Append(ch);
                }
            }
            current = sb.ToString();
        }

        // 출력은 F,+,-,[,] 만 포함 → 해석기와 100% 호환
        return current;
    }

    // SurfaceCreation에서 부르던 초기화와 호환
    public void Initialize(float curvature, float prob, float dispersion)
    {
        curvatureAngle = curvature;
        subdivisionProbability = prob;
        userDispersionAngle = dispersion;
    }
}
