using UnityEngine;
using NativeWebSocket;
using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections;

public class MurfTTSStream : MonoBehaviour
{
    // --- Singleton instance ---
    public static MurfTTSStream Instance { get; private set; }

    [Header("Murf API Settings")]
    public string apiKey = "";  // set in Inspector
    public string voiceId = "en-US-ken";
    public string style = "Wizard";
    public int pitch = -35;

    private WebSocket websocket;
    private AudioSource audioSource;
    private List<float> audioBuffer = new List<float>();

    // Standard audio sample rate. 48000 is typical for TTS services.
    private const int sampleRate = 48000;

    // Minimum number of samples before we create and play an AudioClip.
    private const int minPlaySamples = 2048;

    // If true, we are currently playing a chunk
    private bool isPlayingChunk = false;

    // track coroutine so we can cancel when necessary
    private Coroutine waitForEndCoroutine;

    [Serializable] public class VoiceConfig { public string voice_id; public string style; public int pitch; }
    [Serializable] public class VoiceConfigMessage { public VoiceConfig voice_config; }
    [Serializable] public class TTSRequest { public string context_id; public string text; public bool end = true; }
    [Serializable] public class AdvancedSettings { public int min_buffer_size; public int max_buffer_delay_in_ms; }
    [Serializable] public class AdvancedSettingsMessage { public AdvancedSettings setAdvancedSettings; }
    [Serializable] public class ClearContext { public string context_id; public bool clear = true; }
    [Serializable] public class ClearContextMessage { public ClearContext clearContext; }
    [Serializable] private class MurfAudioMessage { public string type; public string audio; public string context_id; public bool final; }

    private void Awake()
    {
        // Simple singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        if (!audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Recommended AudioSource setup
        audioSource.spatialBlend = 0f; // 2D
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    private async void Start()
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning("[MurfTTS] apiKey is empty — set your key in the Inspector.");
        }

        string url = $"wss://api.murf.ai/v1/speech/stream-input?api_key={apiKey}";
        websocket = new WebSocket(url);

        websocket.OnOpen += async () =>
        {
            Debug.Log("[MurfTTS] Connected to Murf streaming API");

            var voiceConfigMsg = new VoiceConfigMessage()
            {
                voice_config = new VoiceConfig()
                {
                    voice_id = voiceId,
                    style = style,
                    pitch = pitch
                }
            };

            try
            {
                await SendJson(voiceConfigMsg);
                Debug.Log("[MurfTTS] Sent voice_config");
            }
            catch (Exception e)
            {
                Debug.LogError("[MurfTTS] Error sending voice config: " + e.Message);
            }
        };

        websocket.OnError += (e) => Debug.LogError("[MurfTTS] WebSocket Error: " + e);
        websocket.OnClose += (e) => Debug.LogWarning("[MurfTTS] WebSocket Closed");
        websocket.OnMessage += OnMessageReceived;

        try
        {
            await websocket.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogError("[MurfTTS] WebSocket connect failed: " + ex.Message);
        }
    }

    private void Update()
    {
        websocket?.DispatchMessageQueue();

        // If audioSource not playing and we have enough buffered audio, start playback
        if (!audioSource.isPlaying && audioBuffer.Count >= minPlaySamples && !isPlayingChunk)
        {
            PlayBufferedAudioImmediate();
        }
    }

    private async void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (websocket != null)
        {
            try
            {
                await websocket.Close();
                websocket = null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MurfTTS] Error during WebSocket close: " + e.Message);
            }
        }
    }

    private async Task SendJson(object obj)
    {
        try
        {
            string json = JsonUtility.ToJson(obj);
            await websocket.SendText(json);
            Debug.Log("[MurfTTS] Sent JSON");
        }
        catch (Exception e)
        {
            Debug.LogError("[MurfTTS] SendJson error: " + e.Message);
        }
    }

    /// <summary>
    /// Send a text to speak. ContextId is useful to group/interrupt streams.
    /// </summary>
    public async void SendTurn(string contextId, string text)
    {
        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            Debug.LogWarning("[MurfTTS] WebSocket not ready");
            return;
        }

        try
        {
            var req = new TTSRequest { context_id = contextId, text = text, end = true };
            await SendJson(req);
            Debug.Log($"[MurfTTS] Sent turn: '{text}'");
        }
        catch (Exception e)
        {
            Debug.LogError("[MurfTTS] SendTurn error: " + e.Message);
        }
    }

    /// <summary>
    /// Interrupt/clear a context. This also stops any playback and clears buffer.
    /// </summary>
    public async void ClearContextTurn(string contextId)
    {
        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            Debug.LogWarning("[MurfTTS] WebSocket not ready");
            return;
        }

        try
        {
            var clearMsg = new ClearContextMessage
            {
                clearContext = new ClearContext { context_id = contextId, clear = true }
            };
            await SendJson(clearMsg);
            Debug.Log($"[MurfTTS] Clear context request sent for: {contextId}");
        }
        catch (Exception e)
        {
            Debug.LogError("[MurfTTS] ClearContextTurn error: " + e.Message);
        }

        // Also clear buffered audio and stop playback immediately
        StopAndClearPlayback();
    }

    public async void SetAdvancedSettings(int minBufferSize, int maxBufferDelayInMs)
    {
        var settingsMsg = new AdvancedSettingsMessage
        {
            setAdvancedSettings = new AdvancedSettings
            {
                min_buffer_size = minBufferSize,
                max_buffer_delay_in_ms = maxBufferDelayInMs
            }
        };
        await SendJson(settingsMsg);
        Debug.Log($"[MurfTTS] Set advanced settings");
    }

    private void OnMessageReceived(byte[] message)
    {
        string msg = Encoding.UTF8.GetString(message);

        if (msg.Contains("\"audio\""))
        {
            try
            {
                MurfAudioMessage audioMsg = JsonUtility.FromJson<MurfAudioMessage>(msg);
                if (!string.IsNullOrEmpty(audioMsg.audio))
                {
                    byte[] pcmData = Convert.FromBase64String(audioMsg.audio);
                    float[] samples = ConvertPCM16ToFloat(pcmData);

                    audioBuffer.AddRange(samples);

                    if (audioMsg.final && !audioSource.isPlaying && !isPlayingChunk)
                    {
                        PlayBufferedAudioImmediate();
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[MurfTTS] Audio parse error: " + e.Message);
            }
        }
    }

    private float[] ConvertPCM16ToFloat(byte[] bytes)
    {
        int sampleCount = bytes.Length / 2;
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            samples[i] = BitConverter.ToInt16(bytes, i * 2) / 32768f;
        return samples;
    }

    private void PlayBufferedAudioImmediate()
    {
        if (audioBuffer.Count == 0) return;

        int maxSamplesToPlay = Mathf.Min(audioBuffer.Count, sampleRate); // up to 1s chunk
        if (maxSamplesToPlay < minPlaySamples)
            maxSamplesToPlay = Mathf.Min(audioBuffer.Count, minPlaySamples);

        float[] bufferCopy = audioBuffer.GetRange(0, maxSamplesToPlay).ToArray();
        audioBuffer.RemoveRange(0, maxSamplesToPlay);

        AudioClip clip = AudioClip.Create("MurfStreamChunk", bufferCopy.Length, 1, sampleRate, false);
        clip.SetData(bufferCopy, 0);

        audioSource.clip = clip;
        audioSource.Play();

        isPlayingChunk = true;

        if (waitForEndCoroutine != null) StopCoroutine(waitForEndCoroutine);
        waitForEndCoroutine = StartCoroutine(WaitForClipEndThenPlayNext(clip.length));
    }

    public IEnumerator SpeakAndWaitCoroutine(string contextId, string text, float timeoutSeconds = 10f)
{
    if (string.IsNullOrEmpty(text))
        yield break;

    // Defensive checks
    if (websocket == null || websocket.State != WebSocketState.Open)
    {
        // if websocket not ready, just log and return immediately
        Debug.LogWarning("[MurfTTS] SpeakAndWait: websocket not ready, falling back to instant log.");
        yield break;
    }

    // Clear any previous audio/context for a clean start
    try { ClearContextTurn(contextId); } catch { /* ignore */ }

    // Small frame to allow ClearContextTurn to process
    yield return null;

    // Send the new turn (async). SendTurn is async void but we still start waiting for audio.
    SendTurn(contextId, text);

    // Wait until audioBuffer has received some data or timeout
    float startTime = Time.realtimeSinceStartup;
    bool gotAudio = false;
    while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
    {
        if (audioBuffer != null && audioBuffer.Count > 0)
        {
            gotAudio = true;
            break;
        }
        // also break early if audio already playing
        if (audioSource != null && audioSource.isPlaying) { gotAudio = true; break; }
        yield return null;
    }

    // If no audio arrived within timeout, stop waiting
    if (!gotAudio)
    {
        Debug.LogWarning("[MurfTTS] SpeakAndWait: no audio arrived within timeout.");
        yield break;
    }

    // Wait until playback completes (buffer drained and not playing)
    startTime = Time.realtimeSinceStartup;
    while (Time.realtimeSinceStartup - startTime < timeoutSeconds)
    {
        bool bufferEmpty = (audioBuffer == null || audioBuffer.Count == 0);
        bool playing = (audioSource != null && audioSource.isPlaying) || isPlayingChunk;
        if (!playing && bufferEmpty)
            break;
        yield return null;
    }

    // Small buffer to ensure audio finished cleanly
    yield return new WaitForSecondsRealtime(0.05f);
}

    private IEnumerator WaitForClipEndThenPlayNext(float clipLengthSeconds)
    {
        yield return new WaitForSecondsRealtime(clipLengthSeconds);

        isPlayingChunk = false;
        waitForEndCoroutine = null;

        if (audioBuffer.Count >= minPlaySamples)
        {
            PlayBufferedAudioImmediate();
        }
    }

    private void StopAndClearPlayback()
    {
        try
        {
            if (audioSource != null && audioSource.isPlaying) audioSource.Stop();
        }
        catch { }

        audioBuffer.Clear();
        isPlayingChunk = false;

        if (waitForEndCoroutine != null)
        {
            StopCoroutine(waitForEndCoroutine);
            waitForEndCoroutine = null;
        }
    }
}
