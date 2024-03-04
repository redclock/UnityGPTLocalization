using System;
using System.Collections.Generic;
using UnityEngine.Localization.Tables;

namespace RedGame.Framework.EditorTools
{
    public partial class LocalizeGptWindow
    {
        private class TranslateRec
        {
            public bool selected;
            public string key;
            public List<StringTable> srcTables;
            public List<StringTable> dstTables;
            public string srcLangNames;
            public string dstLangNames;
            public string prompt;
            public string systemPrompt;
        }
        
        private TranslateRec[] _recs;

        private void RefreshRecord(string key)
        {
            int index = Array.FindIndex(_recs, rec => rec.key == key);
            if (index >= 0)
            {
                _recs[index] = GeneratePrompt(_curCollection, key);
            }
        }

        private void RefreshRecords()
        {
            if (!_curCollection)
            {
                if (_recs == null || _recs.Length > 0)
                    _recs = Array.Empty<TranslateRec>();
                return;
            }

            Dictionary<string, bool> selectedKeys = new Dictionary<string, bool>();
            if (_recs != null)
            {
                foreach (var rec in _recs)
                {
                    selectedKeys[rec.key] = rec.selected;
                }
            }

            List<TranslateRec> recs = new List<TranslateRec>();
            foreach (var entry in _curCollection.SharedData.Entries)
            {
                var rec = GeneratePrompt(_curCollection, entry.Key);
                if (rec == null)
                    continue;
                if (selectedKeys.TryGetValue(entry.Key, out bool selected))
                {
                    rec.selected = selected;
                } else
                {
                    rec.selected = true;
                }

                recs.Add(rec);
            }

            _recs = recs.ToArray();
        }
    }
}