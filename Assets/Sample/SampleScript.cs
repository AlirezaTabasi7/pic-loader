using UnityEngine;
using UnityEngine.UI;

public class SampleScript : MonoBehaviour
{

	public string imageAddress;
	public PicLoaderSettings picLoaderSettings;
	
	// Start is called before the first frame update
	void Start()
	{
		PicLoader.Init()
			.Set(imageAddress)
			.Into(GetComponent<Image>())
			.OnDownloadProgressChanged((progress) =>
			{
				GetComponent<Image>().fillAmount = progress / 100f;
			})
			.SetSettings(picLoaderSettings)
			.Run();
	}
}