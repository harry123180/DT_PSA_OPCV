using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OC.UI.Console
{
    public class LogList : IList
    {
        public int Count => _filteredList.Count;
        
        public bool IsReadOnly => false;
        public bool IsFixedSize => false;
        public bool IsSynchronized => false;
        public object SyncRoot => this;
        
        public bool ShowInfoLogs;
        public bool ShowWarnLogs;
        public bool ShowErrorLogs;
        public string SearchString;
        public int InfoLogCounter;
        public int WarningLogCounter;
        public int ErrorLogCounter;

        public object this[int index]
        {
            get => _filteredList[index]; 
            set => _filteredList.Insert(index, (ConsoleItemData)value);
        }

        private readonly List<ConsoleItemData> _list;
        private readonly List<ConsoleItemData> _filteredList;

        public LogList()
        {
            _list = new List<ConsoleItemData>();
            _filteredList = new List<ConsoleItemData>();
        }

        public void Filter()
        {
            _filteredList.Clear();

            foreach (var item in _list)
            {
                switch (item.LogType)
                {
                    case LogType.Error:
                        if (ShowErrorLogs) AddFilteredItem(item);
                        break;
                    case LogType.Assert:
                        break;
                    case LogType.Warning:
                        if (ShowWarnLogs) AddFilteredItem(item);
                        break;
                    case LogType.Log:
                        if (ShowInfoLogs) AddFilteredItem(item);
                        break;
                    case LogType.Exception:
                        if (ShowErrorLogs) AddFilteredItem(item);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public void Clear()
        {
            ErrorLogCounter = 0;
            InfoLogCounter = 0;
            WarningLogCounter = 0;

            _list.Clear();
            _filteredList.Clear();
        }

        public void RemoveAt(int index)
        {
            DecreaseCounters(_list[index]);
            _list.RemoveAt(index);
        }

        public int Add(object value)
        {
            if (value != null)
            {
                var item = (ConsoleItemData)value;

                _list.Add(item);
                IncreaseCounters(item);

                Filter();

                return _list.IndexOf(item);
            }

            return _list.Count;
        }

        public bool Contains(object value)
        {
            return value != null && _filteredList.Contains((ConsoleItemData)value);
        }

        public int IndexOf(object value)
        {
            return _filteredList.IndexOf((ConsoleItemData)value);
        }

        public void Insert(int index, object value)
        {
            _list.Insert(index, (ConsoleItemData)value);
        }

        public void Remove(object value)
        {
            DecreaseCounters((ConsoleItemData)value);
            _list.Remove((ConsoleItemData)value);
            _filteredList.Remove((ConsoleItemData)value);
        }

        public void CopyTo(Array array, int index)
        {
            _list.CopyTo((ConsoleItemData[])array, index);
        }

        private void AddFilteredItem(ConsoleItemData consoleItemDataToAdd)
        {
            if (string.IsNullOrEmpty(SearchString))
            {
                _filteredList.Add(consoleItemDataToAdd);
            }
            else
            {
                if (!consoleItemDataToAdd.Message.ToLower().Contains(SearchString.ToLower())) return;
                _filteredList.Add(consoleItemDataToAdd);
                if (consoleItemDataToAdd.ConsoleItem == null) return;
                var start = consoleItemDataToAdd.Message.ToLower().IndexOf(SearchString.ToLower(), StringComparison.Ordinal);
                consoleItemDataToAdd.ConsoleItem.Label.selection.SelectRange(start, start + SearchString.Length);
            }
        }

        private void DecreaseCounters(ConsoleItemData item)
        {
            switch (item.LogType)
            {
                case LogType.Error:
                    ErrorLogCounter--;
                    break;
                case LogType.Assert:
                    break;
                case LogType.Warning:
                    WarningLogCounter--;
                    break;
                case LogType.Log:
                    InfoLogCounter--;
                    break;
                case LogType.Exception:
                    ErrorLogCounter--;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void IncreaseCounters(ConsoleItemData item)
        {
            switch (item.LogType)
            {
                case LogType.Error:
                    ErrorLogCounter++;
                    break;
                case LogType.Assert:
                    break;
                case LogType.Warning:
                    WarningLogCounter++;
                    break;
                case LogType.Log:
                    InfoLogCounter++;
                    break;
                case LogType.Exception:
                    ErrorLogCounter++;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _filteredList.GetEnumerator();
        }
    }
}
