using UnityEngine;

namespace TheLogsAreWrong.Gate2
{
    /// <summary>
    /// Minimal Gate-2 bootstrap marker. It proves the bootstrap scene loads and runs, and it lets the
    /// Windows build smoke exit cleanly on its own instead of being killed.
    /// <para>
    /// This is deliberately not gameplay: it holds no simulation state, reads no input, and touches no
    /// Domain, networking, or presentation contract. The Domain-to-Unity integration strategy is a
    /// separate bounded increment.
    /// </para>
    /// </summary>
    public sealed class Gate2BootstrapRoot : MonoBehaviour
    {
        /// <summary>Command-line switch that makes the built player quit after a short bootstrap run.</summary>
        public const string SmokeArgument = "-tlaw-bootstrap-smoke";

        /// <summary>Marker written to the player log so the launch smoke can assert startup happened.</summary>
        public const string StartedMarker = "TLAW052_BOOTSTRAP_STARTED";

        /// <summary>Marker written immediately before a smoke-mode quit.</summary>
        public const string QuitMarker = "TLAW052_BOOTSTRAP_QUIT";

        [Tooltip("Frames to run before quitting when the bootstrap smoke argument is supplied.")]
        [SerializeField]
        private int _smokeFrames = 60;

        private bool _smokeMode;
        private int _frames;

        private void Start()
        {
            _smokeMode = HasSmokeArgument();
            Debug.Log($"{StartedMarker} scene={gameObject.scene.name} smokeMode={_smokeMode} unity={Application.unityVersion}");
        }

        private void Update()
        {
            if (!_smokeMode)
            {
                return;
            }

            _frames++;
            if (_frames < _smokeFrames)
            {
                return;
            }

            Debug.Log($"{QuitMarker} frames={_frames}");
            Application.Quit(0);
        }

        private static bool HasSmokeArgument()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], SmokeArgument, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
