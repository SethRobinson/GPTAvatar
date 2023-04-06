using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class Friend : ScriptableObject
{
    public string _name = "Unset";
    public string _language = "english";
    public string _basePrompt = "unset";
    public string _directionPrompt;
    public string _advicePrompt = "Tell me there has been an error";
    public int _index;
    public int _friendTokenMemory = 200;
    public string _googleVoice = "";
    public string _elevelLabsVoice = "";
    public float _pitch = 0.0f;
    public float _speed = 1.0f;
    public string _visual = "";
    public int _maxTokensToGenerate = 50;
    public float _temperature = 1.3f;
    public float _elevenlabsStability = 0.7f;
}

public class Config : MonoBehaviour
{  
    public static bool _isTestMode = false; //could do anything, _testMode is checked by random functions
    public const float _clientVersion = 0.1f;
  
    static Config _this;
    public List<AudioClip> m_audioClips;
    AIManager _aiManagerScript;
    List<Friend> _friendList = new List<Friend>();
    string _loadedConfigFile = "";
    float m_version = 0.01f;

    private void Start()
    {
        RTAudioManager.Get().AddClipsToLibrary(m_audioClips);

        //load config file
        string filePath = Application.dataPath + "../../config.txt";

        //check if the file exists
        if (File.Exists(filePath))
        {
            LoadConfigFile(filePath);
        }
        else
        {
            //show error
            RTQuickMessageManager.Get().ShowMessage("Config file not found at " + filePath);
        }

        LoadConfigFile(LoadConfigFromFile(filePath));
    }

    static public Config Get() { return _this; }

    void Awake()
    {
#if RT_BETA

#endif
        _this = this;
        _aiManagerScript = gameObject.GetComponent<AIManager>();
    }

    public string GetVersionString() { return m_version.ToString("0.00"); }
    public float GetVersion() { return m_version; }

    string LoadConfigFromFile(string fileName)
    {

        string config = "";

        try
        {
            using (StreamReader reader = new StreamReader(fileName))
            {
                config = reader.ReadToEnd();
            }

        }
        catch (FileNotFoundException e)
        {
            Debug.Log("Rename config_template.txt to config.txt! (" + e.Message + ")");
        }

        return config;
    }


  
    public string GetConfigText()
    {

        if (_loadedConfigFile.Length > 3)
        {
            //already have one loaded
            return _loadedConfigFile;
        }

        return "";
    }

    public string ResetConfig()
    {
        _loadedConfigFile = "";
        return _loadedConfigFile;
    }
    public void LoadConfigFile(string fileContents)
    {
        int curFriendIndex = _aiManagerScript.GetFriendIndex();

        _loadedConfigFile = fileContents;

        //process it line by line
        _friendList.Clear();

        Friend friend = null;

        using (var reader = new StringReader(fileContents))
        {
            for (string line = reader.ReadLine(); line != null; line = reader.ReadLine())
            {
                // Do something with the line
                string[] words = line.Trim().Split('|');
                if (words[0] == "set_google_api_key")
                {
                    _aiManagerScript.SetGoogleAPIKey(words[1]);
                }
                else
                if (words[0] == "set_openai_api_key")
                {
                    _aiManagerScript.SetOpenAI_APIKey(words[1]);
                }
                else if(words[0] == "set_openai_model")
                {
                    _aiManagerScript.SetOpenAI_Model(words[1]);
                }
                else
                if (words[0] == "set_elevenlabs_api_key")
                {
                    _aiManagerScript.SetElevenLabsAPIKey(words[1]);
                }
                else
                if (words[0] == "add_friend")
                {
                    //need to use scriptable object
                    friend = ScriptableObject.CreateInstance<Friend>();
                    _friendList.Add(friend);
                    friend._index = _friendList.Count - 1;

                    friend._name = words[1];
                    Debug.Log("adding friend " + friend._name);
                } 
                else if (words[0] == "set_friend_base_prompt")
                {
                    //multiline input
                    //collect all the proceeding lines until a line has only "<END_TEXT>" on it
                    string basePrompt = "";
                    for (string line2 = reader.ReadLine(); line2 != null; line2 = reader.ReadLine())
                    {
                        if (line2.Trim() == "<END_TEXT>")
                        { break; }
                        else
                        {
                            basePrompt += line2 + "\n";   
                        }
                    }

                    friend._basePrompt = basePrompt.Trim();
                }
                else if (words[0] == "set_friend_direction_prompt")
                {
                    //multiline input
                    //collect all the proceeding lines until a line has only "<END_TEXT>" on it
                    string basePrompt = "";
                    for (string line2 = reader.ReadLine(); line2 != null; line2 = reader.ReadLine())
                    {
                        if (line2.Trim().ToUpper() == "<END_TEXT>")
                        { break; }
                        else
                        {
                            basePrompt += line2 + "\n";
                        }
                    }

                    friend._directionPrompt = basePrompt.Trim();
                }
                else if (words[0] == "set_friend_advice_prompt")
                {
                    //multiline input
                    //collect all the proceeding lines until a line has only "<END_TEXT>" on it
                    string basePrompt = "";
                    for (string line2 = reader.ReadLine(); line2 != null; line2 = reader.ReadLine())
                    {
                        if (line2.Trim() == "<END_TEXT>")
                        { break; }
                        else
                        {
                            basePrompt += line2 + "\n";
                        }
                    }

                    friend._advicePrompt = basePrompt.Trim();
                } else
                if (words[0] == "set_friend_language")
                {
                    friend._language = words[1].ToLower();
                }
                else
                if (words[0] == "set_friend_token_memory")
                {
                    //convert to int with TryParse
                    int.TryParse(words[1], out friend._friendTokenMemory);
                } else
                if (words[0] == "set_friend_max_tokens_to_generate")
                {
                    //convert to int with TryParse
                    int.TryParse(words[1], out friend._maxTokensToGenerate);
                }

                else
                if (words[0] == "set_friend_voice_pitch")
                {
                    //convert to int with TryParse
                    float.TryParse(words[1], out friend._pitch);
                }
                else
                if (words[0] == "set_friend_voice_speed")
                {
                    //convert to int with TryParse
                    float.TryParse(words[1], out friend._speed);
                }
                else
                if (words[0] == "set_friend_google_voice")
                {
                    //convert to int with TryParse
                    friend._googleVoice = words[1];
                }
                else
                if (words[0] == "set_friend_elevenlabs_voice")
                {
                    //convert to int with TryParse
                    friend._elevelLabsVoice = words[1];
                }
                else
                if (words[0] == "set_friend_visual")
                {
                    //convert to int with TryParse
                    friend._visual = words[1];
                }
                else
                if (words[0] == "set_friend_temperature")
                {
                    //convert to int with TryParse
                    float.TryParse(words[1], out friend._temperature);
                }
                else
                if (words[0] == "set_friend_elevenlabs_stability")
                {
                    //convert to int with TryParse
                    float.TryParse(words[1], out friend._elevenlabsStability);
                }



            }
        }

        Debug.Log("Loaded config.  A total of " + _friendList.Count + " friends laoded.");
        
        
        if (_friendList.Count == 0)
        {
            //error
            _friendList.Add(ScriptableObject.CreateInstance<Friend>()); //at least add one

        }

        if (curFriendIndex >= _friendList.Count)
        {
            //error
            curFriendIndex = 0;
        }

        _aiManagerScript.SetActiveFriend(_friendList[curFriendIndex]);
        
    }

    public Friend GetFriendByIndex(int index)
    {
        return _friendList[index];
    }

    public int GetFriendCount() 
    {
        return _friendList.Count;
    }

    

}
