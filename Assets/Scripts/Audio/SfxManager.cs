using UnityEngine;

namespace Shikaku.UI
{
    public class SfxManager : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private AudioSource sfxSource;

        [Header("Clips")]
        [SerializeField] private AudioClip drawClip;
        [SerializeField] private AudioClip eraseClip;
        [SerializeField] private AudioClip buttonClip;
        [SerializeField] private AudioClip solvedClip;

        [Header("Draw pitch ramp (drag)")]
        [SerializeField] private float tapPitch = 1.0f;
        [SerializeField] private float dragPitchStart = 1.0f;
        [SerializeField] private float dragPitchStep = 0.04f;
        [SerializeField] private float dragPitchMax = 1.35f;
        [SerializeField] private float jitter = 0.01f;

        [Header("Spam control")]
        [SerializeField] private float minInterval = 0.03f;

        private int _dragCount = 0;
        private float _nextAllowedTime = 0f;

        private void Reset()
        {
            sfxSource = GetComponent<AudioSource>();
        }

        public void BeginStroke()
        {
            _dragCount = 0;
            _nextAllowedTime = 0f;
        }

        public void PlayDraw(bool isDrag)
        {
            if (drawClip == null || sfxSource == null) return;

            if (Time.unscaledTime < _nextAllowedTime) return;
            _nextAllowedTime = Time.unscaledTime + minInterval;

            float basePitch = isDrag
                ? Mathf.Min(dragPitchStart + _dragCount * dragPitchStep, dragPitchMax)
                : tapPitch;

            float pitch = basePitch + Random.Range(-jitter, jitter);

            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(drawClip);

            if (isDrag) _dragCount++;
        }

        public void PlayErase()
        {
            if (eraseClip == null || sfxSource == null) return;
            sfxSource.pitch = 1.0f;
            sfxSource.PlayOneShot(eraseClip);
        }

        public void PlayButton()
        {
            if (buttonClip == null || sfxSource == null) return;
            sfxSource.pitch = 1.0f;
            sfxSource.PlayOneShot(buttonClip);
        }

        public void PlaySolved()
        {
            if (solvedClip == null || sfxSource == null) return;
            sfxSource.pitch = 1.0f;
            sfxSource.PlayOneShot(solvedClip);
        }
    }
}

