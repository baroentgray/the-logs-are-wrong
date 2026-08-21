using System.IO;
using UnityEditor.AssetImporters;
using UnityEngine;

namespace TheLogsAreWrong.Gate2.EditorTools
{
    /// <summary>
    /// Imports the existing tracked C1 transport files as Unity data without changing their bytes, format, or
    /// validation semantics. The runtime receives the exact text and delegates all interpretation to PortableAuthority.
    /// </summary>
    [ScriptedImporter(1, new[] { "base64" }, new[] { "manifest" })]
    public sealed class Gate2DeploymentTextImporter : ScriptedImporter
    {
        public override void OnImportAsset(AssetImportContext context)
        {
            var text = ScriptableObject.CreateInstance<Gate2DeploymentTextAsset>();
            text.SetImportedText(File.ReadAllText(context.assetPath));
            context.AddObjectToAsset("deployment-text", text);
            context.SetMainObject(text);
        }
    }
}
