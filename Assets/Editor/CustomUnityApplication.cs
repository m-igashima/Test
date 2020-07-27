using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public static class CustomUnityApplication
{
    [InitializeOnLoadMethod]
    static void Start()
    {
        Selection.selectionChanged += () =>
        {
            if (Selection.activeObject != null)
            {
                //Debug.Log(Selection.activeObject.name + ": IsOpenForEdit = " + AssetDatabase.IsOpenForEdit(Selection.activeObject, StatusQueryOptions.UseCachedIfPossible));
                string path = GetFullPath(AssetDatabase.GetAssetPath(Selection.activeObject));
                System.IO.FileAttributes attributes = System.IO.File.GetAttributes(path);
                Debug.Log(path + ": attributes = " + attributes);

                if ((attributes & System.IO.FileAttributes.ReadOnly) == System.IO.FileAttributes.ReadOnly)
                {
                    // 読み込み専用
                    Selection.activeObject.hideFlags |= HideFlags.NotEditable;
                }
                else
                {
                    Selection.activeObject.hideFlags &= ~HideFlags.NotEditable;
                }
            }
        };
    }



    private static string GetFullPath(string assetPath)
    {
        return Application.dataPath.Replace("/Assets", "/" + assetPath);
    }
}
