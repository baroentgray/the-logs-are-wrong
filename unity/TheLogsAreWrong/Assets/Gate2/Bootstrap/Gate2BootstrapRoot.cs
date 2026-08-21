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

        /// <summary>Marker proving the one production owner reached running startup before bootstrap smoke continues.</summary>
        public const string OwnerStartupMarker = "TLAW071_BOOTSTRAP_OWNER_RUNNING";

        /// <summary>Marker proving bootstrap refuses to proceed if its required production owner did not start.</summary>
        public const string OwnerStartupFailureMarker = "TLAW071_BOOTSTRAP_OWNER_NOT_RUNNING";

        [Tooltip("Frames to run before quitting when the bootstrap smoke argument is supplied.")]
        [SerializeField]
        private int _smokeFrames = 60;

        private bool _smokeMode;
        private int _frames;

        private void Start()
        {
            _smokeMode = HasSmokeArgument();
            Debug.Log($"{StartedMarker} scene={gameObject.scene.name} smokeMode={_smokeMode} unity={Application.unityVersion}");

            var owner = GetComponent<Gate2ProductionHostDriver>();
            if (owner == null || owner.Lifecycle != ProductionHostOwnerLifecycle.Running)
            {
                Debug.LogError(OwnerStartupFailureMarker);
                if (_smokeMode)
                {
                    Application.Quit(2);
                }

                return;
            }

            Debug.Log(OwnerStartupMarker + " shift=" + owner.RunningShiftId + " profile=" + owner.SelectedProfileId);
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
