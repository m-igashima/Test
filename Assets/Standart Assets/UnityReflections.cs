using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

namespace UnityReflections
{
#if UNITY_EDITOR
    public static class AnnotationUtility
    {
        public static float Gizmos3DIconSize
        {
            get
            {
                // AnnotationUtility
                var annotationUtilityType = System.Type.GetType("UnityEditor.AnnotationUtility, UnityEditor");
                var iconSizeType = annotationUtilityType.GetProperty("iconSize", BindingFlags.NonPublic | BindingFlags.Static);
                var iconSize = iconSizeType.GetValue(annotationUtilityType);
                return (float)iconSize * 32;
            }
        }
    }
#endif
}
