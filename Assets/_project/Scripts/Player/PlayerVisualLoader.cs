using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace EclipseProtocol.Player
{
    [DisallowMultipleComponent]
    public class PlayerVisualLoader : MonoBehaviour
    {
        [SerializeField] private string robotResourcePath = "Player/low_poly_animated_robot";
        [SerializeField, Min(0.01f)] private float targetHeight = 2.4f;
        [SerializeField] private Vector3 localEulerAngles;
        [SerializeField] private bool faceMoveDirection = true;
        [SerializeField, Min(0f)] private float turnSpeed = 14f;
        [SerializeField] private string idleAnimationName = "standing";
        [SerializeField] private string walkingAnimationName = "walking";

        private GameObject _visualInstance;
        private PlayerController _playerController;
        private Animator _animator;
        private PlayableGraph _animationGraph;
        private AnimationClipPlayable _currentClipPlayable;
        private AnimationClip _idleClip;
        private AnimationClip _walkingClip;
        private string _currentAnimationName;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            LoadVisual();
        }

        private void Update()
        {
            if (_playerController == null || !_animationGraph.IsValid())
            {
                return;
            }

            bool shouldWalk = _playerController.HasMoveInput;
            AnimationClip targetClip = shouldWalk ? _walkingClip : _idleClip;
            string targetName = shouldWalk ? walkingAnimationName : idleAnimationName;
            PlayAnimation(targetClip, targetName);
            UpdateFacingDirection();
            RestartLoopIfNeeded();
        }

        private void OnDisable()
        {
            if (_animationGraph.IsValid())
            {
                _animationGraph.Stop();
            }
        }

        private void OnEnable()
        {
            if (_animationGraph.IsValid())
            {
                _animationGraph.Play();
            }
        }

        private void OnDestroy()
        {
            if (_animationGraph.IsValid())
            {
                _animationGraph.Destroy();
            }
        }

        private void LoadVisual()
        {
            if (_visualInstance != null)
            {
                return;
            }

            GameObject visualPrefab = Resources.Load<GameObject>(robotResourcePath);
            if (visualPrefab == null)
            {
                Debug.LogError($"[PlayerVisualLoader] Could not load robot visual at Resources/{robotResourcePath}.", this);
                return;
            }

            _visualInstance = Instantiate(visualPrefab, transform);
            _visualInstance.name = "RobotVisual";
            _visualInstance.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.Euler(localEulerAngles));
            _visualInstance.transform.localScale = Vector3.one;

            RemoveGameplayComponents(_visualInstance);
            ScaleAndGroundVisual();
            SetLayerRecursively(_visualInstance, gameObject.layer);
            SetupAnimation();
        }

        private void UpdateFacingDirection()
        {
            if (!faceMoveDirection || _visualInstance == null || !_playerController.HasMoveInput)
            {
                return;
            }

            Vector3 worldDirection = _playerController.FacingDirection;
            if (worldDirection.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Vector3 localDirection = transform.InverseTransformDirection(worldDirection.normalized);
            localDirection.y = 0f;
            if (localDirection.sqrMagnitude <= 0.001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(localDirection.normalized, Vector3.up)
                * Quaternion.Euler(localEulerAngles);
            _visualInstance.transform.localRotation = Quaternion.Slerp(
                _visualInstance.transform.localRotation,
                targetRotation,
                1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
        }

        private void ScaleAndGroundVisual()
        {
            Renderer[] renderers = _visualInstance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return;
            }

            Bounds bounds = CalculateBounds(renderers);
            float scale = targetHeight / Mathf.Max(bounds.size.y, 0.001f);
            _visualInstance.transform.localScale = Vector3.one * scale;

            renderers = _visualInstance.GetComponentsInChildren<Renderer>(true);
            bounds = CalculateBounds(renderers);

            Vector3 localMin = transform.InverseTransformPoint(bounds.min);
            _visualInstance.transform.localPosition += Vector3.up * (-1f - localMin.y);
        }

        private static Bounds CalculateBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        private static void RemoveGameplayComponents(GameObject root)
        {
            foreach (Collider visualCollider in root.GetComponentsInChildren<Collider>(true))
            {
                Destroy(visualCollider);
            }

            foreach (Rigidbody visualRigidbody in root.GetComponentsInChildren<Rigidbody>(true))
            {
                Destroy(visualRigidbody);
            }
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            root.layer = layer;
            foreach (Transform child in root.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }

        private void SetupAnimation()
        {
            _animator = _visualInstance.GetComponentInChildren<Animator>(true);
            if (_animator == null)
            {
                _animator = _visualInstance.AddComponent<Animator>();
            }

            AnimationClip[] clips = Resources.LoadAll<AnimationClip>(robotResourcePath);
            _idleClip = FindClip(clips, idleAnimationName);
            _walkingClip = FindClip(clips, walkingAnimationName);

            if (_idleClip == null && _walkingClip == null)
            {
                Debug.LogWarning($"[PlayerVisualLoader] No animation clips found at Resources/{robotResourcePath}.", this);
                return;
            }

            _animationGraph = PlayableGraph.Create("PlayerRobotAnimation");
            _animationGraph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_animationGraph, "RobotAnimation", _animator);
            output.SetSourcePlayable(Playable.Null);

            PlayAnimation(_idleClip != null ? _idleClip : _walkingClip, _idleClip != null ? idleAnimationName : walkingAnimationName);
            _animationGraph.Play();
        }

        private void PlayAnimation(AnimationClip clip, string animationName)
        {
            if (clip == null || _currentAnimationName == animationName || !_animationGraph.IsValid())
            {
                return;
            }

            if (_currentClipPlayable.IsValid())
            {
                _animationGraph.DestroyPlayable(_currentClipPlayable);
            }

            _currentClipPlayable = AnimationClipPlayable.Create(_animationGraph, clip);
            _currentClipPlayable.SetTime(0d);
            _currentClipPlayable.SetSpeed(1d);

            AnimationPlayableOutput output = (AnimationPlayableOutput)_animationGraph.GetOutput(0);
            output.SetSourcePlayable(_currentClipPlayable);
            _currentAnimationName = animationName;
        }

        private void RestartLoopIfNeeded()
        {
            if (!_currentClipPlayable.IsValid())
            {
                return;
            }

            AnimationClip clip = _currentClipPlayable.GetAnimationClip();
            if (clip != null && clip.length > 0f && _currentClipPlayable.GetTime() >= clip.length)
            {
                _currentClipPlayable.SetTime(0d);
            }
        }

        private static AnimationClip FindClip(AnimationClip[] clips, string clipName)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (string.Equals(clips[i].name, clipName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return clips[i];
                }
            }

            return null;
        }
    }
}
