using System.Linq;
using System.Threading.Tasks;
using RedGame.OpenAI;
using UnityEngine;

namespace RedGame.Framework.EditorTools
{
    public partial class LocalizeGptWindow
    {
        private float _taskStartTime;
        private float _taskDuration = 3;
        private Task<CreateChatCompletionResponse> _task;
        private TranslateRec[] _pendingRecs;
        private int _currentPendingRecIndex;
        
        private void TranslateSingleRec(TranslateRec rec)
        {
            InitOpenAi();
            RefreshRecord(rec.key);
            _pendingRecs = new[] {rec};
            _currentPendingRecIndex = 0;
            AskGpt(rec);
            _taskStartTime = Time.realtimeSinceStartup;
        }
        
        private void TranslateSelectedRecs()
        {
            InitOpenAi();
            var recs = _recs.Where((rec) => rec.selected).ToArray();
            if (recs.Length == 0)
                return;
            
            _pendingRecs = recs;
            foreach (var rec in recs)
            {
                RefreshRecord(rec.key);
            }
            
            _currentPendingRecIndex = 0;
            AskGpt(_pendingRecs[0]);
            _taskStartTime = Time.realtimeSinceStartup;
        }

        private void CancelTask()
        {
            _task = null;
            _pendingRecs = null;
            _currentPendingRecIndex = 0;
        }
        
        private void UpdateTaskProgress()
        {
            if (_task == null)
                return;

            if (!_task.IsCompleted)
                return;

            if (_pendingRecs != null && _currentPendingRecIndex < _pendingRecs.Length)
            {
                var rec = _pendingRecs[_currentPendingRecIndex];
                OnTaskCompleted(_task, rec);
                _taskDuration = Time.realtimeSinceStartup - _taskStartTime;
                
                if (_currentPendingRecIndex < _pendingRecs.Length - 1)
                {
                    _currentPendingRecIndex++;
                    AskGpt(_pendingRecs[_currentPendingRecIndex]);
                    _taskStartTime = Time.realtimeSinceStartup;
                } else
                {
                    _task = null;
                    _pendingRecs = null;
                    RefreshRecords();
                }
            }
        }
        
        private float GetProgress()
        {
            if (_task == null)
                return 0;
           
            float curTaskProgress = Mathf.Clamp01((Time.realtimeSinceStartup - _taskStartTime) / _taskDuration);
            return (_currentPendingRecIndex + curTaskProgress) / _pendingRecs.Length;
        }
    }
}