using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using SimpleJSON;

// Text to speech using 60db.ai ( https://docs.60db.ai/api-reference/tts/text-to-speech )
// Structurally this mirrors ElevenLabsTextToSpeechManager, but 60db returns base64 audio inside
// a JSON payload (like Google does) instead of raw bytes, so we ask for WAV output and reuse the
// GoogleTextToSpeechManager WAV->AudioClip decoder.
public class SixtyDbTextToSpeechManager : MonoBehaviour
{

    void Start()
    {
      //  ExampleOfUse();
    }

    //*  EXAMPLE START (Cut and paste to your own code)*/
    void ExampleOfUse()
    {
        SixtyDbTextToSpeechManager ttsScript = gameObject.GetComponent<SixtyDbTextToSpeechManager>();
        string text = "Hello world!";
        string sixtyDbAPIKey = "put it here";
        string sixtyDb_voiceID = "your-voice-uuid"; //Full list of your voices: https://api.60db.ai/myvoices
        string json = ttsScript.BuildTTSJSON(text, sixtyDb_voiceID);

        //test
        RTDB db = new RTDB();
        ttsScript.SpawnTTSRequest(json, OnTTSCompletedCallback, db, sixtyDbAPIKey);
    }

    void OnTTSCompletedCallback(RTDB db, byte[] wavData)
    {
        if (wavData == null)
        {
            Debug.Log("Error getting wav: " + db.GetString("msg"));
            return;
        }

        GoogleTextToSpeechManager wavDecoder = gameObject.GetComponent<GoogleTextToSpeechManager>();
        AudioSource audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.clip = wavDecoder.MakeAudioClipFromWavFileInMemory(wavData);
        audioSource.Play();
    }

    //*  EXAMPLE END */

    // 60db uses 0-100 for stability/similarity (the opposite scale to ElevenLabs' 0-1). We request
    // wav output so the response can be decoded by the shared WAV decoder.
    public string BuildTTSJSON(string text, string voiceID, float stability = 50.0f, float similarity = 75.0f, float speed = 1.0f)
    {
        string json = $@"{{
            ""text"": ""{ SimpleJSON.JSONNode.Escape(text)}"",
            ""voice_id"": ""{ SimpleJSON.JSONNode.Escape(voiceID)}"",
            ""stability"": {stability},
            ""similarity"": {similarity},
            ""speed"": {speed},
            ""output_format"": ""wav""
        }}";

        return json;
    }

    public bool SpawnTTSRequest(string json, Action<RTDB, byte[]> myCallback, RTDB db, string sixtyDbAPIkey)
    {
        StartCoroutine(GetRequest(json, myCallback, db, sixtyDbAPIkey));
        return true;
    }

    IEnumerator GetRequest(string json, Action<RTDB, byte[]> myCallback, RTDB db, string sixtyDbAPIkey)
    {
        string url = "https://api.60db.ai/tts-synthesize";

#if UNITY_STANDALONE && !RT_RELEASE
        File.WriteAllText("60db_tts_json_sent.json", json);
#endif
        using (var postRequest = UnityWebRequest.PostWwwForm(url, "POST"))
        {
            //Start the request with a method instead of the object itself
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(json);
            postRequest.uploadHandler = (UploadHandler)new UploadHandlerRaw(bodyRaw);
            postRequest.SetRequestHeader("Content-Type", "application/json");
            postRequest.SetRequestHeader("Authorization", "Bearer " + sixtyDbAPIkey);

            // Send the request and wait for it to complete.
            yield return postRequest.SendWebRequest();

            if (postRequest.result != UnityWebRequest.Result.Success)
            {
                string msg = postRequest.error;
                Debug.Log(msg);
#if UNITY_STANDALONE && !RT_RELEASE
                File.WriteAllText("60db_tts_last_error_returned.json", postRequest.downloadHandler.text);
#endif
                db.Set("status", "failed");
                db.Set("msg", msg);
                myCallback.Invoke(db, null);
            }
            else
            {
#if UNITY_STANDALONE && !RT_RELEASE
                File.WriteAllText("60db_tts_json_received.json", postRequest.downloadHandler.text);
#endif

                JSONNode rootNode = JSON.Parse(postRequest.downloadHandler.text);
                yield return null; //wait a frame to lesson the jerkiness

                Debug.Assert(rootNode.Tag == JSONNodeType.Object);

                //60db wraps the result with a success flag and the audio as base64
                if (!rootNode["success"].AsBool)
                {
                    string msg = rootNode["message"];
                    Debug.Log("60db tts failed: " + msg);
                    db.Set("status", "failed");
                    db.Set("msg", msg);
                    myCallback.Invoke(db, null);
                }
                else
                {
                    string audioContent = rootNode["audio_base64"];
                    byte[] wavBytes = System.Convert.FromBase64String(audioContent);

                    db.Set("status", "success");
                    myCallback.Invoke(db, wavBytes);
                }
            }
        }
    }
}
