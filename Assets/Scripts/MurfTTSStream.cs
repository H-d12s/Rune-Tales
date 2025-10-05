using UnityEngine;
using NativeWebSocket;
using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;

public class MurfTTSStream : MonoBehaviour
{
    [Header("Murf API Settings")]
    public string apiKey = "ap2_76d7def2-5aab-4631-b21b-a8129bbcc4a4";  // 🔑 replace with your actual API key
    public string voiceId = "en-US-ken";
    public string style = "Conversational";
    public int pitch = -10;

    private WebSocket websocket;
    private AudioSource audioSource;
    private List<float> audioBuffer = new List<float>();

    private const int sampleRate = 24000;

    private async void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (!audioSource)
            audioSource = gameObject.AddComponent<AudioSource>();

        // ✅ Pass API key via URL (Murf standard)
        string url = $"wss://api.murf.ai/v1/speech/stream-input?api_key={apiKey}";

        websocket = new WebSocket(url);

        websocket.OnOpen += async () =>
        {
            Debug.Log("✅ Connected to Murf streaming API");

            // Step 1️⃣: Send voice configuration
           await SendJson(new
{
    
    voice_config = new
    {
        voice_id = voiceId,
        style = style,
        pitch = pitch
    }
});
            Debug.Log("📤 Sent voice_config — ready to stream text");
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

    private void Update() => websocket?.DispatchMessageQueue();

    private void OnDestroy() => websocket?.Close();

    // --- ✉️ Helper: Send JSON messages ---
    private async Task SendJson(object obj)
    {
        string json = JsonUtility.ToJson(obj);
        await websocket.SendText(json);
        Debug.Log("📨 Sent: " + json);
    }

    // --- 🔊 Send text to generate speech ---
    public async void SendText(string text)
    {
        if (websocket == null || websocket.State != WebSocketState.Open)
        {
            Debug.LogWarning("WebSocket not ready");
            return;
        }

        await SendJson(new { type = "sendText", text = text });
        await SendJson(new { type = "endOfStream" });

        Debug.Log("🗣️ Sent text to Murf: " + text);
    }

    // --- 🧠 Handle incoming messages ---
    private void OnMessageReceived(byte[] message)
    {
        string msg = Encoding.UTF8.GetString(message);
        Debug.Log("⬅️ Murf raw message: " + msg);

        // Handle JSON containing base64 audio
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
                        PlayBufferedAudio();
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Audio parse error: " + e.Message);
            }
        }
    }

    [Serializable]
    private class MurfAudioMessage
    {
        public string type;
        public string audio;
    }

    // --- 🔊 PCM conversion ---
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

        AudioClip clip = AudioClip.Create("MurfStream", audioBuffer.Count, 1, sampleRate, false);
        clip.SetData(audioBuffer.ToArray(), 0);
        audioSource.clip = clip;
        audioSource.Play();

        Debug.Log($"🎧 Playing {audioBuffer.Count} samples ({audioBuffer.Count / (float)sampleRate:F2}s)");
        audioBuffer.Clear();
    }
}
