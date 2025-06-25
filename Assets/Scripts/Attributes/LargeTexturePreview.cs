using UnityEngine;

namespace Attributes
{
    public class LargeTexturePreview : PropertyAttribute
    {
        public readonly float size;
    
        public LargeTexturePreview(float previewSize = 128f)
        {
            size = previewSize;
        }
    }
}
