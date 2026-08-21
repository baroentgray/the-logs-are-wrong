using System;
using UnityEngine;

namespace TheLogsAreWrong.Gate2
{
    /// <summary>
    /// Exact raw deployment text imported from a tracked C1 artifact or manifest. It carries no configuration
    /// semantics: PortableAuthority remains solely responsible for parsing the manifest and materializing C1.
    /// </summary>
    public sealed class Gate2DeploymentTextAsset : ScriptableObject
    {
        [SerializeField]
        [TextArea]
        private string _text;

        public string Text => _text;

        public void SetImportedText(string text)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
        }
    }
}
