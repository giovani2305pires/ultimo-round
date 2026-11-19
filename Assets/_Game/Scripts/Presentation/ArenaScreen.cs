using System.Collections.Generic;
using UnityEngine;

namespace UltimoRound.Presentation
{
    /// <summary>Telão e placas: textos atualizados pela luta.</summary>
    public sealed class ArenaScreen : MonoBehaviour
    {
        public TMPro.TMP_Text Title;
        public TMPro.TMP_Text Subtitle;

        public void Show(string title, string subtitle)
        {
            if (Title != null) Title.text = title;
            if (Subtitle != null) Subtitle.text = subtitle;
        }
    }
}
