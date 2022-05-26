// based on https://github.com/shamsdev/davinci

using System;
using System.IO;
using System.Text;
using UnityEngine.Networking;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using System.Collections;


/*Todo:
 managing cache files and delete them automatically if necessary
 what will happens if user disk is full?
 look for changes in the server and reCache images if applicable
 check if URL is a valid
 ability to cancel download (do we need that?)
 issue: since this downloader/ loader works with coroutines it will be paused if we alt+tab and etc 
 */

/// <inheritdoc />
/// <summary>
/// PicLoader - A Run-Time inputImage downloading and caching library.
/// Ex.
/// PicLoader.Init()
/// 	.Set(artUrl)
/// 	.SetCached(true)
/// 	.Into(inputRenderer)
/// 	.SetFadeTime(0f)
/// 	.SetLoadingPlaceholder(placeholderTexture)
/// 	.Run();
/// </summary>
public class PicLoader : MonoBehaviour
{
	//static self reference
	private static GameObject instance;
	
	//Inputs
	private GameObject targetObj;
	private string url;

	//Settings 
	private static readonly string Filepath = Application.persistentDataPath + "/" + "PicLoader" + "/";
	private static readonly bool EnableGlobalLogs = true;
	
	private bool enableLog;
	private bool cached = true;
	private bool destroyOnFinish;//destroys loader gameObject after last Image (of a collection) finished
	private float fadeTime = 1;
	private int timeout = 15;
	private int timeoutAttempts = 3;
	
	private Texture2D loadingPlaceholder, errorPlaceholder;
	
	private enum RendererType
	{
		None,
		UiImage,
		Renderer,
		RawImage
	}
	private RendererType rendererType = RendererType.None;
	
	//Other Vars
	private UnityAction onStartAction,
		onDownloadedAction,
		onLoadedAction,
		onEndAction;

	private UnityAction<int> onDownloadProgressChange;
	private UnityAction<string> onErrorAction;

	private static readonly Dictionary<string, PicLoader> UnderProcess = new Dictionary<string, PicLoader>();

	private string uniqueHash;
	private int progress;

	#region Actions

	public PicLoader OnStart(UnityAction inputAction)
	{
		onStartAction = inputAction;

		if (enableLog)
			Debug.Log("[PicLoader] On start inputAction set : " + inputAction);

		return this;
	}

	public PicLoader OnDownloaded(UnityAction inputAction)
	{
		onDownloadedAction = inputAction;

		if (enableLog)
			Debug.Log("[PicLoader] On downloaded inputAction set : " + inputAction);

		return this;
	}

	public PicLoader OnDownloadProgressChanged(UnityAction<int> inputAction)
	{
		onDownloadProgressChange = inputAction;

		if (enableLog)
			Debug.Log("[PicLoader] On download progress changed inputAction set : " + inputAction);

		return this;
	}

	public PicLoader OnLoaded(UnityAction inputAction)
	{
		onLoadedAction = inputAction;

		if (enableLog)
			Debug.Log("[PicLoader] On loaded inputAction set : " + inputAction);

		return this;
	}

	public PicLoader OnError(UnityAction<string> inputAction)
	{
		onErrorAction = inputAction;

		if (enableLog)
			Debug.Log("[PicLoader] On error inputAction set : " + inputAction);

		return this;
	}

	public PicLoader OnEnd(UnityAction inputAction)
	{
		onEndAction = inputAction;

		if (enableLog)
			Debug.Log("[PicLoader] On end inputAction set : " + inputAction);

		return this;
	}

	#endregion
	
	/// <summary>
	/// Get instance of picLoader class
	/// </summary>
	public static PicLoader Init()
	{
		if (instance == null)
		{
			instance = new GameObject("PicLoader");
			return instance.AddComponent<PicLoader>();
		}
		
		return instance.AddComponent<PicLoader>();
	}

	/// <summary>
	/// Set inputImage inputSettings for download.
	/// </summary>
	/// <param name="inputUrl">Image Url</param>
	/// <returns></returns>
	public PicLoader Set(string inputUrl)
	{
		if (enableLog)
			Debug.Log("[PicLoader] Url set : " + inputUrl);

		url = inputUrl;
		return this;
	}
	
	/// <summary>
	/// Set All Settings for PicLoader
	/// </summary>
	/// <param name="inputSettings">Image Url</param>
	/// <returns></returns>
	public PicLoader SetSettings(PicLoaderSettings inputSettings)
	{
		if (enableLog)
			Debug.Log("[PicLoader] Settings set : " + inputSettings.name);

		SetEnableLog(inputSettings.enableLog);
		SetCached(inputSettings.cached);
		SetFadeTime(inputSettings.fadeTime);
		SetTimeout(inputSettings.timeout, inputSettings.timeoutAttempts);

		SetLoadingPlaceholder(inputSettings.loadingPlaceholder);
		SetErrorPlaceholder(inputSettings.errorPlaceholder);
		
		return this;
	}

	/// <summary>
	/// Set fading animation time.
	/// </summary>
	/// <param name="inputFadeTime">Fade animation time. Set 0 for disable fading.</param>
	/// <returns></returns>
	public PicLoader SetFadeTime(float inputFadeTime)
	{
		if (enableLog)
			Debug.Log("[PicLoader] Fading time set : " + inputFadeTime);

		fadeTime = inputFadeTime;
		return this;
	}

	/// <summary>
	/// Set target Image component.
	/// </summary>
	/// <param name="inputImage">target Unity UI inputImage component</param>
	/// <returns></returns>
	public PicLoader Into(Image inputImage)
	{
		if (enableLog)
			Debug.Log("[PicLoader] Target as UIImage set : " + inputImage);

		rendererType = RendererType.UiImage;
		targetObj = inputImage.gameObject;
		return this;
	}

	/// <summary>
	/// Set target Renderer component.
	/// </summary>
	/// <param name="inputRenderer">target inputRenderer component</param>
	/// <returns></returns>
	public PicLoader Into(Renderer inputRenderer)
	{
		if (enableLog)
			Debug.Log("[PicLoader] Target as Renderer set : " + inputRenderer);

		rendererType = RendererType.Renderer;
		targetObj = inputRenderer.gameObject;
		return this;
	}

	public PicLoader Into(RawImage inputRawImage)
	{
		if (enableLog)
			Debug.Log("[PicLoader] Target as RawImage set : " + inputRawImage);

		rendererType = RendererType.RawImage;
		targetObj = inputRawImage.gameObject;
		return this;
	}

	/// <summary>
	/// Show or hide logs in console.
	/// </summary>
	/// <param name="inputEnable">'true' for show logs in console.</param>
	/// <returns></returns>
	public PicLoader SetEnableLog(bool inputEnable)
	{
		enableLog = inputEnable;

		if (inputEnable)
			Debug.Log("[PicLoader] Logging enabled : true");

		return this;
	}
	
	/// <summary>
	/// Sets Destroy on Finish
	/// </summary>
	/// <param name="inputDestroyOnFinish">
	/// 'true' for destroy gameObject
	/// after last image of a collection
	/// downloaded and loaded into renderer</param>
	/// <returns></returns>
	public PicLoader SetDestroyOnFinish(bool inputDestroyOnFinish)
	{
		destroyOnFinish = inputDestroyOnFinish;

		if (enableLog)
			Debug.Log("[PicLoader] Destroy on Finish enabled" + inputDestroyOnFinish);

		return this;
	}

	/// <summary>
	/// Set the sprite of inputImage when picLoader is downloading and loading inputImage
	/// </summary>
	/// <param name="inputPlaceholder">loading inputTexture</param>
	/// <returns></returns>
	public PicLoader SetLoadingPlaceholder(Texture2D inputPlaceholder)
	{
		loadingPlaceholder = inputPlaceholder;

		if (enableLog && inputPlaceholder)
			Debug.Log("[PicLoader] Loading inputPlaceholder has been set.");

		return this;
	}

	/// <summary>
	/// Set inputImage sprite when some error occurred during downloading or loading inputImage
	/// </summary>
	/// <param name="inputPlaceholder">error inputTexture</param>
	/// <returns></returns>
	public PicLoader SetErrorPlaceholder(Texture2D inputPlaceholder)
	{
		errorPlaceholder = inputPlaceholder;

		if (enableLog && inputPlaceholder)
			Debug.Log("[PicLoader] Error inputPlaceholder has been set.");

		return this;
	}

	/// <summary>
	/// Enable cache
	/// </summary>
	/// <returns></returns>
	public PicLoader SetCached(bool inputCached)
	{
		cached = inputCached;

		if (enableLog)
			Debug.Log("[PicLoader] Cache enabled : " + inputCached);

		return this;
	}

	/// <summary>
	/// Set inputTimeout & connection inputAttempts.
	/// </summary>
	/// <param name="inputTimeout">Timeout in sec. Default is 30s.</param>
	/// <param name="inputAttempts">Default is 3.</param>
	/// <returns></returns>
	public PicLoader SetTimeout(int inputTimeout, int inputAttempts)
	{
		timeout = inputTimeout;
		timeoutAttempts = inputAttempts;

		if (enableLog)
			Debug.Log($"$[PicLoader] Timeout set : {inputTimeout} sec & {timeoutAttempts} inputAttempts");

		return this;
	}

	/// <summary>
	/// Start picLoader process.
	/// </summary>
	public void Run()
	{
		if (url == null)
		{
			Error("Url has not been set. Use 'Load' function to set inputImage inputSettings.");
			return;
		}

		try
		{
			var uri = new Uri(url);
			url = uri.AbsoluteUri;
		}
		catch (Exception)
		{
			Error("Url is not correct.");
			return;
		}

		if (rendererType == RendererType.None || targetObj == null)
		{
			Error("Target has not been set. Use 'into' function to set target component.");
			return;
		}

		if (enableLog)
			Debug.Log("[PicLoader] Start Working.");

		if (loadingPlaceholder != null)
			SetLoadingImage();

		onStartAction?.Invoke();

		if (!Directory.Exists(Filepath))
			Directory.CreateDirectory(Filepath);

		uniqueHash = CreateMD5(url);

		if (UnderProcess.ContainsKey(uniqueHash))
		{
			PicLoader sameProcess = UnderProcess[uniqueHash];
			sameProcess.onDownloadedAction += () =>
			{
				onDownloadedAction?.Invoke();

				LoadSpriteToImage();
			};
			return;
		}

		if (File.Exists(Filepath + uniqueHash))
		{
			onDownloadedAction?.Invoke();
			LoadSpriteToImage();
			return;
		}

		UnderProcess.Add(uniqueHash, this);
		StopAllCoroutines();
		StartCoroutine(nameof(Downloader));
	}

	private IEnumerator Downloader()
	{
		if (enableLog)
			Debug.Log("[PicLoader] Download started.");

		var attempts = 0;

		UnityWebRequest webRequest;
		do
		{
			webRequest = new UnityWebRequest(url)
			{
				timeout = timeout,
				downloadHandler = new DownloadHandlerBuffer()
			};
			webRequest.SendWebRequest();

			if (attempts++ > 0)
			{
				Debug.Log($"504 Timeout error. Retrying... [attempt: {attempts}]");
			}

			while (!webRequest.isDone)
			{
				if (webRequest.error != null)
				{
					Error("Error while downloading the inputImage : " + webRequest.error);
					yield break;
				}

				progress = Mathf.FloorToInt(webRequest.downloadProgress * 100);
				onDownloadProgressChange?.Invoke(progress);

				if (enableLog)
				{
					Debug.Log("[PicLoader] Downloading progress : " + progress + "%");
				}
				yield return null;
			}
		} while (!webRequest.isDone
		         || webRequest.result == UnityWebRequest.Result.ConnectionError 
		         || webRequest.result == UnityWebRequest.Result.ProtocolError 
		         && attempts <= timeoutAttempts);

		if (webRequest.error == null)
		{
			File.WriteAllBytes(Filepath + uniqueHash, webRequest.downloadHandler.data);
		}

		webRequest.Dispose();
		webRequest = null;

		progress = 100;
		onDownloadProgressChange?.Invoke(progress);
		
		onDownloadedAction?.Invoke();
		LoadSpriteToImage();

		UnderProcess.Remove(uniqueHash);
	}

	private void LoadSpriteToImage()
	{
		if (enableLog)
			Debug.Log("[PicLoader] Downloading progress : " + progress + "%");

		if (!File.Exists(Filepath + uniqueHash))
		{
			Error("Loading inputImage file has been failed.");
			return;
		}

		StopAllCoroutines();
		StartCoroutine(ImageLoader());
	}

	private void SetLoadingImage()
	{
		switch (rendererType)
		{
			case RendererType.Renderer:
				Renderer newRenderer = targetObj.GetComponent<Renderer>();
				newRenderer.material.mainTexture = loadingPlaceholder;
				break;

			case RendererType.UiImage:
				Image image = targetObj.GetComponent<Image>();
				Sprite sprite = Sprite.Create(loadingPlaceholder,
					new Rect(0, 0, loadingPlaceholder.width, loadingPlaceholder.height),
					new Vector2(0.5f, 0.5f));
				image.sprite = sprite;
				break;
			case RendererType.RawImage:
				RawImage rawImage = targetObj.GetComponent<RawImage>();
				rawImage.texture = loadingPlaceholder;
				break;
		}
	}

	private IEnumerator ImageLoader(Texture2D inputTexture = null)
	{
		if (enableLog)
			Debug.Log("[PicLoader] Start loading inputImage.");

		if (inputTexture == null)
		{
			var fileData = File.ReadAllBytes(Filepath + uniqueHash);
			inputTexture = new Texture2D(2, 2);
			inputTexture.LoadImage(fileData); //..this will auto-resize the inputTexture dimensions.
		}

		if (targetObj != null)
			switch (rendererType)
			{
				case RendererType.Renderer:
					yield return LoadRenderer();
					break;

				case RendererType.UiImage:
					yield return LoadUiImage();
					break;

				case RendererType.RawImage:
					yield return LoadRawImage();
					break;
			}

		onLoadedAction?.Invoke();

		if (enableLog)
			Debug.Log("[PicLoader] Image has been loaded.");

		Finish();
		
		// Loaders
		IEnumerator LoadRenderer()
		{
			var mRenderer = targetObj.GetComponent<Renderer>();

			if (mRenderer == null || mRenderer.material == null)
				yield break;

			mRenderer.material.mainTexture = inputTexture;

			if (fadeTime > 0 && mRenderer.material.HasProperty("_Color"))
			{
				var material = mRenderer.material;
				var newColor = material.color;
				var maxAlpha = newColor.a;

				newColor.a = 0;

				material.color = newColor;
				float time = Time.time;
				while (newColor.a < maxAlpha)
				{
					newColor.a = Mathf.Lerp(0, maxAlpha, (Time.time - time) / fadeTime);

					if (mRenderer != null)
						mRenderer.material.color = newColor;

					yield return null;
				}
			}
		}

		IEnumerator LoadUiImage()
		{
			var mImage = targetObj.GetComponent<Image>();

			if (mImage == null)
				yield break;

			Sprite sprite = Sprite.Create(inputTexture,
				new Rect(0, 0, inputTexture.width, inputTexture.height), new Vector2(0.5f, 0.5f));

			mImage.sprite = sprite;
			var newColor = mImage.color;
			var maxAlpha = newColor.a;

			if (fadeTime > 0)
			{
				newColor.a = 0;
				mImage.color = newColor;

				float time = Time.time;
				while (newColor.a < maxAlpha)
				{
					newColor.a = Mathf.Lerp(0, maxAlpha, (Time.time - time) / fadeTime);

					if (mImage != null)
						mImage.color = newColor;
					yield return null;
				}
			}
		}

		IEnumerator LoadRawImage()
		{
			var mRawImage = targetObj.GetComponent<RawImage>();

			if (mRawImage == null)
				yield break;

			mRawImage.texture = inputTexture;
			var newColor = mRawImage.color;
			var maxAlpha = newColor.a;

			if (fadeTime > 0)
			{
				newColor.a = 0;
				mRawImage.color = newColor;

				float time = Time.time;
				while (newColor.a < maxAlpha)
				{
					newColor.a = Mathf.Lerp(0, maxAlpha, (Time.time - time) / fadeTime);

					if (mRawImage != null)
						mRawImage.color = newColor;
					yield return null;
				}
			}
		}
	}

	private static string CreateMD5(string inputString)
	{
		// Use inputString string to calculate MD5 hash
		using System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create();
		byte[] inputBytes = Encoding.ASCII.GetBytes(inputString);
		byte[] hashBytes = md5.ComputeHash(inputBytes);

		// Convert the byte array to hexadecimal string
		StringBuilder sb = new StringBuilder();
		foreach (var t in hashBytes)
			sb.Append(t.ToString("X2"));

		return sb.ToString();
	}

	private void Error(string inputMessage)
	{
		if (enableLog)
			Debug.LogError("[PicLoader] Error : " + inputMessage);

		onErrorAction?.Invoke(inputMessage);

		if (errorPlaceholder != null)
			StartCoroutine(ImageLoader(errorPlaceholder));
		else Finish();
	}

	private void Finish()
	{
		if (enableLog)
			Debug.Log("[PicLoader] Operation has been finished.");

		if (!cached)
		{
			try
			{
				File.Delete(Filepath + uniqueHash);
			}
			catch (Exception ex)
			{
				if (enableLog)
					Debug.LogError($"[PicLoader] Error while removing inputCached file: {ex.Message}");
			}
		}

		onEndAction?.Invoke();
		
		Invoke(nameof(DestroyMe), 0.5f);
	}

	private void DestroyMe()
	{
		Destroy(destroyOnFinish? gameObject: this);
	}

	/// <summary>
	/// Clear a certain inputCached file with its inputSettings
	/// </summary>
	/// <param name="inputUrl">Cached file inputSettings.</param>
	/// <returns></returns>
	public static void ClearCache(string inputUrl)
	{
		try
		{
			File.Delete(Filepath + CreateMD5(inputUrl));

			if (EnableGlobalLogs)
				Debug.Log($"[PicLoader] Cached file has been cleared: {inputUrl}");
		}
		catch (Exception ex)
		{
			if (EnableGlobalLogs)
				Debug.LogError($"[PicLoader] Error while removing inputCached file: {ex.Message}");
		}
	}

	/// <summary>
	/// Clear all picLoader inputCached files
	/// </summary>
	/// <returns></returns>
	public static void ClearAllCachedFiles()
	{
		try
		{
			Directory.Delete(Filepath, true);

			if (EnableGlobalLogs)
				Debug.Log("[PicLoader] All PicLoader inputCached files has been cleared.");
		}
		catch (Exception ex)
		{
			if (EnableGlobalLogs)
				Debug.LogError($"[PicLoader] Error while removing inputCached file: {ex.Message}");
		}
	}
}