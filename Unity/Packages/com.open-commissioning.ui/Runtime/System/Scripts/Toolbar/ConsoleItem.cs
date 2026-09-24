using NaughtyAttributes;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using OC.UI.Console;
using OC.UI.Panel;

namespace OC.UI.Toolbar
{
    public class ConsoleItem : ToolbarWindow
    {
        [Header("Console Settings")]
        [SerializeField]
        private bool _showInfoLogs = true;

        [SerializeField]
        private bool _showWarnLogs = true;

        [SerializeField]
        private bool _showErrorLogs = true;

        private const int LOG_BUFFER_CAPACITY = 3000;
        private const int COMMAND_BUFFER_CAPACITY = 100;
        private const int FIXED_ITEM_HEIGHT = 45;

        private const string STYLE_PANEL_FIELD = "StyleSheet/panel-field";
        private const string INFO_ICON_RESOURCE = "Icons/d_ConsoleInfoIcon@2x";
        private const string WARNING_ICON_RESOURCE = "Icons/d_ConsoleWarnIcon@2x";
        private const string ERROR_ICON_RESOURCE = "Icons/d_ConsoleErrorIcon@2x";
        private const string STYLE_RESOURCE = "StyleSheet/console";
        private const string USS_CONSOLE_LIST_VIEW = "console-list-view";
        private const string USS_CONSOLE_DETAILS_SCROLL_VIEW = "console-details__scroll-view";
        private const string USS_CONSOLE_COMMAND_POPUP = "console-command-popup";
        private const string USS_CONSOLE_COMMAND_POPUP_COMMAND_PREVIEW = "console-command-popup__preview";
        private const string USS_CONSOLE_COMMAND_INPUT = "console-command__input";
        private const string USS_CONSOLE_SEARCH = "console-search";
        private const string USS_CONSOLE_DETAILS_LABEL = "console-details__label";
        private const string USS_CONSOLE_BUTTON_CONTAINER = "console-button-container";
        private const string USS_CONSOLE_FLOATING_PANEL = "console-floating-panel";
        private const string USS_CONSOLE_CLEAR_BUTTON = "console-clear-button";

        private readonly bool _showNamespaceNameInCommandPreview = true;
        private readonly List<string> _commandHistory = new(COMMAND_BUFFER_CAPACITY);

        private string _commandInputFieldAutoCompleteBase;
        private int _commandHistoryCurrentIndex;

        private bool _enableAutoScroll = true;
        private PanelListView _listView;
        private TextField _itemDetails;
        private LogList _logList;

        private PanelStringField _searchField;
        private PanelStringField _commandField;
        private Toggle _showInfoLogsToggle;
        private Toggle _showWarningLogsToggle;
        private Toggle _showErrorLogsToggle;
        private VisualElement _commandPreviewPopup;
        private List<ConsoleMethodInfo> _matchingCommandsForPopup;
        private StyleBackground _infoIcon;
        private StyleBackground _warningIcon;
        private StyleBackground _errorIcon;

        [Button]
        public static void LogInfo()
        {
            Debug.Log("<color=\"blue\">Log</color> test");
        }

        [Button]
        public static void LogWarning()
        {
            Debug.LogWarning("<color=\"yellow\">Warning</color> test");
        }

        [Button]
        public static void LogError()
        {
            Debug.LogError("<color=\"red\">Error</color> test");
        }

        [Button]
        [ConsoleMethod("logtest", "Test all Logtypes"), UnityEngine.Scripting.Preserve]
        public static void LogAll()
        {
            LogInfo();
            LogWarning();
            LogError();
        }

        [Button]
        public void LogMax()
        {
            for (var i = 0; i < LOG_BUFFER_CAPACITY; i++)
            {
                Debug.Log(i);
            }
        }

        protected override void AddContent(SubsystemPanel panel)
        {
            panel.styleSheets.Add(Resources.Load<StyleSheet>(STYLE_RESOURCE));
            panel.styleSheets.Remove(Resources.Load<StyleSheet>(STYLE_PANEL_FIELD));

            var infoSprite = Resources.Load<Sprite>(INFO_ICON_RESOURCE);
            var warnSprite = Resources.Load<Sprite>(WARNING_ICON_RESOURCE);
            var errorSprite = Resources.Load<Sprite>(ERROR_ICON_RESOURCE);

            _infoIcon = new StyleBackground(infoSprite);
            _warningIcon = new StyleBackground(warnSprite);
            _errorIcon = new StyleBackground(errorSprite);

            _listView = new PanelListView();
            _listView.AddToClassList(USS_CONSOLE_LIST_VIEW);
            _listView.itemsSource = _logList;
            _listView.fixedItemHeight = FIXED_ITEM_HEIGHT;
            _listView.makeItem += MakeConsoleLogItem;
            _listView.bindItem += BindConsoleLogItem;
            _listView.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
            _listView.selectionChanged += ListViewOnSelectionChange;

            _listView.VerticalScroller.valueChanged += VerticalScrollerValueChanged;

            var scrollView = new PanelScrollView
            {
                mode = ScrollViewMode.Vertical
            };
            scrollView.AddToClassList(USS_CONSOLE_DETAILS_SCROLL_VIEW);

            _itemDetails = new TextField
            {
                isReadOnly = true,
                multiline = true
            };
            _itemDetails.AddToClassList(USS_CONSOLE_DETAILS_LABEL);
            _itemDetails.RegisterCallback<MouseDownEvent>(OnDetailsMouseDown);
            scrollView.Add(_itemDetails);

            var buttonContainer = new VisualElement();
            buttonContainer.AddToClassList(USS_CONSOLE_BUTTON_CONTAINER);

            var clearButton = new PanelButton("clear", ClearListView);
            clearButton.AddToClassList(USS_CONSOLE_CLEAR_BUTTON);
            buttonContainer.Add(clearButton);

            _searchField = new PanelStringField("Filter:");
            _searchField.AddToClassList(USS_CONSOLE_SEARCH);
            _searchField.RegisterValueChangedCallback(OnSearchFieldChange);
            _searchField.ToggleStringFieldAltStyle(true);
            buttonContainer.Add(_searchField);

            _commandField = new PanelStringField();
            _commandField.AddToClassList(USS_CONSOLE_COMMAND_INPUT);
            _commandField.SetTextInputAlign(TextAnchor.MiddleLeft);
            _commandField.RegisterCallback<KeyDownEvent>(OnCommandFieldKeyDown);
            _commandField.RegisterValueChangedCallback(OnCommandFielfValueChange);
            _commandField.RegisterCallback<BlurEvent>(OnBlurCommandField);
            _commandField.RegisterCallback<FocusEvent>(OnFocusCommandField);


            _commandPreviewPopup = new VisualElement();
            _commandPreviewPopup.AddToClassList(USS_CONSOLE_COMMAND_POPUP);
            _matchingCommandsForPopup = new List<ConsoleMethodInfo>();

            CommandInputDefault();

            _showInfoLogsToggle = new Toggle(infoSprite);
            _showInfoLogsToggle.RegisterValueChangedCallback(OnShowInfoToggle);
            _showInfoLogsToggle.value = _showInfoLogs;
            _logList.ShowInfoLogs = _showInfoLogsToggle.value;
            buttonContainer.Add(_showInfoLogsToggle);

            _showWarningLogsToggle = new Toggle(warnSprite);
            _showWarningLogsToggle.RegisterValueChangedCallback(OnShowWarnToggle);
            _showWarningLogsToggle.value = _showWarnLogs;
            _logList.ShowWarnLogs = _showWarningLogsToggle.value;
            buttonContainer.Add(_showWarningLogsToggle);

            _showErrorLogsToggle = new Toggle(errorSprite);
            _showErrorLogsToggle.RegisterValueChangedCallback(OnShowErrorToggle);
            _showErrorLogsToggle.value = _showErrorLogs;
            _logList.ShowErrorLogs = _showErrorLogsToggle.value;
            buttonContainer.Add(_showErrorLogsToggle);

            SetTypeCounters();

            panel.Add(buttonContainer);
            panel.Add(_listView);
            panel.Add(scrollView);
            panel.Add(_commandField);
            panel.Add(_commandPreviewPopup);
            panel.AddToClassList(USS_CONSOLE_FLOATING_PANEL);
            panel.OnEnableChanged += OnEnableChangeAction;

            RefreshListView();
        }

        private void OnCommandFielfValueChange(ChangeEvent<string> evt)
        {
            SetCommandPopupContent();
        }

        private void OnFocusCommandField(FocusEvent evt)
        {
            SetCommandPopupContent();
        }

        private void OnBlurCommandField(BlurEvent evt)
        {
            ClearCommandPopupContent();
        }

        private void ClearCommandPopupContent()
        {
            if (_commandPreviewPopup == null) return;
            _commandPreviewPopup.Clear();

            if (_matchingCommandsForPopup == null) return;
            _matchingCommandsForPopup.Clear();
        }

        private void OnDetailsMouseDown(MouseDownEvent evt)
        {
            if (evt.button == 1)
            {
                var editor = new TextEditor
                {
                    text = _itemDetails.value
                };
                editor.SelectAll();
                editor.Copy();
                Debug.Log("Copied Log Details to Clipboard!");
            }
        }

        private void OnCommandFieldKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Return:
                    HandleReturnKey(evt);
                    break;
                case KeyCode.Tab:
                    HandleTabKey(evt);
                    break;
                case KeyCode.UpArrow:
                    HandleUpArrowKey(evt);
                    break;
                case KeyCode.DownArrow:
                    HandleDownArrowKey(evt);
                    break;
                case KeyCode.Delete:
                case KeyCode.Backspace:
                    _commandInputFieldAutoCompleteBase = null;
                    break;
                default:
                    switch (evt.character)
                    {
                        case '\n':
                            evt.StopPropagation();
                            break;
                    }

                    break;
            }
        }

        private void HandleDownArrowKey(KeyDownEvent evt)
        {
            if (_commandHistory.Count == 0) return;

            if (_commandHistoryCurrentIndex != _commandHistory.Count - 1)
            {
                _commandHistoryCurrentIndex++;
            }

            _commandField.value = _commandHistory[_commandHistoryCurrentIndex];
            _commandField.SelectRange(0, _commandField.value.Length);
            evt.StopPropagation();
        }

        private void HandleUpArrowKey(KeyDownEvent evt)
        {
            if (_commandHistory.Count == 0) return;

            _commandField.value = _commandHistory[_commandHistoryCurrentIndex];

            if (_commandHistoryCurrentIndex != 0)
            {
                _commandHistoryCurrentIndex--;
            }

            _commandField.cursorIndex = 0;

            _commandField.SelectRange(0, _commandField.value.Length);
            evt.StopPropagation();
        }

        private void HandleTabKey(KeyDownEvent evt)
        {
            if (string.IsNullOrEmpty(_commandField.text))
            {
                evt.StopPropagation();
                return;
            }

            if (string.IsNullOrEmpty(_commandInputFieldAutoCompleteBase))
            {
                _commandInputFieldAutoCompleteBase = _commandField.text;
            }

            var autoCompletedCommand = DebugLogConsole.GetAutoCompleteCommand(_commandInputFieldAutoCompleteBase, _commandField.text);
            if (!string.IsNullOrEmpty(autoCompletedCommand) && autoCompletedCommand != _commandField.text)
            {
                _commandField.value = autoCompletedCommand;
                _commandField.SelectRange(_commandField.cursorIndex, _commandField.value.Length);
            }

            evt.StopPropagation();
        }

        private void HandleReturnKey(KeyDownEvent evt)
        {
            if (string.IsNullOrEmpty(_commandField.text)) return;
            DebugLogConsole.ExecuteCommand(_commandField.value);

            if (_commandHistory.Count == 0)
            {
                _commandHistory.Add(_commandField.value);
            }
            else if (_commandHistory[^1] != _commandField.value)
            {
                if (_commandHistory.Count == _commandHistory.Capacity)
                {
                    _commandHistory.RemoveAt(0);
                }

                _commandHistory.Add(_commandField.value);
            }

            _commandInputFieldAutoCompleteBase = null;
            _commandField.SelectNone();
            CommandInputDefault();
            _commandHistoryCurrentIndex = _commandHistory.Count - 1;
            evt.StopPropagation();
        }

        private void SetCommandPopupContent()
        {
            ClearCommandPopupContent();

            if (!string.IsNullOrEmpty(_commandField.text))
            {
                // add functionality for starts with
                DebugLogConsole.FindCommands(_commandField.text, true, _matchingCommandsForPopup);
                foreach (var methodInfo in _matchingCommandsForPopup)
                {
                    var commandText = methodInfo.command;
                    if (methodInfo.parameters.Length > 0)
                    {
                        foreach (var paramter in methodInfo.parameters)
                        {
                            commandText += " " + paramter;
                        }
                    }

                    var commandPreview = new VisualElement();
                    commandPreview.AddToClassList(USS_CONSOLE_COMMAND_POPUP_COMMAND_PREVIEW);

                    var commandTextLabel = new Label(commandText);
                    var commandAssembly = new Label(methodInfo.assembly)
                    {
                        name = "commandAssembly"
                    };

                    commandPreview.Add(commandTextLabel);

                    if (_showNamespaceNameInCommandPreview)
                    {
                        commandPreview.Add(commandAssembly);
                    }

                    _commandPreviewPopup.Add(commandPreview);
                }
            }
        }

        private void CommandInputDefault()
        {
            _commandField.value = "";
            ClearCommandPopupContent();
        }

        private void OnSearchFieldChange(ChangeEvent<string> evt)
        {
            _logList.SearchString = evt.newValue;
            RefreshListView();
        }

        private void OnShowInfoToggle(ChangeEvent<bool> evt)
        {
            _logList.ShowInfoLogs = evt.newValue;
            RefreshListView();
        }

        private void OnShowWarnToggle(ChangeEvent<bool> evt)
        {
            _logList.ShowWarnLogs = evt.newValue;
            RefreshListView();
        }

        private void OnShowErrorToggle(ChangeEvent<bool> evt)
        {
            _logList.ShowErrorLogs = evt.newValue;
            RefreshListView();
        }

        private void RefreshListView()
        {
            _logList.Filter();
            _enableAutoScroll = true;
            _listView.RefreshItems();
            _itemDetails.value = string.Empty;
            ScrollToButtom();
        }

        private void ClearListView()
        {
            _logList.Clear();
            _listView.Clear();
            _itemDetails.value = string.Empty;
            SetTypeCounters();

            _listView.RefreshItems();
        }

        private void VerticalScrollerValueChanged(float obj)
        {
            if (_listView.VerticalScroller.highValue < 0) return;
            _enableAutoScroll = System.Math.Abs(_listView.VerticalScroller.value - _listView.VerticalScroller.highValue) < 0.1f;
        }

        private void ListViewOnSelectionChange(IEnumerable<object> obj)
        {
            var item = (ConsoleItemData)obj.FirstOrDefault()!;
            _itemDetails.value = item.Details;
        }

        private void OnEnableChangeAction(bool enable)
        {
            _enableAutoScroll = enable;
        }

        private void BindConsoleLogItem(VisualElement item, int index)
        {
            var consoleItem = (Console.ConsoleItem)item;

            ConsoleItemData logListitem = (ConsoleItemData)_logList[index];

            consoleItem.Message = logListitem.Message;

            switch (logListitem.LogType)
            {
                case LogType.Error:
                    consoleItem.Icon.style.backgroundImage = _errorIcon;
                    break;
                case LogType.Assert:
                    break;
                case LogType.Warning:
                    consoleItem.Icon.style.backgroundImage = _warningIcon;
                    break;
                case LogType.Log:
                    consoleItem.Icon.style.backgroundImage = _infoIcon;
                    break;
                case LogType.Exception:
                    consoleItem.Icon.style.backgroundImage = _errorIcon;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private VisualElement MakeConsoleLogItem()
        {
            return new Console.ConsoleItem();
        }

        private void OnEnable()
        {
            _logList = new LogList();
            Application.logMessageReceived += OnNewLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnNewLog;
        }

        private void OnNewLog(string condition, string stackTrace, LogType type)
        {
            var newlineIndex = stackTrace.IndexOf("\n", StringComparison.Ordinal);
            var source = newlineIndex >= 0 ? stackTrace[..newlineIndex] : stackTrace;

            var item = new ConsoleItemData
            {
                LogType = type,
                Message = GetTimestamp() + condition + "\n" + source,
                Details = condition + "\n" + stackTrace
            };

            if (_logList.Count == LOG_BUFFER_CAPACITY)
            {
                _logList.RemoveAt(0);
            }

            _logList.Add(item);

            if (_listView == null) return;

            _listView.RefreshItems();

            SetTypeCounters();
            ScrollToButtom();
        }

        private void SetTypeCounters()
        {
            _showInfoLogsToggle.text = _logList.InfoLogCounter.ToString();
            _showWarningLogsToggle.text = _logList.WarningLogCounter.ToString();
            _showErrorLogsToggle.text = _logList.ErrorLogCounter.ToString();
        }

        private void ScrollToButtom()
        {
            if (_enableAutoScroll)
            {
                _listView.VerticalScroller.value = _listView.VerticalScroller.highValue;
            }
        }

        private string GetTimestamp()
        {
            return "[" + DateTime.Now.ToString("HH:mm:ss") + "] ";
        }
    }
}