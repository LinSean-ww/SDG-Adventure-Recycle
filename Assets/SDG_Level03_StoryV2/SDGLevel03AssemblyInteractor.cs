using UnityEngine;
using UnityEngine.EventSystems;

namespace SDGAdventure.Level03StoryV2
{
    // Pointer events are translated to raycasts in the RenderTexture's real 3D world.
    public sealed class SDGLevel03AssemblyInteractor : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public SDGLevel03AssemblyPreview Preview;
        public void OnPointerDown(PointerEventData data)
        {
            if (Preview != null) Preview.PointerDown(data.position, data.pressEventCamera);
        }
        public void OnDrag(PointerEventData data)
        {
            if (Preview != null) Preview.PointerDrag(data.position, data.pressEventCamera);
        }
        public void OnPointerUp(PointerEventData data)
        {
            if (Preview != null) Preview.PointerUp(data.position, data.pressEventCamera);
        }
    }
}
