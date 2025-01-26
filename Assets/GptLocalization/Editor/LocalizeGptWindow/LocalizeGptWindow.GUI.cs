using System;
using UnityEditor;
using UnityEditor.Localization.UI;
using UnityEngine;

namespace RedGame.Framework.EditorTools
{
    public partial class LocalizeGptWindow
    {
        enum OutputType
        {
            None, Prompt, Error, Info
        }
        
        private SimpleEditorTableView<TranslateRec> _tableView;
        private Vector2 _scrollPosition;
        private string _outputStr;
        private OutputType _outputType;
        private bool _gptFoldout = true;
        private bool _collectionFoldout = true;

        private void OnGUI()
        {
            if (IsBusy())
            {
                OnBusyGUI();
                return;
            }
            
            OnGptModelGUI();
            EditorGUILayout.Space();
            OnSelectCollectionGUI();
            EditorGUILayout.Space();
            if (!_curCollection)
            {
                return;
            }

            OnEntryListGUI();
            OnOutputGUI();
        }

        private SimpleEditorTableView<TranslateRec> CreateTable()
        {
            SimpleEditorTableView<TranslateRec> tableView = new SimpleEditorTableView<TranslateRec>();

            GUIStyle labelGUIStyle = new GUIStyle(GUI.skin.label)
            {
                padding = new RectOffset(left: 10, right: 10, top: 2, bottom: 2)
            };

            tableView.AddColumn("", 30, (rect, rec) =>
            {
                rec.selected = EditorGUI.Toggle(
                    position: rect,
                    value: rec.selected
                );
            }).SetMaxWidth(40).SetSorting((a, b) => a.selected.CompareTo(b.selected));

            tableView.AddColumn("Key", 80, (rect, rec) =>
            {
                EditorGUI.LabelField(
                    position: rect,
                    label: rec.key,
                    style: labelGUIStyle
                );
            }).SetAutoResize(true).SetSorting((a, b) => String.Compare(a.key, b.key, StringComparison.Ordinal));

            tableView.AddColumn("Src Locales", 100, (rect, rec) =>
            {
                EditorGUI.LabelField(
                    position: rect,
                    label: rec.srcLangNames,
                    style: labelGUIStyle
                );
            }).SetAllowToggleVisibility(true);

            tableView.AddColumn("Dst Locales", 100, (rect, rec) =>
            {
                EditorGUI.LabelField(
                    position: rect,
                    label: string.Join(',', rec.dstLangNames),
                    style: labelGUIStyle
                );
            }).SetAllowToggleVisibility(true);

            tableView.AddColumn("Operation", 180, (rect, rec) =>
            {
                Rect rt1 = new Rect(rect.x, rect.y, rect.width / 2, rect.height);

                if (GUI.Button(rt1, "Show Prompt"))
                {
                    RefreshRecord(rec.key);
                    Output("System Prompt: \n" + rec.systemPrompt + "\n User Prompt:\n" + rec.prompt, OutputType.Prompt);
                }

                Rect rt2 = new Rect(rect.x + rect.width / 2, rect.y, rect.width / 2, rect.height);

                if (GUI.Button(rt2, "Translate"))
                {
                    TranslateSingleRec(rec);
                }
            });
            return tableView;
        }

        private void OnSelectCollectionGUI()
        {
            if (_collections == null || _recs == null)
            {
                RefreshStringTableCollection();
            }

            if (_collections == null || _collections.Length == 0)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox("No StringTableCollection found.\n"+
                                        "Please create at least one collection with Unity Localization", MessageType.Error);
                EditorGUILayout.EndHorizontal();
                
                if (GUILayout.Button("Create Collection"))
                {
                    LocalizationTablesWindow.ShowWindow();
                }
                
                return;
            }

            _collectionFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_collectionFoldout, "String Table Collection");

            if (_collectionFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical("Box");
                EditorGUILayout.BeginHorizontal();
                int index = Array.IndexOf(_collections, _curCollection);
                index = EditorGUILayout.Popup("Select Collection", index, _collectionNames);
                if (index >= 0 && index < _collections.Length)
                {
                    _curCollection = _collections[index];
                }

                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.EndFoldoutHeaderGroup();
        }
        
        private void OnGptModelGUI()
        {
            _gptFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(_gptFoldout, "GPT Model Parameters");

            if (_gptFoldout)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.BeginVertical("Box");
                _baseUrl = EditorGUILayout.TextField("Base URL", _baseUrl);
                if (string.IsNullOrEmpty(_baseUrl))
                    _baseUrl = DEFAULT_BASE_URL;
                
                _apiKey = EditorGUILayout.PasswordField("API Key", _apiKey);
                if (!IsValidOpenAIKey(_apiKey))
                {
                    GUIStyle style = new GUIStyle(EditorStyles.helpBox)
                    {
                        richText = true
                    };
                    EditorGUILayout.BeginHorizontal();
                    EditorGUI.indentLevel--;
                    GUILayout.Space(EditorGUIUtility.labelWidth);
                    EditorGUILayout.LabelField("<color=#ff4444>Please enter a valid OpenAI API Key.</color>", style);
                    EditorGUI.indentLevel++;
                    EditorGUILayout.EndHorizontal();
                }
                int index = Array.IndexOf(s_validModels, _model);
                index = EditorGUILayout.Popup("Model", index, s_validModels);
                if (index >= 0 && index < s_validModels.Length)
                {
                    _model = s_validModels[index];
                }
            
                _temperature = EditorGUILayout.Slider("Temperature", _temperature, 0, 1);
                EditorGUILayout.EndVertical();
                
                if (EditorGUI.EndChangeCheck())
                {
                    SaveSettings();
                }

                EditorGUI.indentLevel--;
            }
            
            EditorGUILayout.EndFoldoutHeaderGroup();
            
        }

        private void OnBusyGUI()
        {
            TranslateRec rec = _pendingRecs[_currentPendingRecIndex];
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Translating ", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            Rect progressRect = GUILayoutUtility.GetRect(100, EditorGUIUtility.singleLineHeight, GUILayout.ExpandWidth(true));
            EditorGUI.ProgressBar(progressRect, GetProgress(),
                $"{rec.key}({_currentPendingRecIndex + 1}/{_pendingRecs.Length})");
            if (GUILayout.Button("Cancel", GUILayout.Width(100)))
            {
                CancelTask();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("System Prompt: ", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(rec.systemPrompt, EditorStyles.wordWrappedLabel);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("User Prompt: ", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField(rec.prompt, EditorStyles.wordWrappedLabel);
            EditorGUI.indentLevel--;
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
        }

        private void OnOutputGUI()
        {
            if (!string.IsNullOrEmpty(_outputStr) && _outputType != OutputType.None)
            {
                EditorGUILayout.Space();
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(_outputType.ToString(), EditorStyles.boldLabel);
                if (GUILayout.Button("Clear", GUILayout.Width(100)))
                {
                    _outputStr = string.Empty;
                    _outputType = OutputType.None;
                }
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
                if (_outputType == OutputType.Error)
                {
                    EditorGUILayout.HelpBox(_outputStr, MessageType.Error);
                } else
                {
                    _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition, GUILayout.Height(100));
                    EditorGUILayout.TextArea(_outputStr, GUILayout.ExpandHeight(true));
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void OnEntryListGUI()
        {
            EditorGUILayout.LabelField("Entries to be localized", EditorStyles.boldLabel);
            if (GUILayout.Button("Open Table Editor"))
            {
                LocalizationTablesWindow.ShowWindow(_curCollection);
            }
            if (_recs == null)
                RefreshRecords();

            if (_recs == null || _recs.Length == 0)
            {
                EditorGUILayout.HelpBox("No entry to translate.\n" +
                                        "Only entries that are partially localized will appear here.",
                    MessageType.Warning);
                return;
            }
            
            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button("Select All", GUILayout.Width(100)))
            {
                foreach (var rec in _recs)
                {
                    rec.selected = true;
                }
            }

            if (GUILayout.Button("Deselect All", GUILayout.Width(100)))
            {
                foreach (var rec in _recs)
                {
                    rec.selected = false;
                }
            }

            if (GUILayout.Button("Translate Selected", GUILayout.ExpandWidth(true)))
            {
                TranslateSelectedRecs();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            _tableView ??= CreateTable();
            _tableView.DrawTableGUI(_recs, (_recs.Length + 2) * EditorGUIUtility.singleLineHeight);
        }


        private bool IsValidOpenAIKey(string key)
        {
            var apiKeyPattern = @"^sk-\w{32,}$";
            return System.Text.RegularExpressions.Regex.IsMatch(key, apiKeyPattern);
        }
    }
}