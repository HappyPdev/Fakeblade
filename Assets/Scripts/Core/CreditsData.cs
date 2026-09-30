using System;
using UnityEngine;

namespace FakeBlade.Core
{
    /// <summary>Créditos editables (GDD 9.2.5): nombre del autor y enlaces a redes.</summary>
    [CreateAssetMenu(fileName = "Credits", menuName = "FakeBlade/Credits")]
    public class CreditsData : ScriptableObject
    {
        [Serializable]
        public class Link
        {
            public string label = "Web";
            public string url = "https://";
        }

        public string gameTitle = "FAKEBLADE";
        public string author = "Tu nombre";
        [TextArea(2, 6)] public string extraLines = "Hecho con Unity";
        public Link[] links =
        {
            new Link { label = "Twitter / X", url = "https://x.com/" },
            new Link { label = "Instagram", url = "https://instagram.com/" },
            new Link { label = "itch.io", url = "https://itch.io/" }
        };
    }
}
