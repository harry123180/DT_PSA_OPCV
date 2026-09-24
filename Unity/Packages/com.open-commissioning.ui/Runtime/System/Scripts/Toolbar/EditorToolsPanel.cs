using System;
using System.Collections.Generic;
using OC.Interactions;
using OC.UI.Interactions;
using OC.UI.TransformHandles;
using UnityEngine;
using UnityEngine.UIElements;

namespace OC.UI.Toolbar
{
    [RequireComponent(typeof(UIDocument))]
    [DisallowMultipleComponent]
    public class EditorToolsPanel : MonoBehaviourSingleton<EditorToolsPanel>
    {
        public bool Enable
        {
            get => _enable;
            set
            {
                if (_enable == value) return;
                SetEnable(value);
            }
        }

        [SerializeField]
        private Sprite _viewIcon;
        [SerializeField]
        private Sprite _moveIcon;
        [SerializeField]
        private Sprite _rotateIcon;
        [SerializeField]
        private Sprite _centerDefaultIcon;
        [SerializeField]
        private Sprite _centerActiveIcon;
        [SerializeField]
        private Sprite _globalDefaultIcon;
        [SerializeField]
        private Sprite _globalActiveIcon;

        private bool _enable;
        
        private Toggle _view;
        private Toggle _move;
        private Toggle _rotate;
        private Toggle _center;
        private Toggle _global;

        private List<Toggle> _selectionGroup;

        private VisualElement _toolbar;
        private const string UXML = "UXML/toolbar_editorTools";
        private const string STYLE_SHEET = "StyleSheet/toolbar";
        
        private void Start()
        {
            var uiDocument = GetComponent<UIDocument>();
            _toolbar = Resources.Load<VisualTreeAsset>(UXML).Instantiate().Q("toolbar");
            _toolbar.AddDefaultTheme();
            _toolbar.styleSheets.Add(Resources.Load<StyleSheet>(STYLE_SHEET));
            uiDocument.rootVisualElement.Add(_toolbar);

            _view = _toolbar.Q<Toggle>("view");
            _move = _toolbar.Q<Toggle>("move");
            _rotate = _toolbar.Q<Toggle>("rotate");
            _center = _toolbar.Q<Toggle>("center");
            _global = _toolbar.Q<Toggle>("global");

            _view.DefaultIcon = _viewIcon;
            _move.DefaultIcon = _moveIcon;
            _rotate.DefaultIcon = _rotateIcon;
            _center.DefaultIcon = _centerDefaultIcon;
            _center.ActiveIcon = _centerActiveIcon;
            _global.DefaultIcon = _globalDefaultIcon;
            _global.ActiveIcon = _globalActiveIcon;

            _view.RegisterCallback<ChangeEvent<bool>>(_ => RuntimeTransformHandle.Instance.Tool.Value = ToolType.View);
            _move.RegisterCallback<ChangeEvent<bool>>(_ => RuntimeTransformHandle.Instance.Tool.Value = ToolType.Move);
            _rotate.RegisterCallback<ChangeEvent<bool>>(_ => RuntimeTransformHandle.Instance.Tool.Value= ToolType.Rotation);
            _center.RegisterCallback<ChangeEvent<bool>>(evt => SetHandlePosition(evt.newValue));
            _global.RegisterCallback<ChangeEvent<bool>>(evt => SetHandleRotation(evt.newValue));
            
            RuntimeTransformHandle.Instance.Tool.Subscribe(SetTool);
            RuntimeTransformHandle.Instance.Pivot.Subscribe(SetHandlePosition);
            RuntimeTransformHandle.Instance.Coordinate.Subscribe(SetHandleRotation);
            SelectionManager.Instance.OnSelectionChanged += OnSelectionChanged;

            SetEnable(false);
        }
        
        private void OnDestroy()
        {
            RuntimeTransformHandle.Instance.Tool.Unsubscribe(SetTool);
            RuntimeTransformHandle.Instance.Pivot.Unsubscribe(SetHandlePosition);
            RuntimeTransformHandle.Instance.Coordinate.Unsubscribe(SetHandleRotation);
            SelectionManager.Instance.OnSelectionChanged -= OnSelectionChanged;
        }
        
        private void OnSelectionChanged(List<Interaction> interactions)
        {
            SetEnable(interactions.Count > 0);
        }

        private void SetEnable(bool value)
        {
            _enable = value;
            _toolbar.style.display = _enable
                ? new StyleEnum<DisplayStyle>(DisplayStyle.Flex)
                : new StyleEnum<DisplayStyle>(DisplayStyle.None);
            if (!_enable) RuntimeTransformHandle.Instance.Tool.Value = ToolType.View;
        }

        private void SetHandlePosition(bool enable)
        {
            RuntimeTransformHandle.Instance.Pivot.Value = enable ? PivotMode.Center : PivotMode.Pivot;
        }
        
        private void SetHandleRotation(bool enable)
        {
            RuntimeTransformHandle.Instance.Coordinate.Value = enable ? CoordinateSpace.World : CoordinateSpace.Local;
        }

        private void SetHandlePosition(PivotMode pivotMode)
        {
            _center.SetValueWithoutNotify(pivotMode == PivotMode.Center);
        }
        
        private void SetHandleRotation(CoordinateSpace coordinateSpace)
        {
            _global.SetValueWithoutNotify(coordinateSpace == CoordinateSpace.World);
        }

        private void SetTool(ToolType toolType)
        {
            _view.SetValueWithoutNotify(false);
            _move.SetValueWithoutNotify(false);
            _rotate.SetValueWithoutNotify(false);
            
            switch (toolType)
            {
                case ToolType.View:
                    _view.SetValueWithoutNotify(true);
                    break;
                case ToolType.Move:
                    _move.SetValueWithoutNotify(true);
                    break;
                case ToolType.Rotation:
                    _rotate.SetValueWithoutNotify(true);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(toolType), toolType, null);
            }
        }
    }
}