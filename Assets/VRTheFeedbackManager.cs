﻿using UnityEngine;
using System.Collections;
using System.IO;
using LitJson;
using System.Net;
using System.Threading; 


public class PresignedResponseJSON
{
	public string url;
}

public class VRTheFeedbackManager : MonoBehaviour {

    private AudioSource myAudio;

	string fileName;
	string filePath;
	bool _threadRunning;
	Thread _thread;
	PresignedResponseJSON json;
	private SavWav saveWav;
	private float[] justFeedbackSamples;
	public AudioClip justFeedback;

	void Start () {
        myAudio = GetComponent<AudioSource>();
		saveWav = new SavWav ();
    }
	
    public void RecordFeedback()
    {
        myAudio.clip = Microphone.Start(null, false, 300, 44100);
    }

    public void SaveFeedback()
    {
		Microphone.End (null);
		filePath = Path.Combine (Application.persistentDataPath, "test.mp3");
		justFeedback = saveWav.TrimSilence (myAudio.clip, 0.01f);

		if (filePath != null) {
			StartCoroutine(UploadToServer ());
		}	
    }

	public IEnumerator UploadToServer() {
		string url = "https://www.vrthefeedback.com/upload/presign";
		WWW www = new WWW(url);
		yield return www;
		if (www.error == null)
		{
			Debug.Log ("Server response on presign: " + www.data);
			json = ParsePresignedResponseJSON(www.data);
			justFeedbackSamples = new float[justFeedback.samples * justFeedback.channels];
			justFeedback.GetData (justFeedbackSamples, 0);
			_thread = new Thread(FeedbackUploadThread);
			_thread.Start();	
		}
		else
		{
			Debug.Log("ERROR: " + www.error);
		}  

	}

	public void FeedbackUploadThread() {
		_threadRunning = true;
		Debug.Log("Starting upload on separate thread.");
		EncodeMP3.convert(justFeedbackSamples, filePath, 128);
		UploadObject (json.url, filePath);
		justFeedbackSamples = null;
		justFeedback = null;
		Debug.Log("Upload done..");
		_threadRunning = false;
	}

	private PresignedResponseJSON ParsePresignedResponseJSON(string jsonString)
	{
		JsonData jsonvale = JsonMapper.ToObject(jsonString);
		PresignedResponseJSON parsejson;
		parsejson = new PresignedResponseJSON();
		parsejson.url = jsonvale["url"].ToString();
		return parsejson;
	}

	static void UploadObject(string url, string filePath)
	{
		HttpWebRequest httpRequest = WebRequest.Create(url) as HttpWebRequest;
		httpRequest.Method = "PUT";
		using (Stream dataStream = httpRequest.GetRequestStream())
		{
			byte[] buffer = new byte[8000];
			using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
			{
				int bytesRead = 0;
				while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) > 0)
				{
					dataStream.Write(buffer, 0, bytesRead);
				}
			}
		}
		HttpWebResponse response = httpRequest.GetResponse() as HttpWebResponse;
	}


	void OnDisable()
	{
		if(_threadRunning)
		{
			Debug.Log("Upload still in progress - waiting to finish...");
			_threadRunning = false;
			_thread.Join();
		}
	}

}
