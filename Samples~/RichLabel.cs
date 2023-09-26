﻿using UnityEngine;

namespace ExtInspector.Samples
{
    public class RichLabel: MonoBehaviour
    {
        [Standalone.RichLabel("prefix:<color=red>some <color=\"green\"><b>[<color=yellow><icon='eye-regular.png' /></color><label /></b>]</color>:su<color='yellow'>ff</color>ix</color>")]
        public string richLabel;

        // public string GetRichLabel()
        // {
        //     return "<color=red>RichLabel</color>";
        // }
    }
}
