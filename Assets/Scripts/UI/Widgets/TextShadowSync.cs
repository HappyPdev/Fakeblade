using TMPro;
using UnityEngine;

namespace FakeBlade.UI
{
    /// <summary>Copia el texto a su sombra cuando cambia (títulos con sombra pixel).</summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class TextShadowSync : MonoBehaviour
    {
        public TextMeshProUGUI Shadow;
        private TextMeshProUGUI _text;
        private string _last;

        private void Awake() => _text = GetComponent<TextMeshProUGUI>();

        private void LateUpdate()
        {
            if (Shadow == null || ReferenceEquals(_last, _text.text)) return;
            _last = _text.text;
            Shadow.text = _last;
            Shadow.fontSize = _text.fontSize;
        }
    }
}
