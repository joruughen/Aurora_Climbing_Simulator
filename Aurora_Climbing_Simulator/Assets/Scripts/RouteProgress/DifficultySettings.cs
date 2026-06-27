using UnityEngine;

namespace Aurora.RouteProgress
{
    public enum Difficulty { Principiante, Avanzado }

    public static class DifficultySettings
    {
        public static Difficulty Selected { get; set; } = Difficulty.Principiante;

        public static float GetTimeLimit()
        {
            return Selected == Difficulty.Avanzado ? 120f : 600f;
        }
    }
}