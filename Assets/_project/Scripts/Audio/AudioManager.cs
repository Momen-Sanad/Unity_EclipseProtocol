using UnityEngine;
using UnityEngine.SceneManagement;

namespace EclipseProtocol.Audio
{
    public sealed class AudioManager : MonoBehaviour
    {
        private const int SampleRate = 44100;

        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.18f;
        [SerializeField, Min(0.01f)] private float musicFadeSeconds = 1.25f;

        [Header("Music")]
        [SerializeField] private AudioClip startMenuMusicClip;
        [SerializeField] private AudioClip victoryMusicClip;
        [SerializeField] private AudioClip lossMusicClip;

        [Header("SFX")]
        [SerializeField] private AudioClip dashClip;
        [SerializeField] private AudioClip pickupClip;
        [SerializeField] private AudioClip damageClip;
        [SerializeField] private AudioClip footstepsClip;

        private static AudioManager _instance;
        private AudioSource _sfxSource;
        private AudioSource _musicSource;
        private AudioSource _footstepsSource;
        private AudioClip _repairClip;
        private AudioClip _warningClip;
        private AudioClip _lungeClip;
        private AudioClip _lockedClip;
        private Coroutine _musicFadeRoutine;

        public static AudioManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<AudioManager>();
                }

                if (_instance == null && Application.isPlaying)
                {
                    GameObject audioObject = new GameObject("AudioManager");
                    _instance = audioObject.AddComponent<AudioManager>();
                }

                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(this);
                return;
            }

            _instance = this;
            if (Application.isPlaying && CanPersistAcrossScenes())
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsureReady();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Start()
        {
            ApplySceneAudio(SceneManager.GetActiveScene().name);
        }

        public void PlayDash(Vector3 position)
        {
            PlayOneShot(dashClip, position, 0.85f);
        }

        public void PlayPickup(Vector3 position)
        {
            PlayOneShot(pickupClip, position, 0.8f);
        }

        public void PlayRepairComplete(Vector3 position)
        {
            PlayOneShot(_repairClip, position, 1f);
        }

        public void PlayDamage(Vector3 position)
        {
            PlayOneShot(damageClip, position, 0.9f);
        }

        public void PlayWarning(Vector3 position)
        {
            PlayOneShot(_warningClip, position, 0.8f);
        }

        public void PlayLunge(Vector3 position)
        {
            PlayOneShot(_lungeClip, position, 0.9f);
        }

        public void PlayLocked(Vector3 position)
        {
            PlayOneShot(_lockedClip, position, 0.75f);
        }

        public void PlayVictory(Vector3 position)
        {
            PlayMusic(victoryMusicClip, false, 0f);
        }

        public void PlayLoss(Vector3 position)
        {
            PlayMusic(lossMusicClip, false, 0f);
        }

        public void SetFootstepsMoving(bool isMoving, Vector3 position)
        {
            EnsureReady();
            if (_footstepsSource == null || footstepsClip == null)
            {
                return;
            }

            _footstepsSource.transform.position = position;
            if (isMoving)
            {
                if (!_footstepsSource.isPlaying)
                {
                    _footstepsSource.clip = footstepsClip;
                    _footstepsSource.loop = true;
                    _footstepsSource.volume = sfxVolume;
                    _footstepsSource.Play();
                }

                return;
            }

            if (_footstepsSource.isPlaying)
            {
                _footstepsSource.Stop();
            }
        }

        private void PlayOneShot(AudioClip clip, Vector3 position, float volumeScale)
        {
            EnsureReady();
            if (clip == null || _sfxSource == null)
            {
                return;
            }

            _sfxSource.transform.position = position;
            _sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ApplySceneAudio(scene.name);
        }

        private void ApplySceneAudio(string sceneName)
        {
            EnsureReady();
            SetFootstepsMoving(false, Vector3.zero);

            switch (sceneName)
            {
                case "Start_Screen":
                case "Menu":
                case "Difficulity":
                    PlayMusic(startMenuMusicClip, true, 0f);
                    break;
                case "Gameplay":
                    FadeOutMusic();
                    break;
                case "Victory":
                    PlayMusic(victoryMusicClip, false, 0f);
                    break;
                case "Loss":
                    PlayMusic(lossMusicClip, false, 0f);
                    break;
            }
        }

        private void PlayMusic(AudioClip clip, bool loop, float fadeSeconds)
        {
            EnsureReady();
            if (_musicSource == null || clip == null)
            {
                return;
            }

            if (_musicFadeRoutine != null)
            {
                StopCoroutine(_musicFadeRoutine);
                _musicFadeRoutine = null;
            }

            if (_musicSource.clip == clip && _musicSource.isPlaying)
            {
                _musicSource.loop = loop;
                _musicSource.volume = musicVolume;
                return;
            }

            _musicSource.clip = clip;
            _musicSource.loop = loop;
            _musicSource.volume = fadeSeconds <= 0f ? musicVolume : 0f;
            _musicSource.Play();

            if (fadeSeconds > 0f)
            {
                _musicFadeRoutine = StartCoroutine(FadeMusicVolume(musicVolume, fadeSeconds, false));
            }
        }

        private void FadeOutMusic()
        {
            EnsureReady();
            if (_musicSource == null || !_musicSource.isPlaying)
            {
                return;
            }

            if (_musicFadeRoutine != null)
            {
                StopCoroutine(_musicFadeRoutine);
            }

            _musicFadeRoutine = StartCoroutine(FadeMusicVolume(0f, musicFadeSeconds, true));
        }

        private System.Collections.IEnumerator FadeMusicVolume(float targetVolume, float duration, bool stopWhenDone)
        {
            float startVolume = _musicSource.volume;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _musicSource.volume = Mathf.Lerp(startVolume, targetVolume, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            _musicSource.volume = targetVolume;
            if (stopWhenDone)
            {
                _musicSource.Stop();
                _musicSource.clip = null;
            }

            _musicFadeRoutine = null;
        }

        private void EnsureReady()
        {
            if (_sfxSource == null)
            {
                _sfxSource = gameObject.AddComponent<AudioSource>();
                _sfxSource.playOnAwake = false;
                _sfxSource.spatialBlend = 0.35f;
                _sfxSource.rolloffMode = AudioRolloffMode.Linear;
                _sfxSource.maxDistance = 24f;
            }

            if (_musicSource == null)
            {
                _musicSource = gameObject.AddComponent<AudioSource>();
                _musicSource.playOnAwake = false;
                _musicSource.spatialBlend = 0f;
            }

            if (_footstepsSource == null)
            {
                _footstepsSource = gameObject.AddComponent<AudioSource>();
                _footstepsSource.playOnAwake = false;
                _footstepsSource.spatialBlend = 0.45f;
                _footstepsSource.rolloffMode = AudioRolloffMode.Linear;
                _footstepsSource.maxDistance = 18f;
            }

            dashClip ??= CreateTone("DashPulse", 620f, 0.12f, 0.35f);
            pickupClip ??= CreateTone("EnergyPickup", 880f, 0.16f, 0.32f);
            _repairClip ??= CreateTone("RepairComplete", 520f, 0.32f, 0.36f, 780f);
            damageClip ??= CreateTone("DamageHit", 145f, 0.22f, 0.42f);
            _warningClip ??= CreateTone("HunterWarning", 300f, 0.18f, 0.28f, 420f);
            _lungeClip ??= CreateTone("HunterLunge", 190f, 0.2f, 0.34f, 90f);
            _lockedClip ??= CreateTone("ExtractionLocked", 160f, 0.18f, 0.3f);
        }

        private bool CanPersistAcrossScenes()
        {
            Component[] components = GetComponents<Component>();
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null
                    || component is Transform
                    || component is AudioManager
                    || component is AudioSource)
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static AudioClip CreateTone(string clipName, float frequency, float durationSeconds, float amplitude, float secondFrequency = 0f)
        {
            int sampleCount = Mathf.Max(1, Mathf.CeilToInt(SampleRate * durationSeconds));
            float[] samples = new float[sampleCount];
            float fadeSamples = Mathf.Max(1f, SampleRate * 0.02f);

            for (int i = 0; i < sampleCount; i++)
            {
                float t = i / (float)SampleRate;
                float wave = Mathf.Sin(Mathf.PI * 2f * frequency * t);
                if (secondFrequency > 0f)
                {
                    wave = (wave + Mathf.Sin(Mathf.PI * 2f * secondFrequency * t)) * 0.5f;
                }

                float attack = Mathf.Clamp01(i / fadeSamples);
                float release = Mathf.Clamp01((sampleCount - i) / fadeSamples);
                samples[i] = wave * amplitude * Mathf.Min(attack, release);
            }

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
