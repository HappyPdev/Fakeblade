using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FakeBlade.UI
{
    /// <summary>
    /// Adaptador de EventSystem para PixelOptionRow (menús de un usuario: teclado, mando y ratón).
    /// Arriba/abajo navega; izquierda/derecha cambia el valor; Submit/clic confirma.
    /// </summary>
    [RequireComponent(typeof(PixelOptionRow))]
    public class PixelRowSelectable : Selectable, ISubmitHandler, IPointerClickHandler
    {
        private PixelOptionRow _row;

        private PixelOptionRow Row => _row != null ? _row : (_row = GetComponent<PixelOptionRow>());

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
            navigation = new Navigation { mode = Navigation.Mode.Vertical };
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
            Row.SetFocused(true);
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);
            Row.SetFocused(false);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (!Row.IsButton && (eventData.moveDir == MoveDirection.Left || eventData.moveDir == MoveDirection.Right))
            {
                Row.Step(eventData.moveDir == MoveDirection.Left ? -1 : 1);
                eventData.Use();
                return;
            }
            base.OnMove(eventData);
        }

        public void OnSubmit(BaseEventData eventData) => Row.Submit();

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left) return;
            // Los clics en las flechas los gestiona PixelArrowClick
            if (eventData.pointerPress != null && eventData.pointerPress.GetComponent<PixelArrowClick>() != null) return;
            Row.Submit();
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            if (IsInteractable() && EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }
}
