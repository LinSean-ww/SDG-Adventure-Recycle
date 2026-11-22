using UnityEngine;

namespace SDGAdventure.Level03StoryV2
{
    // Existing EcoAdventureRuntime.Shoot sends TakeDamage(float) to a hit collider.
    // This component is therefore struck by the REAL carried weapon, not a simulated UI click.
    public sealed class SDGLevel03PracticeTarget : MonoBehaviour
    {
        public SDGLevel03StoryV2Controller Owner;
        public void TakeDamage(float damage)
        {
            if (Owner != null && damage > 0f) Owner.TargetHit(this);
        }
    }
}
