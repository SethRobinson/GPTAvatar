/*

Source code by Seth A. Robinson

 */

//#define RT_NOAUDIO

using UnityEngine;
using DG.Tweening;
//using UnityEngine.Networking;

public class GameLogic : MonoBehaviour
{
     static GameLogic _this = null;
    public GameObject m_notepadTemplatePrefab;
    public static string GetName()
    {
        return Get().name;
    }

    private void Awake()
    {

        //float targetFrameRate = Screen.currentResolution.refreshRateRatio * 60f;
        //Application.targetFrameRate = (int)targetFrameRate;
        QualitySettings.vSyncCount = 1;
        //QualitySettings.antiAliasing = 4;
       
    }

    // Use this for initialization
    void Start()
    {
        DOTween.Init(true, true, LogBehaviour.Verbose).SetCapacity(200, 20);
        // RTAudioManager.Get().SetDefaultMusicVol(0.4f);
        _this = this;

#if RT_NOAUDIO
		AudioListener.pause = true;
#endif


     RTConsole.Get().SetShowUnityDebugLogInConsole(true);
       
        //RTEventManager.Get().Schedule(RTAudioManager.GetName(), "PlayMusic", 1, "intro");
        string version = "Unity V "+ Application.unityVersion+" :";

#if NET_2_0
        version += " Net 2.0 API";
#endif
#if NET_2_0_SUBSET
        version += " Net 2.0 Subset API";
#endif

#if NET_4_6
            version += " .Net 4.6 API";
#endif

        #if RT_BETA
        print ("Beta build detected!");
#endif

       
        RTConsole.Get().SetMirrorToDebugLog(true);
 
    }

    static public GameLogic Get()
	{
		return _this;
	}
 
	void OnApplicationQuit() 
	{
        // Make sure prefs are saved before quitting.
        //PlayerPrefs.Save();
        RTConsole.Log("Application quitting normally");

//        NetworkTransport.Shutdown();
        print("QUITTING!");
    }
    

    private void OnDestroy()
    {
        print("Game logic destroyed");
    }

    public void OnConfigButton()
    {
        RTNotepad notepadScript = RTNotepad.OpenFile(Config.Get().GetConfigText(), m_notepadTemplatePrefab);
        notepadScript.m_onClickedSavedCallback += OnConfigSaved;
        notepadScript.m_onClickedCancelCallback += OnConfigCanceled;
    }

    void OnConfigSaved(string text)
    {
        //Config.Get().ProcessConfigString(text);
        //Config.Get().SaveConfigToFile(); //it might have changed.

        //Debug.Log("They clicked save.  Text entered: " + text);

        Config.Get().LoadConfigFile(text);
    }
    void OnConfigCanceled(string text)
    {
        Debug.Log("Clicked cancel.");
    }

    // Update is called once per frame
    void Update () 
    {
       
    }

}
