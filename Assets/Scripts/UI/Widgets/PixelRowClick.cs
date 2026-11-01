using UnityEngine;
using UnityEngine.EventSystems;

namespace FakeBlade.UI
{
    /// <summary>
    /// Clic de ratón en una fila que se maneja a mano (columnas de la selección, una por mando): la confirma y la
    /// resalta al pasar por encima. No es un Selectable, así que el EventSystem nunca la selecciona y el Confirmar
    /// del teclado o del mando no la pulsa por sorpresa. Los clics en las flechas los gestiona PixelArrowClick.
    /// </summary>
    [RequireComponent(typeof(PixelOptionRow))]
    public class PixelRowClick : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private PixelOptionRow _row;
        private bool _hover;

        /// <summary>Resaltar al pasar el ratón (en las columnas el foco lo lleva su jugador: ahí no).</summary>
        public bool Highlight = true;

        private PixelOptionRow Row => _row != null ? _row : (_row = GetComponent<PixelOptionRow>());

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            if (eventData.pointerPress != null && eventData.pointerPress.GetComponent<PixelArrowClick>() != null) return;
            Row.Submit();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!Highlight) return;
            _hover = true;
            Row.SetFocused(true);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_hover) return;
            _hover = false;
            Row.SetFocused(false);
        }

        private void OnDisable()
        {
            if (!_hover) return;
            _hover = false;
            Row.SetFocused(false);
        }
    }
}
