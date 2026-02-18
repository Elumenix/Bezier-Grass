using System;
using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

public class Autoplay : MonoBehaviour
{
    public CinemachineCamera cinemachineCamera;
    public float duration = 5f;
    private bool dollyStarted = false;

    private CinemachineSplineDolly _dolly;

    void Start()
    {
        _dolly = cinemachineCamera.GetComponent<CinemachineSplineDolly>();
        _dolly.CameraPosition = 0f;
    }

    private void Update()
    {
        if (!dollyStarted && Time.time >= 4)
        {
            dollyStarted = true;
            PlayDolly();
        }
    }

    public void PlayDolly()
    {
        StartCoroutine(PlayDollyOverTime(duration));
    }

    private IEnumerator PlayDollyOverTime(float duration)
    {
        float startPosition = _dolly.CameraPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            _dolly.CameraPosition = Mathf.Lerp(startPosition, 1f, t);
            yield return null;
        }

        _dolly.CameraPosition = 1f;
    }
}