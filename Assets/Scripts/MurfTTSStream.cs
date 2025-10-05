using UnityEngine;
using NativeWebSocket;
using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

public class MurfTTSStream : MonoBehaviour
{
    [Header("Murf API Settings")]
    public string apiKey = "ap2_1210100a-8def-4839-9825-095fa0c59ce2";  // Replace with your actual API key
    public string voiceId = "en-US-ken";
    public string style = "Wizard";
    public int pitch = -35;

    private WebSocket websocket;
    private AudioSource audioSource;
    private List<float> audioBuffer = new List<float>();
    private const int sampleRate = 50000;
    private bool isPlaying = false;

    [Serializable]
    public class VoiceConfig
    {
        public string voice_id;
        public string style;
        public int pitch;
    }

    [Serializable]
    public class VoiceConfigMessage
    {
        public VoiceConfig voice_config;
    }

    [Serializable]
    public class TTSRequest
    {
        public string context_id;
        public string text;
        public bool end = true;
    }

    [Serializable]
    public class AdvancedSettings
    {
        public int min_buffer_size;
        public int max_buffer_delay_in_ms;
    }

    [Serializable]
    public class AdvancedSettingsMessage
    {
        public AdvancedSettings setAdvancedSettings;
    }

    [Serializable]
    public class ClearContext
    {
        public string context_id;
        public bool clear = true;
    }

    [Serializable]
    public class ClearContextMessage
    {
        public ClearContext clearContext;
    }

    [Serializable]
    private class MurfAudioMessage
    {
        public string type;
        public string audio;
        public string context_id;
        public bool final;
    }

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (!audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    private async void Start()
    {
        string url = $"wss://api.murf.ai/v1/speech/stream-input?api_key={apiKey}";

        websocket = new WebSocket(url);

        websocket.OnOpen += async () =>
        {
            Debug.Log("✅ Connected to Murf streaming API");

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
                Debug.Log("📤 Sent voice_config — ready for context-based streaming");
            }
            catch (Exception e)
            {
                Debug.LogError("Error sending voice config: " + e.Message);
            }
        };

        websocket.OnError += (e) => Debug.LogError("❌ WebSocket Error: " + e);
        websocket.OnClose += (e) => Debug.LogWarning("🔒 WebSocket Closed");
        websocket.OnMessage += OnMessageReceived;

        try
        {
            await websocket.Connect();
        }
        catch (Exception ex)
        {
            Debug.LogError("WebSocket connect failed: " + ex.Message);
        }
    }

    private void Update()
    {
        websocket?.DispatchMessageQueue();

        if (!audioSource.isPlaying && isPlaying && audioBuffer.Count > 0)
        {
            PlayBufferedAudio();
        }
    }

    private async void OnDestroy()
    {
        if (websocket != null)
        {
            try
            {
                await websocket.Close();
                websocket = null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Error during WebSocket close: " + e.Message);
            }
        }
    }

    private async Task SendJson(object obj)
    {
        try
        {
            string json = JsonUtility.ToJson(obj);
            await websocket.SendText(json);
            Debug.Log("📨 Sent: " + json);
        }
        catch (Exception e)
        {
            Debug.LogError("SendJson error: " + e.Message);
        }
    }

    /// <summary>
    /// Send a text-to-speech turn with context ID for multi-turn/interruptible dialogue
    /// </summary>
    public async void SendTurn(string contextId, string text)
    {
        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            Debug.LogWarning("WebSocket not ready");
            return;
        }

        try
        {
            var req = new TTSRequest { context_id = contextId, text = text, end = true };
            await SendJson(req);
            Debug.Log($"🗣️ Sent turn for context_id: {contextId}, text: {text}");
        }
        catch (Exception e)
        {
            Debug.LogError("SendTurn error: " + e.Message);
        }
    }

    /// <summary>
    /// Interrupt/cancel a TTS turn for a given context ID
    /// </summary>
    public async void ClearContextTurn(string contextId)
    {
        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            Debug.LogWarning("WebSocket not ready");
            return;
        }

        try
        {
            var clearMsg = new ClearContextMessage
            {
                clearContext = new ClearContext { context_id = contextId, clear = true }
            };
            await SendJson(clearMsg);
            Debug.Log($"🧹 Clear context request sent for: {contextId}");
        }
        catch (Exception e)
        {
            Debug.LogError("ClearContextTurn error: " + e.Message);
        }
    }

    /// <summary>
    /// Change streaming buffer/latency settings as needed
    /// </summary>
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
        Debug.Log($"⚙️ Set advanced settings: min_buffer_size={minBufferSize}, max_buffer_delay_in_ms={maxBufferDelayInMs}");
    }

    private void OnMessageReceived(byte[] message)
    {
        string msg = Encoding.UTF8.GetString(message);
        Debug.Log("⬅️ Murf raw message: " + msg);

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

                    if (!audioSource.isPlaying && audioBuffer.Count > sampleRate / 4)
                    {
                        PlayBufferedAudio();
                    }
                    if (audioMsg.final)
                    {
                        Debug.Log("✅ Finished all audio for context_id: " + audioMsg.context_id);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Audio parse error: " + e.Message);
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

    private void PlayBufferedAudio()
    {
        if (audioBuffer.Count == 0) return;

        float[] bufferCopy = audioBuffer.ToArray();
        audioBuffer.Clear();

        AudioClip clip = AudioClip.Create("MurfStream", bufferCopy.Length, 1, sampleRate, false);
        clip.SetData(bufferCopy, 0);
        audioSource.clip = clip;
        audioSource.Play();

        isPlaying = true;

        Debug.Log($"🎧 Playing {bufferCopy.Length} samples ({bufferCopy.Length / (float)sampleRate:F2}s)");
    }
}
