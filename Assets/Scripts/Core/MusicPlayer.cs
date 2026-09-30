using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>
    /// Reproductor de música que respeta el volumen de música de Opciones.
    /// Añadir a un GameObject con AudioSource (la música de cada escena).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class MusicPlayer : MonoBehaviour
    {
        [Range(0f, 1f)]
        [SerializeField] private float baseVolume = 1f;

        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            ApplyVolume();
        }

        private void OnEnable() => SettingsService.OnApplied += ApplyVolume;
        private void OnDisable() => SettingsService.OnApplied -= ApplyVolume;

        private void ApplyVolume()
        {
            if (_source != null) _source.volume = baseVolume * SettingsService.MusicVolume;
        }
    }
}
