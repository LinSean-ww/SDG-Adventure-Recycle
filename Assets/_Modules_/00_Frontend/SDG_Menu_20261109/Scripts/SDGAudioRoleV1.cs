using UnityEngine;

namespace SDGAdventure.FrontEnd
{
    /// <summary>Optional classification for AudioSource objects with non-descriptive names.</summary>
    public sealed class SDGAudioRoleV1 : MonoBehaviour
    {
        public enum Category { Effects, Music }
        public Category category = Category.Effects;
    }
}
