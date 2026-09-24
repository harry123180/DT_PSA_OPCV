using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace OC.UI
{
    [DefaultExecutionOrder(-500)]
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        public int MouseSensitivity
        {
            get => _mouseSensitivity;
            set
            {
                _mouseSensitivity = Mathf.Clamp(value, 1, 10);
                OnSettingsChanged?.Invoke();
            } 
        }

        public bool AutoLoadSave
        {
            get => _autoLoadSave;
            set
            {
                if (_autoLoadSave == value) return;
                _autoLoadSave = value;
                OnSettingsChanged?.Invoke();
            }
        }
        
        public VisualConfig VisualConfig => _visualConfig;

        [Header("Interactions")]
        [SerializeField]
        private VisualConfig _visualConfig;

        [Header("Settings")]
        [SerializeField]
        private int _mouseSensitivity = 5;
        [SerializeField]
        private bool _autoLoadSave;

        private const string MouseSensitivityKey = "MouseSensitivity";
        private const string AutoLoadSaveKey = "AutoLoadSave";

        [Header("Collision")] 
        private bool _isWindowed;

        public UnityEvent OnSettingsChanged;
        
        private InputAction _actionWindow;
        
        
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
            } 
            else 
            {
                Instance = this;
            }
        }
        
        private void Start()
        {
            Load();
        }

        private void OnDisable()
        {
            Save();
        }
        
        private void OnValidate()
        {
            OnSettingsChanged?.Invoke();
        }
        
        private void Load()
        {
            if (PlayerPrefs.HasKey(MouseSensitivityKey))
            {
                _mouseSensitivity = PlayerPrefs.GetInt(MouseSensitivityKey);
            }

            _autoLoadSave = PlayerPrefs.GetInt(AutoLoadSaveKey, 0) == 1;
            OnSettingsChanged?.Invoke();
        }

        private void Save()
        {
            PlayerPrefs.SetInt(MouseSensitivityKey, _mouseSensitivity);
            PlayerPrefs.SetInt(AutoLoadSaveKey, _autoLoadSave ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
    
    public enum ViewModeType
    {
        Default,
        Transparent,
        Hide
    }

    public enum ViewModeState
    {
        Default,
        Warning,
        Error
    }
}
