using UnityEngine;
using UnityEngine.EventSystems;

namespace FakeBlade.UI
{
    /// <summary>Clic de ratón en las flechas < > de una fila.</summary>
    public class PixelArrowClick : MonoBehaviour, IPointerClickHandler
    {
        public PixelOptionRow Row;
        public int Direction;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && Row != null)
                Row.Step(Direction);
        }
    }

}
