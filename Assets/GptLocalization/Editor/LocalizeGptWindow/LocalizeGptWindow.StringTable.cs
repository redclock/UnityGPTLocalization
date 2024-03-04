using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;

namespace RedGame.Framework.EditorTools
{
    public partial class LocalizeGptWindow
    {
        private StringTableCollection _curCollection;
        private StringTableCollection[] _collections;
        private string[] _collectionNames;
        
        private void RefreshStringTableCollection()
        {
            _curCollection = null;
            CancelTask();

            string[] guids = AssetDatabase.FindAssets("t:StringTableCollection");
            _collections = new StringTableCollection[guids.Length];
            _collectionNames = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[i]);
                _collections[i] = AssetDatabase.LoadAssetAtPath<StringTableCollection>(path);
                _collectionNames[i] = _collections[i].name;
            }

            if (_collections.Length > 0)
            {
                _curCollection = _collections[0];
            } else
            {
                _curCollection = null;
            }

            _recs = null;
        }

        // Notify Localization Table Editor to refresh by calling
        // internal method LocalizationEditorSettings.EditorEvents.RaiseCollectionModified
        private void NotifyStringTableEditorRefresh()
        {
            Type classType = typeof(LocalizationEditorEvents);
            MethodInfo methodInfo = classType.GetMethod("RaiseCollectionModified",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (methodInfo != null)
            {
                try
                {
                    methodInfo.Invoke(LocalizationEditorSettings.EditorEvents, new object[] { this, _curCollection });
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}