using Attributes;
using UnityEngine;
using UnityEditor;

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(LargeTexturePreview))]
public class LargeTexturePreviewDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        LargeTexturePreview attr = (LargeTexturePreview)attribute;
        
        // Draw the default property field (I can get rid of the last two lines to only show the image)
        float fieldHeight = EditorGUIUtility.singleLineHeight;
        Rect fieldRect = new Rect(position.x, position.y, position.width, fieldHeight);
        EditorGUI.PropertyField(fieldRect, property, label);
        
        // Draw large preview if texture exists
        if (property.objectReferenceValue != null)
        {
            Texture texture = property.objectReferenceValue as Texture;
            if (texture != null)
            {
                float previewSize = attr.size;
                
                // Using two pixels for spacing
                Rect previewRect = new Rect(position.x, position.y + fieldHeight + 2, previewSize, previewSize);
                EditorGUI.DrawPreviewTexture(previewRect, texture);
            }
        }
    }
    
    // This is essentially just asking how much space unity needs in the inspector before drawing
    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        LargeTexturePreview attr = (LargeTexturePreview)attribute;
        float baseHeight = EditorGUIUtility.singleLineHeight;
        
        // Add preview height if texture exists
        if (property.objectReferenceValue != null)
        {
            return baseHeight + attr.size + 4f; // +4 for spacing
        }
        
        // If we don't need to draw the texture, we limit the space to only fit the property field
        return baseHeight;
    }
}
#endif