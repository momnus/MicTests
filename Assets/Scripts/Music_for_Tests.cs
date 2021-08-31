using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Music_for_Tests : MonoBehaviour
{
    public AK.Wwise.Event Music;
    public GameObject _camera;
       // Start is called before the first frame update
    public void StartMusic()
    {
        Music.Post(_camera);
    }

    // Update is called once per frame
    public void MusicStop()
    {
        Music.Stop(_camera);
    }
}
