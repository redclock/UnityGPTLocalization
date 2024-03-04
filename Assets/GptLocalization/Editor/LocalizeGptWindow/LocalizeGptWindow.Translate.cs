using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using RedGame.OpenAI;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Tables;

namespace RedGame.Framework.EditorTools
{
    public partial class LocalizeGptWindow
    {
        private OpenAIApi _openAi;

        private const string SYSTEM_PROMPT =
            "You are a multilingual translation AI. " +
            "Your response should be in JSON format, " +
            "with each language name as a key and the translated text as the corresponding value.\n\n";

        private void InitOpenAi()
        {
            _openAi = new OpenAIApi(
                apiKey:_apiKey, 
                baseUrl:_baseUrl);
        }
        
        private TranslateRec GeneratePrompt(StringTableCollection collection, string key)
        {
            StringBuilder sb = new StringBuilder();
            TranslateRec rec = new TranslateRec
            {
                key = key, selected = true
            };
            
            sb.Append("Translate the following texts:\n");
            var tables = collection.StringTables;
            rec.srcTables = new List<StringTable>();
            rec.dstTables = new List<StringTable>();
            foreach (var table in tables)
            {
                StringTableEntry entry = table.GetEntry(key);

                if (entry == null || string.IsNullOrWhiteSpace(entry.Value))
                {
                    rec.dstTables.Add(table);
                    continue;
                }

                string langName = table.LocaleIdentifier.CultureInfo.EnglishName;
                rec.srcTables.Add(table);
                if (rec.srcTables.Count <= 2)
                {
                    sb.Append("## From " + langName + ":\n" + entry.Value + "\n");
                }
            }

            sb.Append("\n");

            if (rec.dstTables.Count == 0 || rec.srcTables.Count == 0)
            {
                return null;
            }

            rec.srcLangNames = string.Join(",", rec.srcTables.Select(t => t.LocaleIdentifier.CultureInfo.EnglishName));
            rec.dstLangNames = string.Join(",", rec.dstTables.Select(t => t.LocaleIdentifier.CultureInfo.EnglishName));

            sb.Append("\n# Translate to: \n");
            sb.Append(rec.dstLangNames);
            sb.Append("\n\n");

            StringBuilder requirements = new StringBuilder();
            Comment comment = collection.SharedData.GetEntry(key).Metadata.GetMetadata<Comment>();

            if (comment != null && !string.IsNullOrWhiteSpace(comment.CommentText))
            {
                requirements.Append("* ");
                requirements.Append(comment.CommentText);
                requirements.Append("\n");
            }

            foreach (var table in rec.dstTables)
            {
                var entry = table.GetEntry(key);
                if (entry == null)
                    continue;
                var langComment = entry.GetMetadata<Comment>();
                if (langComment != null && !string.IsNullOrWhiteSpace(langComment.CommentText))
                {
                    string langName = table.LocaleIdentifier.CultureInfo.EnglishName;
                    requirements.Append("* Specifically for " + langName + ": " +
                                        langComment.CommentText);
                    requirements.Append("\n");
                }
            }

            if (requirements.Length > 0)
            {
                rec.systemPrompt = SYSTEM_PROMPT +  
                                   "# Translation Instructions:\n" +
                                   requirements;
            } else
            {
                rec.systemPrompt = SYSTEM_PROMPT;
            }

            rec.prompt = sb.ToString();

            return rec;
        }

        private void AskGpt(TranslateRec rec)
        {
            _task = _openAi.CreateChatCompletion(new CreateChatCompletionRequest()
            {
                Model = _model,
                Messages = new List<ChatMessage>()
                {
                    new() { Role = "system", Content = rec.systemPrompt },
                    new() { Role = "user", Content = rec.prompt }
                },
                Temperature = _temperature,
                ResponseFormat = ResponseFormat.JsonObject
            });
        }

        private void OnTaskCompleted(Task<CreateChatCompletionResponse> task, TranslateRec rec)
        {
            if (task.IsFaulted)
            {
                if (task.Exception != null)
                {
                    Output(task.Exception.Message, OutputType.Error);
                    Debug.LogError(task.Exception);
                }
                return;
            }

            var response = task.Result;
            if (response.Error != null)
            {
                Output(response.Error.Message, OutputType.Error);
                Debug.LogError(response.Error.Message);
                return;
            }

            string answer = response.Choices[0].Message.Content;

            JToken jToken = JToken.Parse(answer);
            Dictionary<string, string> dict = TryParseJson(jToken);

            if (dict != null)
            {
                ApplyTranslation(rec, dict);
            } else
            {
                Output("Failed to parse response as json object:\n" + answer, OutputType.Error);
                Debug.LogError("Failed to parse response as json object");
            }
            
            Output("", OutputType.None);
        }

        private string TryParseText(JToken json)
        {
            if (json.Type == JTokenType.String)
            {
                return json.ToString();
            }

            if (json.Type == JTokenType.Object)
            {
                JObject jObject = (JObject)json;
                if (jObject.ContainsKey("text"))
                    return jObject["text"].ToString();
                if (jObject.ContainsKey("result"))
                    return jObject["result"].ToString();
            }

            return string.Empty;
        }

        private string TryParseKey(JObject jObject)
        {
            if (jObject.ContainsKey("name"))
                return jObject["name"].ToString();

            if (jObject.ContainsKey("translation"))
                return jObject["translation"].ToString();

            return null;
        }

        private Dictionary<string, string> TryParseJson(JToken json)
        {
            // 三种可能：
            // 1. 一个json对象 language: translation
            // 2. 一个json数组，每个元素是一个json对象
            // 3. 有一个根节点的，根节点为"translations"，下层才是

            if (json.Type == JTokenType.Object)
            {
                JObject jObject = (JObject)json;

                JProperty rootProp = jObject.Properties().FirstOrDefault(p =>
                {
                    return p.Name.Equals("translations", StringComparison.OrdinalIgnoreCase) ||
                           p.Name.Equals("translation", StringComparison.OrdinalIgnoreCase);
                });

                if (rootProp != null)
                {
                    return TryParseJson(rootProp.Value);
                }

                return jObject.Properties()
                    .ToDictionary(
                        p => p.Name,
                        p => TryParseText(p.Value));
            }

            if (json.Type == JTokenType.Array)
            {
                JArray jArray = (JArray)json;

                Dictionary<string, string> dict = new Dictionary<string, string>();
                foreach (var item in jArray)
                {
                    if (item.Type == JTokenType.Object)
                    {
                        JObject jObject = (JObject)item;
                        string key = TryParseKey(jObject);
                        if (key != null)
                        {
                            dict[key] = TryParseText(jObject);
                        }
                    }
                }

                return dict;
            }

            return null;
        }

        private void ApplyTranslation(TranslateRec rec, Dictionary<string, string> result)
        {
            foreach (var table in rec.dstTables)
            {
                string langName = table.LocaleIdentifier.CultureInfo.EnglishName;
                if (result.ContainsKey(langName))
                {
                    string translated = result[langName];
                    table.AddEntry(rec.key, translated);
                }
                EditorUtility.SetDirty(table);
            }

            NotifyStringTableEditorRefresh();
        }
    }
}