using UnityEditor;
using UnityEngine;

namespace RedGame.Framework.EditorTools
{
    public partial class LocalizeGptWindow : EditorWindow
    {
        private string _model = "gpt-3.5-turbo";
        private readonly string[] _validModels =
        {
            "gpt-3.5-turbo", 
            "gpt-4-turbo-preview",
            "gpt-4-0125-preview",
            "gpt-4-1106-preview",
            "gpt-3.5-turbo-0125",
            "gpt-3.5-turbo-1106"
        };
        
        private float _temperature;
        private string _apiKey;
        private const string DEFAULT_BASE_URL = "https://api.openai.com/v1";
        private string _baseUrl = DEFAULT_BASE_URL;
        
        [MenuItem("Tools/GPT Localization")]
        private static void ShowWindow()
        {
            var window = GetWindow<LocalizeGptWindow>();
            window.titleContent = new GUIContent("GPT Localization");
            window.Show();
        }

        private void OnEnable()
        {
            LoadSettings();
            EditorApplication.update += UpdateFrame;
            CancelTask();
        }

        private void OnDisable()
        {
            EditorApplication.update -= UpdateFrame;
            CancelTask();
        }

        private void OnFocus()
        {
            if (!_curCollection)
            {
                RefreshStringTableCollection();
            }
            if (!IsBusy())
            {
                RefreshRecords();
            }
        }
        
        private void Output(string str, OutputType type)
        {
            _outputStr = str;
            _outputType = type;
            if (type == OutputType.Error)
                Debug.LogError(str);
        }

        private void UpdateFrame()
        {
            if (IsBusy())
            {
                UpdateTaskProgress();
                Repaint();
            }
        }
        
        private bool IsBusy() => _task != null;
        
        private void LoadSettings()
        {
            _model = EditorPrefs.GetString("LocalizeGptWindow.Model", _model);
            _temperature = EditorPrefs.GetFloat("LocalizeGptWindow.Temperature", _temperature);
            _baseUrl = EditorPrefs.GetString("LocalizeGptWindow.BaseUrl", _baseUrl);
            _apiKey = EditorPrefs.GetString("LocalizeGptWindow.ApiKey", _apiKey);
        }
        
        private void SaveSettings()
        {
            EditorPrefs.SetString("LocalizeGptWindow.Model", _model);
            EditorPrefs.SetFloat("LocalizeGptWindow.Temperature", _temperature);
            EditorPrefs.SetString("LocalizeGptWindow.BaseUrl", _baseUrl);
            EditorPrefs.SetString("LocalizeGptWindow.ApiKey", _apiKey);
        }
    }
}