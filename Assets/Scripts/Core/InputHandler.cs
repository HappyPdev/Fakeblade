using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Maneja el input para un jugador específico.
    /// Soporta teclado (Player 1) y hasta 4 gamepads.
    /// </summary>
    public class InputHandler : MonoBehaviour
    {
        #region Constants
        private const float DEADZONE = 0.2f;
        #endregion

        #region Serialized Fields
        [Header("Input Configuration")]
        [SerializeField] private int playerIndex = 0;
        [SerializeField] private bool useKeyboard = false;
        [SerializeField] private bool invertVertical = false;

        [Header("Sensitivity")]
        [SerializeField] private float sensitivity = 1f;

        [Header("Debug")]
        [SerializeField] private bool showInputDebug = false;
        #endregion

        #region Private Fields
        private string horizontalAxis;
        private string verticalAxis;
        private string dashButton;
        private string specialButton;
        private bool isConfigured = false;
        #endregion

        #region Properties
        public int PlayerIndex => playerIndex;
        #endregion

        #region Unity Lifecycle
        private void Start()
        {
            ConfigureInput();
        }
        #endregion

        #region Configuration
        private void ConfigureInput()
        {
            if (useKeyboard && playerIndex == 0)
            {
                ConfigureKeyboardInput();
            }
            else
            {
                ConfigureGamepadInput();
            }

            isConfigured = true;

            if (showInputDebug)
            {
                Debug.Log($"[InputHandler] Configured for Player {playerIndex + 1} - " +
                         $"Type: {(useKeyboard ? "Keyboard" : "Gamepad")}");
            }
        }

        private void ConfigureKeyboardInput()
        {
            horizontalAxis = "Horizontal";
            verticalAxis = "Vertical";
            dashButton = "Jump";
            specialButton = "Fire1";
        }

        private void ConfigureGamepadInput()
        {
            // Nota: Estos ejes deben estar configurados en Input Manager
            horizontalAxis = $"Horizontal_P{playerIndex + 1}";
            verticalAxis = $"Vertical_P{playerIndex + 1}";
            dashButton = $"Dash_P{playerIndex + 1}";
            specialButton = $"Special_P{playerIndex + 1}";
        }
        #endregion

        #region Input Reading
        public Vector2 GetMovementInput()
        {
            if (!isConfigured) return Vector2.zero;

            float horizontal = 0f;
            float vertical = 0f;

            try
            {
                horizontal = Input.GetAxis(horizontalAxis);
                vertical = Input.GetAxis(verticalAxis);
            }
            catch (System.ArgumentException)
            {
                if (showInputDebug)
                {
                    Debug.LogWarning($"[InputHandler] Axis not configured in Input Manager: {horizontalAxis} or {verticalAxis}");
                }
                return Vector2.zero;
            }

            // Aplicar deadzone
            if (Mathf.Abs(horizontal) < DEADZONE)
                horizontal = 0f;

            if (Mathf.Abs(vertical) < DEADZONE)
                vertical = 0f;

            // Invertir vertical si está configurado
            if (invertVertical)
                vertical = -vertical;

            Vector2 input = new Vector2(horizontal, vertical);

            // Aplicar sensibilidad
            input *= sensitivity;

            // Normalizar si excede magnitud 1
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            return input;
        }

        public bool GetDashInput()
        {
            if (!isConfigured) return false;

            try
            {
                return Input.GetButtonDown(dashButton);
            }
            catch (System.ArgumentException)
            {
                if (showInputDebug)
                {
                    Debug.LogWarning($"[InputHandler] Button not configured: {dashButton}");
                }
                return false;
            }
        }

        public bool GetSpecialInput()
        {
            if (!isConfigured) return false;

            try
            {
                return Input.GetButtonDown(specialButton);
            }
            catch (System.ArgumentException)
            {
                if (showInputDebug)
                {
                    Debug.LogWarning($"[InputHandler] Button not configured: {specialButton}");
                }
                return false;
            }
        }
        #endregion

        #region Public Methods
        public void SetPlayerIndex(int index)
        {
            if (index < 0 || index > 3)
            {
                Debug.LogError($"[InputHandler] Invalid player index: {index}. Must be 0-3.");
                return;
            }

            playerIndex = index;
            ConfigureInput();
        }

        public void SetUseKeyboard(bool useKb)
        {
            useKeyboard = useKb;
            ConfigureInput();
        }

        public void SetSensitivity(float newSensitivity)
        {
            sensitivity = Mathf.Clamp(newSensitivity, 0.1f, 2f);
        }
        #endregion
    }
}