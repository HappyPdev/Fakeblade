using System;
using FakeBlade.Core;
using TMPro;
using UnityEngine;

namespace FakeBlade.UI
{
    /// <summary>Información (GDD 9.2.5): créditos y enlaces del asset CreditsData.</summary>
    public class CreditsScreen : MenuScreen
    {
        private CreditsData _credits;

        public static CreditsScreen Create(Transform parent, HUDTheme theme, CreditsData credits, Action onClose)
        {
            var screen = CreateScreen<CreditsScreen>("CreditsScreen", parent);
            screen._credits = credits;
            screen.Initialize(theme, "INFO", 130, onClose);
            return screen;
        }

        protected override void BuildContent()
        {
            if (_credits == null)
            {
                List.AddText("INFO_EMPTY", 5, Theme.textDimColor);
            }
            else
            {
                var title = List.AddText(null, 8, Theme.chargeReady, TextAlignmentOptions.Center, 12);
                title.text = _credits.gameTitle;

                List.AddText("INFO_CREATED_BY", 4, Theme.textDimColor, TextAlignmentOptions.Center, 7);
                var author = List.AddText(null, 6, Theme.textColor, TextAlignmentOptions.Center, 10);
                author.text = _credits.author;

                if (!string.IsNullOrEmpty(_credits.extraLines))
                {
                    var extra = List.AddText(null, 4, Theme.textDimColor, TextAlignmentOptions.Center, 12);
                    extra.text = _credits.extraLines;
                }

                List.AddSpacer(2);
                if (_credits.links != null)
                {
                    foreach (var link in _credits.links)
                    {
                        if (link == null || string.IsNullOrEmpty(link.url)) continue;
                        string url = link.url;
                        var row = List.AddButton(null, () => Application.OpenURL(url), 9);
                        row.SetLabel(link.label);
                    }
                }
            }

            List.AddSpacer(2);
            List.AddButton("BACK", Close, 9);
        }
    }
}
