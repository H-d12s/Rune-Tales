using UnityEngine;
using NativeWebSocket;
using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

[DisallowMultipleComponent]
public class BattleNarrator : MonoBehaviour
{
    public static BattleNarrator Instance { get; private set; }

    [Header("WebSocket / TTS settings")]
    [Tooltip("Your Murf (or similar) WebSocket API key. Leave empty to disable connecting.")]
    public string apiKey = "ap2_1210100a-8def-4839-9825-095fa0c59ce2";
    public string voiceId = "en-US-ken";
    public string style = "Wizard";
    public int pitch = -35;

    [Header("Playback")]
    public AudioSource audioSource; // can be assigned in inspector; will be added if missing
    public int sampleRate = 48000;
    public int minPlaySamples = 2048;
    [Tooltip("How many initial samples must be buffered before we consider audio 'ready' for typing to start.")]
    public int minReadySamples = 512;
    public float defaultTimeoutSeconds = 10f;

    // internal
    private WebSocket websocket;
    private List<float> audioBuffer = new List<float>();
    private bool isPlayingChunk = false;
    private Coroutine waitForEndCoroutine;

    // duplicate-send guard: keys are contextId + '|' + textHash
    private Dictionary<string, float> recentSends = new Dictionary<string, float>();
    private readonly float recentSendWindow = 3f; // seconds

    [Serializable] public class VoiceConfig { public string voice_id; public string style; public int pitch; }
    [Serializable] public class VoiceConfigMessage { public VoiceConfig voice_config; }
    [Serializable] public class TTSRequest { public string context_id; public string text; public bool end = true; }
    [Serializable] public class ClearContext { public string context_id; public bool clear = true; }
    [Serializable] public class ClearContextMessage { public ClearContext clearContext; }
    [Serializable] private class MurfAudioMessage { public string type; public string audio; public string context_id; public bool final; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        audioSource.spatialBlend = 0f;
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    private async void Start()
    {
        // If no API key, skip connect but keep functionality (SpeakAndWait will log instead).
        if (string.IsNullOrEmpty(apiKey))
        {
            Debug.LogWarning("[BattleNarrator] apiKey empty — TTS will be disabled. Set apiKey in inspector to enable.");
            return;
        }

        string url = $"wss://api.murf.ai/v1/speech/stream-input?api_key={apiKey}";
        websocket = new WebSocket(url);

        websocket.OnOpen += async () =>
        {
            Debug.Log("[BattleNarrator] Connected to TTS streaming API.");
            var cfg = new VoiceConfigMessage
            {
                voice_config = new VoiceConfig { voice_id = voiceId, style = style, pitch = pitch }
            };
            try { await SendJson(cfg); Debug.Log("[BattleNarrator] Sent voice_config"); } catch (Exception e) { Debug.LogWarning("[BattleNarrator] Send voice_config failed: " + e.Message); }
        };

        websocket.OnError += (e) => Debug.LogError("[BattleNarrator] WebSocket Error: " + e);
        websocket.OnClose += (e) => Debug.LogWarning("[BattleNarrator] WebSocket Closed");
        websocket.OnMessage += OnMessageReceived;

        try { await websocket.Connect(); }
        catch (Exception ex) { Debug.LogError("[BattleNarrator] WebSocket connect failed: " + ex.Message); }
    }

    private void Update()
    {
        websocket?.DispatchMessageQueue();

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
            try { await websocket.Close(); websocket = null; }
            catch (Exception e) { Debug.LogWarning("[BattleNarrator] Error closing websocket: " + e.Message); }
        }
    }

    private async Task SendJson(object obj)
    {
        try
        {
            string json = JsonUtility.ToJson(obj);
            await websocket.SendText(json);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BattleNarrator] SendJson error: " + e.Message);
        }
    }

    /// <summary>
    /// SendTurn: sends a TTS request to the server for (contextId, text)
    /// Duplicate sends for the same text+context within a short window are skipped.
    /// </summary>
    public async void SendTurn(string contextId, string text)
    {
        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            Debug.LogWarning("[BattleNarrator] WebSocket not ready — SendTurn skipped.");
            return;
        }

        if (string.IsNullOrEmpty(contextId)) contextId = "default";
        if (text == null) text = "";

        string key = contextId + "|" + text.GetHashCode();

        // Duplicate send guard
        if (recentSends.TryGetValue(key, out float lastTime))
        {
            if (Time.realtimeSinceStartup - lastTime < recentSendWindow)
            {
                Debug.Log($"[BattleNarrator] Duplicate SendTurn skipped for context '{contextId}' text '{(text.Length>40?text.Substring(0,40)+"...":text)}'");
                return;
            }
        }

        // record send time and schedule cleanup
        recentSends[key] = Time.realtimeSinceStartup;
        StartCoroutine(CleanupRecentSendAfterDelay(key, recentSendWindow + 1f));

        try
        {
            var req = new TTSRequest { context_id = contextId, text = text, end = true };
            await SendJson(req);
        }
        catch (Exception e) { Debug.LogWarning("[BattleNarrator] SendTurn error: " + e.Message); }
    }

    private IEnumerator CleanupRecentSendAfterDelay(string key, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        try { recentSends.Remove(key); } catch { }
    }

    public async void ClearContextTurn(string contextId)
    {
        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            // still clear local buffer
            StopAndClearPlayback();
            return;
        }

        try
        {
            var clearMsg = new ClearContextMessage { clearContext = new ClearContext { context_id = contextId, clear = true } };
            await SendJson(clearMsg);
        }
        catch (Exception e) { Debug.LogWarning("[BattleNarrator] ClearContextTurn error: " + e.Message); }

        StopAndClearPlayback();
    }

    public void SetAdvancedSettings(int minBufferSize, int maxBufferDelayInMs)
    {
        // Not implemented here but you can add an advanced message structure if the API supports it.
    }

    private void OnMessageReceived(byte[] message)
    {
        string msg = Encoding.UTF8.GetString(message);
        if (!msg.Contains("\"audio\"")) return;

        try
        {
            MurfAudioMessage audioMsg = JsonUtility.FromJson<MurfAudioMessage>(msg);
            if (!string.IsNullOrEmpty(audioMsg.audio))
            {
                byte[] pcm = Convert.FromBase64String(audioMsg.audio);
                float[] samples = ConvertPCM16ToFloat(pcm);
                audioBuffer.AddRange(samples);

                // If server marks this final chunk and nothing's playing, start immediately
                if (audioMsg.final && !audioSource.isPlaying && !isPlayingChunk)
                    PlayBufferedAudioImmediate();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[BattleNarrator] Audio parse error: " + e.Message);
        }
    }

    private float[] ConvertPCM16ToFloat(byte[] bytes)
    {
        int count = bytes.Length / 2;
        float[] samples = new float[count];
        for (int i = 0; i < count; i++)
            samples[i] = BitConverter.ToInt16(bytes, i * 2) / 32768f;
        return samples;
    }

    private void PlayBufferedAudioImmediate()
    {
        if (audioBuffer.Count == 0) return;

        int maxSamples = Mathf.Min(audioBuffer.Count, sampleRate);
        if (maxSamples < minPlaySamples) maxSamples = Mathf.Min(audioBuffer.Count, minPlaySamples);

        float[] copy = audioBuffer.GetRange(0, maxSamples).ToArray();
        audioBuffer.RemoveRange(0, maxSamples);

        AudioClip clip = AudioClip.Create("NarratorChunk", copy.Length, 1, sampleRate, false);
        clip.SetData(copy, 0);

        audioSource.clip = clip;
        audioSource.Play();

        isPlayingChunk = true;
        if (waitForEndCoroutine != null) StopCoroutine(waitForEndCoroutine);
        waitForEndCoroutine = StartCoroutine(WaitForClipEndThenPlayNext(clip.length));
    }

    private IEnumerator WaitForClipEndThenPlayNext(float clipLen)
    {
        yield return new WaitForSecondsRealtime(clipLen);
        isPlayingChunk = false;
        waitForEndCoroutine = null;
        if (audioBuffer.Count >= minPlaySamples) PlayBufferedAudioImmediate();
    }

    private void StopAndClearPlayback()
    {
        try { if (audioSource != null && audioSource.isPlaying) audioSource.Stop(); } catch { }
        audioBuffer.Clear();
        isPlayingChunk = false;
        if (waitForEndCoroutine != null) { StopCoroutine(waitForEndCoroutine); waitForEndCoroutine = null; }
    }

    /// <summary>
    /// PrepareSpeech sends the TTS request and waits until a small initial chunk is buffered
    /// or playback starts. This is intended for UI: wait for this before starting typewriter.
    /// </summary>
    /// <summary>
    /// Send the text to TTS and wait briefly until Murf returns initial audio samples
    /// or until playback starts. This is used so UI typing only begins once audio is ready.
    /// </summary>
    public IEnumerator PrepareSpeechCoroutine(string contextId, string text, float timeoutSeconds = 4f)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        if (timeoutSeconds <= 0f) timeoutSeconds = 4f;

        // If websocket not connected, don't block UI for audio that won't come.
        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            // still send (or skip) — here we choose to not send and return immediately.
            yield break;
        }

        // Clear previous context for a clean audio start
        try { ClearContextTurn(contextId); } catch { }
        // small frame to let clear take effect
        yield return null;

        // Send the text now (duplicate-guarded inside SendTurn)
        try { SendTurn(contextId, text); } catch { yield break; }

        float start = Time.realtimeSinceStartup;
        bool gotAudio = false;

        // Wait only for a small "ready" buffer; use minReadySamples so UI can start typing earlier.
        while (Time.realtimeSinceStartup - start < timeoutSeconds)
        {
            if (audioBuffer != null && audioBuffer.Count >= minReadySamples)
            {
                gotAudio = true;
                break;
            }
            if (audioSource != null && (audioSource.isPlaying || isPlayingChunk))
            {
                gotAudio = true;
                break;
            }
            yield return null;
        }

        // If no audio arrived within timeout, just return (UI will still proceed)
        yield break;
    }

    /// <summary>
    /// Speak and wait until audio finishes (or timeout). Use StartCoroutine on this.
    /// If websocket is not connected or apiKey empty, this will return quickly (no TTS).
    /// sendText: if true, this coroutine will send the text to the TTS server itself.
    ///           if false, it will assume the text has already been sent (e.g. by PrepareSpeechCoroutine).
    /// </summary>
    public IEnumerator SpeakAndWaitCoroutine(string contextId, string text, float timeoutSeconds = -1f, bool sendText = true)
    {
        if (string.IsNullOrEmpty(text)) yield break;
        if (timeoutSeconds <= 0f) timeoutSeconds = defaultTimeoutSeconds;

        // If we are supposed to send the text but websocket is not connected, bail quickly.
        if (sendText && (websocket == null || websocket.State != WebSocketState.Open))
        {
            Debug.Log("[BattleNarrator] WebSocket not ready - SpeakAndWait will not block for audio. Text: " + text);
            yield break;
        }

        // If caller asked us NOT to send text (sendText==false) but websocket isn't connected,
        // return immediately — there's nothing we can wait for.
        if (!sendText && (websocket == null || websocket.State != WebSocketState.Open))
        {
            Debug.LogWarning("[BattleNarrator] SpeakAndWait called with sendText=false but websocket isn't connected. Returning immediately to avoid hang.");
            yield break;
        }

        // If we are responsible for sending the text, clear previous context for clean audio start
        if (sendText)
        {
            try { ClearContextTurn(contextId); } catch { }
            // one frame to let clear take effect
            yield return null;

            // send the text (duplicate-guarded)
            SendTurn(contextId, text);
        }

        // wait until audio or timeout
        float start = Time.realtimeSinceStartup;
        bool gotAudio = false;
        while (Time.realtimeSinceStartup - start < timeoutSeconds)
        {
            if (audioBuffer.Count > 0 || (audioSource != null && audioSource.isPlaying) || isPlayingChunk)
            {
                gotAudio = true;
                break;
            }
            yield return null;
        }

        if (!gotAudio)
        {
            Debug.LogWarning("[BattleNarrator] SpeakAndWait: no audio arrived within timeout for: " + text);
            yield break;
        }

        // wait until playback completes and buffer drained (or timeout)
        start = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - start < timeoutSeconds)
        {
            bool bufferEmpty = audioBuffer.Count == 0;
            bool playing = (audioSource != null && audioSource.isPlaying) || isPlayingChunk;
            if (!playing && bufferEmpty) break;
            yield return null;
        }

        // tiny buffer
        yield return new WaitForSecondsRealtime(0.05f);
    }

    /// <summary>
    /// Public wrapper to immediately stop playback and clear any buffered audio/context.
    /// Call this when you need to abort TTS (e.g. UI cancel flows).
    /// </summary>
    public void AbortPlaybackAndContext(string contextId = "battle")
    {
        try
        {
            // Try clearing remote TTS context (best-effort)
            try { ClearContextTurn(contextId); } catch { }

            // Stop local playback and clear buffer
            StopAndClearPlayback();
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[BattleNarrator] AbortPlaybackAndContext error: {ex.Message}");
        }
    }
}
