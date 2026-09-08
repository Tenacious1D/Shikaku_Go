using UnityEngine;

namespace Shikaku.UI
{
    [CreateAssetMenu(menuName = "Shikaku/Puzzle Palette")]
    public class PuzzlePalette : ScriptableObject
    {
        [Tooltip("Index = number. Element 0 unused. Set at least 1..16.")]
        public Color[] numberColors = new Color[17]; // 0..16

        public Color GetColorForNumber(int n)
        {
            if (n <= 0 || n >= numberColors.Length) return Color.white;
            return numberColors[n];
        }
    }
}
