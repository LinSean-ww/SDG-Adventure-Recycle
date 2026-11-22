using UnityEngine;

namespace SDGAdventure.Level03StoryV2
{
    /// <summary>
    /// Pixel positions measured on the user-supplied 1448x1086 reference image.
    /// Rendered 900x675 at the same aspect ratio. The visible cable and cursor
    /// collision share exactly this smoothed centreline.
    /// </summary>
    public static class SDGLevel03PhotoTracePath
    {
        private const float SourceW=1448f;
        private const float SourceH=1086f;
        private static readonly Vector2[] ReferencePixels =
        {
            new Vector2(174f,344f),new Vector2(206f,355f),
            new Vector2(244f,376f),new Vector2(276f,421f),
            new Vector2(312f,454f),new Vector2(350f,469f),
            new Vector2(383f,455f),new Vector2(418f,428f),
            new Vector2(453f,391f),new Vector2(503f,365f),
            new Vector2(557f,354f),new Vector2(605f,368f),
            new Vector2(641f,403f),new Vector2(672f,451f),
            new Vector2(707f,515f),new Vector2(748f,572f),
            new Vector2(792f,620f),new Vector2(841f,639f),
            new Vector2(889f,639f),new Vector2(938f,614f),
            new Vector2(979f,580f),new Vector2(1015f,528f),
            new Vector2(1050f,459f),new Vector2(1085f,402f),
            new Vector2(1122f,363f),new Vector2(1169f,333f),
            new Vector2(1224f,314f),new Vector2(1270f,311f)
        };

        public static Vector2[] Build(float uiWidth,float uiHeight)
        {
            Vector2[] knots=new Vector2[ReferencePixels.Length];
            for(int i=0;i<knots.Length;i++)
            {
                Vector2 p=ReferencePixels[i];
                knots[i]=new Vector2(
                    (p.x/SourceW-.5f)*uiWidth,
                    (.5f-p.y/SourceH)*uiHeight);
            }
            return SDGLevel03TraceGraphic.Smooth(knots,10);
        }
    }
}
