using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FakeBlade.UI
{
    /// <summary>
    /// El mando y el teclado nunca pierden el foco de un menú (N1, GDD 9.2): si la selección del EventSystem se queda
    /// vacía o en algo oculto o desactivado (un clic con el ratón fuera del panel, una fila que se oculta), vuelve a la
    /// última fila válida mientras siga visible. Si ya no lo está (se ha cerrado su menú), no hace nada: cada menú
    /// elige su fila al abrirse. Va en el mismo objeto que el EventSystem.
    /// </summary>
    [RequireComponent(typeof(EventSystem))]
    public class MenuFocusGuard : MonoBehaviour
    {
        private EventSystem _events;
        private GameObject _last;
        private Selectable _lastSelectable;

        private void Awake() => _events = GetComponent<EventSystem>();

        private void LateUpdate()
        {
            GameObject current = _events.currentSelectedGameObject;
            if (current != null && current != _last && current.activeInHierarchy)
            {
                var selectable = current.GetComponent<Selectable>();
                if (selectable != null && selectable.IsInteractable())
                {
                    _last = current;
                    _lastSelectable = selectable;
                    return;
                }
            }
            if (current != null && current == _last && IsValid()) return;

            // Selección perdida: vuelve a la última fila si su menú sigue abierto
            if (_last != null && IsValid()) _events.SetSelectedGameObject(_last);
        }

        private bool IsValid() => _last.activeInHierarchy && _lastSelectable != null && _lastSelectable.IsInteractable();
    }
}
