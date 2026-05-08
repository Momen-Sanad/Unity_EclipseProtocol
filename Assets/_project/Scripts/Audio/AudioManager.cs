using UnityEngine;

namespace EclipseProtocol.Audio
{
    public sealed class AudioManager : MonoBehaviour
    {
        private const int SampleRate = 44100;

        [SerializeField, Range(0f, 1f)] private float sfxVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.18f;

        private static AudioManager _instance;
        private AudioSource _sfxSource;
        private AudioSource _musicSource;
        private AudioClip _dashClip;
        private AudioClip _pickupClip;
        private AudioClip _repairClip;
        private AudioClip _damageClip;
        private AudioClip _warningClip;
        private AudioClip _lungeClip;
        private AudioClip _lockedClip;
        private AudioClip _victoryClip;
        private AudioClip _lossClip;
        private AudioClip _ambientLoop;

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
                Destroy(gameObject);
                return;
            }

            _instance = this;
            if (Application.isPlaying)
            {
                DontDestroyOnLoad(gameObject);
            }

            EnsureReady();
        }

        private void Start()
        {
            PlayAmbientLoop();
        }

        public void PlayDash(Vector3 position)
        {
            PlayOneShot(_dashClip, position, 0.85f);
        }

        public void PlayPickup(Vector3 position)
        {
            PlayOneShot(_pickupClip, position, 0.8f);
        }

        public void PlayRepairComplete(Vector3 position)
        {
            PlayOneShot(_repairClip, position, 1f);
        }

        public void PlayDamage(Vector3 position)
        {
            PlayOneShot(_damageClip, position, 0.9f);
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
            PlayOneShot(_victoryClip, position, 1f);
        }

        public void PlayLoss(Vector3 position)
        {
            PlayOneShot(_lossClip, position, 1f);
        }

        private void PlayAmbientLoop()
        {
            EnsureReady();
            if (_musicSource == null || _musicSource.isPlaying)
            {
                return;
            }

            _musicSource.clip = _ambientLoop;
            _musicSource.loop = true;
            _musicSource.volume = musicVolume;
            _musicSource.Play();
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

            _dashClip ??= CreateTone("DashPulse", 620f, 0.12f, 0.35f);
            _pickupClip ??= CreateTone("EnergyPickup", 880f, 0.16f, 0.32f);
            _repairClip ??= CreateTone("RepairComplete", 520f, 0.32f, 0.36f, 780f);
            _damageClip ??= CreateTone("DamageHit", 145f, 0.22f, 0.42f);
            _warningClip ??= CreateTone("HunterWarning", 300f, 0.18f, 0.28f, 420f);
            _lungeClip ??= CreateTone("HunterLunge", 190f, 0.2f, 0.34f, 90f);
            _lockedClip ??= CreateTone("ExtractionLocked", 160f, 0.18f, 0.3f);
            _victoryClip ??= CreateTone("VictoryCue", 660f, 0.45f, 0.34f, 990f);
            _lossClip ??= CreateTone("LossCue", 220f, 0.5f, 0.36f, 110f);
            _ambientLoop ??= CreateTone("StationAmbient", 74f, 2.5f, 0.08f, 111f);
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
