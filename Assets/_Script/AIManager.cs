//If you've bought the Salsa Suite plugin and installed it, you should uncomment the next line to enable lipsyncing.
//If you don't have it, comment it out, it should compile, but without the lipsyncing and eye movements.
#define CRAZY_MINNOW_PRESENT

#if CRAZY_MINNOW_PRESENT
using CrazyMinnow.SALSA;
#endif

using SimpleJSON;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

using static OpenAITextCompletionManager;

public class AIManager : MonoBehaviour
{

    public MicRecorder _microPhoneScript;
    string _openAI_APIKey;
    string _openAI_APIModel;
    string _googleAPIkey;
    string _elevenLabsAPIkey;
    public GameObject _visuals;
    AudioSource _audioSourceToUse = null;
    Vector2 vTextOverlayPos = new Vector2(Screen.width * 0.58f, (float)Screen.height - ((float)Screen.height * 0.4f));
    Vector2 vStatusOverlayPos = new Vector2(Screen.width * 0.44f, (float)Screen.height - ((float)Screen.height * 1.1f));
    public TMPro.TextMeshProUGUI _dialogText;
    public TMPro.TextMeshProUGUI _statusText;

    Queue<GTPChatLine> _chatHistory = new Queue<GTPChatLine>();
   
    public Button _recordButton;
    // Start is called before the first frame update

    Friend _activeFriend;
    Animator _animator = null;

    public TMPro.TextMeshProUGUI _friendNameGUI;

    private void OnDestroy()
    {

    }

    public void SetActiveFriend(Friend newFriend)
    {
        _activeFriend = newFriend;
        if (newFriend == null) return;
        _audioSourceToUse = gameObject.GetComponent<AudioSource>();
        _friendNameGUI.text = _activeFriend._name;
        
        if (_friendNameGUI.text == "Unset")
        {
            _dialogText.text = "Before running this, edit the config_template.txt file to set your API keys, then rename the file to config.txt!";
            return;
        }

        _dialogText.text = "Click Start for the character to introduce themselves.";
        _statusText.text = "";

        ForgetStuff();

         List<GameObject> objs = new List<GameObject> ();
        RTUtil.AddObjectsToListByNameIncludingInactive(_visuals, "char_visual", true, objs);

        foreach (GameObject obj in objs)
        {
            obj.SetActive(false);
        }

        //turn on the one we need
        var activeVisual = RTUtil.FindInChildrenIncludingInactive(_visuals, "char_visual_" + _activeFriend._visual);
        if (activeVisual != null)
        {
            activeVisual.SetActive(true);
        }

        #if CRAZY_MINNOW_PRESENT

        //see if it has a model we should sent the wavs to for lipsyncing
        var lipsyncModel = activeVisual.GetComponentInChildren<Salsa>();

        if (lipsyncModel != null)
        {
            Debug.Log("Found salsa");
            //_salsa
            _audioSourceToUse = lipsyncModel.GetComponent<AudioSource>();
        }
        _animator = activeVisual.GetComponentInChildren<Animator>();
#endif
        SetListening(false);
       
    }

    void SetListening(bool bNew)
    {
        if (_animator != null)
        {
            _animator.SetBool("Listening", bNew);
        }
    }

    void SetTalking(bool bNew)
    {
        if (_animator != null)
        {
            _animator.SetBool("Talking", bNew);
        }
    }
    public void SetGoogleAPIKey(string key)
    {
        _googleAPIkey = key;
    }

    public void SetOpenAI_APIKey(string key)
    {
        _openAI_APIKey = key;
    }
    public void SetOpenAI_Model(string model)
    {
        _openAI_APIModel = model;
    }

    public void SetElevenLabsAPIKey(string key)
    {
        _elevenLabsAPIkey = key;
    }

    public string GetAdvicePrompt()
    {
       return _activeFriend._advicePrompt;
    }

    public void ModFriend(int mod)
    {
        int curFriendIndex = _activeFriend._index;

        //mod the current friend index by mod, but make sure it's not less than
        //0 and more than Config.Get().GetFriendCount()
        int newFriendIndex = (curFriendIndex + mod) % Config.Get().GetFriendCount();
        if (newFriendIndex < 0) newFriendIndex = Config.Get().GetFriendCount() - 1;
        SetActiveFriend(Config.Get().GetFriendByIndex(newFriendIndex));

    }

    public void PlayClickSound()
    {
        RTMessageManager.Get().Schedule(0, RTAudioManager.Get().PlayEx, "muffled_bump", 0.5f ,1.0f, false, 0.0f);
    }
    public void OnPreviousFriend()
    {
        PlayClickSound();
        ModFriend(-1);
    }
    public void OnNextFriend()
    {
        PlayClickSound();
        ModFriend(1);
    }

    void Start()
    {

    }

    public int CountWords(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return 0;
        }

        // Split the input into words and return the count
        string[] words = input.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        return words.Length;
    }

    string GetBasePrompt()
    {
        return _activeFriend._basePrompt;
    }

    string GetDirectionPrompt()
    {
        return _activeFriend._directionPrompt;
    }

    void TrimHistoryIfNeeded()
    {
        int tokenSize = CountWords(GetBasePrompt());
        int historyTokenSize = 0;
        //tokenSize of all words in _chatHistory
        foreach (GTPChatLine line in _chatHistory)
        {
            historyTokenSize += CountWords(line._content);
        }

        int maxTokenUseForPromptsAndHistory = tokenSize+ _activeFriend._friendTokenMemory; //too high and the text gets... corrupted...

        if (tokenSize + historyTokenSize > maxTokenUseForPromptsAndHistory)
        {
            //remove oldest lines until we are under the max
            while (tokenSize + historyTokenSize > maxTokenUseForPromptsAndHistory)
            {
                //we always remove things in pairs, the request, and the answer.

                GTPChatLine line = _chatHistory.Dequeue();
                historyTokenSize -= CountWords(line._content);
                line = _chatHistory.Dequeue();
                historyTokenSize -= CountWords(line._content);
                line = _chatHistory.Dequeue();
                historyTokenSize -= CountWords(line._content);
            }
        }

        Debug.Log("Prompt tokens: " + tokenSize + " History token size:" + historyTokenSize);

    }
    void GetGPT3Text(string question)
    {
        //build a stack of GTPChatLine so we can add as many as we want

        OpenAITextCompletionManager textCompletionScript = gameObject.GetComponent<OpenAITextCompletionManager>();
        Queue<GTPChatLine> lines = new Queue<GTPChatLine>();
        lines.Enqueue(new GTPChatLine("system", GetBasePrompt()));
    
        TrimHistoryIfNeeded();
        //inject chat history
        foreach (GTPChatLine line in _chatHistory)
        {
            lines.Enqueue(line);
        }

        lines.Enqueue(new GTPChatLine("system", GetDirectionPrompt()));

        //the new question
        lines.Enqueue(new GTPChatLine("user", question));
    
        string json = textCompletionScript.BuildChatCompleteJSON(lines, _activeFriend._maxTokensToGenerate, _activeFriend._temperature, _openAI_APIModel);
        RTDB db = new RTDB();
        db.Set("question", question);
        db.Set("role", "user");

        textCompletionScript.SpawnChatCompleteRequest(json, OnGPT3TextCompletedCallback, db, _openAI_APIKey);
        UpdateStatusText(RTUtil.ConvertSansiToUnityColors("(AI is thinking) You said: `$" + question + "``"), 20);
    }

    void OnGPT3TextCompletedCallback(RTDB db, JSONObject jsonNode)
    {

        if (jsonNode == null)
        {
            //must have been an error
            Debug.Log("Got callback! Data: " + db.ToString());
            UpdateStatusText(db.GetString("msg"));
            return;
        }

        /*
        foreach (KeyValuePair<string, JSONNode> kvp in jsonNode)
        {
            Debug.Log("Key: " + kvp.Key + " Val: " + kvp.Value);
        }
        */
        string reply = jsonNode["choices"][0]["message"]["content"];
        if (reply.Length < 5)
        {
            Debug.Log("Error parsing reply: " + reply);
            db.Set("english", "Error. I don't know what to say.");
            db.Set("japanese", "エラーです。なんて言っていいのかわからない。");
            SayText(db);
            return;
        }

            //just whatever is there is fine
            db.Set("english", reply);
            db.Set("japanese", reply);
       
        //Let's say it
        SayText(db);

        _chatHistory.Enqueue(new GTPChatLine(db.GetString("role"), db.GetString("question")));
        _chatHistory.Enqueue(new GTPChatLine("assistant", reply));

    }

    void SayText(RTDB db)
    {
      
        string text = db.GetString(_activeFriend._language);
        string json;
        int sampleRate = 22050;

        if (_activeFriend._elevelLabsVoice.Length > 1 && _elevenLabsAPIkey.Length > 1)
        {
            //get the country code directly from the voice name. This should always work, I hope
            string countryCode = _activeFriend._elevelLabsVoice.Substring(0, 5);
            ElevenLabsTextToSpeechManager ttsScript = gameObject.GetComponent<ElevenLabsTextToSpeechManager>();
            json = ttsScript.BuildTTSJSON(text, _activeFriend._elevenlabsStability);
            ttsScript.SpawnTTSRequest(json, OnTTSCompletedCallbackElevenLabs, db, _elevenLabsAPIkey, _activeFriend._elevelLabsVoice);

            UpdateStatusText("Clearing throat...", 20);

        }
        else if (_activeFriend._googleVoice.Length > 1 && _googleAPIkey.Length > 1)
        {
            //get the country code directly from the voice name. This should always work, I hope
            string countryCode = _activeFriend._googleVoice.Substring(0, 5);
            GoogleTextToSpeechManager ttsScript = gameObject.GetComponent<GoogleTextToSpeechManager>();
            json = ttsScript.BuildTTSJSON(text, countryCode, _activeFriend._googleVoice, sampleRate, _activeFriend._pitch, _activeFriend._speed);
            ttsScript.SpawnTTSRequest(json, OnTTSCompletedCallback, db, _googleAPIkey);
            UpdateStatusText("Clearing throat...", 20);
        } else
        {
            //No text to speech setup for this voice

            UpdateDialogText(db.GetString("japanese"));
            UpdateStatusText("");
        }
    }

    void OnTTSCompletedCallback(RTDB db, byte[] wavData)
    {
        if (wavData == null)
        {
            Debug.Log("Error getting wav: " + db.GetString("msg"));
           
        } else
        {
            GoogleTextToSpeechManager ttsScript = gameObject.GetComponent<GoogleTextToSpeechManager>();
            AudioSource audioSource = _audioSourceToUse;
            audioSource.clip = ttsScript.MakeAudioClipFromWavFileInMemory(wavData);
            audioSource.Play();

        }


        UpdateDialogText(db.GetString("japanese"));
        UpdateStatusText("");
    }


    void OnTTSCompletedCallbackElevenLabs(RTDB db, AudioClip clip)
    {
        if (clip == null)
        {
            Debug.Log("Error getting wav: " + db.GetString("msg"));
          
        } else
        {
            ElevenLabsTextToSpeechManager ttsScript = gameObject.GetComponent<ElevenLabsTextToSpeechManager>();
            AudioSource audioSource = _audioSourceToUse;
            audioSource.clip = clip;
            audioSource.Play();
        }
  

        UpdateDialogText(db.GetString("japanese"));
        UpdateStatusText("");
    }

    public void ProcessMicAudioByFileName(string fAudioFileName)
    {
        OpenAISpeechToTextManager speechToTextScript = gameObject.GetComponent<OpenAISpeechToTextManager>();

        byte[] fileBytes = System.IO.File.ReadAllBytes(fAudioFileName);
        string prompt = "";

        RTDB db = new RTDB();

        //let's add strings from the recent conversation to the prompt text
        foreach (GTPChatLine line in _chatHistory)
        {
            prompt += line._content + "\n";
            if (prompt.Length > 180)
            {
                //whisper will only processes the last 200 words I read
                break;
            }
        }

        if (prompt == "")
        {
            //no history yet?  Ok, use the base prompt, better than nothing
            prompt = _activeFriend._basePrompt;
        }
        

        speechToTextScript.SpawnSpeechToTextRequest(prompt, OnSpeechToTextCompletedCallback, db, _openAI_APIKey, fileBytes);
        UpdateStatusText("Understanding speech...", 20);

    }

   

    void OnSpeechToTextCompletedCallback(RTDB db, JSONObject jsonNode)
    {

        if (jsonNode == null)
        {
            //must have been an error
            Debug.Log("Got callback! Data: " + db.ToString());
            UpdateStatusText(db.GetString("msg"));
            return;
        }

        string reply = jsonNode["text"];
        UpdateStatusText("Heard: "+reply);
        GetGPT3Text(reply);
    }

    public void ToggleRecording()
    {
        if (!_microPhoneScript.IsRecording())
        {
            StopTalking();
            Debug.Log("Recording started");
            //make the button background turn red
            _recordButton.GetComponent<Image>().color = Color.red;
            _microPhoneScript.StartRecording();
            PlayClickSound();
            SetListening(true);

        }
        else
        {
            //Turn the button background color back
            _recordButton.GetComponent<Image>().color = Color.white;
            PlayClickSound();
            //let's set the filename to a temporary space that will work on iOS
            string outputFileName = Application.temporaryCachePath + "/temp.wav";
            _microPhoneScript.StopRecordingAndProcess(outputFileName);
            SetListening(false);

        }
    }

    public void OnStopButton()
    {
        PlayClickSound();
        StopTalking();
    }

    public void OnCopyButton()
    {
        PlayClickSound();
        string text = _dialogText.text;
        if (text.Length > 0)
        {
            GUIUtility.systemCopyBuffer = text;
            UpdateStatusText("Copied to clipboard");
        } else
        {
            UpdateStatusText("Nothing to copy");
        }

    }
    public void OnAdviceButton()
    {
        ForgetStuff();
        //build a stack of GTPChatLine so we can add as many as we want
        PlayClickSound();

        OpenAITextCompletionManager textCompletionScript = gameObject.GetComponent<OpenAITextCompletionManager>();
        Queue<GTPChatLine> lines = new Queue<GTPChatLine>();
        lines.Enqueue(new GTPChatLine("system", GetBasePrompt()));

        TrimHistoryIfNeeded();
        //inject chat history
        foreach (GTPChatLine line in _chatHistory)
        {
            lines.Enqueue(line);
        }

        string question = GetAdvicePrompt();

        //remind it about the format
        lines.Enqueue(new GTPChatLine("system", GetDirectionPrompt()));
        //the new question
        lines.Enqueue(new GTPChatLine("system", question));

        string json = textCompletionScript.BuildChatCompleteJSON(lines, _activeFriend._maxTokensToGenerate, _activeFriend._temperature, _openAI_APIModel);
        RTDB db = new RTDB();
        db.Set("role", "system");
        db.Set("question", question);
        textCompletionScript.SpawnChatCompleteRequest(json, OnGPT3TextCompletedCallback, db, _openAI_APIKey);
        UpdateStatusText(RTUtil.ConvertSansiToUnityColors("Thinking..."), 20);
        UpdateDialogText("");
    }

    public void StopTalking()
    {
        AudioSource audioSource = _audioSourceToUse;
        audioSource.Stop();
        SetTalking(false);

    }
    public void ForgetStuff()
    {
        _chatHistory.Clear();
        StopTalking();

    }
    public void OnForgetButton()
    {
        //Clear chat history
        PlayClickSound();   //write a message about it
        //RTQuickMessageManager.Get().ShowMessage("Chat history cleared");
        ForgetStuff();
    }

    public int GetFriendIndex()
    {
        if (_activeFriend == null)
            return 0;
        else
            return _activeFriend._index;

    }

    void UpdateStatusText(string msg, float timer = 3)
    {
        _statusText.text = msg;
    }

    void UpdateDialogText(string msg)
    {
        _dialogText.text = msg;
    }

    private void Update()
    {
        if (_audioSourceToUse != null) 
        {
            SetTalking(_audioSourceToUse.isPlaying);
        }

    }
   
}
